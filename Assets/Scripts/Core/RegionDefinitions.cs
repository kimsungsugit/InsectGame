using UnityEngine;
using InsectGame.Data;

namespace InsectGame.Core
{
    /// <summary>
    /// 전 지역(RegionData[]) 정의를 PlaySceneBootstrap 모놀리스에서 분리.
    /// 데이터 생성만 담당 — 부작용/의존성 없음. CreateAll()이 매 호출마다 새 인스턴스 반환.
    /// </summary>
    public static class RegionDefinitions
    {
        /// <summary>
        /// 월드 좌표 스케일 — CreateAll()이 전 리전/서브에리어의 center(XZ)·radius에 일괄 적용.
        /// 맵 크기 조정은 이 상수 하나만 변경 (아래 리터럴은 스케일 1.0 기준 원본 배치).
        /// center와 radius를 같은 배율로 키워야 리전 간 겹침/게이트웨이 상대 기하가 보존된다.
        /// </summary>
        public const float WorldScale = 1.5f;

        /// <summary>
        /// 2막(ver2) 리전의 최소 <c>requiredLevel</c>. 1막 마지막인 유적이 이 값 미만이고
        /// 2막 첫 리전 hollow가 정확히 이 값이다.
        ///
        /// "어느 리전이 2막인가"를 묻는 코드는 <b>리전 ID 목록을 박아 두지 말고</b> 이 임계로
        /// 판정한다. 이 저장소에서 하드코딩 리전 목록이 조용히 어긋난 적이 세 번 있었다
        /// (마스터 특권 / 스폰 레벨대 / 의상 해금 문구). 리전을 더 붙여도 여기는 안 바뀐다.
        /// </summary>
        public const int Act2MinRequiredLevel = 42;

        /// <summary>이 리전이 2막(장부에 없는 땅)인가 — 「지워진 개체」 출현 판정 등에 쓴다.</summary>
        public static bool IsAct2Region(RegionData region)
            => region != null && region.requiredLevel >= Act2MinRequiredLevel;

        public static RegionData[] CreateAll()
        {
            RegionData[] regions = new RegionData[]
            {
                // ── 초원: Lv.1~10, 입문 지역, Common/Uncommon 위주 ──
                new RegionData
                {
                    regionId = "meadow",
                    displayName = "초원",
                    description = "평화로운 초원 — 흔한 곤충이 많아 초보자에게 적합합니다.",
                    themeColor = new Color(0.4f, 0.8f, 0.3f),
                    centerPosition = Vector3.zero,
                    radius = 50f,
                    requiredLevel = 1,
                    insectIds = new[]
                    {
                        "beetle_basic", "bee_worker", "cricket_field", "ant_soldier",
                        "grasshopper_green", "ladybug_seven", "caterpillar_green", "aphid_colony",
                        "moth_brown", "beetle_dung",
                        // 확장 64종 — 초원 신규 4종 (C/U)
                        "bee_bumble", "grasshopper_brown", "ladybug_harlequin", "antlion_pit"
                    },
                    guardianInsectId = "mantis_green",
                    guardianDisplayName = "초원의 수호자 사마귀",
                    guardianLevel = 13,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "meadow_cave",
                            displayName = "초원 동굴",
                            description = "초원 아래 숨겨진 동굴 — 어둠 속 곤충이 서식합니다.",
                            centerPosition = new Vector3(15f, 0f, 25f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "centipede_common", "earwig_common", "pill_bug_garden" },
                            minLevel = 3,
                            maxLevel = 10,
                            environmentType = "cave"
                        },
                        new SubAreaData
                        {
                            subAreaId = "meadow_pond",
                            displayName = "숨겨진 웅덩이",
                            description = "초원 한쪽에 숨겨진 작은 웅덩이 — 물가 곤충이 출현합니다.",
                            centerPosition = new Vector3(-20f, 0f, 15f),
                            radius = 8f,
                            exclusiveInsectIds = new[] { "mosquito_common", "damselfly_blue" },
                            minLevel = 2,
                            maxLevel = 8,
                            environmentType = "pond"
                        }
                    }
                },
                // ── 연못: Lv.6~16, 수서곤충 + 희귀종 등장 ──
                new RegionData
                {
                    regionId = "pond",
                    displayName = "연못",
                    description = "물가에 사는 곤충들의 서식지 — 수서곤충과 희귀종이 출현합니다.",
                    themeColor = new Color(0.3f, 0.6f, 1f),
                    centerPosition = new Vector3(100f, 0f, 30f),
                    radius = 45f,
                    requiredLevel = 6,
                    insectIds = new[]
                    {
                        "dragonfly_lake", "water_strider_pond", "fly_house",
                        "dragonfly_emperor",
                        // 확장 64종 — 연못 신규 9종 (C4/U3/R1/E1) + 습지와 공유 1종(fly_crane)
                        "mosquito_tiger", "fly_hover", "water_strider_stream", "diving_beetle_striped",
                        "damselfly_red", "dragonfly_scarlet", "diving_beetle_great",
                        "dragonfly_jade", "firefly_marsh", "fly_crane"
                    },
                    guardianInsectId = "dragonfly_emperor",
                    guardianDisplayName = "연못의 파수꾼 왕잠자리",
                    guardianLevel = 20,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "pond_deep",
                            displayName = "연못 깊은 곳",
                            description = "연못 깊숙한 곳 — 수중 곤충만이 살아남을 수 있습니다.",
                            centerPosition = new Vector3(105f, 0f, 25f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "diving_beetle_deep", "diving_beetle_small", "diving_beetle_king" },
                            minLevel = 8,
                            maxLevel = 16,
                            environmentType = "underwater"
                        },
                        new SubAreaData
                        {
                            subAreaId = "pond_reeds",
                            displayName = "갈대 밀림",
                            description = "갈대가 빽빽이 우거진 곳 — 야행성 곤충이 숨어있습니다.",
                            centerPosition = new Vector3(90f, 0f, 40f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "firefly_blue", "cicada_evening" },
                            minLevel = 6,
                            maxLevel = 14,
                            environmentType = "reeds"
                        }
                    }
                },
                // ── 숲: Lv.12~24, 강력한 곤충 ──
                new RegionData
                {
                    regionId = "forest",
                    displayName = "숲",
                    description = "울창한 숲 속 강력한 곤충이 서식 — 높은 레벨의 도전이 필요합니다.",
                    themeColor = new Color(0.2f, 0.5f, 0.15f),
                    centerPosition = new Vector3(-80f, 0f, 80f),
                    radius = 55f,
                    requiredLevel = 12,
                    insectIds = new[]
                    {
                        "stag_beetle", "rhinoceros_beetle", "cicada_summer", "moth_night",
                        "mantis_green", "longhorn_beetle", "stick_insect_long", "beetle_longhorn_rosalia",
                        "hornet_asian",
                        // 확장 64종 — 숲 신규 5종 (U2/R2/E1)
                        "beetle_longhorn_oak", "cricket_tree", "stag_beetle_saw",
                        "mantis_bark", "rhinoceros_beetle_titan"
                    },
                    guardianInsectId = "beetle_hercules",
                    guardianDisplayName = "숲의 문지기 헤라클레스",
                    guardianLevel = 28,
                    // 오염 거점 — 세 곳 중 **가장 이른 곳**이다(입장 Lv.12).
                    // 산·유적의 하수(32/34)를 여기 세울 수 없어 하수를 새로 둔다: 갓 들어온
                    // 말단이라 레벨이 낮고, 그래서 "나는 시키는 대로 했을 뿐"이 성립한다.
                    // 돌아오는 종은 로살리아하늘소 — 도감에 이름은 있어도 실물을 본 사람이
                    // 거의 없는 종이라, 정화 직후 눈앞에 나타나는 것 자체가 사건이 된다.
                    blightBossNpcId = "ledger_thug_pin",
                    blightSiteName = "명부회 그물터",
                    blightReturningInsectId = "beetle_longhorn_rosalia",
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "forest_deep",
                            displayName = "깊은 숲",
                            description = "빛이 닿지 않는 깊은 숲 — 유령 곤충과 거대 나방이 출현합니다.",
                            centerPosition = new Vector3(-90f, 0f, 95f),
                            radius = 14f,
                            exclusiveInsectIds = new[] { "mantis_ghost", "leaf_insect_phantom", "atlas_moth_giant", "mantis_dead_leaf" },
                            minLevel = 18,
                            maxLevel = 28,
                            environmentType = "deep_forest"
                        },
                        new SubAreaData
                        {
                            subAreaId = "forest_cave",
                            displayName = "숲속 동굴",
                            description = "숲 깊은 곳의 동굴 — 보스급 곤충이 서식합니다.",
                            centerPosition = new Vector3(-70f, 0f, 70f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "beetle_hercules", "scarab_ancient" },
                            minLevel = 15,
                            maxLevel = 24,
                            environmentType = "cave"
                        }
                    }
                },
                // ── 습지: Lv.20~32, 독/어둠 곤충 (신규) ──
                new RegionData
                {
                    regionId = "swamp",
                    displayName = "습지",
                    description = "안개가 자욱한 습지 — 독을 가진 곤충과 어둠의 포식자가 서식합니다.",
                    themeColor = new Color(0.25f, 0.35f, 0.2f),
                    centerPosition = new Vector3(-30f, 0f, -60f),
                    radius = 45f,
                    requiredLevel = 20,
                    insectIds = new[]
                    {
                        "centipede_common", "earwig_common", "mosquito_common", "pill_bug_garden",
                        "damselfly_blue", "firefly_blue", "cicada_evening",
                        // 확장 64종 — 습지 신규 12종 (C5/U3/R2/E2) + 연못과 공유 1종(mosquito_tiger)
                        "mosquito_swamp", "centipede_red", "earwig_swamp", "pill_bug_mud",
                        "fly_crane", "dragonfly_swamp_hawker", "firefly_swamp", "spider_marsh",
                        "mantis_swamp", "centipede_venom", "wasp_night", "spider_bog_widow",
                        "mosquito_tiger"
                    },
                    guardianInsectId = "mantis_ghost",
                    guardianDisplayName = "습지의 유령 사마귀",
                    guardianLevel = 37,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "swamp_fog",
                            displayName = "안개 습지",
                            description = "짙은 안개가 시야를 가리는 곳 — 유령 곤충이 출몰합니다.",
                            centerPosition = new Vector3(-25f, 0f, -70f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "mantis_ghost", "leaf_insect_phantom", "mantis_mist" },
                            minLevel = 24,
                            maxLevel = 32,
                            environmentType = "fog"
                        },
                        new SubAreaData
                        {
                            subAreaId = "swamp_cave",
                            displayName = "습지 동굴",
                            description = "습지 깊숙한 동굴 — 고대 곤충이 잠들어 있습니다.",
                            centerPosition = new Vector3(-40f, 0f, -55f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "scarab_ancient", "beetle_hercules" },
                            minLevel = 22,
                            maxLevel = 30,
                            environmentType = "cave"
                        }
                    }
                },
                // ── 산: Lv.28~40, 고산 곤충 (신규) ──
                new RegionData
                {
                    regionId = "mountain",
                    displayName = "산",
                    description = "험준한 산악 지형 — 강인한 고산 곤충만이 살아남는 극한 환경입니다.",
                    themeColor = new Color(0.5f, 0.45f, 0.4f),
                    centerPosition = new Vector3(-120f, 0f, -30f),
                    radius = 50f,
                    requiredLevel = 28,
                    insectIds = new[]
                    {
                        "hornet_asian", "beetle_longhorn_rosalia", "stick_insect_long",
                        "katydid_leaf", "beetle_click", "spider_garden",
                        // 확장 64종 — 산 신규 12종 (C4/U3/R3/E1/L1)
                        "grasshopper_rock", "cricket_stone", "pill_bug_rock", "ladybug_alpine",
                        "beetle_longhorn_alpine", "cicada_mountain", "caterpillar_pine",
                        "stag_beetle_mountain", "butterfly_apollo", "spider_cliff",
                        "stag_beetle_iron", "cicada_ancient"
                    },
                    guardianInsectId = "atlas_moth_giant",
                    guardianDisplayName = "산의 거신 아틀라스나방",
                    guardianLevel = 45,
                    // 명부회 채집장 — 능선을 그물로 통째로 훑은 자리. 검은 옷의 여자가 지킨다.
                    // 무쇠사슴벌레를 귀환종으로 둔 것은 설계다: 사슴벌레는 이 세계에서 가장
                    // 많이 잡혀 나간 종이고, 돌아오는 것이 눈에 띄어야 정화가 체감된다.
                    blightBossNpcId = "ledger_thug_rule",
                    blightSiteName = "명부회 채집장",
                    blightReturningInsectId = "stag_beetle_iron",
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "mountain_peak",
                            displayName = "산 정상",
                            description = "바람이 휘몰아치는 정상 — 전설급 곤충이 날아다닙니다.",
                            centerPosition = new Vector3(-130f, 0f, -20f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "dragonfly_ancient", "butterfly_morpho", "moth_comet" },
                            minLevel = 34,
                            maxLevel = 42,
                            environmentType = "peak"
                        },
                        new SubAreaData
                        {
                            subAreaId = "mountain_cave",
                            displayName = "산속 동굴",
                            description = "산 깊은 곳의 동굴 — 거대 거미와 나방이 서식합니다.",
                            centerPosition = new Vector3(-110f, 0f, -40f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "spider_golden_orb", "atlas_moth_giant" },
                            minLevel = 30,
                            maxLevel = 38,
                            environmentType = "cave"
                        }
                    }
                },
                // ── 꽃밭: Lv.18~35, 희귀 나비 (분기 경로) ──
                new RegionData
                {
                    regionId = "garden",
                    displayName = "꽃밭",
                    description = "희귀한 나비와 전설의 곤충이 숨어있는 비밀의 정원입니다.",
                    themeColor = new Color(1f, 0.5f, 0.7f),
                    centerPosition = new Vector3(60f, 0f, -90f),
                    radius = 40f,
                    requiredLevel = 18,
                    insectIds = new[]
                    {
                        "butterfly_azure", "butterfly_monarch", "butterfly_swallowtail",
                        "luna_moth_silver", "jewel_beetle_gold", "firefly_glow",
                        "wasp_paper", "butterfly_cabbage",
                        // 확장 64종 — 꽃밭 신규 6종 (C1/U2/R2/E1)
                        "aphid_rose", "bee_carpenter", "butterfly_peacock",
                        "butterfly_glasswing", "moth_hummingbird", "bee_queen"
                    },
                    guardianInsectId = "butterfly_swallowtail",
                    guardianDisplayName = "꽃밭의 문지기 호랑나비",
                    // 입장요구(18)·필드 레벨 대비 게이트가 되도록 상향. 옛 13은 입장레벨보다 낮아 역전(무의미한 게이트).
                    // 이웃 진행(forest req12/guard28, swamp req20/guard37) 사이에 맞춰 33.
                    guardianLevel = 33,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "garden_maze",
                            displayName = "꽃 미로",
                            description = "거대한 꽃으로 이루어진 미로 — 전설급 곤충이 숨어있습니다.",
                            centerPosition = new Vector3(55f, 0f, -100f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "butterfly_alexandras", "mantis_orchid" },
                            minLevel = 30,
                            maxLevel = 40,
                            environmentType = "flower_maze"
                        },
                        new SubAreaData
                        {
                            subAreaId = "garden_greenhouse",
                            displayName = "온실",
                            description = "유리로 된 온실 — 최상위 곤충이 서식합니다.",
                            centerPosition = new Vector3(70f, 0f, -80f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "spider_golden_orb", "beetle_golden_stag" },
                            minLevel = 25,
                            maxLevel = 35,
                            environmentType = "greenhouse"
                        }
                    }
                },
                // ── 고대 유적: Lv.36~50, 전설급 (신규) ──
                new RegionData
                {
                    regionId = "ruins",
                    displayName = "고대 유적",
                    description = "잊혀진 문명의 유적 — 전설급 곤충만이 서식하는 최종 지역입니다.",
                    themeColor = new Color(0.4f, 0.35f, 0.3f),
                    centerPosition = new Vector3(0f, 0f, 140f),
                    radius = 45f,
                    requiredLevel = 36,
                    insectIds = new[]
                    {
                        "mantis_orchid", "butterfly_alexandras", "beetle_golden_stag",
                        "dragonfly_ancient", "butterfly_morpho",
                        // 확장 64종 — 고대 유적 신규 11종 (U1/R4/E3/L3)
                        "cricket_tomb", "scarab_relic", "mantis_obsidian", "spider_tomb",
                        "centipede_ruin", "jewel_beetle_azure", "moth_shadow", "wasp_gold",
                        "scarab_pharaoh", "butterfly_midnight", "hornet_emperor"
                    },
                    // 1막에는 수문장이 없어 유적이 종착지였다. 2막을 열려면 여기 고리가 필요하다 —
                    // RegionManager.DefeatGuardian이 유일한 리전 해금 경로이기 때문이다.
                    // 이 격파가 '봉인이 열린 날'이자 텅 빈 들(hollow)로 가는 문이다.
                    guardianInsectId = "scarab_pharaoh",
                    guardianDisplayName = "유적의 파수꾼 파라오풍뎅이",
                    guardianLevel = 42,
                    // 명부회 창고 — 잡아둔 것을 상자에 재는 곳. 검은 옷의 사내가 지킨다.
                    // 산이 '잡아 가는 현장'이라면 여기는 '쌓아 두는 현장'이다.
                    blightBossNpcId = "ledger_thug_cord",
                    blightSiteName = "명부회 창고",
                    blightReturningInsectId = "jewel_beetle_azure",
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "ruins_temple",
                            displayName = "고대 신전",
                            description = "유적 깊숙한 신전 — 필드 전설 곤충만이 출현합니다.",
                            centerPosition = new Vector3(5f, 0f, 150f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "mantis_orchid", "butterfly_alexandras", "mantis_gold_temple" },
                            minLevel = 40,
                            maxLevel = 50,
                            environmentType = "temple"
                        },
                        new SubAreaData
                        {
                            subAreaId = "ruins_underground",
                            displayName = "유적 지하",
                            description = "유적 아래 봉인된 지하 — 고대 곤충이 잠들어 있습니다.",
                            centerPosition = new Vector3(-10f, 0f, 135f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "beetle_hercules", "leaf_insect_phantom" },
                            minLevel = 38,
                            maxLevel = 48,
                            environmentType = "underground"
                        }
                    }
                },

                // ═══════ 2막(ver2) — "장부에 없는 땅" ═══════
                // 1막 7지역은 고대인의 기록에 있던 땅이고, 아래 6지역은 기록에서 지워졌거나
                // 애초에 오르지 못한 땅이다. 서사는 Docs/StoryBible.md가 단일 출처.
                // 배치는 유적(0,140) 너머 북동~북서로 호를 그린다 — 인접 리전끼리 반지름 합보다
                // 멀게 두어 겹침이 없다(WorldScale이 전부에 일괄 적용되므로 상대 기하는 보존된다).

                // ── 텅 빈 들: Lv.42~48, 잦아듦이 가장 먼저 훑고 간 폐허 초원 ──
                new RegionData
                {
                    regionId = "hollow",
                    displayName = "텅 빈 들",
                    description = "소리가 없는 들판 — 잦아듦이 가장 먼저 훑고 간 자리입니다.",
                    themeColor = new Color(0.55f, 0.55f, 0.5f),
                    centerPosition = new Vector3(100f, 0f, 165f),
                    radius = 45f,
                    // 아래 풀은 신규 6종 + 초원·습지 종 재활용 8종이다. 재활용이 의도다 —
                    // 신규 종으로 채우면 '텅 빈' 들이 오히려 풍요로워 보인다.
                    // (주석을 requiredLevel과 insectIds **사이**에 두지 말 것 —
                    //  game_facts.region_pools()의 정규식이 `requiredLevel = N,\s*insectIds`를
                    //  붙어 있는 것으로 보고 읽어서, 사이에 주석이 끼면 그 리전을 통째로 건너뛰고
                    //  다음 리전 값을 잘못 가져온다. 실제로 hollow가 dunes 풀로 읽혔다.)
                    requiredLevel = 42,
                    insectIds = new[]
                    {
                        "cricket_hush", "moth_ashen", "beetle_husk", "spider_threadbare",
                        "mantis_hollow", "moth_forgotten",
                        "moth_brown", "aphid_colony", "beetle_dung", "earwig_common",
                        "pill_bug_garden", "stick_insect_long", "mantis_dead_leaf", "moth_shadow"
                    },
                    guardianInsectId = "mantis_hollow",
                    guardianDisplayName = "텅 빈 들의 사마귀",
                    guardianLevel = 48,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "hollow_silence",
                            displayName = "침묵의 자리",
                            description = "소리가 완전히 멎은 곳 — 이름을 잃은 것들이 머뭅니다.",
                            centerPosition = new Vector3(92f, 0f, 178f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "mantis_hollow", "moth_forgotten" },
                            minLevel = 44,
                            maxLevel = 50,
                            environmentType = "fog"
                        },
                        new SubAreaData
                        {
                            subAreaId = "hollow_burrow",
                            displayName = "마른 굴",
                            description = "말라붙은 땅굴 — 껍질만 남은 곤충이 숨어 있습니다.",
                            centerPosition = new Vector3(112f, 0f, 154f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "spider_threadbare", "beetle_husk" },
                            minLevel = 42,
                            maxLevel = 48,
                            environmentType = "underground"
                        }
                    }
                },
                // ── 모래언덕: Lv.46~52, 명부회 전진기지 ──
                new RegionData
                {
                    regionId = "dunes",
                    displayName = "모래언덕",
                    description = "기록이 모래에 묻힌 땅 — 누군가 곤충을 상자째 실어 나른 자국이 있습니다.",
                    themeColor = new Color(0.88f, 0.76f, 0.45f),
                    centerPosition = new Vector3(190f, 0f, 105f),
                    radius = 48f,
                    requiredLevel = 46,
                    insectIds = new[]
                    {
                        "beetle_sand", "cricket_dune", "fly_sand", "pill_bug_desert",
                        "bee_digger", "antlion_dune", "grasshopper_locust",
                        "spider_camel", "scarab_sand", "wasp_hawk",
                        "centipede_sand", "hornet_dune",
                        "grasshopper_rock", "pill_bug_rock"
                    },
                    guardianInsectId = "hornet_dune",
                    guardianDisplayName = "모래언덕의 장수말벌",
                    guardianLevel = 52,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "dunes_vault",
                            displayName = "모래 아래 창고",
                            description = "모래에 반쯤 묻힌 저장고 — 상자마다 이름표가 붙어 있습니다.",
                            centerPosition = new Vector3(178f, 0f, 118f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "hornet_dune", "centipede_sand" },
                            minLevel = 48,
                            maxLevel = 54,
                            environmentType = "vault"
                        },
                        new SubAreaData
                        {
                            subAreaId = "dunes_pit",
                            displayName = "개미귀신 구덩이",
                            description = "깔때기 모양 함정이 늘어선 사면 — 발을 헛디디면 빠져나오기 어렵습니다.",
                            centerPosition = new Vector3(205f, 0f, 92f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "antlion_dune", "spider_camel", "scarab_sand" },
                            minLevel = 46,
                            maxLevel = 52,
                            environmentType = "cave"
                        }
                    }
                },
                // ── 서릿길: Lv.50~56, 얼어붙어 시간이 멈춘 땅 ──
                new RegionData
                {
                    regionId = "frostline",
                    displayName = "서릿길",
                    description = "얼음이 시간을 붙들어 둔 땅 — 여기 기록만은 한 번도 바래지 않았습니다.",
                    themeColor = new Color(0.72f, 0.86f, 0.95f),
                    centerPosition = new Vector3(215f, 0f, 205f),
                    radius = 45f,
                    requiredLevel = 50,
                    insectIds = new[]
                    {
                        "pill_bug_frost", "cricket_frost", "moth_snow", "beetle_rime",
                        "spider_frost", "stag_beetle_glacier", "butterfly_snowveil",
                        "mantis_icicle", "moth_aurora",
                        "beetle_hoarfrost", "katydid_snowfield", "bee_glacier",
                        "centipede_frost", "butterfly_apollo"
                    },
                    guardianInsectId = "moth_aurora",
                    guardianDisplayName = "서릿길의 오로라나방",
                    guardianLevel = 56,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "frostline_archive",
                            displayName = "얼음 서고",
                            description = "얼음 벽에 옛 기록이 그대로 갇혀 있는 곳 — 글자가 아직 선명합니다.",
                            centerPosition = new Vector3(205f, 0f, 215f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "moth_aurora", "butterfly_snowveil" },
                            minLevel = 52,
                            maxLevel = 58,
                            environmentType = "archive"
                        },
                        new SubAreaData
                        {
                            subAreaId = "frostline_ridge",
                            displayName = "서리 능선",
                            description = "바람이 얼음 알갱이를 실어 나르는 능선 — 시야가 자주 하얗게 지워집니다.",
                            centerPosition = new Vector3(228f, 0f, 193f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "mantis_icicle", "stag_beetle_glacier" },
                            minLevel = 50,
                            maxLevel = 56,
                            environmentType = "peak"
                        }
                    }
                },
                // ── 잿불 골짜기: Lv.54~60, 기록이 불타 없어진 땅 ──
                new RegionData
                {
                    regionId = "emberfall",
                    displayName = "잿불 골짜기",
                    description = "재가 식지 않는 골짜기 — 기록이 통째로 불타 빈칸이 가장 두껍게 겹친 곳입니다.",
                    themeColor = new Color(0.62f, 0.28f, 0.22f),
                    // (120,255)에서 (128,262)로 옮겼다 — 옛 위치는 사슬상 이웃도 아닌 hollow와
                    // 0.8m 겹쳤다(거리 92.2 < 반경합 93). 겹치면 RegionManager.ContainsPoint가
                    // 먼저 걸린 리전을 돌려줘 그 띠에서 BGM·스폰 풀이 튄다.
                    // RegionProgressionTests.SecondActRegions_DoNotOverlapAnyRegion이 고정한다.
                    centerPosition = new Vector3(128f, 0f, 262f),
                    radius = 48f,
                    requiredLevel = 54,
                    insectIds = new[]
                    {
                        "beetle_cinder", "cricket_ember", "fly_ash", "centipede_ember",
                        "wasp_ash", "cicada_ember", "beetle_longhorn_char",
                        "mantis_ember", "hornet_magma",
                        "pill_bug_cinder", "cricket_slag", "moth_shadow",
                        "beetle_scorch", "moth_smoulder"
                    },
                    guardianInsectId = "hornet_magma",
                    guardianDisplayName = "잿불 골짜기의 용암말벌",
                    guardianLevel = 60,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "emberfall_kiln",
                            displayName = "무너진 가마",
                            description = "한때 무언가를 구워내던 가마 — 지금은 갱도가 조금씩 내려앉고 있습니다.",
                            centerPosition = new Vector3(110f, 0f, 266f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "hornet_magma", "mantis_ember" },
                            minLevel = 56,
                            maxLevel = 62,
                            environmentType = "kiln"
                        },
                        new SubAreaData
                        {
                            subAreaId = "emberfall_vent",
                            displayName = "잿불 굴뚝",
                            description = "땅속에서 열기가 올라오는 갈라진 틈 — 재가 아래에서 위로 흐릅니다.",
                            centerPosition = new Vector3(132f, 0f, 244f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "centipede_ember", "cicada_ember" },
                            minLevel = 54,
                            maxLevel = 60,
                            environmentType = "underground"
                        }
                    }
                },
                // ── 우듬지: Lv.58~64, 꽃밭과 같은 예비 울타리 (2막에서 유일하게 풍성한 땅) ──
                new RegionData
                {
                    regionId = "canopy",
                    displayName = "우듬지",
                    description = "거대수 수관층 — 여기 곤충들은 한 번도 이름을 잃은 적이 없습니다.",
                    themeColor = new Color(0.30f, 0.68f, 0.38f),
                    centerPosition = new Vector3(15f, 0f, 265f),
                    radius = 50f,
                    requiredLevel = 58,
                    insectIds = new[]
                    {
                        "aphid_canopy", "caterpillar_silk", "ladybug_canopy", "bee_stingless",
                        "katydid_canopy", "stick_insect_canopy", "butterfly_crown",
                        "mantis_canopy", "butterfly_worldtree",
                        "beetle_bark_canopy", "cicada_crown", "moth_leafveil",
                        "beetle_longhorn_rosalia", "bee_perfume"
                    },
                    guardianInsectId = "butterfly_worldtree",
                    guardianDisplayName = "우듬지의 세계수나비",
                    guardianLevel = 64,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "canopy_crown",
                            displayName = "가장 높은 가지",
                            description = "거대수 꼭대기 — 여기서는 아래 세계가 전부 내려다보입니다.",
                            centerPosition = new Vector3(5f, 0f, 277f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "butterfly_worldtree", "mantis_canopy" },
                            minLevel = 60,
                            maxLevel = 66,
                            environmentType = "peak"
                        },
                        new SubAreaData
                        {
                            subAreaId = "canopy_bough",
                            displayName = "겹친 가지 속",
                            description = "잎이 몇 겹으로 겹쳐 빛이 잘게 쪼개지는 곳입니다.",
                            centerPosition = new Vector3(30f, 0f, 252f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "stick_insect_canopy", "katydid_canopy", "butterfly_crown" },
                            minLevel = 58,
                            maxLevel = 64,
                            environmentType = "deep_forest"
                        }
                    }
                },
                // ── 이름 없는 자리: Lv.62~70, 최종 지역 ──
                // GetNextRegionId에 case를 넣지 않는다(여기가 끝). ver3를 붙일 때 여기 case가 생긴다.
                new RegionData
                {
                    regionId = "nameless",
                    displayName = "이름 없는 자리",
                    description = "어느 지도에도 적히지 않은 땅 — 이름을 빼앗긴 것들이 모입니다.",
                    themeColor = new Color(0.34f, 0.32f, 0.40f),
                    centerPosition = new Vector3(-80f, 0f, 225f),
                    radius = 42f,
                    requiredLevel = 62,
                    insectIds = new[]
                    {
                        "moth_pale", "cricket_still", "spider_blank", "beetle_unwritten",
                        "centipede_pale", "mantis_blank", "butterfly_erased",
                        "moth_effaced", "mantis_unnamed",
                        "mantis_hollow", "moth_forgotten", "spider_threadbare",
                        "moth_shadow", "butterfly_midnight"
                    },
                    guardianInsectId = "mantis_unnamed",
                    guardianDisplayName = "이름 없는 사마귀",
                    guardianLevel = 70,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "nameless_ledger",
                            displayName = "장부의 방",
                            description = "명부회가 옮겨 온 장부가 벽을 메운 곳 — 이름이 빼곡한데 아무 소리도 없습니다.",
                            centerPosition = new Vector3(-90f, 0f, 235f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "moth_effaced", "butterfly_erased" },
                            minLevel = 64,
                            maxLevel = 70,
                            environmentType = "ledger"
                        },
                        new SubAreaData
                        {
                            subAreaId = "nameless_core",
                            displayName = "빈칸",
                            description = "아무것도 새겨지지 않은 자리 — 그것이 서려던 곳입니다.",
                            centerPosition = new Vector3(-68f, 0f, 214f),
                            radius = 10f,
                            exclusiveInsectIds = new[] { "mantis_unnamed", "mantis_blank" },
                            minLevel = 66,
                            maxLevel = 72,
                            environmentType = "underground"
                        }
                    }
                }
            };

            ApplyWorldScale(regions);
            return regions;
        }

        private static void ApplyWorldScale(RegionData[] regions)
        {
            if (Mathf.Approximately(WorldScale, 1f)) return;

            foreach (var region in regions)
            {
                region.centerPosition = ScaleXZ(region.centerPosition);
                region.radius *= WorldScale;
                if (region.subAreas == null) continue;

                foreach (var sub in region.subAreas)
                {
                    sub.centerPosition = ScaleXZ(sub.centerPosition);
                    sub.radius *= WorldScale;
                }
            }
        }

        private static Vector3 ScaleXZ(Vector3 p)
        {
            return new Vector3(p.x * WorldScale, p.y, p.z * WorldScale);
        }
    }
}
