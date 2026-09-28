using System.Collections;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 타격감 — 히트스톱·넉백·피격 플래시·임팩트 버스트·외침·연출 카메라.
    ///
    /// 예전 타격은 대상이 제자리에서 0.12m 떨고 카메라가 0.12 세기로 0.18초 흔들리는 게 전부였고,
    /// 벌레 속성 임팩트는 바닥에 깔리는 납작한 원판이었다(<c>CreateSafeMaterial</c>은 불투명이라 알파
    /// 페이드가 먹지 않는다). 무엇이 누구를 얼마나 세게 쳤는지가 몸으로 읽히지 않았다.
    ///
    /// 본체(2천 줄 넘는 연출 코루틴 모음)를 더 키우지 않으려고 partial로 떼었다. 본체는
    /// <c>SkillAttackCoroutine</c>에서 <see cref="BeginSkillPresentation"/>·<see cref="ApplyCameraShot"/>·
    /// <see cref="PlayImpactFeel"/>·<see cref="ClearCameraShot"/>를 부르고, 정리는 <c>CleanupArena</c>가
    /// <see cref="ClearImpactState"/>로 한다.
    /// </summary>
    public partial class BattleArenaController
    {
        /// <summary>
        /// 연출이 알아야 하는 타격 정보 — 결과는 이미 확정돼 있고(전투는 동기로 해결된다) 연출은 그걸
        /// 얼마나 세게 보여줄지만 정한다. 판정을 다시 하지 않는다.
        /// </summary>
        public readonly struct HitCue
        {
            /// <summary>외칠 기술 이름. null이면 외치지 않는다(기본 공격).</summary>
            public readonly string SkillName;
            /// <summary>타격 세기 0..1 — 치명타·큰 피해일수록 1. 히트스톱·넉백·카메라 반동이 이걸 따른다.</summary>
            public readonly float Weight;
            /// <summary>이 한 방으로 쓰러졌다 — 슬로모션.</summary>
            public readonly bool Finisher;
            /// <summary>피해 기술인데 피해가 0 — 빗나감(대상이 피한다).</summary>
            public readonly bool Missed;

            public HitCue(string skillName, float weight, bool finisher, bool missed)
            {
                SkillName = skillName;
                Weight = Mathf.Clamp01(weight);
                Finisher = finisher;
                Missed = missed;
            }

            public static readonly HitCue None = new HitCue(null, 0f, false, false);

            /// <summary>
            /// 피해량과 대상 최대 HP로 세기를 낸다. 최대 HP의 40%면 만점 — 치명타 판정선(25%)이
            /// 0.63으로 "무거운 타격"(0.6) 위에 오도록 잡았다.
            /// </summary>
            public static float WeightFor(int damage, int targetMaxHp, bool critical)
            {
                if (damage <= 0 || targetMaxHp <= 0) return 0f;
                float w = Mathf.Clamp01(damage / (targetMaxHp * 0.4f));
                return critical ? Mathf.Max(w, 0.85f) : w;
            }
        }

        private Vector3 baseCamPos;
        private Quaternion baseCamRot = Quaternion.identity;
        private bool hasBaseCam;

        private readonly List<BattleShout.Entry> activeShouts = new List<BattleShout.Entry>();
        private const int MaxConcurrentShouts = 5;

        private readonly Dictionary<GameObject, Coroutine> activeReacts = new Dictionary<GameObject, Coroutine>();
        private readonly Dictionary<GameObject, Pose3> reactRestPoses = new Dictionary<GameObject, Pose3>();
        private MaterialPropertyBlock flashBlock;

        private struct Pose3
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        // ── 외침 ──

        /// <summary>떠 있는 외침. 그리기는 UI(<c>BattleShoutOverlay</c>)가 한다 — 효과 문구와 같은 이음매.</summary>
        public IReadOnlyList<BattleShout.Entry> GetActiveShouts()
        {
            float now = Time.unscaledTime;
            for (int i = activeShouts.Count - 1; i >= 0; i--)
            {
                BattleShout.Entry e = activeShouts[i];
                if (e == null || now - e.StartTime >= e.Duration) activeShouts.RemoveAt(i);
            }
            return activeShouts;
        }

        /// <summary>
        /// 외침을 띄운다. <paramref name="delay"/>만큼 늦게 나타난다(실제 초) — 타격음 뒤에 비명이
        /// 따라와야 둘이 한 덩어리로 안 뭉개진다.
        /// </summary>
        public void PlayShout(BattleShout.Kind kind, Vector3 worldPoint, string text, Color color,
            float duration, float delay = 0f, float tilt = 0f)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (activeShouts.Count >= MaxConcurrentShouts) activeShouts.RemoveAt(0);
            activeShouts.Add(new BattleShout.Entry
            {
                Text = text,
                Kind = kind,
                WorldPoint = worldPoint,
                Color = color,
                // 시작을 미래로 잡으면 나이(now - StartTime)가 음수라 그 동안은 그리지도 지우지도 않는다.
                StartTime = Time.unscaledTime + Mathf.Max(0f, delay),
                Duration = duration,
                Tilt = tilt
            });
        }

        private static BattleShout.Cry CryOf(GameObject model, bool boss)
        {
            if (boss) return BattleShout.Cry.Boss;
            InsectEntity entity = model != null ? model.GetComponent<InsectEntity>() : null;
            return BattleShout.CryFor(entity != null && entity.Data != null ? entity.Data.insectId : null);
        }

        private static string CryKey(BattleShout.Cry cry, bool hurt)
        {
            return (hurt ? "hurt_" : "cry_") + cry.ToString().ToLowerInvariant();
        }

        private Vector3 HeadPoint(GameObject model)
        {
            Bounds b = BattleFraming.ModelBounds(model);
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        // ── 시전 ──

        /// <summary>
        /// 스킬 연출 첫 프레임 — 기술 이름을 외치고 운다. 강화·회복도 외친다(몸을 부풀리는 순간이다).
        /// </summary>
        private void BeginSkillPresentation(GameObject attacker, InsectElement element, HitCue cue)
        {
            if (attacker == null) return;
            bool boss = attacker == bossModel;
            BattleShout.Cry cry = CryOf(attacker, boss);
            if (!string.IsNullOrEmpty(cue.SkillName))
            {
                // 0.95초 — 카메라가 대상 쪽으로 넘어가기 전에 사라진다. 길면 시전자 머리 위에 고정된
                // 말풍선이 화면 가장자리(HP 상자 위)로 밀려난다(실측).
                PlayShout(BattleShout.Kind.Callout, HeadPoint(attacker), BattleShout.Callout(cue.SkillName),
                    GetUIElementColor(element), 0.95f);
            }
            if (AudioManager.Instance != null) AudioManager.Instance.PlayCry(CryKey(cry, false));
        }

        // ── 타격 ──

        /// <summary>
        /// 타격 순간 — 히트스톱, 넉백·플래시, 버스트, 의성어·비명, 카메라 흔들림.
        /// 원래 있던 속성 임팩트(<c>CreateElementImpact3D</c>)와 스킬 효과음은 호출부가 그대로 낸다.
        /// </summary>
        private void PlayImpactFeel(GameObject attacker, GameObject target, Vector3 hitPoint,
            InsectElement element, Color color, HitCue cue)
        {
            if (target == null) return;
            Vector3 dir = target.transform.position - (attacker != null ? attacker.transform.position : hitPoint);
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            float w = cue.Weight;
            bool boss = target == bossModel;

            if (cue.Missed)
            {
                StartReact(target, DodgeCoroutine(target, dir));
                PlayShout(BattleShout.Kind.Sound, hitPoint, "휙!", new Color(0.82f, 0.86f, 0.92f), 0.7f,
                    0f, Random.Range(-8f, 8f));
                return;
            }

            BattlePresentation.HitStop(0.055f + 0.085f * w + (cue.Finisher ? 0.07f : 0f));
            if (cue.Finisher) BattlePresentation.SlowMotion(0.3f, 0.6f);

            StartReact(target, HitReactCoroutine(target, dir, w, boss));
            if (!BattlePresentation.ReducedFlashes) StartCoroutine(ImpactBurstCoroutine(hitPoint, color, w));

            // 자리 나눔 — 피해 숫자·CRITICAL은 대상 몸통 위(BattleScreenUI), 의성어는 맞아서 밀려나는
            // 쪽 아래, 비명은 같은 쪽 머리 높이. 셋을 한 점에 띄우면 타격 순간 글자가 뭉개진다(실측).
            Color soundColor = Color.Lerp(color, Color.white, 0.35f);
            PlayShout(BattleShout.Kind.Sound, hitPoint + dir * 0.65f + Vector3.down * 0.1f,
                BattleShout.Sound(element, cue.Finisher ? 1f : w), soundColor, 0.85f, 0f, Random.Range(-11f, 11f));
            // 보스는 센 타격·마무리에만 비명을 지른다 — 레이드는 한 라운드에 팀원 다섯이 연달아 때려서
            // 매번 지르면 "그르륵!"이 다섯 번 반복된다.
            bool voice = !boss || w >= 0.6f || cue.Finisher;
            BattleShout.Cry cry = CryOf(target, boss);
            if (voice)
                PlayShout(BattleShout.Kind.Hurt, HeadPoint(target) + dir * 0.55f,
                    BattleShout.Hurt(cry, cue.Finisher ? 1f : w, Random.Range(0, 100)),
                    Color.white, 1.0f, 0.12f);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(w >= 0.6f || cue.Finisher ? SfxType.CriticalHit : SfxType.Hit);
                if (voice) StartCoroutine(PlayCryDelayed(CryKey(cry, true), 0.1f));
            }

            CameraFollower follower = ResolveFollower();
            if (follower != null) follower.Shake(0.08f + 0.22f * w, 0.18f + 0.22f * w);
        }

        /// <summary>쓰러짐 — 바닥에 "털썩…"과 마지막 비명. 쓰러지는 동작은 <c>PlayFaintCoroutine</c>이 한다.</summary>
        private void AnnounceFaint(GameObject model)
        {
            if (model == null) return;
            Bounds b = BattleFraming.ModelBounds(model);
            PlayShout(BattleShout.Kind.Sound, new Vector3(b.center.x, b.min.y + 0.1f, b.center.z), "털썩…",
                new Color(0.78f, 0.8f, 0.86f), 1.1f, 0.25f, Random.Range(-6f, 6f));
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayCry(CryKey(CryOf(model, model == bossModel), true));
        }

        private IEnumerator PlayCryDelayed(string key, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayCry(key);
        }

        private CameraFollower ResolveFollower()
        {
            if (cachedCameraFollower == null && Camera.main != null)
                cachedCameraFollower = Camera.main.GetComponent<CameraFollower>();
            return cachedCameraFollower;
        }

        /// <summary>
        /// 모델 하나에 반응 코루틴은 하나만 — 겹치면 앞 것이 잡아 둔 원위치로 먼저 되돌린 뒤 새로 건다.
        /// 안 그러면 두 번째 반응이 밀려난 자리를 "원위치"로 잡아 모델이 조금씩 떠내려간다.
        /// </summary>
        private void StartReact(GameObject model, IEnumerator routine)
        {
            if (model == null) return;
            if (activeReacts.TryGetValue(model, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
                RestoreReact(model);
            }
            reactRestPoses[model] = new Pose3
            {
                Position = model.transform.position,
                Rotation = model.transform.rotation,
                Scale = model.transform.localScale
            };
            activeReacts[model] = StartCoroutine(routine);
        }

        private void RestoreReact(GameObject model)
        {
            if (model == null) return;
            if (reactRestPoses.TryGetValue(model, out Pose3 rest))
            {
                model.transform.position = rest.Position;
                model.transform.rotation = rest.Rotation;
                model.transform.localScale = rest.Scale;
            }
            SetFlash(model, Color.white, 0f);
            activeReacts.Remove(model);
        }

        /// <summary>
        /// 피격 — 맞은 방향으로 튕겨 나갔다가(0.07초) 버티며 돌아오고(0.35초), 윗몸이 젖혀지고 눌렸다 편다.
        /// 동시에 흰 섬광 → 붉은 기가 빠진다. 보스는 무거워서 덜 밀린다.
        /// </summary>
        private IEnumerator HitReactCoroutine(GameObject model, Vector3 dir, float weight, bool boss)
        {
            Pose3 rest = reactRestPoses[model];
            float mass = boss ? 0.45f : 1f;
            float distance = (0.22f + 0.30f * weight) * mass;
            float tilt = (10f + 14f * weight) * mass;
            bool motion = !BattlePresentation.ReducedMotion;
            Vector3 axis = Vector3.Cross(Vector3.up, dir);
            const float Out = 0.07f;
            const float Total = 0.42f;
            float t = 0f;
            while (t < Total)
            {
                if (model == null) yield break;
                t += PresentationDelta;
                float k = t < Out
                    ? 1f - (1f - t / Out) * (1f - t / Out)                         // 튕겨 나감(감속)
                    : 1f - Mathf.SmoothStep(0f, 1f, (t - Out) / (Total - Out));     // 버티며 복귀
                if (motion)
                {
                    model.transform.position = rest.Position + dir * (distance * k);
                    model.transform.rotation = Quaternion.AngleAxis(tilt * k, axis) * rest.Rotation;
                    float squash = 0.14f * weight * k * mass;
                    model.transform.localScale = Vector3.Scale(rest.Scale,
                        new Vector3(1f + squash * 0.6f, 1f - squash, 1f + squash * 0.6f));
                }
                if (!BattlePresentation.ReducedFlashes)
                {
                    if (t < 0.06f) SetFlash(model, Color.white, 0.78f);
                    else SetFlash(model, new Color(1f, 0.32f, 0.26f),
                        0.45f * (1f - Mathf.Clamp01((t - 0.06f) / 0.26f)));
                }
                yield return null;
            }
            RestoreReact(model);
        }

        /// <summary>빗나감 — 대상이 옆으로 흘려 피했다가 돌아온다.</summary>
        private IEnumerator DodgeCoroutine(GameObject model, Vector3 dir)
        {
            Pose3 rest = reactRestPoses[model];
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            const float Total = 0.45f;
            float t = 0f;
            while (t < Total && !BattlePresentation.ReducedMotion)
            {
                if (model == null) yield break;
                t += PresentationDelta;
                float k = Mathf.Sin(Mathf.Clamp01(t / Total) * Mathf.PI);
                model.transform.position = rest.Position + side * (0.45f * k) + Vector3.up * (0.18f * k);
                model.transform.rotation = Quaternion.AngleAxis(-12f * k, dir) * rest.Rotation;
                yield return null;
            }
            RestoreReact(model);
        }

        /// <summary>
        /// 모델 전체에 색을 섞는다. 머티리얼 인스턴스를 만들지 않으려고 PropertyBlock을 쓴다 —
        /// <c>.material</c>로 바꾸면 피격마다 인스턴스가 생기고, 겹친 플래시가 "섞인 색"을 원색으로
        /// 잡아 영구히 물드는 문제가 있었다(<c>PlayHitFlashCoroutine</c>의 방식).
        /// </summary>
        private void SetFlash(GameObject model, Color tint, float amount)
        {
            if (model == null) return;
            MeshRenderer[] renderers = GetRenderersCached(model);
            if (renderers == null) return;
            if (flashBlock == null) flashBlock = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null) continue;
                if (amount <= 0f)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                Material shared = r.sharedMaterial;
                if (shared == null || !shared.HasProperty("_Color")) continue;
                Color baseColor = shared.color;
                Color mixed = Color.Lerp(baseColor, tint, amount);
                mixed.a = baseColor.a;
                flashBlock.Clear();
                flashBlock.SetColor("_Color", mixed);
                r.SetPropertyBlock(flashBlock);
            }
        }

        // ── 임팩트 버스트 ──

        /// <summary>
        /// 흰 섬광 구 + 방사 불꽃 줄기 + 화면을 향한 충격파 고리. 가산 합성이라 어두운 숲 아레나에서
        /// 빛처럼 읽힌다. 속성 임팩트(물보라·전격…)는 그대로 두고 그 위에 "맞았다"를 얹는다.
        /// </summary>
        private IEnumerator ImpactBurstCoroutine(Vector3 point, Color color, float weight)
        {
            if (arenaRoot == null) yield break;
            Color hot = Color.Lerp(color, Color.white, 0.55f);
            Material coreMat = CreateFxMaterial(new Color(1f, 0.97f, 0.86f, 1f), true);
            Material sparkMat = CreateFxMaterial(hot, true);
            Material ringMat = CreateFxMaterial(hot, true);

            GameObject core = FxPrimitive(PrimitiveType.Sphere, "ImpactCore", point, coreMat);
            int sparkCount = 8 + Mathf.RoundToInt(6f * weight);
            var sparks = new GameObject[sparkCount];
            var velocities = new Vector3[sparkCount];
            Camera cam = Camera.main;
            Vector3 toCam = cam != null ? (cam.transform.position - point).normalized : Vector3.back;
            for (int i = 0; i < sparkCount; i++)
            {
                // 화면 평면 쪽으로 퍼지게 — 카메라를 향한 성분을 줄여야 줄기가 점으로 안 보인다.
                Vector3 v = Random.onUnitSphere;
                v -= toCam * Vector3.Dot(v, toCam) * 0.7f;
                v.y = Mathf.Abs(v.y) * 0.8f + 0.1f;
                velocities[i] = v.normalized * Random.Range(4.5f, 7.5f) * (0.8f + 0.4f * weight);
                sparks[i] = FxPrimitive(PrimitiveType.Cube, "ImpactStreak", point, sparkMat);
                sparks[i].transform.rotation = Quaternion.LookRotation(velocities[i]);
            }

            GameObject ringObj = new GameObject("ImpactRing");
            ringObj.transform.SetParent(arenaRoot.transform, false);
            ringObj.transform.position = point;
            ringObj.transform.rotation = Quaternion.LookRotation(-toCam);
            LineRenderer ring = ringObj.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 36;
            ring.sharedMaterial = ringMat;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;

            const float Duration = 0.32f;
            float coreSize = 0.7f + 0.5f * weight;
            float ringSize = 0.8f + 0.6f * weight;
            float t = 0f;
            while (t < Duration)
            {
                if (arenaRoot == null) yield break;
                t += PresentationDelta;
                float p = Mathf.Clamp01(t / Duration);

                float cp = Mathf.Clamp01(t / 0.14f);
                if (core != null)
                {
                    core.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, coreSize, 1f - (1f - cp) * (1f - cp));
                    coreMat.color = new Color(1f, 0.97f, 0.86f, 1f - cp);
                    if (cp >= 1f) { Destroy(core); core = null; }
                }

                sparkMat.color = new Color(hot.r, hot.g, hot.b, 1f - p);
                for (int i = 0; i < sparkCount; i++)
                {
                    if (sparks[i] == null) continue;
                    velocities[i] *= Mathf.Max(0f, 1f - 2.5f * PresentationDelta);   // 공기 저항
                    sparks[i].transform.position += velocities[i] * PresentationDelta;
                    float len = Mathf.Lerp(0.55f, 0.08f, p);
                    sparks[i].transform.localScale = new Vector3(0.045f, 0.045f, len);
                }

                float radius = Mathf.Lerp(0.25f, ringSize, 1f - (1f - p) * (1f - p));
                ring.widthMultiplier = Mathf.Lerp(0.16f, 0.01f, p);
                ringMat.color = new Color(hot.r, hot.g, hot.b, 1f - p);
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float a = i * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
                }
                yield return null;
            }
            if (core != null) Destroy(core);
            for (int i = 0; i < sparkCount; i++) if (sparks[i] != null) Destroy(sparks[i]);
            Destroy(ringObj);
        }

        private GameObject FxPrimitive(PrimitiveType type, string name, Vector3 position, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(arenaRoot.transform, false);
            go.transform.position = position;
            Object.Destroy(go.GetComponent<Collider>());
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>
        /// 빛·불꽃용 머티리얼. <c>Hidden/Internal-Colored</c>는 런타임 GL 그리기용으로 플레이어 빌드에
        /// 항상 들어 있고 블렌드를 속성으로 바꿀 수 있다 — Standard의 투명 변형은 빌드에서 빠지면
        /// 불투명으로 그려진다(<c>CreateBossAura</c> 주석). 없으면 알파 블렌드인 Sprites/Default로.
        /// </summary>
        private Material CreateFxMaterial(Color color, bool additive)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", additive
                    ? (int)UnityEngine.Rendering.BlendMode.One
                    : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                mat.SetInt("_ZWrite", 0);
            }
            else
            {
                // 이 경로는 CreateTransparentMaterial로 돌아가면 안 된다 — 그쪽이 여기를 부른다.
                shader = Shader.Find("Sprites/Default");
                if (shader == null) return CreateSafeMaterial(color);
                mat = new Material(shader);
            }
            mat.renderQueue = 3100;
            mat.color = color;
            runtimeMaterials.Add(mat);   // CleanupArena가 일괄 파기
            return mat;
        }

        // ── 연출 카메라 ──

        private void RememberBaseCamera(Vector3 camPos, Vector3 lookTarget)
        {
            baseCamPos = camPos;
            Vector3 d = lookTarget - camPos;
            baseCamRot = d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d) : Quaternion.identity;
            hasBaseCam = true;
        }

        /// <summary>
        /// 스킬 타임라인 한 프레임의 샷을 카메라에 건다. 중심점은 연출 시작 때 한 번 잰 값을 쓴다 —
        /// 돌진하는 시전자를 매 프레임 쫓으면 카메라가 덜컹거린다.
        /// </summary>
        private void ApplyCameraShot(float progress, float duration, Vector3 attackerCenter,
            Vector3 targetCenter, float weight, bool support)
        {
            if (!hasBaseCam || BattlePresentation.ReducedMotion) return;
            CameraFollower follower = ResolveFollower();
            if (follower == null) return;
            BattleCameraDirector.Shot shot = BattleCameraDirector.Evaluate(BattleCameraDirector.Current,
                progress, duration, baseCamPos, baseCamRot, attackerCenter, targetCenter, weight, support);
            follower.SetBattleShot(shot.Position, shot.Rotation, shot.Weight);
        }

        private void ClearCameraShot()
        {
            CameraFollower follower = ResolveFollower();
            if (follower != null) follower.ClearBattleShot();
        }

        /// <summary>아레나 정리 — 남은 정지·샷·외침·반응 기록을 버린다.</summary>
        private void ClearImpactState()
        {
            BattlePresentation.ClearTimeEffects();
            ClearCameraShot();
            hasBaseCam = false;
            activeShouts.Clear();
            activeReacts.Clear();
            reactRestPoses.Clear();
            telegraphRoutine = null;          // StopAllCoroutines가 이미 멈췄다 — 참조만 버린다
            telegraphPoseSaved = false;
            telegraphFx.Clear();              // 오브젝트는 arenaRoot와 함께 파기된다
        }
    }
}
