using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 월드 소품(지형·건물·식생) 머티리얼의 생성·표면 마감·반투명 전환 — 단일 출처.
    ///
    /// 빌더 다섯 곳(<c>PlaySceneBootstrap</c>·<c>RegionTerrainBuilder</c>·<c>WorldTerrainBuilder</c>·
    /// <c>VillageBuilder</c>·<c>SubAreaWorldBuilder</c>)이 각자 <c>new Material(Standard)</c>를 만들고
    /// 색만 넣었다. Standard의 기본 광택이 0.5라 **바위·흙·나무가 전부 플라스틱처럼 반짝였다**
    /// (배치 캡처에서 산의 바위가 크롬 달걀로 찍혔다). 캐릭터는 <see cref="CharacterPalette.ApplySurface"/>로
    /// 이미 부위별 재질을 받으므로 여기서 다루지 않는다.
    ///
    /// 셰이더 폴백 체인과 Fade 전환도 빌더마다 사본이 있었다(체인 5벌, 반투명 4벌). 사본끼리 한 줄씩 어긋나기
    /// 시작하면 같은 색이 빌더마다 다르게 그려지므로 여기로 모았다.
    ///
    /// **반투명(알파 &lt; 1)은 마감을 건드리지 않는다** — 물·얼음·유리는 반사가 제 질감이다.
    /// </summary>
    public static class SceneryMaterials
    {
        /// <summary>흙·돌·나무·천의 광택. 0이면 완전 무광이라 형태 음영이 죽는다.</summary>
        public const float MatteGloss = 0.12f;

        /// <summary>
        /// 폴백 체인이 고른 셰이더. <b>셰이더는 에셋이라 씬 재로드에 파괴되지 않는다</b> — 정적 참조는
        /// <c>Resources.UnloadUnusedAssets</c>의 도달성 검사에도 잡혀 내려가지 않는다. 그래도 Unity의 <c>== null</c>
        /// (파괴 검사)로 다시 찾게 해 두어 어떤 경로로든 파괴된 객체를 쥔 채 쓰지 않는다.
        /// </summary>
        private static Shader litShader;

        /// <summary>
        /// Standard → URP Lit → Unlit/Color → Sprites/Default → Hidden/InternalErrorShader.
        /// 처음 한 번만 찾는다 — 빌더들이 머티리얼마다 <c>Shader.Find</c>를 네 번씩 불렀다(서브에리어는 진입마다 수십 번).
        /// </summary>
        internal static Shader LitShader
        {
            get
            {
                if (litShader != null) return litShader;
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
                litShader = shader;
                return shader;
            }
        }

        /// <summary>
        /// 플레이어 빌드에 <b>변형이 남아 있어야 하는</b> Standard 키워드 조합 — 런타임에 켜는 조합 전부다
        /// (<see cref="MakeFade"/> · <see cref="SetEmission"/> · 둘 다). 둘 다 Standard의 <c>shader_feature</c>라 빌드에 든
        /// 머티리얼이 쓰는 조합만 컴파일되는데, 이 저장소엔 .mat이 없어 반투명·발광 변형이 빌드에서 통째로 빠졌다 —
        /// 없는 변형은 불투명·무발광 변형으로 그려진다. 에디터 도구 <c>ShaderVariantKeepers</c>가 이 표대로
        /// <c>Resources/</c><see cref="BuildKeeperFolder"/>에 머티리얼을 두어 빌드가 그 조합을 남기게 한다.
        /// 새 조합을 런타임에 켜면 여기에 한 줄 — <c>ShaderVariantKeeperTests</c>가 빠진 조합을 잡는다.
        ///
        /// <b>Standard를 Always Included Shaders에 다시 넣지 말 것.</b> 거기 든 셰이더는 머티리얼의 키워드 사용을 보지 않고
        /// shader_feature를 전부 끈 변형만 남긴다 — 이 머티리얼들을 Resources에 두고도 빌드 변형 수가 그대로였다(2026-09-29
        /// QA 빌드 실측: FORWARD 16 → 목록에서 빼자 64, 반투명 32·발광 32). Standard를 빌드에 싣는 일(<see cref="LitShader"/>의
        /// <c>Shader.Find</c>가 찾게 하는 일)은 키워드 없는 <c>StandardOpaque</c>가 대신한다.
        /// </summary>
        public static readonly (string name, string[] keywords)[] BuildKeepers =
        {
            ("StandardOpaque", new string[0]),   // 키워드 없는 기본 — Standard 자체를 빌드에 싣는 것도 이 머티리얼이다(아래)
            ("StandardFade", new[] { "_ALPHABLEND_ON" }),
            ("StandardEmission", new[] { "_EMISSION" }),
            ("StandardFadeEmission", new[] { "_ALPHABLEND_ON", "_EMISSION" }),
        };

        /// <summary><see cref="BuildKeepers"/> 머티리얼이 놓이는 Resources 하위 폴더.</summary>
        public const string BuildKeeperFolder = "ShaderVariantKeepers";

        /// <summary>반투명으로 다룰 색인가 — <see cref="ApplyFinish"/>가 건너뛰고 <see cref="MakeFade"/>가 필요한 쪽.</summary>
        public static bool IsTranslucent(Color color) => color.a < 0.999f;

        public static void ApplyFinish(Material mat, Color color)
        {
            if (mat == null || IsTranslucent(color)) return;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", MatteGloss);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", MatteGloss);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        }

        /// <summary>
        /// 스스로 은은히 빛나게 한다(Standard의 _EMISSION). 구름처럼 아랫면이 환경광만 받아
        /// 칙칙해지는 것을 들어 올릴 때 쓴다. 폴백 셰이더에는 조용히 무시된다.
        /// </summary>
        public static void SetEmission(Material mat, Color emission)
        {
            if (mat == null || !mat.HasProperty("_EmissionColor")) return;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        /// <summary>
        /// Standard를 Fade 모드로 돌린다 — Unity 표준 머티리얼 인스펙터가 하는 것과 같은 설정이다.
        /// 빌드에서 이 변형이 살아 있는 건 <see cref="BuildKeepers"/> 덕이다.
        ///
        /// Standard는 기본이 Opaque라 <c>mat.color</c>에 알파를 넣어도 <b>무시된다</b> — 블렌드·ZWrite·렌더 큐·
        /// 키워드를 함께 세워야 실제로 비친다. 프로퍼티가 없는 폴백 셰이더(Unlit/Color 등)에서는 <c>HasProperty</c>
        /// 가드가 조용히 넘어간다.
        ///
        /// 옛 사본 넷 중 지형 빌더 둘(<c>WorldTerrainBuilder</c>·<c>SubAreaWorldBuilder</c>의 <c>SetTransparent</c>)은
        /// <c>_Mode</c>를 3(Transparent)으로 적었지만 블렌드는 이것과 같은 SrcAlpha/OneMinusSrcAlpha(=Fade)였다.
        /// <c>_Mode</c>는 셰이더 패스가 읽지 않는 인스펙터 전용 값이라 그리는 결과는 같다 — 실제 블렌드와 맞는 2로 통일했다.
        /// </summary>
        public static void MakeFade(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 2f);   // 2 = Fade
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>Standard → URP Lit → Unlit 폴백으로 머티리얼을 만들고 마감까지 입힌다.</summary>
        public static Material Create(Color color)
        {
            Material mat = new Material(LitShader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            ApplyFinish(mat, color);
            return mat;
        }
    }

    /// <summary>
    /// 색별 머티리얼 캐시의 키 — <b>색 + 표면(발광색·젖은 광택)</b>.
    ///
    /// 옛 캐시는 색만 키로 썼다. 같은 색이 한 배처에선 발광, 다른 배처에선 광택(또는 팔레트가 넘친 무광)으로
    /// 지정되면 <b>먼저 만든 쪽 머티리얼로 합쳐져</b> 한쪽이 엉뚱하게 빛나거나 빛을 잃는다 — 예외도 경고도 없다.
    /// 2026-09-29 기준 겹치는 색이 없어 무증상이었다. 같은 표면끼리는 여전히 하나로 모인다(드로우콜·머티리얼 수 그대로).
    /// </summary>
    public readonly struct SceneryMaterialKey : System.IEquatable<SceneryMaterialKey>
    {
        public Color Albedo { get; }
        public bool Glow { get; }
        /// <summary><see cref="Glow"/>가 아니면 항상 <c>default</c> — 발광 없는 키끼리 쓰레기 값으로 갈라지지 않게.</summary>
        public Color Emission { get; }
        public bool Wet { get; }

        public SceneryMaterialKey(Color albedo, bool glow, Color emission, bool wet)
        {
            Albedo = albedo;
            Glow = glow;
            Emission = glow ? emission : default;
            Wet = wet;
        }

        /// <summary>발광·광택 없는 기본 마감(무광).</summary>
        public static SceneryMaterialKey Matte(Color albedo) => new SceneryMaterialKey(albedo, false, default, false);

        public static SceneryMaterialKey Glowing(Color albedo, Color emission) => new SceneryMaterialKey(albedo, true, emission, false);

        // Color.Equals는 성분별 정확 비교다(== 연산자는 근사 비교) — 옛 Dictionary<Color, …>와 같은 판정을 유지한다.
        public bool Equals(SceneryMaterialKey other) =>
            Albedo.Equals(other.Albedo) && Glow == other.Glow && Emission.Equals(other.Emission) && Wet == other.Wet;

        public override bool Equals(object obj) => obj is SceneryMaterialKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Albedo.GetHashCode();
                h = h * 397 ^ Emission.GetHashCode();
                h = h * 397 ^ (Glow ? 1 : 0);
                h = h * 397 ^ (Wet ? 2 : 0);
                return h;
            }
        }

        public override string ToString() => $"{Albedo} glow={Glow} {Emission} wet={Wet}";
    }
}
