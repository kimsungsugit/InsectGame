#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 의상 표면 무늬(<see cref="OutfitPatternLibrary"/>) — 절차 생성 텍스처.
    ///
    /// 무늬 결함은 조용하다: itemId 오타면 그 옷은 예외 없이 **민무늬**로 돌아가고, 표면을 잘못 고르면
    /// 조끼 소매에 조끼 무늬가 찍힌다. 그래서 표의 키·슬롯·표면 정합과 "무늬끼리 실제로 다른가"를 고정한다.
    /// 그림 자체는 OutfitRenderProbe -outfitAll로 눈으로 본다.
    /// </summary>
    [TestFixture]
    public class OutfitPatternLibraryTests
    {
        private static Dictionary<string, OutfitItem> Catalog()
        {
            Dictionary<string, OutfitItem> map = new Dictionary<string, OutfitItem>();
            foreach (OutfitItem item in CharacterOutfitManager.BuildCatalog()) map[item.itemId] = item;
            return map;
        }

        /// <summary>이 슬롯의 옷이 무늬를 입는 표면(ApplyToCharacter가 부르는 것과 같다).</summary>
        private static PatternSurface[] SurfacesFor(OutfitSlot slot)
        {
            switch (slot)
            {
                case OutfitSlot.Top: return new[] { PatternSurface.Torso, PatternSurface.Panel, PatternSurface.Sleeve };
                case OutfitSlot.Outerwear: return new[] { PatternSurface.Torso, PatternSurface.Sleeve };
                case OutfitSlot.Bottom: return new[] { PatternSurface.Leg };
                case OutfitSlot.Shoes: return new[] { PatternSurface.Boot };
                default: return new PatternSurface[0];
            }
        }

        [Test]
        public void EveryPatternKey_IsARealWearableItem()
        {
            Dictionary<string, OutfitItem> catalog = Catalog();
            foreach (KeyValuePair<string, PatternKind> e in OutfitPatternLibrary.Entries())
            {
                Assert.IsTrue(catalog.TryGetValue(e.Key, out OutfitItem item), $"'{e.Key}'는 카탈로그에 없다 — 오타면 조용히 민무늬가 된다");
                Assert.Greater(SurfacesFor(item.slot).Length, 0, $"{e.Key}: 무늬를 입힐 표면이 없는 슬롯({item.slot})이다");
            }
        }

        /// <summary>무늬가 있는 옷은 자기 슬롯의 표면 중 **하나 이상**에 실제로 그려진다(표면 판정이 전부 거짓이면 표가 장식이 된다).</summary>
        [Test]
        public void EveryPatternedItem_DrawsOnAtLeastOneOfItsSurfaces()
        {
            Dictionary<string, OutfitItem> catalog = Catalog();
            foreach (KeyValuePair<string, PatternKind> e in OutfitPatternLibrary.Entries())
            {
                OutfitItem item = catalog[e.Key];
                bool any = false;
                foreach (PatternSurface s in SurfacesFor(item.slot))
                    any |= OutfitPatternLibrary.Get(e.Key, s, item.primaryColor, item.secondaryColor) != null;
                Assert.IsTrue(any, $"{e.Key}({e.Value}): 어느 표면에도 그려지지 않는다");
            }
        }

        [Test]
        public void UnknownOrPlainItems_HaveNoTexture()
        {
            Assert.IsNull(OutfitPatternLibrary.Get(null, PatternSurface.Torso, Color.red, Color.blue));
            Assert.IsNull(OutfitPatternLibrary.Get("top_does_not_exist", PatternSurface.Torso, Color.red, Color.blue));
            Assert.IsNull(OutfitPatternLibrary.Get("bot_pants", PatternSurface.Leg, Color.grey, Color.white), "기본 바지는 민무늬");
            // 조끼 소매는 속셔츠 색이지 조끼 무늬가 아니다
            Assert.IsNull(OutfitPatternLibrary.Get("top_vest", PatternSurface.Sleeve, Color.yellow, Color.white));
        }

        [Test]
        public void Textures_HaveTheSurfaceSize_AndAreCached()
        {
            Texture2D torso = OutfitPatternLibrary.Get("top_stripe", PatternSurface.Torso, new Color(0.2f, 0.4f, 0.8f), Color.white);
            Texture2D again = OutfitPatternLibrary.Get("top_stripe", PatternSurface.Torso, new Color(0.2f, 0.4f, 0.8f), Color.white);
            Assert.IsNotNull(torso);
            Assert.AreSame(torso, again, "같은 무늬·표면·색은 한 번만 굽는다");
            Assert.AreEqual(256, torso.width);
            Assert.AreEqual(128, torso.height);

            Texture2D other = OutfitPatternLibrary.Get("top_stripe", PatternSurface.Torso, Color.red, Color.white);
            Assert.AreNotSame(torso, other, "색이 다르면 다른 텍스처");

            Texture2D leg = OutfitPatternLibrary.Get("bot_jeans", PatternSurface.Leg, new Color(0.15f, 0.25f, 0.55f), Color.white);
            Assert.AreEqual(64, leg.width);
        }

        [Test]
        public void Render_IsDeterministic()
        {
            Color[] a = OutfitPatternLibrary.Render(PatternKind.Galaxy, PatternSurface.Torso, Color.blue, Color.magenta, 64, 32);
            Color[] b = OutfitPatternLibrary.Render(PatternKind.Galaxy, PatternSurface.Torso, Color.blue, Color.magenta, 64, 32);
            CollectionAssert.AreEqual(a, b, "같은 입력이면 같은 무늬 — 난수 시드가 흔들리면 옷을 갈아입을 때마다 별이 옮겨 다닌다");
        }

        /// <summary>
        /// 상의 14벌의 무늬가 **서로 달라야** 한다 — 이 개편 전엔 상의 전부가 가슴의 같은 색 조각이었다.
        /// 같은 두 색으로 구워도 다른 그림이 나오는지 본다(색 차이에 기대지 않는다).
        /// </summary>
        [Test]
        public void TopPatterns_AreVisuallyDistinct()
        {
            Dictionary<string, OutfitItem> catalog = Catalog();
            Dictionary<string, string> seen = new Dictionary<string, string>();
            foreach (KeyValuePair<string, PatternKind> e in OutfitPatternLibrary.Entries())
            {
                if (catalog[e.Key].slot != OutfitSlot.Top) continue;
                Color[] px = OutfitPatternLibrary.Render(e.Value, PatternSurface.Torso, new Color(0.5f, 0.3f, 0.2f), new Color(0.9f, 0.9f, 0.6f), 64, 32);
                string sig = Signature(px);
                Assert.IsFalse(seen.ContainsKey(sig), $"{e.Key}의 무늬가 {(seen.ContainsKey(sig) ? seen[sig] : "")}와 같다");
                seen[sig] = e.Key;
            }
            Assert.GreaterOrEqual(seen.Count, 14, "상의 무늬가 14벌을 덮지 못한다");
        }

        /// <summary>아틀라스 표면은 앞 칸과 뒤 칸이 다르게 그려질 수 있어야 한다(단추·주머니는 앞에만).</summary>
        [Test]
        public void AtlasSurfaces_KeepFrontOnlyMotifsOffTheBack()
        {
            Color[] px = OutfitPatternLibrary.Render(PatternKind.PirateCoat, PatternSurface.Torso, new Color(0.5f, 0.1f, 0.1f), new Color(0.85f, 0.7f, 0.15f), 128, 64);
            int w = 128, h = 64;
            long front = 0, back = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = px[y * w + x];
                    bool gold = c.r > 0.7f && c.g > 0.55f;
                    if (gold && x < w / 2) front++;
                    if (gold && x >= w / 2) back++;
                }
            }
            Assert.Greater(front, back * 3 / 2, "금단추·금테가 등에 앞면만큼 있다 — 아틀라스 칸이 뒤바뀌었거나 앞면 전용 무늬가 새었다");
        }

        private static string Signature(Color[] px)
        {
            unchecked
            {
                long h = 17;
                for (int i = 0; i < px.Length; i += 3)
                {
                    Color32 c = px[i];
                    h = h * 31 + (c.r >> 3) * 1024 + (c.g >> 3) * 32 + (c.b >> 3);
                }
                return h.ToString();
            }
        }
    }
}
#endif
