using System.Collections;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 이야기 전투의 등장·변신 연출 — ① 상대 교체 등장(1대1 팀 대결), ② 그림자 변신(레이드), ③ 수문장 등장(레이드 인트로),
    /// 그리고 모습을 빌리는 보스의 ④ 그림자 모습. 시각표·곡선·색은 <see cref="BattleStaging"/>(순수), 카메라 샷은
    /// <see cref="BattleCameraDirector"/>가 낸다. 여기는 그걸 몸·연기·빛·카메라에 건다.
    ///
    /// <b>연출은 한 번에 하나다.</b> 다른 연출(스킬·레이드 공격·예고·교체)이 시작되면 <see cref="CompleteStagingNow"/>가 지금 연출을
    /// 끝 상태로 즉시 접는다 — 공중에 뜬 몸을 "원위치"로 잡은 돌진이 나오거나, 바뀌기 전 모습으로 공격하는 일이 없게.
    /// 코루틴을 멈추면 <c>finally</c>가 돌지 않으므로(<c>SkillAttackCoroutine</c> 주석) 끝 상태는 따로 든 정리 함수가 만든다.
    ///
    /// <b>머티리얼</b>: 반투명·빛은 전부 <see cref="CreateFxMaterial"/>(빌드에 늘 있는 <c>Hidden/Internal-Colored</c> — 블렌드·컬링이
    /// 속성이라 셰이더 변형이 필요 없다)이고, 곤충 몸의 그림자 톤은 <b>PropertyBlock</b>으로만 건다(새 머티리얼·키워드 없음).
    /// 그래서 Standard의 반투명·발광 변형(<c>SceneryMaterials.BuildKeepers</c>)에 기대는 곳이 없다.
    /// </summary>
    public partial class BattleArenaController
    {
        private Coroutine stagingRoutine;
        private System.Action stagingFinish;
        private MaterialPropertyBlock stagingBlock;

        // ── 그림자 모습 ──
        /// <summary>이 레이드 보스가 「그림자」(모습을 빌리는 보스)면 원래 곤충 ID, 아니면 null.</summary>
        private string shadowBossId;
        /// <summary>그림자 톤을 입은 모델 → 덮는 세기. <see cref="SetFlash"/>가 쉬는 색을 여기서 낸다.</summary>
        private readonly Dictionary<GameObject, float> shadowStrength = new Dictionary<GameObject, float>();
        private Material shadowRimMat;
        private Material shadowEyeMat;
        private Material shadowLanternMat;
        private Coroutine shadowAuraRoutine;
        /// <summary>
        /// 발광 부위(반딧불이 발광기관 등) 렌더러 — 그림자 톤 대신 보랏빛으로 쉰다. 입힐 때 한 번 채운다
        /// (<c>Renderer.name</c>은 부를 때마다 문자열을 만든다 — <see cref="SetFlash"/>가 매 프레임 도는 동안 부르지 않는다).
        /// </summary>
        private readonly HashSet<MeshRenderer> shadowLanterns = new HashSet<MeshRenderer>();
        /// <summary>새 모습이 울부짖을 때 눈·테두리가 번쩍이는 몫(1 → 0으로 잦아든다).</summary>
        private float shadowEyeBoost;

        // 이름에 "Glow"가 들어가야 한다 — BattleFraming.ModelBounds가 그 이름을 경계에서 뺀다(카메라 구도·받침대 높이가 흔들리지 않게).
        private const string RimGlowName = "ShadowRimGlow";
        private const string EyeGlowName = "ShadowEyeGlow";
        private const string LanternGlowName = "ShadowLanternGlow";

        private static readonly Color RimColor = new Color(0.58f, 0.36f, 1f, 0.3f);
        private static readonly Color EyeGlowColor = new Color(0.86f, 0.62f, 1f, 0.75f);
        private static readonly Color LanternGlowColor = new Color(0.62f, 0.38f, 1f, 0.42f);
        private static readonly Color SmokeColor = new Color(0.07f, 0.035f, 0.11f, 0.85f);
        private static readonly Color WispColor = new Color(0.09f, 0.04f, 0.14f, 0.45f);
        private static readonly Color DustColor = new Color(0.56f, 0.49f, 0.38f, 0.55f);
        /// <summary>테두리 두께(월드 m) — 부위마다 이만큼 큰 뒤집힌 껍질을 씌운다.</summary>
        private const float RimPad = 0.03f;
        /// <summary>이보다 작은 부위(광택점·더듬이 끝)는 테두리를 씌우지 않는다 — 빛 덩어리가 된다.</summary>
        private const float RimMinPartSize = 0.06f;

        private sealed class EntranceState
        {
            public GameObject Model;
            public Pose3 Rest;
            public float FootY;
            public GameObject Player;
            public Vector3 PlayerFrom;
            public Vector3 PlayerTo;
            public readonly List<GameObject> Fx = new List<GameObject>();
        }

        private sealed class TransformState
        {
            public InsectData Form;
            public int Level;
            public bool Shiny;
            public bool Swapped;
            public Pose3 Rest;
            public float FootY;
            public readonly List<GameObject> Fx = new List<GameObject>();
        }

        private sealed class GuardianState
        {
            public GameObject Boss;
            public Pose3 Rest;
        }

        // ───────────── 연출 제어 ─────────────

        /// <summary>
        /// 지금 도는 등장·변신 연출을 <b>끝 상태로 즉시</b> 접는다(모델 교체·자세 복구·연기 정리·카메라 샷 해제). 돌고 있지 않으면 아무것도 안 한다.
        /// 다른 연출의 시작점(스킬·레이드 공격·예고·합체공격·내 곤충 교체)이 부른다.
        /// </summary>
        public void CompleteStagingNow()
        {
            if (stagingRoutine != null)
            {
                StopCoroutine(stagingRoutine);
                stagingRoutine = null;
            }
            System.Action finish = stagingFinish;
            stagingFinish = null;
            finish?.Invoke();
        }

        private void BeginStaging(IEnumerator routine, System.Action finish)
        {
            CompleteStagingNow();
            stagingFinish = finish;
            stagingRoutine = StartCoroutine(routine);
        }

        /// <summary>연출 코루틴이 끝까지 돌았다 — 정리 함수를 한 번 부른다.</summary>
        private void EndStaging()
        {
            stagingRoutine = null;
            System.Action finish = stagingFinish;
            stagingFinish = null;
            finish?.Invoke();
        }

        /// <summary>아레나 정리 — 코루틴은 <c>StopAllCoroutines</c>가, 오브젝트·머티리얼은 아레나 정리가 이미 치운다. 참조만 버린다.</summary>
        private void ClearStagingState()
        {
            stagingRoutine = null;
            stagingFinish = null;   // 부르지 않는다 — 고칠 모델이 이미 없다
            shadowAuraRoutine = null;
            shadowStrength.Clear();
            shadowLanterns.Clear();
            shadowBossId = null;
            shadowRimMat = null;
            shadowEyeMat = null;
            shadowLanternMat = null;
            shadowEyeBoost = 0f;
        }

        private void ApplyStagingShot(BattleCameraDirector.Shot shot)
        {
            if (BattlePresentation.ReducedMotion) return;   // 움직임 줄이기 — 구도 고정(흔들림도 CameraFollower가 끈다)
            CameraFollower follower = ResolveFollower();
            if (follower != null) follower.SetBattleShot(shot.Position, shot.Rotation, shot.Weight);
        }

        private MaterialPropertyBlock StagingBlock()
        {
            if (stagingBlock == null) stagingBlock = new MaterialPropertyBlock();
            stagingBlock.Clear();
            return stagingBlock;
        }

        private static Pose3 PoseOf(GameObject model)
        {
            return new Pose3
            {
                Position = model.transform.position,
                Rotation = model.transform.rotation,
                Scale = model.transform.localScale
            };
        }

        private static void SetPose(GameObject model, Pose3 pose)
        {
            if (model == null) return;
            model.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            model.transform.localScale = pose.Scale;
        }

        private static void DestroyAll(List<GameObject> objects)
        {
            for (int i = 0; i < objects.Count; i++)
                if (objects[i] != null) Destroy(objects[i]);
            objects.Clear();
        }

        // 결정적 난수 — 연출이 UnityEngine.Random을 소비하면 같은 시드의 QA 캡처가 다른 장면이 된다.
        private static float NextRand(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) * (1f / 16777216f);
        }

        // ───────────── ① 상대 교체 등장 ─────────────

        /// <summary>
        /// 팀 대결에서 상대가 다음 곤충을 내보낸다 — 새 곤충을 세우고(<see cref="RebuildEnemyInsect"/>, 받침·구도는 그쪽 그대로) 등장을 건다.
        /// <c>BattleScreenUI</c>의 교체 단계 시작이 부르고, <paramref name="duration"/>은 그 단계 길이(<c>EnemySwitchSeconds</c>)다.
        ///
        /// 0~0.34: 착지점에 빛살이 서고 카메라가 앞 구도에서 그쪽으로 옮겨 간다 · 0.12~0.52: 바깥 뒤쪽에서 포물선으로 뛰어든다 ·
        /// 0.52: 착지 — 먼지·땅 고리·빛살 퍼짐·흔들림·쿵·울음 · ~0.82: 눌렸다 펴지며 자세를 잡는다 · 0.72~0.94: 카메라가 새 구도로 풀린다.
        /// 내 곤충은 그대로 서 있다(새 상대 크기에 맞춰 벌어진 간격만큼 0.3 동안 미끄러진다). 움직임 줄이기면 제자리에 서 있고 먼지만 인다.
        /// </summary>
        public void PlayEnemySwitchIn(InsectData newInsect, int newLevel, bool shiny, float duration)
        {
            if (!isActive || arenaRoot == null || newInsect == null) return;
            CompleteStagingNow();
            RetireFaintShouts();   // 앞 곤충의 「털썩…」이 교체 배너(아래쪽 가운데) 자리에 남지 않게

            bool hadCam = hasBaseCam;
            Vector3 fromPos = baseCamPos;
            Quaternion fromRot = baseCamRot;
            GameObject player = playerModel;
            Vector3 playerFrom = player != null ? player.transform.position : playerBattlePos;

            RebuildEnemyInsect(newInsect, newLevel, shiny);
            if (enemyModel == null) return;
            if (!hadCam)
            {
                fromPos = baseCamPos;
                fromRot = baseCamRot;
            }

            var st = new EntranceState
            {
                Model = enemyModel,
                Rest = PoseOf(enemyModel),
                FootY = BattleFraming.ModelBounds(enemyModel).min.y,
                Player = player,
                PlayerFrom = playerFrom,
                PlayerTo = player != null ? player.transform.position : playerBattlePos
            };
            BeginStaging(EnemyEntranceCoroutine(st, Mathf.Max(0.4f, duration), fromPos, fromRot), () => FinishEntrance(st));
        }

        private IEnumerator EnemyEntranceCoroutine(EntranceState st, float duration, Vector3 fromPos, Quaternion fromRot)
        {
            GameObject model = st.Model;
            Pose3 rest = st.Rest;
            Bounds bounds = BattleFraming.ModelBounds(model);
            Vector3 landing = bounds.center;
            Vector3 opponent = st.Player != null ? BattleFraming.ModelBounds(st.Player).center : playerBattlePos;
            Vector3 away = rest.Position - st.PlayerTo;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.right;
            // 바깥(내 곤충 반대쪽)·뒤(카메라 반대쪽 +z)에서 땅 높이로 출발한다 — 화면 밖에서 뛰어든다.
            Vector3 launch = rest.Position + away * BattleStaging.EntranceLaunchOut + Vector3.forward * BattleStaging.EntranceLaunchBack;
            bool motion = !BattlePresentation.ReducedMotion;
            bool flashes = !BattlePresentation.ReducedFlashes;

            InsectEntity entity = model.GetComponent<InsectEntity>();
            InsectElement element = entity != null && entity.Data != null ? entity.Data.primaryType : InsectElement.Bug;
            Color beamColor = Color.Lerp(GetElementColor3D(element), Color.white, 0.55f);
            Vector3 floor = new Vector3(landing.x, arenaCenter.y + 0.06f, landing.z);

            // 착지점 빛살 — 새 곤충이 어디로 오는지 먼저 알린다. 가산 원기둥이라 밝은 섬광 줄이기면 없다.
            GameObject beam = null;
            Material beamMat = null;
            if (flashes)
            {
                beamMat = CreateFxMaterial(new Color(beamColor.r, beamColor.g, beamColor.b, 0f), true);
                beam = FxPrimitive(PrimitiveType.Cylinder, "EntranceBeam", floor + Vector3.up * 2.2f, beamMat);
                beam.transform.localScale = new Vector3(0.5f, 2.2f, 0.5f);   // 원기둥 높이 2 → 4.4m
                st.Fx.Add(beam);
            }

            bool landed = false;
            float t = 0f;
            while (true)
            {
                if (!isActive || arenaRoot == null || model == null) break;
                float p = Mathf.Clamp01(t / duration);

                if (st.Player != null && st.Player == playerModel)
                    st.Player.transform.position = Vector3.Lerp(st.PlayerFrom, st.PlayerTo, Mathf.SmoothStep(0f, 1f, p / 0.3f));
                if (motion) PoseEntrance(model, rest, launch, st.FootY, p);

                if (!landed && p >= BattleStaging.EntranceLand)
                {
                    landed = true;
                    EntranceLanding(model, floor, bounds, beamColor, flashes);
                }

                if (beam != null)
                {
                    float b = p / BattleStaging.EntranceBeamEnd;
                    if (b >= 1f)
                    {
                        st.Fx.Remove(beam);
                        Destroy(beam);
                        beam = null;
                    }
                    else
                    {
                        float width = Mathf.Lerp(0.55f, 0.12f, b);
                        beam.transform.localScale = new Vector3(width, 2.2f, width);
                        beamMat.color = new Color(beamColor.r, beamColor.g, beamColor.b, 0.7f * Mathf.Sin(b * Mathf.PI));
                    }
                }

                if (hasBaseCam)
                    ApplyStagingShot(BattleCameraDirector.EvaluateEntrance(p, duration, fromPos, fromRot,
                        baseCamPos, baseCamRot, landing, opponent));

                if (p >= 1f) break;
                yield return null;
                t += PresentationDelta;
            }
            EndStaging();
        }

        /// <summary>등장 자세 — 도약 전엔 숨기고, 도약 중엔 포물선·기울기, 착지 뒤엔 발을 땅에 붙인 채 눌렸다 편다.</summary>
        private static void PoseEntrance(GameObject model, Pose3 rest, Vector3 launch, float footY, float p)
        {
            if (p < BattleStaging.EntranceLeapStart)
            {
                model.transform.localScale = Vector3.zero;
                return;
            }
            if (p < BattleStaging.EntranceLand)
            {
                float u = (p - BattleStaging.EntranceLeapStart) / (BattleStaging.EntranceLand - BattleStaging.EntranceLeapStart);
                model.transform.position = BattleStaging.LeapPosition(launch, rest.Position, BattleStaging.EntranceApex, u);
                model.transform.rotation = rest.Rotation * Quaternion.Euler(BattleStaging.LeapPitch(u), 0f, 0f);
                model.transform.localScale = rest.Scale * Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, u / 0.3f));
                return;
            }
            float k = Mathf.Clamp01((p - BattleStaging.EntranceLand) / (BattleStaging.EntranceSettleEnd - BattleStaging.EntranceLand));
            float squash = BattleStaging.LandingSquash(k);
            float width = BattleStaging.SquashWidth(squash);
            Vector3 pos = rest.Position;
            pos.y -= (1f - squash) * (rest.Position.y - footY);   // 발은 땅에 붙인 채 눌린다
            model.transform.position = pos;
            model.transform.rotation = rest.Rotation * Quaternion.Euler(
                Mathf.Lerp(BattleStaging.LeapPitch(1f), 0f, Mathf.SmoothStep(0f, 1f, k / 0.6f)), 0f, 0f);
            model.transform.localScale = Vector3.Scale(rest.Scale, new Vector3(width, squash, width));
        }

        private void EntranceLanding(GameObject model, Vector3 floor, Bounds bounds, Color rayColor, bool flashes)
        {
            float footprint = Mathf.Max(0.35f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.7f);
            StartCoroutine(DustBurstCoroutine(floor, footprint, 10, 1.5f, 0.6f, 0.34f, 2654435761u));
            StartCoroutine(GroundRingCoroutine(floor + Vector3.up * 0.01f, footprint * 0.6f, footprint + 1.4f, 0.16f,
                DustColor, 0.45f, false));
            if (flashes) StartCoroutine(LightRaysCoroutine(floor + Vector3.up * 0.04f, rayColor, footprint + 1.1f));
            CameraFollower follower = ResolveFollower();
            if (follower != null) follower.Shake(0.14f, 0.24f);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySkillSFX(InsectElement.Earth);   // 쿵 — 흙 기술음이 가장 무겁게 떨어진다
                StartCoroutine(PlayCryDelayed(CryKey(CryOf(model, false), false), 0.12f));
            }
        }

        private void FinishEntrance(EntranceState st)
        {
            if (st.Model != null) SetPose(st.Model, st.Rest);
            if (st.Player != null && st.Player == playerModel) st.Player.transform.position = st.PlayerTo;
            DestroyAll(st.Fx);
            ClearCameraShot();
        }

        // ───────────── ② 그림자 변신 ─────────────

        /// <summary>
        /// 레이드 보스가 모습을 바꾼다 — 검은 연기가 감싸고, 한가운데(<see cref="BattleStaging.TransformSwap"/>)에서 모델을
        /// <paramref name="formInsect"/>로 갈아끼우고(<see cref="RebuildRaidBoss"/> — 받침대·오라도 그때 다시 선다, 연기 속이라 안 보인다),
        /// 연기가 걷히며 빌린 모습이 드러난다. <c>RaidBattleUI</c>의 변신 단계 시작이 부르고 <paramref name="duration"/>은 그 단계 길이다.
        ///
        /// 0~0.44: 연기·먹빛이 몸을 덮고 몸부림치며 움츠러든다(카메라가 다가붙는다) · 0.5: 교체 + 어두운 충격파·보랏빛 번쩍 ·
        /// 0.6: 새 모습이 펴지며 울부짖는다(눈빛 번쩍·흔들림·새 속성의 기술음) · ~0.86: 연기가 위로 흩어진다 · 0.72~0.94: 카메라가 풀린다.
        /// 그림자 보스(<see cref="BattleStaging.IsShadowBoss"/>)면 새 모습에 그림자 톤·테두리·눈빛을 입힌다(<see cref="ApplyShadowLook"/>).
        /// </summary>
        public void PlayBossTransform(InsectData formInsect, int level, bool shiny, float duration)
        {
            if (!isActive || arenaRoot == null || formInsect == null) return;
            if (bossModel == null)
            {
                RebuildRaidBoss(formInsect, level, shiny);
                return;
            }
            CompleteStagingNow();
            StopTelegraph(true);
            var st = new TransformState { Form = formInsect, Level = level, Shiny = shiny };
            BeginStaging(BossTransformCoroutine(st, Mathf.Max(0.4f, duration)), () => FinishTransform(st));
        }

        private IEnumerator BossTransformCoroutine(TransformState st, float duration)
        {
            GameObject boss = bossModel;
            st.Rest = PoseOf(boss);
            Bounds bounds = BattleFraming.ModelBounds(boss);
            st.FootY = bounds.min.y;
            Vector3 center = bounds.center;
            Vector3 cameraCenter = center;   // 카메라 기준점은 처음 한 번 — 모델이 바뀌어도 샷이 덜컹거리지 않게
            float radius = Mathf.Max(0.7f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            float height = Mathf.Max(0.8f, bounds.size.y);
            bool motion = !BattlePresentation.ReducedMotion;
            bool flashes = !BattlePresentation.ReducedFlashes;

            // 연기 — 한 머티리얼, 덩이마다 알파는 PropertyBlock.
            const int SmokeCount = 14;
            Material smokeMat = CreateFxMaterial(SmokeColor, false);
            var puffs = new MeshRenderer[SmokeCount];
            var angles = new float[SmokeCount];
            var layers = new float[SmokeCount];
            var sizes = new float[SmokeCount];
            uint seed = 40503u;
            for (int i = 0; i < SmokeCount; i++)
            {
                GameObject go = FxPrimitive(PrimitiveType.Sphere, "ShadowSmoke", center, smokeMat);
                go.transform.localScale = Vector3.zero;
                st.Fx.Add(go);
                puffs[i] = go.GetComponent<MeshRenderer>();
                angles[i] = i * Mathf.PI * 2f / SmokeCount + (NextRand(ref seed) - 0.5f) * 0.5f;
                layers[i] = 0.1f + 0.78f * ((i * 0.618f) % 1f);
                sizes[i] = 0.8f + 0.4f * NextRand(ref seed);
            }
            // 먹구슬 — 안쪽에서 부풀어 바꾸는 순간 몸을 다 가린다. 웅덩이 — 받침대 위로 번지는 그림자.
            Material coreMat = CreateFxMaterial(new Color(BattleStaging.ShadowInk.r, BattleStaging.ShadowInk.g, BattleStaging.ShadowInk.b, 0f), false);
            GameObject core = FxPrimitive(PrimitiveType.Sphere, "ShadowCore", center, coreMat);
            core.transform.localScale = Vector3.zero;
            st.Fx.Add(core);
            Material poolMat = CreateFxMaterial(new Color(0.05f, 0.02f, 0.08f, 0f), false);
            GameObject pool = FxPrimitive(PrimitiveType.Sphere, "ShadowPool", new Vector3(center.x, bossGroundY + 0.015f, center.z), poolMat);
            pool.transform.localScale = Vector3.zero;
            st.Fx.Add(pool);

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySkillSFX(InsectElement.Dark);

            bool roared = false;
            float t = 0f;
            while (true)
            {
                if (!isActive || arenaRoot == null) break;
                float p = Mathf.Clamp01(t / duration);

                if (!st.Swapped && p >= BattleStaging.TransformSwap)
                {
                    SwapBossForm(st);
                    if (bossModel != null)
                    {
                        Bounds swapped = BattleFraming.ModelBounds(bossModel);
                        center = swapped.center;
                        radius = Mathf.Max(radius, Mathf.Max(swapped.extents.x, swapped.extents.z));
                        height = Mathf.Max(height, swapped.size.y);
                    }
                    // 어두운 충격파 — 받침대 위로 퍼진다. 보랏빛 번쩍은 밝은 섬광 줄이기면 없다.
                    StartCoroutine(GroundRingCoroutine(new Vector3(center.x, bossGroundY + 0.03f, center.z), radius * 0.6f,
                        radius * 2.6f, 0.24f, new Color(0.12f, 0.05f, 0.2f, 0.85f), 0.45f, false));
                    if (flashes) StartCoroutine(ShadowFlashCoroutine(center, radius));
                }

                GameObject model = bossModel;
                if (model != null)
                {
                    if (motion) PoseTransform(model, st, p, t);
                    SetFlash(model, BattleStaging.ShadowInk, BattleStaging.TransformVeil(p));
                }

                if (!roared && p >= BattleStaging.TransformRoar)
                {
                    roared = true;
                    shadowEyeBoost = 1f;
                    CameraFollower follower = ResolveFollower();
                    if (follower != null) follower.Shake(0.2f, 0.35f);
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayCry(CryKey(BattleShout.Cry.Boss, false));
                        AudioManager.Instance.PlaySkillSFX(st.Form.primaryType);   // 바뀐 상성을 귀로도 — 새 모습의 속성음
                    }
                }

                float cover = BattleStaging.SmokeCover(p);
                UpdateSmoke(puffs, angles, layers, sizes, center, st.FootY, radius, height, cover, p, t);
                float coreSize = Mathf.Max(height, radius * 2f) * 1.15f * Mathf.Pow(cover, 0.7f);
                core.transform.position = center;
                core.transform.localScale = Vector3.one * coreSize;
                coreMat.color = new Color(BattleStaging.ShadowInk.r, BattleStaging.ShadowInk.g, BattleStaging.ShadowInk.b,
                    0.92f * cover * cover);
                float poolSize = radius * 2.6f * cover;
                // 받침대는 교체 때 새 몸 높이로 다시 선다(RebuildRaidBoss) — 웅덩이도 그 윗면을 따른다.
                pool.transform.position = new Vector3(center.x, bossGroundY + 0.015f, center.z);
                pool.transform.localScale = new Vector3(poolSize, 0.03f, poolSize);
                poolMat.color = new Color(0.05f, 0.02f, 0.08f, 0.75f * cover);

                if (hasBaseCam)
                    ApplyStagingShot(BattleCameraDirector.EvaluateBossTransform(p, duration, baseCamPos, baseCamRot, cameraCenter));

                if (p >= 1f) break;
                yield return null;
                t += PresentationDelta;
            }
            EndStaging();
        }

        /// <summary>변신 중 몸 — 발을 받침대에 붙인 채 움츠러들었다 펴지고, 삼켜지는 동안 몸부림친다.</summary>
        private static void PoseTransform(GameObject model, TransformState st, float p, float seconds)
        {
            float s = BattleStaging.TransformScale(p);
            Vector3 pos = st.Rest.Position;
            pos.y -= (1f - s) * (st.Rest.Position.y - st.FootY);
            if (p < BattleStaging.TransformSwap)
            {
                float k = Mathf.SmoothStep(0f, 1f, p / BattleStaging.TransformSwap);
                pos.x += Mathf.Sin(seconds * 47f) * 0.035f * k;
                pos.z += Mathf.Sin(seconds * 39f + 1f) * 0.02f * k;
            }
            model.transform.SetPositionAndRotation(pos, st.Rest.Rotation);
            model.transform.localScale = st.Rest.Scale * s;
        }

        private void UpdateSmoke(MeshRenderer[] puffs, float[] angles, float[] layers, float[] sizes, Vector3 center,
            float footY, float radius, float height, float cover, float p, float seconds)
        {
            float inner = radius * 0.55f;
            float r = p < BattleStaging.TransformSwap
                ? Mathf.Lerp(radius * 1.5f, inner, Mathf.SmoothStep(0f, 1f, p / BattleStaging.TransformEngulfEnd))
                : Mathf.Lerp(inner, radius * 1.9f, Mathf.SmoothStep(0f, 1f,
                    (p - BattleStaging.TransformSwap - 0.04f) / (BattleStaging.TransformRevealEnd - BattleStaging.TransformSwap - 0.04f)));
            float rise = p < BattleStaging.TransformSwap ? 0f
                : 1.1f * Mathf.SmoothStep(0f, 1f, (p - BattleStaging.TransformSwap) / (BattleStaging.TransformRevealEnd - BattleStaging.TransformSwap));
            float peak = Mathf.Max(radius * 1.05f, height * 0.5f);
            for (int i = 0; i < puffs.Length; i++)
            {
                MeshRenderer puff = puffs[i];
                if (puff == null) continue;
                float a = angles[i] + seconds * 1.0f * (i % 2 == 0 ? 1f : -1f);
                puff.transform.position = new Vector3(center.x + Mathf.Cos(a) * r,
                    footY + height * layers[i] + rise * (0.6f + 0.4f * sizes[i]), center.z + Mathf.Sin(a) * r);
                puff.transform.localScale = Vector3.one * (Mathf.Lerp(0.3f, peak, cover) * sizes[i]);
                MaterialPropertyBlock block = StagingBlock();
                block.SetColor("_Color", new Color(SmokeColor.r, SmokeColor.g, SmokeColor.b, SmokeColor.a * cover * (0.7f + 0.3f * sizes[i])));
                puff.SetPropertyBlock(block);
            }
        }

        private void SwapBossForm(TransformState st)
        {
            st.Swapped = true;
            RebuildRaidBoss(st.Form, st.Level, st.Shiny);
            if (bossModel == null) return;
            if (shadowBossId != null) ApplyShadowLook(bossModel, st.Form.insectId != shadowBossId);
            st.Rest = PoseOf(bossModel);
            st.FootY = BattleFraming.ModelBounds(bossModel).min.y;
        }

        private void FinishTransform(TransformState st)
        {
            if (!st.Swapped) SwapBossForm(st);
            if (bossModel != null)
            {
                SetPose(bossModel, st.Rest);
                SetFlash(bossModel, Color.white, 0f);   // 먹빛을 걷는다 — 그림자 보스면 그림자 톤으로 쉰다
            }
            DestroyAll(st.Fx);
            ClearCameraShot();
        }

        /// <summary>교체 순간의 보랏빛 번쩍 — 가산 구가 0.22초 만에 부풀며 사라진다.</summary>
        private IEnumerator ShadowFlashCoroutine(Vector3 center, float radius)
        {
            if (arenaRoot == null) yield break;
            Color glow = BattleStaging.ShadowGlow;
            Material mat = CreateFxMaterial(new Color(glow.r, glow.g, glow.b, 0.8f), true);
            GameObject flash = FxPrimitive(PrimitiveType.Sphere, "ShadowFlash", center, mat);
            const float Duration = 0.22f;
            float t = 0f;
            while (t < Duration)
            {
                if (arenaRoot == null || flash == null) yield break;
                t += PresentationDelta;
                float k = Mathf.Clamp01(t / Duration);
                flash.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 1.6f, 1f - (1f - k) * (1f - k));
                mat.color = new Color(glow.r, glow.g, glow.b, 0.8f * (1f - k));
                yield return null;
            }
            if (flash != null) Destroy(flash);
        }

        // ───────────── ③ 수문장 등장 ─────────────

        /// <summary>
        /// 수문장 레이드의 인트로 컷(초, <see cref="BattleStaging.GuardianIntroSeconds"/> 안). <see cref="SetupRaidBattle(InsectData,int,bool,InsectData[],int[],Vector3,bool[],string)"/>가
        /// 수문장일 때 부른다. 0~1.3: 낮은 데서 올려다보며 다가간다(수문장은 화면 위쪽 창 — 아래 1/3은 ui-dev 등장 배너 자리) ·
        /// 0.62: 포효(몸을 젖히며 솟고, 울고, 흔들리고, 받침대 위·아래에서 먼지가 일고, 포효 파동) · 1.3~1.85: 천천히 더 붙는다 ·
        /// 1.85~2.5: 원래 구도로 풀린다. 움직임 줄이기면 카메라·몸은 그대로고 먼지·소리만 난다.
        /// </summary>
        public void PlayGuardianIntro()
        {
            if (!isActive || arenaRoot == null || bossModel == null) return;
            var st = new GuardianState { Boss = bossModel, Rest = PoseOf(bossModel) };
            BeginStaging(GuardianIntroCoroutine(st), () => FinishGuardianIntro(st));
        }

        private IEnumerator GuardianIntroCoroutine(GuardianState st)
        {
            GameObject boss = st.Boss;
            Bounds bounds = BattleFraming.ModelBounds(boss);
            Vector3 toTeam = playerBattlePos - bounds.center;
            toTeam.y = 0f;
            toTeam = toTeam.sqrMagnitude > 0.0001f ? toTeam.normalized : Vector3.back;
            Vector3 rearAxis = Vector3.Cross(Vector3.up, toTeam);
            bool motion = !BattlePresentation.ReducedMotion;
            bool shot = TryGuardianShot(bounds, out Vector3 startPos, out Vector3 endPos, out Quaternion rotation);
            Camera fitted = Camera.main;
            float fittedFov = fitted != null ? fitted.fieldOfView : 0f;
            float fittedAspect = fitted != null ? fitted.aspect : 0f;

            bool roared = false;
            float t = 0f;
            while (true)
            {
                if (!isActive || arenaRoot == null || boss == null) break;
                // 화면비·FOV가 바뀌면(첫 프레임에 CameraFollower가 FOV를 맞추는 경우 포함) 그 화면으로 다시 맞춘다 —
                // 아레나 구도(SetupBattleCamera)도 같은 조건으로 다시 잡힌다(Update).
                Camera cam = Camera.main;
                if (cam != null && (Mathf.Abs(cam.fieldOfView - fittedFov) > 0.01f || Mathf.Abs(cam.aspect - fittedAspect) > 0.01f))
                {
                    fittedFov = cam.fieldOfView;
                    fittedAspect = cam.aspect;
                    shot = TryGuardianShot(bounds, out startPos, out endPos, out rotation);
                }
                if (motion)
                {
                    // 포효 — 몸을 뒤로 젖히며 솟는다(보스 공격 예고와 같은 축).
                    float k = BattleStaging.RoarPose(t);
                    boss.transform.SetPositionAndRotation(st.Rest.Position + Vector3.up * (0.28f * k),
                        Quaternion.AngleAxis(-15f * k, rearAxis) * st.Rest.Rotation);
                }
                if (!roared && t >= BattleStaging.GuardianRoarAt)
                {
                    roared = true;
                    GuardianRoar(boss, bounds, toTeam);
                }
                if (shot) ApplyStagingShot(BattleCameraDirector.EvaluateGuardianIntro(t, startPos, endPos, rotation));
                if (t >= BattleStaging.GuardianReleaseEnd) break;
                yield return null;
                // 화면의 인트로 시계(RaidBattleUI.GuardianIntroClockScale)와 같은 배율 — 2배속에서도 별칭·등장 한 줄을 읽는
                // 최소 실제 시간(BattleReadPacing.GuardianIntroMinSeconds)만큼 늘어나 포효의 「쾅」이 배너와 같은 박자에 온다.
                t += PresentationDelta * BattleReadPacing.ClockScale(BattleStaging.GuardianIntroSeconds,
                    BattleReadPacing.GuardianIntroMinSeconds, BattlePresentation.Speed);
            }
            EndStaging();
        }

        /// <summary>
        /// 수문장 샷 — 올려다보는 회전(<see cref="BattleStaging.GuardianLookUpPitch"/>)으로 수문장이 안전 영역의 위쪽 창
        /// (<see cref="BattleStaging.GuardianWindowBottom"/>~<see cref="BattleStaging.GuardianWindowTop"/>)을 채우는 끝 자리를 잡고,
        /// 거기서 물러난 출발 자리를 낸다. 카메라가 벽 밖에 서지 않게 벽을 넓히고 출발 자리를 벽 안쪽에서 자른다.
        /// </summary>
        private bool TryGuardianShot(Bounds bossBounds, out Vector3 startPos, out Vector3 endPos, out Quaternion rotation)
        {
            startPos = endPos = Vector3.zero;
            rotation = Quaternion.identity;
            Camera cam = Camera.main;
            if (cam == null || !hasBaseCam) return false;

            Rect safe = SafeViewport();
            Rect window = new Rect(safe.x, safe.y + safe.height * BattleStaging.GuardianWindowBottom, safe.width,
                safe.height * (BattleStaging.GuardianWindowTop - BattleStaging.GuardianWindowBottom));
            Bounds fit = bossBounds;
            fit.Expand(0.3f);
            float floorY = arenaCenter.y;
            BattleStaging.GuardianEndShot(fit, cam.aspect, cam.fieldOfView, baseCamRot.eulerAngles.y, window,
                playerBattlePos, floorY, out endPos, out Vector3 endTarget, out rotation);

            float span = Mathf.Max(CurrentWallSpan(), Mathf.Abs(endPos.x - arenaCenter.x) + 3f,
                Mathf.Abs(endPos.z - arenaCenter.z) + 3f);
            PlaceArenaWalls(span);
            startPos = BattleStaging.GuardianApproachStart(endPos, rotation, Vector3.Distance(endPos, endTarget),
                arenaCenter, span, floorY);
            return true;
        }

        private void GuardianRoar(GameObject boss, Bounds bounds, Vector3 toTeam)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayCry(CryKey(BattleShout.Cry.Boss, false));
            CameraFollower follower = ResolveFollower();
            if (follower != null) follower.Shake(0.26f, 0.5f);

            float footprint = Mathf.Max(0.5f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.7f);
            // 받침대 위 — 발밑 먼지가 가장자리 너머로 흩날린다(화면 위쪽 창 안이라 배너에 안 가린다).
            Vector3 top = new Vector3(bounds.center.x, bossGroundY + 0.05f, bounds.center.z);
            StartCoroutine(DustBurstCoroutine(top, footprint, 12, 1.7f, 0.7f, 0.45f, 2246822519u));
            // 받침대 아래 — 땅이 울린다.
            Transform rise = arenaRoot.transform.Find("BossRise");
            float riseRadius = rise != null ? Mathf.Max(rise.localScale.x, rise.localScale.z) * 0.5f : footprint + 1f;
            Vector3 floor = new Vector3(bounds.center.x, arenaCenter.y + 0.08f, bounds.center.z);
            StartCoroutine(DustBurstCoroutine(floor, riseRadius, 14, 2.2f, 0.8f, 0.55f, 3266489917u));
            StartCoroutine(GroundRingCoroutine(floor, riseRadius, riseRadius + 3.5f, 0.2f, DustColor, 0.6f, false));
            if (!BattlePresentation.ReducedFlashes)
                StartCoroutine(RoarWaveCoroutine(HeadPoint(boss) + toTeam * 0.3f,
                    Mathf.Max(bounds.extents.x, bounds.extents.y)));
        }

        private void FinishGuardianIntro(GuardianState st)
        {
            if (st.Boss != null && st.Boss == bossModel) SetPose(st.Boss, st.Rest);
            ClearCameraShot();
        }

        /// <summary>포효 파동 — 머리에서 카메라를 향한 고리 둘이 0.12초 간격으로 퍼진다(가산, 옅은 보랏빛 흰색).</summary>
        private IEnumerator RoarWaveCoroutine(Vector3 point, float size)
        {
            if (arenaRoot == null) yield break;
            Camera cam = Camera.main;
            Vector3 toCam = cam != null ? (cam.transform.position - point).normalized : Vector3.back;
            Color pale = new Color(0.92f, 0.88f, 1f);
            Material mat = CreateFxMaterial(new Color(pale.r, pale.g, pale.b, 0f), true);
            var rings = new LineRenderer[2];
            for (int i = 0; i < rings.Length; i++)
            {
                GameObject go = new GameObject("RoarWave");
                go.transform.SetParent(arenaRoot.transform, false);
                go.transform.position = point;
                go.transform.rotation = Quaternion.LookRotation(-toCam);
                rings[i] = go.AddComponent<LineRenderer>();
                rings[i].useWorldSpace = false;
                rings[i].loop = true;
                rings[i].positionCount = 40;
                rings[i].sharedMaterial = mat;
                rings[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rings[i].receiveShadows = false;
                rings[i].widthMultiplier = 0f;
            }
            const float Each = 0.5f;
            const float Stagger = 0.12f;
            float t = 0f;
            while (t < Each + Stagger)
            {
                if (arenaRoot == null) yield break;
                t += PresentationDelta;
                float alpha = 0f;
                for (int i = 0; i < rings.Length; i++)
                {
                    if (rings[i] == null) continue;
                    float k = Mathf.Clamp01((t - i * Stagger) / Each);
                    if (k <= 0f) continue;
                    float radius = Mathf.Lerp(0.4f, size * 2.4f, 1f - (1f - k) * (1f - k));
                    SetFacingRingRadius(rings[i], radius, Mathf.Lerp(0.14f, 0.01f, k));
                    alpha = Mathf.Max(alpha, 0.6f * (1f - k));
                }
                mat.color = new Color(pale.r, pale.g, pale.b, alpha);
                yield return null;
            }
            for (int i = 0; i < rings.Length; i++)
                if (rings[i] != null) Destroy(rings[i].gameObject);
        }

        private static void SetFacingRingRadius(LineRenderer ring, float radius, float width)
        {
            ring.widthMultiplier = width;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        // ───────────── 공용 연출 부품 ─────────────

        /// <summary>
        /// 먼지 — 땅(<paramref name="ground"/>)에서 둥글게 퍼지며 조금 솟고 옅어진다. 반투명(알파 블렌드) 덩이 하나의 머티리얼을 함께 쓴다.
        /// </summary>
        private IEnumerator DustBurstCoroutine(Vector3 ground, float startRadius, int count, float spread, float duration,
            float size, uint seed)
        {
            if (arenaRoot == null) yield break;
            Material mat = CreateFxMaterial(DustColor, false);
            var puffs = new GameObject[count];
            var dirs = new Vector3[count];
            var scales = new float[count];
            for (int i = 0; i < count; i++)
            {
                float a = (i + NextRand(ref seed) * 0.6f) * Mathf.PI * 2f / count;
                dirs[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                scales[i] = size * (0.75f + 0.5f * NextRand(ref seed));
                puffs[i] = FxPrimitive(PrimitiveType.Sphere, "Dust", ground + dirs[i] * startRadius, mat);
                puffs[i].transform.localScale = Vector3.one * (scales[i] * 0.5f);
            }
            float t = 0f;
            while (t < duration)
            {
                if (arenaRoot == null) yield break;
                t += PresentationDelta;
                float k = Mathf.Clamp01(t / duration);
                float ease = 1f - (1f - k) * (1f - k);
                for (int i = 0; i < count; i++)
                {
                    if (puffs[i] == null) continue;
                    puffs[i].transform.position = ground + dirs[i] * (startRadius + spread * ease) + Vector3.up * (0.06f + 0.3f * ease);
                    puffs[i].transform.localScale = Vector3.one * (scales[i] * Mathf.Lerp(0.5f, 1.4f, ease));
                }
                mat.color = new Color(DustColor.r, DustColor.g, DustColor.b, DustColor.a * Mathf.Pow(1f - k, 1.5f));
                yield return null;
            }
            for (int i = 0; i < count; i++)
                if (puffs[i] != null) Destroy(puffs[i]);
        }

        /// <summary>땅 고리 — 바닥에 누운 고리가 <paramref name="from"/>에서 <paramref name="to"/>로 퍼지며 가늘어지고 사라진다.</summary>
        private IEnumerator GroundRingCoroutine(Vector3 ground, float from, float to, float width, Color color,
            float duration, bool additive)
        {
            if (arenaRoot == null) yield break;
            Material mat = CreateFxMaterial(color, additive);
            LineRenderer ring = CreateFloorRing("StagingRing", ground, mat);
            float t = 0f;
            while (t < duration)
            {
                if (arenaRoot == null || ring == null) yield break;
                t += PresentationDelta;
                float k = Mathf.Clamp01(t / duration);
                float ease = 1f - (1f - k) * (1f - k);
                SetRingRadius(ring, Mathf.Lerp(from, to, ease), Mathf.Lerp(width, 0.02f, k));
                mat.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }

        /// <summary>착지 빛살 — 바닥을 따라 여덟 갈래로 짧게 뻗었다 사라진다(가산). 치명타 별 빛살(화면을 향한 넷)과 모양을 갈랐다.</summary>
        private IEnumerator LightRaysCoroutine(Vector3 origin, Color color, float length)
        {
            if (arenaRoot == null) yield break;
            Material mat = CreateFxMaterial(new Color(color.r, color.g, color.b, 0.9f), true);
            const int Count = 8;
            var rays = new GameObject[Count];
            var dirs = new Vector3[Count];
            for (int i = 0; i < Count; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / Count;
                dirs[i] = new Vector3(Mathf.Cos(a), 0.16f, Mathf.Sin(a)).normalized;
                rays[i] = FxPrimitive(PrimitiveType.Cube, "LandingRay", origin, mat);
                rays[i].transform.rotation = Quaternion.LookRotation(dirs[i]);
                rays[i].transform.localScale = Vector3.zero;
            }
            const float Duration = 0.36f;
            float t = 0f;
            while (t < Duration)
            {
                if (arenaRoot == null) yield break;
                t += PresentationDelta;
                float k = Mathf.Clamp01(t / Duration);
                float grow = 1f - (1f - Mathf.Clamp01(k / 0.35f)) * (1f - Mathf.Clamp01(k / 0.35f));
                float len = length * grow;
                float thick = Mathf.Lerp(0.07f, 0.02f, k);
                for (int i = 0; i < Count; i++)
                {
                    if (rays[i] == null) continue;
                    rays[i].transform.position = origin + dirs[i] * (len * 0.5f + 0.15f);
                    rays[i].transform.localScale = new Vector3(thick, thick, len * ((i % 2) == 0 ? 1f : 0.65f));
                }
                mat.color = new Color(color.r, color.g, color.b, 0.9f * (1f - k * k));
                yield return null;
            }
            for (int i = 0; i < Count; i++)
                if (rays[i] != null) Destroy(rays[i]);
        }

        // ───────────── ④ 그림자 모습 ─────────────

        /// <summary>
        /// 그림자 모습을 입힌다 — 몸은 검보라 톤(PropertyBlock — 머티리얼을 새로 만들지 않는다), 부위마다 보랏빛 테두리(뒤집힌 껍질),
        /// 눈·발광기관은 보랏빛으로 빛나고, 몸 둘레로 그림자 김이 피어오른다(<see cref="ShadowAuraCoroutine"/>).
        /// <paramref name="borrowed"/>(빌린 모습)면 더 덮는다 — 진짜 호랑나비·반딧불이와 확실히 갈리게.
        /// </summary>
        private void ApplyShadowLook(GameObject model, bool borrowed)
        {
            if (model == null) return;
            MeshRenderer[] parts = model.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null && IsLanternPart(parts[i].name)) shadowLanterns.Add(parts[i]);
            AddShadowRims(parts);
            AddShadowGlows(parts);
            rendererCache.Remove(model);   // 새로 단 빛 부위까지 캐시에 담기게(쓰러짐 페이드가 함께 지운다)
            shadowStrength[model] = borrowed ? BattleStaging.ShadowBorrowedStrength : BattleStaging.ShadowOriginalStrength;
            SetFlash(model, Color.white, 0f);   // 쉬는 색 = 그림자 톤
            if (shadowAuraRoutine == null && isActiveAndEnabled) shadowAuraRoutine = StartCoroutine(ShadowAuraCoroutine());
        }

        private static bool IsStagingGlow(string partName)
        {
            return partName == RimGlowName || partName == EyeGlowName || partName == LanternGlowName;
        }

        /// <summary>그림자 빛 부위(테두리·눈빛·발광기관 빛)인가 — 머티리얼 참조로 가른다(이름을 읽지 않는다, 매 프레임 불린다).</summary>
        private bool IsStagingGlow(MeshRenderer renderer)
        {
            Material mat = renderer.sharedMaterial;
            return mat != null && (ReferenceEquals(mat, shadowRimMat) || ReferenceEquals(mat, shadowEyeMat)
                || ReferenceEquals(mat, shadowLanternMat));
        }

        private static bool IsEyePart(string partName)
        {
            return partName.Contains("Eye") && !partName.Contains("Spot") && !partName.Contains("Glint")
                && !partName.Contains("Highlight") && !IsStagingGlow(partName);
        }

        /// <summary>반딧불이 발광기관처럼 원래 빛나는 부위 — 어둡게 덮지 않고 보랏빛으로 바꾼다.</summary>
        private static bool IsLanternPart(string partName)
        {
            return partName == "LightOrgan" || partName.StartsWith("GlowOuter") || partName.StartsWith("GlowPulse");
        }

        // 무늬·광택점·효과 부위는 테두리를 씌우지 않는다 — 몸 안쪽에 빛 줄이 생긴다.
        private static bool SkipRim(string partName)
        {
            return partName.StartsWith("Spot") || partName.Contains("EyeSpot") || partName.Contains("Glint")
                || partName.Contains("Highlight") || partName.StartsWith("Vein") || partName.Contains("Sparkle")
                || partName.Contains("Aura") || partName.Contains("Ring") || partName.StartsWith("Stripe")
                || partName.Contains("Marker") || IsEyePart(partName) || IsLanternPart(partName) || IsStagingGlow(partName);
        }

        private static float RimScale(float size)
        {
            return Mathf.Clamp(1f + 2f * RimPad / Mathf.Max(0.001f, size), 1.02f, 1.8f);
        }

        private Color ShadowRestColor(MeshRenderer renderer, Color color, float strength)
        {
            return shadowLanterns.Contains(renderer) ? BattleStaging.ShadowGlowTint(color) : BattleStaging.ShadowTint(color, strength);
        }

        private Material ShadowRimMaterial()
        {
            if (shadowRimMat != null) return shadowRimMat;
            Material mat = CreateFxMaterial(RimColor, true);
            // 뒤집힌 껍질 — 몸보다 조금 큰 복제의 뒷면만 그린다. 몸의 앞면이 가운데를 가려 가장자리만 빛난다.
            // 컬링이 속성인 셰이더(Hidden/Internal-Colored)에서만 — 대체 셰이더(Sprites/Default)는 양면이라 몸 전체가 뿌옇게 덮인다.
            if (!mat.HasProperty("_Cull")) return null;
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
            shadowRimMat = mat;
            return shadowRimMat;
        }

        private void AddShadowRims(MeshRenderer[] parts)
        {
            Material rim = ShadowRimMaterial();
            if (rim == null) return;
            for (int i = 0; i < parts.Length; i++)
            {
                MeshRenderer part = parts[i];
                if (part == null || SkipRim(part.name)) continue;
                MeshFilter filter = part.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Material shared = part.sharedMaterial;
                // 반투명 막(날개·오라)은 테두리를 씌우지 않는다 — 막 너머로 껍질 뒷면이 비쳐 날개 전체가 빛난다.
                if (shared != null && shared.HasProperty("_Color") && shared.color.a < 0.95f) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                Vector3 lossy = part.transform.lossyScale;
                Vector3 size = new Vector3(Mathf.Abs(mesh.size.x * lossy.x), Mathf.Abs(mesh.size.y * lossy.y),
                    Mathf.Abs(mesh.size.z * lossy.z));
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < RimMinPartSize) continue;

                // 축마다 월드 두께 RimPad만큼 키우고, 메시 중심을 축으로 키운다(축이 중심에서 벗어난 조각 메시도 고르게 두른다).
                Vector3 scale = new Vector3(RimScale(size.x), RimScale(size.y), RimScale(size.z));
                var go = new GameObject(RimGlowName);
                go.transform.SetParent(part.transform, false);
                go.transform.localPosition = Vector3.Scale(mesh.center, Vector3.one - scale);
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer hull = go.AddComponent<MeshRenderer>();
                hull.sharedMaterial = rim;
                hull.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                hull.receiveShadows = false;
            }
        }

        private void AddShadowGlows(MeshRenderer[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                MeshRenderer part = parts[i];
                if (part == null) continue;
                bool eye = IsEyePart(part.name);
                bool lantern = part.name == "LightOrgan";
                if (!eye && !lantern) continue;
                if (eye && shadowEyeMat == null) shadowEyeMat = CreateFxMaterial(EyeGlowColor, true);
                if (lantern && shadowLanternMat == null) shadowLanternMat = CreateFxMaterial(LanternGlowColor, true);
                GameObject glow = FxPrimitive(PrimitiveType.Sphere, eye ? EyeGlowName : LanternGlowName, part.transform.position,
                    eye ? shadowEyeMat : shadowLanternMat);
                glow.transform.SetParent(part.transform, false);
                MeshFilter filter = part.GetComponent<MeshFilter>();
                glow.transform.localPosition = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.center : Vector3.zero;
                glow.transform.localRotation = Quaternion.identity;
                glow.transform.localScale = Vector3.one * (eye ? 1.75f : 1.3f);
            }
        }

        /// <summary>
        /// 쓰러짐 직전 — 그림자 톤을 인스턴스 머티리얼에 굽고 PropertyBlock을 걷는다. 쓰러짐 페이드는 머티리얼 색의 알파를
        /// 내리는데 PropertyBlock의 색이 그걸 덮어 그림자 보스만 투명해지지 않았다.
        /// </summary>
        private void BakeShadowTint(GameObject model)
        {
            if (model == null || !shadowStrength.TryGetValue(model, out float strength)) return;
            MeshRenderer[] renderers = GetRenderersCached(model);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null || IsStagingGlow(r)) continue;
                Material shared = r.sharedMaterial;
                if (shared == null || !shared.HasProperty("_Color")) continue;
                Color rest = ShadowRestColor(r, shared.color, strength);
                r.SetPropertyBlock(null);
                r.material.color = rest;   // 쓰러짐이 어차피 인스턴스를 쓴다(PlayFaintCoroutine)
            }
            shadowStrength.Remove(model);
        }

        /// <summary>
        /// 그림자 김과 숨결 — 보스 몸 둘레에서 검보라 김이 피어올라 흩어지고(여섯 줄기, 1.8초 주기), 테두리·눈빛이 천천히 맥동한다.
        /// 변신 직후엔 눈이 번쩍인다(<see cref="shadowEyeBoost"/>). 보스가 쓰러지면(그림자 톤을 구웠다) 새 김을 피우지 않는다.
        /// </summary>
        private IEnumerator ShadowAuraCoroutine()
        {
            const int WispCount = 6;
            const float WispLife = 1.8f;
            Material wispMat = CreateFxMaterial(WispColor, false);
            var wisps = new MeshRenderer[WispCount];
            var age = new float[WispCount];
            var offset = new Vector3[WispCount];
            var size = new float[WispCount];
            for (int i = 0; i < WispCount; i++)
            {
                GameObject go = FxPrimitive(PrimitiveType.Sphere, "ShadowWisp", arenaCenter, wispMat);
                go.transform.localScale = Vector3.zero;
                wisps[i] = go.GetComponent<MeshRenderer>();
                age[i] = WispLife - i * (WispLife / WispCount);   // 첫 줄기는 곧바로, 나머지는 0.3초 간격으로 흩어 피운다
            }

            GameObject measured = null;
            Vector3 centerOffset = Vector3.zero;
            Vector3 extents = Vector3.one * 0.5f;
            uint seed = 97531u;
            float t = 0f;
            while (arenaRoot != null)
            {
                float dt = PresentationDelta;
                t += dt;
                GameObject boss = bossModel;
                bool live = boss != null && boss.activeInHierarchy && shadowStrength.ContainsKey(boss);
                if (live && boss != measured)
                {
                    measured = boss;
                    Bounds b = BattleFraming.ModelBounds(boss);
                    centerOffset = b.center - boss.transform.position;
                    extents = b.extents;
                }

                shadowEyeBoost = Mathf.Max(0f, shadowEyeBoost - dt * 1.4f);
                float breath = 0.5f + 0.5f * Mathf.Sin(t * 2.3f);
                if (shadowRimMat != null)
                    shadowRimMat.color = new Color(RimColor.r, RimColor.g, RimColor.b,
                        Mathf.Clamp01(Mathf.Lerp(0.22f, 0.38f, breath) + 0.35f * shadowEyeBoost));
                if (shadowEyeMat != null)
                    shadowEyeMat.color = new Color(EyeGlowColor.r, EyeGlowColor.g, EyeGlowColor.b,
                        Mathf.Clamp01(Mathf.Lerp(0.55f, 0.85f, breath) + 0.4f * shadowEyeBoost));
                if (shadowLanternMat != null)
                    shadowLanternMat.color = new Color(LanternGlowColor.r, LanternGlowColor.g, LanternGlowColor.b,
                        Mathf.Clamp01(Mathf.Lerp(0.3f, 0.5f, breath) + 0.3f * shadowEyeBoost));

                for (int i = 0; i < WispCount; i++)
                {
                    MeshRenderer wisp = wisps[i];
                    if (wisp == null) continue;
                    age[i] += dt;
                    if (age[i] >= WispLife && live)
                    {
                        age[i] = 0f;
                        float a = NextRand(ref seed) * Mathf.PI * 2f;
                        float r = Mathf.Max(extents.x, extents.z) * Mathf.Lerp(0.45f, 0.85f, NextRand(ref seed));
                        offset[i] = centerOffset + new Vector3(Mathf.Cos(a) * r,
                            extents.y * Mathf.Lerp(-0.6f, 0.35f, NextRand(ref seed)), Mathf.Sin(a) * r);
                        size[i] = Mathf.Max(extents.x, extents.y) * Mathf.Lerp(0.22f, 0.36f, NextRand(ref seed));
                    }
                    if (age[i] < 0f || age[i] >= WispLife || boss == null || size[i] <= 0f)
                    {
                        wisp.transform.localScale = Vector3.zero;
                        continue;
                    }
                    float k = age[i] / WispLife;
                    wisp.transform.position = boss.transform.position + offset[i] + Vector3.up * (0.9f * k);
                    wisp.transform.localScale = Vector3.one * (size[i] * (0.5f + 0.8f * k));
                    MaterialPropertyBlock block = StagingBlock();
                    block.SetColor("_Color", new Color(WispColor.r, WispColor.g, WispColor.b, WispColor.a * Mathf.Sin(k * Mathf.PI)));
                    wisp.SetPropertyBlock(block);
                }
                yield return null;
            }
            shadowAuraRoutine = null;
        }

        // ───────────── 구도 보조 ─────────────

        /// <summary>안전 영역의 뷰포트 사각형(노치·제스처바를 뺀 0~1 좌표).</summary>
        private static Rect SafeViewport()
        {
            Rect safe = Screen.safeArea;
            return safe.width > 0f && safe.height > 0f
                ? new Rect(safe.x / Mathf.Max(1, Screen.width), safe.y / Mathf.Max(1, Screen.height),
                    safe.width / Mathf.Max(1, Screen.width), safe.height / Mathf.Max(1, Screen.height))
                : new Rect(0f, 0f, 1f, 1f);
        }

        /// <summary>지금 경계벽까지의 거리 — 구도가 벽을 밀어냈으면 그 값.</summary>
        private float CurrentWallSpan()
        {
            Transform wall = arenaRoot != null ? arenaRoot.transform.Find("WallN") : null;
            return wall != null ? Mathf.Abs(wall.localPosition.z) : ArenaWallSpan;
        }

        /// <summary>경계벽을 <paramref name="span"/>에 세운다 — 카메라가 벽 밖에 서면 벽의 바깥 면이 화면을 막는다(<see cref="ArenaWallSpan"/> 주석).</summary>
        private void PlaceArenaWalls(float span)
        {
            if (arenaRoot == null) return;
            foreach (string wallName in new[] { "WallN", "WallS", "WallE", "WallW" })
            {
                Transform wall = arenaRoot.transform.Find(wallName);
                if (wall == null) continue;
                bool horizontal = wallName == "WallN" || wallName == "WallS";
                float sign = wallName == "WallN" || wallName == "WallE" ? 1f : -1f;
                wall.localPosition = horizontal ? new Vector3(0f, ArenaWallHeight * 0.5f, span * sign)
                    : new Vector3(span * sign, ArenaWallHeight * 0.5f, 0f);
                wall.localScale = horizontal ? new Vector3(span * 2f, ArenaWallHeight, 0.4f)
                    : new Vector3(0.4f, ArenaWallHeight, span * 2f);
            }
        }
    }
}
