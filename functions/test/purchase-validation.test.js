const test = require("node:test");
const assert = require("node:assert/strict");
const {
  getRewardCount,
  hashPurchaseToken,
  isGrantableGooglePurchase,
  parseUnityGoogleReceipt,
} = require("../purchase-validation");

function makeReceipt(overrides = {}) {
  const purchase = {
    packageName: "com.insectexploration.game",
    productId: "gem_200",
    purchaseToken: "token-123",
    orderId: "GPA.1234",
    ...overrides,
  };
  return JSON.stringify({
    Store: "GooglePlay",
    TransactionID: purchase.orderId,
    Payload: JSON.stringify({ json: JSON.stringify(purchase), signature: "signature" }),
  });
}

test("parses Unity Google Play unified receipt", () => {
  assert.deepEqual(parseUnityGoogleReceipt(makeReceipt()), {
    packageName: "com.insectexploration.game",
    productId: "gem_200",
    purchaseToken: "token-123",
    orderId: "GPA.1234",
  });
});

test("supports Billing products array", () => {
  const receipt = makeReceipt({ productId: undefined, products: ["gem_550"] });
  assert.equal(parseUnityGoogleReceipt(receipt).productId, "gem_550");
});

test("rejects non Google Play receipts", () => {
  const receipt = JSON.stringify({ Store: "AppleAppStore", Payload: "{}" });
  assert.throws(() => parseUnityGoogleReceipt(receipt), /not GooglePlay/);
});

test("only configured products produce rewards", () => {
  assert.equal(getRewardCount("gem_1200"), 900);
  assert.throws(() => getRewardCount("unknown"), /unknown productId/);
});

test("purchase token hashes are deterministic and do not expose the token", () => {
  const hash = hashPurchaseToken("token-123");
  assert.equal(hash.length, 64);
  assert.equal(hash, hashPurchaseToken("token-123"));
  assert.equal(hash.includes("token-123"), false);
});

test("only completed and unconsumed Google purchases are grantable", () => {
  assert.equal(isGrantableGooglePurchase({ purchaseState: 0, consumptionState: 0 }), true);
  assert.equal(isGrantableGooglePurchase({ purchaseState: 0 }), true);
  assert.equal(isGrantableGooglePurchase({ purchaseState: 1, consumptionState: 0 }), false);
  assert.equal(isGrantableGooglePurchase({ purchaseState: 2, consumptionState: 0 }), false);
  assert.equal(isGrantableGooglePurchase({ purchaseState: 0, consumptionState: 1 }), false);
});
