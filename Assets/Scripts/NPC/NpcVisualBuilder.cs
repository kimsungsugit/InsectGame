using System.Collections.Generic;
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

            Material topMat = MakeMaterial(a.top);
            Material bottomMat = MakeMaterial(a.bottom);
            Material skinMat = MakeMaterial(a.skin);
            Material hairMat = MakeMaterial(a.hair);
            Material shirtMat = MakeMaterial(Color.Lerp(a.top, Color.white, 0.45f));
            Material shoesMat = MakeMaterial(new Color(0.2f, 0.12f, 0.06f));

            // ── 몸통 (Cube — 플레이어 치비 비례 이식, NpcWalkAnimator가 "Body"명으로 캐시) ──
            MakePart(PrimitiveType.Cube, "Body", root,
                new Vector3(0f, 0.77f, 0f), new Vector3(0.46f, 0.46f, 0.36f), topMat);

            // ── 셔츠 (앞면 패널) ──
            MakePart(PrimitiveType.Cube, "Shirt", root,
                new Vector3(0f, 0.83f, 0.14f), new Vector3(0.34f, 0.40f, 0.20f), shirtMat);

            // ── 머리 (HeadPivot 컨테이너 + Head 구) ──
            GameObject headPivot = new GameObject("HeadPivot");
            headPivot.transform.SetParent(root, false);
            headPivot.transform.localPosition = new Vector3(0f, 1.22f, 0.03f);
            headPivot.transform.localScale = Vector3.one * 0.60f;

            MakePart(PrimitiveType.Sphere, "Head", headPivot.transform,
                Vector3.zero, new Vector3(0.70f, 0.68f, 0.68f), skinMat);

            // ── 눈 (흰자 + 동공) ──
            Material eyeMat = MakeMaterial(Color.white);
            Material pupilMat = MakeMaterial(new Color(0.12f, 0.08f, 0.05f));
            MakePart(PrimitiveType.Sphere, "EyeL", headPivot.transform,
                new Vector3(-0.12f, -0.03f, 0.32f), new Vector3(0.15f, 0.17f, 0.06f), eyeMat);
            MakePart(PrimitiveType.Sphere, "EyeR", headPivot.transform,
                new Vector3(0.12f, -0.03f, 0.32f), new Vector3(0.15f, 0.17f, 0.06f), eyeMat);
            MakePart(PrimitiveType.Sphere, "PupilL", headPivot.transform,
                new Vector3(-0.12f, -0.04f, 0.35f), new Vector3(0.09f, 0.11f, 0.02f), pupilMat);
            MakePart(PrimitiveType.Sphere, "PupilR", headPivot.transform,
                new Vector3(0.12f, -0.04f, 0.35f), new Vector3(0.09f, 0.11f, 0.02f), pupilMat);

            // ── 머리카락 (스타일 3종 단순 변주) ──
            BuildHair(headPivot.transform, a.hairStyle, hairMat);

            // ── 모자 ──
            if (a.hasHat)
            {
                Material hatMat = MakeMaterial(a.hat);
                MakePart(PrimitiveType.Cylinder, "Cap", headPivot.transform,
                    new Vector3(0f, 0.3f, -0.02f), new Vector3(0.30f, 0.12f, 0.30f), hatMat);
                MakePart(PrimitiveType.Cube, "CapBrim", headPivot.transform,
                    new Vector3(0f, 0.14f, 0.28f), new Vector3(0.28f, 0.03f, 0.14f), hatMat);
            }

            // ── 팔 ──
            MakePart(PrimitiveType.Capsule, "ArmL", root,
                new Vector3(-0.29f, 0.78f, 0f), new Vector3(0.135f, 0.23f, 0.135f), topMat);
            MakePart(PrimitiveType.Capsule, "ArmR", root,
                new Vector3(0.29f, 0.78f, 0f), new Vector3(0.135f, 0.23f, 0.135f), topMat);

            // ── 손 ──
            MakePart(PrimitiveType.Sphere, "HandL", root,
                new Vector3(-0.29f, 0.52f, 0f), new Vector3(0.115f, 0.115f, 0.115f), skinMat);
            MakePart(PrimitiveType.Sphere, "HandR", root,
                new Vector3(0.29f, 0.52f, 0f), new Vector3(0.115f, 0.115f, 0.115f), skinMat);

            // ── 다리 + 부츠 (LegPivot로 묶어 회전 시 발도 함께 — 플레이어와 동일 구조) ──
            BuildLeg(root, "L", -0.13f, bottomMat, shoesMat);
            BuildLeg(root, "R", 0.13f, bottomMat, shoesMat);

            // ── 아이 전용: 루트 스케일 축소 + 뜰채 ──
            if (a.isChild)
            {
                root.localScale = Vector3.one * 0.75f;

                Material netHandleMat = MakeMaterial(new Color(0.6f, 0.4f, 0.2f));
                Material netRingMat = MakeMaterial(new Color(0.95f, 0.92f, 0.88f));
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
        /// (PlayerVisualBuilder.SafeDestroyMat 패턴의 NPC판: NPC 파괴 시점엔 sharedMaterial
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

            MakePart(PrimitiveType.Capsule, $"Leg{side}", pivot.transform,
                new Vector3(0f, -0.14f, 0f), new Vector3(0.20f, 0.20f, 0.20f), bottomMat);
            MakePart(PrimitiveType.Cube, $"Boot{side}", pivot.transform,
                new Vector3(0f, -0.36f, 0.07f), new Vector3(0.21f, 0.15f, 0.30f), shoesMat);
        }

        private static void BuildHair(Transform headPivot, int style, Material hairMat)
        {
            // 공통: 정수리 덮개
            MakePart(PrimitiveType.Sphere, "HairTop", headPivot,
                new Vector3(0f, 0.22f, -0.02f), new Vector3(0.62f, 0.34f, 0.60f), hairMat);

            switch (style)
            {
                case 1: // 중간머리 — 옆/뒤 볼륨 추가
                    MakePart(PrimitiveType.Sphere, "HairSideL", headPivot,
                        new Vector3(-0.2f, 0.05f, -0.02f), new Vector3(0.12f, 0.2f, 0.35f), hairMat);
                    MakePart(PrimitiveType.Sphere, "HairSideR", headPivot,
                        new Vector3(0.2f, 0.05f, -0.02f), new Vector3(0.12f, 0.2f, 0.35f), hairMat);
                    MakePart(PrimitiveType.Sphere, "HairBack", headPivot,
                        new Vector3(0f, 0.08f, -0.15f), new Vector3(0.45f, 0.28f, 0.2f), hairMat);
                    break;
                case 2: // 올림머리 — 뒤통수 번(bun)
                    MakePart(PrimitiveType.Sphere, "HairBun", headPivot,
                        new Vector3(0f, 0.15f, -0.22f), new Vector3(0.22f, 0.22f, 0.22f), hairMat);
                    break;
                    // case 0: 짧은머리 — HairTop만
            }
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

        /// <summary>PlayerVisualBuilder.MakeMaterial 방식 복제 — Standard→URP→Unlit 폴백.</summary>
        private static Material MakeMaterial(Color color)
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
            return mat;
        }
    }
}
