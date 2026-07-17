const { getAuth } = require("firebase-admin/auth");
const { FieldValue, getFirestore } = require("firebase-admin/firestore");
const {
  DEFAULT_RATING,
  createMatch,
  eloChange,
  friendCodeForUid,
  processAction,
  rankForRating,
  sanitizeTeam,
} = require("./social-pvp");

const MAX_LIST_ITEMS = 50;
const QUEUE_TTL_MS = 2 * 60 * 1000;
const WORLD_MAX_PLAYERS = 5;
const WORLD_STALE_MS = 45 * 1000;
const WORLD_CHAT_RANGE = 12;
const WORLD_BATTLE_RANGE = 6;
const WORLD_CHAT_MAX_LENGTH = 80;

function sendJson(response, status, body) {
  response.status(status).set("Cache-Control", "no-store").json(body);
}

async function authenticate(request) {
  const authorization = request.get("authorization") || "";
  if (!authorization.startsWith("Bearer ")) throw new Error("unauthenticated");
  return getAuth().verifyIdToken(authorization.slice(7), true);
}

function safeText(value, fallback, maxLength = 40) {
  const text = String(value || fallback || "").trim();
  return text.slice(0, maxLength) || fallback;
}

function blockDocId(uid1, uid2) {
  return [uid1, uid2].sort().join("_");
}

async function isBlockedEitherDirection(db, uid1, uid2) {
  if (!uid1 || !uid2 || uid1 === uid2) return false;
  const snapshot = await db.collection("socialBlocks").doc(blockDocId(uid1, uid2)).get();
  return snapshot.exists;
}

async function blockedUidSet(db, uid) {
  const snapshot = await db.collection("socialBlocks").where("uids", "array-contains", uid)
    .limit(MAX_LIST_ITEMS).get();
  const blocked = new Set();
  snapshot.docs.forEach((doc) => {
    const uids = Array.isArray(doc.get("uids")) ? doc.get("uids") : [];
    uids.forEach((value) => { if (value && value !== uid) blocked.add(value); });
  });
  return blocked;
}

function distanceSquared(a = {}, b = {}) {
  const dx = (Number(a.x) || 0) - (Number(b.x) || 0);
  const dy = (Number(a.y) || 0) - (Number(b.y) || 0);
  const dz = (Number(a.z) || 0) - (Number(b.z) || 0);
  return dx * dx + dy * dy + dz * dz;
}

function publicWorldPlayer(uid, data = {}, blocked = false) {
  return {
    uid,
    displayName: safeText(data.displayName, "탐험가"),
    level: Math.max(1, Math.round(Number(data.level) || 1)),
    x: Number(data.x) || 0,
    y: Number(data.y) || 0,
    z: Number(data.z) || 0,
    facing: Number(data.facing) || 0,
    joinedAtMs: Number(data.joinedAtMs) || 0,
    lastSeenAtMs: Number(data.lastSeenAtMs) || 0,
    blocked,
  };
}

function publicWorld(id, data = {}, players = []) {
  return {
    worldId: id,
    displayName: safeText(data.displayName, "탐험 필드"),
    playerCount: players.length || Math.max(0, Math.round(Number(data.memberCount) || 0)),
    maxPlayers: WORLD_MAX_PLAYERS,
    players,
  };
}

function publicProfile(uid, data = {}) {
  const rating = Number.isFinite(data.rating) ? Math.round(data.rating) : DEFAULT_RATING;
  return {
    uid,
    displayName: safeText(data.displayName, "탐험가"),
    friendCode: safeText(data.friendCode, friendCodeForUid(uid), 12),
    level: Math.max(1, Math.round(Number(data.level) || 1)),
    rating,
    rank: rankForRating(rating),
    wins: Math.max(0, Math.round(Number(data.wins) || 0)),
    losses: Math.max(0, Math.round(Number(data.losses) || 0)),
    team: Array.isArray(data.team) ? data.team : [],
    activeMatchId: safeText(data.activeMatchId, "", 100),
  };
}

function plainMatch(data = {}) {
  return {
    matchId: data.matchId || "",
    mode: data.mode || "friendly",
    status: data.status || "active",
    player1: data.player1 || {},
    player2: data.player2 || {},
    team1: data.team1 || [],
    team2: data.team2 || [],
    active1: data.active1 || 0,
    active2: data.active2 || 0,
    turnUid: data.turnUid || "",
    turnNumber: data.turnNumber || 1,
    cooldowns1: data.cooldowns1 || [],
    cooldowns2: data.cooldowns2 || [],
    log: data.log || [],
    winnerUid: data.winnerUid || "",
    createdAtMs: data.createdAtMs || 0,
    updatedAtMs: data.updatedAtMs || 0,
  };
}

function requireTeam(profileData) {
  return sanitizeTeam(profileData && profileData.team);
}

async function buildSocialState(db, uid) {
  const profileRef = db.collection("socialProfiles").doc(uid);
  const [profileSnap, friendsSnap, requestsSnap, challengesSnap, queueSnap, blocksSnap] = await Promise.all([
    profileRef.get(),
    profileRef.collection("friends").limit(MAX_LIST_ITEMS).get(),
    db.collection("friendRequests").where("toUid", "==", uid).limit(MAX_LIST_ITEMS).get(),
    db.collection("pvpChallenges").where("toUid", "==", uid).limit(MAX_LIST_ITEMS).get(),
    db.collection("pvpQueue").doc(uid).get(),
    db.collection("socialBlocks").where("uids", "array-contains", uid).limit(MAX_LIST_ITEMS).get(),
  ]);
  const blockedIds = new Set();
  blocksSnap.docs.forEach((doc) => {
    const uids = Array.isArray(doc.get("uids")) ? doc.get("uids") : [];
    uids.forEach((value) => { if (value && value !== uid) blockedIds.add(value); });
  });
  const profile = publicProfile(uid, profileSnap.exists ? profileSnap.data() : {});
  const friends = friendsSnap.docs
    .filter((doc) => !blockedIds.has(doc.id))
    .map((doc) => publicProfile(doc.id, doc.data()));
  const incomingRequests = requestsSnap.docs
    .filter((doc) => doc.get("status") === "pending" && !blockedIds.has(doc.get("fromUid")))
    .map((doc) => ({ requestId: doc.id, ...publicProfile(doc.get("fromUid"), doc.data()) }));
  const incomingChallenges = challengesSnap.docs
    .filter((doc) => doc.get("status") === "pending" && !blockedIds.has(doc.get("fromUid")))
    .map((doc) => ({ challengeId: doc.id, ...publicProfile(doc.get("fromUid"), doc.data()) }));
  const blockedProfiles = await Promise.all([...blockedIds].map(async (blockedUid) => {
    const snap = await db.collection("socialProfiles").doc(blockedUid).get();
    return publicProfile(blockedUid, snap.exists ? snap.data() : {});
  }));
  const queueStartedAtMs = queueSnap.exists ? Number(queueSnap.get("createdAtMs")) || 0 : 0;
  const queueActive = queueSnap.exists && Date.now() - queueStartedAtMs <= QUEUE_TTL_MS;
  if (queueSnap.exists && !queueActive) await queueSnap.ref.delete();
  return {
    success: true,
    profile,
    friends,
    incomingRequests,
    incomingChallenges,
    blockedUsers: blockedProfiles,
    queued: queueActive,
    queueStartedAtMs: queueActive ? queueStartedAtMs : 0,
  };
}

async function syncProfile(db, decoded, body) {
  const uid = decoded.uid;
  const profileRef = db.collection("socialProfiles").doc(uid);
  const snapshot = await profileRef.get();
  const current = snapshot.exists ? snapshot.data() : {};
  const submittedTeam = Array.isArray(body.team) ? body.team : [];
  const team = submittedTeam.length === 0 ? [] : sanitizeTeam(submittedTeam);
  const rating = Number.isFinite(current.rating) ? current.rating : DEFAULT_RATING;
  const profile = {
    displayName: safeText(body.displayName, decoded.name || "탐험가"),
    friendCode: current.friendCode || friendCodeForUid(uid),
    level: Math.max(1, Math.min(100, Math.round(Number(body.level) || 1))),
    rating,
    rank: rankForRating(rating),
    wins: Math.max(0, Math.round(Number(current.wins) || 0)),
    losses: Math.max(0, Math.round(Number(current.losses) || 0)),
    team,
    updatedAt: FieldValue.serverTimestamp(),
  };
  await profileRef.set(profile, { merge: true });
  return { success: true, profile: publicProfile(uid, { ...current, ...profile }) };
}

async function sendFriendRequest(db, uid, body) {
  const friendCode = safeText(body.friendCode, "", 12).toUpperCase();
  if (!friendCode) throw new Error("friend_code_required");
  const targetQuery = await db.collection("socialProfiles")
    .where("friendCode", "==", friendCode).limit(1).get();
  if (targetQuery.empty) throw new Error("friend_not_found");
  const target = targetQuery.docs[0];
  if (target.id === uid) throw new Error("cannot_add_self");
  if (await isBlockedEitherDirection(db, uid, target.id)) throw new Error("user_blocked");
  const ownRef = db.collection("socialProfiles").doc(uid);
  const ownFriendRef = ownRef.collection("friends").doc(target.id);
  const reverseFriendRef = target.ref.collection("friends").doc(uid);
  const requestRef = db.collection("friendRequests").doc(`${target.id}_${uid}`);
  const reverseRequestRef = db.collection("friendRequests").doc(`${uid}_${target.id}`);

  await db.runTransaction(async (transaction) => {
    const [ownSnap, existingFriend, reverseRequest] = await Promise.all([
      transaction.get(ownRef),
      transaction.get(ownFriendRef),
      transaction.get(reverseRequestRef),
    ]);
    if (!ownSnap.exists) throw new Error("profile_not_synced");
    if (existingFriend.exists) throw new Error("already_friends");
    if (reverseRequest.exists && reverseRequest.get("status") === "pending") {
      const own = publicProfile(uid, ownSnap.data());
      const other = publicProfile(target.id, target.data());
      transaction.set(ownFriendRef, { ...other, addedAt: FieldValue.serverTimestamp() });
      transaction.set(reverseFriendRef, { ...own, addedAt: FieldValue.serverTimestamp() });
      transaction.update(reverseRequestRef, { status: "accepted", respondedAt: FieldValue.serverTimestamp() });
      return;
    }
    const own = publicProfile(uid, ownSnap.data());
    transaction.set(requestRef, {
      fromUid: uid,
      toUid: target.id,
      displayName: own.displayName,
      friendCode: own.friendCode,
      level: own.level,
      rating: own.rating,
      rank: own.rank,
      status: "pending",
      createdAt: FieldValue.serverTimestamp(),
    }, { merge: true });
  });
  return { success: true };
}

async function respondFriendRequest(db, uid, body) {
  const requestRef = db.collection("friendRequests").doc(safeText(body.requestId, "", 180));
  const accept = Boolean(body.accept);
  await db.runTransaction(async (transaction) => {
    const requestSnap = await transaction.get(requestRef);
    if (!requestSnap.exists || requestSnap.get("toUid") !== uid
        || requestSnap.get("status") !== "pending") throw new Error("request_not_pending");
    const otherUid = requestSnap.get("fromUid");
    const ownRef = db.collection("socialProfiles").doc(uid);
    const otherRef = db.collection("socialProfiles").doc(otherUid);
    const [ownSnap, otherSnap] = await Promise.all([transaction.get(ownRef), transaction.get(otherRef)]);
    if (!ownSnap.exists || !otherSnap.exists) throw new Error("profile_not_synced");
    if (accept) {
      transaction.set(ownRef.collection("friends").doc(otherUid), {
        ...publicProfile(otherUid, otherSnap.data()), addedAt: FieldValue.serverTimestamp(),
      });
      transaction.set(otherRef.collection("friends").doc(uid), {
        ...publicProfile(uid, ownSnap.data()), addedAt: FieldValue.serverTimestamp(),
      });
    }
    transaction.update(requestRef, {
      status: accept ? "accepted" : "rejected",
      respondedAt: FieldValue.serverTimestamp(),
    });
  });
  return { success: true };
}

async function removeFriend(db, uid, body) {
  const friendUid = safeText(body.friendUid, "", 128);
  if (!friendUid) throw new Error("friend_uid_required");
  await Promise.all([
    db.collection("socialProfiles").doc(uid).collection("friends").doc(friendUid).delete(),
    db.collection("socialProfiles").doc(friendUid).collection("friends").doc(uid).delete(),
  ]);
  return { success: true };
}

async function queueRanked(db, uid) {
  const profileRef = db.collection("socialProfiles").doc(uid);
  const profileSnap = await profileRef.get();
  if (!profileSnap.exists) throw new Error("profile_not_synced");
  const profile = publicProfile(uid, profileSnap.data());
  profile.team = requireTeam(profileSnap.data());
  if (profile.activeMatchId) return { success: true, queued: false, matchId: profile.activeMatchId };

  const queueRef = db.collection("pvpQueue").doc(uid);
  await queueRef.set({ uid, rating: profile.rating, mode: "ranked", createdAtMs: Date.now() });
  const candidates = await db.collection("pvpQueue").where("mode", "==", "ranked").limit(30).get();
  const now = Date.now();
  const candidateDoc = candidates.docs
    .filter((doc) => doc.id !== uid
      && now - (Number(doc.get("createdAtMs")) || 0) <= QUEUE_TTL_MS
      && Math.abs((Number(doc.get("rating")) || DEFAULT_RATING) - profile.rating) <= 350)
    .sort((a, b) => (Number(a.get("createdAtMs")) || 0) - (Number(b.get("createdAtMs")) || 0))[0];
  if (!candidateDoc) return { success: true, queued: true, matchId: "" };

  const candidateUid = candidateDoc.id;
  const candidateProfileRef = db.collection("socialProfiles").doc(candidateUid);
  const candidateQueueRef = db.collection("pvpQueue").doc(candidateUid);
  const matchRef = db.collection("pvpMatches").doc();
  try {
    let match;
    await db.runTransaction(async (transaction) => {
      const [ownQueue, otherQueue, ownProfile, otherProfile] = await Promise.all([
        transaction.get(queueRef), transaction.get(candidateQueueRef),
        transaction.get(profileRef), transaction.get(candidateProfileRef),
      ]);
      if (!ownQueue.exists || !otherQueue.exists) throw new Error("queue_changed");
      if (!ownProfile.exists || !otherProfile.exists) throw new Error("profile_not_synced");
      if (ownProfile.get("activeMatchId") || otherProfile.get("activeMatchId")) throw new Error("already_in_match");
      const player1 = publicProfile(candidateUid, otherProfile.data());
      player1.team = requireTeam(otherProfile.data());
      const player2 = publicProfile(uid, ownProfile.data());
      player2.team = requireTeam(ownProfile.data());
      match = createMatch({ id: matchRef.id, mode: "ranked", player1, player2 });
      transaction.create(matchRef, { ...match, processedActionIds: [], ratingApplied: false });
      transaction.delete(queueRef);
      transaction.delete(candidateQueueRef);
      transaction.set(profileRef, { activeMatchId: matchRef.id }, { merge: true });
      transaction.set(candidateProfileRef, { activeMatchId: matchRef.id }, { merge: true });
    });
    return { success: true, queued: false, matchId: matchRef.id, match: plainMatch(match) };
  } catch (error) {
    if (error.message !== "queue_changed" && error.message !== "already_in_match") throw error;
    const fresh = await profileRef.get();
    const activeMatchId = fresh.exists ? fresh.get("activeMatchId") || "" : "";
    return { success: true, queued: !activeMatchId, matchId: activeMatchId };
  }
}

async function cancelQueue(db, uid) {
  await db.collection("pvpQueue").doc(uid).delete();
  return { success: true, queued: false };
}

async function challengeFriend(db, uid, body) {
  const friendUid = safeText(body.friendUid, "", 128);
  if (!friendUid || friendUid === uid) throw new Error("invalid_friend");
  if (await isBlockedEitherDirection(db, uid, friendUid)) throw new Error("user_blocked");
  const ownRef = db.collection("socialProfiles").doc(uid);
  const friendRef = db.collection("socialProfiles").doc(friendUid);
  const [ownSnap, friendSnap, relationSnap] = await Promise.all([
    ownRef.get(), friendRef.get(), ownRef.collection("friends").doc(friendUid).get(),
  ]);
  if (!ownSnap.exists || !friendSnap.exists) throw new Error("profile_not_synced");
  if (!relationSnap.exists) throw new Error("not_friends");
  requireTeam(ownSnap.data());
  requireTeam(friendSnap.data());
  if (ownSnap.get("activeMatchId") || friendSnap.get("activeMatchId")) throw new Error("already_in_match");
  const own = publicProfile(uid, ownSnap.data());
  const challengeRef = db.collection("pvpChallenges").doc(`${friendUid}_${uid}`);
  await challengeRef.set({
    fromUid: uid,
    toUid: friendUid,
    displayName: own.displayName,
    friendCode: own.friendCode,
    level: own.level,
    rating: own.rating,
    rank: own.rank,
    status: "pending",
    createdAt: FieldValue.serverTimestamp(),
  }, { merge: true });
  return { success: true };
}

async function respondChallenge(db, uid, body) {
  const challengeRef = db.collection("pvpChallenges").doc(safeText(body.challengeId, "", 180));
  const accept = Boolean(body.accept);
  if (!accept) {
    const snap = await challengeRef.get();
    if (!snap.exists || snap.get("toUid") !== uid || snap.get("status") !== "pending") {
      throw new Error("challenge_not_pending");
    }
    await challengeRef.update({ status: "rejected", respondedAt: FieldValue.serverTimestamp() });
    return { success: true, matchId: "" };
  }
  const matchRef = db.collection("pvpMatches").doc();
  let match;
  await db.runTransaction(async (transaction) => {
    const challengeSnap = await transaction.get(challengeRef);
    if (!challengeSnap.exists || challengeSnap.get("toUid") !== uid
        || challengeSnap.get("status") !== "pending") throw new Error("challenge_not_pending");
    const otherUid = challengeSnap.get("fromUid");
    const ownRef = db.collection("socialProfiles").doc(uid);
    const otherRef = db.collection("socialProfiles").doc(otherUid);
    const [ownSnap, otherSnap] = await Promise.all([transaction.get(ownRef), transaction.get(otherRef)]);
    if (!ownSnap.exists || !otherSnap.exists) throw new Error("profile_not_synced");
    if (ownSnap.get("activeMatchId") || otherSnap.get("activeMatchId")) throw new Error("already_in_match");
    const player1 = publicProfile(otherUid, otherSnap.data());
    player1.team = requireTeam(otherSnap.data());
    const player2 = publicProfile(uid, ownSnap.data());
    player2.team = requireTeam(ownSnap.data());
    match = createMatch({ id: matchRef.id, mode: "friendly", player1, player2 });
    transaction.create(matchRef, { ...match, processedActionIds: [], ratingApplied: true });
    transaction.set(ownRef, { activeMatchId: matchRef.id }, { merge: true });
    transaction.set(otherRef, { activeMatchId: matchRef.id }, { merge: true });
    transaction.update(challengeRef, { status: "accepted", respondedAt: FieldValue.serverTimestamp() });
  });
  return { success: true, matchId: matchRef.id, match: plainMatch(match) };
}

async function getMatch(db, uid, body) {
  let matchId = safeText(body.matchId, "", 100);
  if (!matchId) {
    const profile = await db.collection("socialProfiles").doc(uid).get();
    matchId = profile.exists ? profile.get("activeMatchId") || "" : "";
  }
  if (!matchId) return { success: true, match: null };
  const snapshot = await db.collection("pvpMatches").doc(matchId).get();
  if (!snapshot.exists) return { success: true, match: null };
  const data = snapshot.data();
  if (data.player1.uid !== uid && data.player2.uid !== uid) throw new Error("not_match_player");
  return { success: true, match: plainMatch(data) };
}

async function battleAction(db, uid, body) {
  const matchId = safeText(body.matchId, "", 100);
  const actionId = safeText(body.clientActionId, "", 100);
  if (!matchId || !actionId) throw new Error("action_identity_required");
  const matchRef = db.collection("pvpMatches").doc(matchId);
  let result;
  await db.runTransaction(async (transaction) => {
    const matchSnap = await transaction.get(matchRef);
    if (!matchSnap.exists) throw new Error("match_not_found");
    const match = matchSnap.data();
    if (match.player1.uid !== uid && match.player2.uid !== uid) throw new Error("not_match_player");
    const processed = Array.isArray(match.processedActionIds) ? match.processedActionIds : [];
    if (processed.includes(actionId)) {
      result = match;
      return;
    }
    const wasActive = match.status === "active";
    processAction(match, uid, {
      type: safeText(body.actionType, "basic", 20),
      skillIndex: Number(body.skillIndex) || 0,
      slot: Number(body.slot) || 0,
    });
    match.processedActionIds = [...processed, actionId].slice(-20);

    const p1Ref = db.collection("socialProfiles").doc(match.player1.uid);
    const p2Ref = db.collection("socialProfiles").doc(match.player2.uid);
    let p1Snap;
    let p2Snap;
    if (wasActive && match.status === "finished") {
      [p1Snap, p2Snap] = await Promise.all([transaction.get(p1Ref), transaction.get(p2Ref)]);
    }
    transaction.set(matchRef, match, { merge: false });
    if (wasActive && match.status === "finished") {
      transaction.set(p1Ref, { activeMatchId: FieldValue.delete() }, { merge: true });
      transaction.set(p2Ref, { activeMatchId: FieldValue.delete() }, { merge: true });
      if (match.mode === "ranked" && !match.ratingApplied && p1Snap.exists && p2Snap.exists) {
        const p1Rating = Number(p1Snap.get("rating")) || DEFAULT_RATING;
        const p2Rating = Number(p2Snap.get("rating")) || DEFAULT_RATING;
        const p1Won = match.winnerUid === match.player1.uid;
        const winnerRating = p1Won ? p1Rating : p2Rating;
        const loserRating = p1Won ? p2Rating : p1Rating;
        const delta = eloChange(winnerRating, loserRating);
        const p1New = Math.max(0, p1Rating + (p1Won ? delta : -delta));
        const p2New = Math.max(0, p2Rating + (p1Won ? -delta : delta));
        transaction.set(p1Ref, {
          rating: p1New, rank: rankForRating(p1New),
          wins: (Number(p1Snap.get("wins")) || 0) + (p1Won ? 1 : 0),
          losses: (Number(p1Snap.get("losses")) || 0) + (p1Won ? 0 : 1),
        }, { merge: true });
        transaction.set(p2Ref, {
          rating: p2New, rank: rankForRating(p2New),
          wins: (Number(p2Snap.get("wins")) || 0) + (p1Won ? 0 : 1),
          losses: (Number(p2Snap.get("losses")) || 0) + (p1Won ? 1 : 0),
        }, { merge: true });
        match.ratingApplied = true;
        match.ratingDelta = delta;
        transaction.set(matchRef, { ratingApplied: true, ratingDelta: delta }, { merge: true });
      }
    }
    result = match;
  });
  return { success: true, match: plainMatch(result) };
}

async function cleanupStaleWorldPlayers(db, worldId) {
  if (!worldId) return;
  const cutoff = Date.now() - WORLD_STALE_MS;
  const worldRef = db.collection("worlds").doc(worldId);
  const stale = await worldRef.collection("players").where("lastSeenAtMs", "<", cutoff).limit(20).get();
  if (stale.empty) return;
  await db.runTransaction(async (transaction) => {
    const worldSnap = await transaction.get(worldRef);
    const staleSnaps = await Promise.all(stale.docs.map((doc) => transaction.get(doc.ref)));
    let removed = 0;
    staleSnaps.forEach((snap) => {
      if (snap.exists && (Number(snap.get("lastSeenAtMs")) || 0) < cutoff) {
        transaction.delete(snap.ref);
        removed++;
      }
    });
    if (worldSnap.exists && removed > 0) {
      const count = Math.max(0, (Number(worldSnap.get("memberCount")) || 0) - removed);
      transaction.set(worldRef, { memberCount: count, updatedAtMs: Date.now() }, { merge: true });
    }
  });
}

async function leaveWorldForUid(db, uid, worldId) {
  if (!worldId) return;
  const worldRef = db.collection("worlds").doc(worldId);
  const playerRef = worldRef.collection("players").doc(uid);
  const profileRef = db.collection("socialProfiles").doc(uid);
  await db.runTransaction(async (transaction) => {
    const [worldSnap, playerSnap, profileSnap] = await Promise.all([
      transaction.get(worldRef), transaction.get(playerRef), transaction.get(profileRef),
    ]);
    if (playerSnap.exists) transaction.delete(playerRef);
    if (worldSnap.exists && playerSnap.exists) {
      const count = Math.max(0, (Number(worldSnap.get("memberCount")) || 0) - 1);
      transaction.set(worldRef, { memberCount: count, updatedAtMs: Date.now() }, { merge: true });
    }
    if (profileSnap.exists && profileSnap.get("activeWorldId") === worldId) {
      transaction.set(profileRef, { activeWorldId: FieldValue.delete() }, { merge: true });
    }
  });
}

async function worldState(db, uid, worldId) {
  if (!worldId) return { success: true, world: null, messages: [], invites: [] };
  await cleanupStaleWorldPlayers(db, worldId);
  const worldRef = db.collection("worlds").doc(worldId);
  const [worldSnap, playersSnap, messagesSnap, invitesSnap, blocked] = await Promise.all([
    worldRef.get(),
    worldRef.collection("players").limit(WORLD_MAX_PLAYERS + 2).get(),
    worldRef.collection("messages").orderBy("sentAtMs", "desc").limit(30).get(),
    db.collection("worldInvites").where("toUid", "==", uid).limit(20).get(),
    blockedUidSet(db, uid),
  ]);
  if (!worldSnap.exists) return { success: true, world: null, messages: [], invites: [] };
  const cutoff = Date.now() - WORLD_STALE_MS;
  const players = playersSnap.docs
    .filter((doc) => (Number(doc.get("lastSeenAtMs")) || 0) >= cutoff)
    .map((doc) => publicWorldPlayer(doc.id, doc.data(), blocked.has(doc.id)));
  const messages = messagesSnap.docs
    .filter((doc) => !blocked.has(doc.get("fromUid")))
    .filter((doc) => doc.get("fromUid") === uid || doc.get("toUid") === uid)
    .reverse()
    .map((doc) => ({
      messageId: doc.id,
      fromUid: doc.get("fromUid") || "",
      toUid: doc.get("toUid") || "",
      displayName: safeText(doc.get("displayName"), "탐험가"),
      message: safeText(doc.get("message"), "", WORLD_CHAT_MAX_LENGTH),
      sentAtMs: Number(doc.get("sentAtMs")) || 0,
    }));
  const invites = invitesSnap.docs
    .filter((doc) => doc.get("status") === "pending" && !blocked.has(doc.get("fromUid")))
    .map((doc) => ({
      inviteId: doc.id,
      fromUid: doc.get("fromUid") || "",
      displayName: safeText(doc.get("displayName"), "탐험가"),
      worldId: doc.get("worldId") || "",
      worldName: safeText(doc.get("worldName"), "탐험 필드"),
      createdAtMs: Number(doc.get("createdAtMs")) || 0,
    }));
  return { success: true, world: publicWorld(worldId, worldSnap.data(), players), messages, invites };
}

async function listWorlds(db, uid) {
  const [snapshot, invitesSnap, blocked] = await Promise.all([
    db.collection("worlds").limit(30).get(),
    db.collection("worldInvites").where("toUid", "==", uid).limit(20).get(),
    blockedUidSet(db, uid),
  ]);
  const worlds = snapshot.docs
    .map((doc) => publicWorld(doc.id, doc.data()))
    .filter((world) => world.playerCount > 0 && world.playerCount <= WORLD_MAX_PLAYERS)
    .sort((a, b) => a.playerCount - b.playerCount);
  const invites = invitesSnap.docs
    .filter((doc) => doc.get("status") === "pending" && !blocked.has(doc.get("fromUid")))
    .map((doc) => ({
      inviteId: doc.id,
      fromUid: doc.get("fromUid") || "",
      displayName: safeText(doc.get("displayName"), "탐험가"),
      worldId: doc.get("worldId") || "",
      worldName: safeText(doc.get("worldName"), "탐험 필드"),
      createdAtMs: Number(doc.get("createdAtMs")) || 0,
    }));
  return { success: true, worlds, invites };
}

async function joinWorld(db, decoded, body) {
  const uid = decoded.uid;
  const profileRef = db.collection("socialProfiles").doc(uid);
  const profileSnap = await profileRef.get();
  const profileData = profileSnap.exists ? profileSnap.data() : {};
  const previousWorldId = profileData.activeWorldId || "";
  if (previousWorldId) await leaveWorldForUid(db, uid, previousWorldId);

  let worldId = safeText(body.worldId, "", 80);
  if (!worldId) {
    const available = await db.collection("worlds").where("memberCount", "<", WORLD_MAX_PLAYERS).limit(10).get();
    const candidate = available.docs.find((doc) => (Number(doc.get("memberCount")) || 0) < WORLD_MAX_PLAYERS);
    worldId = candidate ? candidate.id : `field_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`;
  }
  await cleanupStaleWorldPlayers(db, worldId);

  const worldRef = db.collection("worlds").doc(worldId);
  const playerRef = worldRef.collection("players").doc(uid);
  const now = Date.now();
  await db.runTransaction(async (transaction) => {
    const [worldSnap, playerSnap] = await Promise.all([
      transaction.get(worldRef), transaction.get(playerRef),
    ]);
    const count = worldSnap.exists ? Math.max(0, Number(worldSnap.get("memberCount")) || 0) : 0;
    if (!playerSnap.exists && count >= WORLD_MAX_PLAYERS) throw new Error("world_full");
    if (!worldSnap.exists) {
      transaction.create(worldRef, {
        displayName: `탐험 필드 ${worldId.slice(-4).toUpperCase()}`,
        memberCount: 1,
        maxPlayers: WORLD_MAX_PLAYERS,
        createdAtMs: now,
        updatedAtMs: now,
      });
    } else {
      transaction.set(worldRef, {
        memberCount: playerSnap.exists ? count : count + 1,
        maxPlayers: WORLD_MAX_PLAYERS,
        updatedAtMs: now,
      }, { merge: true });
    }
    transaction.set(playerRef, {
      uid,
      displayName: safeText(profileData.displayName, decoded.name || body.displayName || "탐험가"),
      level: Math.max(1, Math.round(Number(body.level) || Number(profileData.level) || 1)),
      x: Number(body.x) || 0,
      y: Number(body.y) || 0,
      z: Number(body.z) || 0,
      facing: Number(body.facing) || 0,
      joinedAtMs: playerSnap.exists ? Number(playerSnap.get("joinedAtMs")) || now : now,
      lastSeenAtMs: now,
    }, { merge: true });
    transaction.set(profileRef, { activeWorldId: worldId, updatedAt: FieldValue.serverTimestamp() }, { merge: true });
  });
  return worldState(db, uid, worldId);
}

async function leaveWorld(db, uid, body) {
  const profileSnap = await db.collection("socialProfiles").doc(uid).get();
  const worldId = safeText(body.worldId, profileSnap.exists ? profileSnap.get("activeWorldId") || "" : "", 80);
  await leaveWorldForUid(db, uid, worldId);
  return { success: true };
}

async function syncWorld(db, uid, body) {
  const profileSnap = await db.collection("socialProfiles").doc(uid).get();
  const worldId = safeText(body.worldId, profileSnap.exists ? profileSnap.get("activeWorldId") || "" : "", 80);
  if (!worldId) return { success: true, world: null, messages: [], invites: [] };
  const playerRef = db.collection("worlds").doc(worldId).collection("players").doc(uid);
  const playerSnap = await playerRef.get();
  if (!playerSnap.exists) throw new Error("not_in_world");
  await playerRef.set({
    x: Number(body.x) || 0,
    y: Number(body.y) || 0,
    z: Number(body.z) || 0,
    facing: Number(body.facing) || 0,
    level: Math.max(1, Math.round(Number(body.level) || Number(playerSnap.get("level")) || 1)),
    lastSeenAtMs: Date.now(),
  }, { merge: true });
  return worldState(db, uid, worldId);
}

async function sendWorldChat(db, uid, body) {
  const message = safeText(body.message, "", WORLD_CHAT_MAX_LENGTH);
  const targetUid = safeText(body.targetUid, "", 128);
  if (!message) throw new Error("chat_message_required");
  if (!targetUid || targetUid === uid) throw new Error("invalid_world_target");
  if (await isBlockedEitherDirection(db, uid, targetUid)) throw new Error("user_blocked");
  const profileSnap = await db.collection("socialProfiles").doc(uid).get();
  const worldId = profileSnap.exists ? profileSnap.get("activeWorldId") || "" : "";
  if (!worldId) throw new Error("not_in_world");
  const worldRef = db.collection("worlds").doc(worldId);
  const [own, target] = await Promise.all([
    worldRef.collection("players").doc(uid).get(),
    worldRef.collection("players").doc(targetUid).get(),
  ]);
  if (!own.exists || !target.exists) throw new Error("player_not_nearby");
  if (distanceSquared(own.data(), target.data()) > WORLD_CHAT_RANGE * WORLD_CHAT_RANGE) {
    throw new Error("player_not_nearby");
  }
  await worldRef.collection("messages").add({
    fromUid: uid,
    toUid: targetUid,
    displayName: safeText(own.get("displayName"), "탐험가"),
    message,
    sentAtMs: Date.now(),
  });
  return { success: true };
}

async function inviteFriendToWorld(db, uid, body) {
  const friendUid = safeText(body.friendUid, "", 128);
  if (!friendUid || friendUid === uid) throw new Error("invalid_friend");
  if (await isBlockedEitherDirection(db, uid, friendUid)) throw new Error("user_blocked");
  const ownRef = db.collection("socialProfiles").doc(uid);
  const [ownSnap, relationSnap] = await Promise.all([
    ownRef.get(), ownRef.collection("friends").doc(friendUid).get(),
  ]);
  if (!ownSnap.exists || !relationSnap.exists) throw new Error("not_friends");
  const worldId = ownSnap.get("activeWorldId") || "";
  if (!worldId) throw new Error("not_in_world");
  const worldSnap = await db.collection("worlds").doc(worldId).get();
  if (!worldSnap.exists) throw new Error("world_not_found");
  const own = publicProfile(uid, ownSnap.data());
  const inviteRef = db.collection("worldInvites").doc(`${friendUid}_${uid}`);
  await inviteRef.set({
    fromUid: uid,
    toUid: friendUid,
    displayName: own.displayName,
    worldId,
    worldName: safeText(worldSnap.get("displayName"), "탐험 필드"),
    status: "pending",
    createdAtMs: Date.now(),
  }, { merge: true });
  return { success: true };
}

async function respondWorldInvite(db, uid, body) {
  const inviteRef = db.collection("worldInvites").doc(safeText(body.inviteId, "", 180));
  const inviteSnap = await inviteRef.get();
  if (!inviteSnap.exists || inviteSnap.get("toUid") !== uid || inviteSnap.get("status") !== "pending") {
    throw new Error("world_invite_not_pending");
  }
  const accept = Boolean(body.accept);
  await inviteRef.update({ status: accept ? "accepted" : "rejected", respondedAtMs: Date.now() });
  return { success: true, worldId: accept ? inviteSnap.get("worldId") || "" : "" };
}

async function blockUser(db, uid, body) {
  const targetUid = safeText(body.targetUid, "", 128);
  if (!targetUid || targetUid === uid) throw new Error("invalid_world_target");
  const [ownSnap, targetSnap] = await Promise.all([
    db.collection("socialProfiles").doc(uid).get(),
    db.collection("socialProfiles").doc(targetUid).get(),
  ]);
  if (!targetSnap.exists) throw new Error("friend_not_found");
  const ref = db.collection("socialBlocks").doc(blockDocId(uid, targetUid));
  await Promise.all([
    ref.set({
      uids: [uid, targetUid],
      blockedByUids: FieldValue.arrayUnion(uid),
      updatedAtMs: Date.now(),
    }, { merge: true }),
    db.collection("socialProfiles").doc(uid).collection("friends").doc(targetUid).delete(),
    db.collection("socialProfiles").doc(targetUid).collection("friends").doc(uid).delete(),
  ]);
  return { success: true, blockedUser: publicProfile(targetUid, targetSnap.data()) };
}

async function unblockUser(db, uid, body) {
  const targetUid = safeText(body.targetUid, "", 128);
  const ref = db.collection("socialBlocks").doc(blockDocId(uid, targetUid));
  await db.runTransaction(async (transaction) => {
    const snapshot = await transaction.get(ref);
    if (!snapshot.exists) return;
    const blockers = Array.isArray(snapshot.get("blockedByUids"))
      ? snapshot.get("blockedByUids").filter((value) => value && value !== uid)
      : [];
    if (blockers.length === 0) transaction.delete(ref);
    else transaction.set(ref, { blockedByUids: blockers, updatedAtMs: Date.now() }, { merge: true });
  });
  return { success: true };
}

async function challengeWorldPlayer(db, uid, body) {
  const targetUid = safeText(body.targetUid, "", 128);
  if (!targetUid || targetUid === uid) throw new Error("invalid_world_target");
  if (await isBlockedEitherDirection(db, uid, targetUid)) throw new Error("user_blocked");
  const ownRef = db.collection("socialProfiles").doc(uid);
  const targetRef = db.collection("socialProfiles").doc(targetUid);
  const [ownProfile, targetProfile] = await Promise.all([ownRef.get(), targetRef.get()]);
  if (!ownProfile.exists || !targetProfile.exists) throw new Error("profile_not_synced");
  const worldId = ownProfile.get("activeWorldId") || "";
  if (!worldId || targetProfile.get("activeWorldId") !== worldId) throw new Error("player_not_nearby");
  const worldRef = db.collection("worlds").doc(worldId);
  const [ownPlayer, targetPlayer] = await Promise.all([
    worldRef.collection("players").doc(uid).get(),
    worldRef.collection("players").doc(targetUid).get(),
  ]);
  if (!ownPlayer.exists || !targetPlayer.exists
      || distanceSquared(ownPlayer.data(), targetPlayer.data()) > WORLD_BATTLE_RANGE * WORLD_BATTLE_RANGE) {
    throw new Error("player_not_nearby");
  }
  requireTeam(ownProfile.data());
  requireTeam(targetProfile.data());
  if (ownProfile.get("activeMatchId") || targetProfile.get("activeMatchId")) throw new Error("already_in_match");
  const own = publicProfile(uid, ownProfile.data());
  await db.collection("pvpChallenges").doc(`${targetUid}_${uid}`).set({
    fromUid: uid,
    toUid: targetUid,
    displayName: own.displayName,
    friendCode: own.friendCode,
    level: own.level,
    rating: own.rating,
    rank: own.rank,
    status: "pending",
    source: "world",
    createdAt: FieldValue.serverTimestamp(),
  }, { merge: true });
  return { success: true };
}

async function leaderboard(db) {
  const snapshot = await db.collection("socialProfiles").orderBy("rating", "desc").limit(20).get();
  return { success: true, leaderboard: snapshot.docs.map((doc) => publicProfile(doc.id, doc.data())) };
}

function createSocialPvpHandler() {
  return async (request, response) => {
    if (request.method !== "POST") {
      sendJson(response, 405, { success: false, error: "method_not_allowed" });
      return;
    }
    let decoded;
    try {
      decoded = await authenticate(request);
    } catch (error) {
      sendJson(response, 401, { success: false, error: "unauthenticated" });
      return;
    }
    const db = getFirestore();
    const body = request.body || {};
    try {
      let result;
      switch (body.action) {
        case "syncProfile": result = await syncProfile(db, decoded, body); break;
        case "getSocial": result = await buildSocialState(db, decoded.uid); break;
        case "sendFriendRequest": result = await sendFriendRequest(db, decoded.uid, body); break;
        case "respondFriendRequest": result = await respondFriendRequest(db, decoded.uid, body); break;
        case "removeFriend": result = await removeFriend(db, decoded.uid, body); break;
        case "queueRanked": result = await queueRanked(db, decoded.uid); break;
        case "cancelQueue": result = await cancelQueue(db, decoded.uid); break;
        case "challengeFriend": result = await challengeFriend(db, decoded.uid, body); break;
        case "respondChallenge": result = await respondChallenge(db, decoded.uid, body); break;
        case "getMatch": result = await getMatch(db, decoded.uid, body); break;
        case "battleAction": result = await battleAction(db, decoded.uid, body); break;
        case "leaderboard": result = await leaderboard(db); break;
        case "listWorlds": result = await listWorlds(db, decoded.uid); break;
        case "joinWorld": result = await joinWorld(db, decoded, body); break;
        case "leaveWorld": result = await leaveWorld(db, decoded.uid, body); break;
        case "syncWorld": result = await syncWorld(db, decoded.uid, body); break;
        case "sendWorldChat": result = await sendWorldChat(db, decoded.uid, body); break;
        case "inviteFriendToWorld": result = await inviteFriendToWorld(db, decoded.uid, body); break;
        case "respondWorldInvite": result = await respondWorldInvite(db, decoded.uid, body); break;
        case "blockUser": result = await blockUser(db, decoded.uid, body); break;
        case "unblockUser": result = await unblockUser(db, decoded.uid, body); break;
        case "challengeWorldPlayer": result = await challengeWorldPlayer(db, decoded.uid, body); break;
        default: throw new Error("unknown_action");
      }
      sendJson(response, 200, result);
    } catch (error) {
      const message = error && error.message ? error.message : "social_pvp_failed";
      const conflicts = new Set([
        "already_friends", "already_in_match", "request_not_pending", "challenge_not_pending",
        "not_your_turn", "match_not_active", "skill_on_cooldown", "invalid_switch",
        "world_full", "user_blocked", "world_invite_not_pending",
      ]);
      const clientErrors = new Set([
        "friend_code_required", "friend_not_found", "cannot_add_self", "profile_not_synced",
        "team_must_have_three", "duplicate_team_member", "friend_uid_required", "invalid_friend",
        "not_friends", "not_match_player", "match_not_found", "action_identity_required",
        "invalid_skill", "invalid_active_insect", "unknown_action", "chat_message_required",
        "invalid_world_target", "not_in_world", "player_not_nearby", "world_not_found",
      ]);
      console.error("socialPvpApi failed", { action: body.action, uid: decoded.uid, error });
      sendJson(response, conflicts.has(message) ? 409 : clientErrors.has(message) ? 400 : 500, {
        success: false,
        error: conflicts.has(message) || clientErrors.has(message) ? message : "social_pvp_failed",
      });
    }
  };
}

module.exports = { createSocialPvpHandler, publicProfile };
