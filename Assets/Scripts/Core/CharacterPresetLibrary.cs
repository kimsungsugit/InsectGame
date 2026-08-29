using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 캐릭터 생성 화면의 프리셋과 색 팔레트의 <b>단일 출처</b>.
    ///
    /// 값은 코드에 있고, <see cref="CharacterAppearanceConfig"/> 에셋이 있으면 그쪽이 이긴다.
    /// 순서가 그 반대가 아닌 이유는 <see cref="CharacterAppearanceConfig"/> 주석에 적어 뒀다 —
    /// 이 저장소의 <c>Resources.Load</c> SO 다섯 개가 전부 에셋 없이 폴백으로 돌고 있어서,
    /// 에셋을 정답으로 삼으면 "생성기를 안 돌렸다"가 조용한 값 회귀가 된다.
    ///
    /// 프리셋 인덱스는 <c>InsectGame.Character.OutfitPreset</c>으로 저장되므로 <b>순서 불변</b>이다.
    /// 0~2는 옛 "탐험가/연구원/자유"의 의상 구성을 그대로 물려받는다.
    /// </summary>
    public static class CharacterPresetLibrary
    {
        /// <summary>
        /// 프리셋 하나. 외형(성별·머리·얼굴·피부)과 시작 의상을 함께 정한다.
        ///
        /// 예전엔 의상만 정했고 외형은 라디오로 따로 골랐다 — 그래서 "탐험가"를 골라도
        /// 머리·얼굴은 전부 0번 기본값이었다. 프리셋이 사람 하나를 통째로 표현하도록 묶었다.
        /// </summary>
        public readonly struct Preset
        {
            public readonly string DisplayName;
            public readonly int Gender;
            public readonly int HairStyle;
            public readonly int HairColor;
            public readonly int FaceType;
            public readonly int SkinColor;
            public readonly string[] OutfitItemIds;

            public Preset(string displayName, int gender, int hairStyle, int hairColor,
                int faceType, int skinColor, string[] outfitItemIds)
            {
                DisplayName = displayName;
                Gender = gender;
                HairStyle = hairStyle;
                HairColor = hairColor;
                FaceType = faceType;
                SkinColor = skinColor;
                OutfitItemIds = outfitItemIds;
            }

            /// <summary>이 프리셋의 외형만 뽑아낸다 — 3D 프리뷰가 그대로 받아 쓴다.</summary>
            public AppearanceSpec ToAppearance()
            {
                return new AppearanceSpec
                {
                    gender = Gender,
                    hairStyle = HairStyle,
                    hairColor = HairColor,
                    faceType = FaceType,
                    skinColor = SkinColor,
                };
            }
        }

        // ── 코드 기본값 ──────────────────────────────────────
        //
        // 의상은 전부 unlockedByDefault인 것만 쓴다. CharacterOutfitManager.Equip에 소유 가드가
        // 있어 미보유 아이템은 경고 로그만 남기고 무시되기 때문이다 — 예전에 "연구원"이 4개,
        // "자유"가 3개를 미보유로 지정해서 그 프리셋들이 실제로는 거의 적용되지 않았다.
        // CharacterPresetTests가 이 불변식을 고정한다.
        //
        // EXP/캔디 배율이 붙은 outer_labcoat·bag_science는 일부러 넣지 않았다.
        // 시작부터 무료로 주면 경제 곡선이 흔들리고, 시각적 차별화는 색·형태로 충분하다.

        private static readonly Preset[] Defaults =
        {
            new Preset("초원의 탐험가", 0, 0, 1, 0, 1, new[]
                { "hat_cap", "top_shirt", "outer_jacket", "bot_pants", "shoe_boots", "bag_basic", "tool_net" }),

            new Preset("숲의 관찰자", 1, 2, 0, 2, 1, new[]
                { "hat_none", "top_shirt", "outer_none", "bot_pants", "shoe_sneakers", "bag_basic", "tool_magnify" }),

            new Preset("들판의 아이", 0, 3, 2, 1, 0, new[]
                { "hat_none", "top_polo", "outer_none", "bot_shorts", "shoe_sandals", "bag_none", "tool_net" }),

            new Preset("밤의 채집가", 1, 3, 4, 3, 2, new[]
                { "hat_cap", "top_shirt", "outer_jacket", "bot_pants", "shoe_boots", "bag_basic", "tool_magnify" }),

            new Preset("직접 만들기", 0, 0, 0, 0, 1, new[]
                { "hat_cap", "top_shirt", "outer_jacket", "bot_pants", "shoe_boots", "bag_basic", "tool_net" }),
        };

        // ── 에셋 오버라이드 ──────────────────────────────────

        private static CharacterAppearanceConfig config;
        private static bool configLoadAttempted;
        private static bool fallbackLogged;

        /// <summary>
        /// 에셋을 한 번만 찾는다. 없으면 이후로는 코드 기본값을 쓴다(재시도하지 않는다) —
        /// 이 경로는 프리셋 조회마다 불리므로 매번 <c>Resources.Load</c>를 두드리면 안 된다.
        /// </summary>
        private static CharacterAppearanceConfig Config
        {
            get
            {
                if (configLoadAttempted) return config;
                configLoadAttempted = true;
                config = Resources.Load<CharacterAppearanceConfig>(CharacterAppearanceConfig.ResourcePath);

                if (config == null && !fallbackLogged)
                {
                    fallbackLogged = true;
                    // 에러가 아니다 — 코드 기본값이 정상 경로다.
                    Debug.Log("[CharacterPresetLibrary] CharacterAppearanceConfig 에셋이 없어 코드 기본값을 씁니다. " +
                              "메뉴 InsectGame/Data/Build Character Appearance Config 로 만들 수 있습니다.");
                }
                return config;
            }
        }

        /// <summary>
        /// 코드 기본 프리셋. 두 곳이 쓴다 —
        /// 테스트가 <b>에셋 유무와 무관하게</b> 기본값 자체의 정합성을 검사하고,
        /// <c>CharacterAppearanceConfigBuilder</c>가 이걸 그대로 에셋에 굽는다.
        ///
        /// <c>public</c>인 이유: <c>Assets/Editor/</c>는 별도 어셈블리(Assembly-CSharp-Editor)라
        /// <c>internal</c>이 보이지 않는다. 테스트는 asmdef가 없어 게임 어셈블리로 컴파일되므로
        /// 그쪽만 보고 <c>internal</c>로 두면 에디터 빌드에서만 깨진다.
        /// </summary>
        public static Preset[] CodeDefaults => Defaults;

        // ── 조회 ─────────────────────────────────────────────

        public static int Count
        {
            get
            {
                CharacterAppearanceConfig c = Config;
                return c != null && c.HasUsablePresets ? c.presets.Length : Defaults.Length;
            }
        }

        /// <summary>범위 밖 인덱스는 clamp한다 — 구세이브의 OutfitPreset이 커도 예외가 나지 않게.</summary>
        public static Preset Get(int index)
        {
            CharacterAppearanceConfig c = Config;
            if (c != null && c.HasUsablePresets)
            {
                CharacterPresetEntry e = c.presets[Mathf.Clamp(index, 0, c.presets.Length - 1)];
                if (e != null && e.outfitItemIds != null && e.outfitItemIds.Length > 0)
                {
                    return new Preset(e.displayName, e.gender, e.hairStyle, e.hairColor,
                        e.faceType, e.skinColor, e.outfitItemIds);
                }
                // 항목이 반쪽이면 그 자리만 코드 기본값으로 물러난다.
            }
            return Defaults[Mathf.Clamp(index, 0, Defaults.Length - 1)];
        }

        /// <summary>생성 화면 라디오 라벨. 프리셋 이름이 곧 라벨이다.</summary>
        public static string[] DisplayNames()
        {
            int n = Count;
            string[] names = new string[n];
            for (int i = 0; i < n; i++) names[i] = Get(i).DisplayName;
            return names;
        }

        // ── 색 팔레트 (CharacterPalette가 위임받는다) ──

        internal static Color[] SkinColorsOrNull()
        {
            CharacterAppearanceConfig c = Config;
            return c != null && c.HasUsableSkinColors ? c.skinColors : null;
        }

        internal static Color[] HairColorsOrNull()
        {
            CharacterAppearanceConfig c = Config;
            return c != null && c.HasUsableHairColors ? c.hairColors : null;
        }
    }
}
