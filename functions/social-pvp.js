const crypto = require("node:crypto");

const DEFAULT_RATING = 1000;
const MAX_TEAM_SIZE = 3;
const MAX_SKILLS = 4;

function clamp(value, min, max) {
  const n = Number(value);
  return Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : min;
}

function rankForRating(rating) {
  const r = clamp(rating, 0, 9999);
  if (r >= 2000) return "마스터";
  if (r >= 1800) return "다이아몬드";
  if (r >= 1600) return "플래티넘";
  if (r >= 1400) return "골드";
  if (r >= 1200) return "실버";
  return "브론즈";
}

function friendCodeForUid(uid) {
  return crypto.createHash("sha256").update(String(uid)).digest("hex").slice(0, 8).toUpperCase();
}

function sanitizeSkill(raw = {}) {
  return {
    skillId: String(raw.skillId || "basic_skill").slice(0, 80),
    displayName: String(raw.displayName || "기술").slice(0, 40),
    power: Math.round(clamp(raw.power, 1, 100)),
    element: Math.round(clamp(raw.element, 0, 10)),
    cooldown: Math.round(clamp(raw.cooldown, 0, 6)),
    effectType: Math.round(clamp(raw.effectType, 0, 2)),
    effectValue: clamp(raw.effectValue, 0, 1),
    effectDuration: Math.round(clamp(raw.effectDuration, 1, 6)),
  };
}

function sanitizeTeam(rawTeam) {
  if (!Array.isArray(rawTeam) || rawTeam.length !== MAX_TEAM_SIZE) {
    throw new Error("team_must_have_three");
  }
  const seen = new Set();
  return rawTeam.map((raw = {}, index) => {
    const instanceId = String(raw.instanceId || `slot-${index}`).slice(0, 80);
    if (seen.has(instanceId)) throw new Error("duplicate_team_member");
    seen.add(instanceId);
    const maxHp = Math.round(clamp(raw.maxHp, 20, 600));
    return {
      instanceId,
      insectId: String(raw.insectId || "unknown").slice(0, 80),
      displayName: String(raw.displayName || "곤충").slice(0, 40),
      level: Math.round(clamp(raw.level, 1, 50)),
      primaryType: Math.round(clamp(raw.primaryType, 1, 10)),
      secondaryType: Math.round(clamp(raw.secondaryType, 0, 10)),
      maxHp,
      hp: maxHp,
      attack: Math.round(clamp(raw.attack, 1, 250)),
      defense: Math.round(clamp(raw.defense, 1, 250)),
      skills: Array.isArray(raw.skills) ? raw.skills.slice(0, MAX_SKILLS).map(sanitizeSkill) : [],
    };
  });
}

const STRONG = new Map([
  [1, [2, 9]], [2, [3, 6]], [3, [6, 10]], [4, [1, 2]], [5, [3, 4]],
  [6, [5, 7, 10]], [7, [2, 1]], [8, [9, 7]], [9, [8, 1]], [10, [1, 4]],
]);

function isStrong(attack, defense) {
  return (STRONG.get(attack) || []).includes(defense);
}

function singleEffectiveness(attack, defense) {
  if (!attack || !defense) return 1;
  if (isStrong(attack, defense)) return 1.5;
  if (isStrong(defense, attack)) return 0.67;
  return 1;
}

function typeMultiplier(attack, defender) {
  let result = singleEffectiveness(attack, defender.primaryType);
  if (defender.secondaryType && defender.secondaryType !== defender.primaryType) {
    result *= singleEffectiveness(attack, defender.secondaryType);
  }
  return clamp(result, 0.45, 2.25);
}

function nextAlive(team, start = 0) {
  for (let offset = 0; offset < team.length; offset += 1) {
    const i = (start + offset) % team.length;
    if (team[i].hp > 0) return i;
  }
  return -1;
}

function createMatch({ id, mode, player1, player2, now = Date.now() }) {
  return {
    matchId: id,
    mode,
    status: "active",
    player1: { uid: player1.uid, displayName: player1.displayName, rating: player1.rating || DEFAULT_RATING },
    player2: { uid: player2.uid, displayName: player2.displayName, rating: player2.rating || DEFAULT_RATING },
    team1: sanitizeTeam(player1.team),
    team2: sanitizeTeam(player2.team),
    active1: 0,
    active2: 0,
    turnUid: player1.uid,
    turnNumber: 1,
    // Firestore는 배열 안에 배열을 저장할 수 없으므로 [teamSlot * 4 + skillSlot]로 평탄화한다.
    cooldowns1: Array(MAX_TEAM_SIZE * MAX_SKILLS).fill(0),
    cooldowns2: Array(MAX_TEAM_SIZE * MAX_SKILLS).fill(0),
    attackBonuses1: Array(MAX_TEAM_SIZE).fill(0),
    attackBonuses2: Array(MAX_TEAM_SIZE).fill(0),
    effectTurns1: Array(MAX_TEAM_SIZE).fill(0),
    effectTurns2: Array(MAX_TEAM_SIZE).fill(0),
    log: ["3:3 배틀 시작"],
    winnerUid: "",
    createdAtMs: now,
    updatedAtMs: now,
  };
}

function processAction(match, uid, action = {}) {
  if (!match || match.status !== "active") throw new Error("match_not_active");
  const side = match.player1.uid === uid ? 1 : match.player2.uid === uid ? 2 : 0;
  if (!side) throw new Error("not_match_player");
  if (action.type === "surrender") {
    finishMatch(match, side === 1 ? match.player2.uid : match.player1.uid, `${uid} 항복`);
    return match;
  }
  if (match.turnUid !== uid) throw new Error("not_your_turn");

  const ownTeam = side === 1 ? match.team1 : match.team2;
  const enemyTeam = side === 1 ? match.team2 : match.team1;
  const ownActiveKey = side === 1 ? "active1" : "active2";
  const enemyActiveKey = side === 1 ? "active2" : "active1";
  const cooldownKey = side === 1 ? "cooldowns1" : "cooldowns2";
  const ownBonusKey = side === 1 ? "attackBonuses1" : "attackBonuses2";
  const enemyBonusKey = side === 1 ? "attackBonuses2" : "attackBonuses1";
  const ownTurnsKey = side === 1 ? "effectTurns1" : "effectTurns2";
  const enemyTurnsKey = side === 1 ? "effectTurns2" : "effectTurns1";
  const enemyUid = side === 1 ? match.player2.uid : match.player1.uid;

  if (action.type === "switch") {
    const target = Math.round(clamp(action.slot, 0, MAX_TEAM_SIZE - 1));
    if (target === match[ownActiveKey] || ownTeam[target].hp <= 0) throw new Error("invalid_switch");
    match[ownActiveKey] = target;
    match.log.push(`${ownTeam[target].displayName} 교체 출전`);
  } else {
    const attacker = ownTeam[match[ownActiveKey]];
    const defender = enemyTeam[match[enemyActiveKey]];
    if (!attacker || attacker.hp <= 0 || !defender || defender.hp <= 0) throw new Error("invalid_active_insect");
    let skill = null;
    let basePower = Math.max(1, Math.round(attacker.attack * 0.7));
    if (action.type === "skill") {
      const skillIndex = Math.round(clamp(action.skillIndex, 0, MAX_SKILLS - 1));
      skill = attacker.skills[skillIndex];
      if (!skill) throw new Error("invalid_skill");
      const cooldownIndex = match[ownActiveKey] * MAX_SKILLS + skillIndex;
      if (match[cooldownKey][cooldownIndex] > 0) throw new Error("skill_on_cooldown");
      basePower = skill.power;
      match[cooldownKey][cooldownIndex] = skill.cooldown;
    }
    const label = skill ? skill.displayName : "기본 공격";
    if (skill && skill.effectType === 1) {
      const slot = match[ownActiveKey];
      match[ownBonusKey][slot] = clamp(match[ownBonusKey][slot] + skill.effectValue, -0.7, 2);
      match[ownTurnsKey][slot] = skill.effectDuration + 1;
      match.log.push(`${attacker.displayName}의 ${label}: 공격력 상승`);
    } else if (skill && skill.effectType === 2) {
      const slot = match[enemyActiveKey];
      match[enemyBonusKey][slot] = clamp(match[enemyBonusKey][slot] - skill.effectValue, -0.7, 2);
      match[enemyTurnsKey][slot] = skill.effectDuration + 1;
      match.log.push(`${attacker.displayName}의 ${label}: 상대 공격력 하락`);
    } else {
      const element = skill ? skill.element : 0;
      const effectiveness = typeMultiplier(element, defender);
      const stab = element && (element === attacker.primaryType || element === attacker.secondaryType) ? 1.2 : 1;
      const statRatio = clamp(attacker.attack / Math.max(1, defender.defense), 0.5, 2.5);
      const attackBonus = clamp(1 + match[ownBonusKey][match[ownActiveKey]], 0.3, 3);
      const damage = Math.max(1, Math.round((basePower + attacker.level * 2) * statRatio * effectiveness * stab * attackBonus));
      defender.hp = Math.max(0, defender.hp - damage);
      match.log.push(`${attacker.displayName}의 ${label}: ${damage} 피해${effectiveness > 1.05 ? " (효과 굉장)" : effectiveness < 0.95 ? " (효과 감소)" : ""}`);
    }
    if (defender.hp <= 0) {
      match.log.push(`${defender.displayName} 쓰러짐`);
      const replacement = nextAlive(enemyTeam, match[enemyActiveKey] + 1);
      if (replacement < 0) {
        finishMatch(match, uid, `${uid} 승리`);
        return match;
      }
      match[enemyActiveKey] = replacement;
      match.log.push(`${enemyTeam[replacement].displayName} 자동 출전`);
    }
  }

  for (let i = 0; i < match[cooldownKey].length; i += 1) {
    if (match[cooldownKey][i] > 0) match[cooldownKey][i] -= 1;
  }
  tickEffects(match.attackBonuses1, match.effectTurns1);
  tickEffects(match.attackBonuses2, match.effectTurns2);
  match.turnUid = enemyUid;
  match.turnNumber += 1;
  match.updatedAtMs = Date.now();
  match.log = match.log.slice(-12);
  return match;
}

function tickEffects(bonuses, turns) {
  for (let i = 0; i < turns.length; i += 1) {
    if (turns[i] <= 0) continue;
    turns[i] -= 1;
    if (turns[i] <= 0) bonuses[i] = 0;
  }
}

function finishMatch(match, winnerUid, logText) {
  match.status = "finished";
  match.winnerUid = winnerUid;
  match.turnUid = "";
  match.updatedAtMs = Date.now();
  match.log.push(logText);
  match.log = match.log.slice(-12);
}

function eloChange(winnerRating, loserRating, k = 32) {
  const expected = 1 / (1 + 10 ** ((loserRating - winnerRating) / 400));
  return Math.max(1, Math.round(k * (1 - expected)));
}

module.exports = {
  DEFAULT_RATING, MAX_TEAM_SIZE, createMatch, eloChange, friendCodeForUid,
  processAction, rankForRating, sanitizeTeam, typeMultiplier,
};
