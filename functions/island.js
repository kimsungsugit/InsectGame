// 나의 섬 공유 — 스냅샷 검증·날짜 키·응답 형태.
// firebase 의존이 없다. 단위 테스트(test/island.test.js)가 에뮬레이터 없이 돈다.

const MAX_ISLAND_JSON_LENGTH = 48000;
const MAX_PLACED = 400;
const MAX_INSECTS = 10;
const PLACED_ID_PATTERN = /^[a-z][a-z0-9_]{1,31}$/;
const INSECT_ID_PATTERN = /^[a-z][a-z0-9_]{1,47}$/;

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

// 숫자와 숫자 문자열("12")만 받는다. Number(null)=0, Number(true)=1, Number([5])=5 같은
// 암묵 변환은 받지 않고 fallback으로 돌린다. `+ 0`은 Math.round가 내는 -0을 0으로 편다.
function toInt(value, fallback) {
  let n;
  if (typeof value === "number") n = value;
  else if (typeof value === "string" && value.trim() !== "") n = Number(value);
  else return fallback;
  return Number.isFinite(n) ? Math.round(n) + 0 : fallback;
}

function clampInt(value, min, max, fallback = min) {
  return Math.min(max, Math.max(min, toInt(value, fallback)));
}

function sanitizePlaced(rawPlaced) {
  if (!Array.isArray(rawPlaced)) return [];
  const placed = [];
  // 상한을 먼저 자르고(앞 400개) 그 안에서 형식이 틀린 항목을 버린다.
  for (const item of rawPlaced.slice(0, MAX_PLACED)) {
    if (!isPlainObject(item) || typeof item.id !== "string" || !PLACED_ID_PATTERN.test(item.id)) continue;
    placed.push({
      id: item.id,
      // 섬은 최대 22칸이지만 여유를 둔다.
      x: clampInt(item.x, -16, 15, 0),
      z: clampInt(item.z, -16, 15, 0),
      rot: ((toInt(item.rot, 0) % 4) + 4) % 4,
    });
  }
  return placed;
}

function sanitizeInsects(rawInsects) {
  if (!Array.isArray(rawInsects)) return [];
  const insects = [];
  for (const item of rawInsects.slice(0, MAX_INSECTS)) {
    if (!isPlainObject(item) || typeof item.insectId !== "string"
        || !INSECT_ID_PATTERN.test(item.insectId)) continue;
    insects.push({
      insectId: item.insectId,
      level: clampInt(item.level, 1, 80, 1),
      // Boolean("false")가 true라서 형변환하지 않는다 — 진짜 true만 샤이니다.
      shiny: item.shiny === true,
    });
  }
  return insects;
}

// 정리된 스냅샷 또는 null(검증 실패). 카탈로그 id 목록은 서버에 두지 않는다 —
// 형식만 보고, 모르는 id는 클라이언트가 스스로 버린다.
// ownerName은 버린다(서버가 IslandInfo.ownerName으로 따로 넣는다). 그 외 모르는 필드도 버린다.
function sanitizeIslandSnapshot(raw) {
  if (!isPlainObject(raw)) return null;
  return {
    version: 1,
    sizeLevel: clampInt(raw.sizeLevel, 0, 3),
    comfort: clampInt(raw.comfort, 0, 9999),
    placed: sanitizePlaced(raw.placed),
    insects: sanitizeInsects(raw.insects),
  };
}

// 요청의 `island` 필드(JSON 문자열)를 정리된 스냅샷으로. 문자열이 아니거나, 길이 초과,
// 파싱 실패, 검증 실패면 null — 호출부가 island_invalid로 바꾼다.
function parseIslandPayload(text) {
  if (typeof text !== "string" || text.length > MAX_ISLAND_JSON_LENGTH) return null;
  let parsed;
  try {
    parsed = JSON.parse(text);
  } catch (error) {
    return null;
  }
  return sanitizeIslandSnapshot(parsed);
}

function trimmedText(value, maxLength) {
  return typeof value === "string" ? value.trim().slice(0, maxLength) : "";
}

// getIsland의 대상 해석. 클라이언트(JsonUtility)는 액션과 무관하게 다섯 필드를 전부 보내므로
// 빈 문자열(공백만 있는 것 포함)은 "없는 값"이다. targetUid가 있으면 그것만 쓰고(friendCode는 ""),
// 없으면 친구 코드(대문자·앞뒤 공백 제거), 둘 다 없으면 null — 호출부가 island_target_required로 바꾼다.
function resolveIslandTarget(body) {
  const source = isPlainObject(body) ? body : {};
  const targetUid = trimmedText(source.targetUid, 128);
  if (targetUid) return { targetUid, friendCode: "" };
  const friendCode = trimmedText(source.friendCode, 12).toUpperCase();
  if (friendCode) return { targetUid: "", friendCode };
  return null;
}

// UTC 기준 YYYY-MM-DD. 좋아요 하루 1회 제한의 경계다.
function utcDayKey(date = new Date()) {
  const value = date instanceof Date ? date : new Date(date);
  return value.toISOString().slice(0, 10);
}

function count(value) {
  return Math.max(0, Math.round(Number(value) || 0));
}

// 응답의 IslandInfo. 클라이언트(JsonUtility)가 이 키와 타입에 묶여 있다 — 키를 늘리거나
// 빼거나 타입을 바꾸지 말 것. 저장된 문서가 없으면(data = {}) fallback 값으로 채운다.
function buildIslandInfo(ownerUid, data = {}, options = {}) {
  return {
    ownerUid: String(ownerUid || ""),
    ownerName: String(data.ownerName || options.ownerName || "탐험가"),
    friendCode: String(data.friendCode || options.friendCode || ""),
    isPublic: data.isPublic === true,
    likes: count(data.likes),
    visits: count(data.visits),
    likedByMe: options.likedByMe === true,
    updatedAtMs: count(data.updatedAtMs),
    snapshot: options.includeSnapshot === true && typeof data.snapshot === "string" ? data.snapshot : "",
  };
}

module.exports = {
  MAX_INSECTS, MAX_ISLAND_JSON_LENGTH, MAX_PLACED,
  buildIslandInfo, parseIslandPayload, resolveIslandTarget, sanitizeIslandSnapshot, utcDayKey,
};
