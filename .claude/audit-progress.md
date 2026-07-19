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
- [x] HospitalUI (clean P0/P1, P2:1 처리, 2026-07-19) — score 36(프레임할당9/싱글턴9) 둘 다 거짓양성 확인; 모달 ESC·결제 원자성·stale 아이템 등 회귀 7종 전부 회피(TrainingUI 패턴). P2(OnEnable 재구독) 후속 수정 완료
- [x] GuidedTutorialController (clean P0/P1, 2026-07-19) — score 15(프레임할당5) 거짓양성(Rect/Color struct + GUIStyle 1회 캐시); Start↔OnDestroy 페어링이라 SetActive 소실버그 무관, Dictionary 기반 진행이라 정지·범위초과 없음
- [x] StoryDirector (P1:1, P2:1 처리, 2026-07-19) — CaptureInsect가 InsectUpdated(포획+XP+치료+진화)를 오발화(치료경제가 악화) → 포획 전용 InsectCaptured 이벤트 신설(1884971). 싱글턴2 거짓양성(CloudSaveManager 가드 내). P2(렌더러 미배선 시 인트로 무음소모): deferredBeat 보류/구독 시 flush로 수정(c6f7fc4)

## Uncovered (우선순위순)

2026-07-19 세션에서 치료경제·전투·스킬 신규 코드 대량 유입 → 큐 재생성
(`audit_candidates.py --emit-md`). 1~3순위 HospitalUI·GuidedTutorialController·StoryDirector 처리 완료(Covered).
남은 후보는 전부 score 0(OnGUI/Update 표면 없는 데이터·정의 파일):

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

- 2026-07-19: HospitalUI (신규 병원 치료 UI, 이번 세션 추가) — clean, P0/P1 0건. audit_candidates score 36(프레임할당9/싱글턴9)이 **둘 다 거짓양성**: `new GUIStyle`은 stylesReady로 1회 캐시, `new Rect/Color`는 struct(스택 할당), 싱글턴 역참조는 렌더 경로 전부 삼항 가드 + `UITheme.Instance`는 CreateInstance 폴백이라 null 불가. 이 프로젝트 반복 버그 7종(모달 ESC 무력화·이벤트 짝·결제 원자성·stale 아이템·미캐싱 조회) **전부 회피** — TrainingUI 패턴을 정확히 따름. **P2:1 보류**(OnDisable은 InsectUpdated 해지하나 OnEnable 재구독 없음 — Hospital GO가 SetActive 토글 안 돼 현재 미발현). 이 세션 빌드 블로커 2건 동반 수정: `InsectDatabase.FindById`→`GetById`(컴파일 CS1061), 신규 테스트 4개 `#if UNITY_EDITOR` 가드(IL2CPP nunit 링크 실패). 검증: error CS 0건, APK 112MB 빌드 성공. **P2 후속**(7265b37): OnEnable에서 InsectUpdated 재구독 추가.
- 2026-07-19: GuidedTutorialController (가이드형 튜토리얼 오버레이, 이번 세션 신규) — clean, P0/P1 0건. score 15 "프레임 할당 5"는 거짓양성(`new Rect`×5·`new Color`×2는 struct 스택, `new GUIStyle`×2는 stylesInit 1회 캐시). 항목 1~7 전부 통과: Find는 Start 1회, null 가드 완비, **구독은 Start↔OnDestroy 페어링**이라 SetActive 토글 소실버그에 애초 해당 안 됨(HospitalUI와 다른 정당한 설계 — 씬 상주 오버레이). Dictionary 기반이라 스텝 범위초과 불가, OnQuestCompleted가 항상 ExitGuided로 clear해 영구정지·완료후잔존 없음. 코루틴 없음, 비모달(IsAnyOpen로 겹침 회피). 검증: error CS 0건.
- 2026-07-19: StoryDirector (스토리 트리거 디렉터, score 2) — P1:1 처리 + P2:1 보류. **P1**: OnInsectUpdated가 InsectUpdated(포획+XP+치료+진화 전부 발화)를 CaptureInsect 트리거로 매핑 → 이번 세션 치료경제가 발화지점(HealInsect/CurePoison/SetAfterBattle)을 늘려 pond/swamp/mountain에서 곤충 **치료만 해도** 빈-param 스토리 비트(ch2_water/ch4_bond/ch5_thesis)가 오발화. PlayerInsectCollection에 포획 전용 InsectCaptured 이벤트(AddInsectInternal에서만 발화) 신설, StoryDirector가 그걸 구독(1884971). 싱글턴 참조 2는 거짓양성(CloudSaveManager.Instance 동일라인 null 가드). Update/OnGUI/코루틴 없음, 이벤트 Start↔OnDestroy 페어링. **P2 보류**: Immediate 인트로 ch1_intro의 렌더러(NpcDialogueUI) 구독이 buildWorld+try 안이라 빌더 예외 시 구독 누락→헤드리스 CompleteBeat로 대사 미표시+보상 소모+oneShot 영구소실(buildWorld 기본 true라 정상 플레이 무해). 검증: ci_check 통과, error CS 0건. **P0 동반**(사용자 신고, 커밋 8cb7da8): 부팅 canary가 MaxLearnedSkills(6)를 오검사해 "핵심 시스템 초기화 실패"로 게임 중단 → MaxEquipSlots(4) 정정.
