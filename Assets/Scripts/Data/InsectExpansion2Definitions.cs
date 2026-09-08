using System.Collections.Generic;

namespace InsectGame.Data
{
    /// <summary>
    /// 2막(ver2) 확장 곤충 시드 — "장부에 없는 땅" 6지역의 서식종.
    /// <see cref="InsectExpansionDefinitions"/>와 같은 형태이고, 부트스트랩
    /// EnsureExpandedDatabase가 CreateAll()을 두 번째로 소비해 InsectData로 변환한다.
    ///
    /// 파일을 나눈 이유: 1막 확장 64종은 <c>InsectExpansionDefinitionsTests</c>가
    /// 개수·등급 분포를 정확한 값으로 고정하고 있다. 같은 배열에 2막 종을 밀어 넣으면
    /// 그 테스트가 전부 깨지고, 깨진 값을 고쳐 맞추면 1막 확장의 회귀 검출력이 사라진다.
    ///
    /// ID 명명 규칙은 <see cref="InsectExpansionDefinitions"/> 상단 주석이 단일 출처다
    /// (InsectEntity.BuildModel 분기 오매칭 방지). 요약:
    /// - 벌은 bee_ 접두(bee ⊂ beetle), 개미류는 antlion만, 파리는 fly_ 접두(fly ⊂ dragonfly 등)
    /// - 'ant'를 품는 수식어(giant/phantom/tarantula) 금지 — BuildAnt로 오라우팅된다
    /// - ghost/orchid는 사마귀 전용, luna/atlas는 나방 전용
    ///
    /// 레어도 밴드(기존 128종 준수): weight C 0.90~1.30 / U 0.45~0.72 / R 0.18~0.30 /
    /// E 0.08~0.14 / L 0.03~0.05, difficulty C 0.18~0.25 / U 0.32~0.39 / R 0.44~0.56 /
    /// E 0.58~0.65 / L 0.78~0.85.
    ///
    /// habitat 태그(Hollow/Dunes/Frostline/Emberfall/Canopy/Nameless)는
    /// PlaySceneBootstrap.InferPrimaryType의 zone 폴백과 짝이다 — 한쪽만 고치면 오타가 조용히
    /// Bug 속성으로 떨어진다.
    /// </summary>
    public static class InsectExpansion2Definitions
    {
        /// <summary>2막 신규 시드 전체. 매 호출마다 새 배열 반환(공유 상태 없음).</summary>
        public static InsectSeed[] CreateAll()
        {
            return new InsectSeed[]
            {
                // ── Hollow / 텅 빈 들 (6) — Lv.42~48 ──
                // 잦아듦이 가장 먼저 훑고 간 폐허 초원. 종을 적게 두고 나머지는 초원·습지 종을
                // 재활용해 "초원이 죽은 모습"을 만든다(신규 종을 채우면 오히려 풍요로워 보인다).
                new InsectSeed("cricket_hush", "Hush Cricket", InsectRarity.Common, 1.02f, 0.23f, "It never sings. The field stays silent wherever it settles.", "Hollow"),
                new InsectSeed("moth_ashen", "Ashen Moth", InsectRarity.Common, 0.94f, 0.24f, "Grey dust falls from its wings and settles on nothing.", "Hollow"),
                new InsectSeed("beetle_husk", "Husk Beetle", InsectRarity.Uncommon, 0.54f, 0.37f, "Only the shell is left, yet it still walks the dry field.", "Hollow"),
                new InsectSeed("spider_threadbare", "Threadbare Spider", InsectRarity.Rare, 0.23f, 0.51f, "Its old web hangs torn, catching nothing but wind.", "Hollow"),
                new InsectSeed("mantis_hollow", "Hollow Mantis", InsectRarity.Epic, 0.09f, 0.63f, "A pale mantis that no longer remembers what it hunted.", "Hollow"),
                new InsectSeed("moth_forgotten", "Forgotten Moth", InsectRarity.Epic, 0.10f, 0.62f, "Its wing pattern has faded to blank grey.", "Hollow"),

                // ── Dunes / 모래언덕 (12) — Lv.46~52 ──
                new InsectSeed("beetle_sand", "Sand Beetle", InsectRarity.Common, 1.16f, 0.20f, "Skims across hot dunes on long stilted legs.", "Dunes"),
                new InsectSeed("cricket_dune", "Dune Cricket", InsectRarity.Common, 1.08f, 0.22f, "Burrows at noon and chirps only after dark.", "Dunes"),
                new InsectSeed("fly_sand", "Sand Fly", InsectRarity.Common, 1.20f, 0.19f, "Rises in clouds wherever the sand is disturbed.", "Dunes"),
                new InsectSeed("pill_bug_desert", "Desert Pill Bug", InsectRarity.Common, 0.98f, 0.21f, "Seals itself into a stone-hard ball against the heat.", "Dunes"),
                new InsectSeed("bee_digger", "Digger Bee", InsectRarity.Uncommon, 0.58f, 0.34f, "Sinks tunnels into the dune face to raise its young.", "Dunes"),
                new InsectSeed("antlion_dune", "Dune Antlion", InsectRarity.Uncommon, 0.52f, 0.37f, "Its funnel trap swallows anything that slips the rim.", "Dunes"),
                new InsectSeed("grasshopper_locust", "Desert Locust", InsectRarity.Uncommon, 0.66f, 0.33f, "One becomes a thousand when the rains finally come.", "Dunes"),
                new InsectSeed("spider_camel", "Camel Spider", InsectRarity.Rare, 0.26f, 0.48f, "Runs the open sand faster than anything its size should.", "Dunes"),
                new InsectSeed("scarab_sand", "Sand Scarab", InsectRarity.Rare, 0.24f, 0.50f, "Rolls a perfect sphere across the dunes at dawn.", "Dunes"),
                new InsectSeed("wasp_hawk", "Hawk Wasp", InsectRarity.Rare, 0.20f, 0.54f, "Hunts spiders twice its weight and drags them home.", "Dunes"),
                new InsectSeed("centipede_sand", "Sand Centipede", InsectRarity.Epic, 0.11f, 0.60f, "Swims through loose sand like water, never breaking the surface.", "Dunes"),
                new InsectSeed("hornet_dune", "Dune Hornet", InsectRarity.Epic, 0.09f, 0.64f, "Its paper tower stands hollow over the dunes, humming.", "Dunes"),

                // ── Frostline / 서릿길 (9) — Lv.50~56 ──
                // 얼어붙어 시간이 멈춘 땅. 여기 종들은 한 번도 이름을 잃은 적이 없다.
                new InsectSeed("pill_bug_frost", "Frost Pill Bug", InsectRarity.Common, 1.00f, 0.22f, "Its plates frost over at night and thaw by noon.", "Frostline"),
                new InsectSeed("cricket_frost", "Frost Cricket", InsectRarity.Common, 0.96f, 0.23f, "Chirps slow and low, one note every long breath.", "Frostline"),
                new InsectSeed("moth_snow", "Snow Moth", InsectRarity.Common, 0.92f, 0.24f, "White scales drift from its wings like fine snow.", "Frostline"),
                new InsectSeed("beetle_rime", "Rime Beetle", InsectRarity.Uncommon, 0.56f, 0.36f, "A crust of ice grows along its back and never melts.", "Frostline"),
                new InsectSeed("spider_frost", "Frost Spider", InsectRarity.Uncommon, 0.50f, 0.38f, "Spins webs that freeze solid and ring when struck.", "Frostline"),
                new InsectSeed("stag_beetle_glacier", "Glacier Stag Beetle", InsectRarity.Rare, 0.24f, 0.50f, "Pale jaws that have not opened in a very long time.", "Frostline"),
                new InsectSeed("butterfly_snowveil", "Snowveil Butterfly", InsectRarity.Rare, 0.21f, 0.53f, "Its wings are so thin the ice behind them shows through.", "Frostline"),
                new InsectSeed("mantis_icicle", "Icicle Mantis", InsectRarity.Epic, 0.10f, 0.61f, "Hangs motionless from a frozen branch, indistinguishable from it.", "Frostline"),
                new InsectSeed("moth_aurora", "Aurora Moth", InsectRarity.Epic, 0.09f, 0.64f, "Cold light ripples across its wings in slow bands.", "Frostline"),

                // ── Emberfall / 잿불 골짜기 (9) — Lv.54~60 ──
                // 기록이 불타 없어진 땅. 빈칸이 가장 두껍게 겹친 곳.
                new InsectSeed("beetle_cinder", "Cinder Beetle", InsectRarity.Common, 1.10f, 0.21f, "Walks over warm ash without leaving a print.", "Emberfall"),
                new InsectSeed("cricket_ember", "Ember Cricket", InsectRarity.Common, 1.02f, 0.23f, "Its song crackles like a fire settling.", "Emberfall"),
                new InsectSeed("fly_ash", "Ash Fly", InsectRarity.Common, 1.18f, 0.19f, "Drifts up on the heat and never seems to land.", "Emberfall"),
                new InsectSeed("centipede_ember", "Ember Centipede", InsectRarity.Uncommon, 0.54f, 0.37f, "Glowing seams run between its plates.", "Emberfall"),
                new InsectSeed("wasp_ash", "Ash Wasp", InsectRarity.Uncommon, 0.48f, 0.38f, "Builds its comb from cooled ash and spit.", "Emberfall"),
                new InsectSeed("cicada_ember", "Ember Cicada", InsectRarity.Rare, 0.25f, 0.49f, "Calls loudest where the ground is hottest.", "Emberfall"),
                new InsectSeed("beetle_longhorn_char", "Charred Longhorn", InsectRarity.Rare, 0.22f, 0.52f, "Bores into burnt trunks that no other insect will touch.", "Emberfall"),
                new InsectSeed("mantis_ember", "Ember Mantis", InsectRarity.Epic, 0.10f, 0.62f, "Its forelimbs glow faintly before it strikes.", "Emberfall"),
                new InsectSeed("hornet_magma", "Magma Hornet", InsectRarity.Epic, 0.08f, 0.65f, "Nests in a vent wall where nothing else survives.", "Emberfall"),

                // ── Canopy / 우듬지 (9) — Lv.58~64 ──
                // 꽃밭과 같은 이유로 무사한 땅(예비 울타리). 유일하게 종이 풍성한 2막 리전이다.
                new InsectSeed("aphid_canopy", "Canopy Aphid", InsectRarity.Common, 1.24f, 0.18f, "Rides the high leaves and never once touches ground.", "Canopy"),
                new InsectSeed("caterpillar_silk", "Silk Caterpillar", InsectRarity.Common, 1.06f, 0.20f, "Lowers itself on a thread when the branch sways.", "Canopy"),
                new InsectSeed("ladybug_canopy", "Canopy Ladybug", InsectRarity.Common, 1.00f, 0.22f, "Patrols the aphid herds along the upper boughs.", "Canopy"),
                new InsectSeed("bee_stingless", "Stingless Bee", InsectRarity.Uncommon, 0.60f, 0.33f, "Defends its hive by sheer number instead of venom.", "Canopy"),
                new InsectSeed("katydid_canopy", "Canopy Katydid", InsectRarity.Uncommon, 0.55f, 0.35f, "So exactly leaf-shaped that it casts a leaf's shadow.", "Canopy"),
                new InsectSeed("stick_insect_canopy", "Canopy Stick Insect", InsectRarity.Rare, 0.24f, 0.49f, "Sways in time with the branch it is pretending to be.", "Canopy"),
                new InsectSeed("butterfly_crown", "Crown Butterfly", InsectRarity.Rare, 0.20f, 0.53f, "Circles the treetop at dawn and never descends.", "Canopy"),
                new InsectSeed("mantis_canopy", "Canopy Mantis", InsectRarity.Epic, 0.09f, 0.63f, "Rules the upper boughs; nothing large enough disputes it.", "Canopy"),
                new InsectSeed("butterfly_worldtree", "Worldtree Butterfly", InsectRarity.Legendary, 0.04f, 0.80f, "Its wings carry a pattern older than any written record.", "Canopy"),

                // ── Nameless / 이름 없는 자리 (9) — Lv.62~70 ──
                // 지도에 없는 땅. 이름을 빼앗긴 것들이 모이는 자리라 hollow의 종이 여기 다시 나온다.
                new InsectSeed("moth_pale", "Pale Moth", InsectRarity.Common, 0.90f, 0.24f, "Colourless even in direct light.", "Nameless"),
                new InsectSeed("cricket_still", "Still Cricket", InsectRarity.Common, 0.92f, 0.23f, "Holds its wings open as if about to sing, and does not.", "Nameless"),
                new InsectSeed("spider_blank", "Blank Spider", InsectRarity.Common, 0.94f, 0.24f, "Its web has no pattern at all, only threads.", "Nameless"),
                new InsectSeed("beetle_unwritten", "Unwritten Beetle", InsectRarity.Uncommon, 0.50f, 0.38f, "The marks on its shell stop halfway, as if abandoned.", "Nameless"),
                new InsectSeed("centipede_pale", "Pale Centipede", InsectRarity.Uncommon, 0.46f, 0.39f, "Moves without disturbing the dust it crosses.", "Nameless"),
                new InsectSeed("mantis_blank", "Blank Mantis", InsectRarity.Rare, 0.22f, 0.53f, "Faces you squarely and shows nothing at all.", "Nameless"),
                new InsectSeed("butterfly_erased", "Erased Butterfly", InsectRarity.Rare, 0.19f, 0.55f, "Wing scales rub away at a touch and do not grow back.", "Nameless"),
                new InsectSeed("moth_effaced", "Effaced Moth", InsectRarity.Epic, 0.09f, 0.64f, "Whatever was written on it has been thoroughly gone over.", "Nameless"),
                new InsectSeed("mantis_unnamed", "Unnamed Mantis", InsectRarity.Legendary, 0.03f, 0.84f, "The largest thing here to have lost its name.", "Nameless"),

                // ── 리전 고유성 보강 (12) ──
                // 처음 배치할 때 frostline·emberfall·canopy가 각각 산·유적·숲 종을 5개씩 빌려
                // 썼다. 생태적으로는 말이 되지만 Lv.50~64 구간에서 이미 잡은 종이 절반 가까이
                // 나오면 새 땅에 온 느낌이 안 난다. 리전당 4종씩 채워 12/14를 전용으로 만든다.
                // (hollow는 일부러 늘리지 않는다 — 거긴 '지워진 개체'가 채운다. 위 Hollow 블록 주석 참조.)

                // Frostline / 서릿길 — Lv.50~56
                new InsectSeed("beetle_hoarfrost", "Hoarfrost Beetle", InsectRarity.Common, 1.00f, 0.24f, "Frost grows along its back in the shape of fern leaves.", "Frostline"),
                new InsectSeed("katydid_snowfield", "Snowfield Katydid", InsectRarity.Uncommon, 0.58f, 0.36f, "It chirps only when the wind dies, and the sound carries far.", "Frostline"),
                new InsectSeed("bee_glacier", "Glacier Bee", InsectRarity.Rare, 0.25f, 0.48f, "It warms its flight muscles for a full minute before leaving the hive.", "Frostline"),
                new InsectSeed("centipede_frost", "Frost Centipede", InsectRarity.Rare, 0.21f, 0.53f, "It moves under the ice crust, tracing pale lines you can follow.", "Frostline"),

                // Emberfall / 잿불 골짜기 — Lv.54~60
                new InsectSeed("pill_bug_cinder", "Cinder Pill Bug", InsectRarity.Common, 1.06f, 0.22f, "It rolls through warm ash without scorching its plates.", "Emberfall"),
                new InsectSeed("cricket_slag", "Slag Cricket", InsectRarity.Uncommon, 0.61f, 0.35f, "It nests in cooled slag and drums against the hollow stone.", "Emberfall"),
                new InsectSeed("beetle_scorch", "Scorch Beetle", InsectRarity.Rare, 0.24f, 0.49f, "Its shell is banded black and orange, still hot to the touch.", "Emberfall"),
                new InsectSeed("moth_smoulder", "Smouldering Moth", InsectRarity.Epic, 0.11f, 0.61f, "Embers glow along the veins of its wings and never quite go out.", "Emberfall"),

                // Canopy / 우듬지 — Lv.58~64
                new InsectSeed("beetle_bark_canopy", "Canopy Bark Beetle", InsectRarity.Common, 0.98f, 0.23f, "It grazes the high bark that never sees the forest floor.", "Canopy"),
                new InsectSeed("cicada_crown", "Crown Cicada", InsectRarity.Uncommon, 0.55f, 0.38f, "Its call comes from so high that it sounds like weather.", "Canopy"),
                new InsectSeed("moth_leafveil", "Leafveil Moth", InsectRarity.Rare, 0.22f, 0.50f, "At rest it is indistinguishable from a living leaf, veins and all.", "Canopy"),
                // 'orchid'는 사마귀 전용 키워드다(BuildModel 분기가 bee보다 먼저 걸린다) — bee_perfume으로 둔다.
                new InsectSeed("bee_perfume", "Perfume Bee", InsectRarity.Epic, 0.12f, 0.59f, "It gathers scent instead of nectar, and carries it for days.", "Canopy"),
            };
        }

        /// <summary>시드 ID 전체 — 테스트/검증용.</summary>
        public static string[] AllNewIds()
        {
            InsectSeed[] seeds = CreateAll();
            List<string> ids = new List<string>(seeds.Length);
            foreach (InsectSeed seed in seeds) ids.Add(seed.id);
            return ids.ToArray();
        }
    }
}
