const { onRequest } = require("firebase-functions/v2/https");
const { initializeApp } = require("firebase-admin/app");
const { getAuth } = require("firebase-admin/auth");
const { FieldValue, getFirestore } = require("firebase-admin/firestore");
const { google } = require("googleapis");
const {
  getRewardCount,
  hashPurchaseToken,
  isGrantableGooglePurchase,
  parseUnityGoogleReceipt,
} = require("./purchase-validation");
const { createSocialPvpHandler } = require("./social-api");

initializeApp();

const PACKAGE_NAME = "com.insectexploration.game";
const REGION = "asia-northeast3";

exports.socialPvpApi = onRequest(
  { region: REGION, timeoutSeconds: 60, cors: false },
  createSocialPvpHandler(),
);

function sendJson(response, status, body) {
  response.status(status).set("Cache-Control", "no-store").json(body);
}

async function authenticate(request) {
  const authorization = request.get("authorization") || "";
  if (!authorization.startsWith("Bearer ")) throw new Error("missing bearer token");
  return getAuth().verifyIdToken(authorization.slice(7), true);
}

exports.verifyGooglePlayPurchase = onRequest(
  { region: REGION, timeoutSeconds: 60, cors: false },
  async (request, response) => {
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

    try {
      const requestedProductId = request.body && request.body.productId;
      const parsed = parseUnityGoogleReceipt(request.body && request.body.receipt);
      if (requestedProductId !== parsed.productId || parsed.packageName !== PACKAGE_NAME) {
        sendJson(response, 400, { success: false, error: "purchase_identity_mismatch" });
        return;
      }

      const rewardCount = getRewardCount(parsed.productId);
      const auth = new google.auth.GoogleAuth({
        scopes: ["https://www.googleapis.com/auth/androidpublisher"],
      });
      const publisher = google.androidpublisher({ version: "v3", auth });
      const playResponse = await publisher.purchases.products.get({
        packageName: PACKAGE_NAME,
        productId: parsed.productId,
        token: parsed.purchaseToken,
      });
      const playPurchase = playResponse.data || {};

      // purchaseState: 0=Purchased, 1=Canceled, 2=Pending.
      // consumptionState: 1이면 과거에 이미 소비된 토큰이므로 신규 지급하지 않는다.
      if (!isGrantableGooglePurchase(playPurchase)) {
        sendJson(response, 409, { success: false, error: "purchase_not_completed" });
        return;
      }

      const db = getFirestore();
      const purchaseHash = hashPurchaseToken(parsed.purchaseToken);
      const purchaseRef = db.collection("iapPurchases").doc(purchaseHash);
      const userRef = db.collection("users").doc(decoded.uid);
      let gems = 0;
      let newlyGranted = false;

      await db.runTransaction(async (transaction) => {
        const purchaseSnapshot = await transaction.get(purchaseRef);
        const userSnapshot = await transaction.get(userRef);
        const currentGems = userSnapshot.exists
          && Number.isSafeInteger(userSnapshot.get("gems"))
          ? userSnapshot.get("gems")
          : 0;

        if (purchaseSnapshot.exists) {
          if (purchaseSnapshot.get("uid") !== decoded.uid
              || purchaseSnapshot.get("productId") !== parsed.productId) {
            throw new Error("purchase_token_already_claimed");
          }
          gems = currentGems;
          return;
        }

        gems = currentGems + rewardCount;
        newlyGranted = true;
        transaction.set(userRef, {
          gems,
          lastPurchaseAt: FieldValue.serverTimestamp(),
        }, { merge: true });
        transaction.create(purchaseRef, {
          uid: decoded.uid,
          productId: parsed.productId,
          rewardCount,
          orderId: playPurchase.orderId || parsed.orderId,
          purchaseTimeMillis: playPurchase.purchaseTimeMillis || null,
          createdAt: FieldValue.serverTimestamp(),
        });
      });

      sendJson(response, 200, {
        success: true,
        gems,
        newlyGranted,
        rewardCount,
      });
    } catch (error) {
      console.error("verifyGooglePlayPurchase failed", error);
      const knownConflict = error && error.message === "purchase_token_already_claimed";
      sendJson(response, knownConflict ? 409 : 500, {
        success: false,
        error: knownConflict ? error.message : "verification_failed",
      });
    }
  },
);
