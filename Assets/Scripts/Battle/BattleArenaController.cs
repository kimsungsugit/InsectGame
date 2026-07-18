using System.Collections;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Battle
{
    public class BattleArenaController : MonoBehaviour
    {
        private GameObject arenaRoot;
        private GameObject playerModel;
        private GameObject enemyModel;
        private GameObject arenaFloor;
        private GameObject arenaLight;
        private GameObject[] teamModels;
        private GameObject bossModel;

        private Vector3 arenaCenter;
        private Vector3 playerBattlePos;
        private Vector3 enemyBattlePos;
        private Vector3 bossBattlePos;

        private bool isActive;
        private CameraFollower cachedCameraFollower;

        private int selectedTeamIndex = -1;

        // 전투 모델 스케일 — 전투는 필드 rarity 배율(1.0~1.9) 미적용·고정 배율(등급 무관 동일 크기).
        // 곤충이 화면에서 과대해 하향(카메라·battlePos·FOV·아레나 지오메트리는 불변 = "모델만 축소").
        // 보스는 위압 비율 유지, 팀은 5마리 호 배치라 더 작게.
        private const float BattleInsectScale = 1.1f;   // 1v1 플레이어/적 (기존 1.5)
        private const float BossInsectScale = 1.85f;    // 레이드 보스 (기존 2.5)
        private const float TeamInsectScale = 0.8f;     // 레이드 팀원 (기존 1.0)

        public bool IsActive => isActive;
        public Vector3 ArenaCenter => arenaCenter;
        public Vector3 PlayerModelPos => playerBattlePos;
        public Vector3 EnemyModelPos => enemyBattlePos;
        public GameObject PlayerModel => playerModel;
        public GameObject EnemyModel => enemyModel;
        public GameObject BossModel => bossModel;
        public int TeamModelCount => teamModels != null ? teamModels.Length : 0;
        public GameObject GetTeamModel(int index)
        {
            if (teamModels == null || index < 0 || index >= teamModels.Length) return null;
            return teamModels[index];
        }

        public void SetSelectedTeamIndex(int index) { selectedTeamIndex = index; }

        // 씬 전환/컴포넌트 비활성화 시 정리 — CleanupArena 위임 (DRY + model 필드 null화 포함).
        // 옛은 OnDisable과 CleanupArena가 동일 작업을 중복 수행 + OnDisable에 model null화 누락.
        private void OnDisable()
        {
            CleanupArena();
        }

        public void SetupNormalBattle(InsectData playerInsect, int playerLevel,
            InsectData enemyInsect, int enemyLevel, bool enemyShiny,
            Vector3 worldPlayerPos, Vector3 worldEnemyPos)
        {
            CleanupArena();

            // 아레나를 필드와 완전 분리된 위치에 생성 (필드 오브젝트와 겹침 방지)
            arenaCenter = new Vector3(1000f, 0f, 1000f);

            // 플레이어 앞(가까이), 적 뒤(멀리) — 깊이감 있는 대결 구도
            playerBattlePos = arenaCenter + new Vector3(-1.2f, 0.5f, -2.5f);
            enemyBattlePos = arenaCenter + new Vector3(1.2f, 0.5f, 2.5f);

            arenaRoot = new GameObject("BattleArena");
            arenaRoot.transform.position = arenaCenter;

            CreateArenaFloor();
            CreateBattleLight();

            playerModel = CreateBattleInsect(playerInsect, playerLevel, false, playerBattlePos, BattleInsectScale);
            playerModel.name = "BattleInsect_Player";

            enemyModel = CreateBattleInsect(enemyInsect, enemyLevel, enemyShiny, enemyBattlePos, BattleInsectScale);
            enemyModel.name = "BattleInsect_Enemy";

            // 서로 마주보게
            playerModel.transform.LookAt(enemyBattlePos);
            enemyModel.transform.LookAt(playerBattlePos);

            // 카메라를 아레나 정면으로 이동
            SetupBattleCamera();

            isActive = true;
        }

        public void SetupRaidBattle(InsectData bossInsect, int bossLevel, bool bossShiny,
            InsectData[] teamInsects, int[] teamLevels,
            Vector3 worldBossPos)
        {
            CleanupArena();

            // 아레나를 필드와 완전 분리된 위치에 생성 (필드 오브젝트와 겹침 방지)
            arenaCenter = new Vector3(1000f, 0f, 1000f);

            // 보스: 멀리 위에, 팀: 가까이 아래 — 올려보는 구도
            bossBattlePos = arenaCenter + new Vector3(0f, 1.2f, 4f);
            playerBattlePos = arenaCenter + new Vector3(0f, 0.5f, -3.5f);
            enemyBattlePos = bossBattlePos;

            arenaRoot = new GameObject("RaidArena");
            arenaRoot.transform.position = arenaCenter;

            CreateArenaFloor();
            CreateBattleLight();

            bossModel = CreateBattleInsect(bossInsect, bossLevel, bossShiny, bossBattlePos, BossInsectScale);
            bossModel.name = "RaidInsect_Boss";
            bossModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            CreateBossAura(bossModel.transform);

            int count = teamInsects != null ? teamInsects.Length : 0;
            teamModels = new GameObject[count];

            // 호 형태 배치: 카메라에서 봤을 때 겹치지 않게
            for (int i = 0; i < count; i++)
            {
                if (teamInsects[i] == null) continue;
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float x = Mathf.Lerp(-2.5f, 2.5f, t);
                float z = -3.5f + Mathf.Abs(t - 0.5f) * 2f; // 양 끝이 약간 뒤로
                Vector3 pos = arenaCenter + new Vector3(x, 0.5f, z);
                teamModels[i] = CreateBattleInsect(teamInsects[i], teamLevels[i], false, pos, TeamInsectScale);
                teamModels[i].name = $"RaidInsect_Team_{i}";
                teamModels[i].transform.LookAt(bossBattlePos); // 보스를 바라봄
            }

            SetupBattleCamera();
            isActive = true;
        }

        private GameObject CreateBattleInsect(InsectData insectData, int level, bool isShiny, Vector3 position, float scale)
        {
            GameObject insectObj = new GameObject($"Insect_{insectData.insectId}");
            insectObj.transform.SetParent(arenaRoot.transform, false);
            insectObj.transform.position = position;
            insectObj.transform.localScale = Vector3.one * scale;

            InsectEntity entity = insectObj.AddComponent<InsectEntity>();
            entity.BuildForBattle(insectData, level, isShiny);

            return insectObj;
        }

        // 1v1 배틀 중 플레이어 곤충 교체 시 호출. 이전 모델을 파괴하고 새 곤충으로 재생성.
        public void RebuildPlayerInsect(InsectData newInsect, int newLevel)
        {
            if (!isActive || arenaRoot == null || newInsect == null) return;

            if (playerModel != null)
            {
                Destroy(playerModel);
                playerModel = null;
            }

            playerModel = CreateBattleInsect(newInsect, newLevel, false, playerBattlePos, BattleInsectScale);
            playerModel.name = "BattleInsect_Player";

            if (enemyModel != null)
                playerModel.transform.LookAt(enemyModel.transform.position);
            else
                playerModel.transform.LookAt(enemyBattlePos);
        }

        private void CreateArenaFloor()
        {
            arenaFloor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arenaFloor.name = "ArenaFloor";
            arenaFloor.transform.SetParent(arenaRoot.transform, false);
            arenaFloor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            arenaFloor.transform.localScale = new Vector3(8f, 0.1f, 8f);

            Material floorMat = CreateSafeMaterial(new Color(0.15f, 0.2f, 0.12f));
            arenaFloor.GetComponent<MeshRenderer>().material = floorMat;

            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                float r = 3.8f;
                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = $"ArenaStone_{i}";
                stone.transform.SetParent(arenaRoot.transform, false);
                stone.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, 0.1f, Mathf.Sin(angle) * r);
                stone.transform.localScale = new Vector3(0.4f, 0.25f, 0.4f);
                Material stoneMat = CreateSafeMaterial(new Color(0.4f, 0.4f, 0.38f));
                stone.GetComponent<MeshRenderer>().material = stoneMat;
                Object.Destroy(stone.GetComponent<Collider>());
            }

            GameObject centerLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            centerLine.name = "CenterLine";
            centerLine.transform.SetParent(arenaRoot.transform, false);
            centerLine.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            centerLine.transform.localScale = new Vector3(6f, 0.01f, 0.1f);
            Material lineMat = CreateSafeMaterial(new Color(0.8f, 0.8f, 0.3f, 0.5f));
            centerLine.GetComponent<MeshRenderer>().material = lineMat;
            Object.Destroy(centerLine.GetComponent<Collider>());

            // 아레나 경계벽 — 필드가 보이지 않도록 차단
            Material wallMat = CreateSafeMaterial(new Color(0.2f, 0.35f, 0.18f));
            string[] wallNames = { "WallN", "WallS", "WallE", "WallW" };
            Vector3[] wallPositions = {
                new Vector3(0f, 2.5f, 5f),
                new Vector3(0f, 2.5f, -5f),
                new Vector3(5f, 2.5f, 0f),
                new Vector3(-5f, 2.5f, 0f)
            };
            Vector3[] wallScales = {
                new Vector3(10f, 5f, 0.2f),
                new Vector3(10f, 5f, 0.2f),
                new Vector3(0.2f, 5f, 10f),
                new Vector3(0.2f, 5f, 10f)
            };
            for (int i = 0; i < 4; i++)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = wallNames[i];
                wall.transform.SetParent(arenaRoot.transform, false);
                wall.transform.localPosition = wallPositions[i];
                wall.transform.localScale = wallScales[i];
                wall.GetComponent<MeshRenderer>().material = wallMat;
                Object.Destroy(wall.GetComponent<Collider>());
            }
        }

        private void CreateBattleLight()
        {
            arenaLight = new GameObject("BattleLight");
            arenaLight.transform.SetParent(arenaRoot.transform, false);
            arenaLight.transform.localPosition = new Vector3(0f, 8f, 0f);
            arenaLight.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            Light light = arenaLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.2f;
        }

        private void CreateBossAura(Transform parent)
        {
            GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            aura.name = "BossAura";
            aura.transform.SetParent(parent, false);
            aura.transform.localPosition = Vector3.zero;
            aura.transform.localScale = Vector3.one * 2.5f;
            Material auraMat = CreateSafeMaterial(new Color(0.9f, 0.2f, 0.1f, 0.08f));
            auraMat.SetFloat("_Mode", 3);
            auraMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            auraMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            auraMat.SetInt("_ZWrite", 0);
            auraMat.EnableKeyword("_ALPHABLEND_ON");
            auraMat.renderQueue = 3000;
            aura.GetComponent<MeshRenderer>().material = auraMat;
            Object.Destroy(aura.GetComponent<Collider>());
        }

        public void PlayAttackAnimation(bool isPlayerAttacking, System.Action onImpact = null)
        {
            if (!isActive) return;

            GameObject attacker = isPlayerAttacking ? playerModel : enemyModel;
            GameObject target = isPlayerAttacking ? enemyModel : playerModel;

            if (attacker == null || target == null) return;

            StartCoroutine(AttackCoroutine(attacker, target, onImpact));
        }

        private IEnumerator AttackCoroutine(GameObject attacker, GameObject target, System.Action onImpact)
        {
            Vector3 startPos = attacker.transform.position;
            Vector3 targetPos = target != null ? target.transform.position : startPos;

            // 근접: 적 앞 95%까지 러시 → 임팩트 → 원위치 복귀
            // Phase 1: rush 95% (0~0.3s)
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = t / 0.3f;
                float eased = progress * progress * (3f - 2f * progress);
                Vector3 pos = Vector3.Lerp(startPos, targetPos, eased * 0.95f);
                pos.y += Mathf.Sin(eased * Mathf.PI) * 0.8f;
                attacker.transform.position = pos;
                yield return null;
            }

            onImpact?.Invoke();
            CreateImpactEffect(targetPos);

            if (target != null)
                StartCoroutine(ShakeModel(target, 0.3f, 0.3f));

            // Phase 2: return (0.3s)
            t = 0f;
            Vector3 contactPos = Vector3.Lerp(startPos, targetPos, 0.95f);
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = t / 0.3f;
                attacker.transform.position = Vector3.Lerp(contactPos, startPos, progress);
                yield return null;
            }

            attacker.transform.position = startPos;
        }

        private IEnumerator ShakeModel(GameObject model, float duration, float magnitude)
        {
            Vector3 original = model.transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float x = Random.Range(-1f, 1f) * magnitude * (1f - t / duration);
                float z = Random.Range(-1f, 1f) * magnitude * (1f - t / duration);
                model.transform.position = original + new Vector3(x, 0f, z);
                yield return null;
            }
            model.transform.position = original;
        }

        private void CreateImpactEffect(Vector3 position)
        {
            StartCoroutine(ImpactEffectCoroutine(position));
        }

        private IEnumerator ImpactEffectCoroutine(Vector3 position)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ImpactRing";
            ring.transform.SetParent(arenaRoot.transform, false);
            ring.transform.position = position;
            ring.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
            Material ringMat = CreateSafeMaterial(new Color(1f, 0.8f, 0.2f, 0.7f));
            ring.GetComponent<MeshRenderer>().material = ringMat;
            Object.Destroy(ring.GetComponent<Collider>());

            GameObject[] sparks = new GameObject[8];
            for (int i = 0; i < 8; i++)
            {
                sparks[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sparks[i].name = $"Spark_{i}";
                sparks[i].transform.SetParent(arenaRoot.transform, false);
                sparks[i].transform.position = position;
                sparks[i].transform.localScale = Vector3.one * 0.15f;
                Material sparkMat = CreateSafeMaterial(new Color(1f, 1f, 0.5f, 0.9f));
                sparks[i].GetComponent<MeshRenderer>().material = sparkMat;
                Object.Destroy(sparks[i].GetComponent<Collider>());
            }

            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float progress = t / 0.5f;

                float ringSize = 0.5f + progress * 4f;
                ring.transform.localScale = new Vector3(ringSize, 0.01f * (1f - progress), ringSize);
                ringMat.color = new Color(1f, 0.8f, 0.2f, 0.7f * (1f - progress));

                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 2f / 8f;
                    float dist = progress * 3f;
                    float sparkY = Mathf.Sin(progress * Mathf.PI) * 1.5f;
                    sparks[i].transform.position = position + new Vector3(Mathf.Cos(angle) * dist, sparkY, Mathf.Sin(angle) * dist);
                    float sparkScale = 0.15f * (1f - progress);
                    sparks[i].transform.localScale = Vector3.one * sparkScale;
                }

                yield return null;
            }

            Destroy(ring);
            foreach (var s in sparks)
                if (s != null) Destroy(s);
        }

        public void PlayUniteAttackAnimation(System.Action onComplete)
        {
            StartCoroutine(UniteAttackCoroutine(onComplete));
        }

        private IEnumerator UniteAttackCoroutine(System.Action onComplete)
        {
            if (teamModels == null || bossModel == null) { onComplete?.Invoke(); yield break; }

            Vector3 bossPos = bossModel.transform.position;

            for (int i = 0; i < teamModels.Length; i++)
            {
                if (teamModels[i] == null) continue;
                Vector3 start = teamModels[i].transform.position;
                float dur = 0.25f;
                float t = 0f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float progress = t / dur;
                    float eased = progress * progress;
                    Vector3 pos = Vector3.Lerp(start, bossPos, eased * 0.6f);
                    pos.y += Mathf.Sin(eased * Mathf.PI) * 0.5f;
                    teamModels[i].transform.position = pos;
                    yield return null;
                }

                CreateImpactEffect(bossPos);
                StartCoroutine(ShakeModel(bossModel, 0.2f, 0.4f));

                t = 0f;
                while (t < 0.15f)
                {
                    t += Time.deltaTime;
                    teamModels[i].transform.position = Vector3.Lerp(bossPos * 0.5f + start * 0.5f, start, t / 0.15f);
                    yield return null;
                }
                teamModels[i].transform.position = start;
            }

            CreateBigExplosion(bossPos);
            yield return new WaitForSeconds(0.5f);

            onComplete?.Invoke();
        }

        private void CreateBigExplosion(Vector3 position)
        {
            StartCoroutine(BigExplosionCoroutine(position));
        }

        private IEnumerator BigExplosionCoroutine(Vector3 position)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Explosion";
            flash.transform.SetParent(arenaRoot.transform, false);
            flash.transform.position = position;
            Material flashMat = CreateSafeMaterial(new Color(1f, 0.9f, 0.3f, 0.9f));
            flash.GetComponent<MeshRenderer>().material = flashMat;
            Object.Destroy(flash.GetComponent<Collider>());

            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                float progress = t / 0.8f;
                float size = progress * 5f;
                flash.transform.localScale = Vector3.one * size;
                flashMat.color = new Color(1f, 0.9f, 0.3f, 0.9f * (1f - progress));
                yield return null;
            }
            Destroy(flash);
        }

        private void SetupBattleCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            bool isRaid = bossModel != null;
            Vector3 camPos;
            Vector3 lookTarget;

            if (isRaid)
            {
                // 레이드: 팀 뒤쪽 높은 곳에서 보스를 올려보는 각도
                // 보스의 거대함과 위압감을 강조
                camPos = arenaCenter + new Vector3(0f, 4f, -7.5f);
                lookTarget = bossBattlePos + Vector3.up * 0.8f;
            }
            else
            {
                // 1v1: 플레이어 뒤 어깨 너머에서 적을 바라보는 각도
                // 대결 긴장감 + 플레이어 곤충이 가까이 보임
                camPos = playerBattlePos + new Vector3(1.5f, 2.8f, -4f);
                lookTarget = Vector3.Lerp(playerBattlePos, enemyBattlePos, 0.6f) + Vector3.up * 0.3f;
            }

            cam.transform.position = camPos;
            cam.transform.LookAt(lookTarget);

            CameraFollower follower = cam.GetComponent<CameraFollower>();
            if (follower != null)
            {
                follower.EnterBattleMode(playerBattlePos, enemyBattlePos);
                cam.transform.position = camPos;
                cam.transform.LookAt(lookTarget);
            }
        }

        // ===== 3D Skill Effect System =====

        public void PlaySkillEffect(bool isPlayerAttacking, InsectElement element, SkillEffectType effectType, System.Action onImpact = null)
        {
            PlaySkillEffect(isPlayerAttacking, element, effectType, onImpact, false);
        }

        public void PlaySkillEffect(bool isPlayerAttacking, InsectElement element, SkillEffectType effectType, System.Action onImpact, bool isMelee)
        {
            if (!isActive) return;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySkillSFX(element);

            if (effectType == SkillEffectType.BuffAttack)
            {
                PlayBuffEffect(isPlayerAttacking, element);
                onImpact?.Invoke();
                return;
            }
            if (effectType == SkillEffectType.DebuffAttack)
            {
                PlayDebuffEffect(!isPlayerAttacking, element);
                onImpact?.Invoke();
                return;
            }

            StartCoroutine(SkillAttackCoroutine(isPlayerAttacking, element, onImpact, isMelee));
        }

        private IEnumerator SkillAttackCoroutine(bool isPlayerAttacking, InsectElement element, System.Action onImpact, bool isMelee)
        {
            GameObject attacker = isPlayerAttacking ? playerModel : enemyModel;
            if (isPlayerAttacking && teamModels != null && selectedTeamIndex >= 0 && selectedTeamIndex < teamModels.Length)
                attacker = teamModels[selectedTeamIndex];

            GameObject target = isPlayerAttacking ? (bossModel ?? enemyModel) : playerModel;
            if (attacker == null || target == null) { onImpact?.Invoke(); yield break; }

            Vector3 startPos = attacker.transform.position;
            Vector3 targetPos = target.transform.position;
            Color elemColor = GetElementColor3D(element);

            if (isMelee)
            {
                // 근접 스킬: 러시 → 임팩트 → 복귀 (투사체 없음)
                float t = 0f;
                while (t < 0.3f)
                {
                    t += Time.deltaTime;
                    float progress = t / 0.3f;
                    float eased = progress * progress * (3f - 2f * progress);
                    Vector3 pos = Vector3.Lerp(startPos, targetPos, eased * 0.95f);
                    pos.y += Mathf.Sin(eased * Mathf.PI) * 0.8f;
                    attacker.transform.position = pos;
                    CreateTrailParticle(pos, elemColor, 0.1f);
                    yield return null;
                }

                onImpact?.Invoke();
                CreateElementImpact3D(targetPos, element, elemColor);
                StartCoroutine(ShakeModel(target, 0.4f, 0.35f));

                if (cachedCameraFollower == null && Camera.main != null)
                    cachedCameraFollower = Camera.main.GetComponent<CameraFollower>();
                if (cachedCameraFollower != null)
                {
                    float intensity = (element == InsectElement.Earth || element == InsectElement.Metal) ? 0.35f : 0.2f;
                    cachedCameraFollower.Shake(intensity, 0.25f);
                }

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(SfxType.Hit);

                // 복귀
                t = 0f;
                Vector3 contactPos = Vector3.Lerp(startPos, targetPos, 0.95f);
                while (t < 0.3f)
                {
                    t += Time.deltaTime;
                    float progress = t / 0.3f;
                    attacker.transform.position = Vector3.Lerp(contactPos, startPos, progress);
                    yield return null;
                }
                attacker.transform.position = startPos;
                yield break;
            }

            // 원거리: 투사체 발사
            // Phase 1: element projectile (0~0.4s)
            GameObject projectile = CreateElementProjectile(element, elemColor);
            projectile.transform.position = startPos + Vector3.up * 0.5f;

            float tt = 0f;
            while (tt < 0.4f)
            {
                tt += Time.deltaTime;
                float progress = tt / 0.4f;
                float eased = progress * progress * (3f - 2f * progress);
                Vector3 pos = Vector3.Lerp(startPos + Vector3.up * 0.5f, targetPos + Vector3.up * 0.5f, eased);
                pos += GetElementTrajectoryOffset(element, progress);
                projectile.transform.position = pos;
                projectile.transform.Rotate(Vector3.up, 720f * Time.deltaTime);
                CreateTrailParticle(pos, elemColor, 0.08f);
                yield return null;
            }

            Destroy(projectile);

            // Phase 2: element impact
            onImpact?.Invoke();
            CreateElementImpact3D(targetPos, element, elemColor);
            StartCoroutine(ShakeModel(target, 0.4f, 0.35f));

            // 카메라 쉐이크 (속성별 강도 차이)
            if (cachedCameraFollower == null && Camera.main != null)
                cachedCameraFollower = Camera.main.GetComponent<CameraFollower>();
            if (cachedCameraFollower != null)
            {
                float intensity = (element == InsectElement.Earth || element == InsectElement.Metal) ? 0.35f : 0.2f;
                cachedCameraFollower.Shake(intensity, 0.25f);
            }

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SfxType.Hit);

            yield return new WaitForSeconds(0.4f);
        }

        private GameObject CreateElementProjectile(InsectElement element, Color color)
        {
            GameObject proj = new GameObject($"Projectile_{element}");
            proj.transform.SetParent(arenaRoot.transform, false);

            switch (element)
            {
                case InsectElement.Poison:
                    CreateProjPart(proj, PrimitiveType.Sphere, Vector3.zero, 0.3f, color);
                    CreateProjPart(proj, PrimitiveType.Sphere, new Vector3(0.15f, 0.1f, 0f), 0.15f, new Color(color.r, color.g, color.b, 0.6f));
                    CreateProjPart(proj, PrimitiveType.Sphere, new Vector3(-0.1f, -0.12f, 0.08f), 0.12f, new Color(color.r, color.g, color.b, 0.5f));
                    break;

                case InsectElement.Water:
                    CreateProjPart(proj, PrimitiveType.Sphere, Vector3.zero, 0.3f, color);
                    CreateProjPart(proj, PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), 0.15f, new Color(0.5f, 0.8f, 1f));
                    break;

                case InsectElement.Leaf:
                    CreateProjPart(proj, PrimitiveType.Cube, Vector3.zero, new Vector3(0.4f, 0.02f, 0.15f), color);
                    CreateProjPart(proj, PrimitiveType.Cube, Vector3.zero, new Vector3(0.15f, 0.02f, 0.4f), new Color(color.r * 0.8f, color.g, color.b * 0.8f));
                    break;

                case InsectElement.Wind:
                    CreateProjPart(proj, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.3f, 0.05f, 0.3f), new Color(color.r, color.g, color.b, 0.4f));
                    CreateProjPart(proj, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.05f, 0.2f), new Color(1f, 1f, 1f, 0.3f));
                    break;

                case InsectElement.Electric:
                    CreateProjPart(proj, PrimitiveType.Sphere, Vector3.zero, 0.25f, color);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f;
                        CreateProjPart(proj, PrimitiveType.Sphere,
                            new Vector3(Mathf.Cos(a) * 0.2f, Mathf.Sin(a) * 0.2f, 0f), 0.08f,
                            new Color(1f, 1f, 0.5f));
                    }
                    break;

                case InsectElement.Earth:
                    CreateProjPart(proj, PrimitiveType.Cube, Vector3.zero, new Vector3(0.25f, 0.25f, 0.25f), color);
                    CreateProjPart(proj, PrimitiveType.Cube, new Vector3(0.12f, 0.1f, 0.05f), new Vector3(0.15f, 0.15f, 0.15f), new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f));
                    break;

                case InsectElement.Light:
                    CreateProjPart(proj, PrimitiveType.Sphere, Vector3.zero, 0.25f, color);
                    CreateProjPart(proj, PrimitiveType.Cube, Vector3.zero, new Vector3(0.6f, 0.04f, 0.04f), new Color(1f, 1f, 0.8f, 0.7f));
                    CreateProjPart(proj, PrimitiveType.Cube, Vector3.zero, new Vector3(0.04f, 0.6f, 0.04f), new Color(1f, 1f, 0.8f, 0.7f));
                    break;

                case InsectElement.Dark:
                    CreateProjPart(proj, PrimitiveType.Sphere, Vector3.zero, 0.3f, color);
                    CreateProjPart(proj, PrimitiveType.Sphere, Vector3.zero, 0.5f, new Color(0.1f, 0.05f, 0.15f, 0.3f));
                    break;

                case InsectElement.Metal:
                    CreateProjPart(proj, PrimitiveType.Cube, Vector3.zero, new Vector3(0.5f, 0.03f, 0.15f), color);
                    CreateProjPart(proj, PrimitiveType.Cube, Vector3.zero, new Vector3(0.03f, 0.5f, 0.15f), new Color(0.9f, 0.9f, 0.95f));
                    break;

                default: // Bug, None
                    CreateProjPart(proj, PrimitiveType.Sphere, Vector3.zero, 0.25f, color);
                    break;
            }

            return proj;
        }

        private Vector3 GetElementTrajectoryOffset(InsectElement element, float progress)
        {
            switch (element)
            {
                case InsectElement.Wind:
                    float spiralAngle = progress * Mathf.PI * 4f;
                    return new Vector3(Mathf.Cos(spiralAngle) * 0.5f * (1f - progress),
                                     Mathf.Sin(spiralAngle) * 0.5f * (1f - progress), 0f);
                case InsectElement.Electric:
                    return new Vector3(Mathf.Sin(progress * Mathf.PI * 6f) * 0.4f, 0f, 0f);
                case InsectElement.Water:
                    return new Vector3(0f, Mathf.Sin(progress * Mathf.PI) * 1.5f, 0f);
                case InsectElement.Earth:
                    return new Vector3(0f, -0.3f + Mathf.Sin(progress * Mathf.PI * 2f) * 0.15f, 0f);
                case InsectElement.Light:
                    return Vector3.zero;
                case InsectElement.Dark:
                    return new Vector3(Mathf.Sin(progress * Mathf.PI * 8f) * 0.2f,
                                     Mathf.Cos(progress * Mathf.PI * 6f) * 0.2f, 0f);
                default:
                    return new Vector3(0f, Mathf.Sin(progress * Mathf.PI) * 0.8f, 0f);
            }
        }

        private void CreateElementImpact3D(Vector3 position, InsectElement element, Color color)
        {
            StartCoroutine(ElementImpactCoroutine(position, element, color));
        }

        private IEnumerator ElementImpactCoroutine(Vector3 position, InsectElement element, Color color)
        {
            switch (element)
            {
                case InsectElement.Poison:
                    yield return PoisonImpact3D(position, color);
                    break;
                case InsectElement.Water:
                    yield return WaterImpact3D(position, color);
                    break;
                case InsectElement.Leaf:
                    yield return LeafImpact3D(position, color);
                    break;
                case InsectElement.Wind:
                    yield return WindImpact3D(position, color);
                    break;
                case InsectElement.Electric:
                    yield return ElectricImpact3D(position, color);
                    break;
                case InsectElement.Earth:
                    yield return EarthImpact3D(position, color);
                    break;
                case InsectElement.Light:
                    yield return LightImpact3D(position, color);
                    break;
                case InsectElement.Dark:
                    yield return DarkImpact3D(position, color);
                    break;
                case InsectElement.Metal:
                    yield return MetalImpact3D(position, color);
                    break;
                default:
                    yield return DefaultImpact3D(position, color);
                    break;
            }
        }

        // --- Element Impact Coroutines ---

        private IEnumerator PoisonImpact3D(Vector3 pos, Color color)
        {
            GameObject[] clouds = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                clouds[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                clouds[i].name = $"PoisonCloud_{i}";
                clouds[i].transform.SetParent(arenaRoot.transform, false);
                float a = i * Mathf.PI * 2f / 5f;
                clouds[i].transform.position = pos + new Vector3(Mathf.Cos(a) * 0.3f, 0.2f, Mathf.Sin(a) * 0.3f);
                clouds[i].transform.localScale = Vector3.one * 0.3f;
                Material mat = CreateTransparentMaterial(new Color(color.r, color.g, color.b, 0.5f));
                clouds[i].GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(clouds[i].GetComponent<Collider>());
            }

            float t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                float progress = t / 0.7f;
                for (int i = 0; i < 5; i++)
                {
                    if (clouds[i] == null) continue;
                    float a = i * Mathf.PI * 2f / 5f + progress * 2f;
                    float r = 0.3f + progress * 1.5f;
                    float y = 0.2f + progress * 1.2f;
                    clouds[i].transform.position = pos + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    float scale = 0.3f + progress * 0.8f;
                    clouds[i].transform.localScale = Vector3.one * scale;
                    var mat = clouds[i].GetComponent<MeshRenderer>().material;
                    mat.color = new Color(color.r, color.g, color.b, 0.5f * (1f - progress));
                }
                yield return null;
            }
            foreach (var c in clouds) if (c != null) Destroy(c);
        }

        private IEnumerator WaterImpact3D(Vector3 pos, Color color)
        {
            // Water pillar rising
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "WaterPillar";
            pillar.transform.SetParent(arenaRoot.transform, false);
            pillar.transform.position = pos;
            pillar.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
            Material pillarMat = CreateTransparentMaterial(new Color(color.r, color.g, color.b, 0.6f));
            pillar.GetComponent<MeshRenderer>().material = pillarMat;
            Object.Destroy(pillar.GetComponent<Collider>());

            // Splash droplets
            GameObject[] drops = new GameObject[8];
            Vector3[] dropDirs = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                drops[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                drops[i].name = $"WaterDrop_{i}";
                drops[i].transform.SetParent(arenaRoot.transform, false);
                drops[i].transform.position = pos;
                drops[i].transform.localScale = Vector3.one * 0.12f;
                Material dMat = CreateTransparentMaterial(new Color(0.4f, 0.7f, 1f, 0.7f));
                drops[i].GetComponent<MeshRenderer>().material = dMat;
                Object.Destroy(drops[i].GetComponent<Collider>());
                float a = i * Mathf.PI * 2f / 8f;
                dropDirs[i] = new Vector3(Mathf.Cos(a) * 2f, 2f, Mathf.Sin(a) * 2f);
            }

            float t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                float progress = t / 0.7f;

                // Pillar grows then shrinks
                float pillarH = progress < 0.4f ? progress / 0.4f * 2f : 2f * (1f - (progress - 0.4f) / 0.6f);
                pillar.transform.localScale = new Vector3(0.5f, pillarH, 0.5f);
                pillar.transform.position = pos + Vector3.up * pillarH * 0.5f;
                pillarMat.color = new Color(color.r, color.g, color.b, 0.6f * (1f - progress));

                // Droplets fly outward and fall
                for (int i = 0; i < 8; i++)
                {
                    if (drops[i] == null) continue;
                    Vector3 dp = pos + dropDirs[i] * progress;
                    dp.y = pos.y + dropDirs[i].y * progress - 4.9f * progress * progress;
                    drops[i].transform.position = dp;
                    float s = 0.12f * (1f - progress);
                    drops[i].transform.localScale = Vector3.one * Mathf.Max(s, 0.01f);
                }
                yield return null;
            }
            Destroy(pillar);
            foreach (var d in drops) if (d != null) Destroy(d);
        }

        private IEnumerator LeafImpact3D(Vector3 pos, Color color)
        {
            GameObject[] leaves = new GameObject[6];
            float[] angles = new float[6];
            for (int i = 0; i < 6; i++)
            {
                leaves[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaves[i].name = $"Leaf_{i}";
                leaves[i].transform.SetParent(arenaRoot.transform, false);
                leaves[i].transform.position = pos;
                leaves[i].transform.localScale = new Vector3(0.3f, 0.02f, 0.15f);
                float shade = 0.7f + Random.Range(0f, 0.3f);
                Material mat = CreateSafeMaterial(new Color(color.r * shade, color.g * shade, color.b * shade));
                leaves[i].GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(leaves[i].GetComponent<Collider>());
                angles[i] = i * Mathf.PI * 2f / 6f;
            }

            float t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                float progress = t / 0.7f;
                for (int i = 0; i < 6; i++)
                {
                    if (leaves[i] == null) continue;
                    float dist = progress * 2.5f;
                    float y = Mathf.Sin(progress * Mathf.PI) * 1.5f;
                    leaves[i].transform.position = pos + new Vector3(Mathf.Cos(angles[i]) * dist, y, Mathf.Sin(angles[i]) * dist);
                    leaves[i].transform.Rotate(Vector3.up, 360f * Time.deltaTime);
                    leaves[i].transform.Rotate(Vector3.right, 180f * Time.deltaTime);
                    float s = 1f - progress * 0.5f;
                    leaves[i].transform.localScale = new Vector3(0.3f * s, 0.02f, 0.15f * s);
                }
                yield return null;
            }
            foreach (var l in leaves) if (l != null) Destroy(l);
        }

        private IEnumerator WindImpact3D(Vector3 pos, Color color)
        {
            // Concentric spinning cylinders
            GameObject[] rings = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                rings[i] = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rings[i].name = $"WindRing_{i}";
                rings[i].transform.SetParent(arenaRoot.transform, false);
                rings[i].transform.position = pos + Vector3.up * (i * 0.3f);
                float baseScale = 0.5f + i * 0.3f;
                rings[i].transform.localScale = new Vector3(baseScale, 0.03f, baseScale);
                Material mat = CreateTransparentMaterial(new Color(color.r, color.g, color.b, 0.4f - i * 0.1f));
                rings[i].GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(rings[i].GetComponent<Collider>());
            }

            // Wind debris
            GameObject[] debris = new GameObject[5];
            float[] debrisAngles = new float[5];
            for (int i = 0; i < 5; i++)
            {
                debris[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debris[i].name = $"WindDebris_{i}";
                debris[i].transform.SetParent(arenaRoot.transform, false);
                debris[i].transform.position = pos;
                debris[i].transform.localScale = Vector3.one * 0.08f;
                Material dMat = CreateSafeMaterial(new Color(0.5f, 0.7f, 0.5f));
                debris[i].GetComponent<MeshRenderer>().material = dMat;
                Object.Destroy(debris[i].GetComponent<Collider>());
                debrisAngles[i] = Random.Range(0f, Mathf.PI * 2f);
            }

            float t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                float progress = t / 0.7f;
                for (int i = 0; i < 3; i++)
                {
                    if (rings[i] == null) continue;
                    float expand = (0.5f + i * 0.3f) + progress * 2f;
                    rings[i].transform.localScale = new Vector3(expand, 0.03f, expand);
                    rings[i].transform.Rotate(Vector3.up, (300f + i * 100f) * Time.deltaTime);
                    float y = pos.y + i * 0.3f + progress * 1.5f;
                    rings[i].transform.position = new Vector3(pos.x, y, pos.z);
                    var mat = rings[i].GetComponent<MeshRenderer>().material;
                    mat.color = new Color(color.r, color.g, color.b, (0.4f - i * 0.1f) * (1f - progress));
                }
                for (int i = 0; i < 5; i++)
                {
                    if (debris[i] == null) continue;
                    debrisAngles[i] += 8f * Time.deltaTime;
                    float r = progress * 2f;
                    float dy = Mathf.Sin(progress * Mathf.PI * 2f + i) * 0.5f + progress * 1f;
                    debris[i].transform.position = pos + new Vector3(Mathf.Cos(debrisAngles[i]) * r, dy, Mathf.Sin(debrisAngles[i]) * r);
                }
                yield return null;
            }
            foreach (var r in rings) if (r != null) Destroy(r);
            foreach (var d in debris) if (d != null) Destroy(d);
        }

        private IEnumerator ElectricImpact3D(Vector3 pos, Color color)
        {
            // Bright flash sphere
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "ElectricFlash";
            flash.transform.SetParent(arenaRoot.transform, false);
            flash.transform.position = pos;
            flash.transform.localScale = Vector3.one * 0.5f;
            Material flashMat = CreateSafeMaterial(new Color(1f, 1f, 0.8f));
            flash.GetComponent<MeshRenderer>().material = flashMat;
            Object.Destroy(flash.GetComponent<Collider>());

            // Lightning bolts (cylinders in random directions)
            GameObject[] bolts = new GameObject[4];
            Vector3[] boltDirs = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                bolts[i] = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bolts[i].name = $"Lightning_{i}";
                bolts[i].transform.SetParent(arenaRoot.transform, false);
                bolts[i].transform.position = pos;
                bolts[i].transform.localScale = new Vector3(0.05f, 1f, 0.05f);
                float ax = Random.Range(-60f, 60f);
                float az = Random.Range(-60f, 60f);
                bolts[i].transform.rotation = Quaternion.Euler(ax, 0f, az);
                Material bMat = CreateSafeMaterial(color);
                bolts[i].GetComponent<MeshRenderer>().material = bMat;
                Object.Destroy(bolts[i].GetComponent<Collider>());
                boltDirs[i] = new Vector3(ax, 0f, az);
            }

            // Sparks
            GameObject[] sparks = new GameObject[10];
            Vector3[] sparkVels = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                sparks[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sparks[i].name = $"ElecSpark_{i}";
                sparks[i].transform.SetParent(arenaRoot.transform, false);
                sparks[i].transform.position = pos;
                sparks[i].transform.localScale = Vector3.one * 0.06f;
                Material sMat = CreateSafeMaterial(new Color(1f, 1f, 0.5f));
                sparks[i].GetComponent<MeshRenderer>().material = sMat;
                Object.Destroy(sparks[i].GetComponent<Collider>());
                sparkVels[i] = new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 4f), Random.Range(-3f, 3f));
            }

            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float progress = t / 0.6f;

                // Flash expands then fades
                float flashScale = 0.5f + progress * 3f;
                flash.transform.localScale = Vector3.one * flashScale;
                flashMat.color = new Color(1f, 1f, 0.8f, 1f - progress);

                // Bolts jitter
                for (int i = 0; i < 4; i++)
                {
                    if (bolts[i] == null) continue;
                    float jx = boltDirs[i].x + Mathf.Sin(t * 30f + i * 2f) * 20f;
                    float jz = boltDirs[i].z + Mathf.Cos(t * 25f + i * 3f) * 20f;
                    bolts[i].transform.rotation = Quaternion.Euler(jx, 0f, jz);
                    float boltScale = 1f * (1f - progress);
                    bolts[i].transform.localScale = new Vector3(0.05f, boltScale, 0.05f);
                }

                // Sparks fly out
                for (int i = 0; i < 10; i++)
                {
                    if (sparks[i] == null) continue;
                    sparks[i].transform.position = pos + sparkVels[i] * progress;
                    float s = 0.06f * (1f - progress);
                    sparks[i].transform.localScale = Vector3.one * Mathf.Max(s, 0.01f);
                }
                yield return null;
            }
            Destroy(flash);
            foreach (var b in bolts) if (b != null) Destroy(b);
            foreach (var s in sparks) if (s != null) Destroy(s);
        }

        private IEnumerator EarthImpact3D(Vector3 pos, Color color)
        {
            // Rock pillars rising from below
            GameObject[] pillars = new GameObject[5];
            float[] pillarAngles = new float[5];
            float[] pillarDelays = new float[5];
            for (int i = 0; i < 5; i++)
            {
                pillars[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillars[i].name = $"RockPillar_{i}";
                pillars[i].transform.SetParent(arenaRoot.transform, false);
                float a = i * Mathf.PI * 2f / 5f;
                pillarAngles[i] = a;
                float r = 0.5f + i * 0.2f;
                pillars[i].transform.position = pos + new Vector3(Mathf.Cos(a) * r, -1f, Mathf.Sin(a) * r);
                float w = 0.2f + Random.Range(0f, 0.15f);
                float h = 0.8f + Random.Range(0f, 0.6f);
                pillars[i].transform.localScale = new Vector3(w, h, w);
                float shade = 0.8f + Random.Range(-0.2f, 0.2f);
                Material mat = CreateSafeMaterial(new Color(color.r * shade, color.g * shade, color.b * shade));
                pillars[i].GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(pillars[i].GetComponent<Collider>());
                pillarDelays[i] = i * 0.06f;
            }

            // Dust particles
            GameObject[] dust = new GameObject[6];
            for (int i = 0; i < 6; i++)
            {
                dust[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dust[i].name = $"Dust_{i}";
                dust[i].transform.SetParent(arenaRoot.transform, false);
                dust[i].transform.position = pos;
                dust[i].transform.localScale = Vector3.one * 0.15f;
                Material dMat = CreateTransparentMaterial(new Color(0.6f, 0.5f, 0.3f, 0.4f));
                dust[i].GetComponent<MeshRenderer>().material = dMat;
                Object.Destroy(dust[i].GetComponent<Collider>());
            }

            // Crack lines
            GameObject[] cracks = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                cracks[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cracks[i].name = $"Crack_{i}";
                cracks[i].transform.SetParent(arenaRoot.transform, false);
                cracks[i].transform.position = pos + new Vector3(0f, 0.01f, 0f);
                cracks[i].transform.localScale = new Vector3(2f, 0.01f, 0.05f);
                cracks[i].transform.rotation = Quaternion.Euler(0f, i * 60f, 0f);
                Material cMat = CreateSafeMaterial(new Color(0.3f, 0.2f, 0.1f));
                cracks[i].GetComponent<MeshRenderer>().material = cMat;
                Object.Destroy(cracks[i].GetComponent<Collider>());
            }

            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                float progress = t / 0.8f;

                for (int i = 0; i < 5; i++)
                {
                    if (pillars[i] == null) continue;
                    float localP = Mathf.Clamp01((t - pillarDelays[i]) / 0.3f);
                    float r = 0.5f + i * 0.2f;
                    float y = -1f + localP * 1.5f;
                    if (progress > 0.6f) y -= (progress - 0.6f) / 0.4f * 1.5f;
                    pillars[i].transform.position = pos + new Vector3(Mathf.Cos(pillarAngles[i]) * r, y, Mathf.Sin(pillarAngles[i]) * r);
                }

                for (int i = 0; i < 6; i++)
                {
                    if (dust[i] == null) continue;
                    float da = i * Mathf.PI * 2f / 6f;
                    float dr = progress * 1.5f;
                    dust[i].transform.position = pos + new Vector3(Mathf.Cos(da) * dr, progress * 1f, Mathf.Sin(da) * dr);
                    var dMat = dust[i].GetComponent<MeshRenderer>().material;
                    dMat.color = new Color(0.6f, 0.5f, 0.3f, 0.4f * (1f - progress));
                    dust[i].transform.localScale = Vector3.one * (0.15f + progress * 0.3f);
                }

                for (int i = 0; i < 3; i++)
                {
                    if (cracks[i] == null) continue;
                    float crackLen = Mathf.Min(progress * 5f, 2f);
                    cracks[i].transform.localScale = new Vector3(crackLen, 0.01f, 0.05f);
                }
                yield return null;
            }
            foreach (var p in pillars) if (p != null) Destroy(p);
            foreach (var d in dust) if (d != null) Destroy(d);
            foreach (var c in cracks) if (c != null) Destroy(c);
        }

        private IEnumerator LightImpact3D(Vector3 pos, Color color)
        {
            // Light pillar from above
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "LightPillar";
            pillar.transform.SetParent(arenaRoot.transform, false);
            pillar.transform.position = pos + Vector3.up * 4f;
            pillar.transform.localScale = new Vector3(0.8f, 4f, 0.8f);
            Material pillarMat = CreateTransparentMaterial(new Color(1f, 1f, 0.8f, 0.5f));
            pillar.GetComponent<MeshRenderer>().material = pillarMat;
            Object.Destroy(pillar.GetComponent<Collider>());

            // Cross beams
            GameObject crossH = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossH.name = "LightCrossH";
            crossH.transform.SetParent(arenaRoot.transform, false);
            crossH.transform.position = pos;
            crossH.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            Material crossMat = CreateSafeMaterial(new Color(1f, 0.95f, 0.7f));
            crossH.GetComponent<MeshRenderer>().material = crossMat;
            Object.Destroy(crossH.GetComponent<Collider>());

            GameObject crossV = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossV.name = "LightCrossV";
            crossV.transform.SetParent(arenaRoot.transform, false);
            crossV.transform.position = pos;
            crossV.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            Material crossVMat = CreateSafeMaterial(new Color(1f, 0.95f, 0.7f));
            crossV.GetComponent<MeshRenderer>().material = crossVMat;
            Object.Destroy(crossV.GetComponent<Collider>());

            // Star sparkles
            GameObject[] stars = new GameObject[6];
            float[] starAngles = new float[6];
            for (int i = 0; i < 6; i++)
            {
                stars[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stars[i].name = $"Star_{i}";
                stars[i].transform.SetParent(arenaRoot.transform, false);
                stars[i].transform.position = pos;
                stars[i].transform.localScale = Vector3.one * 0.1f;
                Material sMat = CreateSafeMaterial(new Color(1f, 1f, 0.6f));
                stars[i].GetComponent<MeshRenderer>().material = sMat;
                Object.Destroy(stars[i].GetComponent<Collider>());
                starAngles[i] = i * Mathf.PI * 2f / 6f;
            }

            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                float progress = t / 0.8f;

                // Pillar descends and fades
                float pillarY = 4f - progress * 4f;
                pillar.transform.position = pos + Vector3.up * Mathf.Max(pillarY, 0f);
                pillarMat.color = new Color(1f, 1f, 0.8f, 0.5f * (1f - progress * 0.7f));

                // Cross beams expand
                float crossScale = progress * 4f;
                crossH.transform.localScale = new Vector3(crossScale, 0.06f, 0.06f);
                crossV.transform.localScale = new Vector3(0.06f, 0.06f, crossScale);
                crossMat.color = new Color(1f, 0.95f, 0.7f, 1f - progress);
                crossVMat.color = new Color(1f, 0.95f, 0.7f, 1f - progress);

                // Stars fly outward
                for (int i = 0; i < 6; i++)
                {
                    if (stars[i] == null) continue;
                    float dist = progress * 2f;
                    float sy = Mathf.Sin(progress * Mathf.PI) * 1.5f;
                    stars[i].transform.position = pos + new Vector3(Mathf.Cos(starAngles[i]) * dist, sy, Mathf.Sin(starAngles[i]) * dist);
                    float starScale = 0.1f + Mathf.Sin(progress * Mathf.PI) * 0.15f;
                    stars[i].transform.localScale = Vector3.one * starScale;
                }
                yield return null;
            }
            Destroy(pillar);
            Destroy(crossH);
            Destroy(crossV);
            foreach (var s in stars) if (s != null) Destroy(s);
        }

        private IEnumerator DarkImpact3D(Vector3 pos, Color color)
        {
            // Dark orb contracts then explodes
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "DarkOrb";
            orb.transform.SetParent(arenaRoot.transform, false);
            orb.transform.position = pos;
            orb.transform.localScale = Vector3.one * 2f;
            Material orbMat = CreateTransparentMaterial(new Color(color.r, color.g, color.b, 0.6f));
            orb.GetComponent<MeshRenderer>().material = orbMat;
            Object.Destroy(orb.GetComponent<Collider>());

            // Crack lines
            GameObject[] cracks = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                cracks[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cracks[i].name = $"DarkCrack_{i}";
                cracks[i].transform.SetParent(arenaRoot.transform, false);
                cracks[i].transform.position = pos;
                cracks[i].transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                cracks[i].transform.rotation = Quaternion.Euler(0f, i * 45f, 0f);
                Material cMat = CreateSafeMaterial(new Color(0.5f, 0.1f, 0.6f));
                cracks[i].GetComponent<MeshRenderer>().material = cMat;
                Object.Destroy(cracks[i].GetComponent<Collider>());
            }

            // Dark fragments
            GameObject[] frags = new GameObject[6];
            Vector3[] fragVels = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                frags[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frags[i].name = $"DarkFrag_{i}";
                frags[i].transform.SetParent(arenaRoot.transform, false);
                frags[i].transform.position = pos;
                frags[i].transform.localScale = Vector3.one * 0.15f;
                Material fMat = CreateSafeMaterial(new Color(0.15f, 0.05f, 0.2f));
                frags[i].GetComponent<MeshRenderer>().material = fMat;
                Object.Destroy(frags[i].GetComponent<Collider>());
                fragVels[i] = new Vector3(Random.Range(-3f, 3f), Random.Range(1f, 3f), Random.Range(-3f, 3f));
            }

            float t = 0f;
            // Phase 1: contract (0~0.3s)
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = t / 0.3f;
                float scale = 2f - progress * 1.5f;
                orb.transform.localScale = Vector3.one * scale;
                orbMat.color = new Color(color.r, color.g, color.b, 0.6f + progress * 0.3f);
                yield return null;
            }

            // Phase 2: explode (0.3~0.8s)
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float progress = t / 0.5f;

                float scale = 0.5f + progress * 4f;
                orb.transform.localScale = Vector3.one * scale;
                orbMat.color = new Color(color.r, color.g, color.b, 0.9f * (1f - progress));

                for (int i = 0; i < 4; i++)
                {
                    if (cracks[i] == null) continue;
                    float crackLen = progress * 3f;
                    cracks[i].transform.localScale = new Vector3(crackLen, 0.03f, 0.03f);
                }

                for (int i = 0; i < 6; i++)
                {
                    if (frags[i] == null) continue;
                    frags[i].transform.position = pos + fragVels[i] * progress;
                    frags[i].transform.Rotate(Vector3.one, 500f * Time.deltaTime);
                    float fs = 0.15f * (1f - progress);
                    frags[i].transform.localScale = Vector3.one * Mathf.Max(fs, 0.01f);
                }
                yield return null;
            }
            Destroy(orb);
            foreach (var c in cracks) if (c != null) Destroy(c);
            foreach (var f in frags) if (f != null) Destroy(f);
        }

        private IEnumerator MetalImpact3D(Vector3 pos, Color color)
        {
            // X-slash (two rotating cubes)
            GameObject slash1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slash1.name = "MetalSlash1";
            slash1.transform.SetParent(arenaRoot.transform, false);
            slash1.transform.position = pos;
            slash1.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            slash1.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Material s1Mat = CreateSafeMaterial(color);
            slash1.GetComponent<MeshRenderer>().material = s1Mat;
            Object.Destroy(slash1.GetComponent<Collider>());

            GameObject slash2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slash2.name = "MetalSlash2";
            slash2.transform.SetParent(arenaRoot.transform, false);
            slash2.transform.position = pos;
            slash2.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            slash2.transform.rotation = Quaternion.Euler(0f, 0f, -45f);
            Material s2Mat = CreateSafeMaterial(new Color(0.9f, 0.9f, 0.95f));
            slash2.GetComponent<MeshRenderer>().material = s2Mat;
            Object.Destroy(slash2.GetComponent<Collider>());

            // Metal fragments
            GameObject[] frags = new GameObject[8];
            Vector3[] fragVels = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                frags[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frags[i].name = $"MetalFrag_{i}";
                frags[i].transform.SetParent(arenaRoot.transform, false);
                frags[i].transform.position = pos;
                frags[i].transform.localScale = Vector3.one * 0.1f;
                float shade = 0.6f + Random.Range(0f, 0.3f);
                Material fMat = CreateSafeMaterial(new Color(shade, shade, shade + 0.05f));
                frags[i].GetComponent<MeshRenderer>().material = fMat;
                Object.Destroy(frags[i].GetComponent<Collider>());
                fragVels[i] = new Vector3(Random.Range(-2f, 2f), Random.Range(0.5f, 3f), Random.Range(-2f, 2f));
            }

            // Spark (small orange spheres)
            GameObject[] sparks = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                sparks[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sparks[i].name = $"MetalSpark_{i}";
                sparks[i].transform.SetParent(arenaRoot.transform, false);
                sparks[i].transform.position = pos;
                sparks[i].transform.localScale = Vector3.one * 0.08f;
                Material spMat = CreateSafeMaterial(new Color(1f, 0.6f, 0.1f));
                sparks[i].GetComponent<MeshRenderer>().material = spMat;
                Object.Destroy(sparks[i].GetComponent<Collider>());
            }

            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float progress = t / 0.6f;

                // Slash grows
                float slashLen = progress < 0.3f ? progress / 0.3f * 3f : 3f;
                slash1.transform.localScale = new Vector3(slashLen, 0.04f, 0.04f);
                slash2.transform.localScale = new Vector3(slashLen, 0.04f, 0.04f);
                s1Mat.color = new Color(color.r, color.g, color.b, 1f - progress);
                s2Mat.color = new Color(0.9f, 0.9f, 0.95f, 1f - progress);

                // Fragments fly
                for (int i = 0; i < 8; i++)
                {
                    if (frags[i] == null) continue;
                    Vector3 fPos = pos + fragVels[i] * progress;
                    fPos.y -= 2f * progress * progress;
                    frags[i].transform.position = fPos;
                    frags[i].transform.Rotate(Vector3.one, 400f * Time.deltaTime);
                    float fs = 0.1f * (1f - progress * 0.7f);
                    frags[i].transform.localScale = Vector3.one * fs;
                }

                // Sparks
                for (int i = 0; i < 4; i++)
                {
                    if (sparks[i] == null) continue;
                    float sa = i * Mathf.PI * 0.5f + progress * 5f;
                    float sr = progress * 1.5f;
                    sparks[i].transform.position = pos + new Vector3(Mathf.Cos(sa) * sr, Mathf.Sin(progress * Mathf.PI) * 0.8f, Mathf.Sin(sa) * sr);
                    float ss = 0.08f * (1f - progress);
                    sparks[i].transform.localScale = Vector3.one * Mathf.Max(ss, 0.01f);
                }
                yield return null;
            }
            Destroy(slash1);
            Destroy(slash2);
            foreach (var f in frags) if (f != null) Destroy(f);
            foreach (var s in sparks) if (s != null) Destroy(s);
        }

        private IEnumerator DefaultImpact3D(Vector3 pos, Color color)
        {
            // Similar to existing CreateImpactEffect (ring + sparks)
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "DefaultImpactRing";
            ring.transform.SetParent(arenaRoot.transform, false);
            ring.transform.position = pos;
            ring.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
            Material ringMat = CreateSafeMaterial(new Color(color.r, color.g, color.b, 0.7f));
            ring.GetComponent<MeshRenderer>().material = ringMat;
            Object.Destroy(ring.GetComponent<Collider>());

            GameObject[] sparks = new GameObject[8];
            for (int i = 0; i < 8; i++)
            {
                sparks[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sparks[i].name = $"DefaultSpark_{i}";
                sparks[i].transform.SetParent(arenaRoot.transform, false);
                sparks[i].transform.position = pos;
                sparks[i].transform.localScale = Vector3.one * 0.12f;
                Material sMat = CreateSafeMaterial(new Color(color.r, color.g, color.b, 0.9f));
                sparks[i].GetComponent<MeshRenderer>().material = sMat;
                Object.Destroy(sparks[i].GetComponent<Collider>());
            }

            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float progress = t / 0.5f;
                float ringSize = 0.5f + progress * 4f;
                ring.transform.localScale = new Vector3(ringSize, 0.01f * (1f - progress), ringSize);
                ringMat.color = new Color(color.r, color.g, color.b, 0.7f * (1f - progress));

                for (int i = 0; i < 8; i++)
                {
                    if (sparks[i] == null) continue;
                    float a = i * Mathf.PI * 2f / 8f;
                    float dist = progress * 3f;
                    float sy = Mathf.Sin(progress * Mathf.PI) * 1.5f;
                    sparks[i].transform.position = pos + new Vector3(Mathf.Cos(a) * dist, sy, Mathf.Sin(a) * dist);
                    sparks[i].transform.localScale = Vector3.one * (0.12f * (1f - progress));
                }
                yield return null;
            }
            Destroy(ring);
            foreach (var s in sparks) if (s != null) Destroy(s);
        }

        // --- Buff / Debuff Effects ---

        private void PlayBuffEffect(bool onPlayer, InsectElement element)
        {
            GameObject target = onPlayer ? (teamModels != null && selectedTeamIndex >= 0 && selectedTeamIndex < teamModels.Length ? teamModels[selectedTeamIndex] : playerModel) : playerModel;
            if (target == null) return;
            StartCoroutine(BuffEffectCoroutine(target.transform.position, GetElementColor3D(element)));
        }

        private IEnumerator BuffEffectCoroutine(Vector3 pos, Color color)
        {
            GameObject[] rings = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                rings[i] = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rings[i].name = $"BuffRing_{i}";
                rings[i].transform.SetParent(arenaRoot.transform, false);
                rings[i].transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);
                Material mat = CreateTransparentMaterial(new Color(0.3f, 0.8f, 1f, 0.5f));
                rings[i].GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(rings[i].GetComponent<Collider>());
            }

            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "BuffArrow";
            arrow.transform.SetParent(arenaRoot.transform, false);
            arrow.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            arrow.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Material arrowMat = CreateSafeMaterial(new Color(0.2f, 1f, 0.4f));
            arrow.GetComponent<MeshRenderer>().material = arrowMat;
            Object.Destroy(arrow.GetComponent<Collider>());

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                float progress = t / 1f;
                for (int i = 0; i < 3; i++)
                {
                    float delay = i * 0.15f;
                    float ringProgress = Mathf.Clamp01((progress - delay) / (1f - delay));
                    float y = pos.y - 0.5f + ringProgress * 3f;
                    rings[i].transform.position = new Vector3(pos.x, y, pos.z);
                    float scale = 1.5f * (1f - ringProgress * 0.5f);
                    rings[i].transform.localScale = new Vector3(scale, 0.02f, scale);
                    var mat = rings[i].GetComponent<MeshRenderer>().material;
                    mat.color = new Color(0.3f, 0.8f, 1f, 0.5f * (1f - ringProgress));
                }
                arrow.transform.position = pos + new Vector3(0f, 1f + progress * 1.5f, 0f);
                arrowMat.color = new Color(0.2f, 1f, 0.4f, 1f - progress);
                yield return null;
            }
            foreach (var r in rings) if (r != null) Destroy(r);
            if (arrow != null) Destroy(arrow);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SfxType.BuffApply);
        }

        private void PlayDebuffEffect(bool onTarget, InsectElement element)
        {
            GameObject target = onTarget ? (bossModel ?? enemyModel) : playerModel;
            if (target == null) return;
            StartCoroutine(DebuffEffectCoroutine(target.transform.position));
        }

        private IEnumerator DebuffEffectCoroutine(Vector3 pos)
        {
            GameObject darkOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            darkOrb.name = "DebuffOrb";
            darkOrb.transform.SetParent(arenaRoot.transform, false);
            darkOrb.transform.position = pos + Vector3.up * 3f;
            darkOrb.transform.localScale = Vector3.one * 2f;
            Material orbMat = CreateTransparentMaterial(new Color(0.3f, 0.1f, 0.1f, 0.4f));
            darkOrb.GetComponent<MeshRenderer>().material = orbMat;
            Object.Destroy(darkOrb.GetComponent<Collider>());

            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "DebuffArrow";
            arrow.transform.SetParent(arenaRoot.transform, false);
            arrow.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            arrow.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Material arrowMat = CreateSafeMaterial(new Color(1f, 0.2f, 0.2f));
            arrow.GetComponent<MeshRenderer>().material = arrowMat;
            Object.Destroy(arrow.GetComponent<Collider>());

            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                float progress = t / 0.8f;
                float y = 3f - progress * 3f;
                darkOrb.transform.position = pos + Vector3.up * y;
                float scale = 2f - progress * 1.2f;
                darkOrb.transform.localScale = Vector3.one * scale;
                orbMat.color = new Color(0.3f, 0.1f, 0.1f, 0.4f + progress * 0.3f);

                arrow.transform.position = pos + new Vector3(0f, 2f - progress * 2.5f, 0f);
                arrowMat.color = new Color(1f, 0.2f, 0.2f, 1f - progress);
                yield return null;
            }
            Destroy(darkOrb);
            Destroy(arrow);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SfxType.DebuffApply);
        }

        // --- Skill Effect Helpers ---

        // UI 효과 텍스트 색상 (1v1 / 5v1 컨트롤러 공용)
        public static Color GetUIElementColor(InsectElement element)
        {
            switch (element)
            {
                case InsectElement.Bug:      return new Color(0.7f, 0.85f, 0.3f);
                case InsectElement.Metal:    return new Color(0.75f, 0.78f, 0.85f);
                case InsectElement.Earth:    return new Color(0.65f, 0.45f, 0.25f);
                case InsectElement.Leaf:     return new Color(0.35f, 0.8f, 0.35f);
                case InsectElement.Water:    return new Color(0.3f, 0.65f, 1f);
                case InsectElement.Wind:     return new Color(0.7f, 0.95f, 0.9f);
                case InsectElement.Light:    return new Color(1f, 0.95f, 0.55f);
                case InsectElement.Electric: return new Color(1f, 0.95f, 0.3f);
                case InsectElement.Dark:     return new Color(0.45f, 0.35f, 0.6f);
                case InsectElement.Poison:   return new Color(0.7f, 0.4f, 0.85f);
                default:                     return new Color(0.9f, 0.9f, 0.9f);
            }
        }

        private Color GetElementColor3D(InsectElement element)
        {
            switch (element)
            {
                case InsectElement.Poison: return new Color(0.6f, 0.2f, 0.8f);
                case InsectElement.Water: return new Color(0.2f, 0.5f, 1f);
                case InsectElement.Leaf: return new Color(0.2f, 0.85f, 0.3f);
                case InsectElement.Wind: return new Color(0.6f, 0.9f, 0.7f);
                case InsectElement.Electric: return new Color(1f, 0.95f, 0.2f);
                case InsectElement.Earth: return new Color(0.7f, 0.5f, 0.2f);
                case InsectElement.Light: return new Color(1f, 0.95f, 0.7f);
                case InsectElement.Dark: return new Color(0.3f, 0.1f, 0.4f);
                case InsectElement.Metal: return new Color(0.7f, 0.75f, 0.8f);
                default: return new Color(0.5f, 0.8f, 0.3f); // Bug
            }
        }

        private void CreateProjPart(GameObject parent, PrimitiveType type, Vector3 localPos, float uniformScale, Color color)
        {
            CreateProjPart(parent, type, localPos, Vector3.one * uniformScale, color);
        }

        private void CreateProjPart(GameObject parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = "ProjPart";
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            Material mat = (color.a < 1f) ? CreateTransparentMaterial(color) : CreateSafeMaterial(color);
            part.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(part.GetComponent<Collider>());
        }

        private void CreateTrailParticle(Vector3 pos, Color color, float size)
        {
            StartCoroutine(TrailParticleCoroutine(pos, color, size));
        }

        private IEnumerator TrailParticleCoroutine(Vector3 pos, Color color, float size)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            p.name = "Trail";
            p.transform.SetParent(arenaRoot.transform, false);
            p.transform.position = pos;
            p.transform.localScale = Vector3.one * size;
            Material mat = CreateTransparentMaterial(new Color(color.r, color.g, color.b, 0.5f));
            p.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(p.GetComponent<Collider>());

            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float s = size * (1f - t / 0.3f);
                p.transform.localScale = Vector3.one * s;
                mat.color = new Color(color.r, color.g, color.b, 0.5f * (1f - t / 0.3f));
                yield return null;
            }
            Destroy(p);
        }

        private static Material CreateTransparentMaterial(Color color)
        {
            Material mat = CreateSafeMaterial(color);
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            return mat;
        }

        // ===== End Skill Effect System =====

        // ===== Faint / Hit Flash / Floating Effect Text =====

        public class EffectTextEntry
        {
            public string text;
            public Color color;
            public float startTime;
            public float duration;
        }

        private readonly List<EffectTextEntry> activeEffectTexts = new List<EffectTextEntry>();
        private const int MaxConcurrentEffectTexts = 3;
        private GUIStyle effectTextStyle;
        private GUIStyle effectTextShadowStyle;

        // HitFlash/Faint 호출 시마다 GetComponentsInChildren 반복 방지용 캐시
        private readonly Dictionary<GameObject, MeshRenderer[]> rendererCache = new Dictionary<GameObject, MeshRenderer[]>();

        private MeshRenderer[] GetRenderersCached(GameObject model)
        {
            if (model == null) return new MeshRenderer[0];
            if (rendererCache.TryGetValue(model, out var cached))
            {
                if (cached != null && cached.Length > 0 && cached[0] != null)
                    return cached;
                rendererCache.Remove(model);
            }
            var fresh = model.GetComponentsInChildren<MeshRenderer>();
            rendererCache[model] = fresh;
            return fresh;
        }

        public IReadOnlyList<EffectTextEntry> GetActiveEffectTexts() => activeEffectTexts;

        public void PlayEffectText(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            // 가장 오래된 항목 제거 (최대 3개 유지)
            if (activeEffectTexts.Count >= MaxConcurrentEffectTexts)
                activeEffectTexts.RemoveAt(0);

            activeEffectTexts.Add(new EffectTextEntry
            {
                text = text,
                color = color,
                startTime = Time.time,
                duration = 1.0f
            });
        }

        private void OnGUI()
        {
            if (!isActive) return;
            if (activeEffectTexts.Count == 0) return;

            float now = Time.time;
            // 만료 항목 정리
            for (int i = activeEffectTexts.Count - 1; i >= 0; i--)
            {
                EffectTextEntry e = activeEffectTexts[i];
                if (e == null || now - e.startTime >= e.duration)
                    activeEffectTexts.RemoveAt(i);
            }

            if (activeEffectTexts.Count == 0) return;

            if (effectTextStyle == null)
            {
                effectTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 40,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                effectTextShadowStyle = new GUIStyle(effectTextStyle);
            }
            GUIStyle style = effectTextStyle;
            GUIStyle shadowStyle = effectTextShadowStyle;

            float centerX = Screen.width * 0.5f;
            float baseY = Screen.height * 0.35f;
            float stackSpacing = 56f;
            float width = 600f;
            float height = 50f;

            Color prevColor = GUI.color;
            for (int i = 0; i < activeEffectTexts.Count; i++)
            {
                EffectTextEntry e = activeEffectTexts[i];
                if (e == null) continue;

                float elapsed = now - e.startTime;
                float progress = Mathf.Clamp01(elapsed / e.duration);
                float alpha = 1f - progress;
                float yOffset = -40f * progress; // 위로 떠오름
                float y = baseY + i * stackSpacing + yOffset;
                Rect rect = new Rect(centerX - width * 0.5f, y, width, height);

                // Shadow
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.7f);
                Rect shadowRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
                GUI.Label(shadowRect, e.text, shadowStyle);

                // Main
                Color c = e.color;
                c.a *= alpha;
                GUI.color = c;
                GUI.Label(rect, e.text, style);
            }
            GUI.color = prevColor;
        }

        public IEnumerator PlayFaintCoroutine(GameObject model)
        {
            if (model == null) yield break;

            Vector3 originalPos = model.transform.position;
            Quaternion originalRot = model.transform.rotation;
            Vector3 targetPos = originalPos + new Vector3(0f, -0.2f, 0f);
            Quaternion targetRot = originalRot * Quaternion.Euler(0f, 0f, 90f);

            MeshRenderer[] renderers = GetRenderersCached(model);
            int count = renderers != null ? renderers.Length : 0;
            Color[] originalColors = new Color[count];
            bool[] hasBaseColor = new bool[count];
            Color[] originalBaseColors = new Color[count];

            for (int i = 0; i < count; i++)
            {
                if (renderers[i] == null) continue;
                Material mat = renderers[i].material; // 인스턴스
                // Standard/URP 셰이더를 Transparent(Fade) 모드로 전환해야 알파 페이드가 시각적으로 보임.
                SetMaterialTransparentFade(mat);
                originalColors[i] = mat.HasProperty("_Color") ? mat.color : Color.white;
                if (mat.HasProperty("_BaseColor"))
                {
                    hasBaseColor[i] = true;
                    originalBaseColors[i] = mat.GetColor("_BaseColor");
                }
            }

            float duration = 0.6f;
            float t = 0f;
            while (t < duration)
            {
                if (model == null) yield break;
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / duration);
                float eased = progress * progress * (3f - 2f * progress);

                model.transform.position = Vector3.Lerp(originalPos, targetPos, eased);
                model.transform.rotation = Quaternion.Slerp(originalRot, targetRot, eased);

                float alpha = 1f - progress;
                for (int i = 0; i < count; i++)
                {
                    if (renderers[i] == null) continue;
                    Material mat = renderers[i].material;
                    if (mat.HasProperty("_Color"))
                    {
                        Color c = originalColors[i];
                        c.a = originalColors[i].a * alpha;
                        mat.color = c;
                    }
                    if (hasBaseColor[i] && mat.HasProperty("_BaseColor"))
                    {
                        Color c = originalBaseColors[i];
                        c.a = originalBaseColors[i].a * alpha;
                        mat.SetColor("_BaseColor", c);
                    }
                }
                yield return null;
            }

            if (model != null) model.SetActive(false);
        }

        // Standard / URP Lit 머티리얼을 Transparent(Fade) 모드로 전환.
        // Opaque 모드에서는 알파 변경이 시각적으로 보이지 않으므로 페이드 전에 반드시 호출.
        private static void SetMaterialTransparentFade(Material mat)
        {
            if (mat == null) return;
            // Standard (Built-in)
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 2f); // Fade
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            // URP Lit
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); // Transparent
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f); // Alpha
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            mat.renderQueue = 3000;
        }

        public IEnumerator PlayHitFlashCoroutine(GameObject model)
        {
            if (model == null) yield break;

            Vector3 originalPos = model.transform.position;
            // 모델 정면 방향 기준 뒤로 0.3 백스텝 (forward의 반대)
            Vector3 backDir = -model.transform.forward;
            if (backDir.sqrMagnitude < 0.001f) backDir = Vector3.back;
            Vector3 backPos = originalPos + backDir.normalized * 0.3f;

            MeshRenderer[] renderers = GetRenderersCached(model);
            int count = renderers != null ? renderers.Length : 0;
            Color[] originalColors = new Color[count];
            bool[] hasBaseColor = new bool[count];
            Color[] originalBaseColors = new Color[count];

            for (int i = 0; i < count; i++)
            {
                if (renderers[i] == null) continue;
                Material mat = renderers[i].material;
                originalColors[i] = mat.HasProperty("_Color") ? mat.color : Color.white;
                if (mat.HasProperty("_BaseColor"))
                {
                    hasBaseColor[i] = true;
                    originalBaseColors[i] = mat.GetColor("_BaseColor");
                }
            }

            float duration = 0.15f;
            float t = 0f;
            while (t < duration)
            {
                if (model == null) yield break;
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / duration);
                // 0~0.5 백스텝, 0.5~1 복귀
                float backWeight = progress < 0.5f ? (progress / 0.5f) : (1f - (progress - 0.5f) / 0.5f);
                model.transform.position = Vector3.Lerp(originalPos, backPos, backWeight);

                // 빨강 추가: 0 → 0.5 → 0 (피크 0.5)
                float redWeight = Mathf.Sin(progress * Mathf.PI) * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    if (renderers[i] == null) continue;
                    Material mat = renderers[i].material;
                    if (mat.HasProperty("_Color"))
                    {
                        Color flashed = Color.Lerp(originalColors[i], Color.red, redWeight);
                        flashed.a = originalColors[i].a;
                        mat.color = flashed;
                    }
                    if (hasBaseColor[i] && mat.HasProperty("_BaseColor"))
                    {
                        Color flashed = Color.Lerp(originalBaseColors[i], Color.red, redWeight);
                        flashed.a = originalBaseColors[i].a;
                        mat.SetColor("_BaseColor", flashed);
                    }
                }
                yield return null;
            }

            // 원위치 / 원색 복원
            if (model == null) yield break;
            model.transform.position = originalPos;
            for (int i = 0; i < count; i++)
            {
                if (renderers[i] == null) continue;
                Material mat = renderers[i].material;
                if (mat.HasProperty("_Color")) mat.color = originalColors[i];
                if (hasBaseColor[i] && mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", originalBaseColors[i]);
            }
        }

        // ===== End Faint / Hit Flash / Floating Effect Text =====

        public void CleanupArena()
        {
            // 진행 중인 Faint/HitFlash/스킬 코루틴이 destroyed GameObject에 접근하지 않도록 우선 정지.
            StopAllCoroutines();
            isActive = false;
            if (arenaRoot != null)
            {
                Destroy(arenaRoot);
                arenaRoot = null;
            }
            playerModel = null;
            enemyModel = null;
            bossModel = null;
            teamModels = null;
            activeEffectTexts.Clear();
            rendererCache.Clear();
        }

        public void HighlightTeamMember(int index)
        {
            if (teamModels == null) return;
            for (int i = 0; i < teamModels.Length; i++)
            {
                if (teamModels[i] == null) continue;
                float targetScale = (i == index) ? 1.2f : 1.0f;
                teamModels[i].transform.localScale = Vector3.one * targetScale;
            }
        }

        public void SetModelDead(int teamIndex)
        {
            if (teamModels != null && teamIndex >= 0 && teamIndex < teamModels.Length && teamModels[teamIndex] != null)
            {
                teamModels[teamIndex].transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                teamModels[teamIndex].transform.localScale = Vector3.one * 0.5f;
            }
        }

        private static Material CreateSafeMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            mat.color = color;
            return mat;
        }
    }
}
