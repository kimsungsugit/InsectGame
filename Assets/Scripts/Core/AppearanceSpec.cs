using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 캐릭터 외형 중 <b>의상이 아닌</b> 부분(성별·머리·얼굴·피부). PlayerPrefs가 정하는 값들을
    /// 한 덩어리로 묶어 <see cref="PlayerVisualBuilder.BuildForPreview"/>가 주입받을 수 있게 한다 —
    /// 프리뷰 마네킹은 PlayerPrefs를 직접 읽지 않고 이걸 받는다.
    ///
    /// <c>PlayerVisualBuilder.cs</c> 안에 있던 것을 여기로 옮겼다. 그 파일은 지오메트리 담당이
    /// 고치고 이 struct는 데이터/세이브 담당이 고치는데, 한 파일에 있으면 두 담당이 같은 파일을
    /// 동시에 건드리게 된다(<c>agent-coordination.md</c>). 같은 네임스페이스라 <c>using</c> 변경은 없다.
    /// </summary>
    public struct AppearanceSpec
    {
        public int gender;
        public int hairStyle;
        public int hairColor;
        public int faceType;
        /// <summary>
        /// <see cref="CharacterPalette.Skin"/>의 인덱스(0~3).
        ///
        /// 이 필드는 오래 <b>없었다</b>. 캐릭터 생성 화면이 피부색을 골라
        /// <c>InsectGame.Character.SkinColor</c>에 저장하고 클라우드 동기까지 하는데, 3D 빌더는
        /// 그걸 읽지 않고 (0.92,0.78,0.62)를 하드코딩해서 <b>2D 초상화만 고른 색으로 바뀌고
        /// 필드 캐릭터는 늘 같은 피부</b>였다. 키·세이브·클라우드 배선은 이미 다 있었으므로
        /// 여기에 필드를 더해 읽기만 하면 된다(마이그레이션 불필요).
        /// </summary>
        public int skinColor;

        public static AppearanceSpec FromPlayerPrefs()
        {
            return new AppearanceSpec
            {
                gender = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.Gender"), 0),
                hairStyle = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.HairStyle"), 0),
                hairColor = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.HairColor"), 0),
                faceType = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.FaceType"), 0),
                skinColor = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.SkinColor"), 0),
            };
        }

        /// <summary>이 외형의 실제 피부색.</summary>
        public Color SkinTone => CharacterPalette.Skin(skinColor);

        /// <summary>이 외형의 실제 머리색.</summary>
        public Color HairTone => CharacterPalette.Hair(hairColor);

        /// <summary>
        /// 프리뷰 썸네일 캐시 키용. 외형이 바뀌면 구운 썸네일이 전부 낡는다.
        /// <b>필드를 늘리면 여기도 함께 늘려야 한다</b> — 안 그러면 마네킹이 옛 외형으로 남는다.
        /// </summary>
        public int Hash()
        {
            return (((gender * 31 + hairStyle) * 31 + hairColor) * 31 + faceType) * 31 + skinColor;
        }
    }
}
