using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 3D 플레이어 캐릭터의 프로시저럴 생성/외형 갱신 담당. 필드의 실제 플레이어와
    /// 의상 미리보기용 마네킹(<see cref="BuildForPreview"/>)이 같은 코드로 지어진다.
    /// - 치비 ~3.5등신 비례
    /// - CharacterOutfitManager.OutfitChanged 구독 → ApplyToCharacter가 색·형태 갱신 (재생성 X)
    /// 노드 이름은 <b>암묵 계약</b>이라 변경하지 말 것 — 다섯 곳이 문자열로 찾는다:
    /// PlayerMovement(transform.Find), CharacterOutfitManager.ApplyToCharacter(),
    /// OutfitShapeLibrary의 bind/hideNodes, NpcWalkAnimator(같은 이름을 NPC에 복제),
    /// CharacterModelPreviewRenderer.FocusNodesFor. 틀리면 예외 없이 그냥 동작하지 않는다.
    /// Body/Shirt/Neck/Head/HeadPivot/ArmL/ArmR/HandL/HandR/
    /// LegLPivot/LegRPivot/LegL/LegR/BootL/BootR/
    /// Backpack/BackpackStrap/NetHandle/NetRing/Cap/CapBrim/HatRoot.
    /// </summary>
    public class PlayerVisualBuilder : MonoBehaviour
    {
        // ── 비례 ──
        // 치비 3.4~3.5등신: 발바닥 ≈ 0.045m, 정수리 ≈ 1.45m → 전고 ≈ 1.41m, 머리 높이 0.408m.
        // 2D 포트레이트(CharacterPortraitRenderer)도 같은 톤(≈3.3등신)으로 맞춰져 있다.
        // 머리 확대는 headPivotScale로 한다 — 얼굴 부품(눈/코/입/모자)이 headPivot 자식이라
        // 함께 스케일되어 배치가 자동 유지된다(부품 재배치 불필요).
        //
        // 참고: 이 아래 메서드 본문의 좌표 주석 일부는 옛 6.4등신 시절 값이 남아 있다
        // (예: "위치 1.40 유지", "어깨 ±0.29 / Y 1.40"). 실제 값은 코드가 맞다.

        /// <summary>
        /// <see cref="MakeMaterial"/>가 만든 <b>모든</b> 런타임 머티리얼. 정리의 단일 출처다.
        ///
        /// 예전엔 아래 슬롯 필드 11개만 <c>OnDestroy</c>에 손으로 나열했는데, 얼굴·머리 장식이
        /// 만드는 6~9개(눈·동공·하이라이트·눈썹·코·입 + 여성 홍조·속눈썹 + 올림머리 리본)는
        /// <b>지역 변수라 목록에 오르지 못했다</b> — 마네킹을 다시 지을 때마다 그만큼씩 샜다.
        /// 생성 지점이 <c>MakeMaterial</c> 하나뿐이므로 거기서 등록하면 호출 20여 곳을
        /// 건드리지 않고 전부 덮인다(<c>BattleArenaController.runtimeMaterials</c>와 같은 형태).
        /// </summary>
        private readonly List<Material> runtimeMaterials = new List<Material>();

        // ── 슬롯별 머티리얼 참조 (BuildAll이 여러 노드에 같은 것을 물릴 때 씀) ──
        private Material hatMat;       // Cap + CapBrim
        private Material topMat;       // Shirt
        private Material bottomMat;    // LegL + LegR
        private Material outerwearMat; // Body + ArmL + ArmR (자켓 외피)
        private Material shoesMat;     // BootL + BootR
        private Material backpackMat;  // Backpack 본체
        private Material backpackStrapMat; // Backpack 어깨끈 (어둡게)
        private Material toolMat;      // NetHandle
        private Material toolRingMat;  // NetRing
        private Material skinMat;      // Neck/Head/Hand/Ear/Nose 등 피부 노출 부위
        private Material hairMat;      // Hair (PlayerPrefs HairColor)

        // ── 노드 참조 (SetActive on/off에 사용) ──
        private GameObject hatRoot;
        private GameObject shirtRoot;
        private GameObject outerwearArmL;
        private GameObject outerwearArmR;
        private GameObject backpackRoot;
        private GameObject backpackStrap;
        private GameObject toolHandle;
        private GameObject toolRing;

        private bool subscribedToOutfit;
        private bool builtOnce;
        private bool previewMode;
        private AppearanceSpec look;

        /// <summary>
        /// 이 캐릭터가 실제로 쓰는 피부색. <c>CharacterOutfitManager.ApplyToCharacter</c>가
        /// outer_none(겉옷 벗음)일 때 팔을 이 색으로 되돌리는 데 쓴다.
        ///
        /// 그쪽이 자기 상수를 들고 있으면 생성 화면에서 어두운 피부를 골랐을 때
        /// <b>팔만 밝은 살색으로 남는다</b> — 소유자를 여기 하나로 둔다.
        /// 마네킹(previewMode)도 자기 spec을 들고 있어 프리뷰까지 자동으로 맞는다.
        /// </summary>
        public Color SkinTone => look.SkinTone;

        private void Awake()
        {
            // 프리뷰 마네킹은 비활성 상태에서 BuildForPreview로 이미 지어진 뒤 활성화된다 —
            // 그때 뒤늦게 발화하는 Awake가 두 번째 몸을 짓지 않게 막는다.
            if (builtOnce) return;
            look = AppearanceSpec.FromPlayerPrefs();
            BuildAll();
            builtOnce = true;
        }

        /// <summary>
        /// 의상 미리보기용 마네킹을 짓는다. <b>GameObject가 비활성일 때 호출할 것</b> —
        /// AddComponent는 활성 오브젝트면 즉시 Awake를 발화시켜 PlayerPrefs 외형으로 먼저 지어버린다.
        ///
        /// <code>
        /// GameObject go = new GameObject("OutfitMannequin");
        /// go.SetActive(false);                                    // ← Awake 억제
        /// go.AddComponent&lt;PlayerVisualBuilder&gt;().BuildForPreview(spec);
        /// go.SetActive(true);                                     // Awake는 builtOnce로 스킵
        /// </code>
        /// </summary>
        public void BuildForPreview(AppearanceSpec spec)
        {
            if (builtOnce) return;
            previewMode = true;
            look = spec;
            BuildAll();
            builtOnce = true;
        }

        private void OnEnable()
        {
            // 마네킹은 OutfitChanged를 구독하면 안 된다 — 그 핸들러가 mgr.ApplyToCharacter()를 부르고,
            // 그 안의 GameObject.Find("Player")가 실제 플레이어를 찾아 옷을 다시 입힌다.
            // 마네킹을 하나 만들 때마다 씬 전체 스캔 + 실플레이어 갱신이 도는 조용한 버그가 된다.
            if (previewMode) return;
            TrySubscribeOutfitChanged();
        }

        private void Start()
        {
            if (previewMode) return;
            // OutfitManager가 Awake 순서상 늦게 살아있을 수 있어 Start에서도 1회 시도
            TrySubscribeOutfitChanged();
            // 초기 외형 동기화 (저장된 장착 반영)
            RefreshOutfitColors();
        }

        private void OnDisable()
        {
            if (subscribedToOutfit && CharacterOutfitManager.Instance != null)
            {
                CharacterOutfitManager.Instance.OutfitChanged -= RefreshOutfitColors;
            }
            subscribedToOutfit = false;
        }

        private void OnDestroy()
        {
            // Unity의 MeshRenderer.material = X (setter)는 인스턴스화 없이 sharedMaterial 직접 할당.
            // 즉 Body.sharedMaterial == outerwearMat (동일 객체). ApplyToCharacter가 renderer.material
            // (getter)로 인스턴스화하기 전까지는 캐시가 그대로 sharedMaterial.
            // 만약 ApplyToCharacter가 호출 안 된 상태에서 캐시를 Destroy하면 sharedMaterial이 파괴된
            // 머티리얼을 가리켜 캐릭터가 검은색/분홍색으로 렌더링됨 (회귀 보고됨).
            // → 안전한 destroy: 자식 노드 중 이 머티리얼을 sharedMaterial로 쓰는 게 있으면 스킵.
            //   (player가 영구 객체라 누수 영향 실질적 0)
            //
            // 마네킹은 파괴가 정상 수명이다 — 여기서 강제로 지우지 않으면 다시 지을 때마다
            // 머티리얼 17~20개(성별·머리 스타일에 따라)가 영구히 샌다.
            // 조회는 목록 전체에 대해 **한 번만** 한다(예전엔 필드마다 11번 돌았다).
            // 이 계층은 지금 통째로 파괴되는 중이라 자식 렌더러가 무엇을 sharedMaterial로
            // 들고 있든 다시 그려질 일이 없다 — 그래서 무조건 파기한다.
            //
            // 예전엔 실플레이어일 때 "sharedMaterial로 아직 쓰이면 스킵"했는데, 그 조건이
            // 사실상 **항상** 참이었다: ApplyToCharacter가 색을 칠하지 않는 노드
            // (눈·동공·하이라이트·눈썹·입·홍조·속눈썹·머리·피부·리본 ≈ 10~12개)는 끝까지
            // sharedMaterial이라 하나도 안 지워졌다. "플레이어가 영구 객체"라는 전제도 틀렸다 —
            // DontDestroyOnLoad가 없어 로그아웃·계정삭제의 씬 재로드마다 그만큼씩 샌다
            // (b9a9771이 월드 빌더에서 고친 것과 같은 유형이다).
            //
            // 검정/마젠타 회귀는 **살아 있는** 캐릭터에서 캐시를 지웠을 때의 문제라
            // 이 시점에는 해당하지 않는다. NpcVisualBuilder.CleanupMaterials도 무조건 파기다.
            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                if (runtimeMaterials[i] != null) Destroy(runtimeMaterials[i]);
            }
            runtimeMaterials.Clear();

            // ApplyPartColor가 renderer.material(getter)로 만든 인스턴스는 위 목록에 없다 —
            // 소유자가 CharacterOutfitManager 쪽이라 여기서만 회수할 수 있다.
            // 마네킹은 외형이 바뀔 때마다 통째로 파괴·재생성되므로 그때마다 8~14개가 샜다.
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sharedMaterial != null)
                    Destroy(renderers[i].sharedMaterial);
            }

            // spawn 의상 파츠(OP_*)의 머티리얼은 **여기 목록에 없다** — 생성 지점이
            // OutfitShapeLibrary.CreatePartMaterial로 따로 있기 때문이다. 그쪽 TrimContainer는
            // 갈아입을 때 '남는' 파츠만 지우므로, 마지막까지 입고 있던 파츠는 루트가 통째로
            // 파괴되는 이 순간에 함께 지워야 한다(위 17~20개와 같은 이유).
            OutfitShapeLibrary.DestroySpawnedMaterials(transform);

            hatMat = topMat = bottomMat = outerwearMat = shoesMat = null;
            backpackMat = backpackStrapMat = toolMat = toolRingMat = null;
            skinMat = hairMat = null;
        }

        private void TrySubscribeOutfitChanged()
        {
            if (subscribedToOutfit) return;
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            if (mgr == null) return;
            mgr.OutfitChanged += RefreshOutfitColors;
            subscribedToOutfit = true;
        }

        // ── 프로시저럴 메시 헬퍼 ──────────────────────────────
        //
        // 내장 프리미티브와 **같은 규약**의 단위 메시를 쓴다(구체 지름 1, 캡슐 높이 2·반지름 0.5).
        // 그래야 기존 localPosition/localScale을 한 줄도 안 바꾸고 메시만 갈아끼울 수 있다 —
        // 이 파일의 좌표는 여러 차례 회귀를 거치며 맞춰진 값이라 건드릴수록 위험하다.
        //
        // 예외는 RoundedBox다. 둥근 모서리는 비균등 스케일에 왜곡되므로(0.21×0.15×0.30 부츠에
        // 스케일을 걸면 모서리 반경이 축마다 달라진다) 크기를 메시에 굽고 scale은 1로 둔다.

        /// <summary>지름 1 구체. 내장 Sphere(515정점)의 자리를 대신한다.</summary>
        private static Mesh UnitSphere(int rings, int segments)
        {
            return ProcMeshLibrary.LowSphere(0.5f, 0.5f, 0.5f, rings, segments);
        }

        /// <summary>
        /// 높이 2·반지름 0.5 캡슐. 내장 Capsule(552정점)과 같은 규약이다.
        /// <paramref name="taper"/>는 아래쪽 반지름 비율 — 1이면 원통, 0.7이면 어깨→손목처럼 가늘어진다.
        /// </summary>
        private static Mesh UnitCapsule(float taper)
        {
            float rTop = 0.5f;
            float rBottom = 0.5f * taper;
            return ProcMeshLibrary.TaperedCapsule(rTop, rBottom, 2f - rTop - rBottom, 8, 10);
        }

        /// <summary>
        /// 지름 1 원판(+Z를 향한다). 눈·동공·하이라이트·홍조가 쓰던 <b>눌린 구체</b>를 대신한다 —
        /// 그 8개가 캐릭터 정점의 40%였다. bulge는 z 스케일에 함께 눌리므로 넉넉히 잡아 둔다.
        /// </summary>
        private static Mesh UnitDisc(int segments)
        {
            return ProcMeshLibrary.Disc(0.5f, 0.5f, 0.4f, segments);
        }

        /// <summary>
        /// 커스텀 메시 노드 하나. <c>CreatePrimitive</c>가 아니라서 콜라이더가 생겼다 파괴되는
        /// 왕복이 없다 — 한 캐릭터에 그 왕복이 54번 있었다.
        /// </summary>
        private static GameObject Part(string name, Transform parent, Mesh mesh, Material mat,
            Vector3 pos, Vector3 scale)
        {
            GameObject go = ProcMeshLibrary.CreateNode(name, parent, mesh, mat, pos);
            go.transform.localScale = scale;
            return go;
        }

        /// <summary>회전이 있는 파츠용.</summary>
        private static GameObject Part(string name, Transform parent, Mesh mesh, Material mat,
            Vector3 pos, Vector3 scale, Vector3 euler)
        {
            GameObject go = ProcMeshLibrary.CreateNode(name, parent, mesh, mat, pos, Quaternion.Euler(euler));
            go.transform.localScale = scale;
            return go;
        }

        /// <summary>
        /// 크기를 메시에 구운 둥근 상자 파츠. <b>스케일을 걸지 않는다</b>(모서리 왜곡 방지).
        /// 몸통·셔츠·부츠·손처럼 90° 모서리가 "부품을 겹쳐놓은" 인상을 주던 자리에 쓴다.
        /// </summary>
        private static GameObject BoxPart(string name, Transform parent, Material mat,
            Vector3 pos, Vector3 size, float radius, int subdiv)
        {
            Mesh mesh = ProcMeshLibrary.RoundedBox(size, radius, subdiv);
            return ProcMeshLibrary.CreateNode(name, parent, mesh, mat, pos);
        }

        private static bool shaderDiagLogged;

        /// <summary>
        /// 이 클래스가 런타임 머티리얼을 만드는 <b>유일한 지점</b>. 만든 것은 전부
        /// <see cref="runtimeMaterials"/>에 등록돼 <c>OnDestroy</c>가 일괄 정리한다 —
        /// static이면 등록할 곳이 없어 인스턴스 메서드다.
        /// </summary>
        private Material MakeMaterial(Color color, SurfaceKind kind)
        {
            // Unity 6 + Built-in Pipeline 환경 가정. Standard 못 찾으면 URP/Unlit 순으로 fallback.
            // 최종 fallback도 실패하면 캐릭터가 검정/마젠타 → 진단 로그로 알림.
            Shader shader = Shader.Find("Standard");
            string usedName = "Standard";
            if (shader == null) { shader = Shader.Find("Universal Render Pipeline/Lit"); usedName = "URP/Lit"; }
            if (shader == null) { shader = Shader.Find("Unlit/Color"); usedName = "Unlit/Color"; }
            if (shader == null) { shader = Shader.Find("Sprites/Default"); usedName = "Sprites/Default"; }
            if (shader == null)
            {
                if (!shaderDiagLogged)
                {
                    Debug.LogError(
                        "[PlayerVisualBuilder] 모든 fallback shader를 찾을 수 없습니다 — 캐릭터가 검은색/마젠타로 렌더링됩니다. "
                        + "ProjectSettings → Graphics에서 Standard/URP shader가 Always Included Shaders에 포함되어 있는지 확인하세요.");
                    shaderDiagLogged = true;
                }
                shader = Shader.Find("Hidden/InternalErrorShader");
            }
            else if (!shaderDiagLogged && usedName != "Standard")
            {
                Debug.LogWarning($"[PlayerVisualBuilder] Standard shader 미발견, fallback={usedName} 사용. " +
                                 "mat.color가 적용 안 되는 shader면 캐릭터가 검은색으로 보일 수 있음.");
                shaderDiagLogged = true;
            }
            Material mat = new Material(shader);
            mat.color = color;
            // URP 환경 호환: _BaseColor property도 함께 설정 (Standard는 무시)
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            // 부위별 광택/금속감. 이게 없으면 피부·천·가죽·금속이 전부 Standard 기본값(0.5)으로
            // 렌더돼 한 덩어리 점토처럼 보인다 — kind를 인자로 강제하는 이유다(빠뜨리면 컴파일 실패).
            CharacterPalette.ApplySurface(mat, kind);
            runtimeMaterials.Add(mat);
            return mat;
        }

        private void BuildAll()
        {
            // ── 기본 색상 (의상 미장착 시) ──
            outerwearMat = MakeMaterial(new Color(0.2f, 0.4f, 0.85f), SurfaceKind.Cloth);      // 자켓
            topMat = MakeMaterial(new Color(0.98f, 0.96f, 0.92f), SurfaceKind.Cloth);          // 셔츠
            bottomMat = MakeMaterial(new Color(0.18f, 0.22f, 0.28f), SurfaceKind.Cloth);       // 바지
            // 피부색은 생성 화면이 고른 값(InsectGame.Character.SkinColor)을 따른다. 옛 하드코딩
            // (0.92,0.78,0.62)은 그 키를 읽지 않아 2D 초상화만 바뀌고 필드 캐릭터는 늘 같았다.
            skinMat = MakeMaterial(CharacterPalette.Skin(look.skinColor), SurfaceKind.Skin);
            hatMat = MakeMaterial(new Color(1.0f, 0.65f, 0.2f), SurfaceKind.Cloth);            // 모자
            shoesMat = MakeMaterial(new Color(0.2f, 0.12f, 0.06f), SurfaceKind.Leather);         // 부츠
            backpackMat = MakeMaterial(new Color(1.0f, 0.65f, 0.2f), SurfaceKind.Leather);       // 배낭
            backpackStrapMat = MakeMaterial(new Color(0.7f, 0.45f, 0.14f), SurfaceKind.Leather);
            toolMat = MakeMaterial(new Color(0.6f, 0.4f, 0.2f), SurfaceKind.Leather);            // 잠자리채 손잡이
            toolRingMat = MakeMaterial(new Color(0.95f, 0.92f, 0.88f), SurfaceKind.Metal);     // 잠자리채 망

            int gender = look.gender;

            // 미리보기(CharacterPortraitRenderer)는 직사각형 몸통 (bodyW=42, bodyH=62, X:Y≈0.68).
            // 옛 Capsule은 위아래 반구로 통처럼 둥글어 미리보기 대비 뚱뚱하게 보임 → Cube로 변경.
            // Cube scale: 어깨 너비 ≈ 0.45m, 높이 ≈ 0.7m, 깊이 ≈ 0.28m (슬림 사람 몸통).
            // 귀여운 치비(~3.5등신): 머리 크게(headPivotScale 0.42→0.60), 몸통 짧고 통통,
            // 팔다리 짧게, 목 최소화. 머리 확대는 headPivotScale로 — 얼굴 부품(눈/코/입/모자)이
            // headPivot 자식이라 함께 스케일되어 배치가 자동 유지됨(재배치 불필요).
            float bodyScaleX = gender == 1 ? 0.44f : 0.48f;
            float bodyScaleZ = gender == 1 ? 0.34f : 0.38f;
            float headScale = gender == 1 ? 0.74f : 0.72f;
            float legScale = gender == 1 ? 0.19f : 0.20f;
            const float headPivotScale = 0.60f;

            Transform t = transform;

            // ── 몸통 (자켓 외피) — Cube로 변경: 미리보기 직사각형 비례와 정합. ──
            // Y 0.7 → 0.78 (조금 길게). 위치 1.40 유지 (Y range 1.01~1.79).
            // 90° 모서리 Cube였다 — 벽돌을 얹은 것처럼 보이던 가장 큰 원인이다.
            // 둥근 상자는 크기를 메시에 굽고 스케일을 걸지 않는다(모서리 반경 왜곡 방지).
            BoxPart("Body", t, outerwearMat, new Vector3(0f, 0.77f, 0f),
                new Vector3(bodyScaleX, 0.46f, bodyScaleZ), 0.085f, 3);

            // ── 셔츠 (Top) — Body 안쪽 면적으로 살짝 작게, 자켓 열린 사이로 보이는 영역 ──
            // 셔츠는 자켓 사이로 <b>살짝</b> 보이는 가슴 패널이다. 옛 값(z 0.15 / 폭 0.34)은
            // 몸통 앞면(z 0.19)보다 0.06 앞으로 튀어나오고 폭도 몸통의 71%라, 흰 판이 앞을 통째로
            // 덮고 자켓은 양옆에만 남았다 — 측면에서 보면 판때기를 붙인 것처럼 보였다.
            // 좁히고 몸통 안으로 넣어 자켓이 앞을 덮게 한다.
            shirtRoot = BoxPart("Shirt", t, topMat, new Vector3(0f, 0.83f, 0.10f),
                new Vector3(0.24f, 0.36f, 0.20f), 0.05f, 2);

            // ── 목 ──
            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "Neck";
            neck.transform.SetParent(t, false);
            neck.transform.localPosition = new Vector3(0f, 1.00f, 0.02f);
            neck.transform.localScale = new Vector3(0.14f, 0.05f, 0.12f);
            neck.GetComponent<MeshRenderer>().material = skinMat;
            Object.Destroy(neck.GetComponent<Collider>());

            // ── 머리 ──
            GameObject headPivot = new GameObject("HeadPivot");
            headPivot.transform.SetParent(t, false);
            headPivot.transform.localPosition = new Vector3(0f, 1.22f, 0.03f);
            headPivot.transform.localScale = Vector3.one * headPivotScale;

            // 치비 둥근 머리: 옛 (−0.10, +0.12, −0.04) 달걀형(세로로 긺) → X/Y를 거의 균등하게.
            // Z는 −0.04 유지 — 얼굴 부품 z위치(0.32 등)가 머리 앞면에 그대로 얹히도록(재배치 회피).
            // 머리는 화면에서 가장 크게 보이므로 다른 부위보다 세그먼트를 넉넉히 준다.
            Part("Head", headPivot.transform, UnitSphere(10, 14), skinMat, Vector3.zero,
                new Vector3(headScale - 0.02f, headScale - 0.04f, headScale - 0.04f));

            // ── 모자 (Hat) — hatRoot 컨테이너로 묶어 SetActive(false)로 통째로 숨기기 가능 ──
            hatRoot = new GameObject("HatRoot");
            hatRoot.transform.SetParent(headPivot.transform, false);
            hatRoot.transform.localPosition = Vector3.zero;

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Cap";
            cap.transform.SetParent(hatRoot.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.3f, -0.02f);
            cap.transform.localScale = new Vector3(0.30f, 0.12f, 0.30f);
            cap.GetComponent<MeshRenderer>().material = hatMat;
            Object.Destroy(cap.GetComponent<Collider>());

            GameObject brim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brim.name = "CapBrim";
            brim.transform.SetParent(hatRoot.transform, false);
            brim.transform.localPosition = new Vector3(0f, 0.14f, 0.28f);
            brim.transform.localScale = new Vector3(0.28f, 0.03f, 0.14f);
            brim.GetComponent<MeshRenderer>().material = hatMat;
            Object.Destroy(brim.GetComponent<Collider>());

            // ── 눈/얼굴 (BuildPlayerVisual 원본 그대로) ──
            BuildFace(headPivot, headScale, gender);

            // ── 귀 ──
            BuildEars(headPivot, skinMat);

            // ── 머리카락 ──
            int hairStyle = look.hairStyle;
            int hairColorIdx = look.hairColor;
            hairMat = MakeMaterial(CharacterPalette.Hair(hairColorIdx), SurfaceKind.Hair);
            BuildHair(headPivot, hairStyle, gender, hairMat);

            // ── 팔 (어깨 ±0.29 / Y 1.40 / 캡슐 길이 0.50 / 회전 0°) ──
            // 옛 Y=1.55 + 길이 0.50 → 상단 1.80m로 Body 상단(1.79)과 일치하나 시각적으로 어깨가 여전히 높음.
            // Y 1.40 + 길이 0.50 → 캡슐 범위 1.15~1.65m로 Body(1.01~1.79) 중간 → 자연스러운 인체 비례.
            // 머리(2.20) 영역과 충분히 분리. X ±0.29 Body 가장자리 겹침, Z=0° 수직, swing은 X만.
            // 굵기가 일정한 캡슐이라 사지가 파이프처럼 보였다 — 어깨에서 손목으로 가늘어지게.
            Mesh armMesh = UnitCapsule(0.72f);
            outerwearArmL = Part("ArmL", t, armMesh, outerwearMat,
                new Vector3(-0.29f, 0.78f, 0f), new Vector3(0.135f, 0.23f, 0.135f));
            outerwearArmR = Part("ArmR", t, armMesh, outerwearMat,
                new Vector3(0.29f, 0.78f, 0f), new Vector3(0.135f, 0.23f, 0.135f));

            // ── 손 (팔 끝점: y = 1.40 - 0.25 = 1.15. 손목/손바닥 자연 매달림 = 0.95) ──
            // 구체 하나라 주먹이라기보다 공에 가까웠다. 손가락은 만들지 않는다 —
            // 손 지름이 0.115m라 치비 스케일에서 손가락은 1~2픽셀이다. 대신 세로로 길고
            // 앞뒤로 납작한 미튼(벙어리장갑) 형태가 같은 비용에 확실히 손처럼 보인다.
            Vector3 handSize = new Vector3(0.105f, 0.135f, 0.095f);
            BoxPart("HandL", t, skinMat, new Vector3(-0.29f, 0.52f, 0f), handSize, 0.042f, 2);
            BoxPart("HandR", t, skinMat, new Vector3(0.29f, 0.52f, 0f), handSize, 0.042f, 2);

            // ── 다리 + 부츠 (LegPivot으로 묶어 회전 시 발도 함께 움직이도록) ──
            // 옛은 BootL/R이 Player 직접 자식이라 PlayerMovement.AnimateWalk의 LegL/R 회전이 발에 전파 안 됨
            // → 사용자 보고 "다리는 움직이는데 발이 안 움직임". LegPivot(빈, scale 1) 안에 Leg+Boot 둘 다 자식으로
            // 배치하여 Pivot 회전 시 Leg+Boot 모두 자동 전파. Pivot Y=0.86 = 다리 최상단(허벅지 윗부분)으로
            // 회전 중심이 골반에 위치 → 자연스러운 보행. Leg/Boot scale 비균등이라 Pivot scale=1 유지 필수.
            // 치비: 짧고 통통한 다리 + 좁은 스탠스(X ±0.13). Pivot Y 0.48 = 골반(회전 중심).
            GameObject legLPivot = new GameObject("LegLPivot");
            legLPivot.transform.SetParent(t, false);
            legLPivot.transform.localPosition = new Vector3(-0.13f, 0.48f, 0f);

            Mesh legMesh = UnitCapsule(0.78f);   // 허벅지 → 발목
            Vector3 bootSize = new Vector3(0.21f, 0.15f, 0.30f);
            const float bootRadius = 0.052f;

            Part("LegL", legLPivot.transform, legMesh, bottomMat,
                new Vector3(0f, -0.14f, 0f), new Vector3(legScale, 0.20f, legScale));   // Pivot 아래 0.14m = 절대 Y 0.34
            BoxPart("BootL", legLPivot.transform, shoesMat,
                new Vector3(0f, -0.36f, 0.07f), bootSize, bootRadius, 2);   // 절대 Y 0.12, 발바닥 ~0.045

            GameObject legRPivot = new GameObject("LegRPivot");
            legRPivot.transform.SetParent(t, false);
            legRPivot.transform.localPosition = new Vector3(0.13f, 0.48f, 0f);

            Part("LegR", legRPivot.transform, legMesh, bottomMat,
                new Vector3(0f, -0.14f, 0f), new Vector3(legScale, 0.20f, legScale));
            BoxPart("BootR", legRPivot.transform, shoesMat,
                new Vector3(0f, -0.36f, 0.07f), bootSize, bootRadius, 2);

            // ── 배낭 (Backpack) ──
            backpackRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backpackRoot.name = "Backpack";
            backpackRoot.transform.SetParent(t, false);
            backpackRoot.transform.localPosition = new Vector3(0f, 0.80f, -0.22f);
            backpackRoot.transform.localScale = new Vector3(0.30f, 0.34f, 0.16f);
            backpackRoot.GetComponent<MeshRenderer>().material = backpackMat;
            Object.Destroy(backpackRoot.GetComponent<Collider>());

            backpackStrap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backpackStrap.name = "BackpackStrap";
            backpackStrap.transform.SetParent(backpackRoot.transform, false);
            backpackStrap.transform.localPosition = new Vector3(0f, 0.3f, 1.6f); // backpackRoot 자식, world Z 앞 방향
            backpackStrap.transform.localScale = new Vector3(0.9f, 0.18f, 0.2f);
            backpackStrap.GetComponent<MeshRenderer>().material = backpackStrapMat;
            Object.Destroy(backpackStrap.GetComponent<Collider>());

            // ── 도구 (NetHandle/NetRing) — OutfitShapeLibrary의 도구 레시피가 이름 의존(bind) ──
            // 기본 좌표 = 레시피 테이블의 기본 잠자리채(else 분기) 값과 일치시킴.
            // 옛 좌표(handle 0.55,1.62,-0.18 / ring 0.82,2.28,-0.1 머리높이·뒤쪽)는 stale 레거시로,
            // 레시피가 첫 프레임 전 덮어쓰지만 도구 미장착/early-return 시 머리 위 뒤에 망이
            // 둥둥 뜨는 잠재버그였음. 단일 출처화 + 안전망 이중 목적.
            toolHandle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            toolHandle.name = "NetHandle";
            toolHandle.transform.SetParent(t, false);
            toolHandle.transform.localPosition = new Vector3(0.29f, 0.74f, 0.02f);
            toolHandle.transform.localScale = new Vector3(0.04f, 0.40f, 0.04f);
            toolHandle.transform.localRotation = Quaternion.Euler(20f, 0f, -15f);
            toolHandle.GetComponent<MeshRenderer>().material = toolMat;
            Object.Destroy(toolHandle.GetComponent<Collider>());

            toolRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            toolRing.name = "NetRing";
            toolRing.transform.SetParent(t, false);
            toolRing.transform.localPosition = new Vector3(0.34f, 1.14f, 0.06f);
            toolRing.transform.localScale = new Vector3(0.20f, 0.02f, 0.20f);
            toolRing.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            toolRing.GetComponent<MeshRenderer>().material = toolRingMat;
            Object.Destroy(toolRing.GetComponent<Collider>());

            // 악세서리는 미리 만들지 않는다 — OutfitShapeLibrary의 레시피가 장착 시점에
            // 루트 아래 OP_Accessory 컨테이너로 필요한 파츠만 만든다.

            // 얼굴을 살아 있게 — 눈 깜빡임(과 필요하면 표정). 걷기와 직교하므로 별도 컴포넌트다.
            // 마네킹에도 붙인다: 의상 화면에서 정지한 인형보다 깜빡이는 쪽이 낫고,
            // 썸네일을 구울 때는 CharacterModelPreviewRenderer가 ResetToNeutral로 눈을 뜨게 한다.
            if (gameObject.GetComponent<CharacterFaceAnimator>() == null)
                gameObject.AddComponent<CharacterFaceAnimator>();

            LogVertexBudget();
        }

        /// <summary>
        /// 이 캐릭터가 실제로 쓰는 정점 수를 한 번 찍는다.
        ///
        /// 프로시저럴 메시로 옮기기 전에는 약 10,400정점이었다 — 그중 4,120이 얼굴 8노드의
        /// 눌린 구체였다. 예산을 넘으면 어느 부위가 다시 무거워졌는지 여기서 먼저 보인다.
        /// 에디터 전용이라 기기 빌드에는 들어가지 않는다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogVertexBudget()
        {
            const int Budget = 3500;

            int total = 0;
            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] != null && filters[i].sharedMesh != null)
                    total += filters[i].sharedMesh.vertexCount;
            }

            string label = previewMode ? "마네킹" : "플레이어";
            if (total > Budget)
            {
                Debug.LogWarning($"[PlayerVisualBuilder] {label} 정점 {total} — 예산 {Budget} 초과 " +
                                 $"(노드 {filters.Length}개). 어느 부위가 무거워졌는지 확인할 것.");
            }
            else
            {
                Debug.Log($"[PlayerVisualBuilder] {label} 정점 {total}/{Budget} (노드 {filters.Length}개)");
            }
        }

        private void BuildFace(GameObject headPivot, float headScale, int gender)
        {
            Material eyeMat = MakeMaterial(Color.white, SurfaceKind.Wet);
            Material pupilMat = MakeMaterial(new Color(0.12f, 0.08f, 0.05f), SurfaceKind.Wet);

            // 치비 큰 눈: 옛 0.11 → 0.15/0.17(세로로 큰 동그란 눈), 살짝 아래·바깥(귀여운 인상).
            // 눌린 구체(515정점)에서 원판(17정점)으로 — 이 얼굴 8노드가 캐릭터 정점의 40%였다.
            Mesh eyeMesh = UnitDisc(16);
            Part("EyeL", headPivot.transform, eyeMesh, eyeMat,
                new Vector3(-0.12f, -0.03f, 0.32f), new Vector3(0.15f, 0.17f, 0.06f));
            Part("EyeR", headPivot.transform, eyeMesh, eyeMat,
                new Vector3(0.12f, -0.03f, 0.32f), new Vector3(0.15f, 0.17f, 0.06f));

            Mesh pupilMesh = UnitDisc(12);
            Part("PupilL", headPivot.transform, pupilMesh, pupilMat,
                new Vector3(-0.12f, -0.04f, 0.35f), new Vector3(0.09f, 0.11f, 0.02f));
            Part("PupilR", headPivot.transform, pupilMesh, pupilMat,
                new Vector3(0.12f, -0.04f, 0.35f), new Vector3(0.09f, 0.11f, 0.02f));

            Material hlMat = MakeMaterial(new Color(1f, 1f, 1f, 0.9f), SurfaceKind.Wet);
            Mesh hlMesh = UnitDisc(8);
            Part("HighlightL", headPivot.transform, hlMesh, hlMat,
                new Vector3(-0.10f, 0.01f, 0.36f), new Vector3(0.045f, 0.045f, 0.01f));
            Part("HighlightR", headPivot.transform, hlMesh, hlMat,
                new Vector3(0.10f, 0.01f, 0.36f), new Vector3(0.045f, 0.045f, 0.01f));

            int faceType = look.faceType;
            Material browMat = MakeMaterial(new Color(0.2f, 0.15f, 0.1f), SurfaceKind.Hair);
            GameObject browL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            browL.name = "BrowL";
            browL.transform.SetParent(headPivot.transform, false);
            browL.transform.localPosition = new Vector3(-0.12f, 0.11f, 0.32f);
            browL.transform.localScale = new Vector3(0.09f, 0.016f, 0.02f);
            browL.GetComponent<MeshRenderer>().material = browMat;
            Object.Destroy(browL.GetComponent<Collider>());

            GameObject browR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            browR.name = "BrowR";
            browR.transform.SetParent(headPivot.transform, false);
            browR.transform.localPosition = new Vector3(0.12f, 0.11f, 0.32f);
            browR.transform.localScale = new Vector3(0.09f, 0.016f, 0.02f);
            browR.GetComponent<MeshRenderer>().material = browMat;
            Object.Destroy(browR.GetComponent<Collider>());

            // 코는 몸의 피부 머티리얼을 그대로 쓴다. 예전엔 여기서 같은 색으로 **하나 더** 만들면서
            // 이름까지 필드와 같아(지역 변수가 필드를 가림) 피부색을 한 곳에서 바꾸려 하면
            // 코만 옛 색으로 남는 함정이었다. BuildAll이 이 메서드보다 먼저 필드를 채운다.
            // 치비: 코는 작은 점으로 (큰 눈 강조). 위치도 눈 아래로 내림.
            Part("Nose", headPivot.transform, UnitSphere(4, 6), skinMat,
                new Vector3(0f, -0.10f, 0.35f), new Vector3(0.024f, 0.022f, 0.02f));

            Material mouthMat = MakeMaterial(new Color(0.8f, 0.4f, 0.35f), SurfaceKind.Skin);
            GameObject mouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mouth.name = "Mouth";
            mouth.transform.SetParent(headPivot.transform, false);
            float mouthWidth = 0.04f;
            float mouthY = -0.05f;
            switch (faceType)
            {
                case 0: mouthWidth = 0.04f; mouthY = -0.05f; break;
                case 1: mouthWidth = 0.05f; mouthY = -0.06f; break;
                case 2: mouthWidth = 0.03f; mouthY = -0.04f; break;
                case 3: mouthWidth = 0.02f; mouthY = -0.045f; break;
            }
            // 치비: 큰 눈/작은 코에 맞춰 입을 아래로 내려 균형 (옛 위치는 코와 겹쳐 답답).
            mouth.transform.localPosition = new Vector3(0f, mouthY - 0.08f, 0.32f);
            mouth.transform.localScale = new Vector3(mouthWidth, 0.015f, 0.015f);
            mouth.GetComponent<MeshRenderer>().material = mouthMat;
            Object.Destroy(mouth.GetComponent<Collider>());

            if (gender == 1)
            {
                Material blushMat = MakeMaterial(new Color(1f, 0.6f, 0.6f, 0.4f), SurfaceKind.Skin);
                Mesh blushMesh = UnitDisc(10);
                Part("BlushL", headPivot.transform, blushMesh, blushMat,
                    new Vector3(-0.16f, -0.10f, 0.30f), new Vector3(0.08f, 0.05f, 0.02f));
                Part("BlushR", headPivot.transform, blushMesh, blushMat,
                    new Vector3(0.16f, -0.10f, 0.30f), new Vector3(0.08f, 0.05f, 0.02f));

                Material lashMat = MakeMaterial(new Color(0.1f, 0.08f, 0.05f), SurfaceKind.Hair);
                GameObject lashL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lashL.name = "LashL";
                lashL.transform.SetParent(headPivot.transform, false);
                lashL.transform.localPosition = new Vector3(-0.12f, 0.07f, 0.31f);
                lashL.transform.localScale = new Vector3(0.09f, 0.006f, 0.01f);
                lashL.GetComponent<MeshRenderer>().material = lashMat;
                Object.Destroy(lashL.GetComponent<Collider>());

                GameObject lashR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lashR.name = "LashR";
                lashR.transform.SetParent(headPivot.transform, false);
                lashR.transform.localPosition = new Vector3(0.12f, 0.07f, 0.31f);
                lashR.transform.localScale = new Vector3(0.09f, 0.006f, 0.01f);
                lashR.GetComponent<MeshRenderer>().material = lashMat;
                Object.Destroy(lashR.GetComponent<Collider>());
            }
        }

        private void BuildEars(GameObject headPivot, Material skinMat)
        {
            Mesh earMesh = UnitSphere(5, 7);
            Part("EarL", headPivot.transform, earMesh, skinMat,
                new Vector3(-0.34f, -0.02f, 0f), new Vector3(0.05f, 0.08f, 0.06f));
            Part("EarR", headPivot.transform, earMesh, skinMat,
                new Vector3(0.34f, -0.02f, 0f), new Vector3(0.05f, 0.08f, 0.06f));
        }

        private void BuildHair(GameObject headPivot, int style, int gender, Material hairMat)
        {
            switch (style)
            {
                case 0: BuildShortHair(headPivot, gender, hairMat); break;
                case 1: BuildMediumHair(headPivot, gender, hairMat); break;
                case 2: BuildLongHair(headPivot, gender, hairMat); break;
                case 3: BuildUpHair(headPivot, gender, hairMat); break;
            }
        }

        /// <summary>
        /// 여성 앞머리. 스타일마다 두께가 다르다.
        ///
        /// 예전엔 이 블록이 <see cref="BuildShortHair"/> 안에만 있었는데, <see cref="BuildLongHair"/>가
        /// 그 메서드를 부른 뒤 <b>자기 앞머리를 또 만들었다</b> — 여성+긴머리에서 "HairBangs"라는
        /// 같은 이름의 Cube 둘(두께 0.08 / 0.10)이 같은 자리에 겹쳐 z-fighting이 났다.
        /// 생성 지점을 하나로 모으고 호출을 한 번씩만 두어 구조적으로 막는다.
        /// </summary>
        private void BuildBangs(GameObject headPivot, Material mat, float thickness)
        {
            GameObject bangs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bangs.name = "HairBangs";
            bangs.transform.SetParent(headPivot.transform, false);
            bangs.transform.localPosition = new Vector3(0f, 0.18f, 0.16f);
            bangs.transform.localScale = new Vector3(0.4f, thickness, 0.1f);
            bangs.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(bangs.GetComponent<Collider>());
        }

        /// <param name="withBangs">
        /// 긴머리는 자기 두께의 앞머리를 따로 만들므로 false로 부른다 — 앞머리 노드 중복 방지.
        /// </param>
        private void BuildShortHair(GameObject headPivot, int gender, Material mat, bool withBangs = true)
        {
            Mesh cap = UnitSphere(8, 12);      // 머리를 덮는 큰 덩어리
            Mesh tuft = UnitSphere(6, 8);      // 옆·뒤 작은 덩어리

            Part("HairTop", headPivot.transform, cap, mat,
                new Vector3(0f, 0.22f, -0.02f), new Vector3(0.62f, 0.34f, 0.60f));
            Part("HairSideL", headPivot.transform, tuft, mat,
                new Vector3(-0.2f, 0.05f, -0.02f), new Vector3(0.12f, 0.2f, 0.35f));
            Part("HairSideR", headPivot.transform, tuft, mat,
                new Vector3(0.2f, 0.05f, -0.02f), new Vector3(0.12f, 0.2f, 0.35f));
            Part("HairBack", headPivot.transform, tuft, mat,
                new Vector3(0f, 0.08f, -0.15f), new Vector3(0.45f, 0.28f, 0.2f));

            if (gender == 1 && withBangs) BuildBangs(headPivot, mat, 0.08f);
        }

        private void BuildMediumHair(GameObject headPivot, int gender, Material mat)
        {
            BuildShortHair(headPivot, gender, mat);

            Mesh strandMesh = UnitCapsule(0.85f);   // 끝으로 갈수록 살짝 가늘어지는 머리 다발

            Part("HairExtL", headPivot.transform, strandMesh, mat,
                new Vector3(-0.2f, -0.1f, -0.05f), new Vector3(0.1f, 0.18f, 0.12f));
            Part("HairExtR", headPivot.transform, strandMesh, mat,
                new Vector3(0.2f, -0.1f, -0.05f), new Vector3(0.1f, 0.18f, 0.12f));
            Part("HairBackExt", headPivot.transform, strandMesh, mat,
                new Vector3(0f, -0.08f, -0.18f), new Vector3(0.35f, 0.2f, 0.15f));
        }

        private void BuildLongHair(GameObject headPivot, int gender, Material mat)
        {
            // 앞머리는 아래에서 긴머리 두께(0.10)로 직접 만든다 — 여기서 받으면 둘이 겹친다.
            BuildShortHair(headPivot, gender, mat, withBangs: false);

            Mesh longStrandMesh = UnitCapsule(0.8f);

            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.12f;
                Part($"HairLong_{i}", headPivot.transform, longStrandMesh, mat,
                    new Vector3(x, -0.3f, -0.12f), new Vector3(0.12f, 0.35f, 0.1f));
            }

            Part("HairFrontL", headPivot.transform, longStrandMesh, mat,
                new Vector3(-0.18f, -0.12f, 0.1f), new Vector3(0.06f, 0.22f, 0.06f));
            Part("HairFrontR", headPivot.transform, longStrandMesh, mat,
                new Vector3(0.18f, -0.12f, 0.1f), new Vector3(0.06f, 0.22f, 0.06f));

            if (gender == 1)
            {
                BuildBangs(headPivot, mat, 0.10f);

                for (int i = 0; i < 2; i++)
                {
                    float x = (i == 0) ? -0.08f : 0.08f;
                    Part($"HairVeryLong_{i}", headPivot.transform, longStrandMesh, mat,
                        new Vector3(x, -0.55f, -0.12f), new Vector3(0.1f, 0.3f, 0.08f));
                }
            }
        }

        private void BuildUpHair(GameObject headPivot, int gender, Material mat)
        {
            Part("HairTop", headPivot.transform, UnitSphere(8, 12), mat,
                new Vector3(0f, 0.26f, -0.02f), new Vector3(0.55f, 0.30f, 0.52f));

            if (gender == 0)
            {
                // 스파이크는 끝이 뾰족할수록 좋다 — 테이퍼를 세게 준다.
                Mesh spikeMesh = UnitCapsule(0.35f);

                Part("HairSpike", headPivot.transform, spikeMesh, mat,
                    new Vector3(0f, 0.35f, 0.05f), new Vector3(0.15f, 0.18f, 0.12f),
                    new Vector3(-20f, 0f, 0f));

                for (int side = -1; side <= 1; side += 2)
                {
                    Part($"HairSideSpike_{(side > 0 ? "R" : "L")}", headPivot.transform, spikeMesh, mat,
                        new Vector3(side * 0.15f, 0.28f, 0f), new Vector3(0.08f, 0.12f, 0.08f),
                        new Vector3(0f, 0f, -side * 30f));
                }
            }
            else
            {
                Part("HairBun", headPivot.transform, UnitSphere(7, 10), mat,
                    new Vector3(0f, 0.15f, -0.22f), new Vector3(0.22f, 0.22f, 0.22f));

                Material ribbonMat = MakeMaterial(new Color(0.9f, 0.3f, 0.4f), SurfaceKind.Cloth);
                GameObject ribbon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ribbon.name = "HairRibbon";
                ribbon.transform.SetParent(headPivot.transform, false);
                ribbon.transform.localPosition = new Vector3(0f, 0.15f, -0.22f);
                ribbon.transform.localScale = new Vector3(0.24f, 0.02f, 0.24f);
                ribbon.GetComponent<MeshRenderer>().material = ribbonMat;
                Object.Destroy(ribbon.GetComponent<Collider>());

                BuildBangs(headPivot, mat, 0.08f);

                Mesh sideHairMesh = UnitCapsule(0.8f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part($"HairSide_{(side > 0 ? "R" : "L")}", headPivot.transform, sideHairMesh, mat,
                        new Vector3(side * 0.2f, -0.05f, 0.05f), new Vector3(0.06f, 0.15f, 0.06f));
                }
            }
        }

        // ──────────────────────────────────────────────
        //  OutfitChanged 핸들러: 8슬롯 전부를 ApplyToCharacter가 처리한다.
        //  GameObject 재생성 X (레시피의 spawn 파츠만 갈아끼워진다).
        // ──────────────────────────────────────────────
        public void RefreshOutfitColors()
        {
            // 마네킹은 실장착이 아니라 호출부가 지정한 로드아웃을 입는다(ApplyToCharacter 오버로드).
            if (previewMode) return;

            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            if (mgr == null) return;

            // 노드별 머티리얼 색 + 알파 0 시 SetActive(false) + 슬롯별 형태 레시피.
            // 게임 시작 시점에도 저장 의상 동기화.
            //
            // 악세서리도 여기 포함된다. 예전엔 ApplyToCharacter가 7슬롯만 다루고 Accessory는
            // 이 클래스가 미리 만든 4노드(AccGlassesL/R·AccNecklace·AccBadge) 중 하나를 켜는
            // 별도 경로였는데, 15종 중 8종이 else로 떨어져 날개·오라·후광이 전부 같은
            // 가슴팍 큐브로 보였다. 지금은 OutfitShapeLibrary가 형태의 단일 출처다.
            mgr.ApplyToCharacter();
        }
    }
}
