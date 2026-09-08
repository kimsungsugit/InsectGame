using UnityEngine;

namespace InsectGame.Data
{
    /// <summary>
    /// 캐릭터 생성 화면의 프리셋 하나 — 외형 + 시작 의상.
    ///
    /// 클래스로 감싼 이유: Unity는 <c>string[][]</c> 같은 중첩 배열을 직렬화하지 못한다.
    /// <c>[Serializable]</c> 클래스 안의 <c>string[]</c>는 1단계라 직렬화된다.
    /// </summary>
    [System.Serializable]
    public class CharacterPresetEntry
    {
        public string displayName;

        [Tooltip("0=남자, 1=여자")]
        public int gender;
        [Tooltip("0=짧은, 1=중간, 2=긴, 3=올림")]
        public int hairStyle;
        [Tooltip("0=검정, 1=갈색, 2=금발, 3=빨강, 4=보라, 5=파랑")]
        public int hairColor;
        [Tooltip("0=미소, 1=활짝, 2=차분, 3=무표정")]
        public int faceType;
        [Tooltip("0=밝은, 1=보통, 2=어두운, 3=진한")]
        public int skinColor;

        [Tooltip("시작 의상. 전부 unlockedByDefault여야 한다 — 아니면 Equip이 조용히 무시한다.")]
        public string[] outfitItemIds;
    }

    /// <summary>
    /// 캐릭터 외형 데이터의 <b>선택적</b> 에셋. 색 팔레트와 생성 화면 프리셋을 담는다.
    ///
    /// <b>이 에셋이 없어도 게임은 정상 동작한다.</b> 코드 기본값
    /// (<see cref="InsectGame.Core.CharacterPresetLibrary"/>)이 정답 경로이고 이 에셋은
    /// 인스펙터로 값을 만져보고 싶을 때 얹는 오버라이드다.
    ///
    /// 왜 그렇게 잡았나: 이 저장소에서 <c>Resources.Load</c>로 SO를 찾는 다섯 곳
    /// (<c>InsectDatabase</c>·<c>ItemDatabase</c>·<c>GameplayTuningProfile</c>·<c>UITheme</c>·
    /// <c>PlayUIConfig</c>)이 <b>전부 에셋 파일 없이</b> 코드 폴백으로 돌고 있다.
    /// 에셋을 필수로 잡으면 누군가 생성기를 안 돌린 순간 값이 통째로 옛것으로 돌아가는데,
    /// 그게 예외도 경고도 없이 일어난다. 실제로 <c>Assets/Editor/PlayUIPrefabGenerator.cs</c>는
    /// 한 번도 실행된 적이 없다.
    ///
    /// 필드 초기값은 코드 기본값과 <b>같게</b> 둔다 — <c>CreateInstance</c> 결과가 곧 현행 동작이라야
    /// 생성기가 만든 에셋이 게임을 바꾸지 않는다(<c>ItemRarityPalette</c>가 같은 규율이다).
    /// </summary>
    [CreateAssetMenu(menuName = "InsectGame/Character Appearance Config", fileName = "CharacterAppearanceConfig")]
    public class CharacterAppearanceConfig : ScriptableObject
    {
        /// <summary><c>Resources.Load</c> 경로. 폴더 없이 Resources 바로 아래다.</summary>
        public const string ResourcePath = "CharacterAppearanceConfig";

        [Header("피부색 — 생성 화면 라디오 순서와 1:1 (밝은/보통/어두운/진한)")]
        [Tooltip("순서를 바꾸면 기존 세이브의 Character.SkinColor가 다른 색을 가리킨다.")]
        public Color[] skinColors =
        {
            new Color(1.00f, 0.87f, 0.75f),
            new Color(0.90f, 0.75f, 0.60f),
            new Color(0.65f, 0.50f, 0.35f),
            new Color(0.40f, 0.28f, 0.18f),
        };

        [Header("머리색 — 검정/갈색/금발/빨강/보라/파랑")]
        public Color[] hairColors =
        {
            new Color(0.12f, 0.08f, 0.05f),
            new Color(0.35f, 0.20f, 0.10f),
            new Color(0.85f, 0.70f, 0.30f),
            new Color(0.60f, 0.15f, 0.10f),
            new Color(0.20f, 0.15f, 0.35f),
            new Color(0.15f, 0.30f, 0.50f),
        };

        [Header("생성 화면 프리셋")]
        [Tooltip("인덱스가 Character.OutfitPreset으로 저장된다 — 순서를 바꾸지 말 것.")]
        public CharacterPresetEntry[] presets;

        /// <summary>
        /// 배열이 비었거나 길이가 안 맞으면 그 항목은 코드 기본값으로 물러난다.
        /// 손으로 만든 에셋이 반쪽인 채로 게임을 망가뜨리지 않게 하는 가드다.
        /// </summary>
        public bool HasUsableSkinColors => skinColors != null && skinColors.Length > 0;

        public bool HasUsableHairColors => hairColors != null && hairColors.Length > 0;

        public bool HasUsablePresets => presets != null && presets.Length > 0;
    }
}
