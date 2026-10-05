using System.Collections;
using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 살아 있는 몸 — 대기 <b>숨쉬기</b>(몸통이 부풀었다 줄고 더듬이가 까딱인다)와 상태이상·강화의 <b>몸 표시</b>, 회복 반짝임.
    ///
    /// <list type="bullet">
    /// <item>독 — 몸 위로 보라 거품이 보글보글 올라 톡 터진다.</item>
    /// <item>기절 — 머리 위에서 노란 별 셋이 빙빙 돈다.</item>
    /// <item>공격 강화 — 발밑에서 붉은 주황 불꽃결이 위로 흐르고 발밑 고리가 은은히 빛난다.</item>
    /// <item>방어 강화 — 푸른 육각 막이 몸을 감싸고 천천히 돌며 띠처럼 일렁인다.</item>
    /// <item>공격·방어 약화 — 같은 색의 어두운 결이 머리 위에서 발밑으로 흘러내린다.</item>
    /// <item>회복 — 그 순간 초록 반짝임이 위로 솟는다(한 번).</item>
    /// </list>
    /// 자리가 겹쳐도 읽히게 나눴다 — 별은 머리 위, 거품은 몸 위, 오라·결은 발밑~몸 둘레, 막은 몸 바깥.
    ///
    /// <b>원천은 컨트롤러다</b> — 0.12초마다 읽는다(1대1 <c>PlayerStunTurns</c>·<c>GetActiveEffects</c>, 레이드 <c>AttackStacks</c>·<c>DefenseStacks</c>·
    /// 보스 기절은 건너뛴 응답). 판정은 <see cref="BattleStatusLook"/>(순수)이고 HP 카드 상태 줄과 같은 규칙이다. 컨트롤러는
    /// <see cref="AutoWire(InsectBattleController)"/>·<see cref="AutoWire(RaidBattleController)"/>로 받고, 비어 있으면 전투마다 한 번 찾는다.
    ///
    /// <b>머티리얼</b>: 전부 <see cref="CreateFxMaterial"/>(빌드에 늘 있는 FX 셰이더)를 종류마다 한 벌 만들어 나눠 쓰고 알파는 PropertyBlock으로 —
    /// Standard 변형에 기대지 않는다. 몸 표시는 모델 <b>밖</b>(아레나 루트 아래)에 두고 매 프레임 몸을 따라간다 — 모델 안에 두면 돌진·피격의
    /// 기울기를 같이 타고, 쓰러짐 페이드·카메라 구도(<c>BattleFraming.ModelBounds</c>)에 섞인다.
    /// 숨쉬기는 몸통 부위(<c>Abdomen</c>·<c>Body</c>·마디)의 <b>크기</b>와 더듬이 부위의 자세만 바꾼다 — 모델 루트는 돌진·피격·등장 연출의 몫이다.
    /// </summary>
    public partial class BattleArenaController
    {
        private InsectBattleController duelStatusSource;
        private RaidBattleController raidStatusSource;
        private bool statusSourcesSearched;

        /// <summary>몸 상태를 읽을 1대1 컨트롤러. 안 주면 전투가 열릴 때 한 번 찾는다.</summary>
        public void AutoWire(InsectBattleController duel)
        {
            if (duel != null) duelStatusSource = duel;
        }

        /// <summary>몸 상태를 읽을 레이드 컨트롤러. 안 주면 전투가 열릴 때 한 번 찾는다.</summary>
        public void AutoWire(RaidBattleController raid)
        {
            if (raid != null) raidStatusSource = raid;
        }

        /// <summary>상태를 읽는 간격(실제 초) — <c>GetActiveEffects</c>가 배열을 새로 만들어 매 프레임 부르지 않는다.</summary>
        private const float StatusPollSeconds = 0.12f;
        /// <summary>레이드 회복 반짝임이 타격 순간을 기다리는 상한(초) — 그 사이 연출이 없으면(아이템 등) 그냥 띄운다.</summary>
        private const float HealFlushTimeout = 1.2f;
        /// <summary>상태 표시가 켜지고 꺼지는 속도(초당 몫) — 0.2초에 걸쳐 나타나고 사라진다.</summary>
        private const float StatusFadeRate = 5f;
        private const int StatusLayerCount = 6;   // BattleStatusFlags의 비트 순서

        private float lifeClock;
        private float statusPollTimer;
        private readonly Dictionary<GameObject, LifeRig> lifeRigs = new Dictionary<GameObject, LifeRig>();
        private readonly List<GameObject> lifeModels = new List<GameObject>(8);
        private readonly List<GameObject> lifeStale = new List<GameObject>(4);
        private readonly HashSet<GameObject> faintingModels = new HashSet<GameObject>();
        private readonly Dictionary<int, Material> statusMats = new Dictionary<int, Material>();
        private MaterialPropertyBlock lifeBlock;
        private Mesh starMesh;
        private Mesh flameMesh;

        private sealed class LifeRig
        {
            public GameObject Model;
            public float Phase;
            public int Salt;
            public bool Boss;
            public Transform[] Breath;
            public Vector3[] BreathBase;
            public float[] BreathLag;
            public float[] BreathGain;
            public AntennaRig[] Antennae;
            /// <summary>모델 루트 기준 몸 경계(루트의 위치·회전·크기와 무관) — 매 프레임 루트 변환을 곱해 월드로 옮긴다.</summary>
            public Vector3 LocalCenter;
            public Vector3 LocalExtents;
            public BattleStatusFlags Wanted;
            public GameObject FxRoot;
            public readonly StatusLayer[] Layers = new StatusLayer[StatusLayerCount];
            public int LastHp = -1;
            public bool PendingHeal;
            public float PendingHealAge;
        }

        private sealed class AntennaRig
        {
            public Transform[] Parts;
            public Vector3[] BasePos;
            public Quaternion[] BaseRot;
            public Vector3 Pivot;
            public float Phase;
        }

        private sealed class StatusLayer
        {
            public GameObject Go;
            public float Shown;
            public Transform[] Parts;
            public Renderer[] Renderers;
            public LineRenderer[] Lines;
            public float[] Seeds;
        }

        /// <summary>한 프레임의 몸 자리(월드).</summary>
        private struct BodyFrame
        {
            public Vector3 Center;
            public Vector3 Extents;
            public float Horizontal;
            public float Size;
            public float Feet;
            public float Top;
        }

        // ───────────── 갱신 ─────────────

        /// <summary><c>Update</c>가 매 프레임 부른다(스킬 연출 중에도) — 숨쉬기·상태 표시·회복 반짝임 대기.</summary>
        private void UpdateLife()
        {
            if (arenaRoot == null) return;
            float dt = PresentationDelta;
            lifeClock += BattlePresentation.ReducedMotion ? dt * 0.35f : dt;

            GatherLifeModels();
            for (int i = 0; i < lifeModels.Count; i++)
            {
                LifeRig rig = RigFor(lifeModels[i]);
                if (rig != null && rig.Model.activeInHierarchy) Breathe(rig);
            }

            statusPollTimer -= Time.deltaTime;
            if (statusPollTimer <= 0f)
            {
                statusPollTimer = StatusPollSeconds;
                PollStatuses();
            }

            float realDt = Time.deltaTime;
            for (int i = 0; i < lifeModels.Count; i++)
            {
                if (!lifeRigs.TryGetValue(lifeModels[i], out LifeRig rig)) continue;
                AnimateStatus(rig, realDt);
                if (rig.PendingHeal)
                {
                    rig.PendingHealAge += realDt;
                    if (rig.PendingHealAge >= HealFlushTimeout && !playingSkill) FlushHeal(rig);
                }
            }
            PruneLifeRigs();
        }

        private void GatherLifeModels()
        {
            lifeModels.Clear();
            if (playerModel != null) lifeModels.Add(playerModel);
            if (enemyModel != null) lifeModels.Add(enemyModel);
            if (bossModel != null) lifeModels.Add(bossModel);
            if (teamModels != null)
                for (int i = 0; i < teamModels.Length; i++)
                    if (teamModels[i] != null) lifeModels.Add(teamModels[i]);
        }

        private void PruneLifeRigs()
        {
            lifeStale.Clear();
            foreach (GameObject key in lifeRigs.Keys)
                if (key == null || !lifeModels.Contains(key)) lifeStale.Add(key);
            for (int i = 0; i < lifeStale.Count; i++) ForgetLife(lifeStale[i]);
        }

        /// <summary>모델을 갈아끼우기 전에(<c>ForgetModel</c>) 그 몸의 표시를 걷는다.</summary>
        private void ForgetLife(GameObject model)
        {
            if (!lifeRigs.TryGetValue(model, out LifeRig rig)) return;
            if (rig.FxRoot != null) Destroy(rig.FxRoot);
            lifeRigs.Remove(model);
            faintingModels.Remove(model);
        }

        /// <summary>아레나 정리 — 오브젝트·머티리얼·메시는 아레나 루트·런타임 목록과 함께 파기된다. 참조만 버린다.</summary>
        private void ClearLifeState()
        {
            lifeRigs.Clear();
            lifeModels.Clear();
            lifeStale.Clear();
            faintingModels.Clear();
            statusMats.Clear();
            starMesh = null;
            flameMesh = null;
            statusSourcesSearched = false;
            statusPollTimer = 0f;
        }

        // ───────────── 몸 틀 ─────────────

        private LifeRig RigFor(GameObject model)
        {
            if (model == null) return null;
            if (lifeRigs.TryGetValue(model, out LifeRig rig)) return rig;
            rig = new LifeRig
            {
                Model = model,
                Boss = model == bossModel,
                Salt = model.GetInstanceID()
            };
            rig.Phase = Hash01(rig.Salt);
            MeasureLocal(model, out rig.LocalCenter, out rig.LocalExtents);
            BuildBreath(rig);
            lifeRigs[model] = rig;
            return rig;
        }

        /// <summary>
        /// 모델 루트 기준 경계 — 부위마다 루트까지의 로컬 변환을 곱해 메시 경계 꼭짓점을 모은다. 루트의 월드 변환(등장 중 크기 0 포함)과 무관하다.
        /// 빛 부위(이름에 Glow)·반짝이 장식은 뺀다(<c>BattleFraming.ModelBounds</c>와 같은 기준).
        /// </summary>
        private static void MeasureLocal(GameObject model, out Vector3 center, out Vector3 extents)
        {
            Transform root = model.transform;
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            bool found = false;
            Bounds acc = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter f = filters[i];
                if (f == null || f.sharedMesh == null) continue;
                string n = f.name;
                if (n.Contains("Glow") || n.Contains("Sparkle") || n.Contains("Aura")) continue;
                Matrix4x4 m = Matrix4x4.identity;
                for (Transform cur = f.transform; cur != null && cur != root; cur = cur.parent)
                    m = Matrix4x4.TRS(cur.localPosition, cur.localRotation, cur.localScale) * m;
                Bounds mb = f.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!found) { acc = new Bounds(p, Vector3.zero); found = true; }
                    else acc.Encapsulate(p);
                }
            }
            center = found ? acc.center : Vector3.zero;
            extents = found ? Vector3.Max(acc.extents, Vector3.one * 0.08f) : Vector3.one * 0.3f;
        }

        private BodyFrame FrameOf(LifeRig rig)
        {
            Transform t = rig.Model.transform;
            float scale = Mathf.Abs(t.lossyScale.x);
            BodyFrame f;
            f.Center = t.position + t.rotation * (rig.LocalCenter * scale);
            f.Extents = rig.LocalExtents * scale;
            f.Horizontal = Mathf.Max(0.18f, Mathf.Max(f.Extents.x, f.Extents.z) * 0.85f);
            f.Size = Mathf.Max(f.Extents.x, Mathf.Max(f.Extents.y, f.Extents.z));
            f.Feet = f.Center.y - f.Extents.y;
            f.Top = f.Center.y + f.Extents.y;
            return f;
        }

        // ───────────── 숨쉬기 ─────────────

        private static readonly string[] AntennaPartNames = { "AntBase", "AntMid", "AntTip", "AntFeather" };

        /// <summary>
        /// 숨 쉴 부위 — 배(Abdomen)가 있으면 배(+가슴 절반 세기), 없으면 몸통(Body), 그것도 없으면 마디(Seg*·Shell1~)를 물결처럼.
        /// 더듬이는 좌우 각각 뿌리(AntBase·Antenna의 아래 끝)를 축으로 함께 까딱인다.
        /// </summary>
        private static void BuildBreath(LifeRig rig)
        {
            Transform root = rig.Model.transform;
            var parts = new List<Transform>(6);
            var lags = new List<float>(6);
            var gains = new List<float>(6);
            Transform abdomen = root.Find("Abdomen");
            Transform body = root.Find("Body");
            Transform thorax = root.Find("Thorax");
            if (abdomen != null)
            {
                parts.Add(abdomen); lags.Add(0f); gains.Add(1f);
                if (thorax != null) { parts.Add(thorax); lags.Add(0.12f); gains.Add(0.5f); }
            }
            else if (body != null)
            {
                parts.Add(body); lags.Add(0f); gains.Add(1f);
            }
            else
            {
                for (int i = 0; i < root.childCount && parts.Count < 8; i++)
                {
                    Transform c = root.GetChild(i);
                    string n = c.name;
                    bool segment = n.StartsWith("Seg") || (n.StartsWith("Shell") && n.Length > 5 && char.IsDigit(n[5]));
                    if (!segment) continue;
                    parts.Add(c); lags.Add(0.08f * parts.Count); gains.Add(1f);
                }
            }
            rig.Breath = parts.ToArray();
            rig.BreathLag = lags.ToArray();
            rig.BreathGain = gains.ToArray();
            rig.BreathBase = new Vector3[rig.Breath.Length];
            for (int i = 0; i < rig.Breath.Length; i++) rig.BreathBase[i] = rig.Breath[i].localScale;

            var antennae = new List<AntennaRig>(2);
            for (int s = 0; s < 2; s++)
            {
                string side = s == 0 ? "L" : "R";
                var found = new List<Transform>(4);
                for (int k = 0; k < AntennaPartNames.Length; k++)
                {
                    Transform p = root.Find(AntennaPartNames[k] + side);
                    if (p != null) found.Add(p);
                }
                Transform rootPart = found.Count > 0 ? found[0] : root.Find("Antenna" + side);
                if (found.Count == 0 && rootPart != null) found.Add(rootPart);
                if (rootPart == null || found.Count == 0) continue;
                var a = new AntennaRig
                {
                    Parts = found.ToArray(),
                    BasePos = new Vector3[found.Count],
                    BaseRot = new Quaternion[found.Count],
                    // 마디(MakeSegment)는 캡슐을 두 점 사이에 세운다 — 중심에서 축(위) 방향으로 반 길이(localScale.y) 내려간 곳이 뿌리다.
                    Pivot = rootPart.localPosition - rootPart.localRotation * Vector3.up * rootPart.localScale.y,
                    Phase = rig.Phase + s * 0.07f
                };
                for (int k = 0; k < found.Count; k++)
                {
                    a.BasePos[k] = found[k].localPosition;
                    a.BaseRot[k] = found[k].localRotation;
                }
                antennae.Add(a);
            }
            rig.Antennae = antennae.ToArray();
        }

        private void Breathe(LifeRig rig)
        {
            float period = rig.Boss ? BattleFlourish.BossBreathPeriod : BattleFlourish.BreathPeriod;
            float amp = BattleFlourish.BreathAmplitude * (BattlePresentation.ReducedMotion ? 0.5f : 1f);
            for (int i = 0; i < rig.Breath.Length; i++)
            {
                Transform part = rig.Breath[i];
                if (part == null) continue;
                float s = BattleFlourish.Breath(lifeClock - rig.BreathLag[i], period, rig.Phase) * amp * rig.BreathGain[i];
                part.localScale = Vector3.Scale(rig.BreathBase[i], new Vector3(1f + s, 1f + 0.6f * s, 1f + s));
            }
            for (int s = 0; s < rig.Antennae.Length; s++)
            {
                AntennaRig a = rig.Antennae[s];
                float angle = BattlePresentation.ReducedMotion ? 0f : BattleFlourish.AntennaNod(lifeClock, a.Phase);
                Quaternion q = Quaternion.AngleAxis(angle, Vector3.right);
                for (int k = 0; k < a.Parts.Length; k++)
                {
                    Transform p = a.Parts[k];
                    if (p == null) continue;
                    p.localPosition = a.Pivot + q * (a.BasePos[k] - a.Pivot);
                    p.localRotation = q * a.BaseRot[k];
                }
            }
        }

        // ───────────── 상태 읽기 ─────────────

        private void PollStatuses()
        {
            if (!statusSourcesSearched)
            {
                statusSourcesSearched = true;   // 전투마다 한 번 — 없는 컨트롤러를 매번 찾지 않는다
                if (duelStatusSource == null) duelStatusSource = FindFirstObjectByType<InsectBattleController>();
                if (raidStatusSource == null) raidStatusSource = FindFirstObjectByType<RaidBattleController>();
            }
            if (bossModel != null) PollRaidStatuses();
            else PollDuelStatuses();
        }

        private void PollDuelStatuses()
        {
            InsectBattleController c = duelStatusSource;
            if (c == null) return;
            InsectBattleController.EffectSnapshot[] effects = c.GetActiveEffects();
            SetWanted(playerModel, BattleStatusLook.FromDuel(c.PlayerStunTurns, effects, true));
            SetWanted(enemyModel, BattleStatusLook.FromDuel(c.EnemyStunTurns, effects, false));
        }

        private void PollRaidStatuses()
        {
            RaidBattleController c = raidStatusSource;
            if (c == null) return;
            InsectBattleStats[] team = c.TeamStats;
            for (int i = 0; teamModels != null && i < teamModels.Length; i++)
            {
                InsectBattleStats stats = team != null && i < team.Length ? team[i] : null;
                if (stats == null || teamModels[i] == null) continue;
                SetWanted(teamModels[i], BattleStatusLook.FromStacks(stats.AttackStacks, stats.DefenseStacks, false));
                TrackHeal(teamModels[i], stats.CurrentHp);
            }
            InsectBattleStats boss = c.BossStats;
            if (boss != null)
                SetWanted(bossModel, BattleStatusLook.FromStacks(boss.AttackStacks, boss.DefenseStacks,
                    BattleStatusLook.RaidBossDazed(c.CurrentRoundResult)));
        }

        private void SetWanted(GameObject model, BattleStatusFlags flags)
        {
            LifeRig rig = RigFor(model);
            if (rig != null) rig.Wanted = flags;
        }

        /// <summary>
        /// 레이드 회복 — HP가 오른 팀원을 적어 두고 그 행동의 타격 순간(<see cref="FlushPendingHeals"/>)에 반짝인다. 행동은 커맨드 순간에 해결돼
        /// HP가 먼저 오르고 연출(0.46초 볼리)이 그 뒤에 돈다 — 읽은 순간 띄우면 시전자가 나서기도 전에 반짝인다.
        /// </summary>
        private void TrackHeal(GameObject model, int hp)
        {
            if (!lifeRigs.TryGetValue(model, out LifeRig rig)) return;
            if (rig.LastHp >= 0 && hp > rig.LastHp && rig.LastHp > 0)
            {
                rig.PendingHeal = true;
                rig.PendingHealAge = 0f;
            }
            rig.LastHp = hp;
        }

        /// <summary>적어 둔 회복 반짝임을 지금 띄운다 — 레이드 팀원 행동의 타격 순간이 부른다.</summary>
        private void FlushPendingHeals()
        {
            for (int i = 0; i < lifeModels.Count; i++)
                if (lifeRigs.TryGetValue(lifeModels[i], out LifeRig rig) && rig.PendingHeal) FlushHeal(rig);
        }

        private void FlushHeal(LifeRig rig)
        {
            rig.PendingHeal = false;
            if (rig.Model != null && rig.Model.activeInHierarchy) PlayHealSparkle(rig.Model);
        }

        /// <summary>쓰러지기 시작한 몸 — 상태 표시를 곧바로 거둔다(쓰러짐 페이드가 모델 안 렌더러만 지운다).</summary>
        private void MarkFainting(GameObject model)
        {
            if (model != null) faintingModels.Add(model);
        }

        // ───────────── 상태 표시 ─────────────

        private void AnimateStatus(LifeRig rig, float dt)
        {
            GameObject model = rig.Model;
            bool visible = model != null && model.activeInHierarchy && !faintingModels.Contains(model);
            BodyFrame f = default;
            bool framed = false;
            for (int i = 0; i < StatusLayerCount; i++)
            {
                bool want = visible && ((int)rig.Wanted & (1 << i)) != 0;
                StatusLayer layer = rig.Layers[i];
                if (layer == null)
                {
                    if (!want) continue;
                    layer = rig.Layers[i] = CreateStatusLayer(rig, i);
                    if (layer == null) continue;
                }
                layer.Shown = Mathf.MoveTowards(layer.Shown, want ? 1f : 0f, dt * StatusFadeRate);
                if (layer.Shown <= 0f)
                {
                    if (layer.Go.activeSelf) layer.Go.SetActive(false);
                    continue;
                }
                if (!layer.Go.activeSelf) layer.Go.SetActive(true);
                if (!framed)
                {
                    if (model == null) break;
                    f = FrameOf(rig);
                    framed = true;
                }
                switch (i)
                {
                    case 0: AnimateBubbles(rig, layer, f); break;
                    case 1: AnimateStars(layer, f); break;
                    case 2: AnimateFlames(layer, f); break;
                    case 3: AnimateDownStreaks(layer, f, BattleStatusLook.AttackDownColor, 0f); break;
                    case 4: AnimateShield(layer, f); break;
                    default: AnimateDownStreaks(layer, f, BattleStatusLook.DefenseDownColor, Mathf.PI / 5f); break;
                }
            }
        }

        private StatusLayer CreateStatusLayer(LifeRig rig, int index)
        {
            if (arenaRoot == null) return null;
            if (rig.FxRoot == null)
            {
                rig.FxRoot = new GameObject("StatusFx_" + rig.Model.name);
                rig.FxRoot.transform.SetParent(arenaRoot.transform, false);
            }
            switch (index)
            {
                case 0: return CreateMeshLayer(rig, "Poison", 5, null, StatusMat(0, BattleStatusLook.PoisonColor, false), null);
                case 1: return CreateMeshLayer(rig, "Stun", 3, StarMesh(), StatusMat(1, BattleStatusLook.StunColor, false),
                    StatusMat(11, BattleStatusLook.StunEdgeColor, false));
                case 2:
                {
                    StatusLayer layer = CreateMeshLayer(rig, "AttackUp", 7, FlameMesh(), StatusMat(2, BattleStatusLook.AttackUpColor, true), null);
                    LineRenderer ring = CreateFloorRing("AttackUpRing", layer.Go.transform.position, StatusMat(12, BattleStatusLook.AttackUpColor, true));
                    ring.transform.SetParent(layer.Go.transform, true);
                    layer.Lines = new[] { ring };
                    return layer;
                }
                case 3: return CreateMeshLayer(rig, "AttackDown", 5, FlameMesh(), StatusMat(3, BattleStatusLook.AttackDownColor, false), null);
                case 4: return CreateShieldLayer(rig);
                default: return CreateMeshLayer(rig, "DefenseDown", 5, FlameMesh(), StatusMat(5, BattleStatusLook.DefenseDownColor, false), null);
            }
        }

        /// <summary>
        /// 같은 모양 <paramref name="count"/>개짜리 층. <paramref name="mesh"/>가 null이면 구(거품). <paramref name="edgeMat"/>이 있으면 같은 수의
        /// 뒷면 테두리(별의 진한 테두리)를 더 단다 — <c>Parts</c>의 뒤쪽 절반이 그것이다.
        /// </summary>
        private StatusLayer CreateMeshLayer(LifeRig rig, string name, int count, Mesh mesh, Material mat, Material edgeMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig.FxRoot.transform, false);
            int total = edgeMat != null ? count * 2 : count;
            var layer = new StatusLayer
            {
                Go = go,
                Parts = new Transform[total],
                Renderers = new Renderer[total],
                Seeds = new float[count]
            };
            for (int i = 0; i < total; i++)
            {
                bool edge = i >= count;
                GameObject part = mesh == null
                    ? FxPrimitive(PrimitiveType.Sphere, name + "Bit", go.transform.position, mat)
                    : FxMeshObject(name + (edge ? "Edge" : "Bit"), mesh, edge ? edgeMat : mat);
                part.transform.SetParent(go.transform, true);
                part.transform.localScale = Vector3.zero;
                layer.Parts[i] = part.transform;
                layer.Renderers[i] = part.GetComponent<Renderer>();
            }
            for (int i = 0; i < count; i++) layer.Seeds[i] = Hash01(rig.Salt * 31 + i * 7919 + name.Length);
            return layer;
        }

        /// <summary>방어 강화 — 몸을 감싸는 구 위의 육각 칸(피보나치 배치, 아래쪽은 땅이라 뺀다).</summary>
        private StatusLayer CreateShieldLayer(LifeRig rig)
        {
            var go = new GameObject("DefenseUp");
            go.transform.SetParent(rig.FxRoot.transform, false);
            Material mat = StatusMat(4, BattleStatusLook.DefenseUpColor, true);
            const int Points = 26;
            var tiles = new List<LineRenderer>(Points);
            var seeds = new List<float>(Points);
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < Points; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / Points;
                if (y < -0.35f) continue;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float a = golden * i;
                Vector3 dir = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                var tile = new GameObject("ShieldHex");
                tile.transform.SetParent(go.transform, false);
                tile.transform.localPosition = dir;
                tile.transform.localRotation = Quaternion.LookRotation(dir);
                LineRenderer line = tile.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 6;
                line.alignment = LineAlignment.TransformZ;
                line.sharedMaterial = mat;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                for (int k = 0; k < 6; k++)
                {
                    float ha = (k + 0.5f) * Mathf.PI / 3f;
                    line.SetPosition(k, new Vector3(Mathf.Cos(ha) * 0.3f, Mathf.Sin(ha) * 0.3f, 0f));
                }
                tiles.Add(line);
                seeds.Add(y * 2.6f + a * 0.15f);
            }
            return new StatusLayer { Go = go, Lines = tiles.ToArray(), Seeds = seeds.ToArray() };
        }

        private void AnimateBubbles(LifeRig rig, StatusLayer layer, BodyFrame f)
        {
            Camera cam = Camera.main;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            float size = Mathf.Clamp(f.Size * 0.11f, 0.05f, 0.2f);
            float climb = 0.3f + f.Extents.y * 0.9f;
            for (int i = 0; i < layer.Seeds.Length; i++)
            {
                float cycle = lifeClock / 1.4f + layer.Seeds[i];
                float age = Mathf.Repeat(cycle, 1f);
                int round = Mathf.FloorToInt(cycle);
                float ox = Hash01(rig.Salt + round * 92821 + i * 68917) * 2f - 1f;
                float oz = Hash01(rig.Salt * 7 + round * 31337 + i * 1009) * 2f - 1f;
                BattleStatusLook.Bubble(age, out float rise, out float scale, out float alpha);
                Vector3 start = new Vector3(f.Center.x + ox * f.Horizontal * 0.5f, f.Center.y + f.Extents.y * 0.15f,
                    f.Center.z + oz * f.Horizontal * 0.5f);
                layer.Parts[i].position = start + Vector3.up * (rise * climb)
                    + right * (Mathf.Sin(age * Mathf.PI * 2f + layer.Seeds[i] * 10f) * 0.03f);
                layer.Parts[i].localScale = Vector3.one * (size * scale);
                SetFxAlpha(layer.Renderers[i], BattleStatusLook.PoisonColor, 0.82f * alpha * layer.Shown);
            }
        }

        private void AnimateStars(StatusLayer layer, BodyFrame f)
        {
            Camera cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            Quaternion facing = Quaternion.LookRotation(forward);
            int count = layer.Seeds.Length;
            float ring = Mathf.Clamp(f.Horizontal * 0.6f, 0.2f, 0.9f);
            float y = f.Top + Mathf.Clamp(f.Size * 0.2f, 0.1f, 0.35f);
            float star = Mathf.Clamp(f.Size * 0.15f, 0.08f, 0.28f);
            for (int i = 0; i < count; i++)
            {
                float a = lifeClock * Mathf.PI * 2f / 1.3f + i * Mathf.PI * 2f / count;
                Vector3 p = new Vector3(f.Center.x + Mathf.Cos(a) * ring, y + 0.04f * Mathf.Sin(lifeClock * 5f + i * 2f),
                    f.Center.z + Mathf.Sin(a) * ring * 0.75f);
                Quaternion spin = facing * Quaternion.Euler(0f, 0f, lifeClock * 200f + i * 40f);
                layer.Parts[i].SetPositionAndRotation(p, spin);
                layer.Parts[i].localScale = Vector3.one * star;
                Transform edge = layer.Parts[i + count];
                edge.SetPositionAndRotation(p + forward * 0.012f, spin);
                edge.localScale = Vector3.one * (star * 1.3f);
                SetFxAlpha(layer.Renderers[i], BattleStatusLook.StunColor, layer.Shown);
                SetFxAlpha(layer.Renderers[i + count], BattleStatusLook.StunEdgeColor, 0.9f * layer.Shown);
            }
        }

        private void AnimateFlames(StatusLayer layer, BodyFrame f)
        {
            Quaternion facing = YawFacing();
            float width = Mathf.Clamp(f.Size * 0.13f, 0.05f, 0.32f);
            float climb = f.Extents.y * 1.7f + 0.1f;
            for (int i = 0; i < layer.Seeds.Length; i++)
            {
                float age = Mathf.Repeat(lifeClock / 0.9f + layer.Seeds[i], 1f);
                float a = i * Mathf.PI * 2f / layer.Seeds.Length + lifeClock * 0.6f;
                BattleStatusLook.Streak(age, out float alpha, out float wf);
                Vector3 p = new Vector3(f.Center.x + Mathf.Cos(a) * f.Horizontal * 0.9f, f.Feet + width * 1.2f + age * climb,
                    f.Center.z + Mathf.Sin(a) * f.Horizontal * 0.8f);
                layer.Parts[i].SetPositionAndRotation(p, facing);
                layer.Parts[i].localScale = new Vector3(width * wf, width * 2.6f * (0.7f + 0.5f * Mathf.Sin(age * Mathf.PI)), 1f);
                SetFxAlpha(layer.Renderers[i], BattleStatusLook.AttackUpColor, 0.85f * alpha * layer.Shown);
            }
            if (layer.Lines != null && layer.Lines.Length > 0 && layer.Lines[0] != null)
            {
                LineRenderer ring = layer.Lines[0];
                ring.transform.position = new Vector3(f.Center.x, f.Feet + 0.03f, f.Center.z);
                SetRingRadius(ring, f.Horizontal * 1.05f, Mathf.Clamp(f.Size * 0.05f, 0.025f, 0.08f));
                float glow = BattlePresentation.ReducedFlashes ? 0.3f : 0.3f + 0.15f * Mathf.Sin(lifeClock * 4f);
                SetFxAlpha(ring, BattleStatusLook.AttackUpColor, glow * layer.Shown);
            }
        }

        private void AnimateDownStreaks(StatusLayer layer, BodyFrame f, Color color, float offset)
        {
            Quaternion down = YawFacing() * Quaternion.Euler(0f, 0f, 180f);   // 뾰족한 끝이 아래로
            float width = Mathf.Clamp(f.Size * 0.07f, 0.03f, 0.16f);
            float fall = f.Extents.y * 2f + 0.35f;
            float top = f.Top + 0.2f;
            for (int i = 0; i < layer.Seeds.Length; i++)
            {
                float age = Mathf.Repeat(lifeClock / 1.1f + layer.Seeds[i], 1f);
                float a = (i + 0.5f) * Mathf.PI * 2f / layer.Seeds.Length + offset - lifeClock * 0.25f;
                BattleStatusLook.Streak(age, out float alpha, out float wf);
                Vector3 p = new Vector3(f.Center.x + Mathf.Cos(a) * f.Horizontal * 1.1f, top - age * fall,
                    f.Center.z + Mathf.Sin(a) * f.Horizontal * 1.0f);
                layer.Parts[i].SetPositionAndRotation(p, down);
                layer.Parts[i].localScale = new Vector3(width * wf, width * 4.2f, 1f);
                SetFxAlpha(layer.Renderers[i], color, 0.8f * alpha * layer.Shown);
            }
        }

        private void AnimateShield(StatusLayer layer, BodyFrame f)
        {
            float radius = Mathf.Max(f.Horizontal, f.Extents.y) * 1.2f + 0.06f;
            Transform root = layer.Go.transform;
            root.SetPositionAndRotation(f.Center, Quaternion.Euler(0f, lifeClock * 14f, 0f));
            root.localScale = Vector3.one * radius;
            float width = Mathf.Clamp(radius * 0.035f, 0.012f, 0.06f);
            for (int i = 0; i < layer.Lines.Length; i++)
            {
                LineRenderer line = layer.Lines[i];
                if (line == null) continue;
                line.widthMultiplier = width;
                SetFxAlpha(line, BattleStatusLook.DefenseUpColor,
                    BattleStatusLook.ShieldAlpha(lifeClock, layer.Seeds[i], BattlePresentation.ReducedFlashes) * layer.Shown);
            }
        }

        // ───────────── 회복 반짝임 ─────────────

        /// <summary>
        /// 회복 — 초록 별 반짝임 열둘이 몸 둘레에서 위로 솟고, 초록 고리가 발밑에서 몸 위로 올라간다(0.9초, 한 번). 1대1은 회복 기술의 타격 순간,
        /// 레이드는 회복한 팀원 행동의 타격 순간에 뜬다. 밝은 섬광 줄이기면 가산 대신 알파 합성으로 은은하게.
        /// </summary>
        public void PlayHealSparkle(GameObject model)
        {
            if (!isActive || arenaRoot == null || model == null || !model.activeInHierarchy) return;
            StartCoroutine(HealSparkleCoroutine(model));
        }

        private IEnumerator HealSparkleCoroutine(GameObject model)
        {
            LifeRig rig = RigFor(model);
            if (rig == null) yield break;
            bool additive = !BattlePresentation.ReducedFlashes;
            Color heal = BattleStatusLook.HealColor;
            Material mat = CreateFxMaterial(new Color(heal.r, heal.g, heal.b, 1f), additive);
            Material ringMat = CreateFxMaterial(new Color(heal.r, heal.g, heal.b, 0.8f), additive);
            const int Count = 12;
            var bits = new Transform[Count];
            var renderers = new Renderer[Count];
            var angles = new float[Count];
            var radii = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                GameObject bit = FxMeshObject("HealSparkle", StarMesh(), mat);
                bit.transform.localScale = Vector3.zero;
                bits[i] = bit.transform;
                renderers[i] = bit.GetComponent<Renderer>();
                angles[i] = (i + Hash01(rig.Salt + i * 131) * 0.6f) * Mathf.PI * 2f / Count;
                radii[i] = 0.55f + 0.5f * Hash01(rig.Salt * 3 + i * 977);
            }
            BodyFrame f0 = FrameOf(rig);
            LineRenderer ring = CreateFloorRing("HealRing", new Vector3(f0.Center.x, f0.Feet + 0.03f, f0.Center.z), ringMat);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.BuffApply);

            const float Duration = 0.9f;
            float t = 0f;
            while (t < Duration)
            {
                if (arenaRoot == null || model == null) break;
                t += PresentationDelta;
                BodyFrame f = FrameOf(rig);
                Camera cam = Camera.main;
                Quaternion facing = cam != null ? Quaternion.LookRotation(cam.transform.forward) : Quaternion.identity;
                float size = Mathf.Clamp(f.Size * 0.12f, 0.06f, 0.24f);
                for (int i = 0; i < Count; i++)
                {
                    if (bits[i] == null) continue;
                    float age = Mathf.Clamp01((t - i * 0.03f) / 0.7f);
                    float y = f.Center.y - f.Extents.y * 0.6f + age * (f.Extents.y * 1.9f + 0.4f);
                    float r = f.Horizontal * radii[i] * (1f - 0.3f * age);
                    bits[i].SetPositionAndRotation(new Vector3(f.Center.x + Mathf.Cos(angles[i] + age) * r, y,
                        f.Center.z + Mathf.Sin(angles[i] + age) * r), facing * Quaternion.Euler(0f, 0f, t * 160f + i * 30f));
                    float twinkle = 0.8f + 0.2f * Mathf.Sin(t * 30f + i);
                    bits[i].localScale = Vector3.one * (size * Mathf.Sin(age * Mathf.PI) * twinkle);
                }
                float k = Mathf.Clamp01(t / 0.65f);
                if (ring != null)
                {
                    ring.transform.position = new Vector3(f.Center.x, Mathf.Lerp(f.Feet + 0.03f, f.Top, k), f.Center.z);
                    SetRingRadius(ring, f.Horizontal * Mathf.Lerp(1.15f, 0.6f, k), Mathf.Lerp(0.07f, 0.015f, k));
                    ringMat.color = new Color(heal.r, heal.g, heal.b, 0.8f * (1f - k));
                }
                yield return null;
            }
            for (int i = 0; i < Count; i++) if (bits[i] != null) Destroy(bits[i].gameObject);
            if (ring != null) Destroy(ring.gameObject);
        }

        // ───────────── 부품 ─────────────

        private Material StatusMat(int key, Color color, bool additive)
        {
            if (statusMats.TryGetValue(key, out Material mat) && mat != null) return mat;
            mat = CreateFxMaterial(new Color(color.r, color.g, color.b, 1f), additive);
            statusMats[key] = mat;
            return mat;
        }

        private void SetFxAlpha(Renderer renderer, Color color, float alpha)
        {
            if (renderer == null) return;
            if (lifeBlock == null) lifeBlock = new MaterialPropertyBlock();
            lifeBlock.Clear();
            lifeBlock.SetColor("_Color", new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha)));
            renderer.SetPropertyBlock(lifeBlock);
        }

        /// <summary>카메라를 향해 세로로 선 판(불꽃결·내려가는 결) — 수평으로만 돌린다.</summary>
        private static Quaternion YawFacing()
        {
            Camera cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        }

        /// <summary>메시 하나짜리 FX 오브젝트(아레나 루트 아래, 그림자 없음).</summary>
        private GameObject FxMeshObject(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(arenaRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>다섯 뿔 별(XY 평면, 반지름 1) — 기절 별·회복 반짝임. 컬링이 꺼진 FX 셰이더라 양면으로 보인다.</summary>
        private Mesh StarMesh()
        {
            if (starMesh != null) return starMesh;
            var verts = new Vector3[11];
            var tris = new int[30];
            verts[0] = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 1f : 0.45f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            for (int i = 0; i < 10; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % 10 + 1;
            }
            starMesh = new Mesh { name = "BattleStatusStar", vertices = verts, triangles = tris };
            starMesh.RecalculateBounds();
            runtimeMeshes.Add(starMesh);
            return starMesh;
        }

        /// <summary>불꽃결 한 가닥(XY 평면) — 위가 뾰족하고 아래가 둥근 마름모. 뒤집으면 내려가는 결.</summary>
        private Mesh FlameMesh()
        {
            if (flameMesh != null) return flameMesh;
            flameMesh = new Mesh
            {
                name = "BattleStatusFlame",
                vertices = new[] { new Vector3(0f, 1f, 0f), new Vector3(0.5f, -0.35f, 0f), new Vector3(0f, -1f, 0f), new Vector3(-0.5f, -0.35f, 0f) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            flameMesh.RecalculateBounds();
            runtimeMeshes.Add(flameMesh);
            return flameMesh;
        }

        /// <summary>정수 → 0..1 결정적 난수(연출이 UnityEngine.Random을 소비하지 않게 — QA 캡처가 같은 장면이 된다).</summary>
        private static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n;
                x ^= x >> 16;
                x *= 0x7feb352dU;
                x ^= x >> 15;
                x *= 0x846ca68bU;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
