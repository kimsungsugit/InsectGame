using System.Collections.Generic;

namespace InsectGame.NPC
{
    /// <summary>
    /// 주민 대사/이름 정적 데이터베이스.
    /// npcId 해시 시드로 개인 고정 대사 1줄 + 리전 대사 1줄 + 공용 힌트 1줄을 조합 —
    /// 재방문 시 동일 인물은 항상 동일 톤(결정적).
    /// </summary>
    public static class NpcDialogueDatabase
    {
        // ── 주민 한글 이름 풀 (15개+) ──
        private static readonly string[] Names =
        {
            "김민준", "이서연", "박도윤", "최하은", "정지호",
            "강수아", "조예준", "윤시우", "장하린", "임지우",
            "한은우", "오소율", "서준서", "신다은", "권건우",
            "황아린", "안유찬", "송나은",
        };

        // ── 개인 고정 대사 (npcId 시드로 1인 1줄 고정 — 인물 톤) ──
        private static readonly string[] PersonalLines =
        {
            "안녕하세요! 오늘도 좋은 하루네요.",
            "우리 마을에 온 걸 환영해요!",
            "곤충 채집가라니, 멋진 일을 하시네요.",
            "요즘 허리가 쑤셔서 큰일이야... 그래도 산책은 빼먹을 수 없지.",
            "어릴 적엔 나도 곤충 박사가 꿈이었답니다.",
            "당신 잠자리채, 꽤 좋아 보이는데요?",
            "산책하기 딱 좋은 날씨예요.",
            "마을 사람들은 다들 친절하니 편하게 지내요.",
            "저녁 반찬거리를 고민 중이에요. 별일 없죠?",
            "낯선 얼굴이네요. 종종 들러줘요!",
        };

        // ── 리전별 대사 풀 (regionId → 3~5줄: 분위기/힌트) ──
        private static readonly Dictionary<string, string[]> RegionLines = new Dictionary<string, string[]>
        {
            ["meadow"] = new[]
            {
                "이 초원은 초보 채집가한테 딱이에요. 온순한 곤충이 많거든요.",
                "풀숲을 천천히 살펴보세요. 메뚜기가 잘 숨어 있어요.",
                "초원 너머 연못 쪽엔 물가 곤충이 산대요.",
                "바람 부는 날엔 나비가 낮게 날아요. 잡기 좋죠.",
            },
            ["pond"] = new[]
            {
                "연못가는 축축하니 발밑 조심해요.",
                "물잠자리는 물가 가까이에서만 보여요.",
                "비 오는 날 연못엔 평소 못 보던 손님이 온답니다.",
                "연못 수문장은 만만치 않다던데... 준비 단단히 해요.",
            },
            ["forest"] = new[]
            {
                "숲은 낮에도 어둑해요. 나무 둥치를 잘 살펴봐요.",
                "사슴벌레는 수액 냄새 나는 나무를 좋아해요.",
                "숲 깊은 곳에 숨겨진 장소가 있다는 소문이 있어요.",
                "밤의 숲은 위험하지만, 밤에만 나오는 곤충도 있죠.",
            },
            ["swamp"] = new[]
            {
                "늪지대는 걸음이 느려지니 서두르지 말아요.",
                "여기 곤충들은 좀 사나워요. 조심해서 다가가요.",
                "안개 낀 날 늪에선 희귀한 그림자를 봤다는 사람도 있어요.",
            },
            ["mountain"] = new[]
            {
                "산바람이 차죠? 높은 곳 곤충은 튼튼하답니다.",
                "바위 틈을 잘 보세요. 단단한 등껍질이 숨어 있어요.",
                "산 정상 쪽은 수문장을 이겨야 오를 수 있어요.",
            },
            ["garden"] = new[]
            {
                "이 정원은 마을의 자랑이에요. 꽃마다 나비가 달라요.",
                "꽃향기가 진한 날엔 곤충이 더 모여들어요.",
                "정원사 말로는 새벽에 제일 예쁜 곤충이 온대요.",
            },
            ["ruins"] = new[]
            {
                "이 유적엔 오래된 이야기가 잠들어 있어요.",
                "유적의 곤충은 어딘가 신비로워요. 빛나는 걸 봤다니까요.",
                "돌기둥 그늘을 살펴봐요. 고대 곤충이 숨어 있을지도.",
                "여기까지 온 채집가는 몇 없어요. 대단하네요.",
            },
            // ── 2막(ver2) ── 미등록이면 DefaultRegionLines("이 근처엔 재미있는 곤충이 많아요")로
            // 떨어져 전초기지 주민 6곳이 전부 같은 말을 한다. 리전 색이 사라지는 자리라 채워 둔다.
            ["hollow"] = new[]
            {
                "여긴 소리가 없어요. 처음 왔을 땐 귀가 먹은 줄 알았다니까요.",
                "표석에 원래 이름이 새겨져 있었대요. 지금은 다 지워졌지만요.",
                "그래도 당신이 한 마리씩 잡을 때마다 조금씩 소리가 돌아오는 것 같아요.",
                "검은 그림자 같은 걸 봤다면 쫓지 말고 부르세요. 저건 잃어버린 아이예요.",
            },
            ["dunes"] = new[]
            {
                "모래 밑에 상자가 잔뜩 묻혀 있어요. 누가 두고 갔는지는 몰라요.",
                "한낮엔 다니지 마세요. 곤충도 그때는 다 숨어요.",
                "깔때기 모양 구덩이 보이면 돌아가요. 개미귀신 함정이에요.",
            },
            ["frostline"] = new[]
            {
                "얼음 벽에 글씨가 있어요. 아직도 선명해요, 신기하죠.",
                "여긴 아무것도 안 상해요. 대신 아무것도 안 자라죠.",
                "장갑 없이 돌 만지지 마세요. 손이 붙어요.",
            },
            ["emberfall"] = new[]
            {
                "발밑이 아직 따뜻하죠? 여긴 재가 안 식어요.",
                "갱도 근처는 가지 마세요. 요새 자주 내려앉아요.",
                "이 골짜기 기록은 통째로 타 버렸대요. 남은 게 하나도 없어요.",
            },
            ["canopy"] = new[]
            {
                "위를 봐요. 저 나무 꼭대기까지 올라가면 세상이 다 보인대요.",
                "여긴 아무것도 사라진 적이 없어요. 이 근방에서 유일하게요.",
                "나무껍질에 무늬가 있어요. 유적에서 봤다는 그 무늬랑 같대요.",
                "잎이 겹친 데는 낮에도 어둑해요. 거기 사는 애들이 따로 있죠.",
            },
            ["nameless"] = new[]
            {
                "이 땅은 어느 지도에도 안 나와요. 우리가 지금 서 있는데도요.",
                "석판이 둥글게 서 있죠. 뭔가를 둘러싸고 있었던 모양이래요.",
                "여기서는 이름을 함부로 말하지 마세요. …농담이 아니에요.",
            },
        };

        // 미등록 리전 폴백
        private static readonly string[] DefaultRegionLines =
        {
            "이 근처엔 재미있는 곤충이 많아요.",
            "천천히 둘러보세요. 좋은 곳이랍니다.",
            "이 동네는 처음이죠? 금방 익숙해질 거예요.",
        };

        // ── 공용 힌트 풀 (10줄+) ──
        private static readonly string[] CommonHints =
        {
            "각 지역의 수문장을 쓰러뜨리면 다음 지역으로 갈 수 있어요.",
            "수풀 사이 어딘가에 숨겨진 동굴 같은 곳이 있대요. 잘 찾아봐요.",
            "훈련소에서 곤충을 단련시킬 수 있어요. 캔디가 좀 들지만요.",
            "상점 랜덤상자에서 좋은 물건이 나온다던데, 운을 시험해 봐요.",
            "비 오는 날엔 평소에 못 보던 곤충이 나타나요.",
            "밤에만 나오는 곤충도 있어요. 시간대를 바꿔 다녀 보세요.",
            "동네 아이들이 요즘 곤충 잡기에 푹 빠졌어요. 뺏기기 전에 서둘러요!",
            "반짝반짝 빛나는 곤충을 봤다는 소문이 있어요. 정말일까요?",
            "희귀한 곤충일수록 천천히 다가가야 도망을 안 가요.",
            "잡은 곤충은 도감에 기록돼요. 전부 모아보는 건 어때요?",
            "보석이 부족하면 상점에서 충전할 수 있다더군요.",
            "포획 아이템을 쓰면 잡을 확률이 올라가요. 아끼지 말아요.",
        };

        /// <summary>
        /// 스토리 인물 전용 잡담. <b>비트가 없을 때</b> 쓰인다 — 아직 소개되지 않았거나
        /// 그 인물의 비트를 전부 열람한 뒤다.
        ///
        /// 없으면 <see cref="GetLines"/>의 마을 주민 풀로 떨어지는데, 그러면 명부회 간부가
        /// "오늘 날씨가 좋네요" 류를 말한다. 최종 보스인 관장도 같았다.
        ///
        /// <b>줄거리를 진행시키지 않는다.</b> 여기에 정보를 담으면 저작된 비트를 우회하는
        /// 두 번째 서사 경로가 생긴다 — 인상만 남기고 끝낸다.
        /// </summary>
        private static readonly Dictionary<string, string[]> StoryNpcLines =
            new Dictionary<string, string[]>
        {
            { "village_elder", new[] {
                "허허, 또 왔구나. 몸은 성한 게냐.",
                "무리하진 말거라. 도감은 도망가지 않는단다.",
            } },
            { "catcher_rival", new[] {
                "오, 또 만났네. 몇 마리 늘었어?",
                "난 아직 안 졌어. 그거 기억해 둬.",
            } },
            { "ruins_scholar", new[] {
                "기록은 잘 되고 있나요?",
                "한 마리씩이면 돼요. 조급해하지 말아요.",
            } },
            { "ledger_thug_cord", new[] {
                "…볼일 없으면 비켜라.",
                "여긴 우리가 맡은 구역이다.",
            } },
            { "ledger_thug_rule", new[] {
                "기록할 게 있는 얼굴은 아니군.",
                "돌아가라. 두 번은 말 안 한다.",
            } },
            { "ledger_thug_pin", new[] {
                "…그물 근처엔 오지 마. 나도 곤란해져.",
                "나는 치라니까 친 거야. 그 이상은 몰라.",
            } },
            { "ledger_grip", new[] {
                "손이 비었군. 그럼 할 말도 없다.",
                "여기 있는 건 전부 장부에 오른 것들이다.",
            } },
            { "ledger_scale", new[] {
                "등급, 개체수, 상태. 셋 중 뭘 물으러 왔나.",
                "숫자로 말해라. 그 편이 빠르다.",
            } },
            { "ledger_ink", new[] {
                "…적을 게 없는 날도 있다.",
                "붓을 놓으면 손이 허전해서.",
            } },
            { "ledger_chief", new[] {
                "시간이 없다. 나에게도, 저 아이들에게도.",
                "네 방식이 틀렸다곤 안 했다. 느리다고 했지.",
            } },
        };

        /// <summary>
        /// 스토리 인물의 전용 잡담. 없으면 false — 호출부가 마을 주민 풀로 떨어진다.
        /// </summary>
        public static bool TryGetStoryNpcLines(string storyNpcId, out string[] lines)
        {
            lines = null;
            return !string.IsNullOrEmpty(storyNpcId)
                && StoryNpcLines.TryGetValue(storyNpcId, out lines);
        }

        /// <summary>
        /// npcId 해시 시드 기반 결정적 대사 3줄 — 개인 1 + 리전 1 + 공용 힌트 1.
        /// 같은 npcId/regionId면 항상 같은 조합(재방문 동일 인물 동일 톤).
        /// </summary>
        public static string[] GetLines(string npcId, string regionId)
        {
            int h = StableHash(npcId ?? string.Empty);

            string[] region;
            if (regionId == null || !RegionLines.TryGetValue(regionId, out region))
                region = DefaultRegionLines;

            return new[]
            {
                PersonalLines[Mod(h, PersonalLines.Length)],
                region[Mod(h >> 4, region.Length)],
                CommonHints[Mod(h >> 8, CommonHints.Length)],
            };
        }

        /// <summary>seed로 이름 풀에서 결정적 선택 — NpcManager가 DisplayName 부여에 사용.</summary>
        public static string GetVillagerName(int seed)
        {
            return Names[Mod(seed, Names.Length)];
        }

        /// <summary>
        /// FNV-1a 32비트 — string.GetHashCode는 런타임/버전별로 달라질 수 있어
        /// 결정적 시드(외형/이름/대사 고정)에 부적합. 자체 안정 해시 사용.
        /// </summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (s != null)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        hash ^= s[i];
                        hash *= 16777619u;
                    }
                }
                return (int)hash;
            }
        }

        private static int Mod(int value, int length)
        {
            if (length <= 0) return 0;
            int m = value % length;
            return m < 0 ? m + length : m;
        }
    }
}
