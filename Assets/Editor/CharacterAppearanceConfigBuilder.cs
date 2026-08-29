#if UNITY_EDITOR
using System.IO;
using InsectGame.Core;
using InsectGame.Data;
using UnityEditor;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// <see cref="CharacterAppearanceConfig"/> 에셋 생성기.
    ///
    /// 만들어진 에셋의 초기값은 <b>코드 기본값과 같다</b> — 즉 이걸 돌려도 게임은 한 픽셀도
    /// 바뀌지 않는다. 목적은 인스펙터에서 색·프리셋을 눈으로 조정할 수 있게 여는 것뿐이다.
    /// (<c>ItemRarityPaletteBuilder</c>가 같은 규율이다.)
    ///
    /// 에셋이 없어도 게임은 정상 동작한다. 그래서 이 메뉴를 안 돌려도 아무 일도 일어나지 않는다 —
    /// <c>PlayUIPrefabGenerator</c>가 한 번도 실행되지 않은 채 남아 있는 게 그 증거다.
    /// </summary>
    public static class CharacterAppearanceConfigBuilder
    {
        private const string Folder = "Assets/Resources";
        private const string AssetPath = Folder + "/CharacterAppearanceConfig.asset";

        [MenuItem("InsectGame/Data/Build Character Appearance Config")]
        public static void Build()
        {
            CharacterAppearanceConfig asset = CreateOrLoad();
            FillFromCodeDefaults(asset);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterAppearanceConfigBuilder] 저장: {AssetPath} " +
                      $"(피부 {asset.skinColors.Length}색 / 머리 {asset.hairColors.Length}색 / 프리셋 {asset.presets.Length}개)");
            Selection.activeObject = asset;
        }

        /// <summary>배치모드용 진입점 — <c>-executeMethod</c>로 부른다.</summary>
        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        private static CharacterAppearanceConfig CreateOrLoad()
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            CharacterAppearanceConfig existing =
                AssetDatabase.LoadAssetAtPath<CharacterAppearanceConfig>(AssetPath);
            if (existing != null) return existing;

            CharacterAppearanceConfig created = ScriptableObject.CreateInstance<CharacterAppearanceConfig>();
            AssetDatabase.CreateAsset(created, AssetPath);
            return created;
        }

        /// <summary>
        /// 색 배열은 SO의 필드 초기값이 이미 코드와 같으므로 건드리지 않는다.
        /// 프리셋만 코드 테이블에서 복사한다 — 그쪽은 <c>[SerializeField]</c> 초기값으로 표현할 수 없다.
        /// </summary>
        private static void FillFromCodeDefaults(CharacterAppearanceConfig asset)
        {
            CharacterPresetLibrary.Preset[] defaults = CharacterPresetLibrary.CodeDefaults;
            CharacterPresetEntry[] entries = new CharacterPresetEntry[defaults.Length];

            for (int i = 0; i < defaults.Length; i++)
            {
                CharacterPresetLibrary.Preset p = defaults[i];
                entries[i] = new CharacterPresetEntry
                {
                    displayName = p.DisplayName,
                    gender = p.Gender,
                    hairStyle = p.HairStyle,
                    hairColor = p.HairColor,
                    faceType = p.FaceType,
                    skinColor = p.SkinColor,
                    // 배열을 복사한다 — 참조를 그대로 넣으면 에셋과 코드 테이블이 같은 배열을 공유해
                    // 인스펙터 수정이 런타임 기본값까지 바꾼 것처럼 보인다.
                    outfitItemIds = (string[])p.OutfitItemIds.Clone(),
                };
            }

            asset.presets = entries;
        }
    }
}
#endif
