# Audit 진척

`/audit` 스킬과 훅 2개(`audit_flow_inject`, `audit_reminder`)가 읽고 갱신합니다.
**구조 유지 필수** — 특히 `## Uncovered`의 `- [ ]` 개수가 자동 플로우의 트리거입니다.

- **Covered**: 처리 완료 영역 인덱스. 서술 원문은 `.claude/audit-archive/covered-detail.md`
- **Uncovered**: 다음 audit 후보 큐. 위에서 아래로 우선순위.
  **비면 `python -X utf8 .claude/scripts/audit_candidates.py --emit-md`로 재생성**
- **Round Log**: **최근 5건만** 둔다(이 줄이 개수의 단일 출처). 넘치면
  `.claude/audit-archive/round-log-2026H2.md`로 이관 — 아카이브는 날짜순 정렬

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
- [x] GameConstants (P0:0, P1:2, 2026-07-22) — 6스킬 상수의 stale 테스트를 6/7번째 기준으로 정정하고 story_progress.json을 SaveScope v4 마이그레이션·삭제 대상에 등록
- [x] PlayerProgressUIController (P0:0, P1:1, 2026-07-22) — 런타임 텍스트 참조 바인딩 후 현재 레벨·XP·사탕 값을 즉시 다시 그리도록 Refresh 경로 추가
- [x] InsectElement (clean P0/P1, 2026-07-23) — 전체 enum 상성 매핑, 이중 타입·중복 타입 처리, 미등록 enum 중립 폴백 및 1v1·레이드 호출부의 None 폴백 확인
- [x] InsectExpansionDefinitions (clean P0/P1, 2026-07-24) — 신규 ID 64개의 유일성·기존 ID 비중복·리전/서브에리어 풀 배정과 insectId 저장 호환성 확인
- [x] InsectLoreService (clean P0/P1, 2026-07-27) — DB/Lore ID 128/128 일치, 중복·누락 없음. AutoWire 순서와 정적 캐시 초기화·조회 경로 확인
- [x] NpcDialogueDatabase (clean P0/P1, 2026-07-31) — RegionLines 키 7개가 RegionManager 진행 체인의 리전 ID 7개와 완전 일치. FNV-1a 해시·음수 Mod·null 폴백 안전, GetLines/GetVillagerName은 대화 시작·NPC 생성 시 1회 호출(프레임 할당 아님)
- [x] ItemRarityPalette (clean P0/P1, P2:1 처리, 2026-07-31) — ItemRarity 5값 ↔ switch 6개 정합. `.asset` 인스턴스 부재로 `Resources.Load`가 항상 null이었으나 소비자 3곳 전부 null 가드 + 하드코딩 폴백이라 무해했음. **P2 처리**: `ItemRarityPaletteBuilder`(에디터)로 `Resources/ItemRarityPalette.asset` 생성 + 그리드 아이템 프리팹에 팔레트 주입
- [x] PlayUIRefs (P1:1, 2026-07-31) — 90필드 ↔ 생성기 77대입 ↔ WireFromRefs 88읽기 대조. 미채움 13개 중 12개는 Text/TMP 이중화(의도된 설계)이나 `itemRarityPalette`만 짝 없이 비어 프리팹 경로에서 팔레트가 null → PlayUIPrefabGenerator에 로드 1줄 추가
- [x] NpcVisualBuilder (clean P0/P1, 2026-07-31) — NpcWalkAnimator가 Find하는 노드명 8개 전부 일치(직계 자식), 머티리얼 생명주기 3스폰 경로 모두 OnDestroy에서 CleanupMaterials 커버. NpcManager가 NPC를 파괴하지 않고 SetActive로만 토글하므로 OnDisable이 아닌 OnDestroy에 건 것이 정확
- [x] OpeningSceneController (P1:2, P2:1 처리, 2026-08-03) — score 48 "프레임 할당 16" 중 진짜는 `new GUIContent` 3개뿐(나머지 Rect/Color는 struct). 타이틀 구간 CalcSize 3회/패스를 화면크기 키로 메모이즈, OnDestroy가 재생 중인 AudioClip을 언로드하던 순서 수정, 매 프레임 PlayerPrefs 조회 제거
- [x] UISurface (P1:1, **P2:3 전부 처리**, 2026-08-03) — 스타일 헬퍼 6종이 `CachedStyle("리터럴"+fontSize, 캡처 람다)` 형태라 호출마다 문자열+클로저+델리게이트 3개 할당(적중해도). int 키 캐시 하나로 통합 + 폰트 크기 clamp. **P2 후속 완료**: ①`EvictAll`이 `Object.Destroy`로 텍스처를 파괴하고 `GetRoundedStyle`을 private으로 닫음(UISurface.cs:101-111) ②`Rounded`가 앰비언트 알파를 보존 ③얇은 것 전용 `Flat` 신설, `HospitalUI:155,241`이 사용

- [x] RaidRoundResolver (clean P0/P1, P2:1 보고, 2026-08-03) — 순수 정적 리졸버라 checklist 7항목 전부 해당 없음. 1v1과의 계약 대조에서 **버프·디버프가 레이드에선 영구 누적**(effectDurationTurns 미사용, RecalculateBonuses 부재)임을 확인. **P2 종결(2026-08-03 사용자 결정: 현행 유지)** — 이후 도입된 `MaxBuffStacks=3`이 방향별 상한을 줘 최악(보스 공격 0.3배 고정)이 유한해졌고, 만료를 넣으려면 장비 보너스와 전투 버프가 한 필드에 섞인 구조부터 분리해야 해 레이드 난이도가 실측 없이 오른다. 의도된 divergence로 코드 주석에 못박음

- [x] RaidRoundModels (P1:1, 2026-08-03) — 호출부 0인 `SetBossDamage`가 슬롯 기록 + TotalDamageToTeam 가산을 같이 해, 컨트롤러의 기존 합산과 겹치면 팀 피해가 두 배가 되는 함정이었다. 제거 + 재발 방지 주석

- [x] CaptureChanceCalculator (P1:1, 2026-08-03) — 레벨 차 상한(±5)이 전투 경로에만 있고 미니게임 경로엔 없어, 메인 필드가 항상 Lv.1부터 스폰하는 탓에 고레벨 플레이어의 저레벨 전설 포획이 사실상 보장됐다. 두 경로를 같은 MaximumLevelDelta로 통일

- [x] QuestRewardFormatter (P1:2, 2026-08-03) — GrantRewards가 적용하는 rewardInsectLevel이 표시에서 누락(q_approach의 Lv.6 장수풍뎅이가 종만 표시)되던 것 + HasAny가 포함 조건을 복사해 두 곳에 있던 것을 공유 술어로 통합

- [x] DexBrowseLayout (P1:1, **P2:1 처리**, 2026-08-03) — WrapIndex는 정확했으나 호출부가 항상 ±1이라, 좌측 1열 리스트→전체 폭 그리드 개편 뒤 ↑↓가 아래 행이 아니라 옆 칸으로 갔다. 행 이동을 lastListColumns로 수정. **P2 후속 완료**: 호출부 0인 `FormatElementLabel` 제거(테스트도 함께, 대신 생산 코드가 쓰는 `ShouldShowSecondary`를 직접 고정) + 보유 탭의 `floor(panelW/410f)` 자기 공식을 `GetGridColumns`로 통일(폭 300~1400 중 1229 한 곳만 2→3열로 1px 빨라짐)

- [x] OpeningReplayCoordinator (P0:1, P1:2, 2026-08-03) — UI 루트 SetActive 토글이 BattleScreenUI/RaidBattleUI/RegionMapUI의 OnDisable 해지를 되살리지 않아, 오프닝 다시보기 한 번에 배틀·레이드 화면이 영구 먹통. OnEnable 재구독 + subscription_lint 신설로 CI 고정

- [x] OpeningSequenceState (clean P0/P1, 2026-08-03) — 타임라인 상수 8개 ↔ 이미지 인덱스/블렌드 전 구간 정합, Complete 이중 발화 가드, 스킵이 현재 페이드를 이어받는 처리, 시계 clamp·역행 방어, 게이트 armed 전이 전부 확인. 파일 수정 0건

- [x] UIDirectScroll (P1:1, 2026-08-03) — 클래스엔 z-order 개념이 없는데 도감 개편으로 상세 모달이 그리드 위에 겹치면서, 먼저 그려지는 그리드가 모달 위의 휠·드래그를 Use()로 가로채 상세가 스크롤 불가(터치는 두 인스턴스가 같은 손가락을 잡아 동시 스크롤). Handle에 interactive 매개변수 추가 + 겹친 배경은 입력 포기

- [x] UITween (P1:1, 2026-08-03) — `Evaluate`가 호출마다 `elapsed`를 더했는데 유일한 호출부가 `OnGUI`(프레임당 Layout+Repaint+입력마다 1패스)라 0.2초 페이드가 2배 이상 빨랐고 마우스를 움직이면 더 빨라졌다. `TweenHandle.lastFrame`으로 프레임당 1회만 전진. 이징 5종의 0→0/1→1 계약을 테스트로 고정

- [x] UISafeLayout (clean P0/P1, **P2:1 처리**, 2026-08-03) — 좌표계 계약(Px ↔ UIScale.Begin 미사용 / 가상 ↔ Begin 사용) 34개 파일 전수 일치, Begin/End 전 경로 균형 확인(불일치로 보인 6건은 주석 2 + 조기 return 4). **P2 후속 완료(문서화로 종결)**: 미사용 4멤버를 `rules/ui-layout.md` API 표에 올리고, `UISafeLayout.ContentWidth`(MarginX 24 고정판)와 `UIScale.ContentWidth(margin)`(일반판)이 중복이 아님을 명시 — 삭제하면 ContentTop/Bottom/Height만 남아 비대칭이라 누가 다시 넣는다. `Overflows`는 원래 표에 있었다

- [x] InsectBattleStats (clean P0/P1, 2026-08-03) — 부호 있는 스택 카운터(±MaxBuffStacks)가 반대 방향 전환을 막지 않는지, `protected set`이 `RaidBossStats` 상속 때문에 필요한지 확인. **핵심은 보너스 이중 적용 여부** — `DefenseBonus`는 `ApplyDamage` 안에서만, `AttackBonus`는 호출부 3곳에서만 곱해져 겹치지 않는다
- [x] InsectSizeCalculator (clean P0/P1, 2026-08-03) — FNV-1a 음수 방어(`Abs(int.MinValue)` 회피), `-1` 센티넬 → instanceId 해시 폴백, 무게가 길이 배율의 세제곱인지, 라벨 포맷 경계(10g/1000g, 100mm) 전부 확인
- [x] WeeklyContestSchedule (clean P0/P1, 2026-08-03) — 주차 경계(604800 전후)·음수 시각·빈 풀 폴백, insectId 사전순 정렬로 기기 간 같은 대상 종 보장, 티어 임계 3단 정합 확인
- [x] WeeklyContestManager (clean P0/P1, 2026-08-03) — 포획 전용 `InsectCaptured` 구독(치료·레벨업 오발화 회피), World 루트라 SetActive 토글 무관(Start↔OnDestroy 페어링), PlayerPrefs 키가 `int.TryParse`만 써 culture 무관, 주차 캐시로 128종 정렬 반복 회피 확인
- [x] NpcDuelController (P1:1, 2026-08-03) — 도주 시 `DuelEnded`가 발화하지 않아 90초 재도전 쿨다운이 통째로 우회됐다(주석은 "결과와 무관하게"라고 적혀 있는데 한 결과가 빠져 있었음). `TryEscape`에 발화 추가 + 풀 인덱스의 `Abs(int.MinValue)` 음수 방어를 순수 함수로 분리해 테스트 고정

- [x] InsectBattleController 재감사 (P1:1, 2026-08-03) — `RecalculateBonuses()`가 전투 **시작** 경로에 없어 의상·아이템 ATK/DEF 보너스가 첫 턴에 적용되지 않았다. 하필 `SeedPersistentState`가 감염 곤충일 때만 `AddEffect`→Recalculate를 태워서 "독에 걸려 있어야 의상 보너스가 켜지는" 상태. `BeginBattleCommon` 끝에 호출 추가

- [x] PlaySceneBootstrap 재감사 (clean P0/P1, 2026-08-03) — 감사 이후 2107줄 변경분 대상. EnsureComponent 경로 87개·AutoWire 오버로드·리플렉션 주입 154건·Resources.Load·부팅 canary·신규 등록 3종 전수 대조. 파일 수정 0건

- [x] RegionMapUI 재감사 (P1:1, 2026-08-04) — 도감 브라우저가 항목마다 매 OnGUI 패스에 `$"등급 | CP n"`(보간+enum 박싱+CP 재계산)과 `new string('?', len)`을 새로 만들었다. 둘 다 종 데이터 파생이라 불변 — insectId·이름 길이로 캐시

## Uncovered (우선순위순)

**2026-08-03: `audit_candidates.py`가 재감사 후보도 낸다.**

예전엔 `stem in reviewed`(원문 substring)로 **이름이 한 번이라도 문서에 스치면 영구 제외**했다.
"처리했는데 그 뒤로 바뀐 파일"이라는 개념이 없어서, 감사 이후 수정된 파일이 76개인데도
"후보 0건"을 냈다. 그 사각지대에 있던 `InsectBattleController`(2026-05-20 감사 후
StartDuel·버프 상한·DuelEnded 유입)를 손으로 꺼내 돌렸더니 곧바로 P1이 나왔다 —
의상·아이템 보너스가 전투 첫 턴에 안 붙는 결함이었다. 그래서 스크립트를 고쳤다.

이제 진척 문서의 **항목 머리**에서 감사일을 읽고(프로즈에 스친 이름은 세지 않는다),
git numstat으로 그 이후 변경량을 재서 `MIN_RECHURN`(40줄) 이상 바뀐 파일을 되살린다.
**재감사 우선순위는 점수가 아니라 변경량이다** — 점수는 파일의 성격을 말할 뿐
"감사 이후 무엇이 달라졌는가"를 말하지 않는다. git을 못 읽으면 재감사를 건너뛰고
신규 후보만 내며 그 사실을 출력에 적는다.

아래는 그 스크립트가 뽑은 큐다(`--emit-md --top 6`). 전체 재감사 후보는 55개.

- [ ] DexScreenUI 재감사 (Dex/DexScreenUI.cs, 1538줄, score 250) — 2026-05-20 감사 이후 2026-08-03까지 1797줄 변경
- [ ] RaidBattleUI 재감사 (UI/RaidBattleUI.cs, 3537줄, score 376) — 2026-05-27 감사 이후 2026-08-03까지 1609줄 변경
- [ ] BattleScreenUI 재감사 (UI/BattleScreenUI.cs, 3633줄, score 372) — 2026-05-27 감사 이후 2026-08-03까지 1586줄 변경
- [ ] SubAreaWorldBuilder 재감사 (Core/SubAreaWorldBuilder.cs, 1212줄, score 45) — 2026-05-27 감사 이후 2026-08-03까지 1309줄 변경

score 0은 "OnGUI/Update 표면이 없다"는 뜻일 뿐 clean이라는 뜻이 아니다 —
직전 10라운드가 거의 전부 score 0이었는데 P0 1건·P1 6건이 나왔다.
큐가 비면 `python -X utf8 .claude/scripts/audit_candidates.py --emit-md`로 재생성.

(`verify_coverage.py`는 미할당 0 — 담당 에이전트 매핑은 별개로 완전하다.)

## Round Log

보존 개수는 **파일 상단 안내(9줄)가 단일 출처**다 — 여기에 사본을 적지 않는다.
넘치는 항목은 `.claude/audit-archive/round-log-2026H2.md`로 옮긴다(그 이전은 `-2026H1`).

> 이 로그는 **쓰기 전용**이다 — audit 스킬 Step 4가 추가하지만 어느 단계도 읽지 않는다.
> 그런데 Step 1이 이 파일을 통째로 Read하므로 쌓아두면 매 라운드 컨텍스트를 먹는다.
> 영역별 처리 이력은 위 Covered 인덱스가 이미 갖고 있다.
>
> 개수를 두 곳에 적었다가 실제로 어긋났다 — 상단은 "최근 10건", 여기는 "최근 3건만 둔다"라고
> 서로 다른 말을 하는 동안 23건이 쌓여 47KB가 됐다(2026-08-03 정리).

- 2026-08-03: WeeklyContestManager (score 0 표면 점검) — **clean, P0/P1 0건. 파일 수정 없음.** 이벤트는 `InsectCaptured`(포획 전용)를 구독한다 — `InsectUpdated`였다면 치료·레벨업·진화에도 울려 기록 토스트가 오발화했을 자리이고, StoryDirector가 같은 이유로 이미 옮겨간 이벤트다. **World 루트 소속이라 UI 루트 SetActive 토글과 무관**하므로 `AutoWire`↔`OnDestroy` 페어링이 정확하다(`subscription_lint`의 검사 대상도 아니다). PlayerPrefs 값이 `"주차:등급"` 문자열이고 파싱이 `int.TryParse`뿐이라 **culture 의존이 없다**(소수점 포맷이 끼어들 여지가 없음). `ResolveTarget`이 주차 캐시로 128종 정렬을 조회마다 돌리지 않고, `TutorialQuestUI`가 OnGUI에서 `WeeklyContestTarget`을 읽어도 캐시 적중 경로만 탄다. **관찰(수정 안 함)**: 풀이 비면(`cachedTarget == null`) 매 조회마다 재정렬하는데, `InsectDatabase`에 Common이 반드시 있어 도달하지 않는다.
- 2026-08-03: NpcDuelController (score 1, 큐 1순위) — **P1:1 처리 + 방어 1건**. 채점 근거 "싱글턴 참조 1"은 거짓양성(`TutorialQuestManager.Instance?.NotifyNpcDuelWon()` — null 조건 연산자). **P1: 도주하면 대결 쿨다운이 통째로 우회된다.** `DuelEnded`는 `CheckEnd`의 승리(`InsectBattleController:718`)·전멸패배(`:751`) 두 경로에서만 발화하는데, `TryEscape`(`:229` 부근)는 `battleEnded = true; BattleEnded?.Invoke(false)`만 하고 끝난다. 그래서 도망치면 `OnDuelEnded`가 안 돌아 `kid.MarkDuelFinished`가 호출되지 않고, **같은 아이에게 즉시 재도전**할 수 있다 — `DuelCooldownSeconds` 주석이 "결과와 무관하게 … 연속 파밍 차단"이라고 설계 의도를 못박아 두었는데 정작 한 결과가 빠져 있었다. 도주 성공률이 레벨차에 따라 50~90%라 쉽게 닿는 경로다. `TryEscape`에 `if (duelMode) DuelEnded?.Invoke(false);` 추가(승리/패배와 같은 형태). 부수 효과로 `activeKid`가 도주 후 남아 있던 것도 함께 해소된다. **방어 보강**: `EnsureDuelInsect`가 `Mathf.Abs(StableHash(kid.NpcId))`로 시드를 만드는데 `Abs(int.MinValue)`는 그 자신(음수)이라 `pool[음수]`로 즉사한다 — 같은 저장소의 `InsectSizeCalculator.RollFromInstanceId`와 `NpcDialogueDatabase.Mod`가 이미 쓰는 방어인데 여기만 빠져 있었다(확률은 2^-31이지만 한 줄이고 하우스 패턴이 이미 존재). 순수 함수 `PoolIndexFor(npcId, poolLength, attempt)`로 분리해 long 양수화 + 빈 풀·null ID 폴백을 넣고 테스트 3건으로 고정. **거짓양성/무해 확인**: `AutoWire`의 `DuelEnded` 구독은 `battleController == null` 가드 안이라 이중 구독 불가, `activeRarity`는 `TryStartDuel` 시점 스냅샷이라 대결 중 아이 상태가 바뀌어도 보상 등급이 흔들리지 않는다, `FindPlayerLeader`가 팀·보유 양쪽에서 기절 곤충을 거른다. 검증: error CS 0, ci_check 6검사 통과, PlayMode 268/268(신규 3케이스 포함).
- 2026-08-03: InsectBattleController **재감사** (2026-05-20 최초 감사, 이후 StartDuel·버프 상한·DuelEnded 유입) — **P1:1 처리**. 이 라운드는 `audit_candidates.py`가 "후보 0건"을 낸 상태에서 시작했다 — 스크립트가 `if stem in reviewed: continue`로 **이름 기반 영구 제외**를 하기 때문이다("처리했는데 그 뒤로 바뀐 파일" 개념이 없다). 실측하니 감사 이후 수정된 파일이 **76개**였고, 그중 구조 변경이 가장 큰 이 파일을 1순위로 잡았다. **P1**: `RecalculateBonuses()`가 `AddEffect`(510)·`TickEffects`(558)·`SwapPlayerInsect`(853) 세 곳에서만 불리고 **전투 시작 경로에는 없었다**. `InsectBattleStats` 생성자가 `AttackBonus/DefenseBonus = 0`으로 두므로, `UseSkill`/`UseBasicAttack`의 순서(데미지 계산 → 적 반격 → `TickEffects`)상 **첫 턴에 의상·아이템 전투 보너스가 통째로 빠진다** — 플레이어의 첫 공격은 `GetDamage`의 `1 + AttackBonus`가 1.0이 되고, 적의 첫 반격은 `ApplyDamage`의 `def × (1 + DefenseBonus)`를 못 받는다. 야생 전투가 대개 2~4턴이라 소모품으로 산 효과의 큰 몫이 증발한다. **발현 조건이 결정적 증거였다**: `SeedPersistentState`가 **감염된 곤충일 때만** `AddEffect`를 부르고 그게 `RecalculateBonuses`를 태우므로, 독에 걸린 곤충만 첫 턴부터 보너스를 받았다(건강하면 못 받음). **교차 대조**도 같은 방향 — `SwapPlayerInsect`는 이미 `RecalculateBonuses()`를 부르고(교체 곤충은 정상), 레이드는 `RaidBattleController:111-113`이 시작 시 직접 대입한다(정상). 1v1 시작 경로만 빠져 있었다. `BeginBattleCommon` 끝에 호출 추가(`outfitBonus`/`itemEffects` null 가드가 메서드 안에 이미 있어 AutoWire 전 호출도 안전). **거짓양성/무해 확인**: `duelMode`는 `StartBattle`/`StartDuel` 양쪽이 명시 대입해 이전 대결 상태가 새지 않고, `BeginBattleCommon`이 `enemyEntity` 대입 **전에** 도는 것도 의도대로다(호출부가 뒤에 채운 뒤 이벤트를 울려야 `BattleScreenUI.OnBattleUpdated`의 `GetEnemyEntity()`가 유효하다). `CheckEnd`의 듀얼 분기는 포획 롤·야생 드랍을 정확히 건너뛴다. **테스트**: 호출 순서 문제라 씬 없이 검증 불가 — `rules/testing.md`의 MonoBehaviour 생명주기 제외 대상이고, 배율 공식 자체는 `BuffStackCapTests`가 이미 고정한다. 검증: error CS 0, ci_check 6검사 통과, PlayMode 268/268.
- 2026-08-03: PlaySceneBootstrap **재감사** (2026-05-27 감사 이후 2107줄 변경, 새 재감사 큐의 1순위) — **clean P0/P1, 파일 수정 0건.** 4913줄을 줄줄이 읽는 대신 **계약을 기계적으로 대조**했다(감사 이후 바뀐 부분이 부트스트랩의 배선 계약을 깼는지가 이 파일의 실질 위험이다). ①**EnsureComponent 87경로/102생성** — 서로 다른 타입이 같은 경로를 쓰는 건 `World/CharacterOutfit`(CharacterOutfitManager + OutfitBonusProvider) 하나뿐이고 매니저와 그 보너스 제공자를 한 GameObject에 얹는 정상 패턴이다. ②**AutoWire 오버로드 커버리지** — 생성 타입 88개 중 정의됐는데 그 인자수로 안 불리는 건 2건뿐이고 둘 다 무해: `CharacterOutfitUI.AutoWire(manager)`는 2인자 오버로드(`AutoWire(manager, bonus)`, :466)로 이미 배선되고, `ItemRarityTuningUIController.AutoWire(palette)`는 리플렉션 주입을 쓰는 개발용 잔재다. ③**리플렉션 주입 154건 전수 검증** — `GetType().GetField("x")?.SetValue()`는 필드명이 어긋나도 **컴파일·런타임 모두 조용하다**(PlayUIRefs 라운드가 잡았던 결함 계열). 대상 타입의 실제 필드 목록과 대조해 **미해석 0건** 확인. ④**Resources.Load** — `ItemDatabase`가 애셋 부재로 항상 null이지만 바로 다음 줄 `if (itemDatabase == null) itemDatabase = ItemDatabase.CreateRuntimeDefault();`가 받는다(런타임 팩토리가 실제 단일 출처). ⑤**부팅 canary** — 2026-07-19 P0(`MaxEquipSlots`를 `MaxLearnedSkills`로 오검사해 부팅 차단)가 재발하지 않았고, 단언하는 두 불변식(`MaxEquipSlots == 4`, Leaf→Water 상성 > 1)이 실제로 성립한다. 개별 곤충 데이터 결함은 throw가 아니라 로그+보정이라 한 종의 회귀가 게임 전체를 막지 않는다(그 P0의 교훈이 반영된 형태). ⑥**이번 세션 신규 등록 3종** — `WeeklyContestManager`(:485)·`NpcDuelController`(:565)의 AutoWire 인자가 전부 앞서 만들어지고, `ApplySizeProfile`이 `ScriptableObject.CreateInstance<InsectData>()` **두 경로 모두**(:2595→2610, :3008→3025)에 걸려 `baseSizeMm`을 못 받는 종이 없다(주간 대결 티어가 종 기준값에서 파생되므로 한 경로라도 빠지면 그 종의 임계가 틀어진다). ⑦**구독 시점 대 세이브 로드** — `weeklyContest`가 구독하는 `InsectCaptured`는 `AddInsectInternal`에서만 발화하고 `LoadAndIndex`(Awake)는 그 경로를 타지 않아, 세이브 로드가 보유 곤충 수만큼 포획 이벤트를 쏘는 오발화가 없다. **거짓양성 확인**: score 228의 `inst=158`(싱글턴 참조)은 전부 가드 안이다 — 유일한 무가드 후보였던 `AudioManager.Instance.PlayBGMForRegion`(:413)도 **앞줄**에 `if (region != null && AudioManager.Instance != null)`가 있다(한 줄 단위 스캔의 한계). **검사하지 않은 것**: 절차 생성 지오메트리·좌표값과 4913줄의 라인 단위 로직은 이 라운드 범위 밖이다 — 계약과 델타를 봤다. 코드 변경이 없어 테스트는 재실행하지 않았다(직전 검증 상태 유지: error CS 0, ci_check 통과, PlayMode 268/268).
- 2026-08-04: RegionMapUI **재감사** (2026-05-27 감사 이후 2048줄 변경, 새 재감사 큐 2순위) — **P1:1 처리**. 이 파일은 **score 41로 낮은데 변경량은 2위**였다 — 옛 점수 정렬이었다면 한참 아래였을 자리이고, 새 큐가 아니었다면 안 열렸을 라운드다. **P1**: `DrawDexItem`이 곤충 하나당 매 OnGUI 패스에 문자열을 새로 만든다 — 포획됨이면 `$"{data.rarity}  |  CP {CalculateBasePreview(...)}"`(보간 + enum 박싱 + **전투력 재계산**), 미포획이면 `new string('?', displayName.Length)`. 리전당 곤충이 20여 마리이고 OnGUI는 프레임당 Layout+Repaint+입력마다 돌며, IMGUI 스크롤뷰는 가상화가 없어 화면 밖 항목도 전부 실행된다. 둘 다 **종 데이터에서만 파생돼 세션 내내 불변**이므로 `insectId`(정보줄)와 이름 길이('???')로 캐시했다. **회귀 아님을 확인**: 처음엔 "2026-05-27 캐시 라운드(P1:28)가 7월 전면 재설계(70adc8e)로 되돌아갔다"고 의심했으나, 재설계 **직전** 버전(`70adc8e^`)을 꺼내 보니 같은 자리에 같은 코드가 있었다 — 그 라운드는 GUIStyle 28개를 다뤘고 문자열 할당은 원래 범위 밖이었다. 선재 결함이다. **거짓양성으로 판정(보고 안 함)**: `Label()` 헬퍼가 `wordWrap = false`를 전역으로 걸어(지도 핀 라벨은 1줄이 맞다) **지역 설명**까지 한 줄로 그린다. 방금 고친 글씨 잘림과 같은 계열로 보였으나 실측하면 설명 최대 38자 ≈ 912px에 박스가 972px(세로 모바일)이라 여유 7%로 들어간다 — 지금 잘리지 않으므로 보고하지 않는다. 다만 `UIHelper.LabelFit`은 `CalcHeight` 기반이라 **`wordWrap=false`의 가로 넘침을 못 잡는다**(폭 기준 축소는 `CalcSize().x`가 필요) — 설명이 한 줄만 길어지면 조용히 잘리고 헬퍼도 막지 못한다. 별건 판단 대상. **나머지 checklist**: `OnEnable`/`OnDisable`이 `RaidBossSpawned`를 해지-후-구독으로 짝 맞추고(이번 세션 P0 수정분), `EnsureAssets`가 `ready` 가드로 GUIStyle 26개+disc 텍스처를 1회만 만들며, `GetPlayer()`는 최초 1회만 Find한다. 렌더 경로에 남은 힙 할당은 패스당 1~2개(`$"도감 {caught} / {total}"` 등 항목별이 아닌 것)뿐이라 두었다. 검증: error CS 0, ci_check 6검사 통과, PlayMode 268/268.
