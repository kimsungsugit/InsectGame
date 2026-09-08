#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 캐릭터 색 팔레트와 생성 화면 의상 프리셋.
    ///
    /// 이 픽스처가 존재하는 이유는 <b>둘 다 실패가 조용했기</b> 때문이다.
    /// - 피부색: 생성 화면이 4색을 저장하고 클라우드 동기까지 했는데 3D 빌더가 그 키를 읽지 않아
    ///   필드 캐릭터는 늘 같은 피부였다. 예외도 경고도 없었다.
    /// - 의상 프리셋: "연구원"/"자유"가 미보유 아이템을 <c>Equip</c>해서, <c>Equip</c>의 소유 가드에
    ///   막혀 경고 로그만 남기고 무시됐다. 고른 프리셋이 실제로는 안 입혀졌다.
    /// </summary>
    [TestFixture]
    public class CharacterPaletteTests
    {
        // ── 팔레트 ──

        /// <summary>
        /// 팔레트 길이는 생성 화면의 라디오 선택지 수와 같아야 한다. 팔레트가 짧으면 마지막 선택지가
        /// clamp돼 앞 색과 같아지고(고를 수는 있는데 안 바뀐다), 길면 고를 수 없는 색이 생긴다.
        /// </summary>
        [Test]
        public void SkinPalette_HasOneColorPerCreationChoice()
        {
            Assert.AreEqual(4, CharacterPalette.SkinCount, "생성 화면 피부색 라디오는 밝은/보통/어두운/진한 4개다");
        }

        [Test]
        public void HairPalette_HasOneColorPerCreationChoice()
        {
            Assert.AreEqual(6, CharacterPalette.HairCount, "생성 화면 머리색 라디오는 검정/갈색/금발/빨강/보라/파랑 6개다");
        }

        [Test]
        public void SkinPalette_EveryEntryIsDistinct()
        {
            AssertAllDistinct(CharacterPalette.SkinCount, CharacterPalette.Skin, "피부");
        }

        [Test]
        public void HairPalette_EveryEntryIsDistinct()
        {
            AssertAllDistinct(CharacterPalette.HairCount, CharacterPalette.Hair, "머리");
        }

        private static void AssertAllDistinct(int count, System.Func<int, Color> pick, string label)
        {
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    Assert.AreNotEqual(pick(i), pick(j),
                        label + " 팔레트 " + i + "번과 " + j + "번이 같은 색이다 — 고를 수는 있는데 아무 변화가 없다");
                }
            }
        }

        /// <summary>구세이브·손상된 PlayerPrefs가 범위 밖 인덱스를 줘도 예외 대신 끝 색을 낸다.</summary>
        [Test]
        public void Palette_OutOfRangeIndex_ClampsInsteadOfThrowing()
        {
            Assert.AreEqual(CharacterPalette.Skin(0), CharacterPalette.Skin(-5));
            Assert.AreEqual(CharacterPalette.Skin(CharacterPalette.SkinCount - 1), CharacterPalette.Skin(999));
            Assert.AreEqual(CharacterPalette.Hair(0), CharacterPalette.Hair(-1));
            Assert.AreEqual(CharacterPalette.Hair(CharacterPalette.HairCount - 1), CharacterPalette.Hair(99));
        }

        /// <summary>외형 정보를 모르는 자리(레시피 상수 등)가 쓰는 기본 피부색은 팔레트 안에 있어야 한다.</summary>
        [Test]
        public void DefaultSkin_IsOneOfThePaletteEntries()
        {
            bool found = false;
            for (int i = 0; i < CharacterPalette.SkinCount; i++)
            {
                if (CharacterPalette.Skin(i) == CharacterPalette.DefaultSkin) found = true;
            }

            Assert.IsTrue(found, "DefaultSkin이 팔레트 밖 색이면 선택지로는 낼 수 없는 피부가 생긴다");
        }

        // ── 재질 ──

        /// <summary>
        /// 재질이 부위마다 실제로 갈려야 한다. 전부 같은 값이면 이 시스템을 넣은 의미가 없다 —
        /// 옛 상태(모두 Standard 기본 0.5)와 다를 게 없다.
        /// </summary>
        [Test]
        public void SurfaceValues_SkinClothMetal_AreDistinct()
        {
            CharacterPalette.SurfaceValues(SurfaceKind.Skin, out float skinGloss, out float skinMetal);
            CharacterPalette.SurfaceValues(SurfaceKind.Cloth, out float clothGloss, out _);
            CharacterPalette.SurfaceValues(SurfaceKind.Metal, out float metalGloss, out float metalMetallic);

            Assert.AreNotEqual(skinGloss, clothGloss, "피부와 천이 같은 광택이면 구분이 안 된다");
            Assert.Less(clothGloss, metalGloss, "천이 금속보다 반짝이면 안 된다");
            Assert.AreEqual(0f, skinMetal, "피부는 금속이 아니다");
            Assert.Greater(metalMetallic, 0.5f, "금속은 실제로 금속처럼 반사해야 한다");
        }

        [Test]
        public void SurfaceValues_EveryKind_StaysInUnitRange()
        {
            foreach (SurfaceKind kind in System.Enum.GetValues(typeof(SurfaceKind)))
            {
                CharacterPalette.SurfaceValues(kind, out float gloss, out float metallic);
                Assert.That(gloss, Is.InRange(0f, 1f), kind + ": _Glossiness가 0~1 밖이다");
                Assert.That(metallic, Is.InRange(0f, 1f), kind + ": _Metallic이 0~1 밖이다");
            }
        }

        [Test]
        public void ApplySurface_NullMaterial_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => CharacterPalette.ApplySurface(null, SurfaceKind.Skin));
        }

        // ── 의상 프리셋 ──

        /// <summary>
        /// 2D 폴백(3D 프리뷰가 아직 없는 첫 프레임·미배선 시)의 대표색이 프리셋 수와 맞아야 한다.
        /// 짧으면 뒤쪽 프리셋이 clamp돼 앞 것과 같은 색으로 그려진다 — 프리셋이 3→5로 늘었을 때
        /// 실제로 그 상태였다.
        /// </summary>
        [Test]
        public void PortraitFallbackColors_CoverEveryPreset()
        {
            Assert.AreEqual(CharacterPresetLibrary.Count, CharacterPortraitRenderer.PresetTopColors.Length,
                "2D 폴백 색 배열이 프리셋 수와 다르면 뒤쪽 프리셋이 앞 것과 같은 색이 된다");
        }

        [Test]
        public void OutfitPresets_CountMatchesRadioLabels()
        {
            Assert.AreEqual(LoginUI.OutfitLabels.Length, CharacterPresetLibrary.Count,
                "라벨과 프리셋 수가 다르면 마지막 라디오가 clamp돼 앞 프리셋을 다시 입힌다");
        }

        [Test]
        public void EveryPresetItem_ExistsInCatalog()
        {
            HashSet<string> catalog = new HashSet<string>();
            foreach (OutfitItem item in CharacterOutfitManager.BuildCatalog()) catalog.Add(item.itemId);

            for (int p = 0; p < CharacterPresetLibrary.Count; p++)
            {
                CharacterPresetLibrary.Preset preset = CharacterPresetLibrary.Get(p);
                foreach (string id in preset.OutfitItemIds)
                {
                    Assert.IsTrue(catalog.Contains(id),
                        "프리셋 " + p + "(" + preset.DisplayName + ")의 '" + id +
                        "'가 카탈로그에 없다 — Equip이 조용히 무시한다");
                }
            }
        }

        /// <summary>
        /// <b>이 픽스처의 핵심.</b> 캐릭터 생성 시점에는 상점을 거치지 않았으므로 프리셋이 쓰는
        /// 아이템은 전부 기본 보유여야 한다. <c>CharacterOutfitManager.Equip</c>이 미보유를
        /// <c>LogWarning</c> 후 무시하기 때문에, 어기면 <b>고른 프리셋이 그냥 안 입혀진다</b>.
        /// 실제로 "연구원" 4개·"자유" 3개가 이 상태였다.
        /// </summary>
        [Test]
        public void EveryPresetItem_IsUnlockedByDefault()
        {
            Dictionary<string, OutfitItem> byId = new Dictionary<string, OutfitItem>();
            foreach (OutfitItem item in CharacterOutfitManager.BuildCatalog()) byId[item.itemId] = item;

            for (int p = 0; p < CharacterPresetLibrary.Count; p++)
            {
                CharacterPresetLibrary.Preset preset = CharacterPresetLibrary.Get(p);
                foreach (string id in preset.OutfitItemIds)
                {
                    Assert.IsTrue(byId.TryGetValue(id, out OutfitItem item), "'" + id + "'가 카탈로그에 없다");
                    Assert.IsTrue(item.unlockedByDefault,
                        "프리셋 " + p + "(" + preset.DisplayName + ")의 '" + id + "'가 미보유 아이템이다 — " +
                        "생성 화면에서 이 프리셋을 골라도 해당 슬롯은 기본 복장 그대로 남는다");
                }
            }
        }

        /// <summary>
        /// 에셋(CharacterAppearanceConfig)이 있든 없든 <b>코드 기본값</b>은 그 자체로 옳아야 한다.
        /// 에셋이 없는 게 정상 경로이기 때문이다 — 이 저장소의 Resources SO 다섯 개가 이미 그 상태다.
        /// </summary>
        [Test]
        public void CodeDefaultPresets_AreSelfConsistent_WithoutAnyAsset()
        {
            Dictionary<string, OutfitItem> byId = new Dictionary<string, OutfitItem>();
            foreach (OutfitItem item in CharacterOutfitManager.BuildCatalog()) byId[item.itemId] = item;

            CharacterPresetLibrary.Preset[] defaults = CharacterPresetLibrary.CodeDefaults;
            Assert.Greater(defaults.Length, 0, "코드 기본 프리셋이 비어 있으면 폴백이 무의미하다");

            foreach (CharacterPresetLibrary.Preset p in defaults)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.DisplayName), "프리셋 이름이 비었다 — 라디오가 빈 칸이 된다");
                Assert.IsNotNull(p.OutfitItemIds);
                foreach (string id in p.OutfitItemIds)
                {
                    Assert.IsTrue(byId.TryGetValue(id, out OutfitItem item), p.DisplayName + ": '" + id + "' 없음");
                    Assert.IsTrue(item.unlockedByDefault, p.DisplayName + ": '" + id + "'가 미보유");
                }
            }
        }

        /// <summary>
        /// 프리셋의 외형 인덱스가 팔레트/스타일 범위 안이어야 한다. 벗어나면 clamp돼
        /// 옆 프리셋과 같은 얼굴이 되는데, 화면에서는 "골랐는데 안 바뀐다"로만 보인다.
        /// </summary>
        [Test]
        public void EveryPreset_AppearanceIndices_AreInRange()
        {
            for (int i = 0; i < CharacterPresetLibrary.Count; i++)
            {
                CharacterPresetLibrary.Preset p = CharacterPresetLibrary.Get(i);

                Assert.That(p.Gender, Is.InRange(0, 1), p.DisplayName + ": 성별");
                Assert.That(p.HairStyle, Is.InRange(0, 3), p.DisplayName + ": 머리 스타일(짧은/중간/긴/올림)");
                Assert.That(p.HairColor, Is.InRange(0, CharacterPalette.HairCount - 1), p.DisplayName + ": 머리색");
                Assert.That(p.FaceType, Is.InRange(0, 3), p.DisplayName + ": 표정");
                Assert.That(p.SkinColor, Is.InRange(0, CharacterPalette.SkinCount - 1), p.DisplayName + ": 피부색");
            }
        }

        /// <summary>프리셋의 외형이 그대로 3D 프리뷰로 넘어가야 한다.</summary>
        [Test]
        public void Preset_ToAppearance_CarriesEveryField()
        {
            CharacterPresetLibrary.Preset p = CharacterPresetLibrary.Get(1);
            AppearanceSpec spec = p.ToAppearance();

            Assert.AreEqual(p.Gender, spec.gender);
            Assert.AreEqual(p.HairStyle, spec.hairStyle);
            Assert.AreEqual(p.HairColor, spec.hairColor);
            Assert.AreEqual(p.FaceType, spec.faceType);
            Assert.AreEqual(p.SkinColor, spec.skinColor);
        }

        /// <summary>구세이브의 OutfitPreset이 범위를 벗어나도 예외 대신 clamp돼야 한다.</summary>
        [Test]
        public void PresetLibrary_OutOfRangeIndex_Clamps()
        {
            Assert.AreEqual(CharacterPresetLibrary.Get(0).DisplayName, CharacterPresetLibrary.Get(-3).DisplayName);
            Assert.AreEqual(CharacterPresetLibrary.Get(CharacterPresetLibrary.Count - 1).DisplayName,
                CharacterPresetLibrary.Get(999).DisplayName);
        }

        /// <summary>
        /// 프리셋 하나가 슬롯을 빠뜨리면 그 슬롯만 직전 상태가 남아 프리셋 간 전환이 누적된다.
        /// 악세서리는 기본 장착(acc_none)이 따로 있어 프리셋이 다루지 않는다.
        /// </summary>
        [Test]
        public void EveryPreset_CoversAllSlotsExceptAccessory()
        {
            Dictionary<string, OutfitItem> byId = new Dictionary<string, OutfitItem>();
            foreach (OutfitItem item in CharacterOutfitManager.BuildCatalog()) byId[item.itemId] = item;

            for (int p = 0; p < CharacterPresetLibrary.Count; p++)
            {
                CharacterPresetLibrary.Preset preset = CharacterPresetLibrary.Get(p);
                HashSet<OutfitSlot> covered = new HashSet<OutfitSlot>();

                foreach (string id in preset.OutfitItemIds)
                {
                    Assert.IsTrue(byId.TryGetValue(id, out OutfitItem item), "'" + id + "'가 카탈로그에 없다");
                    Assert.IsTrue(covered.Add(item.slot),
                        "프리셋 " + p + "(" + preset.DisplayName + ")가 " + item.slot +
                        " 슬롯을 두 번 지정한다 — 뒤엣것만 남는다");
                }

                foreach (OutfitSlot slot in System.Enum.GetValues(typeof(OutfitSlot)))
                {
                    if (slot == OutfitSlot.Accessory) continue;
                    Assert.IsTrue(covered.Contains(slot),
                        "프리셋 " + p + "(" + preset.DisplayName + ")에 " + slot +
                        " 슬롯이 없다 — 그 슬롯만 직전 프리셋 값이 남는다");
                }
            }
        }

        /// <summary>
        /// 프리셋 셋이 서로 눈에 띄게 달라야 고르는 의미가 있다. 미보유 아이템을 지정하던 시절엔
        /// 실제 결과가 거의 같았다(대부분 기본 복장으로 폴백).
        /// </summary>
        /// <summary>
        /// 프리셋끼리 <b>눈에 띄게</b> 달라야 고르는 의미가 있다. 의상이 같아도 외형이 다르면
        /// 다른 사람이므로, 둘을 합쳐 본다.
        /// </summary>
        [Test]
        public void Presets_AreVisiblyDifferentFromEachOther()
        {
            for (int a = 0; a < CharacterPresetLibrary.Count; a++)
            {
                for (int b = a + 1; b < CharacterPresetLibrary.Count; b++)
                {
                    CharacterPresetLibrary.Preset pa = CharacterPresetLibrary.Get(a);
                    CharacterPresetLibrary.Preset pb = CharacterPresetLibrary.Get(b);

                    bool sameLook = pa.ToAppearance().Hash() == pb.ToAppearance().Hash();
                    bool sameOutfit = true;
                    if (pa.OutfitItemIds.Length != pb.OutfitItemIds.Length) sameOutfit = false;
                    else
                    {
                        foreach (string id in pa.OutfitItemIds)
                        {
                            if (System.Array.IndexOf(pb.OutfitItemIds, id) < 0) { sameOutfit = false; break; }
                        }
                    }

                    Assert.IsFalse(sameLook && sameOutfit,
                        "프리셋 " + pa.DisplayName + "와 " + pb.DisplayName + "가 외형·의상 모두 같다");
                }
            }
        }
    }
}
#endif
