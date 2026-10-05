using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 스토리 영상 화면(<c>StoryVideoDirector</c>)의 자막 띠·「건너뛰기」 자리 — <b>순수 계산</b>. 가상 좌표(<see cref="HudFrame"/>)를 받는다.
    ///
    /// 아이가 읽는 자막이라 글자는 36pt 굵게, 두 줄까지 들어가는 칸, 진한 띠(0.68)다. 「건너뛰기」는 가로 화면에서는 오른쪽 아래,
    /// <b>세로 화면에서는 오른쪽 위</b>다 — 세로에서는 자막 띠가 화면 폭을 거의 다 써서 아래에 두면 띠와 겹친다(예전 배치는 세로 720×1280에서 가로 224px·세로 2px가 겹쳤다).
    /// 가로에서도 노치 인셋이 커서 띠와 버튼이 맞닿으면 띠를 버튼 위로 올린다. 그림은 QA 빌드 <c>-battleScenario story-video</c>.
    /// </summary>
    public static class StoryVideoScreenLayout
    {
        public const int SubtitleFontSize = 36;
        public const int SkipFontSize = 28;
        /// <summary>자막 띠의 검정 불투명도(자막 페이드가 이 위에 곱해진다).</summary>
        public const float SubtitleBandAlpha = 0.68f;
        /// <summary>자막 칸에 들어가야 하는 줄 수.</summary>
        public const int SubtitleLines = 2;
        public const float SubtitleMaxWidth = 1200f;
        /// <summary>세이프 에어리어 안쪽 좌우 여백(띠가 화면 끝에 붙지 않게).</summary>
        public const float SubtitleSideMargin = 40f;
        public const float SubtitlePadX = 28f;
        public const float SubtitlePadY = 12f;
        public const float SkipWidth = 240f;
        public const float SkipHeight = 56f;
        /// <summary>세이프 에어리어 오른쪽 끝과 「건너뛰기」 사이.</summary>
        public const float SkipInset = 24f;
        /// <summary>띠를 버튼 위로 올릴 때 둘 사이.</summary>
        public const float Gap = 16f;

        /// <summary>한글 줄높이 — <c>literal_fit_lint</c>·<c>DexScreenUI.LineH</c>와 같은 식(fontSize × 1.35).</summary>
        public static float LineHeight(int fontSize) => Mathf.Ceil(fontSize * 1.35f);

        /// <summary>자막 글자 칸 높이 — 두 줄 + 라벨 안쪽 여백 몫.</summary>
        public static float SubtitleTextHeight => LineHeight(SubtitleFontSize) * SubtitleLines + 4f;

        public static float SubtitleBandHeight => SubtitleTextHeight + SubtitlePadY * 2f;

        public static Rect Skip(HudFrame f)
        {
            float x = f.Width - f.SafeRight - SkipInset - SkipWidth;
            float y = f.Portrait ? f.ContentTop : f.ContentBottom - SkipHeight;
            return new Rect(x, y, SkipWidth, SkipHeight);
        }

        /// <summary>자막 띠 — 세이프 에어리어 가운데, 콘텐츠 아래 끝에 붙는다. 「건너뛰기」와 겹치면 그 위로 올린다.</summary>
        public static Rect SubtitleBand(HudFrame f)
        {
            float room = Mathf.Max(1f, f.Width - f.SafeLeft - f.SafeRight);
            float w = Mathf.Max(1f, Mathf.Min(SubtitleMaxWidth, room - SubtitleSideMargin * 2f));
            float x = f.SafeLeft + (room - w) * 0.5f;
            float h = SubtitleBandHeight;
            var band = new Rect(x, f.ContentBottom - h, w, h);
            Rect skip = Skip(f);
            if (band.Overlaps(skip)) band.y = skip.y - Gap - h;
            return band;
        }

        /// <summary>띠 안의 글자 칸.</summary>
        public static Rect SubtitleText(Rect band) => new Rect(
            band.x + SubtitlePadX,
            band.y + SubtitlePadY,
            Mathf.Max(1f, band.width - SubtitlePadX * 2f),
            Mathf.Max(1f, band.height - SubtitlePadY * 2f));
    }
}
