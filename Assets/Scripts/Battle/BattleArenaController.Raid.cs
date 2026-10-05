using System.Collections;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 레이드 3D 연출 — 합체공격, 팀원 한 마리의 공격, 보스 공격 예고와 보스 공격.
    ///
    /// 예전 합체공격은 5마리가 0.42초 만에 동시에 돌진했다 돌아오고, 그 위에 UI가 2D용 사각형 그림과
    /// 화면 전체 노란 섬광을 따로 그렸다. 폭발은 불투명한 노란 공이라 보스를 가렸다. 팀원 공격과 보스
    /// 공격은 흔들림 한 번이 전부라 누가 누구를 쳤는지가 몸으로 읽히지 않았다. 여기선 1v1과 같은 부품
    /// (<c>BattleArenaController.Impact.cs</c>의 히트스톱·넉백·섬광·외침)을 레이드 박자에 맞춰 쓴다.
    /// </summary>
    public partial class BattleArenaController
    {
        private Coroutine telegraphRoutine;
        private bool telegraphPoseSaved;
        private Pose3 bossTelegraphRest;
        private Vector3 telegraphShotPos;
        private readonly List<GameObject> telegraphFx = new List<GameObject>();

        /// <summary>팀원 모델의 화면 좌표(픽셀, y 위쪽) — UI가 슬롯별 피해 숫자를 3D 위치에 붙인다.</summary>
        public Vector3 GetTeamScreenPosition(int slot)
        {
            return ScreenCenterOf(GetTeamModel(slot));
        }

        /// <summary>
        /// 아레나에 서 있는 모델인가 — 쓰러짐 연출(<see cref="PlayFaintCoroutine"/>)은 끝에 모델을 끄고,
        /// 시작 때부터 기절해 있던 팀원(<see cref="HideMembersDownAtStart"/>)도 꺼 둔다. 꺼진 모델은 맞지도 돌진하지도 않는다.
        /// </summary>
        private static bool IsStanding(GameObject model)
        {
            return model != null && model.activeInHierarchy;
        }

        /// <summary>
        /// 레이드를 시작할 때 이미 기절해 있던 팀원을 세우지 않는다. 예전엔 HP 0인 곤충이 멀쩡히 서서 숨 쉬고 있었다
        /// (컨트롤러는 그 슬롯을 처음부터 "쓰러짐을 보여 줬다"로 쳐서 <c>PresentPendingFaints</c>가 눕히지도 않는다).
        /// 감추는 쪽을 골랐다 — 전투 도중 쓰러진 팀원도 쓰러짐 연출 끝에 꺼지므로(<see cref="PlayFaintCoroutine"/>) 두 경우가 같게 보인다.
        /// 카메라 구도는 다섯 자리를 다 세운 채 먼저 잡는다(<see cref="SetupBattleCamera"/> 뒤에 부른다) — 기절 수에 따라 구도가 흔들리지 않게.
        /// </summary>
        private void HideMembersDownAtStart(bool[] teamDown)
        {
            if (teamDown == null || teamModels == null) return;
            for (int i = 0; i < teamModels.Length && i < teamDown.Length; i++)
                if (teamDown[i] && teamModels[i] != null) teamModels[i].SetActive(false);
        }

        // ── 보스 변신(RaidBossForms) ──

        /// <summary>
        /// 레이드 보스가 모습을 바꿨다 — 보스 모델을 <paramref name="formInsect"/>의 모습으로 다시 세운다(기능만).
        /// 처음 보스를 세운 경로(<see cref="CreateBattleInsect"/> → 정면 회전 → 받침대 <c>CreateBossRise</c> → 오라)를 그대로 탄다 —
        /// 받침대는 새 모델의 높이에 맞춰 다시 만든다. 진행 중인 예고는 접는다. 위치·카메라 구도는 그대로다.
        /// <b>검은 연기·실루엣 색 같은 연출은 없다</b> — visual-dev가 붙인다(부르는 쪽: <c>RaidBattleUI</c>의 변신 단계 시작).
        /// </summary>
        public void RebuildRaidBoss(InsectData formInsect, int level, bool shiny)
        {
            if (!isActive || arenaRoot == null || formInsect == null || bossModel == null) return;

            StopTelegraph(true);
            ForgetModel(bossModel);
            Destroy(bossModel);
            Transform oldRise = arenaRoot.transform.Find("BossRise");
            if (oldRise != null) Destroy(oldRise.gameObject);

            bossModel = CreateBattleInsect(formInsect, level, shiny, bossBattlePos, BossInsectScale);
            bossModel.name = "RaidInsect_Boss";
            bossModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            CreateBossRise();
            CreateBossAura(bossModel.transform);
        }

        // ── 합체공격 ──

        public void PlayUniteAttackAnimation(System.Action onComplete)
        {
            CompleteStagingNow();   // 변신이 덜 끝났으면 새 모습으로 — 바뀌기 전 모습을 치지 않게
            StartCoroutine(UniteAttackCoroutine(onComplete));
        }

        /// <summary>
        /// 기 모으기(전원 들썩임) → 차례 돌진(0.16초 간격, 각자 한 방) → 보스 앞에 떠서 대기 → 합동 일격
        /// (큰 히트스톱·슬로모션·섬광·충격파·보스 비명) → 복귀. 시각은 <see cref="RaidUniteTimeline"/>이 단일 출처다.
        /// </summary>
        private IEnumerator UniteAttackCoroutine(System.Action onComplete)
        {
            if (teamModels == null || bossModel == null) { onComplete?.Invoke(); yield break; }

            var members = new List<int>();
            for (int i = 0; i < teamModels.Length; i++)
                if (teamModels[i] != null && teamModels[i].activeInHierarchy) members.Add(i);
            int n = members.Count;
            if (n == 0) { onComplete?.Invoke(); yield break; }

            Bounds bossBounds = BattleFraming.ModelBounds(bossModel);
            Vector3 bossCenter = bossBounds.center;
            var starts = new Vector3[n];
            var strikes = new Vector3[n];
            var hovers = new Vector3[n];
            var gathered = new Vector3[n];
            var colors = new Color[n];
            var hit = new bool[n];
            Vector3 teamCenter = Vector3.zero;
            for (int k = 0; k < n; k++)
            {
                starts[k] = teamModels[members[k]].transform.position;
                teamCenter += starts[k];
            }
            teamCenter /= n;
            Vector3 toTeam = teamCenter - bossCenter;
            toTeam.y = 0f;
            toTeam = toTeam.sqrMagnitude > 0.0001f ? toTeam.normalized : Vector3.back;
            Vector3 side = Vector3.Cross(Vector3.up, toTeam);
            for (int k = 0; k < n; k++)
            {
                float spread = (k - (n - 1) * 0.5f) * 0.45f;
                strikes[k] = bossCenter + toTeam * (bossBounds.extents.z * 0.6f + 0.25f) + side * spread;
                hovers[k] = Vector3.Lerp(strikes[k], starts[k], 0.35f) + Vector3.up * 0.6f;
                gathered[k] = Vector3.Lerp(bossCenter, strikes[k], 0.35f);
                InsectEntity entity = teamModels[members[k]].GetComponent<InsectEntity>();
                colors[k] = GetElementColor3D(entity != null && entity.Data != null ? entity.Data.primaryType : InsectElement.Bug);
            }

            playingSkill = true;
            if (AudioManager.Instance != null)
                for (int k = 0; k < n && k < 2; k++)
                    AudioManager.Instance.PlayCry(CryKey(CryOf(teamModels[members[k]], false), false));

            float gatherAt = RaidUniteTimeline.FinalStrike - 0.15f;
            float t = 0f;
            bool finalDone = false;
            try
            {
                while (t < RaidUniteTimeline.Total)
                {
                    if (!isActive || bossModel == null) yield break;
                    t += PresentationDelta;
                    ApplyUniteCameraShot(t, teamCenter, bossCenter);

                    for (int k = 0; k < n; k++)
                    {
                        GameObject m = teamModels[members[k]];
                        if (m == null) continue;
                        float depart = RaidUniteTimeline.MemberStart + k * RaidUniteTimeline.MemberStagger;
                        float hitAt = RaidUniteTimeline.MemberHit(k);
                        if (!hit[k] && t >= hitAt)
                        {
                            hit[k] = true;
                            UniteMemberHit(strikes[k], toTeam, colors[k]);
                        }

                        Vector3 pos;
                        if (t < depart)
                        {
                            // 기 모으기 — 제각각 들썩인다(같은 위상이면 기계처럼 보인다).
                            pos = starts[k] + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 14f + k * 1.7f)) * 0.14f);
                        }
                        else if (t < hitAt)
                        {
                            float p = Mathf.Clamp01((t - depart) / RaidUniteTimeline.MemberTravel);
                            float eased = p * p * (3f - 2f * p);
                            pos = Vector3.Lerp(starts[k], strikes[k], eased) + Vector3.up * (Mathf.Sin(eased * Mathf.PI) * 0.9f);
                            CreateTrailParticle(pos, colors[k], 0.09f);
                        }
                        else if (t < gatherAt)
                        {
                            float r = Mathf.Clamp01((t - hitAt) / 0.22f);
                            pos = Vector3.Lerp(strikes[k], hovers[k], 1f - (1f - r) * (1f - r))
                                + Vector3.up * (Mathf.Sin(t * 9f + k) * 0.05f);
                        }
                        else if (t < RaidUniteTimeline.FinalStrike)
                        {
                            float r = Mathf.Clamp01((t - gatherAt) / 0.15f);
                            pos = Vector3.Lerp(hovers[k], gathered[k], r * r);
                            CreateTrailParticle(pos, colors[k], 0.1f);
                        }
                        else
                        {
                            float r = Mathf.Clamp01((t - RaidUniteTimeline.FinalStrike - 0.2f) / 0.5f);
                            pos = Vector3.Lerp(gathered[k], starts[k], Mathf.SmoothStep(0f, 1f, r))
                                + Vector3.up * (Mathf.Sin(r * Mathf.PI) * 0.6f);
                        }
                        m.transform.position = pos;
                    }

                    if (!finalDone && t >= RaidUniteTimeline.FinalStrike)
                    {
                        finalDone = true;
                        UniteFinalStrike(bossCenter, toTeam);
                    }
                    yield return null;
                }
            }
            finally
            {
                for (int k = 0; k < n; k++)
                {
                    GameObject m = teamModels != null && members[k] < teamModels.Length ? teamModels[members[k]] : null;
                    if (m != null) m.transform.position = starts[k];
                }
                ClearCameraShot();
                playingSkill = false;
            }
            onComplete?.Invoke();
        }

        private void UniteMemberHit(Vector3 point, Vector3 toTeam, Color color)
        {
            BattlePresentation.HitStop(0.035f);
            if (!BattlePresentation.ReducedFlashes) StartCoroutine(ImpactBurstCoroutine(point, color, 0.35f));
            if (bossModel != null) StartReact(bossModel, HitReactCoroutine(bossModel, -toTeam, 0.3f, true));
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.Hit);
        }

        /// <summary>
        /// 합동 일격의 멈춤 — 팀원 한 마리의 치명타(<see cref="HitCue.HitStopSeconds"/> 0.16~0.18초)보다 길게 둔다.
        /// 다섯이 함께 친 정점이 혼자 친 치명타보다 짧게 멈추면 순서가 뒤집혀 보인다. 합체공격은 치명타를 굴리지 않는다.
        /// </summary>
        public const float UniteFinalHitStop = 0.2f;

        private void UniteFinalStrike(Vector3 bossCenter, Vector3 toTeam)
        {
            BattlePresentation.HitStop(UniteFinalHitStop);
            BattlePresentation.SlowMotion(0.35f, 0.5f);
            Color gold = new Color(1f, 0.85f, 0.35f);
            if (!BattlePresentation.ReducedFlashes)
            {
                StartCoroutine(ImpactBurstCoroutine(bossCenter, gold, 1f));
                StartCoroutine(DefaultImpact3D(bossCenter, gold));
            }
            if (bossModel != null) StartReact(bossModel, HitReactCoroutine(bossModel, -toTeam, 1f, true));
            PlayShout(BattleShout.Kind.Sound, bossCenter + toTeam * 0.9f, "콰과과광!!", gold, 1.1f, 0f, Random.Range(-8f, 8f));
            if (bossModel != null)
                PlayShout(BattleShout.Kind.Hurt, HeadPoint(bossModel) + Vector3.up * 0.2f,
                    BattleShout.Hurt(BattleShout.Cry.Boss, 1f, Random.Range(0, 100)), Color.white, 1.2f, 0.15f);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SfxType.CriticalHit);
                StartCoroutine(PlayCryDelayed(CryKey(BattleShout.Cry.Boss, true), 0.12f));
            }
            CameraFollower follower = ResolveFollower();
            if (follower != null) follower.Shake(0.45f, 0.5f);
        }

        private void ApplyUniteCameraShot(float seconds, Vector3 teamCenter, Vector3 bossCenter)
        {
            if (!hasBaseCam || BattlePresentation.ReducedMotion) return;
            CameraFollower follower = ResolveFollower();
            if (follower == null) return;
            BattleCameraDirector.Shot shot = BattleCameraDirector.EvaluateUnite(seconds, baseCamPos, baseCamRot, teamCenter, bossCenter);
            follower.SetBattleShot(shot.Position, shot.Rotation, shot.Weight);
        }

        // ── 팀원 한 마리의 공격 ──

        /// <summary>
        /// 레이드 팀 행동을 같은 타임라인에 겹쳐 재생한다. 슬롯과 속성 배열은 같은 길이여야 하며,
        /// 사망/누락 모델은 자동으로 건너뛴다.
        /// </summary>
        public void PlayRaidVolley(int[] slots, InsectElement[] elements, System.Action onComplete)
        {
            PlayRaidVolley(slots, elements, onComplete, HitCue.None);
        }

        /// <param name="cue">한 마리 공격일 때의 외칠 기술명·타격 세기. 여러 마리면 외침은 생략한다.</param>
        public void PlayRaidVolley(int[] slots, InsectElement[] elements, System.Action onComplete, HitCue cue)
        {
            CompleteStagingNow();
            StartCoroutine(RaidVolleyCoroutine(slots, elements, onComplete, cue));
        }

        private IEnumerator RaidVolleyCoroutine(int[] slots, InsectElement[] elements, System.Action onComplete, HitCue cue)
        {
            int count = slots != null ? slots.Length : 0;
            if (!isActive || bossModel == null || teamModels == null || count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            GameObject[] attackers = new GameObject[count];
            Vector3[] starts = new Vector3[count];
            Quaternion[] startRots = new Quaternion[count];
            Vector3[] contacts = new Vector3[count];
            GameObject[] projectiles = new GameObject[count];
            bool[] melee = new bool[count];
            bool[] valid = new bool[count];
            bool[] impacted = new bool[count];
            bool[] preStruck = new bool[count];
            BattleMotion.Kind[] kinds = new BattleMotion.Kind[count];
            BattleMotion.Family[] families = new BattleMotion.Family[count];
            Vector3 bossPos = bossModel.transform.position;
            Vector3 bossCenter = BattleFraming.ModelBounds(bossModel).center;
            int validCount = 0;

            for (int i = 0; i < count; i++)
            {
                int slot = slots[i];
                if (slot < 0 || slot >= teamModels.Length || teamModels[slot] == null)
                    continue;

                attackers[i] = teamModels[slot];
                starts[i] = attackers[i].transform.position;
                startRots[i] = attackers[i].transform.rotation;
                InsectElement element = elements != null && i < elements.Length ? elements[i] : InsectElement.Bug;
                melee[i] = IsMeleeElement(element);
                // 1대1과 같은 계열 몸짓 — 볼리 길이(0.46초, 끝에서 타격)는 그대로 두고 곡선만 그 안에 눌러 담는다.
                // RaidBattleUI의 피해 숫자는 그 0.46초를 가정하고 뜬다.
                InsectEntity entity = attackers[i].GetComponent<InsectEntity>();
                string species = entity != null && entity.Data != null ? entity.Data.insectId : null;
                families[i] = BattleMotion.FamilyOf(species);
                kinds[i] = BattleMotion.Resolve(species, melee[i], false);
                // 1대1 공식과 같은 접점 — 보스 쪽으로 78%.
                int spreadSlot = slots[i];
                float spreadX = (spreadSlot - (teamModels.Length - 1) * 0.5f) * 0.16f;
                contacts[i] = Vector3.Lerp(starts[i], bossPos + Vector3.right * spreadX + Vector3.up * 0.15f, 0.78f);
                valid[i] = true;
                validCount++;

                if (!melee[i])
                {
                    projectiles[i] = CreateElementProjectile(element, GetElementColor3D(element));
                    projectiles[i].transform.position = starts[i] + Vector3.up * 0.5f;
                }
            }

            if (validCount == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            playingSkill = true;
            bool single = validCount == 1;
            // 0.035초의 짧은 스태거만 두어 충돌음은 연속으로 들리되, 한 마리가 복귀한 뒤
            // 다음 멤버가 출발하는 직렬 연출은 만들지 않는다.
            const float stagger = 0.035f;
            const float actionDuration = RaidVolleyImpactSeconds;
            // 전용기 — 레이드는 볼리 길이가 정해져 있어(숫자 시각) 앞에 컷인 시간을 끼우지 않는다. 다가가는 동안 기를 모으고
            // 타격에 큰 이펙트, 카메라 반동은 최대 세기다. 컷인 글자 시각은 IsSignaturePlaying·SignatureCutInEndSeconds를 읽는다.
            bool signature = single && cue.Signature;
            if (single)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!valid[i]) continue;
                    InsectElement element = elements != null && i < elements.Length ? elements[i] : InsectElement.Bug;
                    BeginSkillPresentation(attackers[i], element, cue);
                    if (signature)
                        BeginSignature(attackers[i], element, cue, true, actionDuration,
                            BattleFlourish.SignatureCutInEndFor(actionDuration));
                }
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SfxType.SkillUse);
            }

            float totalDuration = actionDuration + Mathf.Max(0, count - 1) * stagger;
            float timer = 0f;
            float impactAt = -1f;
            Vector3 cameraFrom = Vector3.zero;
            float motionScale = BattlePresentation.ReducedMotion ? 0.18f : 1f;
            try
            {
                while (timer < totalDuration)
                {
                    timer += PresentationDelta;
                    if (signature) signatureElapsed = timer;
                    for (int i = 0; i < count; i++)
                    {
                        if (!valid[i] || attackers[i] == null) continue;
                        float local = Mathf.Clamp01((timer - i * stagger) / actionDuration);
                        if (local <= 0f) continue;

                        int slot = slots[i];
                        float spread = (slot - (teamModels.Length - 1) * 0.5f) * 0.16f;
                        Vector3 target = bossPos + Vector3.right * spread + Vector3.up * 0.15f;
                        float eased = local * local * (3f - 2f * local);
                        InsectElement element = elements != null && i < elements.Length ? elements[i] : InsectElement.Bug;
                        Color elementColor = GetElementColor3D(element);

                        // 몸짓 — 볼리 0..1이 몸짓의 0..타격으로 들어간다(마지막 프레임이 정확히 타격 자세).
                        float motionProgress = local * BattleMotion.ImpactOf(kinds[i]);
                        BattleMotion.Pose pose = BattleMotion.Evaluate(kinds[i], families[i], motionProgress).Scaled(motionScale);
                        Vector3 bodyPos = Vector3.LerpUnclamped(starts[i], contacts[i], pose.Travel) + Vector3.up * pose.Lift;
                        attackers[i].transform.SetPositionAndRotation(bodyPos,
                            startRots[i] * Quaternion.Euler(pose.Pitch, pose.Yaw, pose.Roll));
                        if (!preStruck[i] && BattleMotion.HasPreStrike(kinds[i]) && motionProgress >= BattleMotion.SlashFirstSwing)
                        {
                            preStruck[i] = true;
                            PlaySlashArc(attackers[i], bossModel, elementColor, true);
                        }

                        if (melee[i])
                        {
                            CreateTrailParticle(bodyPos, elementColor, 0.08f);
                        }
                        else if (projectiles[i] != null)
                        {
                            Vector3 start = starts[i] + Vector3.up * 0.5f;
                            Vector3 pos = Vector3.Lerp(start, target, eased);
                            pos += GetElementTrajectoryOffset(element, local);
                            projectiles[i].transform.position = pos;
                            projectiles[i].transform.Rotate(Vector3.up, 720f * PresentationDelta);
                            CreateTrailParticle(pos, elementColor, 0.07f);
                        }

                        if (!impacted[i] && local >= 0.98f)
                        {
                            impacted[i] = true;
                            if (projectiles[i] != null)
                            {
                                Destroy(projectiles[i]);
                                projectiles[i] = null;
                            }
                            if (kinds[i] == BattleMotion.Kind.Slash) PlaySlashArc(attackers[i], bossModel, elementColor, false);
                            if (!cue.Missed) CreateElementImpact3D(target, element, elementColor);
                            if (single)
                            {
                                impactAt = timer;
                                cameraFrom = attackers[i].transform.position;
                                // 피해 없는 행동(강화·회복·기절)은 보스를 치지 않는다 — 속성 임팩트만.
                                bool struck = cue.Missed || cue.Weight > 0f || cue.Finisher;
                                if (signature && struck && !cue.Missed)
                                    PlaySignatureBurst(bossCenter, element, SignatureBurstSize(bossModel));
                                if (struck)
                                    PlayImpactFeel(attackers[i], bossModel, bossCenter, element, elementColor, cue);
                                // 속성 스킬음 — 1v1(SkillAttackCoroutine)과 같은 자리·같은 한 번. 예전엔 보스 공격만 냈다.
                                // 한 마리 공격에만 붙인다: 팀원 차례는 한 마리씩 0.85초 간격이라 겹치지 않고,
                                // 합체공격은 이 경로를 타지 않는다(UniteAttackCoroutine). 여러 마리 볼리는 출발 때 SkillUse 한 번이다.
                                if (AudioManager.Instance != null) AudioManager.Instance.PlaySkillSFX(element);
                            }
                            // 회복한 팀원의 반짝임은 이 행동이 닿는 순간에(Life partial — HP가 오른 걸 미리 적어 두었다).
                            FlushPendingHeals();
                        }
                    }
                    // 전용기는 반동을 최대 세기로 — 보통 타격보다 확실히 크게.
                    if (impactAt >= 0f) ApplyPunchShot(timer - impactAt, cameraFrom, bossCenter, signature ? 1f : cue.Weight);
                    yield return null;
                }

                if (!single)
                {
                    StartCoroutine(ShakeModel(bossModel, 0.42f, 0.42f));
                    CameraFollower follower = ResolveFollower();
                    if (follower != null) follower.Shake(0.38f, 0.42f);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.Hit);
                }

                // 복귀 — 몸짓의 타격 뒤 곡선(튕겨 물러나기·내려앉기)을 0.26초에 눌러 담는다. 원거리 시전 자세도 함께 풀린다.
                const float returnDuration = 0.26f;
                timer = 0f;
                while (timer < returnDuration)
                {
                    timer += PresentationDelta;
                    float progress = Mathf.Clamp01(timer / returnDuration);
                    if (signature)
                    {
                        signatureElapsed = totalDuration + timer;
                        if (signatureElapsed >= actionDuration + BattleFlourish.SignatureTailSeconds) EndSignature();
                    }
                    for (int i = 0; i < count; i++)
                    {
                        if (!valid[i] || attackers[i] == null) continue;
                        float impactP = BattleMotion.ImpactOf(kinds[i]);
                        BattleMotion.Pose pose = BattleMotion.Evaluate(kinds[i], families[i],
                            Mathf.Lerp(impactP, BattleMotion.RestProgress, progress)).Scaled(motionScale);
                        attackers[i].transform.SetPositionAndRotation(
                            Vector3.LerpUnclamped(starts[i], contacts[i], pose.Travel) + Vector3.up * pose.Lift,
                            startRots[i] * Quaternion.Euler(pose.Pitch, pose.Yaw, pose.Roll));
                    }
                    if (impactAt >= 0f) ApplyPunchShot(totalDuration - impactAt + timer, cameraFrom, bossCenter, signature ? 1f : cue.Weight);
                    yield return null;
                }
            }
            finally
            {
                for (int i = 0; i < count; i++)
                {
                    if (projectiles[i] != null) Destroy(projectiles[i]);
                    if (valid[i] && attackers[i] != null)
                        attackers[i].transform.SetPositionAndRotation(starts[i], startRots[i]);
                }
                if (signature) EndSignature();
                ClearCameraShot();
                playingSkill = false;
            }
            onComplete?.Invoke();
        }

        /// <summary>
        /// 레이드 팀원 볼리의 타격 시각(연출 시계 초) — 볼리는 이 길이 끝에서 보스를 친다. <c>RaidBattleUI</c>가 3D 피해 숫자를 이 시각에 맞춰 띄운다
        /// (그쪽의 0.45초가 이 값의 사본이다). 계열 몸짓은 이 길이 안에 눌러 담으므로 계열이 달라도 이 시각은 그대로다.
        /// </summary>
        public const float RaidVolleyImpactSeconds = 0.46f;

        /// <summary>
        /// 짧은 타격용 샷 — 레이드 팀원 공격은 0.7초에 한 번씩 몰아치므로 시전자까지 따라가지 않고
        /// 타격 순간에만 보스 쪽으로 튄다(<see cref="BattleCameraDirector.Style.Punch"/>).
        /// </summary>
        private void ApplyPunchShot(float secondsSinceImpact, Vector3 attacker, Vector3 target, float weight)
        {
            if (!hasBaseCam || BattlePresentation.ReducedMotion) return;
            CameraFollower follower = ResolveFollower();
            if (follower == null) return;
            // Evaluate의 타격 후 경과는 (진행도 - ImpactProgress) × 길이 — 길이 1초로 두면 진행도가 곧 초다.
            BattleCameraDirector.Shot shot = BattleCameraDirector.Evaluate(BattleCameraDirector.Style.Punch,
                BattleMotion.ImpactProgress + secondsSinceImpact, 1f, baseCamPos, baseCamRot, attacker, target, weight, false);
            follower.SetBattleShot(shot.Position, shot.Rotation, shot.Weight);
        }

        // ── 보스 공격 예고 ──

        /// <summary>
        /// 보스 공격 예고(UI의 BossTelegraph 페이즈와 같은 길이). 보스가 몸을 젖히며 기를 모으고, 노리는
        /// 팀원 발밑에 붉은 경고 고리가 뛴다. 포효하고, 이름 있는 기술이면 외친다. 예고 자세는 공격이
        /// 이어받아 그 자리에서 달려든다 — 공격이 안 오면(보스 기절) 잠시 뒤 스스로 원래 자세로 돌아온다.
        /// </summary>
        public void PlayRaidBossTelegraph(int targetSlot, bool isAoe, InsectElement element, string skillName, float duration)
        {
            if (!isActive || bossModel == null) return;
            CompleteStagingNow();   // 예고 자세의 원위치가 변신·등장 도중의 자세로 잡히지 않게
            StopTelegraph(true);
            telegraphRoutine = StartCoroutine(BossTelegraphCoroutine(targetSlot, isAoe, element, skillName, Mathf.Max(0.2f, duration)));
        }

        private IEnumerator BossTelegraphCoroutine(int targetSlot, bool isAoe, InsectElement element, string skillName, float duration)
        {
            bossTelegraphRest = new Pose3
            {
                Position = bossModel.transform.position,
                Rotation = bossModel.transform.rotation,
                Scale = bossModel.transform.localScale
            };
            telegraphPoseSaved = true;

            Bounds bounds = BattleFraming.ModelBounds(bossModel);
            Vector3 bossCenter = bounds.center;
            Vector3 toTeam = playerBattlePos - bossCenter;
            toTeam.y = 0f;
            toTeam = toTeam.sqrMagnitude > 0.0001f ? toTeam.normalized : Vector3.back;
            Vector3 rearAxis = Vector3.Cross(Vector3.up, toTeam);

            if (!string.IsNullOrEmpty(skillName))
                PlayShout(BattleShout.Kind.Callout, HeadPoint(bossModel), BattleShout.Callout(skillName), GetUIElementColor(element), 1.1f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayCry(CryKey(BattleShout.Cry.Boss, false));

            Color charge = Color.Lerp(GetElementColor3D(element), new Color(1f, 0.25f, 0.2f), 0.4f);
            Material glowMat = CreateFxMaterial(charge, true);
            GameObject glow = BattlePresentation.ReducedFlashes ? null
                : FxPrimitive(PrimitiveType.Sphere, "BossCharge", bossCenter + toTeam * (bounds.extents.z * 0.5f), glowMat);
            if (glow != null) telegraphFx.Add(glow);

            var rings = new List<LineRenderer>();
            Material ringMat = CreateFxMaterial(new Color(1f, 0.2f, 0.15f, 0.9f), true);
            for (int i = 0; teamModels != null && i < teamModels.Length; i++)
            {
                if (teamModels[i] == null || (!isAoe && i != targetSlot)) continue;
                Vector3 p = teamModels[i].transform.position;
                LineRenderer ring = CreateFloorRing("BossWarning", new Vector3(p.x, arenaCenter.y + 0.07f, p.z), ringMat);
                telegraphFx.Add(ring.gameObject);
                rings.Add(ring);
            }

            float t = 0f;
            bool motion = !BattlePresentation.ReducedMotion;
            while (t < duration)
            {
                if (bossModel == null) yield break;
                t += PresentationDelta;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                if (motion)
                {
                    // 몸을 뒤로 젖히며 솟는다 — 달려들기 전의 예비 동작.
                    bossModel.transform.position = bossTelegraphRest.Position + Vector3.up * (0.28f * k)
                        + Vector3.right * (Mathf.Sin(t * 42f) * 0.02f * k);
                    bossModel.transform.rotation = Quaternion.AngleAxis(-12f * k, rearAxis) * bossTelegraphRest.Rotation;
                }
                if (glow != null)
                {
                    glow.transform.localScale = Vector3.one * (Mathf.Lerp(0.2f, 1.3f, k) * (1f + 0.08f * Mathf.Sin(t * 30f)));
                    glowMat.color = new Color(charge.r, charge.g, charge.b, 0.3f + 0.45f * k);
                }
                // 굵게 — 0.07m 선은 초록 바닥 위에서 거의 안 보였다(QA 캡처).
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 18f);
                ringMat.color = new Color(1f, 0.18f, 0.12f, 0.6f + 0.4f * pulse);
                foreach (LineRenderer ring in rings) SetRingRadius(ring, 0.85f + 0.12f * pulse, 0.14f);

                if (hasBaseCam && motion)
                {
                    telegraphShotPos = Vector3.Lerp(baseCamPos, bossCenter, 0.12f);
                    CameraFollower follower = ResolveFollower();
                    if (follower != null)
                        follower.SetBattleShot(telegraphShotPos, Quaternion.Slerp(baseCamRot,
                            Quaternion.LookRotation(bossCenter - telegraphShotPos), 0.5f), k);
                }
                yield return null;
            }

            // 공격이 이어받지 않으면(보스 기절로 공격 생략) 잠시 버티다 스스로 푼다.
            float hold = 0f;
            while (hold < 1.0f)
            {
                hold += PresentationDelta;
                yield return null;
            }
            telegraphRoutine = null;
            StopTelegraph(true);
            ClearCameraShot();
        }

        /// <summary>예고를 멈춘다. <paramref name="restorePose"/>가 false면 젖힌 자세를 그대로 두고 공격이 이어받는다.</summary>
        private void StopTelegraph(bool restorePose)
        {
            if (telegraphRoutine != null)
            {
                StopCoroutine(telegraphRoutine);
                telegraphRoutine = null;
            }
            for (int i = 0; i < telegraphFx.Count; i++)
                if (telegraphFx[i] != null) Destroy(telegraphFx[i]);
            telegraphFx.Clear();
            if (restorePose && telegraphPoseSaved && bossModel != null)
            {
                bossModel.transform.SetPositionAndRotation(bossTelegraphRest.Position, bossTelegraphRest.Rotation);
                bossModel.transform.localScale = bossTelegraphRest.Scale;
            }
            if (restorePose) telegraphPoseSaved = false;
        }

        private LineRenderer CreateFloorRing(string name, Vector3 ground, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(arenaRoot.transform, false);
            go.transform.position = ground;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 선 면을 바닥에 눕힌다
            LineRenderer ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 40;
            ring.alignment = LineAlignment.TransformZ;
            ring.sharedMaterial = material;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            return ring;
        }

        private static void SetRingRadius(LineRenderer ring, float radius, float width)
        {
            if (ring == null) return;
            ring.widthMultiplier = width;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        // ── 보스 공격 ──

        /// <summary>
        /// 레이드 보스의 예고된 단일/전체 공격을 실제 대상 모델에 동시 재생한다.
        /// </summary>
        public void PlayRaidBossAttack(InsectElement element, bool isAoe, int targetSlot, System.Action onComplete)
        {
            PlayRaidBossAttack(element, isAoe, targetSlot, onComplete, HitCue.None);
        }

        /// <param name="cue">맞는 쪽 세기(피해 ÷ 팀원 최대 HP)와 쓰러짐 여부 — 넉백·히트스톱이 따른다.</param>
        public void PlayRaidBossAttack(InsectElement element, bool isAoe, int targetSlot, System.Action onComplete, HitCue cue)
        {
            CompleteStagingNow();
            StartCoroutine(RaidBossAttackCoroutine(element, isAoe, targetSlot, onComplete, cue));
        }

        private IEnumerator RaidBossAttackCoroutine(InsectElement element, bool isAoe, int targetSlot,
            System.Action onComplete, HitCue cue)
        {
            if (!isActive || bossModel == null || teamModels == null)
            {
                StopTelegraph(true);
                onComplete?.Invoke();
                yield break;
            }

            // 이미 쓰러져 사라진 팀원(쓰러짐 연출 끝에 SetActive(false), 시작 때부터 기절)은 맞지 않는다 —
            // 예전엔 전체 공격이 빈 자리에 속성 임팩트·비명을 띄웠다.
            List<GameObject> targets = new List<GameObject>();
            if (isAoe)
            {
                for (int i = 0; i < teamModels.Length; i++)
                    if (IsStanding(teamModels[i]))
                        targets.Add(teamModels[i]);
            }
            else if (targetSlot >= 0 && targetSlot < teamModels.Length && IsStanding(teamModels[targetSlot]))
            {
                targets.Add(teamModels[targetSlot]);
            }
            else
            {
                GameObject fallback = ResolveBossTarget();
                if (fallback != null) targets.Add(fallback);
            }

            if (targets.Count == 0)
            {
                StopTelegraph(true);
                onComplete?.Invoke();
                yield break;
            }

            // 예고 자세(젖혀 솟은 상태)에서 그대로 달려든다. 돌아올 곳은 예고 전의 원래 자리다.
            bool fromTelegraph = telegraphPoseSaved;
            Vector3 restPos = fromTelegraph ? bossTelegraphRest.Position : bossModel.transform.position;
            Quaternion restRot = fromTelegraph ? bossTelegraphRest.Rotation : bossModel.transform.rotation;
            Vector3 shotFrom = fromTelegraph ? telegraphShotPos : baseCamPos;
            StopTelegraph(false);
            telegraphPoseSaved = false;

            playingSkill = true;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySkillSFX(element);
            // 보스의 전용기 — 예고(외침·젖히기)가 이미 컷인 몫을 했다. 달려드는 동안 기를 모으고 타격에 큰 이펙트를 얹는다.
            const float BossCastSeconds = 0.42f;
            bool signature = cue.Signature;
            if (signature)
                BeginSignature(bossModel, element, cue, false, BossCastSeconds, BattleFlourish.SignatureCutInEndFor(BossCastSeconds));

            Vector3 bossStart = bossModel.transform.position;
            Quaternion bossStartRot = bossModel.transform.rotation;
            Vector3 targetCenter = Vector3.zero;
            for (int i = 0; i < targets.Count; i++)
                targetCenter += BattleFraming.ModelBounds(targets[i]).center;
            targetCenter /= targets.Count;

            bool meleeAttack = IsMeleeElement(element);
            GameObject[] projectiles = meleeAttack ? null : new GameObject[targets.Count];
            if (!meleeAttack)
            {
                Color color = GetElementColor3D(element);
                for (int i = 0; i < targets.Count; i++)
                {
                    projectiles[i] = CreateElementProjectile(element, color);
                    projectiles[i].transform.position = bossStart + Vector3.up * 0.6f;
                }
            }

            bool motion = !BattlePresentation.ReducedMotion;
            CameraFollower follower = ResolveFollower();
            Vector3 shotTarget = Vector3.Lerp(baseCamPos, targetCenter, 0.15f);
            // 1대1·팀원과 같은 계열 몸짓 — 보스는 크고 받침대 위라 달려드는 길(기존 돌진)은 그대로 두고 몸의 기울기·휘두름만 얹는다(0.6배).
            InsectEntity bossEntity = bossModel.GetComponent<InsectEntity>();
            string bossSpecies = bossEntity != null && bossEntity.Data != null ? bossEntity.Data.insectId : null;
            BattleMotion.Family bossFamily = BattleMotion.FamilyOf(bossSpecies);
            BattleMotion.Kind bossKind = BattleMotion.Resolve(bossSpecies, meleeAttack, false);
            float bossImpactP = BattleMotion.ImpactOf(bossKind);
            float bossPoseScale = motion ? 0.6f : 0.12f;
            bool bossPreStruck = false;
            try
            {
                const float castDuration = BossCastSeconds;
                float timer = 0f;
                while (timer < castDuration)
                {
                    if (!isActive || bossModel == null) yield break;
                    timer += PresentationDelta;
                    if (signature) signatureElapsed = timer;
                    float progress = Mathf.Clamp01(timer / castDuration);
                    float eased = progress * progress * (3f - 2f * progress);
                    BattleMotion.Pose bossPose = BattleMotion.Evaluate(bossKind, bossFamily, progress * bossImpactP).Scaled(bossPoseScale);
                    if (!bossPreStruck && BattleMotion.HasPreStrike(bossKind) && progress * bossImpactP >= BattleMotion.SlashFirstSwing)
                    {
                        bossPreStruck = true;
                        PlaySlashArc(bossModel, targets[0], GetElementColor3D(element), true);
                    }
                    if (meleeAttack)
                    {
                        Vector3 lungeTarget = Vector3.Lerp(bossStart, targetCenter, isAoe ? 0.22f : 0.52f);
                        Vector3 pos = Vector3.Lerp(bossStart, lungeTarget, eased);
                        pos.y += Mathf.Max(Mathf.Sin(progress * Mathf.PI) * (isAoe ? 1.4f : 0.8f), bossPose.Lift);
                        bossModel.transform.position = pos;
                    }
                    else
                    {
                        for (int i = 0; i < targets.Count; i++)
                        {
                            if (projectiles[i] == null || targets[i] == null) continue;
                            Vector3 end = targets[i].transform.position + Vector3.up * 0.4f;
                            Vector3 pos = Vector3.Lerp(bossStart + Vector3.up * 0.6f, end, eased);
                            pos += GetElementTrajectoryOffset(element, progress);
                            projectiles[i].transform.position = pos;
                            CreateTrailParticle(pos, GetElementColor3D(element), 0.09f);
                        }
                    }
                    // 젖힌 몸을 펴며 달려든다 — 그 위에 계열 몸짓(사마귀 베기·딱정벌레 머리 숙이기·나는 종 내리꽂기…).
                    bossModel.transform.rotation = Quaternion.Slerp(bossStartRot, restRot, eased)
                        * Quaternion.Euler(bossPose.Pitch, bossPose.Yaw, bossPose.Roll);
                    if (hasBaseCam && motion && follower != null)
                    {
                        Vector3 camPos = Vector3.Lerp(shotFrom, shotTarget, eased);
                        follower.SetBattleShot(camPos, Quaternion.Slerp(baseCamRot,
                            Quaternion.LookRotation(Vector3.Lerp(targetCenter, bossStart, 0.3f) - camPos), 0.5f),
                            fromTelegraph ? 1f : eased);
                    }
                    yield return null;
                }

                // 1v1·팀원 공격(PlayImpactFeel)과 같은 규칙 — 센 비명·센 의성어·CriticalHit 소리는 치명타·마무리만(cue.Heavy).
                // 전체 공격은 큰 폭발과 조금 더 큰 흔들림(HitCue.AreaShakeBonus)으로 "큰 기술"을 보이되, 치명타 한 방보다는
                // 작게 둔다. 예전엔 전체 공격이면 늘 CriticalHit·"콰광!!"·흔들림 0.55라 치명타 전체 공격과 구별이 없었다.
                Color impactColor = GetElementColor3D(element);
                if (bossKind == BattleMotion.Kind.Slash && targets[0] != null) PlaySlashArc(bossModel, targets[0], impactColor, false);
                float w = cue.Weight;
                float shoutWeight = cue.Heavy ? 1f : w;
                Color accent = CriticalAccent(false);   // 보스가 친 치명타 — 붉은빛
                Color burstColor = cue.Critical ? Color.Lerp(impactColor, accent, 0.65f) : impactColor;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (projectiles != null && projectiles[i] != null)
                        Destroy(projectiles[i]);
                    if (targets[i] == null) continue;
                    Vector3 center = BattleFraming.ModelBounds(targets[i]).center;
                    CreateElementImpact3D(targets[i].transform.position, element, impactColor);
                    Vector3 dir = targets[i].transform.position - restPos;
                    dir.y = 0f;
                    dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.back;
                    StartReact(targets[i], HitReactCoroutine(targets[i], dir, w, false, cue.Critical, accent));
                    if (!BattlePresentation.ReducedFlashes)
                        StartCoroutine(ImpactBurstCoroutine(center, burstColor, isAoe ? 0.5f : w));
                    PlayShout(BattleShout.Kind.Hurt, HeadPoint(targets[i]) + dir * 0.4f,
                        BattleShout.Hurt(CryOf(targets[i], false), shoutWeight, Random.Range(0, 100)),
                        Color.white, 0.95f, 0.1f + i * 0.04f);
                }
                // 치명타 빛살은 한 번만 — 전체 공격이면 맞은 무리 한가운데 크게, 한 마리면 그 몸에.
                if (cue.Critical && !BattlePresentation.ReducedFlashes && targets[0] != null)
                    StartCoroutine(CriticalFlareCoroutine(
                        isAoe ? targetCenter : BattleFraming.ModelBounds(targets[0]).center, accent,
                        isAoe ? 1.5f : FlareSize(targets[0])));
                Color soundColor = cue.Critical ? Color.Lerp(accent, Color.white, 0.2f) : Color.Lerp(impactColor, Color.white, 0.35f);
                PlayShout(BattleShout.Kind.Sound, targetCenter + Vector3.down * 0.1f,
                    BattleShout.Sound(element, shoutWeight), soundColor, 0.85f, 0f, Random.Range(-10f, 10f));
                BattlePresentation.HitStop(cue.HitStopSeconds);
                if (cue.Finisher) BattlePresentation.SlowMotion(0.3f, 0.6f);

                if (isAoe)
                    CreateBigExplosion(targetCenter);
                if (signature && targets[0] != null)
                    PlaySignatureBurst(isAoe ? targetCenter : BattleFraming.ModelBounds(targets[0]).center, element,
                        SignatureBurstSize(isAoe ? bossModel : targets[0]));
                if (follower != null)
                    follower.Shake(cue.ShakeAmplitude(isAoe), cue.ShakeSeconds(isAoe));
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(cue.Heavy ? SfxType.CriticalHit : SfxType.Hit);
                    if (targets[0] != null)
                        StartCoroutine(PlayCryDelayed(CryKey(CryOf(targets[0], false), true), 0.1f));
                }

                const float recoverDuration = 0.30f;
                Vector3 bossContact = bossModel.transform.position;
                timer = 0f;
                Vector3 camHit = shotTarget;
                Vector3 kickDir = targetCenter - bossStart;
                kickDir.y = 0f;
                kickDir = kickDir.sqrMagnitude > 0.0001f ? kickDir.normalized : Vector3.back;
                while (timer < recoverDuration)
                {
                    if (bossModel == null) yield break;
                    timer += PresentationDelta;
                    if (signature)
                    {
                        signatureElapsed = BossCastSeconds + timer;
                        if (timer >= BattleFlourish.SignatureTailSeconds) EndSignature();
                    }
                    float k = Mathf.Clamp01(timer / recoverDuration);
                    bossModel.transform.position = Vector3.Lerp(bossContact, restPos, k);
                    BattleMotion.Pose after = BattleMotion.Evaluate(bossKind, bossFamily,
                        Mathf.Lerp(bossImpactP, BattleMotion.RestProgress, k)).Scaled(bossPoseScale);
                    bossModel.transform.rotation = restRot * Quaternion.Euler(after.Pitch, after.Yaw, after.Roll);
                    if (hasBaseCam && motion && follower != null)
                    {
                        Vector3 camPos = camHit + BattleCameraDirector.Kick(kickDir, timer, 0.12f + 0.12f * w);
                        follower.SetBattleShot(camPos, Quaternion.Slerp(baseCamRot,
                            Quaternion.LookRotation(Vector3.Lerp(targetCenter, bossStart, 0.3f) - camPos), 0.5f), 1f - k);
                    }
                    yield return null;
                }
            }
            finally
            {
                if (projectiles != null)
                    for (int i = 0; i < projectiles.Length; i++)
                        if (projectiles[i] != null) Destroy(projectiles[i]);
                if (bossModel != null) bossModel.transform.SetPositionAndRotation(restPos, restRot);
                if (signature) EndSignature();
                ClearCameraShot();
                playingSkill = false;
            }
            onComplete?.Invoke();
        }
    }
}
