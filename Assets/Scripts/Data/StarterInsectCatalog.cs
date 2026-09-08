using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Data
{
    /// <summary>
    /// 첫 파트너 곤충의 선택지.
    ///
    /// 지급 자체는 여전히 스토리 비트 <c>ch1_intro</c>가 한다 — 여기서 하는 일은 그 비트의
    /// 고정 보상을 <b>플레이어가 고른 종으로 바꿔치기</b>하는 것뿐이다.
    ///
    /// 왜 Story.json 스키마를 늘리지 않았나:
    /// - <c>story_lint</c>의 보상 검사(<c>rewardInsectId ∈ insect_ids</c>)는 <b>단일 값</b>만 본다.
    ///   후보 배열을 새로 만들면 그 배열은 아무도 검사하지 않아 오타가 조용히 남는다.
    /// - 선택 UI가 대화창이 아니라 <b>캐릭터 생성 화면</b>에 있어야 한다(요구사항).
    /// - 키가 없는 기존 세이브는 <see cref="DefaultId"/>로 떨어져 <b>오늘과 완전히 같게</b> 동작한다.
    /// </summary>
    public static class StarterInsectCatalog
    {
        /// <summary>이 비트에서만 오버라이드한다.</summary>
        public const string StarterBeatId = "ch1_intro";

        /// <summary>선택하지 않았거나 값이 이상할 때 주는 종 — Story.json의 원래 보상과 같아야 한다.</summary>
        public const string DefaultId = "longhorn_beetle";

        /// <summary>계정 스코프 PlayerPrefs 키(문자열).</summary>
        public const string PrefsKeyBase = "InsectGame.Character.StarterInsect";

        /// <summary>선택지 하나. 표시명·설명은 생성 화면 카드에 그대로 쓴다.</summary>
        public readonly struct Choice
        {
            public readonly string InsectId;
            public readonly string DisplayName;
            public readonly string Blurb;

            public Choice(string insectId, string displayName, string blurb)
            {
                InsectId = insectId;
                DisplayName = displayName;
                Blurb = blurb;
            }
        }

        /// <summary>
        /// 후보 3종. 등급을 <b>Uncommon으로 맞춰</b> 어느 것을 골라도 초반 난이도가 같게 했다 —
        /// 등급이 갈리면 <c>basePower = 10 + rarity*8</c>·<c>baseHp = 44 + rarity*18</c> 때문에
        /// 시작 전투력이 달라진다.
        ///
        /// <b>왜 Rare에서 내렸나.</b> 옛 3종은 전부 Rare였고, 그건 <c>CreateStableInsect</c> 기준
        /// baseHp 80~91 · baseAtk 34~40이다. 같은 레벨 야생 Common(44~55 / 16~22)의
        /// <b>HP 1.7배 · ATK 1.8배</b>라 초반 전투가 사실상 성립하지 않았다.
        /// Uncommon(62~73 / 25~31)은 야생보다 여전히 우위지만 무쌍은 못 한다.
        ///
        /// <b>등급 숫자를 낮추지 않고 종을 바꿨다.</b> 옛 3종은 야생 풀·가챠 Rare 풀
        /// (<c>GachaBoxManager</c>)에도 쓰이고, 무엇보다 <c>butterfly_swallowtail</c>은
        /// <b>꽃밭 수문장(Lv33)</b>이다 — 등급을 건드리면 그 셋이 함께 흔들린다.
        /// 종을 바꾸니 "첫 파트너가 곧 수문장"이라는 이상한 겹침도 함께 사라졌다.
        ///
        /// 속성은 <c>PlaySceneBootstrap.InferPrimaryType</c>이 id 문자열로 정한다:
        /// <c>longhorn_beetle</c>→"beetle"→Metal(2차 Bug),
        /// <c>dragonfly_lake</c>→"lake"→Water(2차 Wind),
        /// <c>butterfly_monarch</c>→"butterfly"→Wind(2차 Light).
        /// 상성은 옛 구성과 같은 <b>Water &gt; Metal &gt; Wind</b> 2변 사슬이다 — 완전 삼각은
        /// 기본 종의 속성 조합만으로는 성립하지 않는다(Poison/Electric 종이 확장 DB에만 있다).
        ///
        /// <b>등급은 활성 DB에서 확인했다.</b> 판정 기준은 <c>EnsureExpandedDatabase</c> →
        /// <c>CreateStableInsect</c>의 값이지 옛 <c>CreateInsect</c> 목록이 아니다 — 같은 id의
        /// 등급이 두 곳에서 갈릴 수 있다(<c>dragonfly_lake</c>가 실제로 그랬고, 그때는 그게
        /// 탈락 사유였는데 이번엔 채택 사유가 됐다). <c>data_lint</c>가 이 등급 일치를 강제한다.
        /// </summary>
        private static readonly Choice[] Choices =
        {
            new Choice(DefaultId, "하늘소",
                "긴 더듬이로 바람을 읽는다. 단단한 등껍질로 버티며 밀어붙인다."),
            new Choice("dragonfly_lake", "호수잠자리",
                "물 위를 스치듯 나는 사냥꾼. 흐름을 읽고 먼저 움직인다."),
            new Choice("butterfly_monarch", "제왕나비",
                "먼 길을 나는 날개. 가볍게 피하며 틈을 노린다."),
        };

        public static int Count => Choices.Length;

        public static Choice Get(int index)
        {
            return Choices[Mathf.Clamp(index, 0, Choices.Length - 1)];
        }

        /// <summary>고른 종의 인덱스. 저장된 값이 목록에 없으면 0(기본값).</summary>
        public static int IndexOf(string insectId)
        {
            for (int i = 0; i < Choices.Length; i++)
                if (Choices[i].InsectId == insectId) return i;
            return 0;
        }

        /// <summary>선택을 저장한다. 캐릭터 생성 화면이 부른다.</summary>
        public static void SaveChoice(string insectId)
        {
            PlayerPrefs.SetString(SaveScope.PrefsKey(PrefsKeyBase), Sanitize(insectId));
        }

        /// <summary>
        /// 지급 시점에 실제로 줄 종을 정한다.
        ///
        /// <b>화이트리스트 검증이 핵심이다.</b> PlayerPrefs는 사용자가 고칠 수 있으므로,
        /// 목록에 없는 id면 무조건 <paramref name="beatDefault"/>로 떨어뜨린다 —
        /// 안 그러면 전설 곤충 id를 적어 넣어 1레벨에 받을 수 있다.
        /// </summary>
        public static string ResolveChoice(string beatDefault)
        {
            string picked = PlayerPrefs.GetString(SaveScope.PrefsKey(PrefsKeyBase), "");
            for (int i = 0; i < Choices.Length; i++)
                if (Choices[i].InsectId == picked) return picked;

            // 미선택·조작·구세이브 전부 여기로 — 오늘과 같은 동작.
            return beatDefault;
        }

        /// <summary>목록에 없는 값은 저장하지 않는다(조작된 값이 세이브에 눌러앉지 않게).</summary>
        private static string Sanitize(string insectId)
        {
            for (int i = 0; i < Choices.Length; i++)
                if (Choices[i].InsectId == insectId) return insectId;
            return DefaultId;
        }
    }
}
