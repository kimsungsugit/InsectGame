using System;

namespace InsectGame.Core
{
    /// <summary>
    /// 실결제 공급자 추상화 — CashShopManager가 결제 모듈(Unity IAP/Google Play Billing)에
    /// 직접 의존하지 않도록 분리. IAPManager가 구현해 SetPurchaseProvider로 등록한다.
    /// (결제 모듈 미설치/미설정 시 공급자가 없거나 IsReady=false → 프로덕션에서 구매 비활성.)
    /// </summary>
    public interface IPurchaseProvider
    {
        /// <summary>스토어 초기화가 완료돼 구매를 받을 수 있는 상태인지.</summary>
        bool IsReady { get; }

        /// <summary>productId 구매를 시작. 완료 시 onComplete(success) 콜백.
        /// success=true는 공급자가 서버 검증과 지급까지 완료했음을 뜻한다.</summary>
        void Purchase(string productId, Action<bool> onComplete);

        /// <summary>스토어의 현지화 가격 문자열(예: "₩2,000", "$1.99"). 미지원/미준비면 null.
        /// 실제 청구액과 일치하도록 UI 표시에 사용(하드코딩 가격 불일치 방지).</summary>
        string GetLocalizedPrice(string productId);
    }
}
