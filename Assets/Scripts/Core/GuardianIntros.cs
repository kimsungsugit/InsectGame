namespace InsectGame.Core
{
    /// <summary>
    /// 수문장 등장 화면의 글 — 리전마다 <b>별칭 한 줄</b>과 <b>등장 한 줄</b>.
    ///
    /// 수문장전은 대부분 레이드로 치르는데(영웅·전설), 레이드에는 대결 컷인(<c>DuelBanter</c>)이 없어 초원의 수호자도
    /// 이름 없는 사마귀도 필드의 곤충처럼 "나타났다" 한 줄로 시작했다. 아이가 "이건 특별한 싸움이다"를 느끼도록
    /// 시작 순간에 「○○의 수문장 · 별칭 / 곤충 이름 / 등장 한 줄」을 띄운다. 그리기는 ui-dev(레이드 시작 화면)가 하고,
    /// 칭호 「○○의 수문장」은 리전 표시명, 곤충 이름은 <c>RegionData.guardianDisplayName</c>에서 온다 — 여기는 그 사이의
    /// 두 줄만 든다(같은 말을 두 곳에 두지 않는다).
    ///
    /// <b>쉬운 말 규칙</b>(StoryBible 15장): 별칭 ≤ <see cref="MaxEpithetChars"/>자, 등장 ≤ <see cref="MaxLineChars"/>자.
    /// 「그림자」는 울타리 밖의 그것에만 쓴다(2장) — 안개·어둠 묘사에 섞지 않는다. 「무명」·「봉인」은 쓰지 않는다.
    /// 수문장이 있는 리전 전부를 덮는지와 길이는 <c>GuardianIntroTests</c>가 실제 리전 정의로 본다.
    ///
    /// 순수 데이터다. 배지 표(<see cref="GuardianBadges"/>)와 같은 리전 키를 쓴다.
    /// </summary>
    public static class GuardianIntros
    {
        public const int MaxEpithetChars = 16;
        public const int MaxLineChars = 30;

        public struct Intro
        {
            public string regionId;
            /// <summary>별칭 — 칭호 옆에 붙는 짧은 이름("풀숲의 낫").</summary>
            public string epithet;
            /// <summary>등장 한 줄 — 수문장이 나서는 그림("풀숲이 갈라지고 큰 낫이 번쩍인다!").</summary>
            public string line;
        }

        private static readonly Intro[] Table =
        {
            // ── 1막 ──
            new Intro { regionId = "meadow",   epithet = "풀숲의 낫",       line = "풀숲이 갈라지고 큰 낫이 번쩍인다!" },
            new Intro { regionId = "pond",     epithet = "물 위의 번개",     line = "수면을 가르며 왕잠자리가 내려온다!" },
            new Intro { regionId = "forest",   epithet = "숲의 큰 뿔",       line = "땅이 울린다. 거대한 뿔이 길을 막는다!" },
            new Intro { regionId = "swamp",    epithet = "안개 속 유령",     line = "안개 속에서 하얀 사마귀가 나타난다!" },
            new Intro { regionId = "mountain", epithet = "산바람의 거인",    line = "커다란 날개가 산바람을 뒤집는다!" },
            new Intro { regionId = "garden",   epithet = "꽃길의 날개",      line = "꽃잎 사이로 호랑나비가 길을 막는다!" },
            new Intro { regionId = "ruins",    epithet = "모래 왕관",        line = "기둥에서 모래가 흘러내린다. 깨어났다!" },
            // ── 2막 ──
            new Intro { regionId = "hollow",    epithet = "소리 없는 낫",    line = "소리 없는 들판에 사마귀가 서 있다." },
            new Intro { regionId = "dunes",     epithet = "모래바람 침",     line = "모래바람 속에서 날갯소리가 울린다!" },
            new Intro { regionId = "frostline", epithet = "얼음빛 날개",     line = "얼음 벽이 빛나고 오로라 날개가 펼쳐진다!" },
            new Intro { regionId = "emberfall", epithet = "불티의 침",       line = "불티를 흩뿌리며 용암말벌이 날아든다!" },
            new Intro { regionId = "canopy",    epithet = "하늘 나무의 날개", line = "우듬지가 술렁이고 큰 나비가 내려앉는다." },
            new Intro { regionId = "nameless",  epithet = "이름 없는 낫",    line = "모습이 자꾸 바뀌는 사마귀가 길을 막는다!" },
        };

        /// <summary>이 리전 수문장의 등장 글. 없으면 false — 그리는 쪽은 별칭·등장 줄 없이 칭호와 이름만 띄운다.</summary>
        public static bool TryGet(string regionId, out Intro intro)
        {
            if (!string.IsNullOrEmpty(regionId))
            {
                for (int i = 0; i < Table.Length; i++)
                {
                    if (Table[i].regionId == regionId)
                    {
                        intro = Table[i];
                        return true;
                    }
                }
            }
            intro = default;
            return false;
        }

        /// <summary>표 전체 — 테스트·검사용(값 복사).</summary>
        public static Intro[] All()
        {
            Intro[] copy = new Intro[Table.Length];
            System.Array.Copy(Table, copy, Table.Length);
            return copy;
        }
    }
}
