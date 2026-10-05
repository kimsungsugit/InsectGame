using System.Collections;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 전투 체감 연출 — ① 전용기(시전자 클로즈업 컷인 → 속성색 큰 이펙트 여러 겹 → 타격), ② 사마귀 베기의 칼날 궤적,
    /// ③ 승리(내 곤충의 계열별 포즈 + 카메라 반 바퀴 / 레이드 팀 전원 점프 + 팀을 담는 카메라), ④ 전투 진입 샷(상대 옆에서 크게 휘돌아 들어온다).
    /// 시각표·포즈는 <see cref="BattleFlourish"/>, 카메라 샷은 <see cref="BattleCameraDirector"/>(둘 다 순수).
    ///
    /// <b>ui-dev가 읽는 공개 값</b> — 전용기 컷인 글자: <see cref="IsSignaturePlaying"/>·<see cref="IsSignatureCutIn"/>·<see cref="SignatureCutInProgress"/>·
    /// <see cref="SignatureFromPlayer"/>·<see cref="SignatureSkillName"/>·<see cref="SignatureElement"/>. 「승리!」: <see cref="IsVictoryPlaying"/>·
    /// <see cref="VictoryFinished"/>·<see cref="VictoryProgress"/>. 진입 배너: <see cref="IsOpeningPlaying"/>·<see cref="OpeningProgress"/>.
    ///
    /// <b>머티리얼</b>: 빛·연기·조각은 전부 <see cref="CreateFxMaterial"/>, 흙·잎 조각만 불투명 <see cref="CreateSafeMaterial"/>,
    /// 쇳조각은 <see cref="CreateSheenMaterial"/> — 새 셰이더·키워드가 없다(Standard 변형 <c>BuildKeepers</c>에 기대지 않는다).
    /// </summary>
    public partial class BattleArenaController
    {
        // ───────────── 전용기 상태(ui-dev 컷인 글자가 읽는다) ─────────────

        private bool signatureActive;
        private float signatureElapsed;
        private float signatureCutInEnd;
        private float signatureImpactSeconds;
        private bool signatureFromPlayer;
        private string signatureSkillName;
        private InsectElement signatureElement;

        /// <summary>
        /// 지금 전용기 연출 중인가 — 스킬 연출 첫 프레임부터 타격 뒤 <see cref="BattleFlourish.SignatureTailSeconds"/>까지(1대1),
        /// 레이드는 그 팀원의 볼리(0.46초) + 꼬리. ui-dev가 기술 이름 컷인을 이 동안 그린다.
        /// </summary>
        public bool IsSignaturePlaying => signatureActive;
        /// <summary>전용기 연출이 시작된 뒤 지난 연출 시계 초(연출 중이 아니면 0).</summary>
        public float SignatureElapsed => signatureActive ? signatureElapsed : 0f;
        /// <summary>
        /// 이번 연출의 컷인 끝(연출 시계 초) — 보통 <see cref="BattleFlourish.SignatureCutInEnd"/>(0.5초), 타격이 이른 짧은 연출이면 그보다 짧다.
        /// 레이드는 볼리 타격(0.46초) 전에 끝난다.
        /// </summary>
        public float SignatureCutInEndSeconds => signatureActive ? signatureCutInEnd : 0f;
        /// <summary>이번 연출의 타격 시각(연출 시계 초) — 1대1 스킬(2.5초)이면 0.9~1.0초.</summary>
        public float SignatureImpactSeconds => signatureActive ? signatureImpactSeconds : 0f;
        /// <summary>시전자 클로즈업 컷인 구간인가(<see cref="BattleFlourish.SignatureCutInStart"/>~<see cref="SignatureCutInEndSeconds"/>).</summary>
        public bool IsSignatureCutIn => signatureActive && signatureElapsed < signatureCutInEnd;
        /// <summary>컷인 진행률 0~1(컷인 밖이면 0이 아니라 1 — 컷인이 지났다). 연출 중이 아니면 0.</summary>
        public float SignatureCutInProgress => !signatureActive ? 0f
            : Mathf.Clamp01(signatureElapsed / Mathf.Max(0.01f, signatureCutInEnd));
        /// <summary>전용기를 쓴 쪽이 내 곤충(레이드는 팀원)인가 — 컷인을 어느 쪽에서 들일지.</summary>
        public bool SignatureFromPlayer => signatureFromPlayer;
        /// <summary>전용기 이름(연출 중이 아니면 null).</summary>
        public string SignatureSkillName => signatureActive ? signatureSkillName : null;
        /// <summary>전용기 속성 — 컷인 띠 색(<see cref="GetUIElementColor"/>).</summary>
        public InsectElement SignatureElement => signatureElement;

        /// <summary>
        /// 전용기 머리 위 기술 이름 말풍선(<c>BeginSkillPresentation</c>)을 띄울지. ui-dev의 컷인이 같은 이름을 크게 그리면 꺼서 겹치지 않게 한다.
        /// </summary>
        public static bool ShowSignatureCallout = true;

        private void BeginSignature(GameObject caster, InsectElement element, HitCue cue, bool fromPlayer, float impactSeconds, float cutInEnd)
        {
            signatureActive = true;
            signatureElapsed = 0f;
            signatureImpactSeconds = impactSeconds;
            signatureCutInEnd = cutInEnd;
            signatureFromPlayer = fromPlayer;
            signatureSkillName = cue.SkillName;
            signatureElement = element;
            if (caster != null) StartCoroutine(SignatureChargeCoroutine(caster, element, Mathf.Max(0.25f, impactSeconds)));
        }

        private void EndSignature()
        {
            signatureActive = false;
            signatureElapsed = 0f;
        }

        // ───────────── 전용기 — 기 모으기 ─────────────

        /// <summary>
        /// 컷인 동안 시전자가 기를 모은다 — 발밑에서 솟는 속성색 빛기둥, 몸 둘레를 나선으로 감아 오르는 속성 조각(잎·물방울·번개·돌…),
        /// 발밑 고리, 몸이 속성색으로 맥동한다. 타격 직전에 조각이 시전자 앞으로 모인다. 밝은 섬광 줄이기면 빛기둥·맥동 없이 조각만.
        /// </summary>
        private IEnumerator SignatureChargeCoroutine(GameObject caster, InsectElement element, float seconds)
        {
            if (arenaRoot == null || caster == null) yield break;
            Color color = GetElementColor3D(element);
            Color glow = Color.Lerp(color, Color.white, 0.35f);
            bool flashes = !BattlePresentation.ReducedFlashes;
            Bounds b = BattleFraming.ModelBounds(caster);
            float radius = Mathf.Max(0.45f, Mathf.Max(b.extents.x, b.extents.z) * 1.1f);
            float height = Mathf.Max(0.6f, b.size.y);
            Vector3 feet = new Vector3(b.center.x, b.min.y + 0.03f, b.center.z);
            // 몸 중심은 루트 기준 어긋남으로 따라간다 — 매 프레임 경계를 다시 재면 렌더러 배열을 새로 만든다.
            Vector3 centerOffset = b.center - caster.transform.position;
            float feetOffset = feet.y - caster.transform.position.y;
            var fx = new List<GameObject>();

            Material columnMat = null;
            GameObject column = null;
            if (flashes)
            {
                columnMat = CreateFxMaterial(new Color(glow.r, glow.g, glow.b, 0f), true);
                column = FxPrimitive(PrimitiveType.Cylinder, "SignatureColumn", feet + Vector3.up * height, columnMat);
                fx.Add(column);
            }
            StartCoroutine(GroundRingCoroutine(feet, radius * 1.6f, radius * 0.5f, 0.12f,
                new Color(glow.r, glow.g, glow.b, 0.85f), Mathf.Min(0.6f, seconds), flashes));

            const int Shards = 10;
            Material shardMat = ShardMaterial(element, color, flashes);
            var shards = new Transform[Shards];
            for (int i = 0; i < Shards; i++)
            {
                GameObject s = CreateElementShard(element, shardMat, feet);
                fx.Add(s);
                shards[i] = s.transform;
            }

            // 조각이 모이는 쪽 — 시전자가 바라보는 앞(모델은 상대를 향해 서 있다).
            Vector3 toward = caster.transform.forward;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;

            float t = 0f;
            try
            {
                while (t < seconds)
                {
                    if (arenaRoot == null || caster == null) yield break;
                    t += PresentationDelta;
                    float k = Mathf.Clamp01(t / seconds);
                    Vector3 center = caster.transform.position + centerOffset;
                    feet.y = caster.transform.position.y + feetOffset;
                    if (column != null)
                    {
                        float width = Mathf.Lerp(0.15f, radius * 1.3f, Mathf.SmoothStep(0f, 1f, k / 0.35f)) * (1f - 0.6f * k * k);
                        column.transform.position = new Vector3(center.x, feet.y + height, center.z);
                        column.transform.localScale = new Vector3(width, height, width);
                        columnMat.color = new Color(glow.r, glow.g, glow.b, 0.45f * Mathf.Sin(Mathf.Min(1f, k * 1.15f) * Mathf.PI));
                    }
                    // 나선으로 감아 오르다(0~0.75) 시전자 앞 한 점으로 모인다(0.75~1).
                    float gather = Mathf.SmoothStep(0f, 1f, (k - 0.75f) / 0.25f);
                    Vector3 focus = center + toward * (radius * 0.9f);
                    for (int i = 0; i < Shards; i++)
                    {
                        if (shards[i] == null) continue;
                        float a = i * Mathf.PI * 2f / Shards + t * 7f;
                        float rise = Mathf.Repeat(k * 1.6f + i * 0.1f, 1f);
                        float r = radius * Mathf.Lerp(1.25f, 0.55f, rise);
                        Vector3 spiral = new Vector3(center.x + Mathf.Cos(a) * r, feet.y + rise * height * 1.1f, center.z + Mathf.Sin(a) * r);
                        shards[i].position = Vector3.Lerp(spiral, focus, gather);
                        shards[i].rotation = Quaternion.Euler(t * 300f + i * 37f, t * 410f, i * 50f);
                        shards[i].localScale = ShardScale(element, radius * 0.22f * (1f - 0.5f * gather));
                    }
                    if (flashes) SetFlash(caster, glow, 0.18f + 0.22f * Mathf.Abs(Mathf.Sin(t * 14f)));
                    yield return null;
                }
            }
            finally
            {
                if (caster != null && flashes) SetFlash(caster, Color.white, 0f);
                for (int i = 0; i < fx.Count; i++) if (fx[i] != null) Destroy(fx[i]);
            }
        }

        // ───────────── 전용기 — 큰 이펙트 ─────────────

        /// <summary>
        /// 전용기 타격 — 속성 임팩트를 세 겹(가운데 + 좌우로 0.07초씩 늦게), 그 위에 속성 조각이 크게 사방으로 터지고(속성마다 다르게 난다 —
        /// 잎은 팔랑이며, 물은 포물선으로 떨어지며, 번개는 지그재그로, 바람은 소용돌이로, 흙은 무겁게, 독은 부풀며 떠오른다), 화면을 향한
        /// 큰 충격 고리 두 개, 흔들림. 보통 기술보다 확실히 크게(<see cref="BattleFlourish.SignatureBurstScale"/>). 밝은 섬광 줄이기면 조각만.
        /// </summary>
        private void PlaySignatureBurst(Vector3 point, InsectElement element, float size)
        {
            if (arenaRoot == null) return;
            Color color = GetElementColor3D(element);
            Camera cam = Camera.main;
            Vector3 side = cam != null ? cam.transform.right : Vector3.right;
            if (!BattlePresentation.ReducedFlashes)
            {
                StartCoroutine(DelayedElementImpact(point + side * (0.55f * size) + Vector3.up * 0.15f, element, color, 0.07f));
                StartCoroutine(DelayedElementImpact(point - side * (0.55f * size) + Vector3.up * 0.1f, element, color, 0.14f));
                StartCoroutine(ImpactBurstCoroutine(point, Color.Lerp(color, Color.white, 0.2f), 1f));
                StartCoroutine(SignatureRingCoroutine(point, color, size));
            }
            StartCoroutine(SignatureShardBurstCoroutine(point, element, color, size));
            CameraFollower follower = ResolveFollower();
            if (follower != null) follower.Shake(0.4f, 0.5f);
        }

        /// <summary>큰 이펙트 크기 — 대상 몸집을 따르되(1대1 곤충 ≈1.0, 레이드 보스 ≈2.0) 화면을 덮지 않게 자른다.</summary>
        private static float SignatureBurstSize(GameObject target)
        {
            return BattleFlourish.SignatureBurstScale * Mathf.Clamp(FlareSize(target) * 0.7f, 0.55f, 1.1f);
        }

        private IEnumerator DelayedElementImpact(Vector3 point, InsectElement element, Color color, float delay)
        {
            float t = 0f;
            while (t < delay)
            {
                if (arenaRoot == null) yield break;
                t += PresentationDelta;
                yield return null;
            }
            CreateElementImpact3D(point, element, color);
        }

        /// <summary>화면을 향한 큰 충격 고리 둘 — 0.1초 간격으로 반지름 2.6배까지 퍼진다(가산).</summary>
        private IEnumerator SignatureRingCoroutine(Vector3 point, Color color, float size)
        {
            if (arenaRoot == null) yield break;
            Camera cam = Camera.main;
            Vector3 toCam = cam != null ? (cam.transform.position - point).normalized : Vector3.back;
            Color hot = Color.Lerp(color, Color.white, 0.45f);
            Material mat = CreateFxMaterial(new Color(hot.r, hot.g, hot.b, 0f), true);
            var rings = new LineRenderer[2];
            for (int i = 0; i < rings.Length; i++)
            {
                var go = new GameObject("SignatureRing");
                go.transform.SetParent(arenaRoot.transform, false);
                go.transform.position = point + toCam * 0.2f;
                go.transform.rotation = Quaternion.LookRotation(-toCam);
                rings[i] = go.AddComponent<LineRenderer>();
                rings[i].useWorldSpace = false;
                rings[i].loop = true;
                rings[i].positionCount = 44;
                rings[i].sharedMaterial = mat;
                rings[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rings[i].receiveShadows = false;
                rings[i].widthMultiplier = 0f;
            }
            const float Each = 0.5f;
            const float Stagger = 0.1f;
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
                    float radius = Mathf.Lerp(0.3f, 2.6f * size, 1f - (1f - k) * (1f - k));
                    SetFacingRingRadius(rings[i], radius, Mathf.Lerp(0.22f, 0.02f, k));
                    alpha = Mathf.Max(alpha, 0.9f * (1f - k));
                }
                mat.color = new Color(hot.r, hot.g, hot.b, alpha);
                yield return null;
            }
            for (int i = 0; i < rings.Length; i++) if (rings[i] != null) Destroy(rings[i].gameObject);
        }

        /// <summary>속성 조각 열넷이 화면 평면 쪽으로 크게 터진다 — 속성마다 나는 모양이 다르다.</summary>
        private IEnumerator SignatureShardBurstCoroutine(Vector3 point, InsectElement element, Color color, float size)
        {
            if (arenaRoot == null) yield break;
            Material mat = ShardMaterial(element, color, !BattlePresentation.ReducedFlashes);
            Camera cam = Camera.main;
            Vector3 toCam = cam != null ? (cam.transform.position - point).normalized : Vector3.back;
            const int Count = 14;
            var shards = new Transform[Count];
            var dirs = new Vector3[Count];
            uint seed = 52711u + (uint)element * 977u;
            for (int i = 0; i < Count; i++)
            {
                shards[i] = CreateElementShard(element, mat, point).transform;
                float a = (i + NextRand(ref seed) * 0.5f) * Mathf.PI * 2f / Count;
                Vector3 d = Quaternion.LookRotation(-toCam) * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                d.y = Mathf.Abs(d.y) * 0.8f + 0.15f;
                dirs[i] = d.normalized;
            }
            const float Duration = 0.65f;
            float reach = 2.4f * size;
            float t = 0f;
            while (t < Duration)
            {
                if (arenaRoot == null) yield break;
                t += PresentationDelta;
                float k = Mathf.Clamp01(t / Duration);
                float ease = 1f - (1f - k) * (1f - k);
                for (int i = 0; i < Count; i++)
                {
                    if (shards[i] == null) continue;
                    shards[i].position = point + ShardFlight(element, dirs[i], ease, k, i) * reach;
                    shards[i].rotation = Quaternion.Euler(t * 520f + i * 23f, t * 380f, i * 41f);
                    shards[i].localScale = ShardScale(element, 0.16f * size * (1f - 0.7f * k));
                }
                yield return null;
            }
            for (int i = 0; i < Count; i++) if (shards[i] != null) Destroy(shards[i].gameObject);
        }

        /// <summary>속성마다 조각이 나는 길(반지름 1 기준) — 같은 방향이라도 결이 다르게.</summary>
        private static Vector3 ShardFlight(InsectElement element, Vector3 dir, float ease, float k, int i)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            side.Normalize();
            switch (element)
            {
                case InsectElement.Leaf:   // 팔랑이며 퍼진다
                    return dir * ease + side * (Mathf.Sin(k * 14f + i) * 0.12f) + Vector3.down * (0.25f * k * k);
                case InsectElement.Water:  // 포물선으로 솟았다 떨어진다
                    return dir * ease + Vector3.up * (0.5f * Mathf.Sin(k * Mathf.PI)) + Vector3.down * (0.6f * k * k);
                case InsectElement.Electric:   // 지그재그
                    return dir * ease + side * ((Mathf.Repeat(k * 9f + i * 0.3f, 1f) < 0.5f ? 1f : -1f) * 0.1f);
                case InsectElement.Wind:   // 소용돌이
                {
                    float a = k * Mathf.PI * 3f + i;
                    return dir * ease + (side * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * (0.25f * (1f - k));
                }
                case InsectElement.Earth:  // 무겁게 솟았다 떨어진다
                    return dir * (ease * 0.8f) + Vector3.up * (0.7f * Mathf.Sin(k * Mathf.PI)) + Vector3.down * (0.9f * k * k);
                case InsectElement.Poison: // 부풀며 위로 떠오른다
                    return dir * (ease * 0.7f) + Vector3.up * (0.5f * k);
                case InsectElement.Dark:   // 한 번 빨려 들었다 터진다
                    return dir * (k < 0.25f ? -0.15f * (k / 0.25f) : Mathf.Lerp(-0.15f, 1.1f, (k - 0.25f) / 0.75f));
                default:
                    return dir * ease;
            }
        }

        private static Vector3 ShardScale(InsectElement element, float s)
        {
            switch (element)
            {
                case InsectElement.Leaf: return new Vector3(s * 1.4f, s * 0.12f, s * 0.7f);
                case InsectElement.Electric: return new Vector3(s * 0.22f, s * 1.6f, s * 0.22f);
                case InsectElement.Metal: return new Vector3(s * 1.7f, s * 0.14f, s * 0.45f);
                case InsectElement.Light: return new Vector3(s * 0.3f, s * 1.5f, s * 0.3f);
                case InsectElement.Wind: return new Vector3(s * 1.4f, s * 0.12f, s * 0.12f);
                default: return Vector3.one * s;
            }
        }

        /// <summary>속성 조각 머티리얼 — 빛나는 속성은 가산, 물·독·어둠은 반투명, 흙·잎은 불투명, 쇠는 광택.</summary>
        private Material ShardMaterial(InsectElement element, Color color, bool glow)
        {
            switch (element)
            {
                case InsectElement.Earth:
                case InsectElement.Leaf:
                    return CreateSafeMaterial(color);
                case InsectElement.Metal:
                    return CreateSheenMaterial(new Color(0.82f, 0.85f, 0.9f), SurfaceKind.Metal);
                case InsectElement.Water:
                case InsectElement.Poison:
                case InsectElement.Dark:
                    return CreateFxMaterial(new Color(color.r, color.g, color.b, 0.75f), false);
                default:
                    return CreateFxMaterial(new Color(color.r, color.g, color.b, 0.95f), glow);
            }
        }

        private GameObject CreateElementShard(InsectElement element, Material mat, Vector3 position)
        {
            PrimitiveType shape;
            switch (element)
            {
                case InsectElement.Water:
                case InsectElement.Poison:
                case InsectElement.Bug:
                case InsectElement.None:
                    shape = PrimitiveType.Sphere;
                    break;
                default:
                    shape = PrimitiveType.Cube;
                    break;
            }
            GameObject go = FxPrimitive(shape, "SignatureShard", position, mat);
            go.transform.localScale = Vector3.zero;
            return go;
        }

        // ───────────── 사마귀 베기 ─────────────

        /// <summary>
        /// 칼날 궤적 — 시전자 앞에서 대상 쪽으로 휘는 활 모양 선(가산, 0.2초). 첫 번째와 두 번째 베기는 반대 대각선이다.
        /// 밝은 섬광 줄이기면 그리지 않는다(몸의 휘두름은 남는다).
        /// </summary>
        private void PlaySlashArc(GameObject attacker, GameObject target, Color color, bool first)
        {
            if (arenaRoot == null || attacker == null || target == null || BattlePresentation.ReducedFlashes) return;
            StartCoroutine(SlashArcCoroutine(attacker, target, color, first));
        }

        private IEnumerator SlashArcCoroutine(GameObject attacker, GameObject target, Color color, bool first)
        {
            Bounds ab = BattleFraming.ModelBounds(attacker);
            Bounds tb = BattleFraming.ModelBounds(target);
            Vector3 dir = tb.center - ab.center;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;
            Vector3 center = Vector3.Lerp(ab.center, tb.center, 0.62f);
            float span = Mathf.Max(0.5f, Mathf.Max(tb.extents.y, ab.extents.y) * 1.5f);
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            // 대각선 — 첫 베기는 위-앞에서 아래-뒤로, 두 번째는 반대.
            Vector3 axis = (Vector3.up * (first ? 1f : -1f) + side * 0.8f).normalized;
            Color hot = Color.Lerp(color, Color.white, 0.6f);
            Material mat = CreateFxMaterial(new Color(hot.r, hot.g, hot.b, 0.95f), true);
            var go = new GameObject("SlashArc");
            go.transform.SetParent(arenaRoot.transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 14;
            line.sharedMaterial = mat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.01f), new Keyframe(0.55f, 1f), new Keyframe(1f, 0.02f));
            const float Duration = 0.2f;
            float t = 0f;
            while (t < Duration)
            {
                if (arenaRoot == null || line == null) yield break;
                t += PresentationDelta;
                float k = Mathf.Clamp01(t / Duration);
                float sweep = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.55f));
                for (int i = 0; i < line.positionCount; i++)
                {
                    float u = (float)i / (line.positionCount - 1) * sweep;
                    float s = Mathf.Lerp(-0.5f, 0.5f, u);
                    line.SetPosition(i, center + axis * (s * span) + dir * (0.25f * span * (1f - 4f * s * s)));
                }
                line.widthMultiplier = 0.14f * (1f - k * 0.7f);
                mat.color = new Color(hot.r, hot.g, hot.b, 0.95f * (1f - k * k));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ───────────── 승리 ─────────────

        private bool victoryPlaying;
        private bool victoryDone;
        private float victoryElapsed;

        /// <summary>승리 연출이 도는 중인가(<see cref="BattleFlourish.VictorySeconds"/>, 실제 초). ui-dev가 「승리!」를 이 동안 띄운다.</summary>
        public bool IsVictoryPlaying => victoryPlaying;
        /// <summary>
        /// 승리 연출이 끝까지 돌았다 — 그 뒤 카메라는 마지막 구도에 머문다(결과 화면 뒤). 이번 전투에서 승리 연출을 안 했으면 false.
        /// 움직임 줄이기면 몸·카메라는 그대로이고 시간만 흐른다(이 값은 같은 시각에 선다).
        /// </summary>
        public bool VictoryFinished => victoryDone;
        /// <summary>승리 연출 진행률 0~1(시작 전 0, 끝나면 1).</summary>
        public float VictoryProgress => victoryDone ? 1f : victoryPlaying ? Mathf.Clamp01(victoryElapsed / BattleFlourish.VictorySeconds) : 0f;

        private sealed class VictoryState
        {
            public GameObject[] Heroes;
            public Pose3[] Rests;
            public BattleMotion.Family[] Families;
            public float[] Delays;
            public Vector3 Pivot;
            public Vector3 Face;
            public float Orbit;
            public float EndDistance;
            public float EndHeight;
        }

        /// <summary>
        /// 1대1 승리 — 내 곤충이 계열별 승리 포즈(<see cref="BattleFlourish.VictoryPose"/>)를 잡고, 카메라가 그 둘레를 얼굴 쪽으로 반 바퀴
        /// (<see cref="BattleFlourish.VictoryOrbitDegrees"/>) 돌며 다가간다(<see cref="BattleFlourish.VictorySeconds"/>). 결과 화면에 들어가는 순간
        /// <c>BattleScreenUI</c>가 부른다(상대의 쓰러짐은 이미 끝났다). 패배·도망은 부르지 않는다.
        /// </summary>
        public void PlayVictory()
        {
            if (!isActive || arenaRoot == null || playerModel == null || !playerModel.activeInHierarchy || bossModel != null) return;
            Bounds b = BattleFraming.ModelBounds(playerModel);
            float baseDistance = hasBaseCam ? Vector3.Distance(new Vector3(baseCamPos.x, 0f, baseCamPos.z), new Vector3(b.center.x, 0f, b.center.z)) : 6f;
            var st = new VictoryState
            {
                Heroes = new[] { playerModel },
                Pivot = b.center,
                Face = playerModel.transform.forward,
                Orbit = BattleFlourish.VictoryOrbitDegrees,
                EndDistance = Mathf.Max(BattleFlourish.VictoryMinDistance, Mathf.Max(b.extents.x, b.extents.y) * 3.2f,
                    baseDistance * BattleFlourish.VictoryCloseRatio),
                EndHeight = 0.15f + b.extents.y * 0.2f
            };
            StartVictory(st);
        }

        /// <summary>
        /// 레이드 승리 — 서 있는 팀원 모두가 0.08초씩 어긋나 계열별 포즈로 뛰어오르고, 카메라가 팀 뒤에서 팀 앞(보스가 있던 쪽)으로
        /// 돌아 다섯 마리를 한 화면에 담는다. 보스 쓰러짐이 끝난 뒤(결과 단계 진입) <c>RaidBattleUI</c>가 부른다.
        /// </summary>
        public void PlayRaidVictory()
        {
            if (!isActive || arenaRoot == null || teamModels == null) return;
            var heroes = new List<GameObject>(teamModels.Length);
            for (int i = 0; i < teamModels.Length; i++)
                if (IsStanding(teamModels[i])) heroes.Add(teamModels[i]);
            if (heroes.Count == 0) return;
            Bounds b = BattleFraming.ModelBounds(heroes[0]);
            for (int i = 1; i < heroes.Count; i++) b.Encapsulate(BattleFraming.ModelBounds(heroes[i]));
            Camera cam = Camera.main;
            float fov = cam != null ? cam.fieldOfView : 42f;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            Vector3 face = bossBattlePos - b.center;
            var st = new VictoryState
            {
                Heroes = heroes.ToArray(),
                Pivot = b.center,
                Face = face,
                Orbit = BattleFlourish.RaidVictoryOrbitDegrees,
                EndDistance = BattleFlourish.FitDistance(Mathf.Max(b.extents.x, b.extents.z) + 0.4f, b.extents.y + 0.9f, fov, aspect),
                EndHeight = 0.9f + b.extents.y
            };
            StartVictory(st);
        }

        private void StartVictory(VictoryState st)
        {
            int n = st.Heroes.Length;
            st.Rests = new Pose3[n];
            st.Families = new BattleMotion.Family[n];
            st.Delays = new float[n];
            for (int i = 0; i < n; i++)
            {
                st.Rests[i] = PoseOf(st.Heroes[i]);
                InsectEntity entity = st.Heroes[i].GetComponent<InsectEntity>();
                st.Families[i] = BattleMotion.FamilyOf(entity != null && entity.Data != null ? entity.Data.insectId : null);
                st.Delays[i] = i * BattleFlourish.RaidVictoryStagger;
            }
            // 벽이 카메라 뒤에 서지 않게 — 끝 자리까지 넉넉히 민다(수문장 샷과 같은 방법).
            float reach = Mathf.Max(st.EndDistance, hasBaseCam ? Vector3.Distance(baseCamPos, st.Pivot) : 0f);
            float span = Mathf.Max(CurrentWallSpan(), Mathf.Abs(st.Pivot.x - arenaCenter.x) + reach + 2f,
                Mathf.Abs(st.Pivot.z - arenaCenter.z) + reach + 2f);
            PlaceArenaWalls(span);
            victoryPlaying = true;
            victoryDone = false;
            victoryElapsed = 0f;
            BeginStaging(VictoryCoroutine(st), () => FinishVictory(st));
        }

        private IEnumerator VictoryCoroutine(VictoryState st)
        {
            bool motion = !BattlePresentation.ReducedMotion;
            bool cried = false;
            float t = 0f;
            while (isActive && arenaRoot != null)
            {
                for (int i = 0; i < st.Heroes.Length; i++)
                {
                    GameObject hero = st.Heroes[i];
                    if (hero == null || !motion) continue;
                    BattleMotion.Pose pose = BattleFlourish.VictoryPose(st.Families[i], t - st.Delays[i]);
                    Pose3 rest = st.Rests[i];
                    hero.transform.SetPositionAndRotation(rest.Position + Vector3.up * pose.Lift,
                        Quaternion.AngleAxis(pose.Yaw, Vector3.up) * rest.Rotation * Quaternion.Euler(pose.Pitch, 0f, pose.Roll));
                }
                if (!cried && t >= 0.15f)
                {
                    cried = true;
                    if (AudioManager.Instance != null && st.Heroes.Length > 0 && st.Heroes[0] != null)
                        AudioManager.Instance.PlayCry(CryKey(CryOf(st.Heroes[0], false), false));
                }
                if (hasBaseCam)
                    ApplyStagingShot(BattleCameraDirector.EvaluateVictoryOrbit(t, baseCamPos, baseCamRot, st.Pivot, st.Face,
                        st.Orbit, st.EndDistance, st.EndHeight));
                victoryElapsed = t;
                if (!victoryDone && t >= BattleFlourish.VictorySeconds)
                {
                    victoryDone = true;
                    victoryPlaying = false;
                }
                yield return null;
                // 실제 초 — 결과 화면(ResultShownSeconds)과 같은 시계라 2배속에서도 「승리!」와 박자가 같다.
                t += Time.deltaTime * BattlePresentation.TimeScale;
            }
        }

        private void FinishVictory(VictoryState st)
        {
            for (int i = 0; i < st.Heroes.Length; i++)
                if (st.Heroes[i] != null) SetPose(st.Heroes[i], st.Rests[i]);
            victoryPlaying = false;
            ClearCameraShot();
        }

        // ───────────── 전투 진입 샷 ─────────────

        private bool openingPlaying;
        private float openingElapsed;

        /// <summary>전투 진입 샷이 도는 중인가(<see cref="BattleFlourish.OpeningShotSeconds"/>, 실제 초).</summary>
        public bool IsOpeningPlaying => openingPlaying;
        /// <summary>진입 샷 진행률 0~1(시작 전·끝난 뒤 1이 아니라 — 돌지 않으면 1).</summary>
        public float OpeningProgress => openingPlaying ? Mathf.Clamp01(openingElapsed / BattleFlourish.OpeningShotSeconds) : 1f;

        /// <summary>
        /// 1대1 전투 진입 — 카메라가 상대 바로 옆(배틀 구도에서 상대 쪽으로 75° 돌아간 낮은 자리)에서 상대를 보며 시작해, 크게 휘돌아 물러나
        /// 배틀 구도에 내려앉는다(<see cref="BattleFlourish.OpeningShotSeconds"/>, 실제 초). <c>BattleScreenUI</c>가 아레나를 세운 직후(인트로 시작)에 부른다 —
        /// ui-dev의 화면 쓸기 전환과 「야생 ○○이(가) 나타났다!」가 그 위에 그려진다. 움직임 줄이기면 카메라는 구도에 고정이다.
        /// 다른 연출이 시작되면(스킬 등) 끝 상태(배틀 구도)로 접힌다.
        /// </summary>
        public void PlayBattleOpening()
        {
            if (!isActive || arenaRoot == null || !hasBaseCam) return;
            GameObject subject = bossModel != null ? bossModel : enemyModel;
            if (subject == null) return;
            Bounds focus = BattleFraming.ModelBounds(subject);
            if (bossModel != null)
            {
                if (teamModels != null)
                    foreach (GameObject m in teamModels)
                        if (m != null) focus.Encapsulate(BattleFraming.ModelBounds(m));
            }
            else if (playerModel != null) focus.Encapsulate(BattleFraming.ModelBounds(playerModel));
            Vector3 subjectCenter = BattleFraming.ModelBounds(subject).center;
            Vector3 focusCenter = focus.center;
            openingPlaying = true;
            openingElapsed = 0f;
            BeginStaging(OpeningCoroutine(subjectCenter, focusCenter), FinishOpening);
        }

        private IEnumerator OpeningCoroutine(Vector3 subject, Vector3 focus)
        {
            float t = 0f;
            while (isActive && arenaRoot != null && t < BattleFlourish.OpeningShotSeconds)
            {
                openingElapsed = t;
                ApplyStagingShot(BattleCameraDirector.EvaluateOpening(t, baseCamPos, baseCamRot, focus, subject));
                yield return null;
                // 실제 초 — 1대1 진입 구간(BattleReadPacing.EntryIntroSeconds)이 배속과 무관하게 실제 시간으로 흐른다.
                t += Time.deltaTime * BattlePresentation.TimeScale;
            }
            EndStaging();
        }

        private void FinishOpening()
        {
            openingPlaying = false;
            openingElapsed = 0f;
            ClearCameraShot();
        }

        /// <summary>아레나 정리 — 코루틴은 이미 멈췄다. 표지만 되돌린다.</summary>
        private void ClearFlourishState()
        {
            signatureActive = false;
            signatureElapsed = 0f;
            signatureSkillName = null;
            victoryPlaying = false;
            victoryDone = false;
            victoryElapsed = 0f;
            openingPlaying = false;
            openingElapsed = 0f;
        }
    }
}
