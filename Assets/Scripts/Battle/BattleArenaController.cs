using System.Collections;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Battle
{
    public partial class BattleArenaController : MonoBehaviour
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
        private float bossGroundY;

        private bool isActive;
        private float framingAspect;
        private float framingFov;
        private Rect framingSafeArea;
        private float PresentationDelta => BattlePresentation.DeltaTime;
        private CameraFollower cachedCameraFollower;

        private int selectedTeamIndex = -1;

        // 전투 모델 스케일 — 전투는 필드 rarity 배율(1.0~1.9) 미적용·고정 배율(등급 무관 동일 크기).
        // 곤충이 화면에서 과대해 하향(카메라·battlePos·FOV·아레나 지오메트리는 불변 = "모델만 축소").
        // 보스는 위압 비율 유지, 팀은 5마리 호 배치라 더 작게.
        private const float BattleInsectScale = 1.1f;   // 1v1 플레이어/적 (기존 1.5)
        // 레이드 카메라를 눈높이로 낮추고 뒤로 물리면서 두 대상 모두 멀어졌다 — 그만큼 키운다.
        private const float BossInsectScale = 2.3f;     // 레이드 보스 (기존 1.85)
        private const float TeamInsectScale = 1.0f;     // 레이드 팀원 (기존 0.8)

        // 스킬 연출 코루틴 진행 여부 — UI 페이즈 전이를 연출 길이에 맞춰 게이팅(BattleScreenUI.PhaseAnimDone).
        private bool playingSkill;
        public bool IsPlayingSkill => playingSkill;

        // `bossAttackTargetSlot` 필드와 `SetBossAttackTargetSlot`가 여기 있었다. 유일한 호출부
        // (`RaidBattleUI.TriggerBossAttackEffect`)가 5f0776f의 라운드 파이프라인 교체로 죽으면서
        // **쓰는 쪽 없이 읽히기만 하는 필드**가 됐다 — 초기값이 있어 컴파일 경고도 나지 않는다.
        // 대상 지정은 `PlayRaidBossAttack(element, isAoe, targetSlot, onComplete)`의 매개변수로 옮겨갔다.
        // 되살리지 말 것: 지금 호출해도 `PlayRaidBossAttack`의 인자 분기가 먼저 이겨 아무 효과가 없다
        // (호출부 0인 `RaidRoundModels.SetBossDamage`를 제거했던 것과 같은 함정).

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

            // 화면 왼쪽 아군 / 오른쪽 적. 깊이 차는 실루엣을 읽을 만큼만 둔다.
            playerBattlePos = arenaCenter + new Vector3(-2.1f, 0.5f, -0.35f);
            enemyBattlePos = arenaCenter + new Vector3(2.1f, 0.5f, 0.35f);

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
            ArrangeNormalCombatants();

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

            // 보스: 멀리 위에, 팀: 가까이 아래 — 올려보는 구도.
            // 간격을 7.5 → 5로 좁히고 보스를 1.2 → 2.2로 높였다. 카메라가 팀 뒤에서 볼 때
            // 둘의 **화면상 높이 차**가 줄어 팀과 보스가 함께 하단 스킬 패널 위쪽에 들어온다.
            // 예전 배치는 팀이 화면 아래 80% 부근에 떨어져 패널에 통째로 가려졌다.
            bossBattlePos = arenaCenter + new Vector3(0f, 2.2f, 3f);
            playerBattlePos = arenaCenter + new Vector3(0f, 0.5f, -2f);
            enemyBattlePos = bossBattlePos;

            arenaRoot = new GameObject("RaidArena");
            arenaRoot.transform.position = arenaCenter;

            CreateArenaFloor();
            CreateBattleLight();

            bossModel = CreateBattleInsect(bossInsect, bossLevel, bossShiny, bossBattlePos, BossInsectScale);
            bossModel.name = "RaidInsect_Boss";
            bossModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            CreateBossRise();
            CreateBossAura(bossModel.transform);

            int count = teamInsects != null ? teamInsects.Length : 0;
            teamModels = new GameObject[count];

            // 호 형태 배치: 카메라에서 봤을 때 겹치지 않게
            for (int i = 0; i < count; i++)
            {
                if (teamInsects[i] == null) continue;
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float x = Mathf.Lerp(-2.6f, 2.6f, t);
                // 호 중심은 playerBattlePos에서 파생한다 — 좌표를 두 곳에 적지 않는다.
                float z = playerBattlePos.z - arenaCenter.z + Mathf.Abs(t - 0.5f) * 1.2f;
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
            if (bossModel == null) ArrangeNormalCombatants();
            SetupBattleCamera();
        }

        private void ArrangeNormalCombatants()
        {
            if (playerModel == null || enemyModel == null) return;
            float half = BattleFraming.DuelHalfSeparation(BattleFraming.ModelBounds(playerModel),
                BattleFraming.ModelBounds(enemyModel));
            playerBattlePos.x = arenaCenter.x - half;
            enemyBattlePos.x = arenaCenter.x + half;
            playerModel.transform.position = playerBattlePos;
            enemyModel.transform.position = enemyBattlePos;
            playerModel.transform.LookAt(enemyBattlePos);
            enemyModel.transform.LookAt(playerBattlePos);
        }

        private void CreateArenaFloor()
        {
            ForestBattleSet.Build(arenaRoot.transform, CreateSafeMaterial, runtimeMeshes);
            arenaFloor = arenaRoot.transform.Find("ForestClearing").gameObject;

            // 아레나 경계벽 — 필드가 보이지 않도록 차단.
            // **카메라보다 멀리** 세운다: 전투 카메라는 1v1이 z≈-6.5, 레이드가 z≈-9.5로 아레나 뒤에
            // 서는데 예전 벽은 ±5에 있었다. 그래서 카메라가 남쪽 벽의 **바깥 면**을 정면으로 마주 봐
            // 화면이 통째로 벽에 막혔다 — 레이드에서 곤충이 하나도 보이지 않던 원인이다.
            // 벽을 옮길 때는 `RaidCamBackDistance`/1v1 카메라 오프셋보다 크게 유지할 것.
            Material wallMat = CreateSafeMaterial(new Color(0.105f, 0.17f, 0.16f));
            string[] wallNames = { "WallN", "WallS", "WallE", "WallW" };
            float span = ArenaWallSpan;
            float thick = 0.4f;
            float wallH = ArenaWallHeight;
            Vector3[] wallPositions = {
                new Vector3(0f, wallH * 0.5f, span),
                new Vector3(0f, wallH * 0.5f, -span),
                new Vector3(span, wallH * 0.5f, 0f),
                new Vector3(-span, wallH * 0.5f, 0f)
            };
            Vector3[] wallScales = {
                new Vector3(span * 2f, wallH, thick),
                new Vector3(span * 2f, wallH, thick),
                new Vector3(thick, wallH, span * 2f),
                new Vector3(thick, wallH, span * 2f)
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

        /// <summary>
        /// 아레나 경계벽까지의 거리. <b>전투 카메라가 이 안에 들어와야</b> 벽이 시야를 막지 않는다 —
        /// 레이드 카메라는 팀 뒤 <see cref="RaidCamBackDistance"/>에 서므로 그보다 넉넉히 크다.
        /// 공개인 이유는 <c>RaidCameraFramingTests</c>가 그 관계를 고정하기 때문이다.
        /// </summary>
        public const float ArenaWallSpan = 13f;
        public const float ArenaWallHeight = 10f;

        private void CreateBattleLight()
        {
            // Local lights cannot relight the exploration scene across the map.
            arenaLight = new GameObject("BattleLightRig");
            arenaLight.transform.SetParent(arenaRoot.transform, false);
            CreateArenaSpot("WarmKey", new Vector3(-3.5f, 7f, -4f), new Color(1f, .88f, .68f), 2.4f, 18f, 105f, true);
            CreateArenaSpot("CoolFill", new Vector3(4f, 5f, -1f), new Color(.62f, .77f, 1f), 1.25f, 17f, 100f, false);
            CreateArenaSpot("LeafRim", new Vector3(0f, 6f, 5f), new Color(.76f, .95f, .85f), 1.7f, 15f, 90f, false);
        }

        private void CreateArenaSpot(string name, Vector3 position, Color color, float intensity,
            float range, float angle, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(arenaLight.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, .65f, 0f) - position);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * .65f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = .6f;
            light.shadowBias = .035f;
        }

        private void CreateBossRise()
        {
            // The raised boss framing needs visible support under a grounded species.
            Bounds bounds = BattleFraming.ModelBounds(bossModel);
            float height = Mathf.Max(0.1f, bounds.min.y - arenaCenter.y);
            GameObject rise = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rise.name = "BossRise";
            rise.transform.SetParent(arenaRoot.transform, false);
            rise.transform.position = new Vector3(bossBattlePos.x, arenaCenter.y + height * 0.5f, bossBattlePos.z);
            rise.transform.localScale = new Vector3(Mathf.Max(3.2f, bounds.size.x + 0.65f), height * 0.5f,
                Mathf.Max(3.2f, bounds.size.z + 0.65f));
            rise.GetComponent<MeshRenderer>().sharedMaterial = CreateSafeMaterial(new Color(0.46f, 0.43f, 0.34f));
            Destroy(rise.GetComponent<Collider>());
            bossGroundY = arenaCenter.y + height + 0.035f;
        }

        private void CreateBossAura(Transform parent)
        {
            // An enclosing transparent sphere can render opaque in a player build
            // when its blending variant is unavailable, hiding the entire boss.
            // A ground marker remains readable with the ordinary opaque material.
            GameObject aura = new GameObject("BossAura");
            aura.transform.SetParent(parent, false);
            var ring = aura.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 48;
            ring.widthMultiplier = 0.035f;
            ring.generateLightingData = true;
            ring.sharedMaterial = CreateSafeMaterial(new Color(0.94f, 0.43f, 0.25f));
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            float ground = (Mathf.Max(arenaCenter.y + 0.055f, bossGroundY) - parent.position.y)
                / Mathf.Max(0.01f, parent.lossyScale.y);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.78f, ground, Mathf.Sin(angle) * 0.78f));
            }
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
                t += PresentationDelta;
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
                t += PresentationDelta;
                float progress = t / 0.3f;
                attacker.transform.position = Vector3.Lerp(contactPos, startPos, progress);
                yield return null;
            }

            attacker.transform.position = startPos;
        }

        private IEnumerator ShakeModel(GameObject model, float duration, float magnitude)
        {
            if (BattlePresentation.ReducedMotion || model == null) yield break;
            Vector3 original = model.transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += PresentationDelta;
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
                t += PresentationDelta;
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

        private void CreateBigExplosion(Vector3 position)
        {
            StartCoroutine(BigExplosionCoroutine(position));
        }

        private IEnumerator BigExplosionCoroutine(Vector3 position)
        {
            if (BattlePresentation.ReducedFlashes) yield break;
            // 가산 FX — 예전 불투명 노란 공은 보스를 0.8초간 통째로 가렸다(합체공격 QA 캡처).
            Material flashMat = CreateFxMaterial(new Color(1f, 0.9f, 0.3f, 0.9f), true);
            GameObject flash = FxPrimitive(PrimitiveType.Sphere, "Explosion", position, flashMat);

            float t = 0f;
            while (t < 0.8f)
            {
                t += PresentationDelta;
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
            Rect safe = Screen.safeArea;
            Rect safeViewport = safe.width > 0f && safe.height > 0f
                ? new Rect(safe.x / Mathf.Max(1, Screen.width), safe.y / Mathf.Max(1, Screen.height),
                    safe.width / Mathf.Max(1, Screen.width), safe.height / Mathf.Max(1, Screen.height))
                : new Rect(0f, 0f, 1f, 1f);
            Vector3 camPos;
            Vector3 lookTarget;

            if (isRaid)
            {
                // 레이드: 팀 뒤쪽 높은 곳에서 보스를 **정면으로** 본다.
                // 좌표는 실제 배치(팀 호 중심·보스 위치)에서 파생한다 — 값을 두 곳에 적지 않는다.
                ComputeRaidCameraFraming(playerBattlePos, bossBattlePos, out camPos, out lookTarget);
                Quaternion rotation = Quaternion.LookRotation(lookTarget - camPos);
                Bounds bounds = BattleFraming.ModelBounds(bossModel);
                if (teamModels != null)
                    foreach (GameObject model in teamModels)
                        if (model != null) bounds.Encapsulate(BattleFraming.ModelBounds(model));
                bounds.Expand(0.3f);
                BattleFraming.Compute(bounds, cam.aspect, cam.fieldOfView, rotation, safeViewport, out camPos, out lookTarget);
            }
            else
            {
                Bounds bounds = BattleFraming.ModelBounds(playerModel);
                bounds.Encapsulate(BattleFraming.ModelBounds(enemyModel));
                bounds.Expand(0.3f);
                BattleFraming.ComputeDuel(bounds, cam.aspect, cam.fieldOfView, Quaternion.Euler(12f, -12f, 0f),
                    safeViewport, out camPos, out lookTarget);
            }
            // Large wings on narrow screens may require more distance than the original arena.
            // Move the backdrop with the framing instead of placing the camera behind a wall.
            float span = Mathf.Max(ArenaWallSpan, Mathf.Abs(camPos.x - arenaCenter.x) + 3f,
                Mathf.Abs(camPos.z - arenaCenter.z) + 3f);
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
            framingAspect = cam.aspect;
            framingFov = cam.fieldOfView;
            framingSafeArea = Screen.safeArea;
            RememberBaseCamera(camPos, lookTarget);   // 연출 샷은 이 구도에서 출발해 여기로 돌아온다

            cam.transform.position = camPos;
            cam.transform.LookAt(lookTarget);

            CameraFollower follower = cam.GetComponent<CameraFollower>();
            if (follower != null) follower.EnterBattleModeFramed(camPos, lookTarget);
        }

        private void Update()
        {
            if (!isActive || playingSkill || Camera.main == null) return;
            if (Mathf.Abs(Camera.main.aspect - framingAspect) > 0.01f
                || Mathf.Abs(Camera.main.fieldOfView - framingFov) > 0.01f || Screen.safeArea != framingSafeArea) SetupBattleCamera();
        }

        public Vector3 GetCombatantScreenPosition(bool player)
        {
            return ScreenCenterOf(player ? playerModel : (bossModel != null ? bossModel : enemyModel));
        }

        /// <summary>
        /// 모델 중심의 화면 좌표. 렌더러 목록은 캐시를 쓴다 — UI가 OnGUI 패스마다 부르므로
        /// <c>GetComponentsInChildren</c>를 매번 돌리면 프레임마다 배열이 새로 생긴다.
        /// </summary>
        private Vector3 ScreenCenterOf(GameObject model)
        {
            if (Camera.main == null || model == null) return Vector3.zero;
            MeshRenderer[] renderers = GetRenderersCached(model);
            Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
            bool found = false;
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || renderer.name == "BossAura") continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return Camera.main.WorldToScreenPoint(bounds.center);
        }

        // 레이드 카메라 구도 — 눈으로 보고 조정하는 값이다.
        // 보스를 더 크게/작게 보고 싶으면 RaidCamBackDistance만 움직이면 된다.
        //
        // 높이를 4.6 → 1.1로 낮춘 이유: 카메라가 팀보다 4.6m 위에서 22도로 내려다보면
        // 가까운 팀은 화면 아래 80% 부근, 먼 보스는 60% 부근에 떨어진다. 하단 스킬 패널이
        // 세로에서 화면의 1/3을 덮으므로 팀 5마리가 통째로 그 뒤에 숨었다. 눈높이에 가깝게
        // 낮추고 뒤로 물리면 팀과 보스의 화면상 높이 차가 6도 안으로 좁혀져 둘 다 패널 위에 남는다.
        // (거리는 `ArenaWallSpan`보다 작아야 한다 — 넘으면 카메라가 경계벽 밖으로 나간다.)
        private const float RaidCamBackDistance = 7.5f;   // 팀 호 중심에서 뒤로
        private const float RaidCamHeight = 1.1f;         // 팀 발치 기준 높이
        private const float RaidCamLookBias = 0.5f;       // 시선 지점(팀→보스 보간). 클수록 보스가 화면 위로
        private const float RaidCamLookLift = 0f;         // 시선 지점 높이 보정(팀↔보스 보간값 그대로)

        /// <summary>
        /// 팀 뒤 위쪽에서 보스를 정면으로 보는 카메라 구도. 화면 상단-중앙에 보스,
        /// 하단 전경에 팀 뒷모습이 오도록 팀→보스 축 <b>뒤쪽</b>에 카메라를 놓는다.
        /// 순수 계산이라 <c>RaidCameraFramingTests</c>가 측면 구도 회귀를 고정한다.
        /// </summary>
        public static void ComputeRaidCameraFraming(
            Vector3 teamPos, Vector3 bossPos, out Vector3 camPos, out Vector3 lookTarget)
        {
            Vector3 axis = bossPos - teamPos;
            axis.y = 0f;   // 수평 성분만 — 보스가 팀보다 높이 서 있어도 카메라가 기울지 않게
            axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;

            camPos = teamPos - axis * RaidCamBackDistance + Vector3.up * RaidCamHeight;
            lookTarget = Vector3.Lerp(teamPos, bossPos, RaidCamLookBias) + Vector3.up * RaidCamLookLift;
        }

        // ===== 3D Skill Effect System =====

        // 근접/원거리 판정 — 물리·근거리 속성(흙/금속/벌레/무속성)은 러시, 나머지 투사체 속성은 원거리 발사.
        // 스킬 데이터에 별도 필드 없이 element로 파생(데이터 무변경). BattleScreenUI/RaidBattleUI가 호출.
        public static bool IsMeleeElement(InsectElement element)
        {
            switch (element)
            {
                case InsectElement.Earth:
                case InsectElement.Metal:
                case InsectElement.Bug:
                case InsectElement.None:
                    return true;
                default:
                    return false;
            }
        }

        public void PlaySkillEffect(bool isPlayerAttacking, InsectElement element, SkillEffectType effectType, System.Action onImpact = null)
        {
            PlaySkillEffect(isPlayerAttacking, element, effectType, onImpact, false);
        }

        public void PlaySkillEffect(bool isPlayerAttacking, InsectElement element, SkillEffectType effectType, System.Action onImpact, bool isMelee, float duration = 2.5f)
        {
            PlaySkillEffect(isPlayerAttacking, element, effectType, onImpact, isMelee, duration, HitCue.None);
        }

        /// <param name="cue">외칠 기술 이름과 타격 세기 — 히트스톱·넉백·카메라 반동을 결과에 맞춘다.</param>
        public void PlaySkillEffect(bool isPlayerAttacking, InsectElement element, SkillEffectType effectType, System.Action onImpact, bool isMelee, float duration, HitCue cue)
        {
            if (!isActive) { onImpact?.Invoke(); return; }

            if (playingSkill) return;
            playingSkill = true;
            StartCoroutine(SkillAttackCoroutine(isPlayerAttacking, element, effectType, onImpact, isMelee, duration, cue));
        }

        // 레이드 보스 공격 대상 팀 모델의 **폴백** — 첫 유효 팀 모델.
        // 피격 슬롯 지정은 호출부가 `PlayRaidBossAttack`의 targetSlot 인자로 넘기고 그쪽이 먼저 처리한다.
        // 여기까지 오는 건 그 인자가 범위 밖이거나 해당 모델이 이미 파괴된 경우뿐이다.
        private GameObject ResolveBossTarget()
        {
            if (teamModels == null) return null;
            for (int i = 0; i < teamModels.Length; i++)
                if (teamModels[i] != null) return teamModels[i];
            return null;
        }

        private IEnumerator SkillAttackCoroutine(bool isPlayerAttacking, InsectElement element,
            SkillEffectType effectType, System.Action onImpact, bool isMelee, float duration, HitCue cue)
        {
            GameObject attacker = isPlayerAttacking ? playerModel : (bossModel != null ? bossModel : enemyModel);
            if (isPlayerAttacking && teamModels != null && selectedTeamIndex >= 0 && selectedTeamIndex < teamModels.Length)
                attacker = teamModels[selectedTeamIndex];
            bool self = effectType == SkillEffectType.BuffAttack || effectType == SkillEffectType.DefenseBuff
                || effectType == SkillEffectType.Heal;
            bool support = self || effectType == SkillEffectType.DebuffAttack;
            GameObject target = self ? attacker : isPlayerAttacking ? (bossModel ?? enemyModel)
                : (bossModel != null ? ResolveBossTarget() : playerModel);
            if (!isActive || attacker == null || target == null) { playingSkill = false; yield break; }

            duration = Mathf.Max(.5f, duration);
            Vector3 start = attacker.transform.position;
            Quaternion startRotation = attacker.transform.rotation;
            Vector3 destination = target.transform.position;
            Vector3 contact = Vector3.Lerp(start, destination, .72f);
            InsectEntity entity = attacker.GetComponent<InsectEntity>();
            BattleMotion.Kind motion = BattleMotion.Resolve(entity != null && entity.Data != null ? entity.Data.insectId : null, isMelee, support);
            Color color = GetElementColor3D(element);
            GameObject projectile = null;
            bool impacted = false;
            float elapsed = 0f;
            // 샷의 기준점은 출발 때 한 번 잰다(돌진하는 시전자를 쫓으면 카메라가 덜컹거린다).
            Vector3 attackerCenter = BattleFraming.ModelBounds(attacker).center;
            Vector3 targetCenter = BattleFraming.ModelBounds(target).center;
            BeginSkillPresentation(attacker, element, cue);
            try
            {
                while (elapsed < duration)
                {
                    if (!isActive || arenaRoot == null || attacker == null || target == null) yield break;
                    elapsed += BattlePresentation.DeltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    ApplyCameraShot(progress, duration, attackerCenter, targetCenter, cue.Weight, support);
                    BattleMotion.Pose pose = BattleMotion.Evaluate(motion, progress);
                    float motionScale = BattlePresentation.ReducedMotion ? .18f : 1f;
                    attacker.transform.position = Vector3.LerpUnclamped(start, contact, pose.Travel * motionScale)
                        + Vector3.up * (pose.Lift * motionScale);
                    attacker.transform.rotation = startRotation * Quaternion.Euler(pose.Pitch * motionScale, 0f, pose.Roll * motionScale);

                    // Projectile travel is independent of the caster's anticipation/recoil.
                    if (motion == BattleMotion.Kind.Projectile && !impacted && progress >= .18f && progress < BattleMotion.ImpactProgress)
                    {
                        float travel = Mathf.SmoothStep(0f, 1f, (progress - .18f) / (BattleMotion.ImpactProgress - .18f));
                        if (projectile == null) projectile = CreateElementProjectile(element, color);
                        projectile.transform.position = Vector3.Lerp(start + Vector3.up * .3f,
                            destination + Vector3.up * .3f, travel) + GetElementTrajectoryOffset(element, travel);
                    }
                    if (!impacted && progress >= BattleMotion.ImpactProgress)
                    {
                        impacted = true;
                        if (projectile != null) { Destroy(projectile); projectile = null; }
                        onImpact?.Invoke();
                        // An impact handler may close the battle; never emit orphan effects.
                        if (!isActive || arenaRoot == null || attacker == null || target == null) yield break;
                        if (support)
                        {
                            if (self) PlayBuffEffect(isPlayerAttacking, element);
                            else PlayDebuffEffect(!isPlayerAttacking, element);
                        }
                        else
                        {
                            if (!cue.Missed) CreateElementImpact3D(destination, element, color);
                            // 넉백·섬광·히트스톱·의성어·비명·흔들림 — 예전 ShakeModel(0.12m)+카메라 0.12를 대신한다.
                            PlayImpactFeel(attacker, target, targetCenter, element, color, cue);
                        }
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySkillSFX(element);
                    }
                    yield return null;
                }
            }
            finally
            {
                if (projectile != null) Destroy(projectile);
                if (attacker != null) attacker.transform.SetPositionAndRotation(start, startRotation);
                ClearCameraShot();
                playingSkill = false;
            }
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
            if (BattlePresentation.ReducedFlashes) return;
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
                t += PresentationDelta;
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
                t += PresentationDelta;
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
                t += PresentationDelta;
                float progress = t / 0.7f;
                for (int i = 0; i < 6; i++)
                {
                    if (leaves[i] == null) continue;
                    float dist = progress * 2.5f;
                    float y = Mathf.Sin(progress * Mathf.PI) * 1.5f;
                    leaves[i].transform.position = pos + new Vector3(Mathf.Cos(angles[i]) * dist, y, Mathf.Sin(angles[i]) * dist);
                    leaves[i].transform.Rotate(Vector3.up, 360f * PresentationDelta);
                    leaves[i].transform.Rotate(Vector3.right, 180f * PresentationDelta);
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
                t += PresentationDelta;
                float progress = t / 0.7f;
                for (int i = 0; i < 3; i++)
                {
                    if (rings[i] == null) continue;
                    float expand = (0.5f + i * 0.3f) + progress * 2f;
                    rings[i].transform.localScale = new Vector3(expand, 0.03f, expand);
                    rings[i].transform.Rotate(Vector3.up, (300f + i * 100f) * PresentationDelta);
                    float y = pos.y + i * 0.3f + progress * 1.5f;
                    rings[i].transform.position = new Vector3(pos.x, y, pos.z);
                    var mat = rings[i].GetComponent<MeshRenderer>().material;
                    mat.color = new Color(color.r, color.g, color.b, (0.4f - i * 0.1f) * (1f - progress));
                }
                for (int i = 0; i < 5; i++)
                {
                    if (debris[i] == null) continue;
                    debrisAngles[i] += 8f * PresentationDelta;
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
            Material flashMat = CreateFxMaterial(new Color(1f, 1f, 0.8f), true);
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
                Material bMat = CreateFxMaterial(color, true);
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
                Material sMat = CreateFxMaterial(new Color(1f, 1f, 0.5f), true);
                sparks[i].GetComponent<MeshRenderer>().material = sMat;
                Object.Destroy(sparks[i].GetComponent<Collider>());
                sparkVels[i] = new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 4f), Random.Range(-3f, 3f));
            }

            float t = 0f;
            while (t < 0.6f)
            {
                t += PresentationDelta;
                float progress = t / 0.6f;

                // Flash expands then fades
                float flashScale = 0.5f + progress * 1.8f;   // 가산 섬광은 3.5배까지 키우면 대상을 통째로 덮는다(QA 캡처)
                flash.transform.localScale = Vector3.one * flashScale;
                flashMat.color = new Color(1f, 1f, 0.8f, 0.85f * (1f - progress) * (1f - progress));

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
                t += PresentationDelta;
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
            Material crossMat = CreateFxMaterial(new Color(1f, 0.95f, 0.7f), true);
            crossH.GetComponent<MeshRenderer>().material = crossMat;
            Object.Destroy(crossH.GetComponent<Collider>());

            GameObject crossV = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossV.name = "LightCrossV";
            crossV.transform.SetParent(arenaRoot.transform, false);
            crossV.transform.position = pos;
            crossV.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            Material crossVMat = CreateFxMaterial(new Color(1f, 0.95f, 0.7f), true);
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
                Material sMat = CreateFxMaterial(new Color(1f, 1f, 0.6f), true);
                stars[i].GetComponent<MeshRenderer>().material = sMat;
                Object.Destroy(stars[i].GetComponent<Collider>());
                starAngles[i] = i * Mathf.PI * 2f / 6f;
            }

            float t = 0f;
            while (t < 0.8f)
            {
                t += PresentationDelta;
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
                t += PresentationDelta;
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
                t += PresentationDelta;
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
                    frags[i].transform.Rotate(Vector3.one, 500f * PresentationDelta);
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
            Material s1Mat = CreateFxMaterial(color, true);
            slash1.GetComponent<MeshRenderer>().material = s1Mat;
            Object.Destroy(slash1.GetComponent<Collider>());

            GameObject slash2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slash2.name = "MetalSlash2";
            slash2.transform.SetParent(arenaRoot.transform, false);
            slash2.transform.position = pos;
            slash2.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            slash2.transform.rotation = Quaternion.Euler(0f, 0f, -45f);
            Material s2Mat = CreateFxMaterial(new Color(0.9f, 0.9f, 0.95f), true);
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
                Material spMat = CreateFxMaterial(new Color(1f, 0.6f, 0.1f), true);
                sparks[i].GetComponent<MeshRenderer>().material = spMat;
                Object.Destroy(sparks[i].GetComponent<Collider>());
            }

            float t = 0f;
            while (t < 0.6f)
            {
                t += PresentationDelta;
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
                    frags[i].transform.Rotate(Vector3.one, 400f * PresentationDelta);
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
            // 바닥을 따라 퍼지는 충격 고리 + 튀는 파편. 예전엔 납작한 실린더(속이 찬 원판)를 **몸통 높이**에
            // 띄워 키웠는데, 불투명 머티리얼이라 페이드도 안 먹어서 반경 4.5m짜리 초록 접시가 0.5초간
            // 두 곤충을 덮었다(QA 캡처 실측). 고리는 선이라 가운데가 비고, 가산 합성이라 사라지며 빛난다.
            Vector3 ground = new Vector3(pos.x, arenaCenter.y + 0.06f, pos.z);
            Material ringMat = CreateFxMaterial(new Color(color.r, color.g, color.b, 0.9f), true);
            GameObject ringObj = new GameObject("DefaultImpactRing");
            ringObj.transform.SetParent(arenaRoot.transform, false);
            ringObj.transform.position = ground;
            LineRenderer ring = ringObj.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 40;
            ring.sharedMaterial = ringMat;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.alignment = LineAlignment.TransformZ;
            ringObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 선 면을 바닥에 눕힌다

            Material sMat = CreateFxMaterial(new Color(color.r, color.g, color.b, 0.9f), true);
            GameObject[] sparks = new GameObject[8];
            for (int i = 0; i < 8; i++)
            {
                sparks[i] = FxPrimitive(PrimitiveType.Sphere, $"DefaultSpark_{i}", pos, sMat);
                sparks[i].transform.localScale = Vector3.one * 0.12f;
            }

            float t = 0f;
            while (t < 0.5f)
            {
                if (arenaRoot == null) yield break;
                t += PresentationDelta;
                float progress = Mathf.Clamp01(t / 0.5f);
                float eased = 1f - (1f - progress) * (1f - progress);
                float radius = Mathf.Lerp(0.3f, 2.2f, eased);
                ring.widthMultiplier = Mathf.Lerp(0.16f, 0.02f, progress);
                ringMat.color = new Color(color.r, color.g, color.b, 0.9f * (1f - progress));
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float a = i * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
                }

                sMat.color = new Color(color.r, color.g, color.b, 0.9f * (1f - progress));
                for (int i = 0; i < 8; i++)
                {
                    if (sparks[i] == null) continue;
                    float a = i * Mathf.PI * 2f / 8f;
                    float dist = eased * 1.6f;
                    float sy = Mathf.Sin(progress * Mathf.PI) * 0.9f;
                    sparks[i].transform.position = pos + new Vector3(Mathf.Cos(a) * dist, sy, Mathf.Sin(a) * dist);
                    sparks[i].transform.localScale = Vector3.one * (0.12f * (1f - progress));
                }
                yield return null;
            }
            Destroy(ringObj);
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
                t += PresentationDelta;
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
                t += PresentationDelta;
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
                t += PresentationDelta;
                float s = size * (1f - t / 0.3f);
                p.transform.localScale = Vector3.one * s;
                mat.color = new Color(color.r, color.g, color.b, 0.5f * (1f - t / 0.3f));
                yield return null;
            }
            Destroy(p);
        }

        /// <summary>
        /// 반투명 이펙트 머티리얼. 예전엔 Standard를 Fade 모드로 바꿔 썼는데 **플레이어 빌드에선 불투명으로
        /// 그려졌다** — 물 기둥이 파란 원기둥, 어둠 구체가 보라 공, 독 구름이 보라 구슬로 대상을 통째로
        /// 가렸다(QA 캡처 실측, 속성 10종). 빌드에 항상 있는 FX 셰이더의 알파 블렌드로 바꾼다.
        /// </summary>
        private Material CreateTransparentMaterial(Color color)
        {
            return CreateFxMaterial(color, false);
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

        /// <summary>
        /// 지금 떠 있는 문구. 만료 항목은 <b>여기서</b> 걷어낸다 — 그리는 쪽(<c>BattleEffectTextOverlay</c>)이
        /// 목록을 수정할 수 없고 이 컴포넌트엔 <c>Update</c>가 없기 때문이다. 호출은 프레임당 한 번
        /// (오버레이 그리기)이고 항목은 최대 <see cref="MaxConcurrentEffectTexts"/>개다.
        /// </summary>
        public IReadOnlyList<EffectTextEntry> GetActiveEffectTexts()
        {
            float now = Time.time;
            for (int i = activeEffectTexts.Count - 1; i >= 0; i--)
            {
                EffectTextEntry e = activeEffectTexts[i];
                if (e == null || now - e.startTime >= e.duration)
                    activeEffectTexts.RemoveAt(i);
            }
            return activeEffectTexts;
        }

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

        // 이 자리에 `OnGUI`가 있었다. 아레나는 3D 연출을 맡는 컴포넌트인데 화면 문구까지 직접
        // 그리고 있었고, 거긴 `UIScale` 밖이라 **픽셀 좌표**였다 — 스케일이 1이 아닌 기기에서
        // 이 문구만 다른 UI보다 25% 작게 찍혔다. 그리기는 `BattleEffectTextOverlay`(UI)로 옮겼다.
        // 여기서 부를 수는 없다: UI가 이미 Battle을 참조하므로 반대 방향은 순환이 된다.

        public IEnumerator PlayFaintCoroutine(GameObject model)
        {
            if (model == null) yield break;
            AnnounceFaint(model);

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
                t += PresentationDelta;
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
                t += PresentationDelta;
                float progress = Mathf.Clamp01(t / duration);
                // 0~0.5 백스텝, 0.5~1 복귀
                float backWeight = progress < 0.5f ? (progress / 0.5f) : (1f - (progress - 0.5f) / 0.5f);
                model.transform.position = BattlePresentation.ReducedMotion ? originalPos : Vector3.Lerp(originalPos, backPos, backWeight);

                // 빨강 추가: 0 → 0.5 → 0 (피크 0.5)
                float redWeight = BattlePresentation.ReducedFlashes ? 0f : Mathf.Sin(progress * Mathf.PI) * 0.5f;
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
            // StopAllCoroutines가 SkillAttackCoroutine을 종료점 도달 전에 죽이면 playingSkill이 true로
            // 고착 → 다음 배틀 PhaseAnimDone이 2s 상한까지 지연. 강제 리셋으로 차단.
            playingSkill = false;
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
            ClearImpactState();
            rendererCache.Clear();

            // 이 전투가 만든 머티리얼 일괄 파기 — GameObject를 지워도 머티리얼은 남는다.
            for (int i = 0; i < runtimeMaterials.Count; i++)
                if (runtimeMaterials[i] != null) Destroy(runtimeMaterials[i]);
            runtimeMaterials.Clear();
            for (int i = 0; i < runtimeMeshes.Count; i++)
                if (runtimeMeshes[i] != null) Destroy(runtimeMeshes[i]);
            runtimeMeshes.Clear();
            arenaFloor = null;
            arenaLight = null;
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

        /// <summary>
        /// 이 전투가 만든 런타임 머티리얼. <b><c>Destroy(gameObject)</c>는 머티리얼을 지우지 않는다</b> —
        /// 같은 저장소가 <c>OutfitShapeLibrary.TrimContainer</c>에서 GameObject보다 머티리얼을 먼저 파괴하고
        /// <c>PlayerVisualBuilder.OnDestroy</c>·<c>NpcVisualBuilder.CleanupMaterials</c>를 두는 이유다.
        ///
        /// 여기선 <b>스킬 연출마다</b> 이펙트 오브젝트(링·스파크·구름·기둥…)가 새로 나고 그때마다
        /// 머티리얼도 새로 난다. 오브젝트는 코루틴이 지우지만 머티리얼은 아무도 안 지워서, 전투가 길수록
        /// 계속 쌓였다. 전투 종료에 한 번에 정리해 누수를 <b>전투 1회 수명</b>으로 가둔다.
        /// </summary>
        private readonly List<Material> runtimeMaterials = new List<Material>();
        private readonly List<Mesh> runtimeMeshes = new List<Mesh>();

        private Material CreateSafeMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            mat.color = color;
            runtimeMaterials.Add(mat);   // CleanupArena가 일괄 파기 — 안 하면 연출마다 샌다
            return mat;
        }
    }
}
