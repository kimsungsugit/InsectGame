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

- [x] DexScreenUI 재감사 (P0:1, P1:2, **P2:3 후속 처리**, 2026-08-06) — 3D 썸네일 도입 접점의 결함. 그리드 2곳이 뷰포트 컬링 없이 전 항목을 돌아 24칸 LRU가 영구 스래싱(P0) + `GetInsectData`의 캡처 람다 O(N)(P1) + `OwnsShiny`가 매 패스 List 할당(P1)

- [x] RaidBattleUI 재감사 (P1:3, **P2:2 + 배너 복원 후속 처리**, 2026-08-06) — 결함 3건 모두 5f0776f의 라운드 파이프라인 교체가 남긴 것. 유나이트 종료 상한이 2.5s→1.15s로 바뀌었는데 2D 오버레이 타임라인은 2.5s 그대로라 5인 팀의 착탄 3건·최종 폭발·TOTAL이 전부 미렌더 + `wantMouseClick` 탭 래치 + TurnAnnounce 배너 기계장치 사장

- [x] BattleScreenUI 재감사 (P1:2, P2:1 보고, 2026-08-06) — 인트로/결과 탭이 **지난 전투의 `escapeRect`로** 소비돼 턴을 보기도 전에 도주가 실행되던 탭 래치(RaidBattleUI와 같은 계열) + `GetSkillColor`가 `SkillEffectType` 7종 중 3종만 처리해 신규 4종이 전부 회색

- [x] SubAreaWorldBuilder 재감사 (P1:1, 2026-08-06) — 서브에리어 진입마다 런타임 머티리얼 41개가 새고 있었다(`Destroy(subAreaRoot)`는 GameObject만 지운다). 25m 이탈·[E] 재진입으로 세션 내내 반복되는 경로라 누적된다

- [x] CharacterModelPreviewRenderer (P1:3, 2026-08-06) — 카드마다·패스마다 썸네일 키 문자열 생성(구조체 키로 교체) + 렌더 프레임마다 PlayerPrefs 4회(InvalidatePreview 배선) + 경계 계산의 배열 반환판(리스트 채우기판으로)

- [x] OutfitShapeLibrary (P1:2, 2026-08-06) — 슬롯 컨테이너 이름의 enum→문자열 할당을 미리 굽고, **프리뷰가 각도만 바뀐 프레임에도 옷을 통째로 다시 입히던 것**을 함께 차단(교차 수정)

- [x] RaidSupportPlanner (P1:2, 2026-08-06) — Stage 4가 버프를 팀 전체로 바꿨는데 플래너는 시전자 스택만 보고 있었고(쓸 수 있는 스킬을 버림), 보스 연속기절 면역도 몰라 저항당할 시도를 반복했다

- [x] CharacterOutfitData (P1:2, 2026-08-06) — `GetPrimaryBonusText`가 카드마다·패스마다 문자열을 만들고(바로 윗줄 GUIStyle 회귀는 막아뒀던 자리), 세트 별 표시가 덧붙이기 루프였다. 둘 다 캐시로 교체

- [x] UIShapes (P1:1, 2026-08-06) — 중간 roundness 혼합 수식의 **방향이 뒤집혀** 있었다(큐브가 타원, 원통이 사각형). 2D 의상 카드 폴백이 실제로 그 값을 넘기고 있었음

- [x] TutorialQuestUI 재감사 (P1:1, 2026-08-06) — 상세 패널이 열려 있는 동안 패스마다 List 2개를 만들고 퀘스트 31개를 다시 갈랐다. 분류가 불변이라 원본 배열 참조를 키로 캐시

- [x] TrainingUI 재감사 (**P0:1**, P1:1, 2026-08-06) — 보유 목록이 컬링 없이 전 개체에 3D 썸네일을 요청해 24칸 LRU가 영구 스래싱(도감 P0와 같은 구조) + 개체마다 정보 문자열 2개를 매 패스 생성

- [x] 3D 썸네일 목록 컬링 일괄 스윕 (**P0:4**, 2026-08-06) — `InsectVisual.Draw` 호출부 11곳 전수. 팀편성·보유곤충·병원·지역맵 4곳이 컬링 없이 전 항목을 돌아 24칸 LRU가 영구 스래싱. 단일 진입점에 규칙 명문화

- [x] BattleArenaController 재감사 (P1:1, 2026-08-06) — 스킬 연출마다 만든 런타임 머티리얼 31경로가 아무도 안 지워 전투가 길수록 누적. CleanupArena에서 일괄 파기

- [x] RaidBattleController 재감사 (P1:1 + 방어 1, 2026-08-06) — 오늘 레이드 5단계로 924줄 바꾼 파일 자체 감사. **회복 수치가 시전자 슬롯에 뜨던 것**(Stage 4에서 대상이 최저 HP 아군으로 갈렸는데 표시가 안 따라감)을 받은 슬롯으로 이전

- [x] LoginUI 재감사 (clean P0/P1, 2026-08-06) — score 183은 InitStyles 가드 안의 GUIStyle 40여 줄이라 거짓양성. 텍스처 3종 생성기 전부 정리 목록에 등록, 구독 짝·싱글턴 가드 정상. 파일 수정 0건

- [x] TutorialQuestManager 재감사 (clean P0/P1, 2026-08-07) — quest_lint 7 PASS. 세이브 5키 × 클라우드 4지점 완전, 구독/해지 6/6 대칭(**q_team 영구정지를 냈던 `battleTeamManager.TeamChanged` 라인 존재 확인**), Update는 조기이탈 3중 + Find 캐시. 파일 수정 0건

- [x] ProceduralAudioGenerator 재감사 (clean P0/P1, 2026-08-07) — static 생성기라 검사 항목 1~7이 대부분 해당 없음. 요청 문자열 ↔ 생성기 case를 **양방향 전수 대조**해 누락 0·사장 0 확인(계산 문자열 경로 2종 포함). `CreateClip`의 in-place 게인은 배열 공유가 없어 이중 적용 불가. 파일 수정 0건

- [x] CashShopUI 재감사 (clean P0/P1, **P2:1 처리**, 2026-08-07) — score 203은 `mainStylesReady`/`gachaStylesReady` 가드 안 GUIStyle 29개라 거짓양성. 탭 3곳 전부 매니저 null 가드, 결제 2단계 검증·환불, 캐시 3종 정상. **P2 처리**: 카드 라벨을 `cardTextCache`로 품목당 1회만 굽되 **실결제 가격은 캐시 제외**(IAP 준비 시 스토어 현지화가로 바뀜) + 레이아웃 전환 시 무효화

- [x] InsectEntity 재감사 (P1:2, 2026-08-07) — `AnimateWings`가 매 프레임 `transform.Find` 2회 + `insectId.Contains` 최대 10회를 다시 했다(날개 없는 종은 그 Find가 영원히 실패 — 실패하는 Find가 가장 비싸다). `Update`의 NameLabel 조회도 배틀 모델에선 영구 실패. 둘 다 같은 파일의 캐시 7개와 같은 형태로 1회 확정

- [x] WorldChannelManager 재감사 (P1:1, 2026-08-07) — 온라인 필드에 올리는 내 레벨을 옛 저장소 `PlayerPrefs("player_level")`에서 읽어 **신규·로컬 플레이가 영원히 Lv.1**로 보였다(복원 계정은 복원 시점 값으로 고착). CloudSaveManager가 같은 이유로 이미 진행도 컨트롤러를 우선하는데 이 클래스만 그 정정에서 빠져 있었다

- [x] CollectionUI 재감사 (P1:1 + 사장 코드 2, 2026-08-07) — 보유 목록이 행마다·패스마다 문자열 4개를 새로 만들었다(enum 박싱 + SizeLabel + 보간 2). 전부 (종·개체·레벨) 파생이라 불변이고 무효화 배선(`ownedCacheDirty`)은 이미 있어서 `cachedOwned`와 같은 순간에 함께 굽게 했다. 빈 `Update()`·빈 `DrawToggleButton()` 제거

- [x] RaidBattleUI 재감사 (clean P0/P1, 2026-08-07) — "2961줄 변경"은 실체가 `Draw.cs` 분리(이동)라 신규 로직은 116줄뿐. 사장 필드 0건(과거 결함이 전부 이 계열이었다), 입력 표면 4종의 페이즈 간 잔존을 전부 추적해 안전 확인. 파일 수정 0건

- [x] RegionTerrainBuilder 재감사 (P1:1, 2026-08-07) — 배치 난수 87회가 시드 없이 돌아 **월드 지형지물이 실행마다 다른 자리에 섰다**. collider를 남기는 9종(나무·통나무·바위·죽은나무·생울타리·아치기둥·폐허 벽/기둥)이라 장식이 아니라 지나갈 수 있는 길 자체가 바뀐다. 고정 시드로 가두고 전역 난수 상태는 finally로 원복(스폰·IV·포획은 그대로 무작위)

- [x] CharacterPortraitRenderer 재감사 (P1:2, 2026-08-07) — `DrawWithOutfit`가 `GUI.color`를 복원하지 않고 끝나 **호출부의 다음 라벨이 장착 악세서리 색으로 물들었다**(상점 "보유 재화/💎/🪙"). + `DrawOutfitAccessories`가 OutfitCache를 우회해 매 OnGUI 패스에 PlayerPrefs + 스코프 키 문자열을 새로 만들고 있었다

- [x] PlayerVisualBuilder 재감사 (P1:1 + 트랩 1, 2026-08-07) — `OnDestroy`가 슬롯 필드 11개만 손으로 나열해, 얼굴·머리 장식이 **지역 변수로** 만든 6~9개가 정리 목록에 오르지 못했다. 마네킹은 파괴가 정상 수명이라 다시 지을 때마다 샜다. 생성 지점(`MakeMaterial`) 하나에 등록을 걸어 호출 19곳 무변경으로 전부 덮음

- [x] CharacterOutfitManager 재감사 (P1:1, P2:1 보고, 2026-08-07) — `GetItemsForSlot`이 `allOutfits.Where(...).ToArray()`라 **의상 패널을 열어 둔 내내 프레임당 2회 이상 95벌을 훑고 배열을 새로 할당**했다(OnGUI는 Layout+Repaint로 최소 2패스). 카탈로그는 `BuildCatalog` 이후 불변이라 `Initialize`에서 슬롯별로 한 번만 갈라 캐시하고, 유일한 LINQ였던 터라 `using System.Linq`도 함께 뗐다. **P2로 넘긴 것**: `unlockCondition`을 **평가하는 코드가 어디에도 없다** — 그리는 곳(`CharacterOutfitUI:722`)만 있고, 부여 API `UnlockItem`은 호출부가 0건이다. 그래서 조건부 4벌(`hat_flower` region_garden / `shoe_waders` region_pond / `top_lab` level_15 / `acc_badge` quest_q_complete)이 `price=0`·`gemPrice=0`·`unlockedByDefault=false`라 **구매도 해금도 불가능한 영구 잠금 상태**다. 고치려면 해금 시점을 어디에 걸지(리전 해금 이벤트/레벨업/퀘스트 완료) 정해야 해서 게임 디자인 결정이 섞인다 — 자동 처리 범위 밖으로 두고 보고만 했다

- [x] CloudSaveManager 재감사 (P1:1, 2026-08-07) — **같은 세션에서 방금 만든 결함을 잡았다.** Phase 5의 명부회 간부 보스전이 격파 기록을 `InsectGame.DefeatedLedgerBosses`(PlayerPrefs CSV)에 쓰면서 **클라우드 동기 5지점을 배선하지 않았다.** `save-system.md`가 "퀘스트 세이브 필드를 늘리면 CloudSaveManager 4곳을 함께 고쳐야 한다"고 못박은 바로 그 함정이고, 증상은 **기기를 바꿀 때마다 같은 간부와 다시 싸우고 승리 보상을 다시 받는 것**이다. `defeatedGuardians`와 완전히 동형이라 그 5지점(수집·적용·직렬화·파싱·DTO)에 나란히 붙였다. 추가로 `NpcDuelController`를 `ICloudReloadable`로 만들고 Bootstrap에 `RegisterReloadable`했다 — PlayerPrefs만 갈아끼우면 인메모리 `defeatedBosses` 캐시가 낡아 그 세션 동안은 여전히 미격파로 보인다(RegionManager 해금 상태와 같은 이유). **통과 확인**: `storyProgress`(이번 2막에서 늘어난 세이브)는 이미 5지점이 전부 배선돼 있었다. `ApplyCloudFile`의 "빈 값이면 로컬 보존, forceReplace면 삭제"는 부트 로드(보존)와 명시적 '클라우드 사용'(치환)을 가르는 **의도된 설계**라 결함이 아니다(주석이 근거를 적어 뒀다). **거짓양성으로 제외 1건**: `CollectSaveData`의 `AuthManager.Instance.DisplayName`이 무가드로 보이나, 진입점 4곳이 전부 `Instance == null || !IsLoggedIn` 조기이탈을 통과하고 `CollectSaveData`는 첫 `yield` 이전(동기 구간)에 실행된다

- [x] CapturePopupUI 재감사 (P1:1 ×5파일, 2026-08-07) — **`"beetle"`이 `"bee"`를 품는데 `id.Contains("bee")` 분기가 `stag`/`rhinoceros`/`hercules`/`longhorn`보다 앞에 있었다.** 그래서 **딱정벌레 31종(사슴벌레·장수풍뎅이·헤라클레스·하늘소·물방개·비단벌레 포함)이 벌 그림으로 그려졌다.** 포획 팝업은 방금 잡은 곤충을 보여주는 자리라 증상이 가장 잘 보이는 곳이다. 같은 사슬이 저장소에 **5곳** 복제돼 있고 그중 `InsectEntity.BuildModel`만 `&& !id.Contains("beetle")` 가드를 갖고 있었다 — 나머지 4곳(`CapturePopupUI`·`DexScreenUI`·`BattleScreenUI`·`RaidBattleUI.Draw` ×2)에 같은 가드를 붙였다. 규칙이 5곳에 복제된 이상 또 어긋날 것이므로 `InsectPortraitRoutingTests`(4검사)를 새로 만들어 **소스에서 조건식을 읽어** 가드 누락을 고정했다. **거짓양성으로 제외 1건**: `InsectEntity.ResolveWings:510`의 `bee || dragonfly`는 모델이 아니라 **날갯짓 속도·진폭**을 정하는 분기라 딱정벌레가 걸려도 종이 바뀌지 않는다. **통과 확인**: 2026-05-27 라운드가 고친 GUIStyle 12개 캐시(`popupStylesReady` 가드)가 살아 있고, `stars` 배열은 포획 시 1회 할당(프레임 경로 아님), `OnEnable`↔`OnDisable`의 `CaptureResolved` 짝과 `AutoWire`의 `-=` 뒤 `+=`가 모두 성립한다

- [x] AuthManager 재감사 (인증 코어 clean, 드리프트 1건 처리, 2026-08-07) — **인증의 급소는 전부 정상이었다**: 토큰·비밀번호를 찍는 `Debug.Log`가 0건이고, `ClearAuth`가 인메모리 6필드 + PlayerPrefs 4키를 모두 지우며, `Logout`이 `ClearAuth`(토큰 무효화) **전에** 마지막 클라우드 플러시를 쏘고(첫 yield 전 헤더 설정 + `DontDestroyOnLoad`라 씬 리로드 후에도 완료), `DeleteAccount`가 `SetDeletionInProgress(true)`로 자동저장의 Firestore 문서 재생성(PII 부활)을 먼저 차단한다. `ScopedKey`는 uid가 없으면 전역 키로 떨어지는 **의도된 폴백**(비로그인 진행 보존)이다. **처리한 것 — 하드코딩 리전 목록 드리프트**: `ApplyMasterPrivileges`가 `"meadow,pond,forest,swamp,mountain,garden,ruins"`를 문자열로 박아 둬, 2막 6지역 + ruins 수문장 신설로 **해금 6건·격파 7건이 누락**됐다. 마스터는 `RegionManager.IsRegionAccessible`의 우회로 이동 자체는 되지만(그래서 증상이 낮다), `IsGuardianDefeated`엔 우회가 없어 **지도에 수문장이 미격파로 남고 필드에 스폰된다** — 진행을 건너뛰라고 있는 계정인데 2막에서만 안 건너뛴다. `RegionDefinitions.CreateAll()`에서 파생하도록 바꾸고, 다시 하드코딩되면 잡도록 `RegionProgressionTests.MasterPrivileges_DeriveRegionLists_NotHardcoded`를 추가했다. **증상 자체는 P2급(이동 가능·표시만 어긋남)인데 처리한 이유**: 고치는 방법이 새 동작 추가가 아니라 **단일 출처 파생**이라 위험이 없고, 두면 리전을 추가할 때마다 같은 자리가 또 낡는다

- [x] CharacterOutfitUI 재감사 (P1:1, 2026-08-07) — **잠긴 의상 카드에 원문 토큰이 그대로 그려졌다.** `GUI.Label(hintRect, item.unlockCondition, ...)`이라 한국어 게임 화면에 `region_garden`·`level_15`·`region_pond`·`quest_q_complete`가 노출된다(조건부 의상 4벌 전부). `DescribeUnlockCondition`으로 문장화했고 — 지역명은 **`RegionDefinitions`에서 파생**한다(이름을 박으면 리전을 고칠 때 낡는다, 같은 세션의 AuthManager 드리프트와 같은 형태) — 미지의 형식은 토큰을 그대로 돌려줘 새 조건을 추가해도 칸이 비지 않는다. 그리는 쪽은 `GUI.Label` → `UIHelper.LabelFit`로 바꿨다: 문구 길이가 데이터에서 오는데 상자가 고정 26px이고 `wordWrap = true`라, 한국어로 길어지면 아랫줄이 통째로 잘리는 자리였다(`rules/ui-layout.md`). `OutfitUnlockConditionTextTests` 5검사로 고정. **이 라운드는 P2와 겹치지만 다른 것을 고쳤다** — 직전 `CharacterOutfitManager` 라운드가 보고한 "조건부 의상 4벌이 영구 잠금"은 **해금 판정**이 없다는 것이고 게임 디자인 결정이 섞여 그대로 남아 있다. 여기서 고친 건 **표시**뿐이다. **거짓양성으로 제외 2건**: ①score 155의 실체인 `new GUIStyle` 12개는 전부 `stylesInitialized` 가드 뒤라 1회만 돈다(이번 큐에서만 다섯 번째로 나온 같은 형태다). ②호버 툴팁이 매 패스 문자열 7개를 만들지만 **한 번에 카드 하나**라, 목록 규모를 P1로 봤던 CollectionUI 라운드가 명시적으로 제외한 것과 같은 크기다. **통과 확인**: `OnDisable`이 `isOpen = false` + `Unregister`를 함께 해 stale 모달을 남기지 않고(옛 회귀의 방어 흔적이 주석에 있다), 툴팁·프리미엄·힌트 스타일이 전부 `UIHelper.CachedStyle`을 탄다

- [x] InsectSpawner 재감사 (P1:1, 2026-08-07) — **스포너 본체는 clean이었고, 결함은 그것을 먹이는 부트스트랩에 있었다.** `PlaySceneBootstrap.GetRegionLevelRange`가 ver1 7지역만 아는 switch에 `default: return 5`라, **2막 6지역이 전부 default로 떨어져 필드 스폰 레벨 상한이 47/51/55/59/63/67에 묶여 있었다**(의도는 48/52/56/60/64/70). 에러도 안 나고 화면상 티도 안 나서 오래 남을 결함이다. 6개 case를 `Docs/StoryBible.md`의 리전 표와 같은 대역으로 넣고, **default를 조용히 넘기지 않고 `LogWarning`을 남기게** 했다 — 다음에 리전을 추가하는 사람이 알아채야 한다. `RegionProgressionTests.EveryRegion_HasExplicitSpawnLevelRange`로 고정. **거짓양성으로 제외 3건 — 전부 내가 세운 가설이었고 코드를 읽어 철회했다**: ①`GetUnderpopulatedRegionId`가 플레이어 위치를 안 봐서 먼 리전에 스폰을 낭비할 것 같았으나, `GetAvailableSpawnPoint`에 **55m 거리 게이트**가 이미 있어(주석이 "60m 디스폰 밖 스폰은 5초 내 정리되는 낭비"라고 이유까지 적어 뒀다) 먼 리전이 선호되어도 실제 스폰은 플레이어 근처로 떨어진다. ②리전이 7→13이 되며 `spawnPointCount/regionCount`가 반토막 날 것 같았으나, `pointsPerRegion = Max(base, radius/11)`이라 **반경 비례가 지배**해 리전 수와 무관하다(6~8개 유지). ③`Update`의 리전 필터가 매 프레임 List를 할당하는 것처럼 보였으나 `spawnIntervalSeconds`(5초) 스로틀 뒤에만 돈다. **통과 확인**: `DespawnEntity`가 `activeSelf` 가드 뒤에 풀 반환하고, `DespawnAllActiveInsects`가 스냅샷 후 리스트를 먼저 비워 콜백 재진입이 no-op이며, `SubAreaChanged` 구독이 `AutoWire`(`-=` 뒤 `+=`)와 `OnDisable`에서 대칭이다

- [x] PlayerMovement 재감사 (P1:1, 2026-08-07) — **걷는 내내 매 프레임 `Collider[]`를 2개씩 할당했다.** `IsBlockedPosition`이 `Physics.OverlapSphere`(할당형)를 쓰는데, Update가 이동 중 **두 번** 부른다 — 다음 위치 차단 판정(`nextPos`)과 끼임 감지(`transform.position`). 초당 120개 배열이 GC로 가는 셈이라 모바일에서 특히 나쁘다. `OverlapSphereNonAlloc` + 고정 버퍼(16)로 바꿨고, 같은 형태였던 `IsClearAt`(안전 위치 탐색이라 한 번의 복구에서 여러 번 돈다)도 같은 버퍼를 쓰게 했다 — **둘은 호출이 겹치지 않는다**(끼임 판정이 끝난 뒤에야 `RecoverToSafePosition`이 불린다). 버퍼가 차도 오판정이 없다: 가득 찼다는 건 이미 막을 것을 찾았다는 뜻이라 결과가 바뀌지 않는다. **거짓양성으로 제외 2건**: ①이번 세션에 월드 경계를 320→520으로 넓혀 이동 제한이 낡았을까 봤으나, PlayerMovement엔 좌표 클램프가 **없다** — 경계는 `WorldTerrainBuilder`가 세운 물리 벽이 담당하므로 자동으로 따라간다. ②`IsBlockedPosition`의 `foreach (regionManager.Regions)`가 리전 7→13으로 두 배가 됐지만 `ContainsPoint`는 곱셈 두 번이라 프레임당 13회는 무시할 수준이다. **통과 확인**: 리전 잠금이 이동 단계에서 실제로 강제되고(`IsRegionAccessible`, 서브에리어 안에서는 좌표계가 달라 의도적으로 건너뛴다), OnGUI 스타일 3종이 캐시 필드이며, 클릭 이동 raycast가 `try-finally`로 player layer를 복원한다(본인 raycast 통과 회귀 방어 흔적)

- [x] BattleTeamUI 재감사 (P1:2, 2026-08-07) — **①화면 전체가 한국어인데 이 화면만 영어로 남아 있었다**: 제목 `"BATTLE TEAM"`, 피커 제목 `"Select Insect for Slot {n}"`, 뒤로 버튼 `"< Back"`. 퀵바(`QuickAccessBarUI`)와 HUD(`PlayerStatusHUD`)가 이 화면을 **"배틀팀"**으로 부르고 있어 같은 말로 맞췄다. 화살표는 ASCII `<`를 유지했다 — 그 라벨에서 이미 렌더되던 글자라 폰트 아틀라스가 확실하다(전각 `＜`로 바꿨다가 되돌렸다). **②빈 `Update() { }`** — 본문이 없어도 Unity는 매 프레임 managed→native 호출을 한다. `CollectionUI` 재감사(같은 날)가 지운 것과 같은 것이라 지우고 되살리지 말라는 주석을 남겼다. **거짓양성으로 제외 3건**: ①score 102의 실체인 `new GUIStyle` 11개는 전부 `teamStylesInit` 가드 뒤다 — **이번 큐에서만 여섯 번째로 나온 같은 형태**(LoginUI·TrainingUI·CollectionUI·CashShopUI·CharacterOutfitUI에 이어). ②피커 목록이 개체마다 `$"Lv.{n} | CP {n}"`을 매 패스 만들지만 `DexBrowseLayout.GetVisibleRowRange`로 **이미 컬링돼** 보이는 행에만 돈다(CollectionUI 라운드가 넣은 방어가 여기선 처음부터 있었다). ③슬롯 5칸의 `$"Lv.{lv} | {data.rarity} | CP {cp}"`는 enum 박싱을 포함하나 **5개 고정**이라, 목록 규모를 P1로 봤던 라운드가 "한 번에 개체 하나~다섯 행"을 명시적으로 제외한 것과 같은 크기다. **통과 확인**: `CloseModal`과 `OnDisable`이 `isOpen`·`selectingSlot`·`directScroll`·레지스트리를 동일하게 정리해 stale 모달이 남지 않는다

- [x] StoryJournalUI (P1:1, 2026-08-08) — **탭 한 줄이 프레임마다 스토리 전체를 훑고 있었다.** `DrawChapterTabs`가 챕터 15개마다 `SeenIn()`으로 그 챕터 비트를 세는데, 안쪽 `HasSeen`이 `progress.seenBeatIds.Contains`(**`List<string>` O(진행도)**)라 패스당 **비트 72개 × 진행도만큼의 문자열 비교**가 든다. 다 본 세이브면 패스당 5천 회가 넘고, OnGUI는 프레임당 Layout+Repaint 최소 2패스에 마우스를 움직이면 더 돈다. 여기에 챕터마다 `$"{라벨}  {done}/{total}"` 보간 문자열 15개와 `ChapterLabel`의 15칸 선형 탐색이 얹혔다. **열람 상태는 저널을 열어 둔 동안 절대 바뀌지 않는다** — 다시 읽기(`ShowStoryReplay`)가 seen을 마킹하지 않는 게 이 화면의 설계이고 클래스 주석이 그 이유를 적어 뒀다. 라벨과 `done == 0` 판정을 `SeenCount`를 키로 캐시했다(저널 밖에서 비트를 하나 더 열람하면 키가 달라져 자동으로 다시 굽는다). **자체 회귀 1건을 즉시 되돌림**: 처음엔 배경색 판정을 캐시된 라벨에서 문자열 파싱(`EndsWith`+`Substring`)으로 되읽게 썼는데, 그건 매 패스 문자열 연산을 도로 넣는 것이라 `bool[]`을 함께 캐시하는 형태로 고쳤다. **거짓양성으로 제외 3건**: ①score 41의 "프레임 할당 10"은 대부분 `new Rect`/`new Color`(struct, 스택) + `EnsureStyles`의 `new GUIStyle` 7개(`stylesReady` 가드 뒤 1회) — **이번 큐에서만 일곱 번째로 나온 같은 형태**. ②"싱글턴 참조 11"의 실체인 `UITheme.Instance`는 정적 필드에 캐시돼 첫 호출만 `Resources.Load`를 탄다. ③`DrawBeatList`가 컬링 없이 `list.Count`를 전부 도는 게 도감·보유목록 라운드의 P0와 같은 형태로 보이나, **3D 썸네일 요청이 없어 LRU 스래싱이 없고** 한 챕터 최대 8행(ch7)이라 뷰포트(824px)에 거의 다 들어와 스크롤조차 잘 생기지 않는다 — 규모로 보면 기존 라운드들이 명시적으로 제외한 "한 번에 다섯 행" 급이다. **통과 확인**: `IModalUI` 3짝(`Toggle`/`CloseModal`/`OnDisable`)이 `isOpen`·`directScroll`·레지스트리를 동일하게 정리하고(주석이 "isOpen을 남기면 ESC가 건너뛴다"는 옛 회귀를 적어 뒀다), `ReplayBeat`가 `HasSeen`으로 미열람 비트를 한 번 더 걸러 대사 소비/보상 누출을 막으며, `EnsureIndex`의 미등록 챕터 정렬이 Dictionary 순회의 비결정성을 없앤다. Bootstrap 배선(`EnsureComponent` → `AutoWire(storyDirector, npcDialogue)` → `quickBar.AutoWire`)도 완전하다

- [x] InsectExpansion2Definitions (P1:1, 2026-08-08) — **데이터 자체는 clean이었고, 결함은 그것을 소비하는 쪽과의 "짝"을 아무도 검사하지 않는다는 것이었다.** 54종의 ID 유일성·1막 64종과의 비충돌·레어도 밴드(weight/difficulty 5등급 × 2범위) 위반 0건·등급 분포(C18/U12/R12/E10/L2)·풀 배정·`BuildModel` 라우팅 54종 전수를 확인했고 파일 수정은 없다. **처리한 것**: 시드 파일 주석이 "habitat 태그는 `InferPrimaryType`의 zone 폴백과 **짝**이다 — 한쪽만 고치면 오타가 조용히 Bug 속성으로 떨어진다"고 경고하는데, 기존 `NewSeeds_HabitatTags_AreKnown`은 **시드 쪽만 자기 하드코딩 HashSet과 맞춰 봤다.** 짝의 반대편(부트스트랩)에서 분기가 사라지거나 오타가 나면 그대로 통과하고, 그 리전 종 6~12마리의 속성이 전부 Bug가 된다 — 속성은 상성 계산의 입력이라 전투가 통째로 어긋나는데 에러도 경고도 없다. 같은 형태가 하나 더 있었다: Epic/Legendary 12종의 **전용기 switch**도 "누락하면 default 『궁극 생태 해방』으로 조용히 떨어져 전용기가 전부 같은 이름이 된다"는 주석만 있고 검사가 없었다. 둘 다 소스를 읽어 대조하는 테스트로 고정했다(`InsectPortraitRoutingTests`가 같은 이유로 만든 선례를 따랐다 — 분기가 private 인스턴스 메서드라 실행할 수 없다). **검출력 실측**: `zone == "Canopy"`에 오타를 주입하니 Canopy를 잡았고, `case "mantis_canopy":`를 지우니 그 종을 잡았다. **거짓양성으로 철회 2건 — 둘 다 내가 세운 가설이었고 코드를 읽어 접었다**: ①"2막 6지역이 zone 폴백에 하나도 없다"고 봤으나 `grep -A 45`의 범위가 짧아 못 본 것이었다(3139~3151에 6개가 전부 있다). ②`IsAntInsectId`가 `mantis`의 'ant'를 잡아 사마귀가 전부 Earth로 떨어질 것 같았으나, 구현이 `StartsWith("ant_") || Contains("_ant_") || Contains("antlion")`이라 애초에 안 걸린다. 이번 큐에서 세 번째로 반복되는 교훈이다 — **의심스러운 구조를 발견하면 방어 코드를 먼저 찾을 것.**

- [x] NpcBossDuels (P1:1, 2026-08-08) — **표 자체는 clean이었고, 결함은 주석이 약속한 검증이 실제로는 없다는 것이었다.** 파일 주석이 "곤충 ID·아이템 ID는 각각 InsectExpansion2Definitions / ItemDatabase에 실재해야 하며 `NpcBossDuelTests`가 그 정합을 고정한다"고 적었는데, 그 테스트 8건을 열어 보니 곤충은 `EveryBoss_InsectExistsInExpansion`이 실재성을 보지만 **아이템은 `EveryBoss_HasRewardAndDisplayName`이 `string.IsNullOrEmpty`만 본다.** 오타를 물어도 런타임엔 조용히 실패해(퀘스트 보상과 같은 함정 — `LogWarning`만) **간부를 이겼는데 보상만 안 들어온다.** 아이템 ID가 캡처아이템·상점 진열·상점 지급·ItemDatabase **네 소스의 합집합**이라 C#에서 다시 모으면 사본이 생겨 어긋나므로, 그 레지스트리를 이미 완비한 `quest_lint.py`에 검사를 붙였다(`extract_boss_duel_rewards` + 검사 4b). 추출이 0건이면 `ExtractorBroken`을 던지게 해 파서가 낡았을 때 "위반 0건"으로 조용히 죽지 않게 했다 — 이 파일의 관통 원칙이다. **검출력 실측**: `net_gold` → `net_goldd` 오타를 주입하니 `ledger_grip:net_goldd`를 잡았고, 현재 3건은 전부 실재한다(`net_gold`·`full_restore` ×2, 아이템 31종 대조). 주석도 "고정하는 곳이 둘로 나뉜다"로 정확히 고쳤다 — 약속과 실제가 어긋난 게 이 라운드의 본질이라 문서를 그대로 두면 다음 사람이 또 믿는다. **통과 확인**: 상대 곤충 3종(`centipede_sand`/`mantis_icicle`/`moth_effaced`)이 2막 정의에 실재하고, `storyNpcId` 3개가 story.json의 `NpcTalk` 트리거·화자와 일치하며, `ledger_ink`가 표에 없다(잿불 골짜기에서 이탈해 아군이 되므로 — 테스트가 명시적으로 고정한다). `All()`이 struct 배열을 복사해 돌려주고 `TryGet`은 3항목 선형 탐색이라 성능 표면이 없다. 호출부 3곳(`NpcDuelController` 진입·표시·보상)도 배선돼 있다

- [x] OutfitShapeLibrary 재감사 (P1:1, 2026-08-08) — **직전 라운드가 고친 머티리얼 누수의 "나머지 절반"이었다.** 2026-08-07 `PlayerVisualBuilder` 라운드가 `OnDestroy`의 손나열을 `MakeMaterial` 생성 지점 등록으로 바꿔 17~20개를 덮었는데, **spawn 의상 파츠의 머티리얼은 생성 지점이 `OutfitShapeLibrary.CreatePartMaterial`로 따로 있어 그 목록에 들어가지 않는다.** 이쪽 정리는 `TrimContainer`뿐이고 그건 갈아입을 때 **남는** 파츠만 지우므로, **마지막까지 입고 있던 파츠**는 루트가 통째로 파괴될 때 아무도 안 지운다. 마네킹은 파괴가 정상 수명이다 — `CharacterModelPreviewRenderer`가 외형 해시(성별·피부·머리·얼굴)가 바뀔 때마다 `Destroy(mannequin)` 후 다시 짓고, 그건 캐릭터 커스터마이즈 화면에서 토글 한 번마다 일어난다. 동시 착용 기준 **최대 22개**(hat 7 / acc 6 / outer 5 / bag 4)가 매번 샜다. `DestroySpawnedMaterials(root)`를 신설해 `PlayerVisualBuilder.OnDestroy`가 자기 목록을 비운 직후 부르게 했다. **bind 노드는 건드리지 않는다** — 그쪽 소유자는 PlayerVisualBuilder라 이중 파기하면 캐릭터가 분홍색이 된다(그 파일 주석이 적어 둔 검정/마젠타 회귀와 같은 자리). 가르는 기준이 노드 이름의 `OP_` 접두 하나뿐이라 `BindNames_NeverStartWithSpawnPrefix`로 고정했다. **거짓양성으로 철회 2건 — 둘 다 내 가설이었고 코드를 읽어 접었다**: ①`hideNodes`(HideCap/HideBackpack)를 끄는 코드는 있는데 되살리는 코드가 없어 "모자를 바꾸면 기본 캡이 영영 안 돌아온다"고 봤으나, 복원은 `ApplyPartColor`가 하고(알파가 있으면 `SetActive(true)`) `ApplyToCharacter`가 **ApplyPartColor → ApplyShapeRecipe 순서**로 부른다 — 켜고 나서 필요할 때만 다시 끄는 구조다. `OutfitShapeLibrary` 주석 270행이 "SetActive(true)도 하지 않는다 — 판정은 ApplyPartColor 한 곳에만 둔다"고 그 계약을 명시해 뒀다. ②`TryGet`이 `id.Contains` 사슬이라 부분문자열 오매칭이 있을 줄 알았으나, Tool만 그렇고(주석이 순서 의존을 명시 + `OutfitRecipeProjectionTests`가 분기를 고정) 나머지 슬롯은 `ExactRecipes.TryGetValue` **정확 매칭**이다. **통과 확인**: 갈아입기 경로의 누수는 이미 막혀 있다(`TrimContainer`가 머티리얼을 함께 파기, 벗을 때도 `TrimContainer(container, 0)`로 도달) / `ApplySpawned`가 파츠 재사용 시 `sharedMaterial.color`만 바꿔 `renderer.material` 게터의 인스턴스 증식을 피한다 / `GetPrimMesh`가 PrimitiveType별 built-in 메시를 1회만 추출 / 기존 테스트 22건이 카탈로그 정합·bind 노드 실재·도구 분기 도달성을 이미 덮는다

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

아래는 그 스크립트가 뽑은 큐다. **2026-08-07에 큐가 0이 되어 재생성했다**(`--emit-md` 상위 15).
2막 작업으로 생긴 신규 파일 3개가 후보에 올라온 게 이번 재생성의 특징이다 —
`StoryJournalUI`·`InsectExpansion2Definitions`·`NpcBossDuels`는 한 번도 감사된 적이 없다.

- [ ] CharacterModelPreviewRenderer 재감사 (Core/CharacterModelPreviewRenderer.cs, 477줄, score 19) — 2026-08-06 감사 이후 2026-08-07까지 476줄 변경
- [ ] PlayerStatusHUD 재감사 (UI/PlayerStatusHUD.cs, 508줄, score 98) — 2026-05-21 감사 이후 2026-08-04까지 451줄 변경
- [ ] GachaBoxManager 재감사 (Core/GachaBoxManager.cs, 345줄, score 8) — 2026-05-20 감사 이후 2026-07-18까지 291줄 변경
- [ ] CameraFollower 재감사 (Core/CameraFollower.cs, 341줄, score 0) — 2026-05-21 감사 이후 2026-08-07까지 287줄 변경
- [ ] WorldLobbyUI 재감사 (UI/WorldLobbyUI.cs, 534줄, score 94) — 2026-05-27 감사 이후 2026-08-03까지 267줄 변경
- [ ] AudioManager 재감사 (Core/AudioManager.cs, 585줄, score 13) — 2026-05-21 감사 이후 2026-08-07까지 263줄 변경
- [ ] PlayerInsectCollection 재감사 (Core/PlayerInsectCollection.cs, 732줄, score 24) — 2026-05-21 감사 이후 2026-08-07까지 256줄 변경
- [ ] RaidSupportPlanner 재감사 (Battle/RaidSupportPlanner.cs, 253줄, score 0) — 2026-08-06 감사 이후 2026-08-07까지 252줄 변경
- [ ] CashShopManager 재감사 (Core/CashShopManager.cs, 323줄, score 14) — 2026-05-22 감사 이후 2026-07-19까지 239줄 변경
- [ ] RaidRoundResolver 재감사 (Battle/RaidRoundResolver.cs, 515줄, score 0) — 2026-08-03 감사 이후 2026-08-07까지 234줄 변경
- [ ] RegionManager 재감사 (Core/RegionManager.cs, 342줄, score 19) — 2026-05-20 감사 이후 2026-08-07까지 207줄 변경

score 0은 "OnGUI/Update 표면이 없다"는 뜻일 뿐 clean이라는 뜻이 아니다 —
직전 10라운드가 거의 전부 score 0이었는데 P0 1건·P1 6건이 나왔다.
큐가 비면 `python -X utf8 .claude/scripts/audit_candidates.py --emit-md`로 재생성.

(`verify_coverage.py`는 미할당 0 — 담당 에이전트 매핑은 별개로 완전하다.)

## Round Log

보존 개수는 **파일 상단 안내(9줄)가 단일 출처**다 — 여기에 사본을 적지 않는다.
넘치는 항목은 `.claude/audit-archive/round-log-2026H2.md`로 옮긴다(그 이전은 `-2026H1`).

> 이 로그는 **쓰기 전용**이다 — audit 스킬 Step 4가 추가하지만 어느 단계도 읽지 않는다.
> 그런데 Step 1이 이 파일을 통째로 Read하므로 쌓아두면 매 라운드 컨텍스트를 먹는다.
- 2026-08-08: OutfitShapeLibrary **재감사** (2026-08-06 감사 이후 1053줄 변경, 큐 1순위, score 0) — **P1:1 처리. 직전 PlayerVisualBuilder 라운드가 고친 머티리얼 누수의 "나머지 절반"이었다.** 그 라운드는 `MakeMaterial` 생성 지점에 등록을 걸어 17~20개를 덮었는데, spawn 의상 파츠는 생성 지점이 `OutfitShapeLibrary.CreatePartMaterial`로 따로 있어 그 목록에 들어가지 않는다. 이쪽 정리는 `TrimContainer`뿐이고 그건 갈아입을 때 **남는** 파츠만 지우므로, **마지막까지 입고 있던 파츠**가 루트 파괴 시 샌다 — 마네킹은 파괴가 정상 수명이라(외형 해시가 바뀔 때마다 재생성) 커스터마이즈 토글 한 번마다 **최대 22개**(hat 7 / acc 6 / outer 5 / bag 4)가 누수됐다. `DestroySpawnedMaterials`를 신설해 `OnDestroy`가 자기 목록을 비운 직후 부르게 하고, bind 노드 이중 파기(캐릭터가 분홍색이 된다)를 막는 `OP_` 접두 계약을 `BindNames_NeverStartWithSpawnPrefix`로 고정했다. **거짓양성 2건 철회 — 둘 다 내 가설이었고 코드를 읽어 접었다**: hideNodes 복원 부재(→ 복원은 `ApplyPartColor`가 하고 `ApplyToCharacter`가 ApplyPartColor → ApplyShapeRecipe 순서로 부른다, 주석이 그 계약을 명시해 뒀다) / `TryGet`의 부분문자열 오매칭(→ Tool만 Contains 사슬이고 나머지 슬롯은 `ExactRecipes.TryGetValue` 정확 매칭). 검증: error CS 0, ci_check 8검사 통과, PlayMode 462/462.
- 2026-08-08: NpcBossDuels **신규 감사** (큐 1순위, score 0, 2막 신규 파일) — **표는 clean, 결함은 주석이 약속한 검증이 실제로는 없다는 것.** "아이템 ID는 ItemDatabase에 실재해야 하며 NpcBossDuelTests가 정합을 고정한다"고 적혀 있으나 그 테스트는 **비어 있지 않은지만** 본다(곤충은 실재성을 본다). 오타를 물면 조용히 실패해 **간부를 이겼는데 보상만 안 들어온다**. 아이템 ID가 네 소스의 합집합이라 그 레지스트리를 이미 모으는 quest_lint.py에 검사 4b를 붙이고, 추출 0건이면 ExtractorBroken을 던지게 해 파서가 낡았을 때 조용히 죽지 않게 했다. **오타 주입으로 검출력 실측**(net_gold → net_goldd 검출, 현재 3건은 전부 실재). 주석도 "고정하는 곳이 둘로 나뉜다"로 정정했다 — 약속과 실제가 어긋난 게 본질이라 문서를 두면 다음 사람이 또 믿는다. 통과 확인: 상대 곤충 3종 실재, storyNpcId 3개가 story.json NpcTalk 트리거와 일치, ledger_ink 제외(아군화), 호출부 3곳 배선. 검증: error CS 0, ci_check 8검사 통과, PlayMode 461/461.
- 2026-08-08: InsectExpansion2Definitions **신규 감사** (큐 1순위, score 0, 2막 신규 파일) — **데이터는 clean, 결함은 "짝"을 아무도 검사하지 않는다는 것.** 54종의 ID 유일성·1막 비충돌·레어도 밴드·등급분포·풀배정·BuildModel 라우팅 전수 정상(파일 수정 0건). 시드 주석이 habitat ↔ InferPrimaryType zone 폴백을 **짝**이라 못박았는데 기존 테스트는 시드 쪽만 자기 하드코딩 목록과 봤다 — 부트스트랩에서 분기가 사라지면 통과하고 그 리전 종의 속성이 전부 조용히 Bug가 된다. Epic/Legendary 12종의 전용기 switch도 같은 형태로 무검사였다. 둘 다 소스 대조 테스트로 고정하고 **오타·삭제를 주입해 검출력을 실측**했다. **거짓양성 2건 철회 — 둘 다 내 가설이었다**: zone 폴백 누락(grep -A 45 범위가 짧았다, 실제로는 6개 전부 존재) / IsAntInsectId가 mantis를 잡을 것(StartsWith("ant_")||"_ant_"||"antlion"이라 안 걸림). 이번 큐에서 세 번째 같은 교훈 — **의심스러운 구조를 보면 방어 코드를 먼저 찾을 것.** 검증: error CS 0, ci_check 8검사 통과, PlayMode 461/461.
> 영역별 처리 이력은 위 Covered 인덱스가 이미 갖고 있다.
>
> 개수를 두 곳에 적었다가 실제로 어긋났다 — 상단은 "최근 10건", 여기는 "최근 3건만 둔다"라고
> 서로 다른 말을 하는 동안 23건이 쌓여 47KB가 됐다(2026-08-03 정리).

- 2026-08-08: StoryJournalUI **신규 감사** (큐 1순위, score 41, 한 번도 감사된 적 없는 2막 신규 파일) — **P1:1 처리. 탭 한 줄이 프레임마다 스토리 전체를 훑었다.** `DrawChapterTabs`가 챕터 15개마다 그 챕터 비트를 세는데 `HasSeen`이 `List<string>.Contains`라, 다 본 세이브에서 **패스당 5천 회가 넘는 문자열 비교** + 보간 문자열 15개가 들었다(OnGUI는 프레임당 2패스 이상). 열람 상태는 저널을 열어 둔 동안 불변이므로(다시 읽기가 seen을 마킹하지 않는 게 이 화면의 설계다) `SeenCount`를 키로 라벨과 `done==0`을 캐시했다. **자체 회귀 1건 즉시 되돌림** — 배경색 판정을 캐시된 라벨의 문자열 파싱으로 되읽게 썼다가 매 패스 문자열 연산을 도로 넣는 꼴이라 `bool[]` 동반 캐시로 고쳤다. **거짓양성 3건 제외**: score 41의 실체는 struct(`new Rect`/`new Color`)와 `stylesReady` 가드 뒤 GUIStyle 7개로 **이번 큐에서만 일곱 번째 같은 형태** / `UITheme.Instance`는 정적 캐시라 첫 호출만 Resources.Load / 비트 목록의 컬링 부재는 도감 P0와 형태가 같아 보이나 3D 썸네일 요청이 없어 LRU 스래싱이 없고 최대 8행이라 뷰포트에 거의 다 들어온다. 검증: error CS 0, ci_check 8검사 통과, PlayMode 459/459.
- 2026-08-07: BattleTeamUI **재감사** (2026-05-21 감사 이후 487줄 변경, 큐 1순위, score 102) — **P1:2 처리. 이 라운드로 큐가 0이 되어 재생성했다**(상위 15, 신규 파일 3개 포함). ①화면 전체가 한국어인데 이 화면만 영어였다(`"BATTLE TEAM"`·`"Select Insect for Slot {n}"`·`"< Back"`) — 퀵바와 HUD가 이 화면을 "배틀팀"이라 부르므로 같은 말로 맞췄다. 화살표는 ASCII `<` 유지(전각으로 바꿨다가 폰트 아틀라스 확실성 때문에 되돌렸다). ②빈 `Update() { }` 제거 — 본문이 없어도 매 프레임 managed→native 호출이 든다(같은 날 CollectionUI 라운드가 지운 것과 동일). **거짓양성 3건 제외**: score 102의 GUIStyle 11개는 `teamStylesInit` 가드 뒤로 **이번 큐에서만 여섯 번째 같은 형태** / 피커 목록은 이미 `GetVisibleRowRange`로 컬링됨 / 슬롯 문자열은 5개 고정이라 기존 라운드가 명시적으로 제외한 규모. 검증: error CS 0, ci_check 8검사 통과, PlayMode 444/444.
