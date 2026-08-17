using System.Collections.Generic;
using InsectGame.Battle;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 전투 중 화면 위쪽에 잠깐 떠오르는 짧은 문구 — "효과가 굉장했다!", "쓰러졌다!", "빗나갔다!",
    /// 그리고 방금 쓴 스킬 이름. 목록의 주인은 <see cref="BattleArenaController"/>이고 여기선 그리기만 한다.
    ///
    /// <b>왜 UI 쪽으로 옮겨왔나.</b> 예전엔 <c>BattleArenaController</c>가 자기 <c>OnGUI</c>에서 직접
    /// 그렸는데, 거긴 <see cref="UIScale"/> 밖이라 <b>실제 픽셀 좌표</b>였다. 나머지 전투 UI는 전부
    /// 가상 캔버스(가로 1920×1080 / 세로 1080×1920) 안에서 그려지므로, 스케일이 1이 아닌 화면에서는
    /// 이 문구만 혼자 어긋났다 — 1440×3200 폰이나 2560×1440 데스크톱은 스케일이 1.333이라 같은
    /// <c>fontSize = 40</c>이 다른 라벨보다 <b>25% 작게</b> 찍히고, <c>Screen.height * 0.35</c>도
    /// 가상 좌표 기준보다 아래로 내려갔다. 1920×1080에서만 우연히 맞아서 Game View로는 안 보였다.
    ///
    /// 아레나(Battle)가 <see cref="UIScale"/>를 직접 부를 수는 없다 — UI가 이미 Battle을 참조하므로
    /// 순환이 된다(<c>rules/architecture.md</c>). 그리는 쪽을 UI로 옮기는 게 방향에 맞고,
    /// <c>GetActiveEffectTexts()</c>가 공개인 것도 원래 그 이음매로 만들어 둔 것이다.
    /// </summary>
    public static class BattleEffectTextOverlay
    {
        private static GUIStyle textStyle;
        private static GUIStyle shadowStyle;

        /// <summary>기준 폰트. 위로 떠오르는 짧은 강조 문구라 본문보다 크다.</summary>
        private const int FontSize = 40;

        /// <summary>40px 글자의 한글 세로 폭(약 54)에 위아래 여백까지. 50이던 자리라 받침이 잘렸다.</summary>
        private const float RowHeight = 58f;
        private const float RowSpacing = 64f;
        private const float MaxWidth = 760f;

        /// <summary>떠오르는 거리(가상 px). 사라지는 동안 이만큼 위로 올라간다.</summary>
        private const float RiseDistance = 40f;

        /// <summary>
        /// 호출부는 <c>UIScale.Begin()</c>과 <c>UIScale.End()</c> <b>사이</b>에서 불러야 한다 —
        /// 좌표가 가상 캔버스 기준이다.
        /// </summary>
        public static void Draw(BattleArenaController arena)
        {
            if (arena == null || !arena.IsActive) return;

            IReadOnlyList<BattleArenaController.EffectTextEntry> texts = arena.GetActiveEffectTexts();
            if (texts == null || texts.Count == 0) return;

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = FontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                shadowStyle = new GUIStyle(textStyle);
            }

            float now = Time.time;
            float centerX = UIScale.VirtualScreenWidth * 0.5f;
            float baseY = UIScale.VirtualScreenHeight * 0.35f;
            float width = Mathf.Min(MaxWidth, UIScale.ContentWidth());

            Color previous = GUI.color;
            for (int i = 0; i < texts.Count; i++)
            {
                BattleArenaController.EffectTextEntry entry = texts[i];
                if (entry == null) continue;

                float alpha = 1f - Mathf.Clamp01((now - entry.startTime) / Mathf.Max(0.0001f, entry.duration));
                float y = baseY + i * RowSpacing - RiseDistance * (1f - alpha);
                Rect rect = new Rect(centerX - width * 0.5f, y, width, RowHeight);

                // 스킬 이름이 그대로 들어오므로(`{skill.displayName}!`) 길이를 데이터가 정한다 —
                // 가운데 정렬이라 넘치면 앞뒤가 같이 잘린다. LabelFit이 폭·높이를 함께 본다.
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.7f);
                UIHelper.LabelFit(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height),
                    entry.text, shadowStyle);

                Color color = entry.color;
                color.a *= alpha;
                GUI.color = color;
                UIHelper.LabelFit(rect, entry.text, textStyle);
            }
            GUI.color = previous;
        }
    }
}
