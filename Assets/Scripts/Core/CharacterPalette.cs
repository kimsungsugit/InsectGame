using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 표면이 빛을 어떻게 받는가. Standard 셰이더의 <c>_Glossiness</c>/<c>_Metallic</c>을 부위별로
    /// 가르는 데 쓴다.
    ///
    /// 왜 필요한가: <see cref="PlayerVisualBuilder.MakeMaterial"/>은 오랫동안 <c>mat.color</c>만
    /// 세팅해서 피부·천·가죽·금속이 전부 Standard 기본 광택(0.5)으로 렌더됐다 — 전부 같은
    /// 재질로 보이는 게 "캐릭터가 점토 같다"의 큰 몫이었다.
    /// <c>InsectEntity.ApplyColorRaw</c>가 곤충 34종에 같은 처방을 이미 적용했고
    /// 그 주석이 이걸 "품질 저하 핵심"으로 기록한다.
    /// </summary>
    public enum SurfaceKind
    {
        /// <summary>피부 — 아주 약한 확산 광택. Head/Neck/Hand/Ear/Nose.</summary>
        Skin,
        /// <summary>천 — 거의 무광. Body/Shirt/Leg.</summary>
        Cloth,
        /// <summary>가죽 — 중간 광택. Boot/Backpack/Strap.</summary>
        Leather,
        /// <summary>머리카락 — 결이 보이는 광택 + 미세 금속감.</summary>
        Hair,
        /// <summary>금속 — 잠자리채 테, 금장식.</summary>
        Metal,
        /// <summary>젖은 표면 — 눈동자·하이라이트.</summary>
        Wet,
    }

    /// <summary>
    /// 캐릭터 색의 <b>단일 출처</b>. 3D 플레이어·3D 마네킹·2D 포트레이트·NPC가 전부 여기를 읽는다.
    ///
    /// 예전엔 피부색이 <b>네 곳</b>에 따로 하드코딩돼 있었다 —
    /// <c>PlayerVisualBuilder</c>(0.92,0.78,0.62) / <c>OutfitShapeLibrary</c>(같은 값) /
    /// <c>CharacterOutfitManager.ApplyToCharacter</c>(같은 값) / <c>NpcVisualBuilder</c>(배열 첫 항).
    /// 게다가 2D 포트레이트는 <b>값 자체가 달라서</b>(0.90,0.75,0.60) UI 속 얼굴과 필드 캐릭터의
    /// 피부톤이 미묘하게 어긋났다. 여기가 그 다섯 갈래를 하나로 모은다.
    ///
    /// 인덱스는 캐릭터 생성 화면의 라디오 순서와 <b>1:1</b>이다. 순서를 바꾸면 기존 세이브의
    /// <c>InsectGame.Character.SkinColor</c>/<c>HairColor</c>가 다른 색을 가리키게 된다 — 바꾸지 말 것.
    /// </summary>
    public static class CharacterPalette
    {
        /// <summary>"밝은 / 보통 / 어두운 / 진한". 2D 포트레이트가 쓰던 값이 기준이다.</summary>
        private static readonly Color[] SkinColors =
        {
            new Color(1.00f, 0.87f, 0.75f),
            new Color(0.90f, 0.75f, 0.60f),
            new Color(0.65f, 0.50f, 0.35f),
            new Color(0.40f, 0.28f, 0.18f),
        };

        /// <summary>"검정 / 갈색 / 금발 / 빨강 / 보라 / 파랑". 2D와 3D가 원래 같은 값이었다.</summary>
        private static readonly Color[] HairColors =
        {
            new Color(0.12f, 0.08f, 0.05f),
            new Color(0.35f, 0.20f, 0.10f),
            new Color(0.85f, 0.70f, 0.30f),
            new Color(0.60f, 0.15f, 0.10f),
            new Color(0.20f, 0.15f, 0.35f),
            new Color(0.15f, 0.30f, 0.50f),
        };

        // 아래 네 접근자는 CharacterAppearanceConfig 에셋이 있으면 그쪽 값을 쓴다.
        // 에셋이 없는 게 정상 경로이며, 그때는 위 코드 배열이 답이다
        // (그렇게 잡은 이유는 CharacterAppearanceConfig의 클래스 주석에 있다).

        /// <summary>생성 화면의 피부색 선택지 수. 라디오 라벨 배열과 길이가 같아야 한다.</summary>
        public static int SkinCount => ActiveSkinColors.Length;

        /// <summary>생성 화면의 머리색 선택지 수.</summary>
        public static int HairCount => ActiveHairColors.Length;

        private static Color[] ActiveSkinColors => CharacterPresetLibrary.SkinColorsOrNull() ?? SkinColors;

        private static Color[] ActiveHairColors => CharacterPresetLibrary.HairColorsOrNull() ?? HairColors;

        /// <summary>범위 밖 인덱스는 clamp한다 — 구세이브·손상된 PlayerPrefs가 예외를 내지 않게.</summary>
        public static Color Skin(int index)
        {
            Color[] src = ActiveSkinColors;
            return src[Mathf.Clamp(index, 0, src.Length - 1)];
        }

        public static Color Hair(int index)
        {
            Color[] src = ActiveHairColors;
            return src[Mathf.Clamp(index, 0, src.Length - 1)];
        }

        /// <summary>
        /// 외형 정보를 모르는 자리(레시피 상수 등)가 쓰는 기본 피부색 = "보통".
        /// 옛 하드코딩 (0.92,0.78,0.62)를 대체한다.
        /// </summary>
        public static Color DefaultSkin => Skin(1);

        /// <summary>
        /// 부위 재질을 머티리얼에 얹는다. <b>색은 건드리지 않는다</b> — 호출부가 이미 칠한 뒤다.
        ///
        /// <c>InsectEntity.ApplyColorRaw</c>와 같은 가드를 쓴다: Standard/URP Lit이 아니면
        /// (<c>Unlit/Color</c>·<c>Sprites/Default</c> 폴백) 해당 프로퍼티가 없으므로 그냥 돌아간다.
        /// <c>"Unlit/Color".Contains("Lit")</c>는 대소문자가 달라 false다 — 의도된 판정이다.
        ///
        /// <c>_Glossiness</c>(Standard)와 <c>_Smoothness</c>(URP)를 함께 세팅한다.
        /// 알파가 있는 머티리얼의 블렌드 모드는 <b>건드리지 않는다</b> — 지금 홍조·하이라이트가
        /// 그 상태로 의도대로 보이고 있어, 여기서 <c>_Mode</c>를 바꾸면 사라지는 회귀가 난다.
        /// </summary>
        public static void ApplySurface(Material mat, SurfaceKind kind)
        {
            if (mat == null || mat.shader == null) return;

            string shaderName = mat.shader.name;
            bool pbr = shaderName == "Standard" || shaderName.Contains("Lit");
            if (!pbr) return;

            SurfaceValues(kind, out float gloss, out float metallic);
            mat.SetFloat("_Glossiness", gloss);
            mat.SetFloat("_Smoothness", gloss);
            mat.SetFloat("_Metallic", metallic);
        }

        /// <summary>
        /// 재질 수치표. <see cref="ApplySurface"/>에서 분리해 둔 건 <b>순수 함수라 테스트할 수 있기</b>
        /// 때문이다 — Material을 만들려면 셰이더가 필요해 값 검증이 렌더 환경에 묶인다.
        /// </summary>
        internal static void SurfaceValues(SurfaceKind kind, out float glossiness, out float metallic)
        {
            switch (kind)
            {
                case SurfaceKind.Skin:    glossiness = 0.22f; metallic = 0.00f; break;
                case SurfaceKind.Cloth:   glossiness = 0.10f; metallic = 0.00f; break;
                case SurfaceKind.Leather: glossiness = 0.34f; metallic = 0.00f; break;
                case SurfaceKind.Hair:    glossiness = 0.46f; metallic = 0.04f; break;
                case SurfaceKind.Metal:   glossiness = 0.68f; metallic = 0.80f; break;
                case SurfaceKind.Wet:     glossiness = 0.85f; metallic = 0.00f; break;
                default:                  glossiness = 0.30f; metallic = 0.00f; break;
            }
        }
    }
}
