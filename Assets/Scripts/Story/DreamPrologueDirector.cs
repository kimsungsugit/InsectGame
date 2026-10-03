using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 「챔피언의 꿈」 — 새 계정이 처음 필드에 서면 한 번 도는 프롤로그(설정에서 다시 볼 수 있다).
    ///
    /// <list type="number">
    /// <item><b>챔피언전</b>: 최상급 전설 곤충으로 실제 전투 화면에서 싸운다. 규칙은 <see cref="SandboxBattleRules"/>
    /// — 반드시 이기고 3~4턴에 끝난다.</item>
    /// <item><b>챔피언의 섬</b>: 꾸며진 섬(방문 모드의 스냅샷)을 직접 걸으며 이동·곤충 살펴보기를 익힌다.</item>
    /// <item><b>깨어남</b>: 섬이 흐려지고 마을 어르신의 목소리에 깬다 — 꿈이었다. 곤충 소리 없는 초원이 본편의 시작이다.</item>
    /// </list>
    ///
    /// <b>꿈 밖의 상태는 하나도 바뀌지 않는다.</b> 그 약속은 <see cref="DreamPrologueState.Active"/> 하나로 지킨다 —
    /// 퀘스트 진행·필드 HUD·자동 주행·섬 HUD가 그 표지를 읽고 물러난다. 전투는 샌드박스라 보상·도감·저장이 없다.
    /// 끝나는 길이 여럿이라(끝까지·건너뛰기·오류·씬 재로드) 정리는 <see cref="Finish"/> 한 곳에 모으고
    /// OnDisable/OnDestroy도 같은 곳을 지난다 — 표지가 켜진 채 남으면 그 세션의 퀘스트가 영영 안 올라간다.
    /// </summary>
    public class DreamPrologueDirector : MonoBehaviour
    {
        private enum Phase { Idle, Opening, Battle, ToIsland, Island, ToWake, Wake }
        private enum IslandStep { Move, Approach, Fountain, Linger }

        // ── 시간표(초) ──
        // 영상이 못 뜰 때의 예전 도입: 페이드 → 글자 카드. 영상 뒤에는 이미 검어서 카드만 짧게 뜬다.
        private const float OpenFadeSeconds = 0.8f;
        private const float OpenTitleSeconds = 2.8f;
        private const float IntroCardSeconds = 2.4f;
        private const float IntroPrepFadeSeconds = 0.5f;
        private const float BattleRevealSeconds = 1.0f;
        private const float FlashSeconds = 0.5f;
        private const float IslandRevealSeconds = 1.1f;
        private const float IslandTitleSeconds = 3.2f;
        private const float WakeFadeOutSeconds = 1.0f;
        private const float WakeFadeInSeconds = 1.8f;
        private const float CaptionSeconds = 2.7f;
        private const float InsectCardSeconds = 4.2f;
        private const float LingerSeconds = 2.8f;
        // 어디서 막혀도 꿈이 끝나게 하는 상한들.
        private const float BattleStartTimeoutSeconds = 3f;
        private const float BattleEndTimeoutSeconds = 14f;
        private const float IslandTimeoutSeconds = 180f;

        // ── 걸음 기준 ──
        private const float MoveGoalMeters = 8f;
        private const float ApproachMeters = 3.2f;
        private const float FountainMeters = 2.6f;
        private const float TeleportMeters = 5f;   // 한 프레임에 이만큼 움직였으면 걸음이 아니라 순간이동이다

        private static readonly (string speaker, string text)[] WakeCaptions =
        {
            ("마을 어르신", "이봐… 일어나게나."),
            ("", "꿈이었나."),
            ("", "그 많던 곤충들이… 하나도 보이지 않는다."),
        };

        private TutorialQuestManager questManager;
        private PlayerMovement playerMovement;
        private Transform playerTransform;
        private RegionManager regionManager;
        private IslandWorldBuilder islandWorld;
        private InsectBattleController battleController;
        private BattleScreenUI battleScreen;
        private InsectDatabase database;
        private FieldMomentFeed momentFeed;

        private Phase phase = Phase.Idle;
        private float phaseTime;
        private float stableTimer;
        private bool battleStarted;
        private bool battleEnded;
        private bool subscribed;
        private int battleTurns;
        private float battleEndedAt;
        private string championName = string.Empty;
        private string challengerName = string.Empty;
        // OnGUI 패스마다 문자열을 새로 만들지 않는다 — 값이 바뀔 때만 다시 만든다.
        private string versusLine = string.Empty;
        private int shownMeters = -1;
        private string meterLabel = string.Empty;

        // 페이드(전체 화면 한 장). 색은 검정(꿈으로 들어갈 때)·흰색(장면 전환·깨어남).
        private float fadeAlpha;
        private Color fadeColor = Color.black;

        private IslandStep step;
        private float stepTime;
        private float movedMeters;
        private Vector3 lastPlayerPos;
        // (cardStart/cardDuration이 카드 시간을 든다)
        private string cardTitle = string.Empty;
        private string cardSub = string.Empty;
        private float cardStart;
        private float cardDuration;
        private bool enteredIsland;
        private int captionIndex;
        private float captionTime;

        private Camera cachedCamera;
        // 검수 촬영이 한 단계를 세워 두는 동안 시간이 흐르지 않게 한다. enabled=false는 못 쓴다 — OnDisable이 꿈을 끝낸다.
        private bool pausedForCapture;
        // 도입: 경기장 입장 영상 → 글자 카드. 영상이 못 뜨면(openVideoDone이 곧바로 참) 예전처럼 카드만.
        private DreamIntroVideo introVideo;
        private bool openVideoDone;
        private float openClock;        // 카드 단계의 시계
        private float openCardStart;    // 카드 글자가 뜨기 시작하는 시각(openClock 기준)
        private float openCardEnd;
        private GUIStyle skipStyle;
        private GUIStyle titleStyle, subStyle, bigStyle, hintStyle, captionStyle, speakerStyle, glyphStyle, distStyle;
        private bool stylesReady;

        public bool IsRunning => phase != Phase.Idle;

        public void AutoWire(TutorialQuestManager quests, PlayerMovement movement, Transform player,
            RegionManager region, IslandWorldBuilder island, InsectBattleController battle,
            BattleScreenUI battleUi, InsectDatabase db, FieldMomentFeed feed)
        {
            if (questManager == null) questManager = quests;
            if (playerMovement == null) playerMovement = movement;
            if (playerTransform == null) playerTransform = player;
            if (regionManager == null) regionManager = region;
            if (islandWorld == null) islandWorld = island;
            if (battleController == null) battleController = battle;
            if (battleScreen == null) battleScreen = battleUi;
            if (database == null) database = db;
            if (momentFeed == null) momentFeed = feed;
        }

        private static string PlayedKey => SaveScope.PrefsKey(DreamPrologueRules.PlayedPrefsKey);

        // ── 외부 진입점 ──

        /// <summary>설정 화면의 「다시 보기」가 눌러도 되는가.</summary>
        public bool CanReplay => CanReplayIgnoring(null);

        /// <summary>
        /// <paramref name="modalToIgnore"/>(버튼이 든 설정 화면)를 뺀 다른 모달이 없고 조작이 풀려 있는가.
        /// 설정 화면은 자기가 모달이라 그냥 물으면 늘 막힌다.
        /// </summary>
        public bool CanReplayIgnoring(System.Type modalToIgnore) => DreamPrologueRules.CanReplay(
                IsRunning, ModalUIRegistry.IsAnyOpenExcept(modalToIgnore),
                playerMovement != null && playerMovement.IsFrozen,
                regionManager != null && regionManager.CurrentSubArea != null)
            && !DreamPrologueState.Active && battleController != null && battleScreen != null && islandWorld != null;

        public bool TryReplay()
        {
            if (!CanReplay) return false;
            Begin();
            return true;
        }

        // ── 생명주기 ──

        private void Update()
        {
            if (pausedForCapture) return;
            if (phase == Phase.Idle)
            {
                WatchAutoStart();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            phaseTime += dt;
            switch (phase)
            {
                case Phase.Opening: UpdateOpening(dt); break;
                case Phase.Battle: UpdateBattle(dt); break;
                case Phase.ToIsland: UpdateToIsland(); break;
                case Phase.Island: UpdateIsland(dt); break;
                case Phase.ToWake: UpdateToWake(); break;
                case Phase.Wake: UpdateWake(dt); break;
            }
        }

        private void OnDisable() { if (phase != Phase.Idle) Finish(false); }

        private void OnDestroy()
        {
            if (phase != Phase.Idle) Finish(false);
            else DreamPrologueState.End();
            if (introVideo != null) introVideo.Dispose();
        }

        private void OnApplicationPause(bool paused)
        {
            if (introVideo != null) introVideo.Pause(paused);
        }

        private void WatchAutoStart()
        {
            if (questManager == null || playerMovement == null || regionManager == null) return;

            TutorialQuest active = questManager.ActiveQuest;
            bool playedBefore = PlayerPrefs.GetInt(PlayedKey, 0) != 0;
            bool ok = battleController != null && battleScreen != null && islandWorld != null
                && DreamPrologueRules.ShouldAutoStart(
                    active != null ? active.questId : null, questManager.ActiveProgress, playedBefore,
                    ModalUIRegistry.IsAnyOpen(), playerMovement.IsFrozen, regionManager.CurrentSubArea != null);

            stableTimer = ok ? stableTimer + Time.unscaledDeltaTime : 0f;
            if (stableTimer >= DreamPrologueRules.StableSeconds) Begin();
        }

        // ── 시작 ──

        private void Begin()
        {
            stableTimer = 0f;
            DreamPrologueState.Begin();
            if (playerMovement != null) playerMovement.SetFrozen(true);

            championName = ReadChampionName();
            InsectData foe = ResolveSpecies(DreamPrologueData.ChallengerInsectId);
            challengerName = foe != null ? foe.displayName : string.Empty;
            versusLine = string.IsNullOrEmpty(challengerName) ? string.Empty : $"vs  {challengerName}";
            enteredIsland = false;
            battleStarted = false;
            battleEnded = false;
            battleTurns = 0;

            SetPhase(Phase.Opening);
            fadeColor = Color.black;
            fadeAlpha = 0f;
            StartIntro();
        }

        private static string ReadChampionName()
        {
            string name = PlayerPrefs.GetString(SaveScope.PrefsKey("InsectGame.Character.Name"), string.Empty);
            return string.IsNullOrWhiteSpace(name) ? "챔피언" : name.Trim();
        }

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        // ── 도입(경기장 입장 영상 → 검은 화면의 타이틀 카드) ──

        // 영상을 틀고(성공하면 영상 단계), 파일을 미리 못 찾으면 글자 카드로 대신한다.
        private void StartIntro()
        {
            if (introVideo == null) introVideo = new DreamIntroVideo(gameObject);
            if (introVideo.Play()) { openVideoDone = false; openClock = 0f; }
            else SetOpeningCard(false);
        }

        // 카드 단계의 시간표. 영상 뒤엔 이미 검은 화면이라 곧바로 글자가 뜨고, 영상 없이는 페이드 인 뒤에 뜬다.
        private void SetOpeningCard(bool afterVideo)
        {
            openVideoDone = true;
            openClock = 0f;
            openCardStart = afterVideo ? 0f : OpenFadeSeconds;
            openCardEnd = openCardStart + (afterVideo ? IntroCardSeconds : OpenTitleSeconds);
        }

        private void UpdateOpening(float dt)
        {
            if (!openVideoDone)
            {
                introVideo.Tick(dt);
                DreamIntroVideo.State state = introVideo.Current;
                if (state == DreamIntroVideo.State.Preparing)
                {
                    fadeAlpha = Mathf.Clamp01(phaseTime / IntroPrepFadeSeconds);   // 준비하는 동안 검게 가라앉는다
                    return;
                }
                if (state == DreamIntroVideo.State.Playing)
                {
                    fadeAlpha = 1f;   // 영상이 화면을 덮는다 — 이 검정은 첫 프레임 전의 틈 메우기다
                    return;
                }
                // 끝났거나(마지막 프레임은 검다) 못 틀었다 — 카드로 이어 간다.
                bool played = state == DreamIntroVideo.State.Ended;
                introVideo.Stop();
                SetOpeningCard(played);
                return;
            }

            openClock += dt;
            fadeAlpha = openCardStart <= 0f ? 1f : Mathf.Clamp01(openClock / openCardStart);
            if (openClock < openCardEnd) return;

            if (TryStartBattle()) SetPhase(Phase.Battle);
            else BeginIsland();   // 전투를 못 세우면(종 없음 등) 섬부터 — 꿈은 계속된다
        }

        // ── 챔피언전 ──

        private bool TryStartBattle()
        {
            InsectData ace = ResolveSpecies(DreamPrologueData.AceInsectId);
            InsectData foe = ResolveSpecies(DreamPrologueData.ChallengerInsectId);
            if (ace == null || foe == null || battleController == null || battleScreen == null) return false;

            PlayerInsectData champion = DreamPrologueData.BuildChampionInsect(ace);
            InsectSkill[] skills = DreamPrologueData.PickChampionSkills(ace, DreamPrologueData.AceLevel);

            SubscribeBattle();
            if (!battleController.StartSandbox(ace, DreamPrologueData.AceLevel, foe,
                    DreamPrologueData.ChallengerLevel, skills, champion))
            {
                UnsubscribeBattle();
                return false;
            }
            battleStarted = true;
            return true;
        }

        private void SubscribeBattle()
        {
            if (subscribed || battleController == null) return;
            subscribed = true;
            battleController.BattleUpdated += OnBattleUpdated;
            battleController.BattleEnded += OnBattleEnded;
        }

        private void UnsubscribeBattle()
        {
            if (!subscribed || battleController == null) { subscribed = false; return; }
            subscribed = false;
            battleController.BattleUpdated -= OnBattleUpdated;
            battleController.BattleEnded -= OnBattleEnded;
        }

        private void OnBattleUpdated(InsectBattleStats player, InsectBattleStats enemy)
        {
            if (battleController != null && battleController.IsSandbox && battleController.PlayerActedThisRound)
                battleTurns++;
        }

        private void OnBattleEnded(bool playerWon)
        {
            battleEnded = true;
            battleEndedAt = phaseTime;
        }

        private void UpdateBattle(float dt)
        {
            // 검은 화면이 걷히며 전투 연출이 드러난다.
            fadeAlpha = Mathf.Clamp01(1f - phaseTime / BattleRevealSeconds);

            bool screenActive = battleScreen != null && battleScreen.IsBattleActive;
            // 전투 화면이 안 뜨면 꿈이 거기서 멈춘다 — 상한 뒤에 섬으로 넘어간다.
            if (!screenActive && !battleEnded && phaseTime > BattleStartTimeoutSeconds) { EndBattlePhase(); return; }
            // 끝난 뒤 결과 화면이 닫히면(EndBattle) 다음으로.
            if (battleEnded && !screenActive) { EndBattlePhase(); return; }
            if (battleEnded && phaseTime - battleEndedAt > BattleEndTimeoutSeconds) EndBattlePhase();
        }

        private void EndBattlePhase()
        {
            UnsubscribeBattle();
            SetPhase(Phase.ToIsland);
            // 전투 화면이 막 사라져 월드가 한 프레임 비칠 수 있다 — 흰색으로 곧바로 덮는다.
            fadeColor = Color.white;
            fadeAlpha = 1f;
        }

        // ── 장면 전환(흰 섬광) ──

        private void UpdateToIsland()
        {
            fadeAlpha = 1f;
            if (phaseTime < FlashSeconds) return;   // 흰 화면을 잠깐 머문 뒤 섬을 연다
            BeginIsland();
        }

        // ── 챔피언의 섬 ──

        private void BeginIsland()
        {
            fadeColor = Color.white;
            fadeAlpha = 1f;
            if (!EnterIsland())
            {
                BeginWake();   // 섬을 못 열면 곧바로 깨어난다 — 꿈을 어디서든 마칠 수 있어야 한다
                return;
            }
            SetPhase(Phase.Island);
            step = IslandStep.Move;
            stepTime = 0f;
            movedMeters = 0f;
            lastPlayerPos = playerTransform != null ? playerTransform.position : Vector3.zero;
            ShowCard("챔피언의 섬", "함께 싸운 곤충들이 모두 모여 있다", IslandTitleSeconds);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM(BgmType.ExploreMeadow);
        }

        private bool EnterIsland()
        {
            if (islandWorld == null) return false;
            islandWorld.DreamMode = true;
            IslandSnapshot snapshot = DreamPrologueData.BuildIslandSnapshot(championName);
            IslandSaveRules.SanitizeSnapshot(snapshot);   // 남이 만든 데이터와 같은 길 — 모르는 ID·겹침은 여기서 걸러진다
            if (playerMovement != null) playerMovement.SetFrozen(false);
            enteredIsland = islandWorld.EnterVisit(snapshot);
            if (!enteredIsland) islandWorld.DreamMode = false;
            return enteredIsland;
        }

        private void UpdateIsland(float dt)
        {
            // 섬광이 걷히며 섬이 드러난다.
            fadeAlpha = Mathf.Clamp01(1f - phaseTime / IslandRevealSeconds);
            stepTime += dt;

            if (phaseTime > IslandTimeoutSeconds) { BeginWake(); return; }
            if (islandWorld == null || playerTransform == null || !islandWorld.IsOnIsland)
            {
                // 섬에서 쫓겨났다(끼임 복구·강제 이탈) — 같은 꿈을 이어 갈 수 없다.
                BeginWake();
                return;
            }

            Vector3 pos = playerTransform.position;
            Vector3 delta = pos - lastPlayerPos;
            delta.y = 0f;
            float stepMeters = delta.magnitude;
            if (stepMeters < TeleportMeters) movedMeters += stepMeters;
            lastPlayerPos = pos;

            switch (step)
            {
                case IslandStep.Move:
                    if (movedMeters >= MoveGoalMeters) NextStep(IslandStep.Approach);
                    break;
                case IslandStep.Approach:
                    if (islandWorld.TryGetNearestInsect(pos, out Vector3 insectPos, out InsectData data, out int level)
                        && PlanarDistance(pos, insectPos) <= ApproachMeters)
                    {
                        ShowCard($"{data.displayName}", $"{data.rarity.Korean()}  ·  Lv.{level}  ·  당신의 파트너", InsectCardSeconds);
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.LevelUp);
                        NextStep(IslandStep.Fountain);
                    }
                    break;
                case IslandStep.Fountain:
                    // 카드를 읽을 시간을 준다 — 곧바로 다음 목표로 시선을 뺏지 않는다.
                    if (stepTime > 1.5f && PlanarDistance(pos, DreamPrologueData.FountainWorldPosition()) <= FountainMeters)
                    {
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.SetComplete);
                        NextStep(IslandStep.Linger);
                    }
                    break;
                case IslandStep.Linger:
                    if (stepTime >= LingerSeconds) BeginWake();
                    break;
            }
        }

        private void NextStep(IslandStep next)
        {
            step = next;
            stepTime = 0f;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void ShowCard(string title, string sub, float seconds)
        {
            cardTitle = title;
            cardSub = sub;
            cardStart = Time.unscaledTime;
            cardDuration = seconds;
        }

        // ── 깨어남 ──

        private void BeginWake()
        {
            UnsubscribeBattle();
            SetPhase(Phase.ToWake);
            fadeColor = Color.white;
        }

        private void UpdateToWake()
        {
            fadeAlpha = Mathf.Clamp01(phaseTime / WakeFadeOutSeconds);
            if (fadeAlpha < 1f) return;

            LeaveIsland();
            if (playerMovement != null) playerMovement.SetFrozen(true);
            if (AudioManager.Instance != null) AudioManager.Instance.RestoreExploreBGM();
            captionIndex = 0;
            captionTime = 0f;
            SetPhase(Phase.Wake);
        }

        private void LeaveIsland()
        {
            if (islandWorld == null) return;
            if (islandWorld.IsOnIsland) islandWorld.ExitIsland();
            islandWorld.DreamMode = false;
            enteredIsland = false;
        }

        private void UpdateWake(float dt)
        {
            fadeAlpha = Mathf.Clamp01(1f - phaseTime / WakeFadeInSeconds);
            captionTime += dt;
            if (captionTime < CaptionSeconds) return;

            captionTime = 0f;
            captionIndex++;
            if (captionIndex >= WakeCaptions.Length) Finish(true);
        }

        // ── 건너뛰기 ──

        private void Skip()
        {
            switch (phase)
            {
                case Phase.Opening:
                    // 도입을 건너뛰어도 전투는 본다 — 건너뛰기가 연출 전체를 날리는 버튼이 되면 안 된다.
                    if (introVideo != null) introVideo.Stop();
                    SetOpeningCard(true);
                    openClock = openCardEnd;   // 카드도 건너뛴다 — 다음 프레임에 전투가 선다
                    break;
                case Phase.Battle:
                    // 전투는 정상 종료 길(결과 화면·정리)을 타게 이긴 것으로 끝낸다.
                    if (battleController != null) battleController.ForceSandboxVictory();
                    break;
                case Phase.Island:
                    BeginWake();
                    break;
                case Phase.Wake:
                    Finish(true);
                    break;
            }
        }

        // ── 정리 ──

        /// <summary>
        /// 꿈을 끝낸다 — <b>모든 종료 길이 여기를 지난다.</b> 이 줄들 중 하나라도 빠지면 표지가 켜진 채 남거나
        /// 섬에 갇히거나 플레이어가 얼어 붙는다.
        /// </summary>
        private void Finish(bool completed)
        {
            if (introVideo != null) introVideo.Stop();   // 영상 소리·디코더·BGM 더킹을 놓는다
            UnsubscribeBattle();
            LeaveIsland();
            Phase was = phase;
            phase = Phase.Idle;
            fadeAlpha = 0f;
            DreamPrologueState.End();
            if (playerMovement != null) playerMovement.SetFrozen(false);
            if (AudioManager.Instance != null && was != Phase.Idle) AudioManager.Instance.RestoreExploreBGM();

            // 끝까지 보았거나 건너뛰었을 때만 "봤다"고 적는다 — 오류·씬 재로드로 끊긴 것은 다음 기회에 다시 보여 준다.
            if (completed)
            {
                PlayerPrefs.SetInt(PlayedKey, 1);
                PlayerPrefs.Save();
                if (momentFeed != null)
                    momentFeed.Push(FieldMomentKind.Discovery, "꿈속의 섬을 현실로",
                        "곤충을 모으고 키워서 나만의 섬을 꾸며 보세요");
            }
        }

        private InsectData ResolveSpecies(string insectId)
        {
            if (database == null) return null;
            InsectData data = database.GetById(insectId);
            if (data != null) return data;
            // ID가 사라졌다면 아무 전설종이라도 — 꿈이 통째로 안 뜨는 것보다 낫다. 테스트가 ID 존재를 따로 고정한다.
            foreach (InsectData candidate in database.insects)
                if (candidate != null && candidate.rarity == InsectRarity.Legendary) return candidate;
            return null;
        }

        // ── 그리기 ──

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            UITheme t = UITheme.Instance;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            subStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 66, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            captionStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleCenter };
            speakerStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            speakerStyle.normal.textColor = t.accentAmber;
            glyphStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            glyphStyle.normal.textColor = t.surfaceBase;
            distStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            skipStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            skipStyle.normal.textColor = t.textPrimary;
        }

        private void OnGUI()
        {
            if (phase == Phase.Idle) return;

            GUI.depth = -100;   // 전투 화면·섬 위에 얹는다
            UIScale.Begin();
            InitStyles();
            // 페이드가 먼저다 — 검은 화면 위의 타이틀 카드와 흰 화면 위의 자막이 페이드 **위에** 보여야 한다.
            DrawFade();
            switch (phase)
            {
                case Phase.Opening: DrawOpening(); break;
                case Phase.Battle: DrawBattleHint(); break;
                case Phase.Island: DrawIslandOverlay(); break;
                case Phase.Wake: DrawCaption(); break;
            }
            if (phase != Phase.ToIsland && phase != Phase.ToWake) DrawSkipButton();
            GUI.color = Color.white;
            UIScale.End();
        }

        private void DrawFade()
        {
            if (fadeAlpha <= 0.001f) return;
            GUI.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, fadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawOpening()
        {
            if (!openVideoDone)
            {
                if (introVideo != null)
                    introVideo.Draw(new Rect(0f, 0f, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight));
                return;
            }
            DrawOpeningCard();
        }

        private void DrawOpeningCard()
        {
            float textAlpha = Mathf.Clamp01((openClock - openCardStart) / 0.7f)
                * Mathf.Clamp01((openCardEnd - openClock) / 0.5f);
            if (textAlpha <= 0f) return;
            UITheme t = UITheme.Instance;
            Rect panel = UISafeLayout.CenteredPanel(Mathf.Min(900f, UISafeLayout.ContentWidth), 300f);

            titleStyle.normal.textColor = Faded(t.accentAmber, textAlpha);
            UIHelper.LabelFit(new Rect(panel.x, panel.y, panel.width, 50f), "—  챔피언 결정전  —", titleStyle);
            bigStyle.normal.textColor = Faded(t.textPrimary, textAlpha);
            UIHelper.LabelFit(new Rect(panel.x, panel.y + 60f, panel.width, 110f), championName, bigStyle);
            if (versusLine.Length > 0)
            {
                subStyle.normal.textColor = Faded(t.textSecondary, textAlpha);
                UIHelper.LabelFit(new Rect(panel.x, panel.y + 190f, panel.width, 50f), versusLine, subStyle);
            }
        }

        private static Color Faded(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

        // 전투 중 안내 — 한 턴에 한 줄. 위치는 전투 화면의 상단 HUD 아래다.
        private void DrawBattleHint()
        {
            if (battleEnded || phaseTime < BattleRevealSeconds * 0.6f) return;
            // 세로 화면에서는 양쪽 HP 카드가 폭을 거의 다 차지한다 — 그 아래로 내린다(2026-10-03 검수 캡처).
            DrawHintPill(BattleHintFor(battleTurns), UIScale.IsPortrait ? 250f : 168f);
        }

        private static string BattleHintFor(int turns)
        {
            bool mobile = UIScale.IsMobileLayout;
            switch (turns)
            {
                case 0: return mobile ? "아래 기술 카드를 눌러 공격하세요" : "기술 카드를 누르거나 [1]~[4] 키로 공격하세요";
                case 1: return "상성이 맞으면 '효과가 굉장했다!' — 기술마다 대기 시간이 있어요";
                case 2: return "마지막 일격! 가장 강한 기술을 써 보세요";
                default: return "결승전, 마무리!";
            }
        }

        /// <summary>섬 걷기 안내 알약의 윗변 — 안전 영역 위에서 이만큼(건너뛰기 버튼과 같은 줄, 가운데).</summary>
        public const float IslandPillTop = 40f;

        /// <summary>안내 알약의 자리 — 순수 계산(겹침 전수 검사가 섬 단계의 것을 읽는다).</summary>
        public static Rect HintPillRect(HudFrame f, float topOffset)
        {
            Rect pill = f.TopPanel(Mathf.Min(f.ContentWidth, f.Mobile ? 640f : 900f), 64f);
            pill.y += topOffset;
            return pill;
        }

        private void DrawHintPill(string text, float topOffset)
        {
            UITheme t = UITheme.Instance;
            Rect pill = HintPillRect(HudFrame.Current, topOffset);
            FieldHudInput.RegisterBlockingRect(pill);
            UISurface.HudCard(pill);
            hintStyle.normal.textColor = t.textPrimary;
            UIHelper.LabelFit(new Rect(pill.x + 18f, pill.y, pill.width - 36f, pill.height), text, hintStyle);
        }

        // ── 섬 ──

        private void DrawIslandOverlay()
        {
            UITheme t = UITheme.Instance;

            // 목표 안내
            string hint = null;
            Vector3 target = Vector3.zero;
            bool hasTarget = false;
            switch (step)
            {
                case IslandStep.Move:
                    hint = UIScale.IsMobileLayout ? "1/3  왼쪽 조이스틱으로 걸어 보세요" : "1/3  [WASD] 또는 클릭으로 걸어 보세요";
                    break;
                case IslandStep.Approach:
                    hint = "2/3  곤충에게 다가가 보세요";
                    if (islandWorld != null && playerTransform != null
                        && islandWorld.TryGetNearestInsect(playerTransform.position, out Vector3 insectPos, out _, out _))
                    {
                        target = insectPos;
                        hasTarget = true;
                    }
                    break;
                case IslandStep.Fountain:
                    hint = "3/3  분수 앞으로 가 보세요";
                    target = DreamPrologueData.FountainWorldPosition();
                    hasTarget = true;
                    break;
                case IslandStep.Linger:
                    hint = "모두가 당신을 기다리고 있었다";
                    break;
            }
            if (hint != null && phaseTime > 1f) DrawHintPill(hint, IslandPillTop);
            if (hasTarget && phaseTime > 1f) DrawWaypoint(target, t.accentMint);

            // 카드(섬 이름·곤충 소개) — 0.3초에 나타나 끝나기 0.4초 전부터 사라진다
            float shown = Time.unscaledTime - cardStart;
            if (cardDuration > 0f && shown < cardDuration)
                DrawCard(Mathf.Clamp01(shown / 0.3f) * Mathf.Clamp01((cardDuration - shown) / 0.4f));
        }

        /// <summary>
        /// 섬 카드(섬 이름·곤충 소개)의 자리 — 순수 계산. 아래 가운데. <b>세로 모바일</b>은 잡기 버튼 열(오른쪽 아래)이 폭 760 카드의
        /// 오른쪽 끝에 걸리므로 그 위의 습격 경고 글자보다 위로 올린다.
        /// </summary>
        public static Rect IslandCardRect(HudFrame f)
        {
            Rect card = f.BottomPanel(Mathf.Min(f.ContentWidth, 760f), 150f);
            card.y -= 24f;
            if (f.Mobile && f.Portrait)
                card.y = Mathf.Min(card.y, InsectGame.Capture.CatchButtonLayout.WarnRect(f).y - UITheme.Space.S - card.height);
            return card;
        }

        private void DrawCard(float alpha)
        {
            UITheme t = UITheme.Instance;
            Rect card = IslandCardRect(HudFrame.Current);
            FieldHudInput.RegisterBlockingRect(card);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            UISurface.Card(card, t.surfaceCard, Color.Lerp(t.surfaceBorder, t.accentAmber, 0.6f));
            UISurface.Flat(new Rect(card.x + UITheme.Radius.Card, card.y + 3f, card.width - UITheme.Radius.Card * 2f, 4f), t.accentAmber);
            GUI.color = Color.white;
            bigStyle.fontSize = 44;
            bigStyle.normal.textColor = Faded(t.accentAmber, alpha);
            UIHelper.LabelFit(new Rect(card.x + 20f, card.y + 16f, card.width - 40f, 70f), cardTitle, bigStyle);
            bigStyle.fontSize = 66;
            subStyle.normal.textColor = Faded(t.textSecondary, alpha);
            UIHelper.LabelFit(new Rect(card.x + 20f, card.y + 90f, card.width - 40f, 44f), cardSub, subStyle);
        }

        // 목표 표식 — 화면 안이면 그 자리에 고리와 거리, 밖이면 가장자리에 붙여 방향만 알린다(지도 쐐기와 같은 약속).
        private void DrawWaypoint(Vector3 world, Color color)
        {
            if (cachedCamera == null) cachedCamera = Camera.main;
            if (cachedCamera == null || playerTransform == null) return;

            Vector3 sp = cachedCamera.WorldToScreenPoint(world + Vector3.up * 1.2f);
            if (sp.z < 0f) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }
            float s = Mathf.Max(0.3f, UIScale.Scale);
            Vector2 g = new Vector2(sp.x / s, (Screen.height - sp.y) / s);

            float margin = 70f;
            Rect inner = new Rect(UIScale.VirtualSafeLeft + margin, UISafeLayout.ContentTop + margin + 110f,
                UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - margin * 2f,
                UISafeLayout.ContentHeight - margin * 2f - 220f);
            bool onScreen = sp.z > 0f && inner.Contains(g);
            Vector2 p = new Vector2(Mathf.Clamp(g.x, inner.x, inner.xMax), Mathf.Clamp(g.y, inner.y, inner.yMax));

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            float r = 26f + 6f * pulse;
            UIShapes.Ellipse(new Rect(p.x - r - 6f, p.y - r - 6f, (r + 6f) * 2f, (r + 6f) * 2f), new Color(color.r, color.g, color.b, 0.3f));
            UIShapes.Ellipse(new Rect(p.x - 26f, p.y - 26f, 52f, 52f), color);

            if (onScreen)
            {
                GUI.Label(new Rect(p.x - 26f, p.y - 26f, 52f, 52f), "!", glyphStyle);
            }
            else
            {
                // 방향 화살표 — 화면 가운데에서 목표로 향하는 각도로 돌린다.
                Vector2 center = new Vector2(UIScale.VirtualScreenWidth * 0.5f, UIScale.VirtualScreenHeight * 0.5f);
                Vector2 dir = g - center;
                float angle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
                Matrix4x4 saved = GUI.matrix;
                GUI.matrix = MapMarkerProjection.PivotMatrix(saved, p, angle);
                GUI.Label(new Rect(-26f, -26f, 52f, 52f), "▲", glyphStyle);
                GUI.matrix = saved;
            }

            int meters = Mathf.RoundToInt(PlanarDistance(playerTransform.position, world));
            if (meters != shownMeters)
            {
                shownMeters = meters;
                meterLabel = meters + "m";
            }
            distStyle.normal.textColor = Color.white;
            UIHelper.LabelFit(new Rect(p.x - 50f, p.y + 32f, 100f, 26f), meterLabel, distStyle);
        }

        // ── 깨어남의 자막 ──

        private void DrawCaption()
        {
            if (captionIndex >= WakeCaptions.Length) return;
            // 흰 화면이 걷히는 동안 첫 줄이 먼저 뜬다 — 눈을 뜨는 것과 목소리가 겹친다.
            float inA = Mathf.Clamp01(captionTime / 0.4f);
            float outA = Mathf.Clamp01((CaptionSeconds - captionTime) / 0.4f);
            float alpha = inA * outA;
            if (alpha <= 0f) return;

            UITheme t = UITheme.Instance;
            var (speaker, text) = WakeCaptions[captionIndex];
            Rect panel = UISafeLayout.BottomPanel(Mathf.Min(UISafeLayout.ContentWidth, 1000f), 150f);
            panel.y -= 60f;
            bool hasSpeaker = !string.IsNullOrEmpty(speaker);
            // 흰 화면이 걷히는 동안에는 밝은 바탕 위에 흰 글자가 얹힌다 — 어두운 띠를 깐다.
            UISurface.Rounded(new Rect(panel.x, panel.y - 8f, panel.width, hasSpeaker ? 128f : 108f),
                new Color(t.surfaceBase.r, t.surfaceBase.g, t.surfaceBase.b, 0.78f * alpha));
            if (hasSpeaker)
            {
                speakerStyle.normal.textColor = Faded(t.accentAmber, alpha);
                UIHelper.LabelFit(new Rect(panel.x, panel.y, panel.width, 34f), speaker, speakerStyle);
            }
            captionStyle.normal.textColor = Faded(hasSpeaker ? t.textPrimary : t.textSecondary, alpha);
            UIHelper.LabelFit(new Rect(panel.x, panel.y + (hasSpeaker ? 40f : 20f), panel.width, 70f), text, captionStyle);
        }

        // ── 건너뛰기 ──

        /// <summary>
        /// 건너뛰기 버튼의 자리 — 순수 계산. 오른쪽 위. 전투 중에는 오른쪽 위가 배속 버튼 자리라 아레나 바닥 쪽(아래 행동 패널 바로 위)으로 내린다.
        /// </summary>
        public static Rect SkipRect(HudFrame f, bool battle)
        {
            float w = f.Mobile ? 170f : 150f;
            float h = f.Mobile ? 64f : 50f;
            float y = battle ? Mathf.Clamp(f.Height * 0.6f, f.ContentTop, f.ContentBottom - h) : f.ContentTop + 10f;
            return new Rect(f.Width - f.SafeRight - w - 20f, y, w, h);
        }

        private void DrawSkipButton()
        {
            UITheme t = UITheme.Instance;
            Rect rect = SkipRect(HudFrame.Current, phase == Phase.Battle);
            FieldHudInput.RegisterBlockingRect(rect);
            skipStyle.fontSize = UIScale.IsMobileLayout ? 26 : 22;
            if (UISurface.Button(rect, "건너뛰기 ›", Faded(t.surfaceRaised, 0.85f), skipStyle)) Skip();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ── 검수 빌드 전용 ──

        /// <summary>
        /// 지정한 단계의 화면을 세워 둔다(실제 IMGUI 촬영용). 섬 단계는 실제 섬을 짓고, 전투 단계는 호출부가
        /// 전투 화면을 직접 띄운 뒤 힌트만 맡는다.
        /// </summary>
        public void ShowForCapture(string stage, float seconds)
        {
            if (introVideo != null) introVideo.Stop();   // 앞 단계의 영상이 뒤에서 계속 돌지 않게
            DreamPrologueState.Begin();
            championName = "하늘";
            challengerName = "태고의 비천룡";
            versusLine = "vs  태고의 비천룡";
            pausedForCapture = false;
            switch (stage)
            {
                case "opening":
                    SetPhase(Phase.Opening);
                    SetOpeningCard(false);
                    openClock = openCardStart + seconds;
                    fadeColor = Color.black;
                    fadeAlpha = 1f;
                    break;
                case "opening-video":
                    // 진짜 영상을 실시간으로 튼다 — 시간을 세워 두지 않는다(촬영기가 시각을 골라 찍는다).
                    SetPhase(Phase.Opening);
                    fadeColor = Color.black;
                    fadeAlpha = 0f;
                    StartIntro();
                    break;
                case "battle":
                    SetPhase(Phase.Battle);
                    phaseTime = seconds;
                    battleTurns = 0;
                    break;
                case "island-title":
                    EnterIslandForCapture();
                    phaseTime = seconds;
                    fadeAlpha = 0f;
                    ShowCard("챔피언의 섬", "함께 싸운 곤충들이 모두 모여 있다", 60f);   // 도착 카드를 그대로 세워 둔다
                    break;
                case "island-move":
                case "island-approach":
                case "island-fountain":
                    EnterIslandForCapture();
                    phaseTime = seconds;
                    cardDuration = 0f;
                    step = stage == "island-move" ? IslandStep.Move
                        : stage == "island-approach" ? IslandStep.Approach : IslandStep.Fountain;
                    stepTime = 0f;
                    fadeAlpha = 0f;
                    break;
                case "island-card":
                    EnterIslandForCapture();
                    phaseTime = seconds;
                    fadeAlpha = 0f;
                    ShowCard("헤라클레스 천공각", "전설  ·  Lv.80  ·  당신의 파트너", 60f);
                    break;
                case "wake":
                    SetPhase(Phase.Wake);
                    captionIndex = 0;
                    captionTime = seconds;
                    phaseTime = seconds;
                    fadeColor = Color.white;
                    fadeAlpha = Mathf.Clamp01(1f - phaseTime / WakeFadeInSeconds);
                    break;
            }
            // 이 단계가 시간으로 넘어가지 않게 멈춰 둔다(영상 단계만 실시간으로 돈다).
            pausedForCapture = stage != "opening-video";
        }

        // 섬은 한 번만 짓는다 — 단계마다 다시 지으면 곤충이 처음 자리로 돌아가고 촬영이 느려진다.
        private void EnterIslandForCapture()
        {
            if (islandWorld != null && islandWorld.IsOnIsland)
            {
                SetPhase(Phase.Island);
                return;
            }
            BeginIsland();
        }

        /// <summary>도입 영상이 실제로 재생 중일 때의 영상 시각(초). 재생 중이 아니면 -1 — 촬영기가 영상 시각을 기다릴 때 쓴다.</summary>
        public float IntroClockForCapture =>
            introVideo != null && introVideo.Current == DreamIntroVideo.State.Playing ? introVideo.Clock : -1f;

        /// <summary>도입 영상이 실제로 끝나(또는 못 틀어) 글자 카드 단계로 넘어갔는가.</summary>
        public bool IntroReachedCardForCapture => openVideoDone;

        /// <summary>실제 전투 장면 검수용 — 도입을 건너뛰고 샌드박스 전투를 바로 연다. 이후 흐름(섬·깨어남)은 실제 그대로 돈다.</summary>
        public bool StartBattleForCapture()
        {
            DreamPrologueState.Begin();
            championName = "하늘";
            challengerName = "태고의 비천룡";
            if (!TryStartBattle()) return false;
            SetPhase(Phase.Battle);
            return true;
        }

        public void EndCapture()
        {
            pausedForCapture = false;
            Finish(false);
        }
#endif
    }
}
