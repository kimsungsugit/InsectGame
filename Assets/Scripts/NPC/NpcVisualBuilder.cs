using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.NPC
{
    /// <summary>NPC 외형 파라미터 — System.Random(seed) 기반 결정적 생성.</summary>
    public struct NpcAppearance
    {
        public bool isChild;
        public int hairStyle;   // 0 짧은머리 / 1 중간머리 / 2 올림머리
        public bool hasHat;
        public Color hair;
        public Color top;
        public Color bottom;
        public Color skin;
        public Color hat;
    }

    /// <summary>
    /// NPC 프로시저럴 모델 빌더 — PlayerVisualBuilder.BuildAll 지오메트리의 단순화 이식.
    /// PlayerPrefs/CharacterOutfitManager 의존 없음. 노드명(Body/Shirt/HeadPivot/ArmL/ArmR/
    /// LegLPivot/LegRPivot/NetHandle/NetRing)은 NpcWalkAnimator가 transform.Find로 캐시하므로 변경 금지.
    /// 콜라이더는 몸통 캡슐(트리거) 하나만 루트에 부착 — 파츠 콜라이더는 전부 Destroy.
    /// (트리거로 두어 PlayerMovement의 이동 차단/끼임 감지에 걸리지 않게 함.)
    /// 생성된 머티리얼 정리는 NPC 컴포넌트 OnDestroy에서 CleanupMaterials 호출.
    /// </summary>
    public static class NpcVisualBuilder
    {
        // ── 팔레트 (결정적 랜덤 변주용) ──
        private static readonly Color[] HairPalette =
        {
            new Color(0.12f, 0.08f, 0.05f),
            new Color(0.35f, 0.2f, 0.1f),
            new Color(0.55f, 0.42f, 0.28f),
            new Color(0.45f, 0.45f, 0.5f),
            new Color(0.2f, 0.15f, 0.35f),
        };

        private static readonly Color[] TopPalette =
        {
            new Color(0.75f, 0.3f, 0.25f),
            new Color(0.25f, 0.5f, 0.75f),
            new Color(0.35f, 0.6f, 0.35f),
            new Color(0.85f, 0.7f, 0.4f),
            new Color(0.6f, 0.45f, 0.7f),
            new Color(0.9f, 0.88f, 0.82f),
        };

        private static readonly Color[] KidTopPalette =
        {
            new Color(1.0f, 0.55f, 0.3f),
            new Color(0.4f, 0.75f, 0.95f),
            new Color(0.55f, 0.85f, 0.4f),
            new Color(0.95f, 0.8f, 0.3f),
            new Color(0.9f, 0.5f, 0.7f),
        };

        private static readonly Color[] BottomPalette =
        {
            new Color(0.18f, 0.22f, 0.28f),
            new Color(0.35f, 0.28f, 0.2f),
            new Color(0.25f, 0.35f, 0.3f),
            new Color(0.4f, 0.4f, 0.45f),
        };

        /// <summary>
        /// NPC 피부 <b>다양성</b>용 4색. 플레이어의 <c>CharacterPalette.Skin</c>(생성 화면의
        /// "밝은/보통/어두운/진한" 선택지)과는 성격이 다르므로 통합하지 않는다 — 인덱스를 그대로
        /// 옮기면 스토리 NPC 9종의 고정 외형이 통째로 바뀐다(그건 이 작업의 목표가 아니다).
        /// 첫 항이 플레이어의 옛 하드코딩 피부색과 같은 값인 건 우연이 아니라 복제의 흔적이다.
        /// </summary>
        private static readonly Color[] SkinPalette =
        {
            new Color(0.92f, 0.78f, 0.62f),
            new Color(0.85f, 0.68f, 0.5f),
            new Color(0.95f, 0.83f, 0.7f),
            new Color(0.72f, 0.55f, 0.4f),
        };

        private static readonly Color[] HatPalette =
        {
            new Color(1.0f, 0.65f, 0.2f),
            new Color(0.3f, 0.45f, 0.65f),
            new Color(0.55f, 0.35f, 0.25f),
            new Color(0.85f, 0.3f, 0.3f),
        };

        /// <summary>seed 기반 결정적 성인 주민 외형.</summary>
        public static NpcAppearance RandomVillager(int seed)
        {
            System.Random rng = new System.Random(seed);
            return new NpcAppearance
            {
                isChild = false,
                hairStyle = rng.Next(0, 3),
                hasHat = rng.NextDouble() < 0.4,
                hair = HairPalette[rng.Next(HairPalette.Length)],
                top = TopPalette[rng.Next(TopPalette.Length)],
                bottom = BottomPalette[rng.Next(BottomPalette.Length)],
                skin = SkinPalette[rng.Next(SkinPalette.Length)],
                hat = HatPalette[rng.Next(HatPalette.Length)],
            };
        }

        /// <summary>seed 기반 결정적 아이 외형 — 밝은 상의 팔레트 + 모자 확률 높음.</summary>
        public static NpcAppearance RandomKid(int seed)
        {
            System.Random rng = new System.Random(seed);
            return new NpcAppearance
            {
                isChild = true,
                hairStyle = rng.Next(0, 3),
                hasHat = rng.NextDouble() < 0.55,
                hair = HairPalette[rng.Next(HairPalette.Length)],
                top = KidTopPalette[rng.Next(KidTopPalette.Length)],
                bottom = BottomPalette[rng.Next(BottomPalette.Length)],
                skin = SkinPalette[rng.Next(SkinPalette.Length)],
                hat = HatPalette[rng.Next(HatPalette.Length)],
            };
        }

        /// <summary>
        /// 스토리 NPC 고정 외형 — 구분되는 실루엣. seed 대신 storyNpcId로 결정.
        /// 동행자(어르신/라온/세라)와 명부회 4인(관장·집게·저울·먹).
        /// **여기에 case를 빠뜨리면 그 NPC가 default(마을 어르신) 외형으로 뜬다** —
        /// <see cref="NpcManager.StoryNpcDisplayName"/>의 이름 switch와 짝이다(둘 다 등록할 것).
        /// 명부회는 아이보리 상의 + 남색 하의로 제복처럼 통일하고 머리·모자로 개체를 가른다
        /// (세라의 보라 상의와 겹치지 않게 함).
        /// </summary>
        public static NpcAppearance StoryNpcAppearance(string storyNpcId)
        {
            switch (storyNpcId)
            {
                case "ledger_chief": // 하월(관장) — 백발·모자 없음. 명부회 수장
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 1, hasHat = false,
                        hair = HairPalette[3], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[1], hat = HatPalette[1],
                    };
                case "ledger_grip": // 집게 — 포획반장. 짧은머리 + 붉은 모자
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = true,
                        hair = HairPalette[0], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[3], hat = HatPalette[3],
                    };
                case "ledger_scale": // 저울 — 분류관. 올림머리 + 모자 없음
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = false,
                        hair = HairPalette[4], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[2], hat = HatPalette[1],
                    };
                case "ledger_ink": // 먹 — 필경사. 올림머리 + 청회 모자
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = true,
                        hair = HairPalette[0], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[0], hat = HatPalette[1],
                    };
                // 1막 하수 2인 — **간부와 같은 상의(TopPalette[5])를 입는다.** 그게 유일한 단서다.
                // 2막에서 집게·저울을 만나면 "저 옷을 어디서 봤더라"가 되게 하려는 것이라
                // 색을 다르게 하면 안 된다. 대신 모자로 둘을 구분한다.
                case "ledger_thug_cord": // 끈 — 그물 담당. 챙 깊은 모자로 얼굴을 가린다
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = true,
                        hair = HairPalette[0], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[2], hat = HatPalette[1],
                    };
                case "ledger_thug_rule": // 자 — 측량 담당. 모자 없이 묶은 머리
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = false,
                        hair = HairPalette[3], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[1], hat = HatPalette[1],
                    };
                case "ledger_thug_pin": // 핀 — 그물터 말단. 모자 있고 앞머리를 덮었다(가장 어리다)
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 1, hasHat = true,
                        hair = HairPalette[1], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[0], hat = HatPalette[1],
                    };
                case "catcher_rival": // 라온 — 곤충잡이 아이(뜰채·모자·밝은 상의)
                    return new NpcAppearance
                    {
                        isChild = true, hairStyle = 0, hasHat = true,
                        hair = HairPalette[1], top = KidTopPalette[0], bottom = BottomPalette[1],
                        skin = SkinPalette[2], hat = HatPalette[0],
                    };
                case "ruins_scholar": // 세라 — 학자(올림머리·모자 없음·보라 상의)
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = false,
                        hair = HairPalette[0], top = TopPalette[4], bottom = BottomPalette[0],
                        skin = SkinPalette[0], hat = HatPalette[1],
                    };
                default: // village_elder — 마을 어르신(백발·모자·따뜻한 상의)
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = true,
                        hair = HairPalette[3], top = TopPalette[3], bottom = BottomPalette[1],
                        skin = SkinPalette[1], hat = HatPalette[2],
                    };
            }
        }

        /// <summary>root 아래에 NPC 모델 생성. 아이는 루트 스케일 0.75 + 뜰채(NetHandle/NetRing) 부착.</summary>
        public static void Build(Transform root, NpcAppearance a)
        {
            if (root == null) return;

            Material topMat = MakeMaterial(a.top, SurfaceKind.Cloth);
            Material bottomMat = MakeMaterial(a.bottom, SurfaceKind.Cloth);
            Material skinMat = MakeMaterial(a.skin, SurfaceKind.Skin);
            Material hairMat = MakeMaterial(a.hair, SurfaceKind.Hair);
            Material shirtMat = MakeMaterial(Color.Lerp(a.top, Color.white, 0.45f), SurfaceKind.Cloth);
            Material shoesMat = MakeMaterial(new Color(0.2f, 0.12f, 0.06f), SurfaceKind.Leather);

            // ── 몸통 (둥근 상자 — 플레이어 치비 비례 이식, NpcWalkAnimator가 "Body"명으로 캐시) ──
            MakeBoxPart("Body", root, new Vector3(0f, 0.77f, 0f),
                new Vector3(0.46f, 0.46f, 0.36f), 0.085f, 3, topMat);

            // ── 셔츠 (앞면 패널) ──
            // 플레이어와 같은 이유로 좁혔다 — 넓으면 흰 판이 앞을 덮어 상의 색이 안 보인다.
            MakeBoxPart("Shirt", root, new Vector3(0f, 0.83f, 0.10f),
                new Vector3(0.24f, 0.36f, 0.20f), 0.05f, 2, shirtMat);

            // ── 머리 (HeadPivot 컨테이너 + Head 구) ──
            GameObject headPivot = new GameObject("HeadPivot");
            headPivot.transform.SetParent(root, false);
            headPivot.transform.localPosition = new Vector3(0f, 1.22f, 0.03f);
            headPivot.transform.localScale = Vector3.one * 0.60f;

            MakeMeshPart("Head", headPivot.transform, UnitSphere(10, 14),
                Vector3.zero, new Vector3(0.70f, 0.68f, 0.68f), skinMat);

            // ── 눈 (흰자 + 동공) ──
            Material eyeMat = MakeMaterial(Color.white, SurfaceKind.Wet);
            Material pupilMat = MakeMaterial(new Color(0.12f, 0.08f, 0.05f), SurfaceKind.Wet);
            Mesh eyeMesh = UnitDisc(16);
            Mesh pupilMesh = UnitDisc(12);
            MakeMeshPart("EyeL", headPivot.transform, eyeMesh,
                new Vector3(-0.12f, -0.03f, 0.32f), new Vector3(0.15f, 0.17f, 0.06f), eyeMat);
            MakeMeshPart("EyeR", headPivot.transform, eyeMesh,
                new Vector3(0.12f, -0.03f, 0.32f), new Vector3(0.15f, 0.17f, 0.06f), eyeMat);
            MakeMeshPart("PupilL", headPivot.transform, pupilMesh,
                new Vector3(-0.12f, -0.04f, 0.35f), new Vector3(0.09f, 0.11f, 0.02f), pupilMat);
            MakeMeshPart("PupilR", headPivot.transform, pupilMesh,
                new Vector3(0.12f, -0.04f, 0.35f), new Vector3(0.09f, 0.11f, 0.02f), pupilMat);

            // ── 머리카락 (스타일 3종 단순 변주) ──
            BuildHair(headPivot.transform, a.hairStyle, hairMat);

            // ── 모자 ──
            if (a.hasHat)
            {
                Material hatMat = MakeMaterial(a.hat, SurfaceKind.Cloth);
                MakePart(PrimitiveType.Cylinder, "Cap", headPivot.transform,
                    new Vector3(0f, 0.3f, -0.02f), new Vector3(0.30f, 0.12f, 0.30f), hatMat);
                MakePart(PrimitiveType.Cube, "CapBrim", headPivot.transform,
                    new Vector3(0f, 0.14f, 0.28f), new Vector3(0.28f, 0.03f, 0.14f), hatMat);
            }

            // ── 팔 ──
            Mesh armMesh = UnitCapsule(0.72f);
            MakeMeshPart("ArmL", root, armMesh,
                new Vector3(-0.29f, 0.78f, 0f), new Vector3(0.135f, 0.23f, 0.135f), topMat);
            MakeMeshPart("ArmR", root, armMesh,
                new Vector3(0.29f, 0.78f, 0f), new Vector3(0.135f, 0.23f, 0.135f), topMat);

            // ── 손 (미튼) ──
            Vector3 handSize = new Vector3(0.105f, 0.135f, 0.095f);
            MakeBoxPart("HandL", root, new Vector3(-0.29f, 0.52f, 0f), handSize, 0.042f, 2, skinMat);
            MakeBoxPart("HandR", root, new Vector3(0.29f, 0.52f, 0f), handSize, 0.042f, 2, skinMat);

            // ── 다리 + 부츠 (LegPivot로 묶어 회전 시 발도 함께 — 플레이어와 동일 구조) ──
            BuildLeg(root, "L", -0.13f, bottomMat, shoesMat);
            BuildLeg(root, "R", 0.13f, bottomMat, shoesMat);

            // ── 아이 전용: 루트 스케일 축소 + 뜰채 ──
            if (a.isChild)
            {
                root.localScale = Vector3.one * 0.75f;

                Material netHandleMat = MakeMaterial(new Color(0.6f, 0.4f, 0.2f), SurfaceKind.Leather);
                Material netRingMat = MakeMaterial(new Color(0.95f, 0.92f, 0.88f), SurfaceKind.Metal);
                GameObject handle = MakePart(PrimitiveType.Cylinder, "NetHandle", root,
                    new Vector3(0.29f, 0.74f, 0.02f), new Vector3(0.04f, 0.40f, 0.04f), netHandleMat);
                handle.transform.localRotation = Quaternion.Euler(20f, 0f, -15f);
                GameObject ring = MakePart(PrimitiveType.Cylinder, "NetRing", root,
                    new Vector3(0.34f, 1.14f, 0.06f), new Vector3(0.20f, 0.02f, 0.20f), netRingMat);
                ring.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            }

            // ── 몸통 캡슐 콜라이더 (루트, 트리거) — 파츠 콜라이더는 전부 제거 완료 상태 ──
            CapsuleCollider capsule = root.gameObject.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = root.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.75f, 0f);
            capsule.radius = 0.28f;
            capsule.height = 1.5f;
            capsule.isTrigger = true;

        }

        /// <summary>
        /// Build가 생성한 인스턴스 머티리얼 정리 — NPC 컴포넌트 OnDestroy에서 호출.
        /// (PlayerVisualBuilder.OnDestroy 패턴의 NPC판: NPC 파괴 시점엔 sharedMaterial
        /// 사용자가 함께 사라지므로 고유 sharedMaterial을 전부 Destroy해도 안전.)
        /// </summary>
        public static void CleanupMaterials(Transform root)
        {
            if (root == null) return;
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            var seen = new HashSet<Material>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Material m = renderers[i].sharedMaterial;
                if (m != null && seen.Add(m)) Object.Destroy(m);
            }
        }

        private static void BuildLeg(Transform root, string side, float x, Material bottomMat, Material shoesMat)
        {
            GameObject pivot = new GameObject($"Leg{side}Pivot");
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = new Vector3(x, 0.48f, 0f);

            MakeMeshPart($"Leg{side}", pivot.transform, UnitCapsule(0.78f),
                new Vector3(0f, -0.14f, 0f), new Vector3(0.20f, 0.20f, 0.20f), bottomMat);
            MakeBoxPart($"Boot{side}", pivot.transform, new Vector3(0f, -0.36f, 0.07f),
                new Vector3(0.21f, 0.15f, 0.30f), 0.052f, 2, shoesMat);
        }

        private static void BuildHair(Transform headPivot, int style, Material hairMat)
        {
            // 공통: 정수리 덮개
            MakeMeshPart("HairTop", headPivot, UnitSphere(8, 12),
                new Vector3(0f, 0.22f, -0.02f), new Vector3(0.62f, 0.34f, 0.60f), hairMat);

            switch (style)
            {
                case 1: // 중간머리 — 옆/뒤 볼륨 추가
                    Mesh tuft = UnitSphere(6, 8);
                    MakeMeshPart("HairSideL", headPivot, tuft,
                        new Vector3(-0.2f, 0.05f, -0.02f), new Vector3(0.12f, 0.2f, 0.35f), hairMat);
                    MakeMeshPart("HairSideR", headPivot, tuft,
                        new Vector3(0.2f, 0.05f, -0.02f), new Vector3(0.12f, 0.2f, 0.35f), hairMat);
                    MakeMeshPart("HairBack", headPivot, tuft,
                        new Vector3(0f, 0.08f, -0.15f), new Vector3(0.45f, 0.28f, 0.2f), hairMat);
                    break;
                case 2: // 올림머리 — 뒤통수 번(bun)
                    MakeMeshPart("HairBun", headPivot, UnitSphere(7, 10),
                        new Vector3(0f, 0.15f, -0.22f), new Vector3(0.22f, 0.22f, 0.22f), hairMat);
                    break;
                    // case 0: 짧은머리 — HairTop만
            }
        }

        // ── 프로시저럴 메시 헬퍼 (PlayerVisualBuilder와 같은 규약) ──
        //
        // 내장 프리미티브와 같은 단위 크기 메시를 쓰므로 기존 localPosition/localScale을
        // 그대로 둔 채 메시만 갈아끼운다. 노드 이름은 NpcWalkAnimator가 문자열로 찾으므로 불변.

        private static GameObject MakeMeshPart(string name, Transform parent, Mesh mesh,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = ProcMeshLibrary.CreateNode(name, parent, mesh, mat, localPos);
            go.transform.localScale = localScale;
            return go;
        }

        /// <summary>크기를 메시에 구운 둥근 상자 — 스케일을 걸지 않는다(모서리 반경 왜곡 방지).</summary>
        private static GameObject MakeBoxPart(string name, Transform parent, Vector3 localPos,
            Vector3 size, float radius, int subdiv, Material mat)
        {
            Mesh mesh = ProcMeshLibrary.RoundedBox(size, radius, subdiv);
            return ProcMeshLibrary.CreateNode(name, parent, mesh, mat, localPos);
        }

        private static Mesh UnitSphere(int rings, int segments)
        {
            return ProcMeshLibrary.LowSphere(0.5f, 0.5f, 0.5f, rings, segments);
        }

        private static Mesh UnitCapsule(float taper)
        {
            float rTop = 0.5f;
            float rBottom = 0.5f * taper;
            return ProcMeshLibrary.TaperedCapsule(rTop, rBottom, 2f - rTop - rBottom, 8, 10);
        }

        private static Mesh UnitDisc(int segments)
        {
            return ProcMeshLibrary.Disc(0.5f, 0.5f, 0.4f, segments);
        }

        private static GameObject MakePart(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().material = mat;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            return go;
        }

        private static bool shaderDiagLogged;

        /// <summary>
        /// PlayerVisualBuilder.MakeMaterial 방식 복제 — Standard→URP→Unlit 폴백 + 부위별 PBR 재질.
        /// 재질을 안 나누면 NPC가 플레이어와 나란히 섰을 때 혼자 무광 점토로 보인다.
        /// </summary>
        private static Material MakeMaterial(Color color, SurfaceKind kind)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                if (!shaderDiagLogged)
                {
                    Debug.LogError("[NpcVisualBuilder] fallback shader를 찾을 수 없습니다 — NPC가 검은색/마젠타로 렌더링됩니다.");
                    shaderDiagLogged = true;
                }
                shader = Shader.Find("Hidden/InternalErrorShader");
            }
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            CharacterPalette.ApplySurface(mat, kind);
            return mat;
        }
    }
}
