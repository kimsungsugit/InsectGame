using System.Collections.Generic;

namespace InsectGame.Data
{
    /// <summary>
    /// 곤충 성향 표 — <b>종마다 한 줄</b>(<see cref="InsectHabits"/>의 저작 데이터). 규칙(배수·궁합·습격)은 InsectHabits.cs가 든다.
    ///
    /// <b>대상.</b> 필드·서브에리어·가챠 전 종 194종 — 부트스트랩 <c>EnsureExpandedDatabase</c>가 만드는 기본 종과
    /// <c>InsectExpansionDefinitions</c>·<c>InsectExpansion2Definitions</c> 시드 전부다. 종을 늘리면 여기에 한 줄을 더한다
    /// (안 하면 <see cref="InsectHabit.Neutral"/>이 되어 조용히 성향 없는 종이 된다 — <c>InsectHabitsTests</c>가 DB와 표를 맞춰 본다).
    ///
    /// <b>읽는 법.</b> <c>R(종 ID, 활동 시간대, 좋아하는 날씨, 싫어하는 날씨[, 기질])</c>. 날씨는 <c>|</c>로 묶는다.
    ///
    /// <b>정한 방식 — 곤충학 근거를 계통 단위로 먼저 정하고(아래 구역 머리말), 종마다 서식지·생김새로 예외를 뒀다.</b>
    /// <code>
    /// 활동 시간대   주행성: 나비·벌·말벌·잠자리·무당벌레·매미·메뚜기·비단벌레·스카라베 (햇볕이 있어야 날고 체온을 올린다)
    ///               야행성: 나방·반딧불·귀뚜라미·지네·집게벌레·모기·사슴벌레·장수풍뎅이·대벌레·여치·개미귀신 (밤에 먹이를 찾고 짝을 부른다)
    ///               무관: 쥐며느리·진딧물·애벌레·물방개·소똥구리, 그리고 시간이 멈춘 땅(지워진 곳·이름 없는 자리)의 종
    /// 날씨 취향     비 ↓: 날개가 얇은 날것(나비·벌·반딧불·매미·말벌)  비 ↑: 물·습기에 사는 것(물방개·소금쟁이·모기·지네·잎 먹는 것)
    ///               센바람 ↓: 줄을 치거나 불빛이 흩어지는 것(거미·반딧불·모기·소금쟁이)  센바람 ↑: 바람 속성(나비·벌·나방·잠자리)·먼지 바람 속의 종
    ///               눈 ↓: 따뜻한 철의 변온동물(나비·나방·귀뚜라미·물방개)  눈 ↑: 서릿길·산에 사는 종(사용자 요청)
    ///               안개 ↑: 습기를 먹는 야행성·매복 포식자(안개가 엄폐다)  안개 ↓: 햇볕을 쬐어야 하는 것(무당벌레·메뚜기)
    ///               맑음 ↑: 주행성 대부분·마른 땅의 종  맑음 ↓: 마름을 싫어하는 것(지네·집게벌레)
    /// 속성 정합     <b>물 속성은 비를 싫어하지 않고, 바람 속성은 센바람을 좋아한다</b>(사용자 근거 예). 속성은 부트스트랩이 이름·서식지로
    ///               정하므로(InferPrimaryType) 날것이 대부분 바람 속성이다 — "날것은 센바람을 싫어함"과 부딪히는 자리라 바람 속성이 이긴다.
    ///               <c>InsectHabitsTests</c>가 DB의 속성으로 이 약속을 고정한다(속성이 바뀌면 표를 같이 고친다).
    /// 기질          습격형: 매복·사냥하는 종 위주(말벌류·사마귀·지네·거미·몇몇 큰 종). 등급이 높을수록 많다 —
    ///               일반 2 · 고급 3 · 희귀 7 · 영웅 8 · 전설 5. 전부 활동 시간대가 있다(시간 무관 습격형은 깨는 때가 없다).
    ///               가챠 전용종은 필드에 나오지 않으므로 전부 온순이다.
    /// </code>
    ///
    /// <b>균형.</b> 한 날씨에 풀의 대부분이 불리해지지 않게 맞췄다 — 어떤 리전 풀에서도 한 날씨가 불리한 종은 절반 아래이고
    /// (<c>InsectHabitsTests</c>가 풀마다 센다), 전체로도 어느 날씨든 싫어하는 종이 3분의 1을 넘지 않는다. 처음엔 나비·벌이 전부
    /// 비·바람을 싫어해 정원 풀 17종 중 15종이 비에 불리했다 — 계통 단위로만 정하면 이렇게 쏠리므로 종마다 예외를 두었다.
    ///
    /// 날씨는 <b>지역에서 보이는 날씨</b>다(<c>WeatherForecast.EffectiveIn</c>) — 설산에서는 비가 눈이 되므로 서릿길·산 종은
    /// 눈 날이 길다. 사막·잿불에는 눈이 오지 않는다(센바람으로 바뀐다).
    /// </summary>
    public static partial class InsectHabits
    {
        // 표를 짧게 적기 위한 별칭 — 이 파일 안에서만 쓴다.
        private const InsectActivity Any = InsectActivity.Any;
        private const InsectActivity Diurnal = InsectActivity.Diurnal;
        private const InsectActivity Nocturnal = InsectActivity.Nocturnal;
        private const WeatherSet None = WeatherSet.None;
        private const WeatherSet Clear = WeatherSet.Clear;
        private const WeatherSet Rain = WeatherSet.Rain;
        private const WeatherSet Fog = WeatherSet.Fog;
        private const WeatherSet Wind = WeatherSet.Wind;
        private const WeatherSet Snow = WeatherSet.Snow;
        private const InsectTemperament Docile = InsectTemperament.Docile;
        private const InsectTemperament Ambusher = InsectTemperament.Ambusher;

        private static Dictionary<string, InsectHabit> BuildTable()
        {
            var t = new Dictionary<string, InsectHabit>(256);
            int lines = 0;

            void R(string insectId, InsectActivity activity, WeatherSet likes, WeatherSet dislikes,
                InsectTemperament temperament = Docile)
            {
                lines++;
                t[insectId] = new InsectHabit(activity, likes, dislikes, temperament);
            }

            // ── 꿀벌 ──
            // 주행성 · 맑음 ↑ · 비 ↓ — 비행근육을 데우려 햇볕이 필요하고 비가 오면 벌집으로 돌아간다. 바람 속성이라 센바람 ↑(바람을 타고 난다).
            // 범블비는 추위·비를 견뎌 싫은 날씨가 없고, 여왕벌은 벌집 안이라 맑음을 따지지 않는다. 설산 벌(glacier)은 눈 ↑.
            R("bee_worker",                Diurnal,   Clear | Wind,  Rain);
            R("bee_bumble",                Diurnal,   Clear | Wind,  None);
            R("bee_carpenter",             Diurnal,   Clear | Wind,  Rain);
            R("bee_queen",                 Diurnal,   Wind,          Rain);
            R("bee_digger",                Diurnal,   Clear | Wind,  Rain);
            R("bee_stingless",             Diurnal,   Fog | Wind,    Rain);
            R("bee_glacier",               Diurnal,   Clear | Wind | Snow, Rain);
            R("bee_perfume",               Diurnal,   Clear | Wind,  Rain);

            // ── 말벌·장수말벌 ──
            // 주행성 포식자 — 비 ↓(둥지로 들어간다). 밤말벌(wasp_night)만 야행성 · 안개 ↑ · 눈 ↓. 종이말벌은 싫은 날씨가 없다.
            // 사막·잿불 말벌은 건조한 열(맑음)·먼지 바람 ↑. 번개말벌(가챠)은 비·바람 ↑(폭풍). 습격형은 표에 Ambusher로 적었다.
            R("hornet_asian",              Diurnal,   None,          Rain, Ambusher);
            R("hornet_emperor",            Diurnal,   Wind,          Rain, Ambusher);
            R("hornet_dune",               Diurnal,   Clear | Wind,  Rain, Ambusher);
            R("hornet_magma",              Diurnal,   Clear,         Rain);
            R("wasp_paper",                Diurnal,   Clear,         None, Ambusher);
            R("wasp_night",                Nocturnal, Fog,           Snow, Ambusher);
            R("wasp_gold",                 Diurnal,   Clear,         Rain, Ambusher);
            R("wasp_hawk",                 Diurnal,   Clear,         Rain, Ambusher);
            R("wasp_ash",                  Diurnal,   Wind,          Rain);
            R("gacha_storm_hornet",        Diurnal,   Rain | Wind,   None);

            // ── 나비 ──
            // 주행성 · 맑음 ↑ · 바람 속성이라 센바람 ↑. 얇은 날개 종(cabbage·azure·apollo·crown·worldtree)은 비 ↓,
            // 변온 나비(monarch·swallowtail·peacock)와 열대 우림종(morpho·glasswing·alexandras)은 눈 ↓(변온동물은 눈을 싫어한다) — 열대종은 안개 ↑(습한 숲).
            // 밤의 여제(midnight)는 달빛 아래 나는 야행성. 설원 나비(snowveil)는 눈 ↑. 지워진 땅의 나비(erased)는 시간을 안 탄다.
            R("butterfly_cabbage",         Diurnal,   Clear | Wind,  Rain);
            R("butterfly_azure",           Diurnal,   Clear | Wind,  Rain);
            R("butterfly_monarch",         Diurnal,   Clear | Wind,  Snow);
            R("butterfly_swallowtail",     Diurnal,   Clear | Wind,  Snow);
            R("butterfly_morpho",          Diurnal,   Fog | Wind,    Snow);
            R("butterfly_alexandras",      Diurnal,   Clear | Fog | Wind, Snow);
            R("butterfly_peacock",         Diurnal,   Clear | Wind,  Snow);
            R("butterfly_glasswing",       Diurnal,   Fog | Wind,    Snow);
            R("butterfly_apollo",          Diurnal,   Clear | Wind,  Rain);
            R("butterfly_midnight",        Nocturnal, Clear | Wind,  Rain);
            R("butterfly_snowveil",        Diurnal,   Wind | Snow,   None);
            R("butterfly_crown",           Diurnal,   Clear | Wind,  Rain);
            R("butterfly_worldtree",       Any,       Wind,          Rain);
            R("butterfly_erased",          Any,       Fog | Wind,    None);
            R("gacha_rainbow_butterfly",   Diurnal,   Clear,         Rain);

            // ── 나방 ──
            // 야행성 · 안개 ↑(습한 밤공기) · 바람 속성이라 센바람 ↑. 낮에 나는 벌새나방은 주행성 · 안개 ↓. 설산 나방(snow·aurora)은 눈 ↑,
            // 보통 나방(brown·night·luna)은 눈 ↓. 그림자나방(shadow)은 맑음 ↓(어둠 속 종), 잿불 나방(smoulder)은 비 ↓, 혜성나방(comet)은 맑은 정상 하늘 ↑.
            // 지워진 땅·이름 없는 자리의 나방(ashen·pale·effaced·forgotten)은 시간을 안 탄다 — 그 땅은 멈춰 있다.
            R("moth_brown",                Nocturnal, Fog | Wind,    Snow);
            R("moth_night",                Nocturnal, Fog | Wind,    Snow);
            R("moth_hummingbird",          Diurnal,   Clear | Wind,  Fog);
            R("moth_shadow",               Nocturnal, Fog | Wind,    Clear);
            R("moth_comet",                Nocturnal, Clear | Wind,  None);
            R("moth_ashen",                Any,       Wind,          None);
            R("moth_forgotten",            Any,       Fog | Wind,    None);
            R("moth_snow",                 Nocturnal, Wind | Snow,   None);
            R("moth_aurora",               Nocturnal, Wind | Snow,   None);
            R("moth_pale",                 Any,       Fog | Wind,    None);
            R("moth_effaced",              Any,       Fog | Wind,    None);
            R("moth_smoulder",             Nocturnal, Clear | Wind,  Rain);
            R("moth_leafveil",             Nocturnal, Rain | Fog | Wind, None);
            R("atlas_moth_giant",          Nocturnal, Fog | Wind,    None);
            R("luna_moth_silver",          Nocturnal, Fog | Wind,    Snow);
            R("gacha_phantom_moth",        Nocturnal, Fog,           None);

            // ── 잠자리·실잠자리 ──
            // 주행성 · 맑음 ↑(시각 사냥) · 바람 속성이라 센바람 ↑(활공). 작은 잠자리(scarlet·jade)는 비 ↓, 실잠자리는 가늘어서 비·안개 둘 다 ↓.
            // 왕잠자리·고대잠자리는 날씨를 안 가린다. 호수잠자리(lake)는 물 속성이라 비 ↑, 습지잠자리(swamp_hawker)는 안개 선을 순찰해 안개 ↑.
            R("dragonfly_lake",            Diurnal,   Clear | Rain | Wind, None);
            R("dragonfly_emperor",         Diurnal,   Clear | Wind,  None);
            R("dragonfly_ancient",         Diurnal,   Clear | Wind,  None, Ambusher);
            R("dragonfly_scarlet",         Diurnal,   Clear | Wind,  Rain);
            R("dragonfly_jade",            Diurnal,   Clear | Wind,  Rain);
            R("dragonfly_swamp_hawker",    Diurnal,   Fog | Wind,    None);
            R("damselfly_blue",            Diurnal,   Clear | Wind,  Rain | Fog);
            R("damselfly_red",             Diurnal,   Clear | Wind,  Rain | Fog);
            R("gacha_crystal_dragonfly",   Diurnal,   Clear | Wind,  None);

            // ── 파리·모기 ──
            // 파리: 주행성 · 맑음 ↑. 집파리는 바람 ↓, 꽃등에는 안개 ↓(호버링). 각다귀(crane)는 비·안개 ↑, 모래·재 파리는 먼지 바람 ↑.
            // 모기: 물 속성 · 야행성(호랑이모기만 주행) · 비 ↑ · 센바람 ↓. 호랑이·집모기는 눈도 ↓, 호랑이모기 외에는 안개 ↑(습도).
            R("mosquito_common",           Nocturnal, Rain | Fog,    Wind | Snow);
            R("mosquito_tiger",            Diurnal,   Rain,          Wind | Snow);
            R("mosquito_swamp",            Nocturnal, Rain | Fog,    Wind);
            R("fly_house",                 Diurnal,   Clear,         Wind);
            R("fly_hover",                 Diurnal,   Clear,         Fog);
            R("fly_crane",                 Any,       Rain | Fog,    None);
            R("fly_sand",                  Diurnal,   Clear | Wind,  None);
            R("fly_ash",                   Any,       Wind,          None);

            // ── 반딧불이 ──
            // 야행성 · 안개 ↑(습한 풀밭) · 비 ↓ — 비가 오면 발광을 멈춘다. 연못·습지 반딧불은 센바람도 ↓(불빛이 흩어진다).
            R("firefly_blue",              Nocturnal, Fog,           Rain | Wind);
            R("firefly_glow",              Nocturnal, Fog,           Rain);
            R("firefly_marsh",             Nocturnal, Fog,           Rain | Wind);
            R("firefly_swamp",             Nocturnal, Fog,           Rain | Wind);
            R("gacha_neon_firefly",        Nocturnal, Fog,           Rain | Wind);

            // ── 무당벌레 ──
            // 주행성 · 맑음 ↑(일광욕) · 안개 ↓. 고산 무당벌레(alpine)는 눈 ↑.
            R("ladybug_seven",             Diurnal,   Clear,         Fog);
            R("ladybug_harlequin",         Diurnal,   Clear,         Fog);
            R("ladybug_alpine",            Diurnal,   Clear | Snow,  Fog);
            R("ladybug_canopy",            Diurnal,   Clear,         Fog);
            R("gacha_golden_ladybug",      Diurnal,   Clear,         Fog);

            // ── 매미 ──
            // 주행성 · 맑음 ↑ · 비 ↓(비 오면 울지 않는다). 여름매미는 눈도 ↓. 저녁매미는 해질녘 종이라 시간 무관.
            // 고산 매미는 눈 ↑(고대매미는 비 ↓), 잿불 매미는 열 ↑, 우듬지 매미는 센바람 ↑.
            R("cicada_evening",            Any,       Clear,         None);
            R("cicada_summer",             Diurnal,   Clear,         Rain | Snow);
            R("cicada_mountain",           Diurnal,   Clear | Snow,  None);
            R("cicada_ancient",            Diurnal,   Snow,          Rain);
            R("cicada_ember",              Diurnal,   Clear,         None);
            R("cicada_crown",              Diurnal,   Clear | Wind,  Rain);

            // ── 귀뚜라미 ──
            // 야행성 · 눈 ↓(추우면 울음이 멈춘다). 마른 땅 종(dune·ember·slag)은 맑음 ↑, 설산·고산 종(frost·stone)은 눈 ↑.
            // 지워진 땅 종(hush·tomb)은 안개 ↑. 이름 없는 자리의 귀뚜라미(still)·슬래그 귀뚜라미는 시간 무관.
            R("cricket_field",             Nocturnal, None,          Snow);
            R("cricket_tree",              Nocturnal, None,          Snow);
            R("cricket_stone",             Nocturnal, Snow,          None);
            R("cricket_tomb",              Nocturnal, Fog,           None);
            R("cricket_hush",              Nocturnal, Fog,           None);
            R("cricket_dune",              Nocturnal, Clear,         None);
            R("cricket_frost",             Nocturnal, Snow,          None);
            R("cricket_ember",             Nocturnal, Clear,         None);
            R("cricket_still",             Any,       Fog,           None);
            R("cricket_slag",              Any,       Clear,         None);

            // ── 메뚜기·여치 ──
            // 메뚜기: 주행성 · 맑음 ↑(일광욕) · 안개·눈 ↓. 사막메뚜기는 센바람 ↑(떼가 바람을 탄다), 바위메뚜기는 눈 ↑.
            // 여치(katydid): 야행성 · 비 ↑(잎이 젖는다) · 눈 ↓. 우듬지 여치는 안개 ↑, 설원 여치는 눈 ↑.
            R("grasshopper_green",         Diurnal,   Clear,         Fog | Snow);
            R("grasshopper_brown",         Diurnal,   Clear,         Fog | Snow);
            R("grasshopper_rock",          Diurnal,   Clear | Snow,  None);
            R("grasshopper_locust",        Diurnal,   Clear | Wind,  None);
            R("katydid_leaf",              Nocturnal, Rain,          Snow);
            R("katydid_canopy",            Nocturnal, Rain | Fog,    None);
            R("katydid_snowfield",         Nocturnal, Snow,          None);

            // ── 진딧물·애벌레·개미 ──
            // 진딧물: 시간·날씨 무관(정원 진딧물은 바람 ↑, 우듬지 진딧물은 안개 ↑). 비에 씻기는 쪽으로 정했다가 뺐다 —
            // 정원 풀 17종 가운데 비에 불리한 종이 절반을 넘었다. 애벌레: 시간 무관 · 비 ↑(잎이 자란다), 소나무 애벌레는 눈 ↑. 개미: 주행성 · 맑음 ↑ · 비 ↓.
            R("ant_soldier",               Diurnal,   Clear,         Rain);
            R("caterpillar_green",         Any,       Rain,          None);
            R("caterpillar_pine",          Any,       Snow,          None);
            R("caterpillar_silk",          Any,       Rain,          None);
            R("aphid_colony",              Any,       None,          None);
            R("aphid_rose",                Any,       Wind,          None);
            R("aphid_canopy",              Any,       Fog,           None);

            // ── 개미귀신 ──
            // 야행성(성충) · 맑음 ↑(마른 모래여야 깔때기가 선다) · 비 ↓(함정이 무너진다).
            R("antlion_pit",               Nocturnal, Clear,         Rain);
            R("antlion_dune",              Nocturnal, Clear,         Rain);

            // ── 지네·집게벌레·쥐며느리 ──
            // 지네·집게벌레: 야행성 · 비·안개 ↑(습기) · 맑음 ↓(마름). 유적 지네(ruin)는 안개만 ↑, 사막·잿불 지네는 열을 견뎌 맑음 ↓가 없다.
            // 쥐며느리: 시간 무관 · 비·안개 ↑(습기). 사막 종은 무관, 바위·설산 종은 눈 ↑, 재 쥐며느리는 열 ↑.
            R("centipede_common",          Nocturnal, Rain | Fog,    Clear, Ambusher);
            R("centipede_red",             Nocturnal, Rain | Fog,    Clear, Ambusher);
            R("centipede_venom",           Nocturnal, Rain | Fog,    Clear, Ambusher);
            R("centipede_ruin",            Nocturnal, Fog,           Clear);
            R("centipede_sand",            Nocturnal, Fog,           None, Ambusher);
            R("centipede_ember",           Nocturnal, Clear,         None);
            R("centipede_pale",            Nocturnal, Fog,           None);
            R("centipede_frost",           Nocturnal, Snow,          None);
            R("pill_bug_garden",           Any,       Rain | Fog,    None);
            R("pill_bug_mud",              Any,       Rain | Fog,    None);
            R("pill_bug_rock",             Any,       Snow,          None);
            R("pill_bug_desert",           Any,       None,          None);
            R("pill_bug_frost",            Any,       Snow,          None);
            R("pill_bug_cinder",           Any,       Clear,         None);
            R("earwig_common",             Nocturnal, Rain | Fog,    Clear);
            R("earwig_swamp",              Nocturnal, Rain | Fog,    Clear);

            // ── 거미 ──
            // 야행성(시간 무관인 종도 있다) · 안개 ↑(이슬 맺힌 거미줄) · 센바람 ↓(줄이 찢긴다). 습지 거미는 비도 ↑, 낙타거미는 맑음 ↑(사막).
            // 정원 거미(garden)는 바람 속성이라 센바람 ↑. 황금무당거미는 주행성 — 햇살에 줄이 반짝인다. 설산 거미는 눈 ↑.
            R("spider_garden",             Any,       Fog | Wind,    None);
            R("spider_golden_orb",         Diurnal,   Clear,         Wind);
            R("spider_marsh",              Nocturnal, Rain | Fog,    Wind, Ambusher);
            R("spider_bog_widow",          Nocturnal, Rain | Fog,    Wind, Ambusher);
            R("spider_cliff",              Any,       Fog,           None);
            R("spider_tomb",               Any,       Fog,           None);
            R("spider_threadbare",         Any,       Fog,           Wind);
            R("spider_camel",              Nocturnal, Clear,         None, Ambusher);
            R("spider_frost",              Any,       Snow,          None);
            R("spider_blank",              Any,       Fog,           None);
            R("gacha_ice_spider",          Nocturnal, Rain | Snow,   None);

            // ── 사마귀 ──
            // 매복 포식자 — 안개를 엄폐로 쓴다(안개 ↑). 꽃·잎에 숨는 종(green·orchid·canopy)과 사원·잿불 종은 주행성.
            // 썩은잎·지워진 땅·설산 사마귀는 시간 무관. 유령사마귀는 눈 ↓, 흑요석 사마귀는 맑음 ↓(어둠 속 사냥꾼).
            R("mantis_green",              Diurnal,   Clear,         None, Ambusher);
            R("mantis_ghost",              Nocturnal, Fog,           Snow, Ambusher);
            R("mantis_orchid",             Diurnal,   Clear,         None);
            R("mantis_bark",               Nocturnal, Fog,           None, Ambusher);
            R("mantis_swamp",              Nocturnal, Rain | Fog,    None, Ambusher);
            R("mantis_obsidian",           Nocturnal, Fog,           Clear, Ambusher);
            R("mantis_dead_leaf",          Any,       Fog,           None);
            R("mantis_mist",               Nocturnal, Fog,           None, Ambusher);
            R("mantis_gold_temple",        Diurnal,   Clear,         None, Ambusher);
            R("mantis_hollow",             Any,       Fog,           None);
            R("mantis_icicle",             Any,       Snow,          None);
            R("mantis_ember",              Diurnal,   Clear,         None);
            R("mantis_canopy",             Diurnal,   Fog,           None);
            R("mantis_blank",              Any,       Fog,           None);
            R("mantis_unnamed",            Nocturnal, Fog,           None, Ambusher);
            R("gacha_shadow_mantis",       Nocturnal, Fog,           None);

            // ── 사슴벌레·장수풍뎅이 ──
            // 야행성(수액 채집·싸움) · 안개 ↑. 작은 종은 눈 ↓, 큰 종(titan·hercules·golden)은 눈을 견딘다. 설산·고산 사슴벌레는 눈 ↑.
            R("stag_beetle",               Nocturnal, Fog,           None);
            R("stag_beetle_saw",           Nocturnal, Fog,           Snow);
            R("stag_beetle_mountain",      Nocturnal, Snow,          None);
            R("stag_beetle_iron",          Nocturnal, Snow,          None);
            R("stag_beetle_glacier",       Nocturnal, Snow,          None);
            R("rhinoceros_beetle",         Nocturnal, Fog,           Snow);
            R("rhinoceros_beetle_titan",   Nocturnal, Fog,           None, Ambusher);
            R("beetle_hercules",           Nocturnal, Fog,           None, Ambusher);
            R("beetle_golden_stag",        Nocturnal, Fog,           None);

            // ── 비단벌레·스카라베·기타 딱정벌레 ──
            // 비단벌레·스카라베·모래·잿불 딱정벌레: 주행성 · 맑음 ↑(광택·열). 비단벌레는 안개 ↓.
            // 풍뎅이·하늘소·소똥구리: 대부분 시간 무관 — 무늬만 다른 중립이다. 설원 딱정벌레는 눈 ↑, 우듬지·소똥 딱정벌레는 비 ↑, 장수하늘소 로살리아는 주행성.
            R("beetle_basic",              Any,       None,          None);
            R("beetle_dung",               Any,       Rain,          None);
            R("beetle_click",              Nocturnal, None,          None);
            R("beetle_longhorn_rosalia",   Diurnal,   Clear,         None);
            R("beetle_longhorn_oak",       Any,       None,          None);
            R("beetle_longhorn_alpine",    Any,       Snow,          None);
            R("beetle_husk",               Any,       Fog,           None);
            R("beetle_sand",               Diurnal,   Clear,         None);
            R("beetle_rime",               Any,       Snow,          None);
            R("beetle_cinder",             Any,       Clear,         None);
            R("beetle_longhorn_char",      Any,       Clear,         None);
            R("beetle_unwritten",          Any,       Fog,           None);
            R("beetle_hoarfrost",          Any,       Snow,          None);
            R("beetle_scorch",             Diurnal,   Clear,         None);
            R("beetle_bark_canopy",        Any,       Rain,          None);
            R("longhorn_beetle",           Any,       None,          None);
            R("scarab_ancient",            Any,       None,          None);
            R("scarab_relic",              Diurnal,   Clear,         None);
            R("scarab_pharaoh",            Diurnal,   Clear,         None);
            R("scarab_sand",               Diurnal,   Clear,         None);
            R("jewel_beetle_gold",         Diurnal,   Clear,         Fog);
            R("jewel_beetle_azure",        Diurnal,   Clear,         Fog);
            R("gacha_diamond_beetle",      Diurnal,   Clear,         None);
            R("gacha_celestial_beetle",    Nocturnal, Clear,         None);

            // ── 물방개·소금쟁이 ──
            // 물방개: 시간 무관 · 비 ↑(수면 활동) · 눈 ↓(수면이 언다). 소금쟁이: 주행성 · 비 ↑ · 눈·센바람 ↓(수면 파문).
            R("water_strider_pond",        Diurnal,   Rain,          Wind | Snow);
            R("water_strider_stream",      Diurnal,   Rain,          Wind | Snow);
            R("diving_beetle_deep",        Any,       Rain,          Snow);
            R("diving_beetle_small",       Any,       Rain,          Snow);
            R("diving_beetle_striped",     Any,       Rain,          Snow);
            R("diving_beetle_great",       Any,       Rain,          Snow);
            R("diving_beetle_king",        Any,       Rain,          Snow);

            // ── 대벌레·잎벌레 ──
            // 야행성 · 안개 ↑(잎 위 습기). 우듬지 대벌레는 비도 ↑. 긴 대벌레는 눈 ↓.
            R("stick_insect_long",         Nocturnal, Fog,           Snow);
            R("stick_insect_canopy",       Nocturnal, Rain | Fog,    None);
            R("leaf_insect_phantom",       Nocturnal, Fog,           None);

            authoredRows = lines;
            return t;
        }
    }
}
