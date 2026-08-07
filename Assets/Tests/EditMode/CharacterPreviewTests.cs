#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 의상 3D 미리보기의 순수 계산부 — 썸네일 캐시 키, LRU, 슬롯별 프레이밍, 로드아웃.
    /// 실제 렌더(RenderTexture·Camera.Render·마네킹 생성)는 `rules/testing.md`상 테스트 제외다.
    /// </summary>
    [TestFixture]
    public class CharacterPreviewTests
    {
        // ── 썸네일 캐시 키 ──

        /// <summary>
        /// 키는 <b>구조체</b>다 — 호출부가 카드마다·OnGUI 패스마다 조회하므로 문자열이면 그때마다
        /// 새 문자열이 난다(2026-08-06 audit에서 P1으로 교체). Dictionary가 박싱 없이 비교하려면
        /// 값 동등성이 정확해야 하므로 여기서 고정한다.
        /// </summary>
        [Test]
        public void ThumbId_SameInputs_AreEqualAndShareHash()
        {
            var a = new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Hat, "hat_crown");
            var b = new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Hat, "hat_crown");

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ThumbId_DifferentSlotSameItem_Differs()
        {
            Assert.AreNotEqual(
                new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Hat, "x"),
                new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Tool, "x"));
        }

        [Test]
        public void ThumbId_DifferentItemSameSlot_Differs()
        {
            Assert.AreNotEqual(
                new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Hat, "hat_crown"),
                new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Hat, "hat_wizard"));
        }

        /// <summary>
        /// null 아이템 id는 ""로 정규화된다 — 정규화가 없으면 <c>GetHashCode</c>에서 NRE가 난다.
        /// </summary>
        [Test]
        public void ThumbId_NullItemId_NormalizesAndDoesNotThrow()
        {
            var nul = new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Hat, null);
            var empty = new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Hat, "");

            Assert.AreEqual(empty, nul);
            Assert.DoesNotThrow(() => nul.GetHashCode());
        }

        /// <summary>
        /// 외형이 바뀌면 <b>키가 아니라 캐시 자체</b>가 비워진다 — <c>EnsureMannequin</c>이
        /// <c>ReleaseThumbs</c>를 부른다. 그래서 키에 외형 해시를 섞을 필요가 없다(예전엔 섞었고,
        /// 그 탓에 키가 문자열이어야 했다).
        /// </summary>
        [Test]
        public void ThumbId_DoesNotCarryAppearanceHash()
        {
            Assert.AreEqual(
                new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Hat, "hat_crown"),
                new CharacterModelPreviewRenderer.ThumbId(OutfitSlot.Hat, "hat_crown"));
        }

        // ── LRU ──

        [Test]
        public void TouchKey_ExistingKey_MovesToNewestWithoutDuplicating()
        {
            List<string> order = new List<string> { "a", "b", "c" };

            CharacterModelPreviewRenderer.TouchKey(order, "a");

            Assert.AreEqual(3, order.Count);
            Assert.AreEqual("a", order[order.Count - 1]);
            Assert.AreEqual("b", order[0]);
        }

        [Test]
        public void EvictKeys_OverCap_DropsOldestFirst()
        {
            List<string> order = new List<string> { "a", "b", "c", "d" };

            List<string> evicted = CharacterModelPreviewRenderer.EvictKeys(order, 2);

            CollectionAssert.AreEqual(new[] { "a", "b" }, evicted);
            CollectionAssert.AreEqual(new[] { "c", "d" }, order);
        }

        [Test]
        public void EvictKeys_NullOrNegativeCap_DoesNotThrow()
        {
            // 제네릭이 된 뒤로는 null 리터럴만으론 T를 못 정한다 — 명시한다.
            Assert.AreEqual(0, CharacterModelPreviewRenderer.EvictKeys<string>(null, 3).Count);
            Assert.AreEqual(0, CharacterModelPreviewRenderer.EvictKeys(new List<string> { "a" }, -1).Count);
        }

        // ── 프레이밍 ──

        /// <summary>
        /// 슬롯마다 클로즈업할 노드가 있어야 한다. 비어 있으면 전신으로 물러나는데,
        /// 100px 카드에서 전신을 찍으면 모자가 15px가 된다.
        /// </summary>
        [Test]
        public void FocusNodesFor_EverySlot_ReturnsNodes()
        {
            foreach (OutfitSlot slot in (OutfitSlot[])Enum.GetValues(typeof(OutfitSlot)))
            {
                string[] nodes = CharacterModelPreviewRenderer.FocusNodesFor(slot);
                Assert.IsNotNull(nodes, slot.ToString());
                Assert.Greater(nodes.Length, 0, slot.ToString());
            }
        }

        /// <summary>
        /// 프리뷰 카메라는 -Z에서 +Z를 보고 캐릭터는 +Z를 향한다 — 회전 0°면 뒤통수다.
        /// 기본 각도는 정면(180° 부근)이어야 하고, 등에 붙는 가방·망토만 뒤쪽을 잡는다.
        /// </summary>
        [Test]
        public void ThumbAngleFor_FrontIsAroundOneEighty_BackSlotsAreNot()
        {
            float front = CharacterModelPreviewRenderer.ThumbAngleFor(OutfitSlot.Hat);
            Assert.Greater(front, 150f);
            Assert.Less(front, 230f);

            Assert.Less(CharacterModelPreviewRenderer.ThumbAngleFor(OutfitSlot.Backpack), 90f);
            Assert.Less(CharacterModelPreviewRenderer.ThumbAngleFor(OutfitSlot.Outerwear), 90f);
            Assert.AreEqual(front, CharacterModelPreviewRenderer.ThumbAngleFor(OutfitSlot.Tool));
        }

        // ── 로드아웃 ──

        /// <summary>배열 길이가 enum보다 짧으면 마지막 슬롯이 조용히 무시된다.</summary>
        [Test]
        public void OutfitLoadout_SlotCount_MatchesEnum()
        {
            Assert.AreEqual(Enum.GetValues(typeof(OutfitSlot)).Length, OutfitLoadout.SlotCount);
        }

        [Test]
        public void OutfitLoadout_SetGet_RoundTrips()
        {
            OutfitLoadout l = new OutfitLoadout();

            l.Set(OutfitSlot.Tool, "tool_wand");

            Assert.AreEqual("tool_wand", l.Get(OutfitSlot.Tool));
            Assert.IsNull(l.Get(OutfitSlot.Hat));
        }

        [Test]
        public void OutfitLoadout_Clear_ResetsEverySlot()
        {
            OutfitLoadout l = new OutfitLoadout();
            foreach (OutfitSlot s in (OutfitSlot[])Enum.GetValues(typeof(OutfitSlot))) l.Set(s, "x");

            l.Clear();

            foreach (OutfitSlot s in (OutfitSlot[])Enum.GetValues(typeof(OutfitSlot)))
                Assert.IsNull(l.Get(s), s.ToString());
        }

        /// <summary>해시가 안 바뀌면 입어보기를 해도 프리뷰가 다시 그려지지 않는다.</summary>
        [Test]
        public void OutfitLoadout_Hash_ChangesWhenOneSlotChanges()
        {
            OutfitLoadout a = new OutfitLoadout();
            OutfitLoadout b = new OutfitLoadout();
            a.Set(OutfitSlot.Hat, "hat_cap");
            b.Set(OutfitSlot.Hat, "hat_cap");
            Assert.AreEqual(a.Hash(), b.Hash());

            b.Set(OutfitSlot.Hat, "hat_crown");
            Assert.AreNotEqual(a.Hash(), b.Hash());
        }

        // ── 외형 스펙 ──

        [Test]
        public void AppearanceSpec_Hash_EqualSpecsAreEqual()
        {
            AppearanceSpec a = new AppearanceSpec { gender = 1, hairStyle = 2, hairColor = 3, faceType = 4 };
            AppearanceSpec b = new AppearanceSpec { gender = 1, hairStyle = 2, hairColor = 3, faceType = 4 };

            Assert.AreEqual(a.Hash(), b.Hash());
        }

        [Test]
        public void AppearanceSpec_Hash_EachFieldMatters()
        {
            AppearanceSpec baseline = new AppearanceSpec { gender = 1, hairStyle = 2, hairColor = 3, faceType = 4 };

            AppearanceSpec g = baseline; g.gender = 0;
            AppearanceSpec hs = baseline; hs.hairStyle = 5;
            AppearanceSpec hc = baseline; hc.hairColor = 5;
            AppearanceSpec ft = baseline; ft.faceType = 5;

            Assert.AreNotEqual(baseline.Hash(), g.Hash(), "성별");
            Assert.AreNotEqual(baseline.Hash(), hs.Hash(), "헤어스타일");
            Assert.AreNotEqual(baseline.Hash(), hc.Hash(), "헤어색");
            Assert.AreNotEqual(baseline.Hash(), ft.Hash(), "얼굴형");
        }
    }
}
#endif
