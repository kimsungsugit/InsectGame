using System;
using System.Collections.Generic;
using UnityEngine;
#if INSECTGAME_IAP
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

namespace InsectGame.Core
{
    /// <summary>
    /// Google Play Billing(Unity IAP) 어댑터. CashShopManager에 IPurchaseProvider로 등록된다.
    ///
    /// 활성화 절차(프로덕션 결제 연동):
    ///   1) Package Manager에서 "In-App Purchasing"(com.unity.purchasing) 설치 + 서비스 약관 동의
    ///   2) Play Console에 보석 상품(gem_200/gem_550/gem_1200) 등록 — 상품 ID = CashShopItem.itemId
    ///   3) Player Settings → Scripting Define Symbols 에 <c>INSECTGAME_IAP</c> 추가
    /// 정의가 없으면(기본) Purchasing 의존 코드가 컴파일되지 않아 빌드가 깨지지 않으며,
    /// IsReady=false → CashShopManager가 프로덕션에서 보석 구매를 비활성(무료 지급 안 함).
    /// </summary>
    public class IAPManager : MonoBehaviour, IPurchaseProvider
#if INSECTGAME_IAP
        , IDetailedStoreListener
#endif
    {
        public bool IsReady =>
#if INSECTGAME_IAP
            storeController != null;
#else
            false;
#endif

        public void Purchase(string productId, Action<bool> onComplete)
        {
#if INSECTGAME_IAP
            if (storeController == null) { onComplete?.Invoke(false); return; }
            pending[productId] = onComplete;
            storeController.InitiatePurchase(productId);
#else
            onComplete?.Invoke(false);
#endif
        }

        public string GetLocalizedPrice(string productId)
        {
#if INSECTGAME_IAP
            if (storeController == null) return null;
            Product p = storeController.products.WithID(productId);
            return p != null && p.metadata != null ? p.metadata.localizedPriceString : null;
#else
            return null;
#endif
        }

        private void Start()
        {
            if (CashShopManager.Instance != null)
                CashShopManager.Instance.SetPurchaseProvider(this);
#if INSECTGAME_IAP
            InitializePurchasing();
#endif
        }

#if INSECTGAME_IAP
        private IStoreController storeController;
        private readonly Dictionary<string, Action<bool>> pending = new Dictionary<string, Action<bool>>();

        private void InitializePurchasing()
        {
            if (storeController != null) return;
            ConfigurationBuilder builder =
                ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            if (CashShopManager.Instance != null)
            {
                foreach (CashShopItem pkg in CashShopManager.Instance.GetGemPackages())
                    builder.AddProduct(pkg.itemId, ProductType.Consumable); // 보석=소비성 상품
            }
            UnityPurchasing.Initialize(this, builder);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            storeController = controller;
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            OnInitializeFailed(error, null);
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogWarning("[IAP] 초기화 실패: " + error + " " + message);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            // 권장: 영수증 서버 검증(args.purchasedProduct.receipt → 백엔드). 미검증 시 변조 위험.
            string id = args.purchasedProduct.definition.id;
            // 권위 지급 — product 기준이라 콜백/세션 유실, 재시작 재전달과 무관하게 정확히 1회 지급.
            if (CashShopManager.Instance != null)
                CashShopManager.Instance.GrantGemPackageByProductId(id);
            // UI 피드백(콜백이 살아있으면)
            if (pending.TryGetValue(id, out Action<bool> cb))
            {
                pending.Remove(id);
                cb?.Invoke(true);
            }
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription desc)
        {
            string id = product != null ? product.definition.id : null;
            if (id != null && pending.TryGetValue(id, out Action<bool> cb))
            {
                pending.Remove(id);
                cb?.Invoke(false);
            }
            Debug.LogWarning("[IAP] 구매 실패: " + (desc != null ? desc.message : ""));
        }
#endif
    }
}
