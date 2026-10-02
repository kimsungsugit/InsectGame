using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 섬 화면 여섯 개(HUD·꾸미기·상점·곤충·방문·가이드)가 함께 쓰는 스타일과 문구.
    /// 화면마다 GUIStyle과 결과 문구 switch를 따로 두면 같은 말이 화면마다 다르게 적힌다.
    /// 색은 전부 <see cref="UITheme"/> 토큰에서 받는다.
    /// </summary>
    internal static class IslandUiKit
    {
        // 키가 상수이고 람다가 아무것도 캡처하지 않아 적중 경로가 무할당이다(UIHelper.CachedStyle 주석 참조).
        public static GUIStyle Title => UIHelper.CachedStyle("island_title",
            () => Make(30, FontStyle.Bold, TextAnchor.MiddleLeft, false));

        public static GUIStyle Body => UIHelper.CachedStyle("island_body",
            () => Make(24, FontStyle.Normal, TextAnchor.MiddleLeft, false));

        public static GUIStyle BodyWrap => UIHelper.CachedStyle("island_body_wrap",
            () => Make(24, FontStyle.Normal, TextAnchor.UpperLeft, true));

        public static GUIStyle BodyCenter => UIHelper.CachedStyle("island_body_center",
            () => Make(24, FontStyle.Normal, TextAnchor.MiddleCenter, false));

        public static GUIStyle Small => UIHelper.CachedStyle("island_small",
            () => Make(20, FontStyle.Normal, TextAnchor.MiddleLeft, false));

        public static GUIStyle SmallCenter => UIHelper.CachedStyle("island_small_center",
            () => Make(20, FontStyle.Normal, TextAnchor.MiddleCenter, false));

        public static GUIStyle Button => UIHelper.CachedStyle("island_button",
            () => Make(24, FontStyle.Bold, TextAnchor.MiddleCenter, false));

        public static GUIStyle ButtonSmall => UIHelper.CachedStyle("island_button_small",
            () => Make(20, FontStyle.Bold, TextAnchor.MiddleCenter, false));

        private static GUIStyle Make(int size, FontStyle fontStyle, TextAnchor anchor, bool wrap)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = anchor,
                wordWrap = wrap,
                richText = false,
            };
            style.normal.textColor = UITheme.Instance.textPrimary;
            return style;
        }

        /// <summary>
        /// 색을 입혀 맞춰 그린다. 스타일이 공유 캐시라 색을 바꾼 채 두면 다음 호출부가 그 색으로 그린다 — 되돌린다.
        /// </summary>
        public static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = color;
            UIHelper.LabelFit(rect, text, style);
            style.normal.textColor = previous;
        }

        public static string CategoryLabel(IslandObjectCategory category)
        {
            switch (category)
            {
                case IslandObjectCategory.Building: return "건물";
                case IslandObjectCategory.Furniture: return "가구";
                case IslandObjectCategory.Terrain: return "지형지물";
                default: return "도구";
            }
        }

        public static Color CategoryColor(IslandObjectCategory category)
        {
            UITheme t = UITheme.Instance;
            switch (category)
            {
                case IslandObjectCategory.Building: return t.accentCoral;
                case IslandObjectCategory.Furniture: return t.accentAmber;
                case IslandObjectCategory.Terrain: return t.accentMint;
                default: return t.insectRare;
            }
        }

        /// <summary>물건 효과 한 줄. 효과가 없으면 빈 문자열.</summary>
        public static string EffectText(IslandObjectDef def)
        {
            if (def == null) return string.Empty;
            switch (def.effect)
            {
                case IslandEffectKind.YieldBonus: return "생산 +" + Mathf.RoundToInt(def.effectValue * 100f) + "%";
                case IslandEffectKind.CapHours: return "누적 +" + def.effectValue.ToString("0.#") + "시간";
                case IslandEffectKind.BondSpeed: return "친밀도 속도 +" + Mathf.RoundToInt(def.effectValue * 100f) + "%";
                default: return string.Empty;
            }
        }

        public static string PlaceResultText(IslandPlaceResult result)
        {
            switch (result)
            {
                case IslandPlaceResult.Ok: return "놓았습니다";
                case IslandPlaceResult.OutOfBounds: return "섬 밖에는 놓을 수 없습니다";
                case IslandPlaceResult.Reserved: return "나루터 앞은 비워 두어야 합니다";
                case IslandPlaceResult.Overlap: return "다른 물건과 겹칩니다";
                case IslandPlaceResult.NotOwned: return "보관함에 남은 것이 없습니다";
                default: return "놓을 수 없는 물건입니다";
            }
        }

        public static string BuyResultText(IslandBuyResult result)
        {
            switch (result)
            {
                case IslandBuyResult.Ok: return "구매했습니다";
                case IslandBuyResult.NotEnoughCoins: return "코인이 부족합니다";
                case IslandBuyResult.NotEnoughGems: return "다이아가 부족합니다";
                case IslandBuyResult.Maxed: return "더 넓힐 수 없습니다";
                case IslandBuyResult.Locked: return "섬을 더 넓혀야 살 수 있습니다";
                default: return "살 수 없는 물건입니다";
            }
        }

        public static string ReleaseResultText(IslandReleaseResult result)
        {
            switch (result)
            {
                case IslandReleaseResult.Ok: return "섬에 풀어놓았습니다";
                case IslandReleaseResult.AlreadyReleased: return "이미 섬에 있습니다";
                case IslandReleaseResult.NoFreeSlot: return "빈 자리가 없습니다 — 상점의 [확장]에서 자리를 늘릴 수 있어요";
                default: return "그 곤충을 찾을 수 없습니다";
            }
        }

        // 하트 문자열은 여섯 가지뿐이라 미리 만들어 둔다 — 곤충 목록이 행마다 매 프레임 이어 붙이지 않게.
        private static readonly string[] HeartStrings =
        {
            "♡♡♡♡♡", "♥♡♡♡♡", "♥♥♡♡♡", "♥♥♥♡♡", "♥♥♥♥♡", "♥♥♥♥♥",
        };

        public static string Hearts(int bondLevel)
        {
            return HeartStrings[Mathf.Clamp(bondLevel, 0, HeartStrings.Length - 1)];
        }
    }
}
