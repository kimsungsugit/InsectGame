const test = require("node:test");
const assert = require("node:assert/strict");
const {
  createMatch,
  eloChange,
  friendCodeForUid,
  processAction,
  rankForRating,
  sanitizeTeam,
  typeMultiplier,
} = require("../social-pvp");

function team(prefix) {
  return [0, 1, 2].map((index) => ({
    instanceId: `${prefix}-${index}`,
    insectId: `species-${index}`,
    displayName: `${prefix}${index}`,
    level: 10,
    primaryType: index === 0 ? 1 : 3,
    secondaryType: 0,
    maxHp: 100,
    attack: 30,
    defense: 30,
    skills: [{ skillId: "bug-hit", displayName: "Bug Hit", power: 40, element: 1, cooldown: 2 }],
  }));
}

test("friend codes are stable and do not reveal the uid", () => {
  const code = friendCodeForUid("firebase-user-123");
  assert.equal(code, friendCodeForUid("firebase-user-123"));
  assert.match(code, /^[A-F0-9]{8}$/);
  assert.equal(code.includes("USER"), false);
});

test("3v3 team validation rejects missing and duplicate members", () => {
  assert.throws(() => sanitizeTeam(team("a").slice(0, 2)), /team_must_have_three/);
  const duplicate = team("a");
  duplicate[2].instanceId = duplicate[0].instanceId;
  assert.throws(() => sanitizeTeam(duplicate), /duplicate_team_member/);
  assert.equal(sanitizeTeam(team("valid")).length, 3);
});

test("type chart applies strong and resisted damage multipliers", () => {
  const leafDefender = { primaryType: 2, secondaryType: 0 };
  assert.equal(typeMultiplier(1, leafDefender), 1.5);
  assert.equal(typeMultiplier(3, leafDefender), 0.67);
});

test("only the active player can act and knockout auto-switches", () => {
  const match = createMatch({
    id: "m1",
    mode: "ranked",
    player1: { uid: "p1", displayName: "P1", rating: 1000, team: team("p1") },
    player2: { uid: "p2", displayName: "P2", rating: 1000, team: team("p2") },
  });
  assert.equal(match.cooldowns1.length, 12);
  assert.equal(Array.isArray(match.cooldowns1[0]), false);
  assert.throws(() => processAction(match, "p2", { type: "basic" }), /not_your_turn/);
  match.team2[0].hp = 1;
  processAction(match, "p1", { type: "skill", skillIndex: 0 });
  assert.equal(match.team2[0].hp, 0);
  assert.equal(match.active2, 1);
  assert.equal(match.turnUid, "p2");
  assert.equal(match.status, "active");
});

test("last knockout and surrender finish a match", () => {
  const make = () => createMatch({
    id: "m2",
    mode: "friendly",
    player1: { uid: "p1", displayName: "P1", team: team("p1") },
    player2: { uid: "p2", displayName: "P2", team: team("p2") },
  });
  const knockout = make();
  knockout.team2[0].hp = 1;
  knockout.team2[1].hp = 0;
  knockout.team2[2].hp = 0;
  processAction(knockout, "p1", { type: "basic" });
  assert.equal(knockout.status, "finished");
  assert.equal(knockout.winnerUid, "p1");

  const surrendered = make();
  processAction(surrendered, "p1", { type: "surrender" });
  assert.equal(surrendered.status, "finished");
  assert.equal(surrendered.winnerUid, "p2");
});

test("buff and debuff skills change server-side attack state", () => {
  const p1Team = team("p1");
  p1Team[0].skills[0] = {
    skillId: "focus", displayName: "Focus", power: 1, element: 1,
    cooldown: 2, effectType: 1, effectValue: 0.4, effectDuration: 3,
  };
  const match = createMatch({
    id: "m3",
    mode: "ranked",
    player1: { uid: "p1", displayName: "P1", team: p1Team },
    player2: { uid: "p2", displayName: "P2", team: team("p2") },
  });
  const hpBefore = match.team2[0].hp;
  processAction(match, "p1", { type: "skill", skillIndex: 0 });
  assert.equal(match.team2[0].hp, hpBefore);
  assert.equal(match.attackBonuses1[0], 0.4);
  assert.equal(match.effectTurns1[0], 3);
});

test("rank bands and Elo changes are monotonic", () => {
  assert.notEqual(rankForRating(1000), rankForRating(1400));
  assert.notEqual(rankForRating(1400), rankForRating(2000));
  assert.equal(eloChange(1000, 1000), 16);
  assert.ok(eloChange(800, 1400) > eloChange(1400, 800));
});
