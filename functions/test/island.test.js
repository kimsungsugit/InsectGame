const test = require("node:test");
const assert = require("node:assert/strict");
const {
  MAX_INSECTS,
  MAX_ISLAND_JSON_LENGTH,
  MAX_PLACED,
  buildIslandInfo,
  parseIslandPayload,
  resolveIslandTarget,
  sanitizeIslandSnapshot,
  utcDayKey,
} = require("../island");

function snapshot(overrides = {}) {
  return {
    version: 1,
    ownerName: "클라이언트가 보낸 이름",
    sizeLevel: 2,
    comfort: 120,
    placed: [
      { id: "f_bench", x: -3, z: 2, rot: 0 },
      { id: "f_lamp", x: 4, z: -1, rot: 3 },
    ],
    insects: [
      { insectId: "stag_beetle", level: 12, shiny: false },
      { insectId: "morpho", level: 30, shiny: true },
    ],
    ...overrides,
  };
}

test("a valid snapshot survives intact except for ownerName", () => {
  assert.deepEqual(sanitizeIslandSnapshot(snapshot()), {
    version: 1,
    sizeLevel: 2,
    comfort: 120,
    placed: [
      { id: "f_bench", x: -3, z: 2, rot: 0 },
      { id: "f_lamp", x: 4, z: -1, rot: 3 },
    ],
    insects: [
      { insectId: "stag_beetle", level: 12, shiny: false },
      { insectId: "morpho", level: 30, shiny: true },
    ],
  });
});

test("non-objects are rejected", () => {
  for (const raw of [null, undefined, 0, 12, "{}", true, [], [snapshot()]]) {
    assert.equal(sanitizeIslandSnapshot(raw), null, `${JSON.stringify(raw)} should be rejected`);
  }
});

test("an empty object becomes an empty island", () => {
  assert.deepEqual(sanitizeIslandSnapshot({}), {
    version: 1, sizeLevel: 0, comfort: 0, placed: [], insects: [],
  });
});

test("version is rewritten and unknown top-level fields are dropped", () => {
  const result = sanitizeIslandSnapshot(snapshot({ version: 99, likes: 5000, admin: true }));
  assert.equal(result.version, 1);
  assert.deepEqual(Object.keys(result), ["version", "sizeLevel", "comfort", "placed", "insects"]);
});

test("sizeLevel and comfort are clamped to integers", () => {
  assert.equal(sanitizeIslandSnapshot(snapshot({ sizeLevel: -1 })).sizeLevel, 0);
  assert.equal(sanitizeIslandSnapshot(snapshot({ sizeLevel: 9 })).sizeLevel, 3);
  assert.equal(sanitizeIslandSnapshot(snapshot({ sizeLevel: 1.6 })).sizeLevel, 2);
  assert.equal(sanitizeIslandSnapshot(snapshot({ comfort: -50 })).comfort, 0);
  assert.equal(sanitizeIslandSnapshot(snapshot({ comfort: 123456 })).comfort, 9999);
  assert.equal(sanitizeIslandSnapshot(snapshot({ comfort: 9999 })).comfort, 9999);
  assert.equal(sanitizeIslandSnapshot(snapshot({ comfort: 41.5 })).comfort, 42);
});

test("non-array placed and insects become empty lists", () => {
  const result = sanitizeIslandSnapshot(snapshot({ placed: { 0: { id: "f_bench" } }, insects: "none" }));
  assert.deepEqual(result.placed, []);
  assert.deepEqual(result.insects, []);
});

test("placed is cut to the first 400 entries", () => {
  const placed = Array.from({ length: MAX_PLACED + 25 }, (_, index) => ({
    id: `f_item_${index}`, x: 0, z: 0, rot: 0,
  }));
  const result = sanitizeIslandSnapshot(snapshot({ placed }));
  assert.equal(MAX_PLACED, 400);
  assert.equal(result.placed.length, 400);
  assert.equal(result.placed[0].id, "f_item_0");
  assert.equal(result.placed[399].id, "f_item_399");

  const exact = sanitizeIslandSnapshot(snapshot({ placed: placed.slice(0, MAX_PLACED) }));
  assert.equal(exact.placed.length, 400);
});

test("placed entries with a malformed id are dropped", () => {
  const result = sanitizeIslandSnapshot(snapshot({
    placed: [
      { id: "f_bench", x: 1, z: 1, rot: 1 },
      { id: "F_Bench", x: 1, z: 1, rot: 1 },
      { id: "1bench", x: 1, z: 1, rot: 1 },
      { id: "f", x: 1, z: 1, rot: 1 },
      { id: "f-bench", x: 1, z: 1, rot: 1 },
      { id: "f bench", x: 1, z: 1, rot: 1 },
      { id: "f_bench\n", x: 1, z: 1, rot: 1 },
      { id: `f${"a".repeat(32)}`, x: 1, z: 1, rot: 1 },
      { id: ["f_bench"], x: 1, z: 1, rot: 1 },
      { id: 42, x: 1, z: 1, rot: 1 },
      { x: 1, z: 1, rot: 1 },
      null,
      "f_bench",
      ["f_bench"],
      { id: `f${"a".repeat(31)}`, x: 2, z: 2, rot: 2 },
      { id: "fb", x: 3, z: 3, rot: 3 },
    ],
  }));
  assert.deepEqual(result.placed, [
    { id: "f_bench", x: 1, z: 1, rot: 1 },
    { id: `f${"a".repeat(31)}`, x: 2, z: 2, rot: 2 },
    { id: "fb", x: 3, z: 3, rot: 3 },
  ]);
});

test("placed coordinates are rounded and clamped, extra fields dropped", () => {
  const result = sanitizeIslandSnapshot(snapshot({
    placed: [
      { id: "f_a", x: -99, z: 99, rot: 0, scale: 50, owner: "someone" },
      { id: "f_b", x: -16, z: 15, rot: 0 },
      { id: "f_c", x: 2.6, z: -2.6, rot: 0 },
      { id: "f_d", x: -0.4, z: 0.4, rot: 0 },
    ],
  }));
  assert.deepEqual(result.placed, [
    { id: "f_a", x: -16, z: 15, rot: 0 },
    { id: "f_b", x: -16, z: 15, rot: 0 },
    { id: "f_c", x: 3, z: -3, rot: 0 },
    { id: "f_d", x: 0, z: 0, rot: 0 },
  ]);
});

test("rot is normalised into 0..3, including negatives", () => {
  const rots = [-1, -4, -5, 4, 7, 2, 1.6, -0.4];
  const result = sanitizeIslandSnapshot(snapshot({
    placed: rots.map((rot) => ({ id: "f_bench", x: 0, z: 0, rot })),
  }));
  assert.deepEqual(result.placed.map((item) => item.rot), [3, 0, 3, 0, 3, 2, 2, 0]);
});

test("numeric strings are read as numbers, everything else falls back", () => {
  const result = sanitizeIslandSnapshot({
    sizeLevel: "2",
    comfort: " 150 ",
    placed: [
      { id: "f_a", x: "-3", z: "7.5", rot: "5" },
      { id: "f_b", x: "abc", z: "", rot: "left" },
      { id: "f_c", x: null, z: true, rot: [2] },
      { id: "f_d", x: Number.NaN, z: Number.POSITIVE_INFINITY, rot: {} },
    ],
    insects: [
      { insectId: "stag_beetle", level: "40", shiny: "true" },
      { insectId: "morpho", level: "high", shiny: 1 },
    ],
  });
  assert.equal(result.sizeLevel, 2);
  assert.equal(result.comfort, 150);
  assert.deepEqual(result.placed, [
    { id: "f_a", x: -3, z: 8, rot: 1 },
    { id: "f_b", x: 0, z: 0, rot: 0 },
    { id: "f_c", x: 0, z: 0, rot: 0 },
    { id: "f_d", x: 0, z: 0, rot: 0 },
  ]);
  assert.deepEqual(result.insects, [
    { insectId: "stag_beetle", level: 40, shiny: false },
    { insectId: "morpho", level: 1, shiny: false },
  ]);
  assert.equal(sanitizeIslandSnapshot({ sizeLevel: "big", comfort: null }).sizeLevel, 0);
  assert.equal(sanitizeIslandSnapshot({ sizeLevel: "big", comfort: null }).comfort, 0);
});

test("insects are cut to 10, level clamped, shiny forced to a boolean", () => {
  const insects = Array.from({ length: MAX_INSECTS + 4 }, (_, index) => ({
    insectId: `bug_${index}`, level: index, shiny: index % 2 === 0, nickname: "버려질 필드",
  }));
  const result = sanitizeIslandSnapshot(snapshot({ insects }));
  assert.equal(MAX_INSECTS, 10);
  assert.equal(result.insects.length, 10);
  assert.deepEqual(result.insects[0], { insectId: "bug_0", level: 1, shiny: true });
  assert.deepEqual(result.insects[9], { insectId: "bug_9", level: 9, shiny: false });

  const levels = sanitizeIslandSnapshot(snapshot({
    insects: [
      { insectId: "bug_a", level: 0 },
      { insectId: "bug_b", level: -7 },
      { insectId: "bug_c", level: 80 },
      { insectId: "bug_d", level: 81 },
      { insectId: "bug_e", level: 999 },
      { insectId: "bug_f", level: 12.5 },
      { insectId: "bug_g" },
    ],
  })).insects;
  assert.deepEqual(levels.map((insect) => insect.level), [1, 1, 80, 80, 80, 13, 1]);
  assert.deepEqual(levels.map((insect) => insect.shiny), [false, false, false, false, false, false, false]);
});

test("insects with a malformed insectId are dropped", () => {
  const result = sanitizeIslandSnapshot(snapshot({
    insects: [
      { insectId: "stag_beetle", level: 5, shiny: false },
      { insectId: "Stag_Beetle", level: 5, shiny: false },
      { insectId: "s", level: 5, shiny: false },
      { insectId: "../users", level: 5, shiny: false },
      { insectId: `b${"a".repeat(48)}`, level: 5, shiny: false },
      { level: 5, shiny: false },
      null,
      { insectId: `b${"a".repeat(47)}`, level: 5, shiny: false },
    ],
  }));
  assert.deepEqual(result.insects.map((insect) => insect.insectId), [
    "stag_beetle", `b${"a".repeat(47)}`,
  ]);
});

test("the request payload must be a JSON string within the size limit", () => {
  assert.deepEqual(parseIslandPayload(JSON.stringify(snapshot())), sanitizeIslandSnapshot(snapshot()));
  assert.equal(parseIslandPayload(snapshot()), null, "an object is not a string payload");
  assert.equal(parseIslandPayload(undefined), null);
  assert.equal(parseIslandPayload(null), null);
  assert.equal(parseIslandPayload(""), null);
  assert.equal(parseIslandPayload("{not json"), null);
  assert.equal(parseIslandPayload("[]"), null);
  assert.equal(parseIslandPayload("null"), null);
  assert.equal(parseIslandPayload("12"), null);
  assert.equal(parseIslandPayload("\"text\""), null);

  assert.equal(MAX_ISLAND_JSON_LENGTH, 48000);
  const base = JSON.stringify({ sizeLevel: 1, pad: "" });
  const atLimit = JSON.stringify({ sizeLevel: 1, pad: "x".repeat(MAX_ISLAND_JSON_LENGTH - base.length) });
  assert.equal(atLimit.length, MAX_ISLAND_JSON_LENGTH);
  assert.equal(parseIslandPayload(atLimit).sizeLevel, 1);
  const overLimit = JSON.stringify({ sizeLevel: 1, pad: "x".repeat(MAX_ISLAND_JSON_LENGTH - base.length + 1) });
  assert.equal(overLimit.length, MAX_ISLAND_JSON_LENGTH + 1);
  assert.equal(parseIslandPayload(overLimit), null);
});

test("a full 400-item island still fits the payload limit after sanitising", () => {
  const placed = Array.from({ length: MAX_PLACED }, (_, index) => ({
    id: `f_${"long_furniture_name_padding_x".slice(0, 26)}${String(index).padStart(3, "0")}`,
    x: -16, z: -16, rot: 3,
  }));
  const insects = Array.from({ length: MAX_INSECTS }, (_, index) => ({
    insectId: `${"insect_name_padded_to_the_longest_allowed_len".slice(0, 46)}${index}`,
    level: 80, shiny: false,
  }));
  const stored = JSON.stringify(sanitizeIslandSnapshot(snapshot({ comfort: 9999, placed, insects })));
  assert.equal(JSON.parse(stored).placed.length, MAX_PLACED);
  assert.equal(JSON.parse(stored).insects.length, MAX_INSECTS);
  assert.ok(stored.length <= MAX_ISLAND_JSON_LENGTH, `stored snapshot is ${stored.length} chars`);
});

test("empty request fields count as absent when resolving the island target", () => {
  // 클라이언트는 액션과 무관하게 다섯 필드를 전부 보낸다.
  const wire = (overrides) => ({
    action: "getIsland", island: "", isPublic: false, friendCode: "", targetUid: "", ...overrides,
  });
  assert.deepEqual(resolveIslandTarget(wire({ friendCode: "ab12cd34" })),
    { targetUid: "", friendCode: "AB12CD34" });
  assert.deepEqual(resolveIslandTarget(wire({ targetUid: "   ", friendCode: "  ab12cd34 \n" })),
    { targetUid: "", friendCode: "AB12CD34" });
  assert.deepEqual(resolveIslandTarget(wire({ targetUid: "uid-9", friendCode: "AB12CD34" })),
    { targetUid: "uid-9", friendCode: "" }, "targetUid wins when both are present");
  assert.deepEqual(resolveIslandTarget(wire({ targetUid: "  uid-9  " })),
    { targetUid: "uid-9", friendCode: "" });
  assert.equal(resolveIslandTarget(wire({})), null);
  assert.equal(resolveIslandTarget(wire({ targetUid: " ", friendCode: "\t" })), null);
  assert.equal(resolveIslandTarget({ action: "getIsland" }), null);
  assert.equal(resolveIslandTarget(undefined), null);
  assert.equal(resolveIslandTarget(null), null);
  assert.equal(resolveIslandTarget({ targetUid: 42, friendCode: { code: "AB12CD34" } }), null,
    "non-string values are not targets");
  // likeIsland는 uid만 넘긴다 — 친구 코드가 있어도 대상이 되지 않는다.
  assert.equal(resolveIslandTarget({ targetUid: wire({ friendCode: "AB12CD34" }).targetUid }), null);
});

test("utcDayKey is the UTC calendar date", () => {
  assert.equal(utcDayKey(new Date("2026-10-02T00:00:00.000Z")), "2026-10-02");
  assert.equal(utcDayKey(new Date("2026-10-02T23:59:59.999Z")), "2026-10-02");
  // 한국 시간 10월 3일 08:59는 UTC로 아직 10월 2일이다.
  assert.equal(utcDayKey(new Date("2026-10-03T08:59:00+09:00")), "2026-10-02");
  assert.equal(utcDayKey(new Date("2026-10-03T09:00:00+09:00")), "2026-10-03");
  assert.equal(utcDayKey(new Date("2026-01-05T12:00:00Z")), "2026-01-05");
  assert.equal(utcDayKey(Date.UTC(2027, 0, 1, 0, 0, 0)), "2027-01-01");
  assert.match(utcDayKey(), /^\d{4}-\d{2}-\d{2}$/);
});

test("IslandInfo always carries the nine contract fields with fixed types", () => {
  const keys = [
    "ownerUid", "ownerName", "friendCode", "isPublic", "likes", "visits",
    "likedByMe", "updatedAtMs", "snapshot",
  ];
  const empty = buildIslandInfo("uid-1", {}, { ownerName: "테스터", friendCode: "ABCD1234" });
  assert.deepEqual(Object.keys(empty), keys);
  assert.deepEqual(empty, {
    ownerUid: "uid-1", ownerName: "테스터", friendCode: "ABCD1234", isPublic: false,
    likes: 0, visits: 0, likedByMe: false, updatedAtMs: 0, snapshot: "",
  });

  const stored = {
    uid: "uid-1", ownerName: "저장된 이름", friendCode: "FFFF0000", isPublic: true,
    likes: 7, visits: 31, updatedAtMs: 1790000000000, snapshot: "{\"version\":1}",
    updatedAt: { sentinel: true },
  };
  const listed = buildIslandInfo("uid-1", stored, { ownerName: "폴백", friendCode: "ABCD1234" });
  assert.deepEqual(Object.keys(listed), keys);
  assert.equal(listed.ownerName, "저장된 이름");
  assert.equal(listed.friendCode, "FFFF0000");
  assert.equal(listed.isPublic, true);
  assert.equal(listed.likes, 7);
  assert.equal(listed.visits, 31);
  assert.equal(listed.updatedAtMs, 1790000000000);
  assert.equal(listed.likedByMe, false);
  assert.equal(listed.snapshot, "", "only getIsland fills the snapshot");

  const visited = buildIslandInfo("uid-1", stored, { likedByMe: true, includeSnapshot: true });
  assert.equal(visited.likedByMe, true);
  assert.equal(visited.snapshot, "{\"version\":1}");
});
