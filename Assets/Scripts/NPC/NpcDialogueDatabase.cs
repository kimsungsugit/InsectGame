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
