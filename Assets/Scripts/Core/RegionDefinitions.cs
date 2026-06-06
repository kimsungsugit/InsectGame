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
        public static RegionData[] CreateAll()
        {
            return new RegionData[]
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
                        "moth_brown", "beetle_dung"
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
                        "dragonfly_emperor"
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
                            exclusiveInsectIds = new[] { "diving_beetle_deep", "diving_beetle_small" },
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
                        "hornet_asian"
                    },
                    guardianInsectId = "beetle_hercules",
                    guardianDisplayName = "숲의 문지기 헤라클레스",
                    guardianLevel = 28,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "forest_deep",
                            displayName = "깊은 숲",
                            description = "빛이 닿지 않는 깊은 숲 — 유령 곤충과 거대 나방이 출현합니다.",
                            centerPosition = new Vector3(-90f, 0f, 95f),
                            radius = 14f,
                            exclusiveInsectIds = new[] { "mantis_ghost", "leaf_insect_phantom", "atlas_moth_giant" },
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
                        "damselfly_blue", "firefly_blue", "cicada_evening"
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
                            exclusiveInsectIds = new[] { "mantis_ghost", "leaf_insect_phantom" },
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
                        "katydid_leaf", "beetle_click", "spider_garden"
                    },
                    guardianInsectId = "atlas_moth_giant",
                    guardianDisplayName = "산의 거신 아틀라스나방",
                    guardianLevel = 45,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "mountain_peak",
                            displayName = "산 정상",
                            description = "바람이 휘몰아치는 정상 — 전설급 곤충이 날아다닙니다.",
                            centerPosition = new Vector3(-130f, 0f, -20f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "dragonfly_ancient", "butterfly_morpho" },
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
                        "wasp_paper", "butterfly_cabbage"
                    },
                    guardianInsectId = "butterfly_swallowtail",
                    guardianDisplayName = "꽃밭의 문지기 호랑나비",
                    guardianLevel = 13,
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
                        "dragonfly_ancient", "butterfly_morpho"
                    },
                    guardianInsectId = null,
                    guardianDisplayName = null,
                    guardianLevel = 0,
                    subAreas = new SubAreaData[]
                    {
                        new SubAreaData
                        {
                            subAreaId = "ruins_temple",
                            displayName = "고대 신전",
                            description = "유적 깊숙한 신전 — 필드 전설 곤충만이 출현합니다.",
                            centerPosition = new Vector3(5f, 0f, 150f),
                            radius = 12f,
                            exclusiveInsectIds = new[] { "mantis_orchid", "butterfly_alexandras" },
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
                }
            };
        }
    }
}
