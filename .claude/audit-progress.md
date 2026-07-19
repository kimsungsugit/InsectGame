# Audit 진척

`/audit` 스킬과 훅 2개(`audit_flow_inject`, `audit_reminder`)가 읽고 갱신합니다.
**구조 유지 필수** — 특히 `## Uncovered`의 `- [ ]` 개수가 자동 플로우의 트리거입니다.

- **Covered**: 처리 완료 영역 인덱스. 서술 원문은 `.claude/audit-archive/covered-detail.md`
- **Uncovered**: 다음 audit 후보 큐. 위에서 아래로 우선순위.
  **비면 `python -X utf8 .claude/scripts/audit_candidates.py --emit-md`로 재생성**
- **Round Log**: 최근 10건. 이전 이력은 `.claude/audit-archive/round-log-2026H1.md`

## Covered (P0/P1 처리 완료)

- [x] CharacterPortraitRenderer (P0:2, P1:2, 2026-05-20)
- [x] PlayerVisualBuilder (P1:5, 2026-05-20)
- [x] CloudSaveManager (P1:2, 2026-05-20)
- [x] PlaySceneBootstrap (P1:1, 2026-05-20)
- [x] CharacterOutfitUI (P1:2, 2026-05-20)
- [x] CharacterOutfitManager (P1:2, 2026-05-20)
- [x] PlayerMovement (P1:1, 2026-05-20)
- [x] CashShopManager (P1:1, 2026-05-20)
- [x] CaptureMinigameController (P0:1, P1:1, 2026-05-20)
- [x] GachaBoxManager (P1:1, 2026-05-20)
- [x] TutorialQuestManager (P1:1, P0:4, 2026-05-20)
- [x] BattleScreenUI (P2:1 DrawCombo + P1:2 P2:1 추가, 2026-05-20)
- [x] SubAreaWorldBuilder (P0:8 + UI:1, 2026-05-20)
- [x] AuthManager (P1:2, 2026-05-20)
- [x] LoginUI (P1:2, 2026-05-20)
- [x] RaidBattleUI (P0:2, P2:1, 2026-05-20)
- [x] RaidBattleController (P1:1, P0:1, 2026-05-20)
- [x] InsectBattleController (P0:2, 2026-05-20)
- [x] CaptureController (P0:1, 2026-05-20)
- [x] CaptureFeedbackController (clean, 2026-05-20)
- [x] BattleArenaController (P1:1, 2026-05-20)
- [x] DexController (clean, 2026-05-20)
- [x] DexDetailUIController (clean, 2026-05-20)
- [x] DexScreenUI (P0:31, 2026-05-20)
- [x] DexListUIController (clean, 2026-05-20)
- [x] SaveService 글로벌 atomic write 라운드 (P1:8, 2026-05-21)
- [x] CollectionUI 핫스팟 (P0:1, P1:3, 2026-05-21)
- [x] TrainingUI 핫스팟 (P0:1, P1:3, 2026-05-21)
- [x] CollectionUI 잔여 영역 (P1:27, 2026-05-21)
- [x] TrainingUI 잔여 영역 (P1:34, 2026-05-21)
- [x] PlayerStatusHUD (P0:1, P1:2, 2026-05-21)
- [x] TrainingManager (P1:1, 2026-05-21)
- [x] CashShopManager 잔여 영역 (P1:3, 2026-05-22)
- [x] InsectSpawner Spawn/Despawn 다중 호출 검증 (clean, 2026-05-22)
- [x] RegionMapUI 깊은 점검 (clean P0, P1 보류, 2026-05-27)
- [x] TutorialQuestUI DrawQuestPanel 캐시 (P0:1, 2026-05-27)
- [x] WorldChannelManager 깊은 점검 (clean P0/P1, 2026-05-27)
- [x] **RegionTerrainBuilder c.y 이중 합산 버그
- [x] Forest/Ruins 환경 추가 축소
- [x] 메인 월드 Forest/Ruins region 환경 높이 (P1:2, 2026-05-27)
- [x] SubArea 덮개 가시성 (P1:2, 2026-05-27)
- [x] 어깨 추가 + SubArea 부유/환경 높이 후속 (P1:4, 2026-05-27)
- [x] 캐릭터 시각 버그 3종 (P1:3+구조 변경, 2026-05-27)
- [x] TutorialQuestUI 잔존 캐시 (P1:8, 2026-05-27)
- [x] CapturePopupUI 캐시 (P1:12, 2026-05-27)
- [x] RegionMapUI 캐시 + OnDisable 정리 (P1:28+1, 2026-05-27)
- [x] WorldLobbyUI MakeTex 누수 (P0:7, 2026-05-27)
- [x] CharacterOutfitUI infoStyle 캐시 + OnDisable 정리 (P0:1+P1:1, 2026-05-27)
- [x] CapturePopupUI 점검 (clean P0, P1:1 보류, 2026-05-27)
- [x] CharacterPortraitRenderer 깊은 정합성 (clean, 2026-05-27)
- [x] PlaySceneBootstrap 깊은 점검 (clean, 2026-05-27)
- [x] SubAreaWorldBuilder 깊은 점검 (clean, 2026-05-27)
- [x] TutorialQuestManager 깊은 점검 (clean, 2026-05-27)
- [x] BattleScreenUI 깊은 로직 (clean, 2026-05-27)
- [x] RaidBattleUI 깊은 로직 (clean, 2026-05-27)
- [x] BattleArenaController 이펙트 (clean, 2026-05-27)
- [x] PlayerMovement 모달 차단 + InputAction 흐름 (clean, 2026-05-27)
- [x] InsectSpawner (P1:2, 2026-05-20)
- [x] RegionManager (P1:3, 2026-05-20)
- [x] RegionTerrainBuilder + RegionMapUI (UI:2, 2026-05-21)
- [x] ItemEffectManager (clean, 2026-05-21)
- [x] WorldStateProvider (clean, 2026-05-21)
- [x] WorldChannelManager (P1:1, 2026-05-21)
- [x] BattleTeamManager (P1:2, 2026-05-21)
- [x] BattleTeamUI (P1:16+22, 2026-05-21)
- [x] PlayerInsectCollection (P1:3, 2026-05-21)
- [x] PlayerProgressController (P1:3, 2026-05-21)
- [x] PlayerProgressSaveService (P1:1, 2026-05-21)
- [x] PlayerCandyInventory (P1:2, 2026-05-21)
- [x] PlayerCurrencyWallet (P1:5, 2026-05-21)
- [x] PlayerItemInventory (P1:3, 2026-05-21)
- [x] PlayerInsectLevelUpUIController (P1:3, 2026-05-21)
- [x] ShopUIController (P1:1, 2026-05-21)
- [x] GachaBoxUI → CashShopUI 가챠 영역 (P0:11, 2026-05-21)
- [x] ModalUIRegistry (clean, 2026-05-21)
- [x] CashShopUI 보석 충전/아이템 상점 영역 (P0:16, 2026-05-21)
- [x] UIHelper (P0:1, 2026-05-21)
- [x] AudioManager (P1:1, 2026-05-21)
- [x] ProceduralAudioGenerator (clean, 2026-05-21)
- [x] InsectEntity 외부 로직 (P0:1, P1:3, 2026-05-21)
- [x] CameraFollower (P1:2, 2026-05-21)
- [x] InsectDatabase (P1:1, 2026-05-21)
- [x] OutfitSetData (clean, 2026-05-21)
- [x] PlayerItemInventoryGridUIController (P1:1, 2026-05-21)
- [x] WorldFieldMultiplayerUI (P0:1, P1:1, P2:2, 2026-07-17)
- [x] UIHelper 텍스처 캐시 오버플로 (P1:1, 2026-07-17)
- [x] AccountLinkUI (P1:3, P2:2, 2026-07-17)
- [x] SubAreaEnvironment (P1:1, 2026-07-17)
- [x] AccountSettingsUI (P0:1, P1:2, 2026-07-17)
- [x] SaveConflictUI (P0:1, P1 보류, 2026-07-17)
- [x] KeyGuideHUD (P1:2, 2026-07-17)
- [x] QuickAccessBarUI (P0:1, 2026-07-17)
- [x] SocialPvpUI (P0:1, P1:2, 2026-07-17)
- [x] SceneAutoWire (clean — dead code 확인 후 삭제, 2026-07-17)
- [x] VillageBuilder (P1:2, P2:1, 2026-07-17)
- [x] NpcManager (P1:1 + 튜닝 프로필 동반 수정, 2026-07-17)
- [x] NpcDialogueUI (P1:1, 2026-07-17)
- [x] CatcherKidNpc (P1:1, 2026-07-17)
- [x] VillagerNpc + NpcWalkAnimator (clean, 2026-07-17)
- [x] CaptureItemPickup + CaptureItemSpawner + CaptureProximityTrigger (clean — dead code 확인, 2026-07-17)
- [x] HospitalUI (clean P0/P1, P2:1 보류, 2026-07-19) — score 36(프레임할당9/싱글턴9) 둘 다 거짓양성 확인; 모달 ESC·결제 원자성·stale 아이템 등 회귀 7종 전부 회피(TrainingUI 패턴)

## Uncovered (우선순위순)

2026-07-19 세션에서 치료경제·전투·스킬 신규 코드 대량 유입 → 큐 재생성
(`audit_candidates.py --emit-md`). 1순위 HospitalUI는 이번 라운드 처리 완료(Covered).
남은 후보 score 순:

- [ ] GuidedTutorialController (UI/GuidedTutorialController.cs, 164줄, score 15) — 프레임 할당 5
- [ ] StoryDirector (Story/StoryDirector.cs, 388줄, score 2) — 싱글턴 참조 2
- [ ] GameConstants (Core/GameConstants.cs, 86줄, score 0) — 표면 점검
- [ ] PlayerProgressUIController (Core/PlayerProgressUIController.cs, 119줄, score 0) — 표면 점검
- [ ] InsectElement (Data/InsectElement.cs, 96줄, score 0) — 표면 점검
- [ ] InsectExpansionDefinitions (Data/InsectExpansionDefinitions.cs, 146줄, score 0) — 표면 점검
- [ ] InsectLoreService (Data/InsectLoreService.cs, 105줄, score 0) — 표면 점검
- [ ] ItemRarityPalette (Data/ItemRarityPalette.cs, 135줄, score 0) — 표면 점검
- [ ] NpcDialogueDatabase (NPC/NpcDialogueDatabase.cs, 167줄, score 0) — 표면 점검
- [ ] NpcVisualBuilder (NPC/NpcVisualBuilder.cs, 333줄, score 0) — 표면 점검
- [ ] PlayUIRefs (UI/PlayUIRefs.cs, 119줄, score 0) — 표면 점검

score 0은 OnGUI/Update 표면이 없는 데이터·정의 파일 — 표면 점검만(P0/P1 가능성 낮음).
큐가 비면 `python -X utf8 .claude/scripts/audit_candidates.py --emit-md`로 재생성.


## Round Log

최근 3건만 둔다. 전체 이력은 `.claude/audit-archive/round-log-2026H1.md`.

> 이 로그는 **쓰기 전용**이다 — audit 스킬 Step 4가 추가하지만 어느 단계도 읽지 않는다.
> 그런데 Step 1이 이 파일을 통째로 Read하므로 매 라운드 컨텍스트를 먹었다(17KB, 파일의 73%).
> 영역별 처리 이력은 위 Covered 인덱스가 이미 갖고 있다. 길어지면 아카이브로 옮길 것.

- 2026-07-17: **3라운드 병렬** (QuickAccessBarUI / SocialPvpUI / SceneAutoWire) — P0:2, P1:2 처리 + clean 1. **QuickAccessBarUI P0**: 이 파일은 그동안 *모달 가드 관례의 근거*(:113)로 인용돼왔는데 그 가드가 **모바일 렌더 경로에만** 있었다. 핫키 경로는 `IsInputBlocked()`(battle/raid/frozen)만 거치는데, 주석의 전제 "모든 모달이 SetFrozen을 거니 frozen으로 커버된다"가 깨져 있었다 — SocialPvpUI·WorldFieldMultiplayerUI는 모달 등록만 하고 SetFrozen을 안 부른다. 결과: 친구코드·채팅 입력에 N/T/G/C/Q/M/V/P를 치면 **글자마다 화면이 토글**되고, e.Use()가 그 글자를 삼켜 **입력조차 안 됐다**. TryHotkey(index)→bool 신설로 막힌 키는 e.Use()를 하지 않게 하고, 열린 화면 자신의 키는 통과시켜 토글 유지. 렌더 경로엔 가드를 넣지 않았다(넣으면 데스크톱 바가 사라지고 active 하이라이트가 죽는 회귀). **SocialPvpUI P0**: `GUI.enabled = !IsBusy && teamReady`가 "매칭 취소"까지 덮어, 큐 대기 중 팀을 바꾸면 취소가 죽었다. queued는 서버 권위값이라 재접속해도 유지 → **영구 감금**. AccountLinkUI·AccountSettingsUI에 이은 **세 번째 동일 패턴**. **SocialPvpUI P1:2**: (a) OnStateChanged가 level-trigger라 매치 중 폴링(2.5초)마다 탭이 배틀로 튕겨 친구/랭크 탭에 머물 수 없었다 → edge-trigger화. (b) HasValidTeam을 OnGUI마다 호출해 BuildTeamSnapshot이 매번 List·배열(전부 class = 진짜 힙 할당)을 만들고 팀 미완성 시 **예외까지 반복** → 0.5초 스로틀 캐싱. **SceneAutoWire clean**: "미캐싱 조회 40건"이 이중 거짓양성 — 40건 전부 Awake 하나에 있고(파일이 통째로 Awake), 게다가 **이 컴포넌트는 어디에도 붙어있지 않아 Awake가 실행되지 않는다**(GUID 참조 0, 씬 MonoBehaviour는 Bootstrap 하나). 즉 **파일 전체가 dead code**이며 Bootstrap이 배선을 전부 흡수했다. 삭제 권고(P2) — 방치 시 누가 붙이면 이중 배선되는 잠복 지뢰이고, verify_coverage.py:12/architect.md:20/architect.toml:10이 활성 파일로 등재 중이라 동반 정리 필요. **채점기 실패 모드 확정**: "프레임 할당"(6라운드 연속)과 "미캐싱 조회"(이번) 둘 다 **실행 컨텍스트를 보지 않고 구문만 세는** 같은 병 — Awake 스코프·씬 참조 필터가 필요하다. 검증: error CS 0건.
- 2026-07-17: **3라운드 병렬** (VillageBuilder / NpcManager / NpcDialogueUI) — P1:4, P2:1 처리. **NpcManager P1**: 스폰 캡(주민 10, 아이 6)이 VillageBuilder가 저작한 앵커 수(14, 7)보다 작은데 SyncSpawns가 앞에서부터 잘라 쓴다. 본마을 8개가 먼저 들어가고 전초기지가 리전 순서로 뒤에 붙으므로 **swamp·mountain·garden·ruins 전초기지 4곳에 오두막·모닥불은 지어지는데 주민이 영구히 0명**이었다(매 세션 1회 예외 없이 실행). 캡을 14/7로 상향. **동반 수정**: GameplayTuningProfile 기본값도 10/6이라, 지금은 프로필 에셋이 없어 ApplyTuning이 죽은 경로지만 누군가 에셋을 만드는 순간 NpcManager:131-132가 덮어써 재발한다 → 프로필도 14/7로. **VillageBuilder P1:2**: c.y 이중 합산 가설은 반증(Polar가 Y를 0만 더하고 WorldTerrainBuilder가 전 리전 평탄화). 대신 "지면을 Y=0으로 가정한" 계열 2건 — (a) 광장 상면이 정확히 0.08로 Region_ 평면(Bootstrap:1265가 Y=0.08 배치)과 **같은 평면**이라 지름 16m 원반이 z-fight/소실, y 0.03→0.10. (b) 9개 건물 중 **상점만 Roof 프리미티브가 없어** 간판이 벽 상단 위 0.3m 허공에 떴다(훈련소는 Roof 상단=간판 하단 밀착이 관례) → Roof 추가로 벽·간판 양쪽 밀착. P2 동반: TentCamp GroundMat도 같은 원인으로 매몰(0.055<0.08) → 0.12. **NpcDialogueUI P1:1**: 7연속 모달 소프트락 패턴은 여기 없다(각 가설 반증 — GetLines가 항상 3개 반환, Show가 null을 isOpen 전에 차단, NpcManager가 SetActive(false)해도 Update:78이 잡아 CloseModal, GUI.enabled 미사용). OnDisable이 Unregister만 하고 isOpen을 안 되돌려 재활성 시 ESC 영구 무력화 + 그 주민과 영구 대화 불가가 되는 latent 버그만 수정(CloseModal 위임). 옛 주석이 인용한 CaptureChoiceUI 관례는 이미 두 번 폐기된 방식이다. 검증: Unity PlayMode 38/38, error CS 0건.
- 2026-07-17: **3라운드 병렬** (CatcherKidNpc / VillagerNpc+NpcWalkAnimator / Capture아이템 3종) — P1:1 처리 + clean 2, 채점기 구멍 1건 수정. **CatcherKidNpc P1**: Rare+ 곤충이 12m 안에 있으면 아이가 영구 고착했다 — 스캔이 잡기/구경 후보를 한 루프에서 최근접 하나로 뽑는데 Rare+는 CanKidTarget을 우회해 무조건 후보가 되므로, 최근접이 Rare면 Watch(2~4s)→Idle→재스캔→같은 Rare가 여전히 최근접→Watch로 순환한다(Wander 불가, 포획 영구 중단). 같은 원인으로 Rare 뒤의 Common도 bestSq에 밀려 영영 가려졌다. 탈출은 그 곤충이 60m 밖으로 사라질 때뿐이라 플레이어가 근처면 무기한. 수정: bestCatch/bestWatch 분리(잡기 우선) + lastWatchedInsect로 직전 대상 제외, **해제는 Wander 완주 시점**(진입 시점에 풀면 그 케이스 스캔이 곧바로 같은 곤충을 다시 잡아 고착 재발). 상태 전이 모사로 검증: before는 Watch↔Idle 무한·포획 0회, after는 Watch→Idle→Wander→완주→Idle 순환이며 Common 동반 시 40틱 20회 포획. **공정성은 clean** — SetEngaged/PlayerClaimRadius 3중 방어로 플레이어 미니게임 중 곤충은 가로챌 수 없고, 유령 곤충도 없으며, 쿨다운 하한은 [Range]가 강제. **VillagerNpc+NpcWalkAnimator clean**: Talking 고착 3경로 전부 반증(비활성 GO에서도 EndTalk은 C# 호출이라 정상 동작, 씬 전환은 OnDisable→CloseModal, 대화 중 SetFrozen이라 이탈 불가). 좌표도 MaxGroundStep 클램프가 지붕 오인 차단. **Capture 아이템 3종 clean(dead code)**: PlaySceneBootstrap:335-338이 주석으로 꺼놨고(`// 필드 아이템 스폰 비활성화 — 아이템은 샵/보상에서만`) 씬 GUID 역참조도 0건이라 필드 아이템 흐름 자체가 없다. CaptureProximityTrigger.TryStartCapture도 호출자 0(실제 포획은 CaptureInputController가 재구현) — 단 position/radius 제공용으로 살아있어 삭제 불가. **이 라운드가 채점기 구멍을 드러냈다**: code_mentions가 raw text를 읽어 **주석 안의 언급도 살아있음으로 셌다** → strip_cs 적용(같은 커밋). 1단계 참조만 보는 한계는 주석에 명시. 검증: Unity PlayMode 38/38, error CS 0건.
- 2026-07-19: HospitalUI (신규 병원 치료 UI, 이번 세션 추가) — clean, P0/P1 0건. audit_candidates score 36(프레임할당9/싱글턴9)이 **둘 다 거짓양성**: `new GUIStyle`은 stylesReady로 1회 캐시, `new Rect/Color`는 struct(스택 할당), 싱글턴 역참조는 렌더 경로 전부 삼항 가드 + `UITheme.Instance`는 CreateInstance 폴백이라 null 불가. 이 프로젝트 반복 버그 7종(모달 ESC 무력화·이벤트 짝·결제 원자성·stale 아이템·미캐싱 조회) **전부 회피** — TrainingUI 패턴을 정확히 따름. **P2:1 보류**(OnDisable은 InsectUpdated 해지하나 OnEnable 재구독 없음 — Hospital GO가 SetActive 토글 안 돼 현재 미발현). 이 세션 빌드 블로커 2건 동반 수정: `InsectDatabase.FindById`→`GetById`(컴파일 CS1061), 신규 테스트 4개 `#if UNITY_EDITOR` 가드(IL2CPP nunit 링크 실패). 검증: error CS 0건, APK 112MB 빌드 성공.
