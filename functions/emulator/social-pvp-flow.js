const assert = require("node:assert/strict");

const PROJECT_ID = "insect-exploration-8f0ca";
const AUTH_URL = "http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=emulator-key";
const API_URL = `http://127.0.0.1:5001/${PROJECT_ID}/asia-northeast3/socialPvpApi`;

function makeTeam(prefix, primaryType) {
  return [0, 1, 2].map((index) => ({
    instanceId: `${prefix}-instance-${index}`,
    insectId: `${prefix}-species-${index}`,
    displayName: `${prefix.toUpperCase()} 곤충 ${index + 1}`,
    level: 12,
    primaryType,
    secondaryType: 0,
    maxHp: 120,
    hp: 120,
    attack: 42,
    defense: 34,
    skills: [
      {
        skillId: `${prefix}-damage-${index}`,
        displayName: "타입 강타",
        power: 38,
        element: primaryType,
        cooldown: 2,
        effectType: 0,
        effectValue: 0,
        effectDuration: 1,
      },
      {
        skillId: `${prefix}-buff-${index}`,
        displayName: "공격 집중",
        power: 1,
        element: primaryType,
        cooldown: 3,
        effectType: 1,
        effectValue: 0.3,
        effectDuration: 3,
      },
    ],
  }));
}

async function signUp(email) {
  const response = await fetch(AUTH_URL, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ email, password: "test-password-123", returnSecureToken: true }),
  });
  const body = await response.json();
  assert.equal(response.ok, true, `Auth signup failed: ${JSON.stringify(body)}`);
  return { uid: body.localId, token: body.idToken, email };
}

async function callApi(user, action, values = {}) {
  const response = await fetch(API_URL, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      authorization: `Bearer ${user.token}`,
    },
    body: JSON.stringify({ action, ...values }),
  });
  const body = await response.json();
  assert.equal(response.ok, true,
    `${action} failed (${response.status}): ${JSON.stringify(body)}`);
  assert.equal(body.success, true, `${action} returned failure`);
  return body;
}

async function callApiExpectError(user, action, expectedError, values = {}) {
  const response = await fetch(API_URL, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      authorization: `Bearer ${user.token}`,
    },
    body: JSON.stringify({ action, ...values }),
  });
  const body = await response.json();
  assert.equal(response.ok, false, `${action} should fail`);
  assert.equal(body.error, expectedError);
  return response.status;
}

// 클라이언트(JsonUtility)는 액션과 무관하게 섬 요청의 다섯 필드를 전부 보낸다.
function islandRequest(values = {}) {
  return { island: "", isPublic: false, friendCode: "", targetUid: "", ...values };
}

const ISLAND_INFO_KEYS = [
  "ownerUid", "ownerName", "friendCode", "isPublic", "likes", "visits",
  "likedByMe", "updatedAtMs", "snapshot",
];

// 섬 공유 — 차단 구간 전까지. player1이 주인, player2가 방문자다.
async function islandFlow(owner, visitor, ownerProfile) {
  const empty = await callApi(owner, "getMyIsland", islandRequest());
  assert.deepEqual(Object.keys(empty.island), ISLAND_INFO_KEYS);
  assert.deepEqual(empty.island, {
    ownerUid: owner.uid, ownerName: ownerProfile.displayName, friendCode: ownerProfile.friendCode,
    isPublic: false, likes: 0, visits: 0, likedByMe: false, updatedAtMs: 0, snapshot: "",
  });
  assert.equal(await callApiExpectError(visitor, "getIsland", "island_not_found",
    islandRequest({ targetUid: owner.uid })), 400);
  assert.equal(await callApiExpectError(visitor, "getIsland", "island_not_found",
    islandRequest({ friendCode: ownerProfile.friendCode })), 400);

  assert.equal(await callApiExpectError(owner, "publishIsland", "island_invalid",
    islandRequest({ isPublic: true })), 400);
  await callApiExpectError(owner, "publishIsland", "island_invalid",
    islandRequest({ island: "{not json", isPublic: true }));
  await callApiExpectError(owner, "publishIsland", "island_invalid",
    islandRequest({ island: "[]", isPublic: true }));
  await callApiExpectError(owner, "publishIsland", "island_invalid",
    islandRequest({ island: JSON.stringify({ pad: "x".repeat(48000) }), isPublic: true }));
  await callApiExpectError(owner, "publishIsland", "island_invalid", { island: { sizeLevel: 1 }, isPublic: true });

  const island = JSON.stringify({
    version: 7,
    ownerName: "가짜 이름",
    sizeLevel: 2,
    comfort: 120,
    placed: [
      { id: "f_bench", x: -3, z: 2, rot: 0 },
      { id: "Bad-Id", x: 0, z: 0, rot: 0 },
      { id: "f_lamp", x: 99, z: -99, rot: -1, extra: true },
    ],
    insects: [
      { insectId: "stag_beetle", level: 12, shiny: false },
      { insectId: "morpho", level: 500, shiny: true },
    ],
  });
  const expectedSnapshot = {
    version: 1,
    sizeLevel: 2,
    comfort: 120,
    placed: [
      { id: "f_bench", x: -3, z: 2, rot: 0 },
      { id: "f_lamp", x: 15, z: -16, rot: 3 },
    ],
    insects: [
      { insectId: "stag_beetle", level: 12, shiny: false },
      { insectId: "morpho", level: 80, shiny: true },
    ],
  };

  const hidden = await callApi(owner, "publishIsland", islandRequest({ island, isPublic: false }));
  assert.deepEqual(Object.keys(hidden.island), ISLAND_INFO_KEYS);
  assert.equal(hidden.island.isPublic, false);
  assert.equal(hidden.island.ownerName, ownerProfile.displayName);
  assert.equal(hidden.island.snapshot, "");
  assert.ok(hidden.island.updatedAtMs > 0);
  assert.equal(await callApiExpectError(visitor, "getIsland", "island_private",
    islandRequest({ targetUid: owner.uid })), 409);
  assert.equal(await callApiExpectError(visitor, "likeIsland", "island_private",
    islandRequest({ targetUid: owner.uid })), 409);
  const ownView = await callApi(owner, "getIsland", islandRequest({ targetUid: owner.uid }));
  assert.equal(ownView.island.visits, 0, "the owner's own visit is not counted");
  assert.deepEqual(JSON.parse(ownView.island.snapshot), expectedSnapshot);

  const shown = await callApi(owner, "publishIsland", islandRequest({ island, isPublic: true }));
  assert.equal(shown.island.isPublic, true);

  assert.equal(await callApiExpectError(visitor, "getIsland", "island_target_required",
    islandRequest({ targetUid: "  ", friendCode: " " })), 400);
  await callApiExpectError(visitor, "getIsland", "island_not_found", islandRequest({ friendCode: "ZZZZZZZZ" }));
  await callApiExpectError(visitor, "getIsland", "island_not_found", islandRequest({ targetUid: "no/such/uid" }));

  // 빈 targetUid + 소문자·공백 섞인 친구 코드.
  const visit1 = await callApi(visitor, "getIsland",
    islandRequest({ friendCode: ` ${ownerProfile.friendCode.toLowerCase()} ` }));
  assert.deepEqual(Object.keys(visit1.island), ISLAND_INFO_KEYS);
  assert.equal(visit1.island.ownerUid, owner.uid);
  assert.equal(visit1.island.ownerName, ownerProfile.displayName);
  assert.equal(visit1.island.friendCode, ownerProfile.friendCode);
  assert.equal(visit1.island.visits, 1);
  assert.equal(visit1.island.likes, 0);
  assert.equal(visit1.island.likedByMe, false);
  assert.deepEqual(JSON.parse(visit1.island.snapshot), expectedSnapshot);

  assert.equal(await callApiExpectError(visitor, "likeIsland", "island_target_required",
    islandRequest({ friendCode: ownerProfile.friendCode })), 400);
  assert.equal(await callApiExpectError(owner, "likeIsland", "cannot_like_self",
    islandRequest({ targetUid: owner.uid })), 400);
  await callApiExpectError(visitor, "likeIsland", "island_not_found", islandRequest({ targetUid: visitor.uid + "x" }));
  const liked = await callApi(visitor, "likeIsland", islandRequest({ targetUid: owner.uid }));
  assert.deepEqual(Object.keys(liked.island), ISLAND_INFO_KEYS);
  assert.equal(liked.island.likes, 1);
  assert.equal(liked.island.likedByMe, true);
  assert.equal(liked.island.snapshot, "");
  assert.equal(await callApiExpectError(visitor, "likeIsland", "already_liked_today",
    islandRequest({ targetUid: owner.uid })), 409);

  // targetUid와 friendCode가 둘 다 있으면 targetUid가 이긴다.
  const visit2 = await callApi(visitor, "getIsland",
    islandRequest({ targetUid: owner.uid, friendCode: "ZZZZZZZZ" }));
  assert.equal(visit2.island.visits, 2);
  assert.equal(visit2.island.likes, 1);
  assert.equal(visit2.island.likedByMe, true);

  // 다시 올려도 likes/visits는 그대로다.
  const republished = await callApi(owner, "publishIsland", islandRequest({ island, isPublic: true }));
  assert.equal(republished.island.likes, 1);
  assert.equal(republished.island.visits, 2);
  assert.ok(republished.island.updatedAtMs >= shown.island.updatedAtMs);
  const mine = await callApi(owner, "getMyIsland", islandRequest());
  assert.equal(mine.island.isPublic, true);
  assert.equal(mine.island.likes, 1);
  assert.equal(mine.island.visits, 2);
  assert.equal(mine.island.likedByMe, false);
  assert.equal(mine.island.snapshot, "");
}

// 프로필을 동기화한 적 없는 계정도 섬을 올리고 친구 코드로 찾힌다.
async function islandWithoutProfileFlow(visitor, stamp) {
  const loner = await signUp(`island-loner-${stamp}@example.test`);
  const mine = await callApi(loner, "getMyIsland", islandRequest());
  assert.match(mine.island.friendCode, /^[A-F0-9]{8}$/);
  assert.equal(mine.island.ownerName, "탐험가");
  await callApi(loner, "publishIsland", islandRequest({ island: "{}", isPublic: true }));
  const visit = await callApi(visitor, "getIsland", islandRequest({ friendCode: mine.island.friendCode }));
  assert.equal(visit.island.ownerUid, loner.uid);
  assert.equal(visit.island.ownerName, "탐험가");
  assert.deepEqual(JSON.parse(visit.island.snapshot),
    { version: 1, sizeLevel: 0, comfort: 0, placed: [], insects: [] });
  const deleted = await callApi(loner, "deleteIsland", islandRequest());
  assert.deepEqual(deleted, { success: true });
}

async function main() {
  const stamp = Date.now();
  const player1 = await signUp(`pvp1-${stamp}@example.test`);
  const player2 = await signUp(`pvp2-${stamp}@example.test`);
  const team1 = makeTeam("p1", 1);
  const team2 = makeTeam("p2", 2);

  const synced1 = await callApi(player1, "syncProfile", {
    displayName: "테스터 1", level: 20, team: team1,
  });
  const synced2 = await callApi(player2, "syncProfile", {
    displayName: "테스터 2", level: 21, team: team2,
  });
  assert.match(synced1.profile.friendCode, /^[A-F0-9]{8}$/);
  assert.match(synced2.profile.friendCode, /^[A-F0-9]{8}$/);

  await callApi(player1, "sendFriendRequest", {
    friendCode: synced2.profile.friendCode,
  });
  const social2 = await callApi(player2, "getSocial");
  assert.equal(social2.incomingRequests.length, 1);
  await callApi(player2, "respondFriendRequest", {
    requestId: social2.incomingRequests[0].requestId,
    accept: true,
  });
  const friends1 = await callApi(player1, "getSocial");
  assert.equal(friends1.friends.length, 1);
  assert.equal(friends1.friends[0].uid, player2.uid);

  const joined1 = await callApi(player1, "joinWorld", { x: 0, y: 0, z: 0, facing: 0 });
  assert.equal(joined1.world.maxPlayers, 5);
  assert.equal(joined1.world.playerCount, 1);
  await callApi(player1, "inviteFriendToWorld", { friendUid: player2.uid });
  const worlds2 = await callApi(player2, "listWorlds");
  assert.equal(worlds2.invites.length, 1);
  const acceptedInvite = await callApi(player2, "respondWorldInvite", {
    inviteId: worlds2.invites[0].inviteId, accept: true,
  });
  assert.equal(acceptedInvite.worldId, joined1.world.worldId);
  const joined2 = await callApi(player2, "joinWorld", {
    worldId: acceptedInvite.worldId, x: 2, y: 0, z: 0, facing: 180,
  });
  assert.equal(joined2.world.playerCount, 2);

  const extraPlayers = [];
  let fullWorld = joined2.world;
  for (let index = 3; index <= 5; index++) {
    const player = await signUp(`field${index}-${stamp}@example.test`);
    extraPlayers.push(player);
    await callApi(player, "syncProfile", { displayName: `필드 테스터 ${index}`, level: 10, team: [] });
    const joined = await callApi(player, "joinWorld", {
      worldId: acceptedInvite.worldId, x: index * 3, y: 0, z: 0, facing: 0,
    });
    fullWorld = joined.world;
  }
  assert.equal(fullWorld.playerCount, 5);
  const sixthPlayer = await signUp(`field6-${stamp}@example.test`);
  await callApi(sixthPlayer, "syncProfile", { displayName: "여섯 번째", level: 10, team: [] });
  await callApiExpectError(sixthPlayer, "joinWorld", "world_full", {
    worldId: acceptedInvite.worldId, x: 18, y: 0, z: 0,
  });

  await callApi(player1, "sendWorldChat", { targetUid: player2.uid, message: "같이 곤충 잡자!" });
  const synced2World = await callApi(player2, "syncWorld", {
    worldId: acceptedInvite.worldId, x: 2, y: 0, z: 0, facing: 180,
  });
  assert.equal(synced2World.messages.at(-1).message, "같이 곤충 잡자!");

  await callApi(player1, "challengeWorldPlayer", { targetUid: player2.uid });
  const challenged2 = await callApi(player2, "getSocial");
  assert.equal(challenged2.incomingChallenges.length, 1);
  const friendly = await callApi(player2, "respondChallenge", {
    challengeId: challenged2.incomingChallenges[0].challengeId,
    accept: true,
  });
  assert.equal(friendly.match.mode, "friendly");
  assert.equal(friendly.match.team1.length, 3);
  await callApi(player1, "battleAction", {
    matchId: friendly.matchId,
    clientActionId: `friendly-surrender-${stamp}`,
    actionType: "surrender",
  });

  const queued1 = await callApi(player1, "queueRanked");
  assert.equal(queued1.queued, true);
  const queued2 = await callApi(player2, "queueRanked");
  assert.equal(queued2.queued, false);
  assert.ok(queued2.matchId);
  assert.equal(queued2.match.mode, "ranked");

  const ranked = queued2.match;
  const surrendering = ranked.player1.uid === player1.uid ? player1 : player2;
  const winner = surrendering.uid === player1.uid ? player2 : player1;
  const finished = await callApi(surrendering, "battleAction", {
    matchId: ranked.matchId,
    clientActionId: `ranked-surrender-${stamp}`,
    actionType: "surrender",
  });
  assert.equal(finished.match.status, "finished");
  assert.equal(finished.match.winnerUid, winner.uid);

  const final1 = await callApi(player1, "getSocial");
  const final2 = await callApi(player2, "getSocial");
  assert.equal(final1.profile.wins + final2.profile.wins, 1);
  assert.equal(final1.profile.losses + final2.profile.losses, 1);
  assert.notEqual(final1.profile.rating, final2.profile.rating);

  const board = await callApi(player1, "leaderboard");
  assert.equal(board.leaderboard.length, 6);
  assert.ok(board.leaderboard[0].rating >= board.leaderboard[1].rating);

  await islandFlow(player1, player2, synced1.profile);
  await islandWithoutProfileFlow(player2, stamp);

  await callApi(player1, "blockUser", { targetUid: player2.uid });
  const blockedSocial = await callApi(player1, "getSocial");
  assert.equal(blockedSocial.blockedUsers.length, 1);
  assert.equal(blockedSocial.friends.length, 0);
  await callApiExpectError(player1, "sendWorldChat", "user_blocked", {
    targetUid: player2.uid, message: "보이면 안 됨",
  });
  await callApiExpectError(player2, "challengeWorldPlayer", "user_blocked", {
    targetUid: player1.uid,
  });
  assert.equal(await callApiExpectError(player2, "getIsland", "user_blocked",
    islandRequest({ targetUid: player1.uid })), 409);
  await callApiExpectError(player2, "getIsland", "user_blocked",
    islandRequest({ friendCode: synced1.profile.friendCode }));
  await callApiExpectError(player2, "likeIsland", "user_blocked",
    islandRequest({ targetUid: player1.uid }));
  await callApi(player1, "unblockUser", { targetUid: player2.uid });
  const unblockedSocial = await callApi(player1, "getSocial");
  assert.equal(unblockedSocial.blockedUsers.length, 0);

  // 차단 중 거부된 방문은 세지 않았다(2 → 3). 삭제는 likes까지 지워 같은 날 다시 누를 수 있다.
  const afterUnblock = await callApi(player2, "getIsland", islandRequest({ targetUid: player1.uid }));
  assert.equal(afterUnblock.island.visits, 3);
  assert.deepEqual(await callApi(player1, "deleteIsland", islandRequest()), { success: true });
  await callApiExpectError(player2, "getIsland", "island_not_found", islandRequest({ targetUid: player1.uid }));
  await callApiExpectError(player2, "likeIsland", "island_not_found", islandRequest({ targetUid: player1.uid }));
  const clearedIsland = await callApi(player1, "getMyIsland", islandRequest());
  assert.equal(clearedIsland.island.isPublic, false);
  assert.equal(clearedIsland.island.likes, 0);
  assert.equal(clearedIsland.island.visits, 0);
  assert.equal(clearedIsland.island.updatedAtMs, 0);
  await callApi(player1, "publishIsland", islandRequest({ island: "{}", isPublic: true }));
  const likedAgain = await callApi(player2, "likeIsland", islandRequest({ targetUid: player1.uid }));
  assert.equal(likedAgain.island.likes, 1);
  await callApi(player1, "deleteIsland", islandRequest());
  await callApi(player1, "deleteIsland", islandRequest());
  await callApi(player1, "leaveWorld", { worldId: joined1.world.worldId });
  await callApi(player2, "leaveWorld", { worldId: joined1.world.worldId });
  for (const player of extraPlayers) {
    await callApi(player, "leaveWorld", { worldId: joined1.world.worldId });
  }

  console.log(JSON.stringify({
    success: true,
    friendRequest: "passed",
    friendly3v3: "passed",
    ranked3v3: "passed",
    ratingUpdate: "passed",
    leaderboard: "passed",
    fivePlayerWorld: "passed",
    fieldInvite: "passed",
    proximityChat: "passed",
    fieldBattle: "passed",
    blockEnforcement: "passed",
    islandShare: "passed",
    ratings: [final1.profile.rating, final2.profile.rating],
  }, null, 2));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
