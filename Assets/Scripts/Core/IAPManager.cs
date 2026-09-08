using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Purchasing;

namespace InsectGame.Core
{
    /// <summary>
    /// Unity IAP v5 Google Play Billing 어댑터.
    /// 구매는 Firebase Functions의 서버 검증이 성공하기 전까지 ConfirmPurchase 하지 않는다.
    /// 검증 서버가 없거나 인증되지 않은 릴리스에서는 IsReady=false로 구매를 차단한다.
    /// </summary>
    public class IAPManager : MonoBehaviour, IPurchaseProvider
    {
        [Serializable]
        private class PurchaseVerificationRequest
        {
            public string productId;
            public string transactionId;
            public string receipt;
        }

        [Serializable]
        private class PurchaseVerificationResponse
        {
            public bool success;
            public int gems;
            public bool newlyGranted;
            public int rewardCount;
            public string error;
        }

        private StoreController storeController;
        private bool productsReady;
        private readonly Dictionary<string, Action<bool>> pending =
            new Dictionary<string, Action<bool>>();
        private readonly HashSet<string> verifyingOrders = new HashSet<string>();

        public bool IsReady => productsReady
            && FirebaseConfig.IsPurchaseVerificationConfigured
            && AuthManager.Instance != null
            && AuthManager.Instance.IsLoggedIn;

        private void Start()
        {
            if (CashShopManager.Instance != null)
                CashShopManager.Instance.SetPurchaseProvider(this);
            InitializePurchasing();
        }

        private void OnDestroy()
        {
            if (storeController == null) return;
            storeController.OnStoreConnected -= OnStoreConnected;
            storeController.OnStoreDisconnected -= OnStoreDisconnected;
            storeController.OnProductsFetched -= OnProductsFetched;
            storeController.OnProductsFetchFailed -= OnProductsFetchFailed;
            storeController.OnPurchasePending -= OnPurchasePending;
            storeController.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            storeController.OnPurchaseFailed -= OnPurchaseFailed;
            storeController.OnPurchaseDeferred -= OnPurchaseDeferred;
        }

        public void Purchase(string productId, Action<bool> onComplete)
        {
            if (!IsReady || string.IsNullOrEmpty(productId) || pending.ContainsKey(productId))
            {
                onComplete?.Invoke(false);
                return;
            }

            Product product = storeController.GetProductById(productId);
            if (product == null || !product.availableToPurchase)
            {
                onComplete?.Invoke(false);
                return;
            }

            ConfigureGoogleAccountId();
            pending[productId] = onComplete;
            try
            {
                storeController.PurchaseProduct(product);
            }
            catch (Exception e)
            {
                CompletePending(productId, false);
                Debug.LogWarning("[IAP] 구매 시작 실패: " + e.Message);
            }
        }

        public string GetLocalizedPrice(string productId)
        {
            if (storeController == null) return null;
            Product product = storeController.GetProductById(productId);
            return product != null && product.metadata != null
                ? product.metadata.localizedPriceString
                : null;
        }

        private async void InitializePurchasing()
        {
            try
            {
                storeController = UnityIAPServices.StoreController();
                storeController.OnStoreConnected += OnStoreConnected;
                storeController.OnStoreDisconnected += OnStoreDisconnected;
                storeController.OnProductsFetched += OnProductsFetched;
                storeController.OnProductsFetchFailed += OnProductsFetchFailed;
                storeController.OnPurchasePending += OnPurchasePending;
                storeController.OnPurchaseConfirmed += OnPurchaseConfirmed;
                storeController.OnPurchaseFailed += OnPurchaseFailed;
                storeController.OnPurchaseDeferred += OnPurchaseDeferred;
                await storeController.Connect();
            }
            catch (Exception e)
            {
                productsReady = false;
                Debug.LogWarning("[IAP] 스토어 연결 실패: " + e.Message);
            }
        }

        private void OnStoreConnected()
        {
            List<ProductDefinition> products = new List<ProductDefinition>();
            if (CashShopManager.Instance != null)
            {
                foreach (CashShopItem item in CashShopManager.Instance.GetGemPackages())
                    products.Add(new ProductDefinition(item.itemId, ProductType.Consumable));
            }

            if (products.Count == 0)
            {
                Debug.LogWarning("[IAP] 등록할 보석 상품이 없습니다.");
                return;
            }
            storeController.FetchProducts(products);
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription description)
        {
            productsReady = false;
            Debug.LogWarning("[IAP] 스토어 연결 끊김: "
                + (description != null ? description.message : "unknown"));
        }

        private void OnProductsFetched(List<Product> products)
        {
            productsReady = products != null && products.Any(p => p != null && p.availableToPurchase);
            if (!productsReady)
                Debug.LogWarning("[IAP] 구매 가능한 보석 상품을 불러오지 못했습니다.");
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            productsReady = false;
            Debug.LogWarning("[IAP] 상품 조회 실패: "
                + (failure != null ? failure.FailureReason.ToString() : "unknown"));
        }

        private void OnPurchasePending(PendingOrder order)
        {
            Product product = GetFirstProduct(order);
            if (product == null || string.IsNullOrEmpty(order.Info.Receipt))
            {
                Debug.LogWarning("[IAP] 검증할 상품 또는 영수증이 없습니다.");
                return;
            }

            string orderKey = BuildOrderKey(order);
            if (!verifyingOrders.Add(orderKey)) return;
            StartCoroutine(VerifyAndConfirmSafely(order, product, orderKey));
        }

        private IEnumerator VerifyAndConfirmSafely(PendingOrder order, Product product,
            string orderKey)
        {
            CloudSaveManager cloudSave = CloudSaveManager.Instance;
            if (cloudSave != null)
            {
                // 이미 전송 중인 오래된 스냅샷이 끝난 뒤 서버 잔액을 갱신한다.
                while (cloudSave.IsSaving) yield return null;
                cloudSave.SetPremiumTransactionInProgress(true);
            }

            yield return VerifyAndConfirm(order, product, orderKey, true);

            if (cloudSave != null)
                cloudSave.SetPremiumTransactionInProgress(false);
        }

        private IEnumerator VerifyAndConfirm(PendingOrder order, Product product,
            string orderKey, bool allowTokenRefresh)
        {
            if (!FirebaseConfig.IsPurchaseVerificationConfigured
                || AuthManager.Instance == null
                || !AuthManager.Instance.IsLoggedIn)
            {
                verifyingOrders.Remove(orderKey);
                CompletePending(product.definition.id, false);
                yield break;
            }

            PurchaseVerificationRequest payload = new PurchaseVerificationRequest
            {
                productId = product.definition.id,
                transactionId = order.Info.TransactionID ?? string.Empty,
                receipt = order.Info.Receipt
            };

            string responseText = null;
            long responseCode;
            UnityWebRequest.Result result;
            using (UnityWebRequest request = new UnityWebRequest(
                FirebaseConfig.PurchaseVerificationUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization",
                    "Bearer " + AuthManager.Instance.IdToken);
                yield return request.SendWebRequest();
                responseCode = request.responseCode;
                result = request.result;
                responseText = request.downloadHandler != null
                    ? request.downloadHandler.text
                    : null;
            }

            if ((responseCode == 401 || responseCode == 403) && allowTokenRefresh)
            {
                bool refreshed = false;
                yield return AuthManager.Instance.TryRefreshTokenForRetry(v => refreshed = v);
                if (refreshed)
                {
                    yield return VerifyAndConfirm(order, product, orderKey, false);
                    yield break;
                }
            }

            PurchaseVerificationResponse verified = null;
            if (result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(responseText))
            {
                try { verified = JsonUtility.FromJson<PurchaseVerificationResponse>(responseText); }
                catch (Exception e) { Debug.LogWarning("[IAP] 검증 응답 파싱 실패: " + e.Message); }
            }

            if (verified == null || !verified.success || CashShopManager.Instance == null
                // newlyGranted를 함께 넘긴다 — 이미 청구된 토큰의 재검증이면 서버가 false와
                // 마지막 PATCH 시점 잔액을 준다. 그걸 절대 세팅하면 그 사이 로컬 젬 변동이 뒤집힌다.
                || !CashShopManager.Instance.ApplyVerifiedGemBalance(
                    product.definition.id, verified.gems, verified.newlyGranted))
            {
                verifyingOrders.Remove(orderKey);
                CompletePending(product.definition.id, false);
                Debug.LogWarning("[IAP] 서버 검증 실패. 구매는 미확정 상태로 재시도됩니다: "
                    + (verified != null ? verified.error : "HTTP " + responseCode));
                yield break;
            }

            // 서버가 토큰 중복방지와 잔액 갱신을 끝낸 뒤에만 소비성 구매를 확정/소비한다.
            try
            {
                storeController.ConfirmPurchase(order);
                CompletePending(product.definition.id, true);
            }
            catch (Exception e)
            {
                // 서버 지급은 이미 원자적으로 완료됐다. 다음 시작 시 동일 토큰을 재검증한 뒤 재확정한다.
                Debug.LogWarning("[IAP] 구매 확정 재시도 필요: " + e.Message);
                CompletePending(product.definition.id, true);
            }
            finally
            {
                verifyingOrders.Remove(orderKey);
            }
        }

        private void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed)
                Debug.LogWarning("[IAP] 구매 확정 실패: " + failed.Details);
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            Product product = GetFirstProduct(order);
            if (product != null) CompletePending(product.definition.id, false);
            Debug.LogWarning("[IAP] 구매 실패: "
                + (order != null ? order.FailureReason + " " + order.Details : "unknown"));
        }

        private void OnPurchaseDeferred(DeferredOrder order)
        {
            Product product = GetFirstProduct(order);
            if (product != null) CompletePending(product.definition.id, false);
            Debug.Log("[IAP] 보호자 승인 등으로 구매가 보류되었습니다.");
        }

        private void ConfigureGoogleAccountId()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (storeController == null || AuthManager.Instance == null
                || string.IsNullOrEmpty(AuthManager.Instance.UserId)) return;
            IGooglePlayStoreExtendedService google = storeController.GooglePlayStoreExtendedService;
            if (google != null)
                google.SetObfuscatedAccountId(HashIdentifier(AuthManager.Instance.UserId));
#endif
        }

        private void CompletePending(string productId, bool success)
        {
            if (string.IsNullOrEmpty(productId)) return;
            if (!pending.TryGetValue(productId, out Action<bool> callback)) return;
            pending.Remove(productId);
            callback?.Invoke(success);
        }

        private static Product GetFirstProduct(Order order)
        {
            return order?.CartOrdered?.Items()?.FirstOrDefault()?.Product;
        }

        private static string BuildOrderKey(Order order)
        {
            string transactionId = order?.Info?.TransactionID;
            if (!string.IsNullOrEmpty(transactionId)) return transactionId;
            return HashIdentifier(order?.Info?.Receipt ?? Guid.NewGuid().ToString("N"));
        }

        private static string HashIdentifier(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                StringBuilder result = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) result.Append(bytes[i].ToString("x2"));
                return result.ToString();
            }
        }
    }
}
