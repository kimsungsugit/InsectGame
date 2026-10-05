#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 캐릭터 본체(몸통·팔·얼굴·머리) — 2026-09-30 개편에서 새로 생긴 **계약**을 고정한다.
    ///
    /// 모양 자체는 <c>OutfitRenderProbe -outfitLooks</c>로 눈으로 본다. 여기서는 모양이 틀려도
    /// 예외 없이 조용히 어긋나는 것들만 본다: 걷기 관절, 모자와의 간섭, 정점 예산, 표정의 구분.
    /// 외형은 <see cref="PlayerVisualBuilder.BuildForPreview"/>로 주입해 PlayerPrefs에 기대지 않는다.
    /// </summary>
    [TestFixture]
    public class CharacterBodyTests
    {
        private readonly List<GameObject> built = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in built) if (go != null) Object.DestroyImmediate(go);
            built.Clear();
        }

        private GameObject Build(int gender, int hairStyle, int faceType = 0)
        {
            GameObject go = new GameObject("CharacterBodyTests");
            go.SetActive(false);   // Awake 억제 — PlayerPrefs 외형으로 먼저 지어지지 않게
            go.AddComponent<PlayerVisualBuilder>().BuildForPreview(
                new AppearanceSpec { gender = gender, hairStyle = hairStyle, faceType = faceType });
            built.Add(go);
            return go;
        }

        private static IEnumerable<(int g, int h)> AllLooks()
        {
            for (int g = 0; g < 2; g++)
                for (int h = 0; h < 4; h++)
                    yield return (g, h);
        }

        // ── 팔 관절 ──

        /// <summary>
        /// 걷기(<c>PlayerMovement.AnimateWalk</c>)는 팔을 **제자리에서** 돌리고 손·도구를
        /// <c>RotateAttachment(쉬는 자리, arm.localPosition, 각)</c>로 따라 돌린다 — 팔의 원점이 곧 관절이다.
        /// 원점이 캡슐 가운데(y 0.78)였을 땐 팔 윗부분이 흔들 때마다 몸통 뒤로 빠졌다.
        /// 원점은 팔 윗부분에 있어야 하고, 쉬는 자세의 팔 범위(0.566~1.026)는 예전과 같아야 한다.
        /// </summary>
        [Test]
        public void Arm_PivotsAtShoulder_WithUnchangedRestShape()
        {
            GameObject go = Build(0, 0);
            foreach (string name in new[] { "ArmL", "ArmR" })
            {
                Transform arm = go.transform.Find(name);
                Assert.IsNotNull(arm, name);
                Bounds local = arm.GetComponent<MeshFilter>().sharedMesh.bounds;
                float top = arm.localPosition.y + local.max.y * arm.localScale.y;
                float bottom = arm.localPosition.y + local.min.y * arm.localScale.y;

                Assert.AreEqual(1.026f, top, 0.01f, name + " 윗끝이 바뀌었다");
                Assert.AreEqual(0.566f, bottom, 0.01f, name + " 아랫끝이 바뀌었다");
                Assert.Greater(arm.localPosition.y, 0.88f, name + "의 원점이 어깨가 아니다(가운데면 팔이 한가운데서 돈다)");
            }
        }

        /// <summary>손 쉬는 자리는 PlayerMovement·NpcWalkAnimator·도구 레시피가 상수(±0.29, 0.52)로 들고 있다.</summary>
        [Test]
        public void Hands_RestWhereTheWalkCycleExpectsThem()
        {
            GameObject go = Build(1, 2);
            Assert.AreEqual(new Vector3(-0.29f, 0.52f, 0f), go.transform.Find("HandL").localPosition);
            Assert.AreEqual(new Vector3(0.29f, 0.52f, 0f), go.transform.Find("HandR").localPosition);
        }

        /// <summary>
        /// 걷기에서 **몸통만** 위아래로 튄다(PlayerMovement bob). 몸 앞뒤에 붙은 것 — 셔츠 판·라펠·옷깃·가방 — 은
        /// 몸통의 자식이어야 함께 움직인다. 루트 자식이던 때는 걸을 때 셔츠 판과 가방이 몸통 앞뒤에서 미끄러졌다.
        /// </summary>
        [Test]
        public void TorsoAttachments_RideOnTheBody()
        {
            GameObject go = Build(0, 0);
            foreach (string name in new[] { "Shirt", "LapelL", "LapelR", "Collar", "Backpack" })
            {
                Transform t = OutfitShapeLibrary.FindDeep(go.transform, name);
                Assert.IsNotNull(t, name + "가 없다");
                Assert.AreEqual("Body", t.parent.name, name + "가 몸통의 자식이 아니다 — 걸을 때 몸통과 따로 논다");
            }
        }

        /// <summary>어깨 캡은 팔의 자식이어야 흔들어도 관절에 붙어 있다. 이름은 ApplyToCharacter가 칠하는 계약이다.</summary>
        [Test]
        public void ShoulderCaps_RideOnTheArms()
        {
            GameObject go = Build(0, 1);
            Assert.IsNotNull(go.transform.Find("ArmL/ShoulderL"));
            Assert.IsNotNull(go.transform.Find("ArmR/ShoulderR"));
        }

        // ── 모자와의 간섭 ──

        /// <summary>
        /// 모자 선 위로 솟는 머리는 HairCrown에만 둔다 — 모자 레시피의 hideNodes와 ApplyToCharacter가 이름으로 찾는다.
        /// 스타일과 무관하게 항상 있어야 한다(없으면 hideNodes가 조용히 무시된다).
        /// </summary>
        [Test]
        public void HairCrown_ExistsForEveryLook_AndHoldsTheUpdo()
        {
            foreach ((int g, int h) in AllLooks())
            {
                Transform crown = OutfitShapeLibrary.FindDeep(Build(g, h).transform, OutfitShapeLibrary.HairCrownNode);
                Assert.IsNotNull(crown, $"성별{g} 머리{h}: HairCrown이 없다");
                if (h == 3) Assert.Greater(crown.childCount, 0, $"성별{g} 올림머리: 솟는 부분이 HairCrown 밖에 있다 — 모자를 뚫는다");
            }
        }

        /// <summary>
        /// 정수리 덮개(HairTop)는 기본 캡(중심 y 0.24, 반지름 y 0.17 → 꼭대기 0.41) 안에 들어가야 한다 —
        /// 넘으면 모든 모자가 머리카락에 뚫린다. 크게 만든 여자 머리도 마찬가지다.
        /// </summary>
        [Test]
        public void HairTop_StaysUnderTheCap()
        {
            foreach ((int g, int h) in AllLooks())
            {
                Transform top = OutfitShapeLibrary.FindDeep(Build(g, h).transform, "HairTop");
                Assert.IsNotNull(top);
                float crownY = top.localPosition.y + top.localScale.y * 0.5f;
                Assert.LessOrEqual(crownY, 0.41f, $"성별{g} 머리{h}: 정수리 덮개 꼭대기 {crownY:0.000}");
            }
        }

        // ── 얼굴 ──

        /// <summary>네 표정이 서로 다른 입이어야 한다 — 예전엔 폭 0.02~0.05의 상자 하나라 화면에서 구분이 안 됐다.</summary>
        [Test]
        public void MouthShapes_AreAllDistinct()
        {
            HashSet<string> seen = new HashSet<string>();
            for (int f = 0; f < 4; f++)
            {
                PlayerVisualBuilder.MouthShape(f, out PlayerVisualBuilder.MouthKind kind, out Vector2 size, out float y);
                Assert.IsTrue(seen.Add($"{kind}:{size.x:0.000}x{size.y:0.000}"), $"표정 {f}의 입이 다른 표정과 같다");
                Assert.Less(y, -0.12f, $"표정 {f}: 입이 코(y −0.10) 위로 올라왔다");
            }
        }

        /// <summary>
        /// 얼굴 부품은 머리 표면 **앞**에 있어야 보인다. 옛 입은 z를 상수로 박아 머리가 큰 여자 얼굴에서 반쯤 파묻혔다.
        /// 입(노드 "Mouth")과 눈썹을 두 성별 모두 확인한다. 머리 반지름은 Head 노드 스케일에서 읽는다(지름 1 구).
        /// </summary>
        [Test]
        public void FaceParts_SitInFrontOfTheHeadSurface()
        {
            for (int g = 0; g < 2; g++)
            {
                for (int f = 0; f < 4; f++)
                {
                    Transform root = Build(g, 0, f).transform;
                    Transform head = OutfitShapeLibrary.FindDeep(root, "Head");
                    Vector3 r = head.localScale * 0.5f;
                    foreach (string part in new[] { "Mouth", "BrowL", "BrowR" })
                    {
                        Transform t = OutfitShapeLibrary.FindDeep(root, part);
                        Assert.IsNotNull(t, part);
                        Vector3 p = t.localPosition;
                        float u = p.x / r.x, v = p.y / r.y;
                        float surface = r.z * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u - v * v));
                        Assert.Greater(p.z, surface, $"성별{g} 표정{f}: {part}가 머리 속(z {p.z:0.000} ≤ 표면 {surface:0.000})");
                    }
                }
            }
        }

        [Test]
        public void BrowTone_IsDarkerThanTheHair()
        {
            for (int i = 0; i < CharacterPalette.HairCount; i++)
            {
                Color hair = CharacterPalette.Hair(i);
                Color brow = PlayerVisualBuilder.BrowTone(hair);
                Assert.Less(brow.grayscale, hair.grayscale + 0.05f, $"머리색 {i}: 눈썹이 머리보다 밝다");
            }
        }

        // ── 비용 ──

        /// <summary>
        /// 한 캐릭터의 정점 수. 에디터 로그(<c>LogVertexBudget</c>)는 경고만 하므로 여기서 상한을 건다.
        /// 프리미티브 시절 10,400 → 1,938로 줄였다가, 머리 스타일·표정·열린 자켓·잠자리채 그물을 살리며 4,200까지 허용했다
        /// (PlayerVisualBuilder.LogVertexBudget과 같은 값).
        /// </summary>
        [Test]
        public void EveryLook_StaysUnderTheVertexBudget()
        {
            foreach ((int g, int h) in AllLooks())
            {
                int total = 0;
                foreach (MeshFilter mf in Build(g, h, 1).GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) total += mf.sharedMesh.vertexCount;
                Assert.LessOrEqual(total, 4200, $"성별{g} 머리{h}: 정점 {total}");
            }
        }

        // ── 몸통 ──

        /// <summary>
        /// 몸통 위(어깨) 배율은 1이어야 한다 — 팔이 ±0.29에 묶여 있어 어깨가 좁아지면 팔과 몸 사이가 뜬다.
        /// 성별 실루엣 차이는 허리·골반에서 낸다.
        /// </summary>
        [Test]
        public void TorsoProfile_KeepsShouldersFull_AndDiffersByGender()
        {
            PlayerVisualBuilder.GetTorsoProfile(0, out float mb, out float mm, out float mt);
            PlayerVisualBuilder.GetTorsoProfile(1, out float fb, out float fm, out float ft);
            Assert.AreEqual(1f, mt, 1e-5f);
            Assert.AreEqual(1f, ft, 1e-5f);
            Assert.Less(fm, fb, "여자는 허리가 골반보다 좁아야 한다");
            Assert.Less(mb, mt, "남자는 허리 아래가 어깨보다 좁아야 한다");
        }
    }
}
#endif
