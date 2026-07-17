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
  await callApi(player1, "unblockUser", { targetUid: player2.uid });
  const unblockedSocial = await callApi(player1, "getSocial");
  assert.equal(unblockedSocial.blockedUsers.length, 0);
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
    ratings: [final1.profile.rating, final2.profile.rating],
  }, null, 2));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
