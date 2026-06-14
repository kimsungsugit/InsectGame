using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 필드 3D 플레이어 캐릭터의 프로시저럴 생성/외형 갱신 담당.
    /// - 6.4등신 슬림 비례 (CharacterPortraitRenderer와 일관)
    /// - CharacterOutfitManager.OutfitChanged 구독 → 머티리얼 색만 갱신 (재생성 X)
    /// - 8개 슬롯 시각 매핑 + Accessory 시각 노드 추가
    /// 노드 이름은 PlayerMovement.cs(transform.Find)와 CharacterOutfitManager.ApplyToCharacter()가
    /// 의존하므로 변경하지 말 것: Body/Shirt/ArmL/ArmR/HandL/HandR/LegL/LegR/BootL/BootR/
    /// Backpack/NetHandle/NetRing/Cap/CapBrim/HeadPivot.
    /// </summary>
    public class PlayerVisualBuilder : MonoBehaviour
    {
        // ── 슬림 비례 상수 (포트레이트 6.4등신 ↔ 3D 매핑) ──
        // CharacterPortraitRenderer: headW=24/headH=30, bodyW=42(38)/bodyH=62, legW=13/legH=95.
        // 3D 변환: 캡슐 본래 폭 1m → 0.5×0.5면 정사이즈, 옛 0.78×0.66은 과체중.

        // ── 슬롯별 머티리얼 캐시 (RefreshOutfitColors가 사용) ──
        private Material hatMat;       // Cap + CapBrim
        private Material topMat;       // Shirt
        private Material bottomMat;    // LegL + LegR
        private Material outerwearMat; // Body + ArmL + ArmR (자켓 외피)
        private Material shoesMat;     // BootL + BootR
        private Material backpackMat;  // Backpack 본체
        private Material backpackStrapMat; // Backpack 어깨끈 (어둡게)
        private Material toolMat;      // NetHandle
        private Material toolRingMat;  // NetRing
        private Material accessoryMat; // Accessory 노드들
        private Material skinMat;      // Neck/Head/Hand/Ear 등 피부 노출 부위
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
        private GameObject accessoryRoot;     // 부모 컨테이너
        private GameObject accGlassesL;
        private GameObject accGlassesR;
        private GameObject accNecklace;
        private GameObject accBadge;          // 가슴팍 기본 큐브

        private bool subscribedToOutfit;

        private void Awake()
        {
            BuildAll();
        }

        private void OnEnable()
        {
            TrySubscribeOutfitChanged();
        }

        private void Start()
        {
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
            SafeDestroyMat(ref hatMat);
            SafeDestroyMat(ref topMat);
            SafeDestroyMat(ref bottomMat);
            SafeDestroyMat(ref outerwearMat);
            SafeDestroyMat(ref shoesMat);
            SafeDestroyMat(ref backpackMat);
            SafeDestroyMat(ref backpackStrapMat);
            SafeDestroyMat(ref toolMat);
            SafeDestroyMat(ref toolRingMat);
            SafeDestroyMat(ref accessoryMat);
            SafeDestroyMat(ref skinMat);
            SafeDestroyMat(ref hairMat);
        }

        private void SafeDestroyMat(ref Material m)
        {
            if (m == null) return;
            // sharedMaterial로 아직 사용 중인지 확인 — 사용 중이면 Unity가 자체 cleanup
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sharedMaterial == m)
                {
                    m = null;
                    return;
                }
            }
            Destroy(m);
            m = null;
        }

        private void TrySubscribeOutfitChanged()
        {
            if (subscribedToOutfit) return;
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            if (mgr == null) return;
            mgr.OutfitChanged += RefreshOutfitColors;
            subscribedToOutfit = true;
        }

        private static bool shaderDiagLogged;
        private static Material MakeMaterial(Color color)
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
            return mat;
        }

        private void BuildAll()
        {
            // ── 진단 로그 (검은 캐릭터 보고 시 추적용) ──
            Debug.Log("[PlayerVisualBuilder] BuildAll 시작 — player에 머티리얼 + 자식 노드 생성");

            // ── 기본 색상 (의상 미장착 시) ──
            outerwearMat = MakeMaterial(new Color(0.2f, 0.4f, 0.85f));      // 자켓
            topMat = MakeMaterial(new Color(0.98f, 0.96f, 0.92f));          // 셔츠
            bottomMat = MakeMaterial(new Color(0.18f, 0.22f, 0.28f));       // 바지
            skinMat = MakeMaterial(new Color(0.92f, 0.78f, 0.62f));         // 클래스 필드로 승격 (OnDestroy 정리)
            hatMat = MakeMaterial(new Color(1.0f, 0.65f, 0.2f));            // 모자
            shoesMat = MakeMaterial(new Color(0.2f, 0.12f, 0.06f));         // 부츠
            backpackMat = MakeMaterial(new Color(1.0f, 0.65f, 0.2f));       // 배낭
            backpackStrapMat = MakeMaterial(new Color(0.7f, 0.45f, 0.14f));
            toolMat = MakeMaterial(new Color(0.6f, 0.4f, 0.2f));            // 잠자리채 손잡이
            toolRingMat = MakeMaterial(new Color(0.95f, 0.92f, 0.88f));     // 잠자리채 망
            accessoryMat = MakeMaterial(new Color(0.1f, 0.1f, 0.1f));       // 악세서리

            int gender = PlayerPrefs.GetInt("InsectGame.Character.Gender", 0);

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
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(t, false);
            body.transform.localPosition = new Vector3(0f, 0.77f, 0f);
            body.transform.localScale = new Vector3(bodyScaleX, 0.46f, bodyScaleZ);
            body.GetComponent<MeshRenderer>().material = outerwearMat;
            Object.Destroy(body.GetComponent<Collider>());

            // ── 셔츠 (Top) — Body 안쪽 면적으로 살짝 작게, 자켓 열린 사이로 보이는 영역 ──
            shirtRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shirtRoot.name = "Shirt";
            shirtRoot.transform.SetParent(t, false);
            shirtRoot.transform.localPosition = new Vector3(0f, 0.83f, 0.15f);
            shirtRoot.transform.localScale = new Vector3(0.34f, 0.40f, 0.20f);
            shirtRoot.GetComponent<MeshRenderer>().material = topMat;
            Object.Destroy(shirtRoot.GetComponent<Collider>());

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

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(headPivot.transform, false);
            head.transform.localPosition = Vector3.zero;
            // 치비 둥근 머리: 옛 (−0.10, +0.12, −0.04) 달걀형(세로로 긺) → X/Y를 거의 균등하게.
            // Z는 −0.04 유지 — 얼굴 부품 z위치(0.32 등)가 머리 앞면에 그대로 얹히도록(재배치 회피).
            head.transform.localScale = new Vector3(headScale - 0.02f, headScale - 0.04f, headScale - 0.04f);
            head.GetComponent<MeshRenderer>().material = skinMat;
            Object.Destroy(head.GetComponent<Collider>());

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
            int hairStyle = PlayerPrefs.GetInt("InsectGame.Character.HairStyle", 0);
            int hairColorIdx = PlayerPrefs.GetInt("InsectGame.Character.HairColor", 0);
            Color[] hairColors = {
                new Color(0.12f, 0.08f, 0.05f),
                new Color(0.35f, 0.2f, 0.1f),
                new Color(0.85f, 0.7f, 0.3f),
                new Color(0.6f, 0.15f, 0.1f),
                new Color(0.2f, 0.15f, 0.35f),
                new Color(0.15f, 0.3f, 0.5f),
            };
            Color hairColor = hairColors[Mathf.Clamp(hairColorIdx, 0, hairColors.Length - 1)];
            hairMat = MakeMaterial(hairColor);
            BuildHair(headPivot, hairStyle, gender, hairMat);

            // ── 팔 (어깨 ±0.29 / Y 1.40 / 캡슐 길이 0.50 / 회전 0°) ──
            // 옛 Y=1.55 + 길이 0.50 → 상단 1.80m로 Body 상단(1.79)과 일치하나 시각적으로 어깨가 여전히 높음.
            // Y 1.40 + 길이 0.50 → 캡슐 범위 1.15~1.65m로 Body(1.01~1.79) 중간 → 자연스러운 인체 비례.
            // 머리(2.20) 영역과 충분히 분리. X ±0.29 Body 가장자리 겹침, Z=0° 수직, swing은 X만.
            outerwearArmL = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            outerwearArmL.name = "ArmL";
            outerwearArmL.transform.SetParent(t, false);
            outerwearArmL.transform.localPosition = new Vector3(-0.29f, 0.78f, 0f);
            outerwearArmL.transform.localScale = new Vector3(0.135f, 0.23f, 0.135f);
            outerwearArmL.transform.localRotation = Quaternion.identity;
            outerwearArmL.GetComponent<MeshRenderer>().material = outerwearMat;
            Object.Destroy(outerwearArmL.GetComponent<Collider>());

            outerwearArmR = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            outerwearArmR.name = "ArmR";
            outerwearArmR.transform.SetParent(t, false);
            outerwearArmR.transform.localPosition = new Vector3(0.29f, 0.78f, 0f);
            outerwearArmR.transform.localScale = new Vector3(0.135f, 0.23f, 0.135f);
            outerwearArmR.transform.localRotation = Quaternion.identity;
            outerwearArmR.GetComponent<MeshRenderer>().material = outerwearMat;
            Object.Destroy(outerwearArmR.GetComponent<Collider>());

            // ── 손 (팔 끝점: y = 1.40 - 0.25 = 1.15. 손목/손바닥 자연 매달림 = 0.95) ──
            GameObject handL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handL.name = "HandL";
            handL.transform.SetParent(t, false);
            handL.transform.localPosition = new Vector3(-0.29f, 0.52f, 0f);
            handL.transform.localScale = new Vector3(0.115f, 0.115f, 0.115f);
            handL.GetComponent<MeshRenderer>().material = skinMat;
            Object.Destroy(handL.GetComponent<Collider>());

            GameObject handR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handR.name = "HandR";
            handR.transform.SetParent(t, false);
            handR.transform.localPosition = new Vector3(0.29f, 0.52f, 0f);
            handR.transform.localScale = new Vector3(0.115f, 0.115f, 0.115f);
            handR.GetComponent<MeshRenderer>().material = skinMat;
            Object.Destroy(handR.GetComponent<Collider>());

            // ── 다리 + 부츠 (LegPivot으로 묶어 회전 시 발도 함께 움직이도록) ──
            // 옛은 BootL/R이 Player 직접 자식이라 PlayerMovement.AnimateWalk의 LegL/R 회전이 발에 전파 안 됨
            // → 사용자 보고 "다리는 움직이는데 발이 안 움직임". LegPivot(빈, scale 1) 안에 Leg+Boot 둘 다 자식으로
            // 배치하여 Pivot 회전 시 Leg+Boot 모두 자동 전파. Pivot Y=0.86 = 다리 최상단(허벅지 윗부분)으로
            // 회전 중심이 골반에 위치 → 자연스러운 보행. Leg/Boot scale 비균등이라 Pivot scale=1 유지 필수.
            // 치비: 짧고 통통한 다리 + 좁은 스탠스(X ±0.13). Pivot Y 0.48 = 골반(회전 중심).
            GameObject legLPivot = new GameObject("LegLPivot");
            legLPivot.transform.SetParent(t, false);
            legLPivot.transform.localPosition = new Vector3(-0.13f, 0.48f, 0f);

            GameObject legL = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            legL.name = "LegL";
            legL.transform.SetParent(legLPivot.transform, false);
            legL.transform.localPosition = new Vector3(0f, -0.14f, 0f);  // Pivot 아래 0.14m = 절대 Y 0.34
            legL.transform.localScale = new Vector3(legScale, 0.20f, legScale);
            legL.GetComponent<MeshRenderer>().material = bottomMat;
            Object.Destroy(legL.GetComponent<Collider>());

            GameObject bootL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bootL.name = "BootL";
            bootL.transform.SetParent(legLPivot.transform, false);
            bootL.transform.localPosition = new Vector3(0f, -0.36f, 0.07f);  // Pivot 아래 0.36m = 절대 Y 0.12, 발바닥 ~0.045
            bootL.transform.localScale = new Vector3(0.21f, 0.15f, 0.30f);
            bootL.GetComponent<MeshRenderer>().material = shoesMat;
            Object.Destroy(bootL.GetComponent<Collider>());

            GameObject legRPivot = new GameObject("LegRPivot");
            legRPivot.transform.SetParent(t, false);
            legRPivot.transform.localPosition = new Vector3(0.13f, 0.48f, 0f);

            GameObject legR = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            legR.name = "LegR";
            legR.transform.SetParent(legRPivot.transform, false);
            legR.transform.localPosition = new Vector3(0f, -0.14f, 0f);
            legR.transform.localScale = new Vector3(legScale, 0.20f, legScale);
            legR.GetComponent<MeshRenderer>().material = bottomMat;
            Object.Destroy(legR.GetComponent<Collider>());

            GameObject bootR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bootR.name = "BootR";
            bootR.transform.SetParent(legRPivot.transform, false);
            bootR.transform.localPosition = new Vector3(0f, -0.36f, 0.07f);
            bootR.transform.localScale = new Vector3(0.21f, 0.15f, 0.30f);
            bootR.GetComponent<MeshRenderer>().material = shoesMat;
            Object.Destroy(bootR.GetComponent<Collider>());

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

            // ── 도구 (NetHandle/NetRing) — CharacterOutfitManager.ApplyToolShape가 이름 의존 ──
            // 기본 좌표 = ApplyToolShape의 net(기본 잠자리채) else 분기 최종값과 일치시킴.
            // 옛 좌표(handle 0.55,1.62,-0.18 / ring 0.82,2.28,-0.1 머리높이·뒤쪽)는 stale 레거시로,
            // ApplyToolShape이 첫 프레임 전 덮어쓰지만 도구 미장착/early-return 시 머리 위 뒤에 망이
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

            // ── 악세서리 (신규) ──
            BuildAccessoryNodes();
        }

        private void BuildFace(GameObject headPivot, float headScale, int gender)
        {
            Material eyeMat = MakeMaterial(Color.white);
            Material pupilMat = MakeMaterial(new Color(0.12f, 0.08f, 0.05f));

            GameObject eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeL.name = "EyeL";
            eyeL.transform.SetParent(headPivot.transform, false);
            // 치비 큰 눈: 옛 0.11 → 0.15/0.17(세로로 큰 동그란 눈), 살짝 아래·바깥(귀여운 인상).
            eyeL.transform.localPosition = new Vector3(-0.12f, -0.03f, 0.32f);
            eyeL.transform.localScale = new Vector3(0.15f, 0.17f, 0.06f);
            eyeL.GetComponent<MeshRenderer>().material = eyeMat;
            Object.Destroy(eyeL.GetComponent<Collider>());

            GameObject eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeR.name = "EyeR";
            eyeR.transform.SetParent(headPivot.transform, false);
            eyeR.transform.localPosition = new Vector3(0.12f, -0.03f, 0.32f);
            eyeR.transform.localScale = new Vector3(0.15f, 0.17f, 0.06f);
            eyeR.GetComponent<MeshRenderer>().material = eyeMat;
            Object.Destroy(eyeR.GetComponent<Collider>());

            GameObject pupilL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupilL.name = "PupilL";
            pupilL.transform.SetParent(headPivot.transform, false);
            pupilL.transform.localPosition = new Vector3(-0.12f, -0.04f, 0.35f);
            pupilL.transform.localScale = new Vector3(0.09f, 0.11f, 0.02f);
            pupilL.GetComponent<MeshRenderer>().material = pupilMat;
            Object.Destroy(pupilL.GetComponent<Collider>());

            GameObject pupilR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupilR.name = "PupilR";
            pupilR.transform.SetParent(headPivot.transform, false);
            pupilR.transform.localPosition = new Vector3(0.12f, -0.04f, 0.35f);
            pupilR.transform.localScale = new Vector3(0.09f, 0.11f, 0.02f);
            pupilR.GetComponent<MeshRenderer>().material = pupilMat;
            Object.Destroy(pupilR.GetComponent<Collider>());

            Material hlMat = MakeMaterial(new Color(1f, 1f, 1f, 0.9f));
            GameObject hlL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hlL.name = "HighlightL";
            hlL.transform.SetParent(headPivot.transform, false);
            hlL.transform.localPosition = new Vector3(-0.10f, 0.01f, 0.36f);
            hlL.transform.localScale = new Vector3(0.045f, 0.045f, 0.01f);
            hlL.GetComponent<MeshRenderer>().material = hlMat;
            Object.Destroy(hlL.GetComponent<Collider>());

            GameObject hlR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hlR.name = "HighlightR";
            hlR.transform.SetParent(headPivot.transform, false);
            hlR.transform.localPosition = new Vector3(0.10f, 0.01f, 0.36f);
            hlR.transform.localScale = new Vector3(0.045f, 0.045f, 0.01f);
            hlR.GetComponent<MeshRenderer>().material = hlMat;
            Object.Destroy(hlR.GetComponent<Collider>());

            int faceType = PlayerPrefs.GetInt("InsectGame.Character.FaceType", 0);
            Material browMat = MakeMaterial(new Color(0.2f, 0.15f, 0.1f));
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

            Material skinMat = MakeMaterial(new Color(0.92f, 0.78f, 0.62f));
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "Nose";
            nose.transform.SetParent(headPivot.transform, false);
            // 치비: 코는 작은 점으로 (큰 눈 강조). 위치도 눈 아래로 내림.
            nose.transform.localPosition = new Vector3(0f, -0.10f, 0.35f);
            nose.transform.localScale = new Vector3(0.024f, 0.022f, 0.02f);
            nose.GetComponent<MeshRenderer>().material = skinMat;
            Object.Destroy(nose.GetComponent<Collider>());

            Material mouthMat = MakeMaterial(new Color(0.8f, 0.4f, 0.35f));
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
                Material blushMat = MakeMaterial(new Color(1f, 0.6f, 0.6f, 0.4f));
                GameObject blushL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blushL.name = "BlushL";
                blushL.transform.SetParent(headPivot.transform, false);
                blushL.transform.localPosition = new Vector3(-0.16f, -0.10f, 0.30f);
                blushL.transform.localScale = new Vector3(0.08f, 0.05f, 0.02f);
                blushL.GetComponent<MeshRenderer>().material = blushMat;
                Object.Destroy(blushL.GetComponent<Collider>());

                GameObject blushR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blushR.name = "BlushR";
                blushR.transform.SetParent(headPivot.transform, false);
                blushR.transform.localPosition = new Vector3(0.16f, -0.10f, 0.30f);
                blushR.transform.localScale = new Vector3(0.08f, 0.05f, 0.02f);
                blushR.GetComponent<MeshRenderer>().material = blushMat;
                Object.Destroy(blushR.GetComponent<Collider>());

                Material lashMat = MakeMaterial(new Color(0.1f, 0.08f, 0.05f));
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
            GameObject earL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earL.name = "EarL";
            earL.transform.SetParent(headPivot.transform, false);
            earL.transform.localPosition = new Vector3(-0.34f, -0.02f, 0f);
            earL.transform.localScale = new Vector3(0.05f, 0.08f, 0.06f);
            earL.GetComponent<MeshRenderer>().material = skinMat;
            Object.Destroy(earL.GetComponent<Collider>());

            GameObject earR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earR.name = "EarR";
            earR.transform.SetParent(headPivot.transform, false);
            earR.transform.localPosition = new Vector3(0.34f, -0.02f, 0f);
            earR.transform.localScale = new Vector3(0.05f, 0.08f, 0.06f);
            earR.GetComponent<MeshRenderer>().material = skinMat;
            Object.Destroy(earR.GetComponent<Collider>());
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

        private void BuildShortHair(GameObject headPivot, int gender, Material mat)
        {
            GameObject hairTop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hairTop.name = "HairTop";
            hairTop.transform.SetParent(headPivot.transform, false);
            hairTop.transform.localPosition = new Vector3(0f, 0.22f, -0.02f);
            hairTop.transform.localScale = new Vector3(0.62f, 0.34f, 0.60f);
            hairTop.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(hairTop.GetComponent<Collider>());

            GameObject hairSideL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hairSideL.name = "HairSideL";
            hairSideL.transform.SetParent(headPivot.transform, false);
            hairSideL.transform.localPosition = new Vector3(-0.2f, 0.05f, -0.02f);
            hairSideL.transform.localScale = new Vector3(0.12f, 0.2f, 0.35f);
            hairSideL.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(hairSideL.GetComponent<Collider>());

            GameObject hairSideR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hairSideR.name = "HairSideR";
            hairSideR.transform.SetParent(headPivot.transform, false);
            hairSideR.transform.localPosition = new Vector3(0.2f, 0.05f, -0.02f);
            hairSideR.transform.localScale = new Vector3(0.12f, 0.2f, 0.35f);
            hairSideR.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(hairSideR.GetComponent<Collider>());

            GameObject hairBack = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hairBack.name = "HairBack";
            hairBack.transform.SetParent(headPivot.transform, false);
            hairBack.transform.localPosition = new Vector3(0f, 0.08f, -0.15f);
            hairBack.transform.localScale = new Vector3(0.45f, 0.28f, 0.2f);
            hairBack.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(hairBack.GetComponent<Collider>());

            if (gender == 1)
            {
                GameObject bangs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bangs.name = "HairBangs";
                bangs.transform.SetParent(headPivot.transform, false);
                bangs.transform.localPosition = new Vector3(0f, 0.18f, 0.16f);
                bangs.transform.localScale = new Vector3(0.4f, 0.08f, 0.1f);
                bangs.GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(bangs.GetComponent<Collider>());
            }
        }

        private void BuildMediumHair(GameObject headPivot, int gender, Material mat)
        {
            BuildShortHair(headPivot, gender, mat);

            GameObject hairExtL = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hairExtL.name = "HairExtL";
            hairExtL.transform.SetParent(headPivot.transform, false);
            hairExtL.transform.localPosition = new Vector3(-0.2f, -0.1f, -0.05f);
            hairExtL.transform.localScale = new Vector3(0.1f, 0.18f, 0.12f);
            hairExtL.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(hairExtL.GetComponent<Collider>());

            GameObject hairExtR = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hairExtR.name = "HairExtR";
            hairExtR.transform.SetParent(headPivot.transform, false);
            hairExtR.transform.localPosition = new Vector3(0.2f, -0.1f, -0.05f);
            hairExtR.transform.localScale = new Vector3(0.1f, 0.18f, 0.12f);
            hairExtR.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(hairExtR.GetComponent<Collider>());

            GameObject hairBackExt = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hairBackExt.name = "HairBackExt";
            hairBackExt.transform.SetParent(headPivot.transform, false);
            hairBackExt.transform.localPosition = new Vector3(0f, -0.08f, -0.18f);
            hairBackExt.transform.localScale = new Vector3(0.35f, 0.2f, 0.15f);
            hairBackExt.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(hairBackExt.GetComponent<Collider>());
        }

        private void BuildLongHair(GameObject headPivot, int gender, Material mat)
        {
            BuildShortHair(headPivot, gender, mat);

            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.12f;
                GameObject strand = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                strand.name = $"HairLong_{i}";
                strand.transform.SetParent(headPivot.transform, false);
                strand.transform.localPosition = new Vector3(x, -0.3f, -0.12f);
                strand.transform.localScale = new Vector3(0.12f, 0.35f, 0.1f);
                strand.GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(strand.GetComponent<Collider>());
            }

            GameObject frontL = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            frontL.name = "HairFrontL";
            frontL.transform.SetParent(headPivot.transform, false);
            frontL.transform.localPosition = new Vector3(-0.18f, -0.12f, 0.1f);
            frontL.transform.localScale = new Vector3(0.06f, 0.22f, 0.06f);
            frontL.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(frontL.GetComponent<Collider>());

            GameObject frontR = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            frontR.name = "HairFrontR";
            frontR.transform.SetParent(headPivot.transform, false);
            frontR.transform.localPosition = new Vector3(0.18f, -0.12f, 0.1f);
            frontR.transform.localScale = new Vector3(0.06f, 0.22f, 0.06f);
            frontR.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(frontR.GetComponent<Collider>());

            if (gender == 1)
            {
                GameObject bangs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bangs.name = "HairBangs";
                bangs.transform.SetParent(headPivot.transform, false);
                bangs.transform.localPosition = new Vector3(0f, 0.18f, 0.16f);
                bangs.transform.localScale = new Vector3(0.4f, 0.1f, 0.1f);
                bangs.GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(bangs.GetComponent<Collider>());

                for (int i = 0; i < 2; i++)
                {
                    float x = (i == 0) ? -0.08f : 0.08f;
                    GameObject longStrand = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    longStrand.name = $"HairVeryLong_{i}";
                    longStrand.transform.SetParent(headPivot.transform, false);
                    longStrand.transform.localPosition = new Vector3(x, -0.55f, -0.12f);
                    longStrand.transform.localScale = new Vector3(0.1f, 0.3f, 0.08f);
                    longStrand.GetComponent<MeshRenderer>().material = mat;
                    Object.Destroy(longStrand.GetComponent<Collider>());
                }
            }
        }

        private void BuildUpHair(GameObject headPivot, int gender, Material mat)
        {
            GameObject hairTop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hairTop.name = "HairTop";
            hairTop.transform.SetParent(headPivot.transform, false);
            hairTop.transform.localPosition = new Vector3(0f, 0.26f, -0.02f);
            hairTop.transform.localScale = new Vector3(0.55f, 0.30f, 0.52f);
            hairTop.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(hairTop.GetComponent<Collider>());

            if (gender == 0)
            {
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                spike.name = "HairSpike";
                spike.transform.SetParent(headPivot.transform, false);
                spike.transform.localPosition = new Vector3(0f, 0.35f, 0.05f);
                spike.transform.localScale = new Vector3(0.15f, 0.18f, 0.12f);
                spike.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                spike.GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(spike.GetComponent<Collider>());

                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject sideSpike = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    sideSpike.name = $"HairSideSpike_{(side > 0 ? "R" : "L")}";
                    sideSpike.transform.SetParent(headPivot.transform, false);
                    sideSpike.transform.localPosition = new Vector3(side * 0.15f, 0.28f, 0f);
                    sideSpike.transform.localScale = new Vector3(0.08f, 0.12f, 0.08f);
                    sideSpike.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 30f);
                    sideSpike.GetComponent<MeshRenderer>().material = mat;
                    Object.Destroy(sideSpike.GetComponent<Collider>());
                }
            }
            else
            {
                GameObject bun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bun.name = "HairBun";
                bun.transform.SetParent(headPivot.transform, false);
                bun.transform.localPosition = new Vector3(0f, 0.15f, -0.22f);
                bun.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
                bun.GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(bun.GetComponent<Collider>());

                Material ribbonMat = MakeMaterial(new Color(0.9f, 0.3f, 0.4f));
                GameObject ribbon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ribbon.name = "HairRibbon";
                ribbon.transform.SetParent(headPivot.transform, false);
                ribbon.transform.localPosition = new Vector3(0f, 0.15f, -0.22f);
                ribbon.transform.localScale = new Vector3(0.24f, 0.02f, 0.24f);
                ribbon.GetComponent<MeshRenderer>().material = ribbonMat;
                Object.Destroy(ribbon.GetComponent<Collider>());

                GameObject bangs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bangs.name = "HairBangs";
                bangs.transform.SetParent(headPivot.transform, false);
                bangs.transform.localPosition = new Vector3(0f, 0.18f, 0.16f);
                bangs.transform.localScale = new Vector3(0.4f, 0.08f, 0.1f);
                bangs.GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(bangs.GetComponent<Collider>());

                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject sideHair = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    sideHair.name = $"HairSide_{(side > 0 ? "R" : "L")}";
                    sideHair.transform.SetParent(headPivot.transform, false);
                    sideHair.transform.localPosition = new Vector3(side * 0.2f, -0.05f, 0.05f);
                    sideHair.transform.localScale = new Vector3(0.06f, 0.15f, 0.06f);
                    sideHair.GetComponent<MeshRenderer>().material = mat;
                    Object.Destroy(sideHair.GetComponent<Collider>());
                }
            }
        }

        // ──────────────────────────────────────────────
        //  Accessory: 안경/목걸이/배지(기본 큐브) 세 가지를 미리 생성하고
        //  itemId에 따라 SetActive 분기.
        // ──────────────────────────────────────────────
        private void BuildAccessoryNodes()
        {
            accessoryRoot = new GameObject("AccessoryRoot");
            accessoryRoot.transform.SetParent(transform, false);
            accessoryRoot.transform.localPosition = Vector3.zero;

            // 안경: 치비 눈 월드높이(headPivot 1.22 + 눈 local×0.60 ≈ 1.20)에 맞춤. 옛 Y2.20은
            // 캐릭터 키 2.38 기준이라 치비에서 머리 위 ~1유닛 허공에 떴음. 커진 눈에 맞춰 렌즈도 확대.
            accGlassesL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            accGlassesL.name = "AccGlassesL";
            accGlassesL.transform.SetParent(accessoryRoot.transform, false);
            accGlassesL.transform.localPosition = new Vector3(-0.072f, 1.20f, 0.21f);
            accGlassesL.transform.localScale = new Vector3(0.10f, 0.09f, 0.02f);
            accGlassesL.GetComponent<MeshRenderer>().material = accessoryMat;
            Object.Destroy(accGlassesL.GetComponent<Collider>());

            accGlassesR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            accGlassesR.name = "AccGlassesR";
            accGlassesR.transform.SetParent(accessoryRoot.transform, false);
            accGlassesR.transform.localPosition = new Vector3(0.072f, 1.20f, 0.21f);
            accGlassesR.transform.localScale = new Vector3(0.10f, 0.09f, 0.02f);
            accGlassesR.GetComponent<MeshRenderer>().material = accessoryMat;
            Object.Destroy(accGlassesR.GetComponent<Collider>());

            // 목걸이/펜던트: 목/쇄골(치비 목 ~1.0). 옛 Y1.88 → 1.00.
            accNecklace = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            accNecklace.name = "AccNecklace";
            accNecklace.transform.SetParent(accessoryRoot.transform, false);
            accNecklace.transform.localPosition = new Vector3(0f, 1.00f, 0.20f);
            accNecklace.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
            accNecklace.GetComponent<MeshRenderer>().material = accessoryMat;
            Object.Destroy(accNecklace.GetComponent<Collider>());

            // 기본 배지: 가슴팍(치비 Body Y0.77 앞면 ~0.85). 옛 Y1.65 → 0.85.
            accBadge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            accBadge.name = "AccBadge";
            accBadge.transform.SetParent(accessoryRoot.transform, false);
            accBadge.transform.localPosition = new Vector3(0f, 0.85f, 0.20f);
            accBadge.transform.localScale = new Vector3(0.10f, 0.10f, 0.04f);
            accBadge.GetComponent<MeshRenderer>().material = accessoryMat;
            Object.Destroy(accBadge.GetComponent<Collider>());

            // 초기 상태: 모두 숨김 (RefreshOutfitColors에서 itemId에 맞춰 켬)
            accGlassesL.SetActive(false);
            accGlassesR.SetActive(false);
            accNecklace.SetActive(false);
            accBadge.SetActive(false);
        }

        // ──────────────────────────────────────────────
        //  OutfitChanged 핸들러: 머티리얼 색 + Accessory 노드 가시성 갱신.
        //  GameObject 재생성 X.
        // ──────────────────────────────────────────────
        public void RefreshOutfitColors()
        {
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            if (mgr == null) return;

            // 7개 슬롯 (Hat/Top/Bottom/Outerwear/Shoes/Backpack/Tool): ApplyToCharacter가 노드별
            // 머티리얼 색 + 알파 0 시 SetActive(false) 적용. 게임 시작 시점에도 저장 의상 동기화.
            mgr.ApplyToCharacter();

            // Accessory: ApplyToCharacter에 미포함이라 PlayerVisualBuilder가 자체 노드로 처리.
            ApplyAccessory(mgr.GetEquipped(OutfitSlot.Accessory));
        }

        private void ApplyAccessory(OutfitItem item)
        {
            if (accessoryRoot == null) return;

            // 모두 꺼두고 itemId에 맞는 노드만 켠다.
            if (accGlassesL != null) accGlassesL.SetActive(false);
            if (accGlassesR != null) accGlassesR.SetActive(false);
            if (accNecklace != null) accNecklace.SetActive(false);
            if (accBadge != null) accBadge.SetActive(false);

            if (item == null) return;
            if (item.primaryColor.a < 0.01f) return; // acc_none

            string id = item.itemId ?? "";
            Color c = item.primaryColor;

            if (id.Contains("glasses") || id.Contains("visor") || id.Contains("eyepatch"))
            {
                if (accGlassesL != null)
                {
                    accGlassesL.SetActive(true);
                    SetColor(accGlassesL, c);
                }
                if (accGlassesR != null)
                {
                    accGlassesR.SetActive(true);
                    // eyepatch는 한쪽만
                    if (id.Contains("eyepatch")) accGlassesR.SetActive(false);
                    else SetColor(accGlassesR, c);
                }
            }
            else if (id.Contains("necklace") || id.Contains("pendant") || id.Contains("orb") || id.Contains("crystal_orb"))
            {
                if (accNecklace != null)
                {
                    accNecklace.SetActive(true);
                    SetColor(accNecklace, c);
                }
            }
            else
            {
                // 스카프/배지/엠블럼/완장/머플러/반다나/날개/오라/후광/네온/도그태그 등 기본 가슴팍 큐브
                if (accBadge != null)
                {
                    accBadge.SetActive(true);
                    SetColor(accBadge, c);
                }
            }
        }

        private static void SetColor(GameObject go, Color c)
        {
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            Material mat = mr.material;
            if (mat == null) return;
            mat.color = c;
        }
    }
}
