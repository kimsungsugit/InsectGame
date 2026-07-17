const crypto = require("node:crypto");

const GEM_PRODUCTS = Object.freeze({
  gem_200: 150,
  gem_550: 400,
  gem_1200: 900,
});

function parseJson(value, label) {
  if (value && typeof value === "object") return value;
  if (typeof value !== "string" || value.length === 0) {
    throw new Error(`${label} is missing`);
  }
  try {
    return JSON.parse(value);
  } catch {
    throw new Error(`${label} is not valid JSON`);
  }
}

function parseUnityGoogleReceipt(receipt) {
  const unified = parseJson(receipt, "receipt");
  if (unified.Store !== "GooglePlay") {
    throw new Error("receipt store is not GooglePlay");
  }

  const payload = parseJson(unified.Payload, "receipt payload");
  const purchase = parseJson(payload.json, "Google purchase data");
  const productId = purchase.productId
    || (Array.isArray(purchase.products) ? purchase.products[0] : null);

  if (!purchase.purchaseToken || !purchase.packageName || !productId) {
    throw new Error("Google purchase data is incomplete");
  }

  return {
    packageName: purchase.packageName,
    productId,
    purchaseToken: purchase.purchaseToken,
    orderId: purchase.orderId || unified.TransactionID || "",
  };
}

function getRewardCount(productId) {
  const rewardCount = GEM_PRODUCTS[productId];
  if (!Number.isSafeInteger(rewardCount) || rewardCount <= 0) {
    throw new Error("unknown productId");
  }
  return rewardCount;
}

function hashPurchaseToken(token) {
  return crypto.createHash("sha256").update(token, "utf8").digest("hex");
}

function isGrantableGooglePurchase(purchase) {
  return Boolean(purchase)
    && purchase.purchaseState === 0
    && purchase.consumptionState !== 1;
}

module.exports = {
  GEM_PRODUCTS,
  getRewardCount,
  hashPurchaseToken,
  isGrantableGooglePurchase,
  parseUnityGoogleReceipt,
};
