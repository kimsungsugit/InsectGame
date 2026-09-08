#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="UIHelper.LabelFit"/>이 쓰는 폰트 축소 캐시의 <b>키</b>.
    ///
    /// 그리기 자체는 IMGUI 컨텍스트가 필요해 검증할 수 없다. 여기서 고정하는 건 하나다 —
    /// <b>서로 다른 스타일이 캐시를 나눠 쓰지 않는가.</b> 측정은 <c>style.CalcHeight</c>/
    /// <c>CalcSize</c>로 하므로 폰트·볼드·<c>wordWrap</c>·패딩이 결과를 바꾸는데, 예전 키는
    /// (텍스트, 폭, 높이, 기준폰트)만 담아 그 넷이 같으면 답을 공유했다. 특히 <c>wordWrap</c>이
    /// 다르면 가로 검사 여부 자체가 갈리므로, 가로를 아예 안 본 값이 가로로 넘치는 라벨에
    /// 쓰일 수 있었다 — 증상이 조용한 글자 잘림이라 <c>LabelFit</c>이 막으려던 결함이
    /// 캐시를 통해 되살아난다.
    /// </summary>
    [TestFixture]
    public class UIHelperFitKeyTests
    {
        private const string Text = "사라져가는 곤충을 만나 기록하세요";
        private const float W = 300f;
        private const float H = 84f;
        private const int Size = 24;

        private static long Key(GUIStyle style) => UIHelper.FitKey(Text, W, H, Size, style);

        [Test]
        public void SameStyle_SameInputs_SameKey()
        {
            GUIStyle style = new GUIStyle { fontSize = Size, wordWrap = true };
            Assert.AreEqual(Key(style), Key(style));
        }

        [Test]
        public void DifferentStyleInstances_DoNotShareKey()
        {
            // 값이 똑같아 보여도 별개 스타일이면 별개 항목이어야 한다 — 측정에 영향을 주는
            // 속성을 빠짐없이 나열하는 대신 참조 동일성으로 가른다.
            GUIStyle a = new GUIStyle { fontSize = Size, wordWrap = true };
            GUIStyle b = new GUIStyle { fontSize = Size, wordWrap = true };
            Assert.AreNotEqual(Key(a), Key(b));
        }

        [Test]
        public void WordWrapDifference_DoesNotShareKey()
        {
            // 가장 위험한 조합 — wordWrap이 다르면 FitFontSize의 가로 검사 여부가 갈린다.
            GUIStyle wrapped = new GUIStyle { fontSize = Size, wordWrap = true };
            GUIStyle single = new GUIStyle { fontSize = Size, wordWrap = false };
            Assert.AreNotEqual(Key(wrapped), Key(single));
        }

        [Test]
        public void BoldDifference_DoesNotShareKey()
        {
            // 볼드는 같은 폰트 크기에서 더 넓다 — 일반 라벨이 계산한 "들어감"을 볼드가 쓰면 넘친다.
            GUIStyle normal = new GUIStyle { fontSize = Size, fontStyle = FontStyle.Normal };
            GUIStyle bold = new GUIStyle { fontSize = Size, fontStyle = FontStyle.Bold };
            Assert.AreNotEqual(Key(normal), Key(bold));
        }

        [Test]
        public void TextRectAndBaseSize_StillDiscriminate()
        {
            // 스타일을 키에 넣었다고 기존 네 축이 죽으면 안 된다.
            GUIStyle style = new GUIStyle { fontSize = Size, wordWrap = true };
            long baseline = UIHelper.FitKey(Text, W, H, Size, style);

            Assert.AreNotEqual(baseline, UIHelper.FitKey("다른 문구", W, H, Size, style));
            Assert.AreNotEqual(baseline, UIHelper.FitKey(Text, W + 40f, H, Size, style));
            Assert.AreNotEqual(baseline, UIHelper.FitKey(Text, W, H + 20f, Size, style));
            Assert.AreNotEqual(baseline, UIHelper.FitKey(Text, W, H, Size + 6, style));
        }
    }
}
#endif
