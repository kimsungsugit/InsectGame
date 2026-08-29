#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.UI;

namespace InsectGame.Tests
{
    /// <summary>
    /// 캐릭터 생성 화면의 <b>순수 계산부</b>만 본다 — <c>testing.md</c>가 OnGUI 렌더링을
    /// 테스트 제외로 명시하므로 그리기 자체는 검증하지 않는다.
    ///
    /// 여기서 지키는 건 하나다: <b>하단 버튼이 패널 안에 남는가</b>.
    /// 넘치면 "모험 시작"을 누를 수 없어 게임에 진입조차 못 하는데, 그건 세로가 짧은
    /// 가로모드에서만 나타나 개발 중에는 보이지 않는다.
    /// </summary>
    [TestFixture]
    public class CharacterCreateFlowTests
    {
        /// <summary>패널 높이 상한 — <c>DrawCharacterCreatePanel</c>의 <c>Mathf.Min(1313f, ...)</c>.</summary>
        private const float PanelMaxHeight = 1313f;

        /// <summary>
        /// 세로가 짧은 경우의 대략치. 가로모드 폰에서 세이프에어리어와 세로 마진을 뺀 값이
        /// 이 근처까지 내려간다(<c>UISafeLayout.Px.ClampHeight</c>가 줄인다).
        /// </summary>
        private const float ShortScreenHeight = 620f;

        private static IEnumerable<LoginUI.CreateStep> AllSteps()
        {
            yield return LoginUI.CreateStep.Preset;
            yield return LoginUI.CreateStep.Customize;
            yield return LoginUI.CreateStep.Starter;
        }

        /// <summary>
        /// 라디오/버튼 높이가 레이아웃을 지배하는 단계. 스타터는 고정 크기 카드뿐이라
        /// 모바일/데스크톱 차이가 없어 그 검사에서 뺀다.
        /// </summary>
        private static IEnumerable<LoginUI.CreateStep> TouchSizedSteps()
        {
            yield return LoginUI.CreateStep.Preset;
            yield return LoginUI.CreateStep.Customize;
        }

        /// <summary>
        /// <b>이 픽스처의 핵심.</b> 프리뷰를 최소 크기까지 줄인 상태에서도 나머지 콘텐츠가
        /// 들어가야 한다. 프리뷰는 줄일 수 있지만 라디오와 버튼은 줄일 수 없기 때문이다.
        /// </summary>
        [Test]
        public void EveryStep_FitsOnAShortScreen_EvenAtMinimumPreview()
        {
            foreach (LoginUI.CreateStep step in AllSteps())
            {
                foreach (bool mobile in new[] { false, true })
                {
                    float needed = LoginUI.TotalContentHeight(step, mobile, ShortScreenHeight);

                    Assert.LessOrEqual(needed, ShortScreenHeight,
                        $"{step}({(mobile ? "모바일" : "데스크톱")}): 프리뷰를 최소로 줄여도 {needed:F0}px가 " +
                        $"필요해 짧은 화면 {ShortScreenHeight:F0}px를 넘는다 — 하단 버튼이 잘린다");
                }
            }
        }

        [Test]
        public void EveryStep_FitsInsideTheFullHeightPanel()
        {
            foreach (LoginUI.CreateStep step in AllSteps())
            {
                foreach (bool mobile in new[] { false, true })
                {
                    float needed = LoginUI.TotalContentHeight(step, mobile, PanelMaxHeight);

                    Assert.LessOrEqual(needed, PanelMaxHeight,
                        $"{step}({(mobile ? "모바일" : "데스크톱")}): 전체 높이 패널에서도 넘친다");
                }
            }
        }

        /// <summary>프리뷰는 화면이 좁아져도 최소 크기 아래로 내려가지 않는다(내려가면 알아볼 수 없다).</summary>
        [Test]
        public void PreviewHeight_NeverGoesBelowMinimum()
        {
            // 스타터 단계에는 3D 프리뷰가 없다(곤충 카드가 화면을 채운다) — 0이 정상이다.
            foreach (LoginUI.CreateStep step in TouchSizedSteps())
            {
                foreach (bool mobile in new[] { false, true })
                {
                    foreach (float panel in new[] { 300f, 500f, ShortScreenHeight, 900f, PanelMaxHeight })
                    {
                        float h = LoginUI.PreviewHeightFor(step, mobile, panel);
                        Assert.GreaterOrEqual(h, LoginUI.MinPreviewH, $"{step} @ {panel}px");
                    }
                }
            }
        }

        /// <summary>패널이 커지면 프리뷰도 커지되 상한에서 멈춘다 — 무한히 늘어나면 레이아웃이 깨진다.</summary>
        [Test]
        public void PreviewHeight_GrowsWithPanel_ThenCaps()
        {
            float small = LoginUI.PreviewHeightFor(LoginUI.CreateStep.Preset, false, 700f);
            float large = LoginUI.PreviewHeightFor(LoginUI.CreateStep.Preset, false, PanelMaxHeight);
            float huge = LoginUI.PreviewHeightFor(LoginUI.CreateStep.Preset, false, 5000f);

            Assert.GreaterOrEqual(large, small, "패널이 커지면 프리뷰도 커져야 한다");
            Assert.AreEqual(large, huge, 0.01f, "상한을 넘어 계속 커지면 안 된다");
        }

        /// <summary>
        /// 모바일은 터치 타깃이 커서 항상 데스크톱보다 세로를 더 쓴다 — 검사가 헐거워지지 않게 확인한다.
        ///
        /// <c>FixedContentHeight</c>가 아니라 <c>TotalContentHeight</c>로 본다: 세부 조정이 2열이 되면서
        /// 라디오 높이가 고정 블록 밖(<c>CustomizeRowsHeight</c>)으로 옮겨갔기 때문에,
        /// 고정 블록만 보면 두 레이아웃이 같은 값을 낸다.
        /// </summary>
        [Test]
        public void MobileLayout_NeedsMoreHeightThanDesktop()
        {
            foreach (LoginUI.CreateStep step in TouchSizedSteps())
            {
                Assert.Greater(LoginUI.TotalContentHeight(step, true, PanelMaxHeight),
                    LoginUI.TotalContentHeight(step, false, PanelMaxHeight),
                    $"{step}: 모바일이 데스크톱보다 낮으면 터치 타깃 규칙이 깨진 것이다");
            }
        }

        // ── 프리셋이 프리뷰로 전달되는가 ──

        /// <summary>
        /// 프리셋을 고르면 외형 <b>전체</b>가 그 사람으로 바뀌어야 한다.
        /// 의상만 바뀌던 시절에는 "탐험가"를 골라도 머리·얼굴이 전부 0번 기본값이었다.
        /// </summary>
        [Test]
        public void EveryPreset_CarriesACompleteLook()
        {
            for (int i = 0; i < CharacterPresetLibrary.Count; i++)
            {
                CharacterPresetLibrary.Preset p = CharacterPresetLibrary.Get(i);
                AppearanceSpec spec = p.ToAppearance();

                Assert.AreEqual(p.Gender, spec.gender, p.DisplayName);
                Assert.AreEqual(p.HairStyle, spec.hairStyle, p.DisplayName);
                Assert.AreEqual(p.HairColor, spec.hairColor, p.DisplayName);
                Assert.AreEqual(p.FaceType, spec.faceType, p.DisplayName);
                Assert.AreEqual(p.SkinColor, spec.skinColor, p.DisplayName);
            }
        }

        /// <summary>
        /// 프리셋들이 서로 다른 외형이어야 프리뷰에서 고르는 의미가 있다 —
        /// 해시가 같으면 마네킹이 재생성되지 않아 화면이 그대로다.
        /// </summary>
        [Test]
        public void Presets_ProduceDifferentAppearanceHashes()
        {
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < CharacterPresetLibrary.Count; i++)
            {
                seen.Add(CharacterPresetLibrary.Get(i).ToAppearance().Hash());
            }

            Assert.Greater(seen.Count, 1,
                "모든 프리셋의 외형 해시가 같다 — 어느 것을 골라도 3D 프리뷰가 안 바뀐다");
        }
    }
}
#endif
