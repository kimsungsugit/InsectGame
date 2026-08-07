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

- [x] InsectEntity 재감사 (P1:2, 2026-08-07) — `AnimateWings`가 매 프레임 `transform.Find` 2회 + `insectId.Contains` 최대 10회를 다시 했다(날개 없는 종은 그 Find가 영원히 실패 — 실패하는 Find가 가장 비싸다). `Update`의 NameLabel 조회도 배틀 모델에선 영구 실패. 둘 다 같은 파일의 캐시 7개와 같은 형태로 1회 확정

- [x] WorldChannelManager 재감사 (P1:1, 2026-08-07) — 온라인 필드에 올리는 내 레벨을 옛 저장소 `PlayerPrefs("player_level")`에서 읽어 **신규·로컬 플레이가 영원히 Lv.1**로 보였다(복원 계정은 복원 시점 값으로 고착). CloudSaveManager가 같은 이유로 이미 진행도 컨트롤러를 우선하는데 이 클래스만 그 정정에서 빠져 있었다

- [x] CollectionUI 재감사 (P1:1 + 사장 코드 2, 2026-08-07) — 보유 목록이 행마다·패스마다 문자열 4개를 새로 만들었다(enum 박싱 + SizeLabel + 보간 2). 전부 (종·개체·레벨) 파생이라 불변이고 무효화 배선(`ownedCacheDirty`)은 이미 있어서 `cachedOwned`와 같은 순간에 함께 굽게 했다. 빈 `Update()`·빈 `DrawToggleButton()` 제거

- [x] RaidBattleUI 재감사 (clean P0/P1, 2026-08-07) — "2961줄 변경"은 실체가 `Draw.cs` 분리(이동)라 신규 로직은 116줄뿐. 사장 필드 0건(과거 결함이 전부 이 계열이었다), 입력 표면 4종의 페이즈 간 잔존을 전부 추적해 안전 확인. 파일 수정 0건

- [x] RegionTerrainBuilder 재감사 (P1:1, 2026-08-07) — 배치 난수 87회가 시드 없이 돌아 **월드 지형지물이 실행마다 다른 자리에 섰다**. collider를 남기는 9종(나무·통나무·바위·죽은나무·생울타리·아치기둥·폐허 벽/기둥)이라 장식이 아니라 지나갈 수 있는 길 자체가 바뀐다. 고정 시드로 가두고 전역 난수 상태는 finally로 원복(스폰·IV·포획은 그대로 무작위)

- [x] CharacterPortraitRenderer 재감사 (P1:2, 2026-08-07) — `DrawWithOutfit`가 `GUI.color`를 복원하지 않고 끝나 **호출부의 다음 라벨이 장착 악세서리 색으로 물들었다**(상점 "보유 재화/💎/🪙"). + `DrawOutfitAccessories`가 OutfitCache를 우회해 매 OnGUI 패스에 PlayerPrefs + 스코프 키 문자열을 새로 만들고 있었다

- [x] PlayerVisualBuilder 재감사 (P1:1 + 트랩 1, 2026-08-07) — `OnDestroy`가 슬롯 필드 11개만 손으로 나열해, 얼굴·머리 장식이 **지역 변수로** 만든 6~9개가 정리 목록에 오르지 못했다. 마네킹은 파괴가 정상 수명이라 다시 지을 때마다 샜다. 생성 지점(`MakeMaterial`) 하나에 등록을 걸어 호출 19곳 무변경으로 전부 덮음

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

- [ ] CashShopUI 재감사 (UI/CashShopUI.cs, 853줄, score 203) — 2026-05-21 감사 이후 2026-08-03까지 766줄 변경
- [ ] ProceduralAudioGenerator 재감사 (Core/ProceduralAudioGenerator.cs, 1958줄, score 0) — 2026-05-21 감사 이후 2026-06-06까지 696줄 변경
- [ ] TutorialQuestManager 재감사 (Core/TutorialQuestManager.cs, 1139줄, score 38) — 2026-05-27 감사 이후 2026-08-03까지 681줄 변경
- [ ] CharacterOutfitManager 재감사 (Core/CharacterOutfitManager.cs, 797줄, score 9) — 2026-05-20 감사 이후 2026-08-07까지 671줄 변경
- [ ] CloudSaveManager 재감사 (Core/CloudSaveManager.cs, 870줄, score 35) — 2026-05-20 감사 이후 2026-07-18까지 669줄 변경
- [ ] CapturePopupUI 재감사 (UI/CapturePopupUI.cs, 1096줄, score 76) — 2026-05-27 감사 이후 2026-08-04까지 657줄 변경
- [ ] AuthManager 재감사 (Core/AuthManager.cs, 1002줄, score 3) — 2026-05-20 감사 이후 2026-07-17까지 630줄 변경
- [ ] CharacterOutfitUI 재감사 (UI/CharacterOutfitUI.cs, 850줄, score 155) — 2026-05-27 감사 이후 2026-08-07까지 595줄 변경
- [ ] InsectSpawner 재감사 (Spawning/InsectSpawner.cs, 781줄, score 26) — 2026-05-22 감사 이후 2026-07-18까지 518줄 변경
- [ ] PlayerMovement 재감사 (Core/PlayerMovement.cs, 653줄, score 23) — 2026-05-27 감사 이후 2026-08-07까지 515줄 변경
- [ ] BattleTeamUI 재감사 (UI/BattleTeamUI.cs, 380줄, score 102) — 2026-05-21 감사 이후 2026-08-07까지 487줄 변경

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

- 2026-08-07: InsectEntity **재감사** (2026-05-21 감사 이후 883줄 변경, 큐 1순위, score 8) — **P1:2 처리.** **P1-①: `AnimateWings`가 매 프레임 `transform.Find`를 두 번 한다.** `Update`가 무조건 부르는데 본문 첫 줄이 `transform.Find("WingL")` + `transform.Find("WingR")`이고, 이어서 종별 날갯짓 파라미터를 정하려고 `insectId.Contains`를 최대 10회 다시 돌린다. 둘 다 **빌드당 한 번이면 끝나는 값**이다(모델은 `Initialize`/`BuildForBattle`이 동기로 다 짓고 나서야 첫 Update가 돈다). 가장 나쁜 건 **날개가 없는 종**(기어다니는 곤충)이다 — Find가 영원히 실패하는데 실패하는 Find가 자식 전체를 훑어 제일 비싸다. 필드엔 곤충이 여러 마리 동시에 살아 있고 각자 Update를 돌므로 매 프레임 수천 번의 이름 비교가 된다. **이 파일은 이미 그 문제를 알고 캐시를 7개나 들고 있다** — `cachedNameLabel`·`cachedShinySparkle`·`cachedGroundMarker`·`cachedGrass`·`cachedMoveStyle`·`cachedShinyShift`(인스턴스) + `cachedMainCam`·`cachedPlayer`(static, 프레임당 1회 게이트)까지 두고 `Initialize`/`BuildForBattle`의 풀 재사용 리셋 블록에서 일제히 비운다. `EnsureMoveStyle`도 `cachedMoveStyle >= 0`로 1회만 돈다. **날개만 그 규율에서 빠져 있었다.** `ResolveWings()`로 노드와 파라미터를 함께 확정하고 리셋 블록 두 곳에 배선했다 — 못 찾아도 `wingsResolved`를 **맨 먼저** 세워 재시도하지 않는다. `Contains` 분기 순서(butterfly/damselfly/dragonfly가 mosquito|fly보다 **앞**)는 셋 다 "fly"를 품고 있어 의미가 있으므로 그대로 옮겼다. **P1-②: `Update`의 NameLabel 조회도 같은 형태.** `if (cachedNameLabel == null) transform.Find("NameLabel")`인데 **배틀 모델엔 그 노드가 아예 없다**(`BuildForBattle`은 `CreateNameLabel`을 부르지 않는다). 그래서 레이드의 팀 5마리 + 보스, 1v1의 2마리가 전투 내내 매 프레임 실패하는 Find를 돌렸다. `nameLabelResolved` 센티넬로 1회만 찾게 했다. **거짓양성으로 제외 2건**: ①`AnimateShinySparkle`도 `transform.Find("ShinySparkle")`을 하지만 없으면 **그 자리에서 만들어** 캐시에 넣으므로 두 번째 프레임부터 조회가 없다(게다가 `shiny`는 1% 개체뿐). ②`ClearChildren`이 `GetComponentsInChildren<Renderer>`로 배열을 할당하지만 스폰/디스폰 시점 1회이지 프레임 경로가 아니다. **통과 확인**: 인스턴스 머티리얼 정리가 `ClearChildren`에 살아 있고(풀 재사용마다 수십 개 누수를 막던 자리 — 이번 주 다른 3개 파일에서 같은 계열을 고쳤다), `UpdatePlayerTracking`이 `playerTrackFrame`으로 프레임당 1회만 돌아 `GameObject.FindWithTag`가 곤충 수만큼 반복되지 않으며, `Despawn`이 `despawnedThisCycle`로 풀 이중 반환을 막고, `Initialize`가 `forBattle`을 명시적으로 false로 되돌려 풀에서 나온 개체가 정적으로 굳는 회귀를 막는다. 검증: error CS 0, ci_check 8검사 통과, PlayMode **405/405**(프리팹 없는 프로시저럴 엔티티라 `rules/testing.md`의 MonoBehaviour 생명주기 제외 대상 — 신규 테스트 0).
- 2026-08-07: WorldChannelManager **재감사** (2026-05-27 감사 이후 893줄 변경, 큐 1순위, score 29) — **P1:1 처리.** **P1: 온라인 필드의 내 레벨이 항상 Lv.1이다.** `joinWorld`·`syncWorld` 페이로드가 `level = PlayerPrefs.GetInt("player_level", 1)`로 채워지는데, 그 키는 **옛 저장소**다 — 전체 저장소를 뒤져보니 쓰는 곳이 `CloudSaveManager.ApplySaveData:467` **단 하나**(클라우드 복원 시 1회)이고, 실제 레벨은 `PlayerProgressController` → `player_progress.json`에 산다(`save-system.md`). 레벨업은 그 JSON에만 남고 이 키를 갱신하지 않으므로 **신규 설치·로컬 플레이는 영원히 1**, 클라우드에서 복원한 계정은 **복원 시점 값으로 굳는다.** 이 값은 서버로 올라가 다른 사람 화면에 그대로 뜬다 — `WorldFieldMultiplayerUI`의 근처 탐험가 배너(:163)·플레이어 라벨(:328)·친구 목록(:510)과 `WorldLobbyUI`의 로비 목록(:439), **4곳**이다. **이 결함은 이미 한 번 진단된 것이다** — `CloudSaveManager` 상단 주석이 *"옛은 PlayerPrefs(player_level/player_candies/player_coins)를 읽어 JSON 파일에 저장하는 실제 시스템과 어긋나 레벨/XP/캔디/코인이 클라우드에 전혀 동기화되지 않았음(항상 기본값 0/1 업로드)"*라고 적어두고 그 파일은 `progressController != null ? progressController.Level : PlayerPrefs...` 형태로 고쳤는데, **`WorldChannelManager`만 그 정정에서 빠졌다.** 같은 폴백 식을 쓰는 `LocalPlayerLevel` 프로퍼티로 교체했다(진행도 컨트롤러 우선, 없으면 옛 키). 조회는 `CacheLocalPlayer`와 같은 지연 캐시라 비용 프로필이 같고, 호출 지점도 Update가 아니라 join/sync 코루틴(최대 1초에 1회)이다. **거짓양성으로 제외 2건**: ①`AuthManager.Instance.IdToken`·`.DisplayName`을 null 가드 없이 역참조하지만 모든 진입점이 `CanStartRequest()`/`Update()`에서 `IsFirebaseReady()`(=`Instance != null && IsLoggedIn && IdToken 비어있지 않음`)를 먼저 통과한다. ②`CacheLocalPlayer`가 `PlayerMovement`를 못 찾으면 매 sync마다 `FindFirstObjectByType`을 다시 부르지만 1초에 1회이고 파괴된 참조를 되찾는 데 필요한 형태다(Unity의 fake-null). **통과 확인**: `SendRequest`에 `timeout = 12`가 있어 half-open 연결이 `IsBusy`/`syncInFlight`를 영구 true로 고정하는 소프트락을 막고, 코루틴 5종이 전부 `try/finally`로 플래그를 복구하며(이벤트 핸들러 예외에도), sync 응답이 돌아왔을 때 `CurrentWorld == null`이면 stale 응답으로 월드를 되살리지 않고(`나가기 눌렀는데 재입장` 차단), `RespondInviteRoutine`이 `JoinWorldRoutine` 호출 **전에** 자기 `IsBusy`를 먼저 푸는 순서도 정확하다. 401 재시도가 `allowRetry=false`로 한 번만 돌아 무한 재귀가 없다. 검증: error CS 0, ci_check 8검사 통과, PlayMode **405/405**(네트워크 경로라 `rules/testing.md`의 외부 서비스 제외 대상 — 신규 테스트 0).
- 2026-08-07: CollectionUI **재감사** (2026-05-21 감사 이후 913줄 변경, 큐 1순위, score 226) — **P1:1 처리 + 사장 코드 2건 제거.** **P1: 보유 목록이 행마다·매 패스마다 문자열 4개를 새로 만든다.** `DrawInsectItem`이 ①`data.rarity.ToString()`(enum → **박싱** + 문자열) ②`"  |  " + SizeLabel(SizeMm(...))`(계산 + 할당 + 연결) ③`$"Lv.{level} | {rarity} | IV: {n}%{size}"` ④`$"HP:{n} ATK:{n} DEF:{n}"`를 매번 만든다. 네 값 모두 **(종·개체·레벨) 파생이라 불변**인데 OnGUI는 Layout·Repaint·입력마다 패스가 도므로 프레임당 여러 번 반복된다. 이 세션의 3D 썸네일 컬링 스윕이 같은 루프(`CollectionUI:381`)를 이미 손봤지만 **컬링은 "몇 행을 그리나"를 줄일 뿐 "행마다 무엇을 새로 만드나"는 그대로**라, 보이는 행 × 패스 수만큼 계속 나오고 있었다. 고치는 방법은 저장소 안에 이미 두 번 있다 — `RegionMapUI` 재감사(2026-08-04)가 "항목마다 매 패스 `$"등급 | CP n"` 보간+enum 박싱"을, `TrainingUI` 재감사(2026-08-06)가 "개체마다 정보 문자열 2개를 매 패스 생성"을 같은 방식으로 고쳤다. **CollectionUI만 남아 있었다.** 무효화 배선을 새로 만들 필요도 없었다 — `ownedCacheDirty`가 이미 `InsectUpdated` 구독으로 돌아가고, `TryLevelUpWithCandy`가 `data.level++` 직후 그 이벤트를 쏘므로(PlayerInsectCollection:389) 레벨이 바뀌면 문자열도 함께 다시 굽힌다. `cachedOwned`를 다시 만드는 바로 그 자리에 `BuildRowTextCache()`를 붙이고 행 그리기는 구운 문자열을 받기만 한다. **사장 코드 2건**: 빈 `Update()`(본문이 없어도 Unity가 매 프레임 managed→native 호출을 한다)와 빈 `DrawToggleButton()`(`OnGUI` 첫 줄에서 `isOpen` 검사보다 **앞에** 불려 화면이 닫혀 있을 때도 매 패스 돌았다) 제거 — 퀵바 진입점은 `QuickAccessBarUI`가 맡는다. **거짓양성으로 제외 1건**: score 226의 실체는 `InitDetailStyles`의 GUIStyle 30여 개 + `InitItemStyles` 5개인데 둘 다 `detailStylesReady`/`itemStylesReady` 가드로 1회만 돈다(LoginUI 183·TrainingUI 246과 같은 형태 — 채점기는 가드를 못 본다). **통과 확인**: `OnEnable`↔`OnDisable`의 `InsectUpdated` 짝이 성립하고(`-=` 뒤 `+=`), `ModalUIRegistry` 등록/해제가 `Toggle`·`CloseModal`·`OnDisable` 세 경로 모두에 있으며, `TutorialQuestManager.Instance`는 null 가드, 상세 패널의 스크롤 뷰포트가 `Mathf.Max(1f, ...)`로 음수를 막고, 설명 라벨이 `UIHelper.LabelFit`을 써 고정 84px 상자에서 잘리지 않는다. 상세 패널·학습기술 목록도 매 패스 문자열을 만들지만 **한 번에 개체 하나 · 5행 안팎**이라 목록과 규모가 다르고, 이번 라운드는 목록만 다뤘다. 검증: error CS 0, ci_check 8검사 통과, PlayMode **405/405**(OnGUI 경로라 `rules/testing.md`의 IMGUI 제외 대상 — 신규 테스트 0).
- 2026-08-07: RaidBattleUI **재감사** (큐 1순위, score 33, "2026-08-06 감사 이후 2961줄 변경") — **clean P0/P1, 파일 수정 0건.** **후보 점수의 근거가 통째로 허수였다** — git numstat이 센 2961줄은 대부분 `RaidBattleUI.Draw.cs`로의 **이동**이고, 실제 추가는 116줄(그중 상당수가 주석, 5줄은 오늘 내가 넣은 카메라 호출)이다. 채점기는 "무엇이 왜 바뀌었나"를 모르고 줄 수만 세므로 **파일 분할이 곧 재감사 최우선순위로 둔갑한다** — 스킬 문서가 "점수는 열어볼 순서이지 무엇이 문제인지가 아니다"라고 적어둔 그대로다. **사장 코드 계열 기계 점검(0건)**: 이 파일의 과거 결함 3건이 전부 "5f0776f 파이프라인 교체가 남긴 죽은 배선"(`TriggerBossAttackEffect`·`FinishTurnAnnounce`·`SetBossDamage`)이었으므로, 두 파일의 주석을 걷어낸 뒤 private 필드 전량의 쓰기/읽기 횟수를 세어 불균형을 찾았다. 걸린 3건(`TeamRushMinDuration`·`UniteRushMinDuration`·`BossImpactMinDuration`)은 전부 `const`라 선언이 쓰기로 잡힌 것이고 각각 `Update`에서 정확히 한 번 읽힌다 — 진짜 사장 필드는 없다. **입력 표면 잔존 4종 전수 추적(전부 안전)**: `insectBtnRects`·`raidSkillRects`·`stanceRects`·`uniteBtnRect`는 OnGUI가 채우고 Update가 읽는데 Update가 프레임에서 **먼저** 돌아 한 프레임 낡은 값을 본다. ①슬롯 선택 → SelectSkill 전이는 `else if` 체인이라 같은 프레임에 스킬 분기가 이어 돌지 않고, ②`wantMouseClick`이 Update 말미에 매 프레임 소거돼(이전 라운드가 고친 자리) 배너를 넘긴 탭이 슬롯 선택으로 새지 않으며, ③`stanceRects`는 그리는 곳이 `DrawInsectSelector` 하나뿐이고 히트 테스트도 SelectInsect에서만 하며, ④`uniteBtnVisible`은 `DrawUniteGaugeBar`가 Intro/Result에서 **세팅 전에 early-return**해 낡은 값이 남을 수 있으나, `EndRaid`가 false로 비우고 클릭 경로가 `CanUniteAttack`(IsActive + roundStage Ready + 게이지 만충 + 생존 2 이상)을 함께 요구해 도달 불가다 — 잠재 형태이지 현재 결함이 아니라 P1로 올리지 않았다. **통과 확인**: `OnEnable`↔`OnDisable`이 컨트롤러 이벤트 5개를 정확히 짝짓고(`SubscribeRaidController`를 `AutoWire`와 공유, `-=` 뒤 `+=`라 중복 없음 — 오프닝 다시보기가 UI 루트를 토글해도 되살아난다), `AudioManager`·`TutorialQuestManager` 싱글턴은 참조 전부 null 가드, `cachedRegionMgr`가 `FindFirstObjectByType`을 레이드 승리당 1회로 묶고, `EndRaid`가 미초기화 상태로 남기는 필드들(`actionTimer`·`bossShake`·`resultShown`·`displayBossHp`)은 5초 결과 화면에서 자연히 소진되거나 `OnRaidUpdated`의 시작 블록이 다시 채운다. **문서가 못박은 교차 불변식도 확인**: 이 파일 `OnDisable`의 `timeScale` 복구가 오프닝 다시보기를 깨뜨리지 않으려면 `ApplyReplayBlock`이 `SetActive(false)`를 `Time.timeScale = 0f`보다 **먼저** 불러야 하는데(`rules/ui-layout.md`), 현재 코드가 그 순서이고 "반드시 마지막" 주석까지 살아 있다. 코드 변경이 없어 테스트는 재실행하지 않았다(직전 검증 상태 유지: error CS 0, ci_check 통과, PlayMode 405/405).
- 2026-08-07: RegionTerrainBuilder **재감사** (2026-05-21 감사 이후 1147줄 변경, 큐 마지막 항목) — **P1:1 처리 + 큐 재생성.** **P1: 월드 지형지물이 실행마다 다른 자리에 선다.** 이 빌더는 `Random.Range`를 **87번** 부르는데 프로젝트 어디에도 `Random.InitState`가 없다 — Unity 전역 난수는 실행마다 다른 상태에서 시작하므로 나무 25그루·통나무·바위·죽은나무·버섯·생울타리·아치·폐허 벽/기둥의 배치가 **매 실행 새로 뽑힌다.** 장식이면 넘어갈 일이지만, `Prim()` 55곳 중 **9곳이 collider를 남긴다**(나무 trunk·통나무·숲 바위·죽은나무·산 바위·생울타리·아치 기둥·폐허 벽·폐허 기둥). 그 9종은 `PlayerMovement.IsBlockedPosition`의 OverlapSphere가 막는 실제 장애물이라, **지나갈 수 있는 길 자체가 실행마다 바뀐다** — 어제 걷던 경로가 오늘 막히고, 그런 버그는 재시작하면 사라져 재현조차 되지 않는다(`CameraFollower.ResolveObstruction`의 차폐 판정에도 같은 콜라이더가 걸린다). 고정 시드(`TerrainLayoutSeed`)로 배치 구간만 가두되 **`Random.state`를 저장·복원**했다 — 전역 난수를 고정한 채 두면 스폰·IV·포획 판정까지 결정론이 되어 훨씬 나쁜 문제가 되고, 빌드 중 예외가 나도 반드시 되돌아가도록 `finally`에 뒀다. 세션 안의 다양성은 그대로고 세션 사이의 안정성만 생긴다. **거짓양성으로 제외 3건**: ①`Mat()`이 `new Material`을 64번 만들고 정리 코드가 없어 누수로 보이지만, 지형은 부팅 시 1회 짓고 씬 수명 내내 존재하는 영구 객체다(마네킹·연출처럼 반복 생성·파괴되는 경로가 아니다). ②그 64개가 루프 안에서 만들어지면 오브젝트마다 머티리얼이 하나씩 생겨 배칭이 깨지는데, **실측 결과 루프 안 호출은 0건**이고 `BuildFenceArc`처럼 루프 밖에서 한 번 만들어 60개 기둥이 공유한다 — 이미 올바른 형태다. ③`Prim()`이 `SetParent`를 하지 않아 씬 루트가 지저분해지지만 `BuildAllRegions`의 호출부는 `PlaySceneBootstrap` 1곳·1회뿐이라 중복 생성이나 정리 누락이 생기지 않는다. **통과 확인**: 랜덤 소품이 본 마을 부지를 피하는 `RandomSpotAvoiding`이 meadow의 랜덤 배치 2곳 모두에 걸려 있고(상수는 `VillageBuilder`가 단일 출처), 나무 배치 반경이 최대 0.65R이라 1.0R 근처의 fence gateway(통로)를 막지 않으며, `centerPosition.y`가 7리전 모두 0이라 과거의 `c.y` 이중 합산 계열 회귀는 재발 여지가 없다. MonoBehaviour이지만 생명주기 콜백·이벤트 구독·싱글턴 참조가 하나도 없어 검사 항목 1~4·7은 해당 없음. **큐 재생성**: 이 항목으로 Uncovered가 0이 돼 `audit_candidates.py`로 15건을 새로 뽑았다(1순위는 오늘 2961줄이 바뀐 `RaidBattleUI`). 검증: error CS 0, ci_check 8검사 통과, PlayMode **405/405**(지형 생성은 씬 의존이라 `rules/testing.md`의 MonoBehaviour 생명주기 제외 대상 — 신규 테스트 0).
