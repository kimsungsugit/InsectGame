using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// IMGUI 2D 도형 원시요소 — 타원·캡슐·실루엣.
    ///
    /// <see cref="UISurface"/>가 <b>패널·카드</b>의 표면을 맡는다면 여기는 <b>그림</b>을 맡는다.
    /// 곤충 포트레이트가 주 소비자다.
    ///
    /// 왜 필요했나
    /// -----------
    /// 곤충 2D는 전부 <c>GUI.DrawTexture(rect, Texture2D.whiteTexture)</c>였다 —
    /// <c>CapturePopupUI</c>의 25종 238개 + <c>DexScreenUI.DrawTinyInsect</c> 44개,
    /// 합쳐 <b>축 정렬 직사각형 282개</b>에 회전 0회·원 0개. 날개도 몸통도 눈도 네모라
    /// 실루엣이 안 살고 값싸 보였다.
    ///
    /// 소프트 디스크 생성은 이미 <c>MinimapUI.MakeDisc</c>(하드 엣지)와
    /// <c>RegionMapUI.MakeSoftDisc</c>(소프트 엣지)에 <b>각각 private으로 중복</b>돼 있었다.
    /// 여기로 합친다 — 텍스처 하나를 세 소비자가 공유한다.
    /// </summary>
    public static class UIShapes
    {
        private const int DiscSize = 64;
        // 외곽 소프트 엣지 폭(픽셀). 확대해서 써도 가장자리가 계단지지 않을 만큼만.
        private const float EdgePixels = 1.5f;

        private static Texture2D discTex;

        /// <summary>
        /// 안티앨리어싱 원형 알파 텍스처(흰색 + 반경 알파). 비균등 스케일로 그리면 타원이 된다.
        /// 지연 생성 후 세션 내내 재사용 — 64×64 RGBA 하나(16KB)뿐이다.
        /// </summary>
        public static Texture2D Disc
        {
            get
            {
                if (discTex != null) return discTex;
                discTex = new Texture2D(DiscSize, DiscSize, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp
                };
                float c = (DiscSize - 1) / 2f;
                float edge = EdgePixels / c;
                for (int y = 0; y < DiscSize; y++)
                {
                    for (int x = 0; x < DiscSize; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        discTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) / edge)));
                    }
                }
                discTex.Apply();
                return discTex;
            }
        }

        /// <summary><paramref name="rect"/>에 내접하는 타원. 정사각이면 원.</summary>
        public static void Ellipse(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Disc);
            GUI.color = prev;
        }

        /// <summary>
        /// 두 점을 잇는 둥근 막대(캡슐). 다리·더듬이처럼 <b>각도가 있는</b> 부위에 쓴다 —
        /// 축 정렬 직사각형으로는 대각선 다리를 그릴 수 없어 곤충이 네모나 보였다.
        /// </summary>
        public static void Capsule(Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 delta = to - from;
            float len = delta.magnitude;
            if (len < 0.01f || thickness <= 0f)
            {
                Ellipse(new Rect(from.x - thickness * 0.5f, from.y - thickness * 0.5f, thickness, thickness), color);
                return;
            }

            Vector2 mid = (from + to) * 0.5f;
            Rect bar = new Rect(mid.x - len * 0.5f, mid.y - thickness * 0.5f, len, thickness);

            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(AngleDegrees(delta), mid);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            // 양 끝을 원으로 덮어 둥근 캡을 만든다(막대만 그리면 끝이 각진다).
            GUI.DrawTexture(new Rect(bar.x - thickness * 0.5f, bar.y, thickness, thickness), Disc);
            GUI.DrawTexture(new Rect(bar.xMax - thickness * 0.5f, bar.y, thickness, thickness), Disc);
            GUI.color = prev;
            GUI.matrix = saved;
        }

        /// <summary>IMGUI 화면 좌표(y 아래로 증가) 기준 각도(도).</summary>
        public static float AngleDegrees(Vector2 delta)
        {
            return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 파트 하나. <paramref name="roundness"/> 0이면 각진 사각형, 1이면 타원, 사이는 섞는다.
        ///
        /// 곤충 포트레이트의 직사각형 호출부가 전부 이걸 지나가므로 <b>여기 하나를 조정하면
        /// 25종이 함께 바뀐다</b>. 벌 배마디 줄무늬처럼 각져야 자연스러운 파트는 0을 넘긴다.
        /// </summary>
        public static void Part(Rect rect, Color color, float roundness = 1f)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            float r = Mathf.Clamp01(roundness);
            Color prev = GUI.color;
            GUI.color = color;
            if (r <= 0.01f)
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            else if (r >= 0.99f)
            {
                GUI.DrawTexture(rect, Disc);
            }
            else
            {
                // 안쪽은 사각형, 바깥 테두리만 타원으로 깎아 중간값을 만든다.
                //
                // ★ 인셋은 roundness에 **비례**한다. 예전엔 `(1f - r)`이라 방향이 뒤집혀 있었다 —
                // r이 클수록(둥글어야 할수록) 덮는 사각형이 커져 **각지게** 보이고, r이 작을수록
                // 사각형이 조각으로 줄어 **둥글게** 보였다. 정확히 반대다.
                // 경계값으로 확인하면 지금이 맞다: r→0이면 인셋 0 → 사각형이 disc를 전부 덮어 각지고,
                // r→1이면 인셋 0.5 → 덮을 사각형이 없어 disc 그대로 타원이다(양쪽 분기와 연속).
                float inset = BlendInsetRatio(r);
                float ix = rect.width * inset;
                float iy = rect.height * inset;
                GUI.DrawTexture(rect, Disc);
                GUI.DrawTexture(new Rect(rect.x + ix, rect.y + iy,
                    rect.width - ix * 2f, rect.height - iy * 2f), Texture2D.whiteTexture);
            }
            GUI.color = prev;
        }

        /// <summary>
        /// 중간 roundness에서 disc 위에 덮는 안쪽 사각형의 <b>변당 인셋 비율</b>(0~0.5).
        ///
        /// 0이면 사각형이 disc를 전부 덮어 각지고, 0.5면 덮을 게 없어 disc 그대로 타원이다.
        /// 그리기는 테스트할 수 없으니 이 수식만 떼어 고정한다 — 부호가 뒤집혔던 자리라 방향이 중요하다.
        /// </summary>
        internal static float BlendInsetRatio(float roundness)
        {
            return Mathf.Clamp01(roundness) * 0.5f;
        }

        /// <summary>
        /// 파트 뒤에 까는 어두운 외곽. 배경과 곤충이 같은 명도일 때 형체가 뭉개지는 걸 막는다.
        /// 파트를 그리기 <b>전에</b> 부른다.
        /// </summary>
        public static void Silhouette(Rect rect, Color color, float pad, float roundness = 1f)
        {
            if (pad <= 0f) return;
            Part(new Rect(rect.x - pad, rect.y - pad, rect.width + pad * 2f, rect.height + pad * 2f),
                color, roundness);
        }

        // ── 화살표(전투 상성 표시) ──

        private const int TriangleSize = 64;
        private static Texture2D triangleTex;

        /// <summary>
        /// 위를 가리키는 안티앨리어싱 삼각형 알파 텍스처(흰색) — 꼭짓점이 위 가운데, 밑변이 아래. 아래를 가리킬 때는
        /// 텍스처 좌표를 뒤집어 그린다(<see cref="Arrow"/>). 글리프(⬆)는 기기 폰트에 없을 수 있어 도형으로 그린다.
        /// </summary>
        public static Texture2D Triangle
        {
            get
            {
                if (triangleTex != null) return triangleTex;
                triangleTex = new Texture2D(TriangleSize, TriangleSize, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp
                };
                float edge = EdgePixels / TriangleSize;
                float slant = Mathf.Sqrt(5f);   // 빗변 v = 1 − 2|u − ½|의 법선 길이
                for (int y = 0; y < TriangleSize; y++)
                {
                    for (int x = 0; x < TriangleSize; x++)
                    {
                        float u = (x + 0.5f) / TriangleSize;
                        float v = (y + 0.5f) / TriangleSize;   // 텍스처 v는 위로 증가 — 꼭짓점이 위
                        float toSide = (1f - v - 2f * Mathf.Abs(u - 0.5f)) / slant;
                        float alpha = Mathf.Clamp01(Mathf.Min(toSide, v) / edge);
                        triangleTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                triangleTex.Apply();
                return triangleTex;
            }
        }

        /// <summary>
        /// 굵은 화살표 하나 — 위 58%가 머리(삼각형), 아래가 몸통(폭 40%). <paramref name="up"/>이 거짓이면 아래를 가리킨다.
        /// </summary>
        public static void Arrow(Rect rect, bool up, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            float headH = rect.height * 0.58f;
            float stemW = rect.width * 0.4f;
            float stemX = rect.x + (rect.width - stemW) * 0.5f;
            Color prev = GUI.color;
            GUI.color = prev * color;
            if (up)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, headH), Triangle);
                // 머리 밑변과 몸통이 1px 겹치게 — 사이가 벌어져 보이지 않게.
                GUI.DrawTexture(new Rect(stemX, rect.y + headH - 1f, stemW, rect.height - headH + 1f), Texture2D.whiteTexture);
            }
            else
            {
                GUI.DrawTexture(new Rect(stemX, rect.y, stemW, rect.height - headH + 1f), Texture2D.whiteTexture);
                GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.yMax - headH, rect.width, headH), Triangle,
                    new Rect(0f, 1f, 1f, -1f));
            }
            GUI.color = prev;
        }
    }
}
