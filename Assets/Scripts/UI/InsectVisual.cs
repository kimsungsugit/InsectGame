using InsectGame.Data;
using InsectGame.Dex;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 곤충 그림 한 장을 그리는 단일 진입점 — <b>3D 모델 썸네일이 있으면 그것, 없으면 2D 폴백.</b>
    ///
    /// 왜 파사드인가
    /// -------------
    /// 곤충을 그리는 화면이 9곳(도감 그리드·상세, 보유 곤충, 팀 편성, 병원, 훈련, 포획 선택,
    /// 지역맵 도감)인데, 각자 "3D 있으면 3D"를 판단하게 두면 정책이 아홉 갈래로 흩어진다.
    /// 여기 한 곳만 고치면 전부 따라온다.
    ///
    /// 예전엔 <see cref="InsectModelPreviewRenderer"/>의 호출부가 도감 상세 **한 곳뿐**이었다.
    /// 나머지 여덟 화면은 손으로 그린 축 정렬 사각형 282개를 봤다.
    ///
    /// 렌더러 참조는 정적 훅으로 받는다 — <c>PlaySceneBootstrap</c>이
    /// <c>InsectEntity.FleePreventChanceProvider</c>를 같은 방식으로 주입하는 선례가 있고,
    /// 새 싱글턴을 늘리지 않는다(<c>rules/unity-csharp.md</c>).
    ///
    /// ★ 목록에서 부를 때는 반드시 뷰포트 컬링을 먼저 하라
    /// -------------------------------------------------
    /// 썸네일 캐시는 <b>한 뷰포트 분량(24칸)</b>이고 적중할 때마다 LRU를 갱신한다. IMGUI 스크롤뷰엔
    /// 가상화가 없으므로, 호출부가 컬링하지 않고 60마리를 매 패스 훑으면 24칸이 절대 안정되지 않아
    /// 렌더러가 <b>프레임마다 곤충 모델을 통째로 만들었다 부수고</b> RenderTexture를 create/Release 한다.
    /// 계산은 <c>DexBrowseLayout.GetVisibleRowRange</c>(행) / <c>GetVisibleItemRange</c>(그리드)를 쓴다.
    ///
    /// 이 결함은 <b>썸네일 도입(726795a) 한 번에 6개 화면에서 동시에</b> 생겼고, 2026-08-06 audit이
    /// 도감·훈련·팀편성·보유곤충·병원·지역맵을 차례로 고쳤다. 새 목록을 만들 때 같은 실수를 반복하지 말 것.
    /// </summary>
    public static class InsectVisual
    {
        /// <summary>Bootstrap이 씬 생성 시 1회 주입. 미배선이면 2D 폴백만 나온다(기능은 죽지 않는다).</summary>
        public static InsectModelPreviewRenderer Renderer;

        /// <summary>
        /// <paramref name="rect"/> 안에 곤충 한 마리를 그린다.
        ///
        /// 썸네일은 종별로 <b>프레임당 하나씩</b> 렌더되므로, 목록을 처음 열면 몇 프레임 동안
        /// 2D 폴백이 보이다가 차례로 3D로 바뀐다. 그래서 폴백 품질이 중요하다.
        /// </summary>
        public static void Draw(Rect rect, InsectData data, bool shiny, float alpha)
        {
            if (data == null || rect.width <= 0f || rect.height <= 0f) return;

            if (Renderer != null)
            {
                Texture thumb = Renderer.GetThumbnail(data, shiny);
                if (thumb != null)
                {
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    GUI.DrawTexture(rect, thumb, ScaleMode.ScaleToFit, true);
                    GUI.color = prev;
                    return;
                }
            }

            CapturePopupUI.DrawTypedInsectPortrait(
                rect.center.x, rect.center.y, data.insectId, data.rarity, alpha);
        }

        /// <summary>중심점 + 한 변으로 그리는 편의 오버로드 — 기존 호출부가 대개 이 형태였다.</summary>
        public static void Draw(float cx, float cy, float size, InsectData data, bool shiny, float alpha)
        {
            Draw(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), data, shiny, alpha);
        }
    }
}
