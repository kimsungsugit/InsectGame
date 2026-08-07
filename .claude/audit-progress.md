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

- [ ] RaidBattleUI 재감사 (UI/RaidBattleUI.cs, 806줄, score 33) — 2026-08-06 감사 이후 2026-08-07까지 2961줄 변경
- [ ] CollectionUI 재감사 (UI/CollectionUI.cs, 831줄, score 226) — 2026-05-21 감사 이후 2026-08-07까지 913줄 변경
- [ ] WorldChannelManager 재감사 (Core/WorldChannelManager.cs, 511줄, score 29) — 2026-05-27 감사 이후 2026-07-17까지 893줄 변경
- [ ] InsectEntity 재감사 (Spawning/InsectEntity.cs, 1807줄, score 8) — 2026-05-21 감사 이후 2026-07-19까지 883줄 변경
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

- 2026-08-07: RegionTerrainBuilder **재감사** (2026-05-21 감사 이후 1147줄 변경, 큐 마지막 항목) — **P1:1 처리 + 큐 재생성.** **P1: 월드 지형지물이 실행마다 다른 자리에 선다.** 이 빌더는 `Random.Range`를 **87번** 부르는데 프로젝트 어디에도 `Random.InitState`가 없다 — Unity 전역 난수는 실행마다 다른 상태에서 시작하므로 나무 25그루·통나무·바위·죽은나무·버섯·생울타리·아치·폐허 벽/기둥의 배치가 **매 실행 새로 뽑힌다.** 장식이면 넘어갈 일이지만, `Prim()` 55곳 중 **9곳이 collider를 남긴다**(나무 trunk·통나무·숲 바위·죽은나무·산 바위·생울타리·아치 기둥·폐허 벽·폐허 기둥). 그 9종은 `PlayerMovement.IsBlockedPosition`의 OverlapSphere가 막는 실제 장애물이라, **지나갈 수 있는 길 자체가 실행마다 바뀐다** — 어제 걷던 경로가 오늘 막히고, 그런 버그는 재시작하면 사라져 재현조차 되지 않는다(`CameraFollower.ResolveObstruction`의 차폐 판정에도 같은 콜라이더가 걸린다). 고정 시드(`TerrainLayoutSeed`)로 배치 구간만 가두되 **`Random.state`를 저장·복원**했다 — 전역 난수를 고정한 채 두면 스폰·IV·포획 판정까지 결정론이 되어 훨씬 나쁜 문제가 되고, 빌드 중 예외가 나도 반드시 되돌아가도록 `finally`에 뒀다. 세션 안의 다양성은 그대로고 세션 사이의 안정성만 생긴다. **거짓양성으로 제외 3건**: ①`Mat()`이 `new Material`을 64번 만들고 정리 코드가 없어 누수로 보이지만, 지형은 부팅 시 1회 짓고 씬 수명 내내 존재하는 영구 객체다(마네킹·연출처럼 반복 생성·파괴되는 경로가 아니다). ②그 64개가 루프 안에서 만들어지면 오브젝트마다 머티리얼이 하나씩 생겨 배칭이 깨지는데, **실측 결과 루프 안 호출은 0건**이고 `BuildFenceArc`처럼 루프 밖에서 한 번 만들어 60개 기둥이 공유한다 — 이미 올바른 형태다. ③`Prim()`이 `SetParent`를 하지 않아 씬 루트가 지저분해지지만 `BuildAllRegions`의 호출부는 `PlaySceneBootstrap` 1곳·1회뿐이라 중복 생성이나 정리 누락이 생기지 않는다. **통과 확인**: 랜덤 소품이 본 마을 부지를 피하는 `RandomSpotAvoiding`이 meadow의 랜덤 배치 2곳 모두에 걸려 있고(상수는 `VillageBuilder`가 단일 출처), 나무 배치 반경이 최대 0.65R이라 1.0R 근처의 fence gateway(통로)를 막지 않으며, `centerPosition.y`가 7리전 모두 0이라 과거의 `c.y` 이중 합산 계열 회귀는 재발 여지가 없다. MonoBehaviour이지만 생명주기 콜백·이벤트 구독·싱글턴 참조가 하나도 없어 검사 항목 1~4·7은 해당 없음. **큐 재생성**: 이 항목으로 Uncovered가 0이 돼 `audit_candidates.py`로 15건을 새로 뽑았다(1순위는 오늘 2961줄이 바뀐 `RaidBattleUI`). 검증: error CS 0, ci_check 8검사 통과, PlayMode **405/405**(지형 생성은 씬 의존이라 `rules/testing.md`의 MonoBehaviour 생명주기 제외 대상 — 신규 테스트 0).
- 2026-08-07: CharacterPortraitRenderer **재감사** (2026-05-27 감사 이후 1119줄 변경, 큐 1순위) — **P1:2 처리.** **P1-①: `DrawWithOutfit`가 주변 `GUI.color`를 오염시킨 채 반환한다.** 내부 `Draw`는 마지막 줄에서 `GUI.color = Color.white`로 스스로 되돌리는데, `DrawWithOutfit`은 그 **뒤에** `DrawArmsAsSkin`·`DrawBackpackWithSlot`·`DrawOutfitAccessories` 셋을 더 붙이고 그 셋은 전부 `DrawCol`(= `GUI.color = col` 후 그리기)로 끝난다. IMGUI의 `GUI.color`는 이후 모든 그리기에 곱해지므로, **호출부가 다음에 그리는 것이 마지막 파츠 색으로 물든다.** 실제 피해는 `CashShopUI:269-275`다 — `DrawWithOutfit` 바로 아래 "보유 재화"·"💎 {gems}"·"🪙 {coins}" 라벨 3개가 색을 따로 세팅하지 않아 그대로 곱해진다. 하필 `DrawOutfitAccessories`의 마지막 분기가 악세서리 색이고 **기본 악세서리 색이 `(0.1, 0.1, 0.1)`**이라, 그 계열을 장착하면 재화 라벨이 거의 검정으로 깔려 안 보이는 수준까지 간다. 도구만 낀 상태면 망 색(0.95 크림)이라 미묘하고, 장비 조합에 따라 증상이 오락가락해 재현이 어려운 형태다. 같은 파일의 `DrawItemPreview`는 `prevCol`을 저장·복원하고 `Draw`도 스스로 되돌린다 — **저자가 이 위험을 알고 두 곳엔 방어를 했는데 공개 진입점 하나만 빠졌다.** `CashShopUI:254`가 `DrawWithOutfit` **직전에** `GUI.color = Color.white`를 두고 있는 것도 같은 오해의 흔적이다(복원이 필요한 쪽은 호출 뒤다). 공개 진입점이 스스로 되돌리게 고쳐 호출부가 이 사정을 몰라도 되게 했다. **P1-②: `DrawOutfitAccessories`가 `OutfitCache`를 우회한다.** 이 파일의 캐시 블록은 주석에 *"매 OnGUI 호출 시 PlayerPrefs 5회 + GetEquipped 8회를 60FPS×13회/초=780회/초 차단"*이라고 목적을 못박아 뒀는데, 정작 `DrawOutfitAccessories:509`가 `PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.Gender"))`를 **매 패스 직접** 읽고 있었다 — 값은 이미 `cache.gender`에 있다. `PrefsKey`→`AuthManager.ScopedKey`는 `baseKey + "." + uid` **문자열을 매번 새로 만들므로** 네이티브 조회에 더해 프레임 할당까지 붙는다(OnGUI는 Layout·Repaint·입력마다 패스가 돌아 초당 60회보다 많다). 유일한 호출부 `DrawWithOutfit`이 이미 `gender`를 지역에 들고 있어 매개변수로 넘겼다. 이제 이 파일의 `PlayerPrefs` 접근은 `RefreshCache` 5줄뿐이다. **거짓양성으로 제외 2건**: ①`SortRecipeByDepth`가 static `recipeOrder[32]`를 공유해 "동시 호출 시 뒤섞임"으로 보이지만 Unity는 단일 스레드고 정렬→그리기가 `DrawRecipePreview` 안에서 동기적으로 끝난다. 32칸 상한의 조용한 절단도 확인했으나 **실제 레시피 45개의 파츠 수는 최대 2개**라 도달 불가. ②`EnsureSubscribed`가 매니저 파괴 시 `-=`를 건너뛰는 것처럼 보이지만, 파괴된 MonoBehaviour는 Unity의 `!= null` 오버라이드가 false를 주고 그 이벤트도 함께 사라지므로 결과가 같다(새 매니저가 오면 재구독 + 캐시 무효화도 정상). 검증: error CS 0, ci_check 8검사 통과, PlayMode **405/405**(둘 다 OnGUI 경로라 `rules/testing.md`의 IMGUI 제외 대상 — 신규 테스트 0).
- 2026-08-07: PlayerVisualBuilder **재감사** (2026-05-20 감사 이후 1170줄 변경, 큐 1순위) — **P1:1 처리 + 트랩 1건 제거.** **P1: 얼굴·머리 장식 머티리얼이 정리 목록에 없다.** `OnDestroy`가 `SafeDestroyMat(ref hatMat)` 식으로 **슬롯 필드 11개를 손으로 나열**하는데, `BuildFace`(눈·동공·하이라이트·눈썹·코·입 + 여성 홍조·속눈썹)와 `BuildUpHair`(리본)가 만드는 6~9개는 **지역 변수**라 그 목록에 오를 수가 없었다. 실플레이어는 영구 객체라 영향이 실질적 0이지만(파일 주석이 그렇게 적어둔 근거가 여기까지만 맞았다), **마네킹은 파괴가 정상 수명**이다 — `CharacterModelPreviewRenderer.EnsureMannequin`이 외형 해시가 바뀔 때마다 `Destroy(mannequin)` 후 다시 짓는다. 즉 외형을 바꿀 때마다 6~9개씩 샜다. 하필 `SafeDestroyMat`의 `previewMode` 분기 주석이 "여기서 강제로 지우지 않으면 마네킹 하나당 머티리얼 11개가 영구히 샌다"라고 **정확히 이 문제를 알아채고도 11개(=필드 개수)만 세었다** — 실제 생성량은 17~20개다. 생성 지점이 `MakeMaterial` 하나뿐이므로 static을 인스턴스 메서드로 바꿔 `runtimeMaterials`에 등록하고 `OnDestroy`가 목록을 일괄 처리하게 했다 — **호출 19곳을 건드리지 않았다**(2026-08-06 `BattleArenaController` 라운드가 31곳에 쓴 것과 같은 형태). 부수 효과로 `GetComponentsInChildren<MeshRenderer>` 조회가 필드마다 11번 돌던 것이 1번으로 줄었다. **트랩 제거**: `BuildFace`가 코에 쓰려고 **필드와 같은 이름의 지역 변수** `Material skinMat`을 같은 색으로 하나 더 만들고 있었다 — 피부색을 한 곳에서 바꾸려 하면 코만 옛 색으로 남는다. 필드를 쓰도록 접고 머티리얼 하나를 없앴다. **거짓양성으로 제외 1건**: 그 지역 `skinMat` 때문에 "코 색이 안 따라간다"를 P1로 올릴 뻔했으나, `CharacterOutfitManager.ApplyToCharacter`가 실제로 칠하는 노드는 Cap/CapBrim/Shirt/Body/Arm/Leg/Boot/Backpack/Net 14개뿐이고 **피부 노드는 아무도 recolor하지 않는다**(skinColor는 outer_none일 때 팔 색으로만 쓰인다). 현재는 시각 결함이 아니라 잠재 함정이다. **통과 확인**: `Awake`↔`BuildForPreview`의 `builtOnce` 상호 배제가 비활성 AddComponent 계약과 정확히 맞고(활성 상태로 붙이면 PlayerPrefs 외형으로 먼저 지어진다는 주석대로), `OnEnable`↔`OnDisable`의 `OutfitChanged` 짝이 `subscribedToOutfit` 가드로 중복 없이 성립하며(마네킹은 `previewMode`로 구독 자체를 건너뛴다 — 구독하면 핸들러가 `GameObject.Find("Player")`로 실플레이어까지 갱신하는 조용한 버그가 된다), `CharacterOutfitManager.Instance`는 참조 3곳 전부 null 가드, `MakeMaterial`의 셰이더 4단 폴백과 진단 로그 1회 래치도 정상. 검증: error CS 0, ci_check 8검사 통과, PlayMode **405/405**(머티리얼 파괴는 `rules/testing.md`의 MonoBehaviour 생명주기 제외 대상이라 신규 테스트 0).
- 2026-08-06: UIShapes (큐 1순위, score 0) — **P1:1 처리.** 147줄짜리 도형 헬퍼인데 **계산 방향이 뒤집힌 진짜 그림 버그**가 있었다. **P1: `Part()`의 중간 roundness 혼합이 반대로 동작한다.** disc 위에 덮는 안쪽 사각형의 인셋을 `(1f - r)`로 잡아서, r이 클수록(둥글어야 할수록) 덮는 사각형이 커져 **각지게** 보이고 r이 작을수록 조각으로 줄어 **둥글게** 보였다 — 정확히 반대다. 경계로 확인하면 명백하다: r=0.02면 disc가 거의 다 드러나 타원, r=0.98이면 사각형이 98%를 덮어 네모다. **사장 코드가 아니라 살아 있는 경로였다** — 호출 277개 중 275개는 기본값 1f(순수 타원 분기)라 무사했지만, 2D 의상 카드 폴백의 `CharacterPortraitRenderer.RecipeRoundness`가 **Cube에 0.12f, 세운 원통에 0.8f**를 실제로 넘긴다. 즉 큐브 파츠가 타원으로, 원통 파츠가 사각형으로 그려지고 있었다. 그 폴백은 3D 썸네일이 구워지기 전 몇 프레임 동안 보이는 그림이라 품질이 중요하다고 파일 주석이 못박은 자리이기도 하다. 인셋을 roundness에 **비례**시키고(`BlendInsetRatio`), 그리기와 분리된 순수 수식으로 떼어 테스트 5건으로 방향·연속성을 고정했다 (경계 0/1, 단조 증가, clamp, 양쪽 순수 분기 임계에서의 연속). **통과 확인**: `Disc` 텍스처는 정적 지연 생성 후 재사용이고 파괴돼도 Unity의 `!= null` 오버라이드가 받아 다시 굽는다, `Capsule`이 `GUI.matrix`를 저장·복원하며 회전, `new Rect`/`new Color`는 struct라 GC 없음. MonoBehaviour가 아니라 구독·싱글턴·Bootstrap·세이브 항목은 해당 없음. 검증: error CS 0, ci_check 8검사 통과, PlayMode **396/396**(신규 5). **큐 재생성**: Uncovered가 0이 돼 `audit_candidates.py`로 새로 뽑았다 — 이제 전부 **재감사 후보**이고, 이번 세션에 대폭 바뀐 `RaidBattleController`(레이드 5단계)가 큐에 들어왔다.
- 2026-08-06: **3D 썸네일 목록 컬링 일괄 스윕** (큐 순서 대신 사용자 요청으로 횡단 점검) — **P0:4 처리.** 직전 TrainingUI 라운드에서 "726795a가 건드린 화면이 9곳인데 도감·훈련 2곳만 컬링됐다"는 걸 발견해, 큐 순서(LoginUI)를 미루고 `InsectVisual.Draw` 호출부를 **전수(11곳) 기계적으로** 훑었다 — 각 호출의 위 45줄에서 루프·BeginScrollView·컬링 흔적을 찾는 스크립트로 판정했다. **컬링 없이 전 항목을 돌던 4곳**: `BattleTeamUI:324`(곤충 선택기), `CollectionUI:381`(보유 목록), `HospitalUI:248`(치료 대상 목록), `RegionMapUI:668`(리전 도감). 전부 `DexBrowseLayout.GetVisibleRowRange`로 통일했다 (UI→Dex는 `InsectVisual`·`PlayerStatusHUD` 등이 이미 쓰는 방향). **해당 없음으로 판정한 5곳**(전부 근거 확인): `DexScreenUI:703`은 이미 컬링된 그리드 루프가 부르는 타일 내부, `BattleTeamUI:207`·`CaptureChoiceUI:504`는 고정 5슬롯, `CaptureChoiceUI:186`·`CollectionUI:468`은 단일 개체. **재발 방지**: 원인은 개별 화면의 부주의가 아니라 **썸네일 도입 커밋 하나가 6개 화면에 동시에** 같은 결함을 심은 것이므로, 단일 진입점 `InsectVisual`의 클래스 주석에 "목록에서 부를 때는 반드시 뷰포트 컬링을 먼저 하라"를 이유·계산 함수·이력과 함께 못박았다. 새 목록을 만드는 사람이 반드시 읽는 자리다. **이 스윕이 왜 필요했나**: 도감 라운드(P0)에서 결함을 정확히 진단하고도 **같은 썸네일을 쓰는 다른 화면을 보지 않아** 5개 화면이 그대로 남아 있었다. 라운드 단위 감사가 "한 커밋이 여러 화면에 심은 결함"에 약하다는 사례다. 검증: error CS 0, ci_check 8검사 통과, PlayMode **396/396**(컬링 순수부는 도감 라운드의 8케이스가 이미 고정).
