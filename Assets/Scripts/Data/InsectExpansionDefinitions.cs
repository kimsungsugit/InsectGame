namespace InsectGame.Data
{
    /// <summary>
    /// 곤충 확장 시드 — CreateStableInsect(id, name, rarity, weight, difficulty, desc, habitat) 인자와 1:1 대응.
    /// 순수 데이터 구조체(부작용 없음). 스탯/스킬은 부트스트랩의 CreateStableInsect가 자동 파생.
    /// </summary>
    [System.Serializable]
    public struct InsectSeed
    {
        public string id;
        public string name;
        public InsectRarity rarity;
        public float weight;
        public float difficulty;
        public string desc;
        public string habitat;

        public InsectSeed(string id, string name, InsectRarity rarity, float weight, float difficulty, string desc, string habitat)
        {
            this.id = id;
            this.name = name;
            this.rarity = rarity;
            this.weight = weight;
            this.difficulty = difficulty;
            this.desc = desc;
            this.habitat = habitat;
        }
    }

    /// <summary>
    /// 곤충 가짓수 2배 확장(64→128종) — 신규 64종 시드 정의.
    /// 부트스트랩 EnsureExpandedDatabase가 CreateAll()을 소비해 InsectData로 변환한다.
    ///
    /// ID 명명 규칙 (InsectEntity.BuildModel 분기 순서 기준 — 오매칭 방지):
    /// - 벌은 bee_ 접두(bee⊂beetle 가드), 개미류는 antlion만 사용(ant⊂mantis·phantom·giant 충돌 회피)
    /// - 파리는 fly_ 접두 또는 mosquito(fly⊂dragonfly·butterfly·firefly·damselfly)
    /// - ghost/orchid(사마귀 전용), luna/atlas(나방 별칭), giant/elephant 등 'ant' 포함 수식어 사용 금지
    /// - gold/jewel/diamond/celestial→Metal, night/shadow→Dark, glow/firefly→Light 타입 수식어는 의도한 속성일 때만
    ///
    /// 레어도 밴드(기존 64종 준수): weight C 0.90~1.30 / U 0.45~0.72 / R 0.18~0.30 / E 0.08~0.14 / L 0.03~0.05,
    /// difficulty C 0.18~0.25 / U 0.32~0.39 / R 0.44~0.56 / E 0.58~0.65 / L 0.78~0.85.
    /// 분포: Common 16 / Uncommon 16 / Rare 15 / Epic 11 / Legendary 6 (에픽+ 17종 → 전용기 필요).
    /// </summary>
    public static class InsectExpansionDefinitions
    {
        /// <summary>신규 64종 시드 전체를 생성한다. 매 호출마다 새 배열 반환(공유 상태 없음).</summary>
        public static InsectSeed[] CreateAll()
        {
            return new InsectSeed[]
            {
                // ── Meadow (4) — 입문 리전: Common/Uncommon 위주 ──
                new InsectSeed("bee_bumble", "Bumblebee", InsectRarity.Common, 1.15f, 0.22f, "A round, fuzzy bee that hums between clover blooms.", "Meadow"),
                new InsectSeed("grasshopper_brown", "Brown Grasshopper", InsectRarity.Common, 1.10f, 0.21f, "Blends into dry stalks until it springs away.", "Meadow"),
                new InsectSeed("ladybug_harlequin", "Harlequin Ladybug", InsectRarity.Uncommon, 0.62f, 0.34f, "Wears a different spot pattern on every shell.", "Meadow"),
                new InsectSeed("antlion_pit", "Pit Antlion", InsectRarity.Uncommon, 0.58f, 0.36f, "Digs funnel traps in soft sand and waits below.", "Meadow"),

                // ── Pond (9) — 수서 곤충: Common~Rare + Epic 1 ──
                new InsectSeed("mosquito_tiger", "Tiger Mosquito", InsectRarity.Common, 1.18f, 0.19f, "Striped and quick, it darts along the waterline.", "Pond"),
                new InsectSeed("fly_hover", "Hover Fly", InsectRarity.Common, 1.12f, 0.19f, "Hangs motionless in the air, then flicks away.", "Pond"),
                new InsectSeed("water_strider_stream", "Stream Water Strider", InsectRarity.Common, 1.05f, 0.21f, "Rides gentle currents without sinking an inch.", "Pond"),
                new InsectSeed("diving_beetle_striped", "Striped Diving Beetle", InsectRarity.Common, 0.95f, 0.23f, "Stripes ripple as it paddles through shallow water.", "Pond"),
                new InsectSeed("damselfly_red", "Red Damselfly", InsectRarity.Uncommon, 0.60f, 0.33f, "A crimson thread drifting between the reeds.", "Pond"),
                new InsectSeed("dragonfly_scarlet", "Scarlet Dragonfly", InsectRarity.Uncommon, 0.64f, 0.34f, "Burns bright red as it patrols the sunny shore.", "Pond"),
                new InsectSeed("diving_beetle_great", "Great Diving Beetle", InsectRarity.Uncommon, 0.55f, 0.37f, "A heavyweight swimmer that hunts in open water.", "Pond"),
                new InsectSeed("dragonfly_jade", "Jade Dragonfly", InsectRarity.Rare, 0.26f, 0.47f, "Its jade-green body gleams over still water.", "Pond"),
                new InsectSeed("firefly_marsh", "Marsh Firefly", InsectRarity.Epic, 0.11f, 0.61f, "Scatters pale lights across the misty shallows.", "Pond"),

                // ── Forest (5) — 중레벨: Uncommon~Epic ──
                new InsectSeed("beetle_longhorn_oak", "Oak Longhorn", InsectRarity.Uncommon, 0.62f, 0.35f, "Taps old oak bark with antennae longer than itself.", "Forest"),
                new InsectSeed("cricket_tree", "Tree Cricket", InsectRarity.Uncommon, 0.58f, 0.36f, "Sings a clear trill from high branches at night.", "Forest"),
                new InsectSeed("stag_beetle_saw", "Saw Stag Beetle", InsectRarity.Rare, 0.28f, 0.46f, "Its jagged jaws saw through rivals with ease.", "Forest"),
                new InsectSeed("mantis_bark", "Bark Mantis", InsectRarity.Rare, 0.24f, 0.50f, "Flattens against tree trunks, invisible until it strikes.", "Forest"),
                new InsectSeed("rhinoceros_beetle_titan", "Titan Rhinoceros Beetle", InsectRarity.Epic, 0.10f, 0.62f, "A massive horned bruiser that topples branches.", "Forest"),

                // ── Garden (6) — 나비/벌 중심: Common~Epic ──
                new InsectSeed("aphid_rose", "Rose Aphid", InsectRarity.Common, 1.22f, 0.18f, "Clings to rose buds and sips the sweet sap.", "Garden"),
                new InsectSeed("bee_carpenter", "Carpenter Bee", InsectRarity.Uncommon, 0.55f, 0.34f, "Bores neat round tunnels into old garden posts.", "Garden"),
                new InsectSeed("butterfly_peacock", "Peacock Butterfly", InsectRarity.Uncommon, 0.52f, 0.35f, "Flashes eye spots that startle curious birds.", "Garden"),
                new InsectSeed("butterfly_glasswing", "Glasswing Butterfly", InsectRarity.Rare, 0.22f, 0.52f, "Its transparent wings show the flowers behind them.", "Garden"),
                new InsectSeed("moth_hummingbird", "Hummingbird Hawk Moth", InsectRarity.Rare, 0.25f, 0.48f, "Hovers at blossoms and drinks like a tiny bird.", "Garden"),
                new InsectSeed("bee_queen", "Queen Bee", InsectRarity.Epic, 0.10f, 0.62f, "The regal heart of the hive, guarded by loyal workers.", "Garden"),

                // ── Swamp (12) — 신규 리전 Lv.20~32: 독/어둠 테마 ──
                new InsectSeed("mosquito_swamp", "Swamp Mosquito", InsectRarity.Common, 1.20f, 0.20f, "Swarms rise from the bog at every dusk.", "Swamp"),
                new InsectSeed("centipede_red", "Red Centipede", InsectRarity.Common, 0.98f, 0.24f, "A scarlet hunter weaving through rotten logs.", "Swamp"),
                new InsectSeed("earwig_swamp", "Swamp Earwig", InsectRarity.Common, 1.05f, 0.22f, "Hides its pincers under waterlogged bark.", "Swamp"),
                new InsectSeed("pill_bug_mud", "Mud Pill Bug", InsectRarity.Common, 1.10f, 0.20f, "Rolls into a muddy ball when the ground shakes.", "Swamp"),
                new InsectSeed("fly_crane", "Crane Fly", InsectRarity.Common, 1.14f, 0.19f, "Wobbles on stilt legs above the wet moss.", "Swamp"),
                new InsectSeed("dragonfly_swamp_hawker", "Swamp Hawker Dragonfly", InsectRarity.Uncommon, 0.60f, 0.35f, "Patrols the fog line for careless midges.", "Swamp"),
                new InsectSeed("firefly_swamp", "Swamp Firefly", InsectRarity.Uncommon, 0.48f, 0.38f, "Its green glow lures travelers deeper into the marsh.", "Swamp"),
                new InsectSeed("spider_marsh", "Marsh Spider", InsectRarity.Uncommon, 0.56f, 0.36f, "Strings low webs between the reeds at night.", "Swamp"),
                new InsectSeed("mantis_swamp", "Swamp Mantis", InsectRarity.Rare, 0.25f, 0.49f, "Stalks prey knee-deep in the stagnant water.", "Swamp"),
                new InsectSeed("centipede_venom", "Venom Centipede", InsectRarity.Rare, 0.22f, 0.52f, "One bite from its fangs numbs prey instantly.", "Swamp"),
                new InsectSeed("wasp_night", "Night Wasp", InsectRarity.Epic, 0.11f, 0.60f, "A silent black wasp that hunts after dark.", "Swamp"),
                new InsectSeed("spider_bog_widow", "Bog Widow Spider", InsectRarity.Epic, 0.10f, 0.62f, "Lurks under black water with venom to spare.", "Swamp"),

                // ── Mountain (12) — 신규 리전 Lv.28~40: 대지/강철 테마 ──
                new InsectSeed("grasshopper_rock", "Rock Grasshopper", InsectRarity.Common, 1.08f, 0.22f, "Leaps between boulders on powerful hind legs.", "Mountain"),
                new InsectSeed("cricket_stone", "Stone Cricket", InsectRarity.Common, 1.06f, 0.23f, "Chirps echo through the scree at twilight.", "Mountain"),
                new InsectSeed("pill_bug_rock", "Rock Pill Bug", InsectRarity.Common, 1.00f, 0.21f, "Its armored shell shrugs off falling pebbles.", "Mountain"),
                new InsectSeed("ladybug_alpine", "Alpine Ladybug", InsectRarity.Common, 0.96f, 0.24f, "A hardy ladybug that winters under high rocks.", "Mountain"),
                new InsectSeed("beetle_longhorn_alpine", "Alpine Longhorn", InsectRarity.Uncommon, 0.60f, 0.36f, "Blue-grey and tough, it thrives above the treeline.", "Mountain"),
                new InsectSeed("cicada_mountain", "Mountain Cicada", InsectRarity.Uncommon, 0.58f, 0.35f, "Its call bounces between the high cliff walls.", "Mountain"),
                new InsectSeed("caterpillar_pine", "Pine Caterpillar", InsectRarity.Uncommon, 0.65f, 0.32f, "Marches nose-to-tail with its kin along pine boughs.", "Mountain"),
                new InsectSeed("stag_beetle_mountain", "Mountain Stag Beetle", InsectRarity.Rare, 0.26f, 0.48f, "Wrestles rivals on wind-blasted ridges.", "Mountain"),
                new InsectSeed("butterfly_apollo", "Apollo Butterfly", InsectRarity.Rare, 0.20f, 0.54f, "White wings with red eyespots drift over alpine meadows.", "Mountain"),
                new InsectSeed("spider_cliff", "Cliff Spider", InsectRarity.Rare, 0.22f, 0.51f, "Anchors its web across dizzying rock faces.", "Mountain"),
                new InsectSeed("stag_beetle_iron", "Iron Stag Beetle", InsectRarity.Epic, 0.10f, 0.61f, "Its metallic jaws can dent solid rock.", "Mountain"),
                new InsectSeed("cicada_ancient", "Ancient Cicada", InsectRarity.Legendary, 0.04f, 0.80f, "Slept underground for an age before its first song.", "Mountain"),

                // ── Ruins (11) — 신규 최종 리전 Lv.36~50: Rare/Epic/Legendary 위주 ──
                new InsectSeed("cricket_tomb", "Tomb Cricket", InsectRarity.Uncommon, 0.50f, 0.38f, "Sings alone in the silence of buried halls.", "Ruins"),
                new InsectSeed("scarab_relic", "Relic Scarab", InsectRarity.Rare, 0.24f, 0.50f, "Carved patterns on its shell mirror the old murals.", "Ruins"),
                new InsectSeed("mantis_obsidian", "Obsidian Mantis", InsectRarity.Rare, 0.20f, 0.54f, "Its blades gleam like polished black glass.", "Ruins"),
                new InsectSeed("spider_tomb", "Tomb Spider", InsectRarity.Rare, 0.21f, 0.53f, "Weaves dusty webs across forgotten doorways.", "Ruins"),
                new InsectSeed("centipede_ruin", "Ruin Centipede", InsectRarity.Rare, 0.23f, 0.51f, "Slithers through cracks in the crumbling stone.", "Ruins"),
                new InsectSeed("jewel_beetle_azure", "Azure Jewel Beetle", InsectRarity.Epic, 0.10f, 0.62f, "A living sapphire found among the toppled pillars.", "Ruins"),
                new InsectSeed("moth_shadow", "Shadow Moth", InsectRarity.Epic, 0.11f, 0.61f, "Flutters through torchless corridors unseen.", "Ruins"),
                new InsectSeed("wasp_gold", "Gold Wasp", InsectRarity.Epic, 0.09f, 0.63f, "Gilded armor flashes as it guards the treasury.", "Ruins"),
                new InsectSeed("scarab_pharaoh", "Pharaoh Scarab", InsectRarity.Legendary, 0.04f, 0.83f, "Legends say it rolled the sun across the sky.", "Ruins"),
                new InsectSeed("butterfly_midnight", "Midnight Empress Butterfly", InsectRarity.Legendary, 0.035f, 0.82f, "Appears only when moonlight touches the altar.", "Ruins"),
                new InsectSeed("hornet_emperor", "Emperor Hornet", InsectRarity.Legendary, 0.04f, 0.81f, "The undisputed tyrant of the ruined skies.", "Ruins"),

                // ── 서브에리어 전용 (5) — 각 1종 ──
                new InsectSeed("diving_beetle_king", "King Diving Beetle", InsectRarity.Rare, 0.24f, 0.50f, "Rules the deepest waters of the pond.", "Pond"),
                new InsectSeed("mantis_dead_leaf", "Dead Leaf Mantis", InsectRarity.Epic, 0.09f, 0.63f, "Indistinguishable from the litter of the deep forest.", "Forest"),
                new InsectSeed("mantis_mist", "Mist Mantis", InsectRarity.Epic, 0.09f, 0.64f, "Half-seen through the fog, then suddenly upon you.", "Swamp"),
                new InsectSeed("moth_comet", "Comet Moth", InsectRarity.Legendary, 0.04f, 0.82f, "Twin tails trail like a comet across the summit sky.", "Mountain"),
                new InsectSeed("mantis_gold_temple", "Golden Temple Mantis", InsectRarity.Legendary, 0.035f, 0.84f, "A gilded sentinel said to guard the inner shrine.", "Ruins")
            };
        }

        /// <summary>테스트/검증용 — CreateAll()에서 파생한 신규 ID 전체 목록.</summary>
        public static string[] AllNewIds()
        {
            InsectSeed[] seeds = CreateAll();
            string[] ids = new string[seeds.Length];
            for (int i = 0; i < seeds.Length; i++)
            {
                ids[i] = seeds[i].id;
            }
            return ids;
        }
    }
}
