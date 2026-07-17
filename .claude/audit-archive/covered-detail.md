# Covered 상세 — 각 라운드의 변경 내역 원문

`.claude/audit-progress.md`의 Covered 인덱스에 대응하는 서술 원문이다.
본체는 훅·스킬이 매 호출마다 읽으므로 이름만 남기고 서술은 여기에 둔다.

### CharacterPortraitRenderer (P0:2, P1:2, 2026-05-20)

DrawWithOutfit 캐싱, DrawItemPreview 추가, footHalfGap 0.18→0.28, Proportions 헬퍼 통일. **사용자 명시 요청**: 캐릭터 미리보기 도구가 magnify만 표시되던 회귀 → DrawCharacterTool 9종 분기 추가 (gun/wand/lasso/shuriken/sword/web_shooter/magnify/camera/기본). ApplyToolShape ↔ DrawToolPreview ↔ DrawCharacterTool 3곳 분기 순서 1:1 정합.

### PlayerVisualBuilder (P1:5, 2026-05-20)

Body Cube화, 어깨 ±0.29, 신발 ±0.19, SafeDestroyMat 가드, 도구 손 좌표

### CloudSaveManager (P1:2, 2026-05-20)

401 토큰 갱신 재시도 (Load/Save), pendingSave 회귀 수정

### PlaySceneBootstrap (P1:1, 2026-05-20)

CriticalBootstrapException fail-fast + OnGUI 사용자 알림, GachaBox.AutoWire(database), RegionDefinitions 분리

### CharacterOutfitUI (P1:2, 2026-05-20)

카드 미리보기 DrawItemPreview 호출, bigTitle/bigBonus CachedStyle

### CharacterOutfitManager (P1:2, 2026-05-20)

ApplyToolShape 손 좌표 재배치, magnify/camera 분리, **도구별 mesh 교체** (Cube/Cylinder/Sphere)로 막대기 회귀 차단

### PlayerMovement (P1:1, 2026-05-20)

OutfitChanged 구독 + InvalidateToolBase, 도구 swing 동기

### CashShopManager (P1:1, 2026-05-20)

wallet 단일 소스, 1회 PlayerPrefs 마이그레이션

### CaptureMinigameController (P0:1, P1:1, 2026-05-20)

InitMinigameStyles 6 캐시 필드, OnDisable 안전망

### GachaBoxManager (P1:1, 2026-05-20)

ValidateInsectId + DB 캐시

### TutorialQuestManager (P1:1, P0:4, 2026-05-20)

CompleteQuest 후 CloudSave 즉시 동기. **튜토리얼 진행 회귀 P0:4건** — NotifyTeamSet/NotifyItemUsed/NotifyTraining/NotifySkillEquipped 호출 누락으로 q_equip/q_item/q_training/q_team이 영구 정지 + 후속 13개 퀘스트 연쇄 정지. 4곳에 Notify 호출 추가 (BattleTeamManager.TeamChanged 구독, ItemEffectManager.ActivateItem, TrainingManager.TrainSkill, TrainingUI EquipSkill 사용자 액션).

### BattleScreenUI (P2:1 DrawCombo + P1:2 P2:1 추가, 2026-05-20)

DrawCombo ComboCol, DrawSwapSelect SwapHeaderBase + alpha 갱신, DisableCanvasBattleUI + CheckGuardianDefeat FindFirstObjectByType 캐싱

### SubAreaWorldBuilder (P0:8 + UI:1, 2026-05-20)

FindSafeSpawnPosition spiral 탐색, BuildCave/BuildTemple 입구 끼임 차단. 진입/퇴장 토스트 알림(3초) + F2 수동 Exit + 우측 하단 안내. RegionManager.ForceExitSubArea(). **사용자 명시 요청 2차**: 9개 환경 boundary wall halfSize를 floor 안쪽으로 조정 (Cave 16→14, DeepForest 18→16, Underwater 16→14, FogSwamp 16→14, MountainPeak 14→11, Temple 14→13, Reeds 14→11, GenericArea 12→9), BuildGreenhouse CreateGlassWall NoCollider 제거(collider 보존), Y 안전망(Y<-3 → 자동 RequestExit + 알림) + cachedPlayerTransform lazy 캐싱.

### AuthManager (P1:2, 2026-05-20)

TryRefreshTokenForRetry public + refreshInProgress 가드. **사용자 명시 요청**: ValidateCredentials 헬퍼 + Register/Login 진입 시 호출 (빈 이메일/짧은 비밀번호 차단, Firebase API 호출 절약). 인증 흐름 전반 검증: TryAutoLogin 단일 호출, idTokenAcquiredAt 갱신 모든 경로 OK, AuthFailed→LoginUI 복귀, 회원가입→CharacterCreate 흐름 정상.

### LoginUI (P1:2, 2026-05-20)

AuthFailed 구독 + 외형 변경 후 InvalidateCache. **사용자 명시 요청**: OnGUI 매 프레임 17개 new GUIStyle 변형 → base 스타일 fontSize 동적 갱신 패턴으로 교체 (BattleScreenUI/DexScreenUI 패턴). OnGUI 본문 잔존 new GUIStyle = 0.

### RaidBattleUI (P0:2, P2:1, 2026-05-20)

EndRaid 재진입 누수(타이머 4개 + 팀 캐시 3개 초기화), GameObject.Find Player → playerMovement.transform 캐싱

### RaidBattleController (P1:1, P0:1, 2026-05-20)

UseUniteAttack 데미지 표시 정합성, CheckEnd 패배 시 BossEntity.Despawn 누락 + OnRaidVictory의 Despawn 가드 분리

### InsectBattleController (P0:2, 2026-05-20)

패배 + 도주 성공 시 enemyEntity.Despawn 누락 — 둘 다 추가. 옛은 전투 후 곤충 필드 잔존, 중첩 발동 가능

### CaptureController (P0:1, 2026-05-20)

미니게임 캡처 실패 시 50% Despawn → 100% Despawn. 사용자 명시: "미니게임 끝나면 사라져야"

### CaptureFeedbackController (clean, 2026-05-20)

Explore 점검 완료. P0/P1 회귀 없음. CaptureResolved += -= 짝 정상, AudioManager/TutorialQuestManager.Instance null 가드 모두 있음, OnGUI 메서드 없음(TextMeshPro 사용), 타이머 흐름 순차적

### BattleArenaController (P1:1, 2026-05-20)

OnDisable ↔ CleanupArena 중복 정리 → OnDisable이 CleanupArena 위임으로 단일화. CleanupArena가 model 필드 null화까지 포함 (DRY + 옛 OnDisable 누락 보강). Explore P0:2 거짓양성 (Rect는 struct stack 할당, SetupNormalBattle 첫 줄 CleanupArena 호출됨)

### DexController (clean, 2026-05-20)

Explore P0/P1 모두 거짓양성. SaveAndNotify는 RegisterEncounter/RegisterCapture 끝에 무조건 호출됨 (라인 51, 70), encounter/capture는 별개 dictionary라 race 없음, 0.1초 debounce 시나리오는 정상 게임 흐름에서 발생 불가. DexListUIController는 별도 시스템 (DexController audit 범위 외).

### DexDetailUIController (clean, 2026-05-20)

Canvas 기반 UI (TMP_Text/Image), OnGUI 없음. 이벤트 구독 없음 (직접 Show/Hide 호출), AutoWire 안전, null 가드 견고 (라인 35-44). Resources.Load는 Show 1회 호출이라 P0 아님. 부수 발견: DexScreenUI에 OnGUI new GUIStyle 다수 → Uncovered 1순위로 분리.

### DexScreenUI (P0:31, 2026-05-20)

매 OnGUI 31개 new GUIStyle + 수십 개 new Color 회귀. InitDexStyles 패턴 + 31개 캐시 필드 + 매 프레임 핫패스 Color 38개 static readonly. OnGUI 본문 잔존 new GUIStyle = 0. 매 프레임 GC 압박 105~150 객체 → 0 객체. DrawTopBar(6) + DrawPokedex loop(6) + DrawDetail(12) + DrawOwnedCard loop(5) + DrawItems/Row(4) + DrawNoSelection(1) + DrawCentered(1) 모두 캐시 사용.

### DexListUIController (clean, 2026-05-20)

Explore P0/P1 모두 거짓양성. SortList의 dexController null 가드는 BuildList:60 early return으로 보장됨. AutoWire 이벤트 짝은 모든 시나리오(null/동일/새 인스턴스)에서 정상. BuildList 재생성은 사용자 액션 시만 호출 (매 프레임 아님).

### SaveService 글로벌 atomic write 라운드 (P1:8, 2026-05-21)

신규 AtomicFileWriter (Core/AtomicFileWriter.cs, 38줄) — tmp 파일 쓰기 + File.Replace로 OS 레벨 atomic rename. 8곳 일괄 교체: PlayerProgressSaveService/PlayerCandyInventory/PlayerCurrencyWallet/PlayerItemInventory/PlayerInsectCollection/BattleTeamManager/DexSaveService/CloudSaveManager.SaveLocalFile. 게임 종료 직전 크래시/SIGKILL 시 부분 쓰기로 인한 세이브 손상 차단. CashShopManager는 PlayerPrefs 사용으로 글로벌 라운드 제외. DexSaveService에 Save(null) 가드도 일관성 추가.

### CollectionUI 핫스팟 (P0:1, P1:3, 2026-05-21)

(a) P0: DrawInsectItem 핫스팟 5개 GUIStyle + 4개 Color 캐시화 — owned.Count × 5 = 최대 100+/프레임 → 0. InitItemStyles + itemNameStyle/itemInfoStyle/itemGradeStyle/itemStatMiniStyle/itemViewStyle + 6 static readonly Color. nameStyle/gradeStyle textColor 동적 갱신(BattleScreenUI 패턴). (b) P1: GetAllOwned 캐싱 — cachedOwned + ownedCacheDirty 플래그, HandleInsectUpdated에서 invalidate. line 116(DrawInsectList) + line 320(DrawStats) 매 프레임 List<PlayerInsectData> 할당 제거. (c) P1: OnEnable/OnDisable/AutoWire에 InsectUpdated -=/+= 짝 추가. 옛은 OnDisable이 ModalUIRegistry.Unregister만 호출, 이벤트 해제 누락. AutoWire가 OnEnable 후 호출되는 경우 isActiveAndEnabled 가드로 구독. (d) P1: DrawCenteredLabel의 new Color 2곳 static readonly. **잔여**: DrawPanel(P1:5)/DrawDetailPanel(P1:10)/DrawStats(P1:5)/DrawLevelUpSection(P1:10)/DrawStatBar(P1:5) — 호출 빈도 1회/프레임으로 P1, 별도 라운드 권고.

### TrainingUI 핫스팟 (P0:1, P1:3, 2026-05-21)

(a) P0: DrawInsectSelect loop 4 GUIStyle + 4 Color 캐시 — owned.Count × 4 = 최대 80+/프레임 → 0. InitInsectSelectStyles + insectSelectSub/Name/Info/BtnStyle + 4 static readonly Color. NameStyle textColor 동적 갱신. (b) P1: GetAllOwned 캐싱 — cachedOwned + ownedCacheDirty + HandleInsectUpdated invalidate (CollectionUI 패턴). (c) P1: OnEnable/OnDisable/AutoWire에 InsectUpdated -=/+= 짝 추가. 옛은 OnDisable이 ModalUIRegistry.Unregister만 호출, 이벤트 해제 누락. AutoWire가 OnEnable 후 호출되는 경우 isActiveAndEnabled 가드. (d) **잔여**: DrawPanel/DrawMethodSelect/DrawSkillLearn/DrawSkillEquip/DrawSkillReplace — 약 40+ new GUIStyle/Color, 호출 빈도 1회/프레임으로 P1, 별도 라운드 권고.

### CollectionUI 잔여 영역 (P1:27, 2026-05-21)

DrawPanel/DrawDetailPanel/DrawStats/DrawLevelUpSection/DrawStatBar/DrawCenteredLabel 27개 new GUIStyle 일괄 캐시화. InitDetailStyles + 25 캐시 필드(panelTitle/Close/TabActive/TabInactive, detailBack/Name/Rarity/GradeDisp/GradePerc/Desc/Hint, statsLabel/Value/CandyVal, luLvLabel/Num/XpLabel/XpVal/MaxLv/Btn/CandyInfo/Msg, barLabel/Iv/Total/IvLabel, centeredLabel) + 25 static readonly Color. 5개 메서드 모두 textColor 동적 갱신 패턴(detailName/Rarity/GradeDisp/GradePerc + luCandyInfo/Msg + barIv + centeredLabel). DrawStats에 GetCachedOwned 사용 추가(매 프레임 GetAllOwned 회피). 동적 rarityCol scaled/alpha scaled new Color는 struct stack 거짓양성. OnGUI 본문 잔존 new GUIStyle = 0.

### TrainingUI 잔여 영역 (P1:34, 2026-05-21)

DrawPanel/DrawBackButton/DrawMethodSelect/DrawSkillLearn/DrawSkillEquip/DrawSkillReplace/DrawSkillCard/DrawFeedback 34 new GUIStyle 일괄 캐시화. InitTrainDetailStyles + 33 캐시 필드 + 38 static readonly Color. 8 메서드 textColor 동적 갱신 패턴(methodName/CardName/Cost + learnHeader + equipName/SlotName/LearnedName + replaceNew/OldName + cardSkillName + feedback). themeColor scaled new Color (line 246)는 struct stack 거짓양성, feedback alpha 동적 new Color도 거짓양성. OnGUI 본문 잔존 new GUIStyle = 0.

### PlayerStatusHUD (P0:1, P1:2, 2026-05-21)

(a) P0: OnGUI 본문 new Color 16곳 → 16 static readonly Color 일괄 추출 (PanelBg/AccentBlue/Divider, LvBadgeBgDark/Accent, XpBarBg/FillDark/Light, StatCandyPink/CoinGold/GemBlue/TeamOrange/OwnedGreen/DiscoveredBlue/CapturedGold, RegionDefault). DrawResourceSection 4 + DrawCollectionSection 3 + DrawLevelSection 5 + DrawPanel/DrawRegionSection 3+1. (b) P1: GetAllOwned 캐싱 — cachedOwnedCount + ownedCountCacheDirty + HandleInsectUpdated invalidate (CollectionUI/TrainingUI 패턴). (c) P1: OnEnable/OnDisable에 InsectUpdated 구독/해제 짝 추가 (subscribedInsects 플래그). 옛은 매 프레임 DrawCollectionSection이 GetAllOwned() 호출하여 새 List 할당. shine alpha 동적/regionCol scaled/accent scaled/alertCol alpha 동적 new Color는 struct stack 거짓양성. new Rect 좌표 동적도 거짓양성.

### TrainingManager (P1:1, 2026-05-21)

TrainSkill의 SpendCandy 호출을 LearnSkill/ReplaceSkill 성공 후로 이동 + 실패 시 early return. 옛은 ReplaceSkill 실패(replaceSkillId가 learnedSkillIds에 없는 경우 등) 시 candy 차감 후 학습 안 됨 → candy 손실. 정상 흐름(TrainingUI.DrawSkillReplace의 learned 리스트에서 선택)에선 안전하나 방어 부재. LearnSkill은 line 64(HasLearnedSkill) + line 65(ContainsKey) + line 75(IsSkillsFull) 사전 가드로 실패 불가(거짓양성)이나 일관성 차원에서 동일 패턴 적용. CanTrain vs SpendCandy race / 자동 장착 NotifySkillEquipped 미발화 / TrainingCompleted vs NotifyTraining 순서 모두 거짓양성.

### CashShopManager 잔여 영역 (P1:3, 2026-05-22)

(a) PurchaseWithGems line 119 stale `gems` 캐시 → `Gems` 프로퍼티(wallet 우선) 사용 — 외부 wallet 변경 후 잘못된 잔액 검사 차단. (b) line 137 `gemsBefore = gems` → `Gems` — 환불 액수 stale 방지. (c) AddGems(0) early return — 무의미한 GemsChanged 발화 + 구독자 무의미한 갱신 차단. 단일 스레드 race는 모두 거짓양성, FindFirstObjectByType 매 호출은 결제 빈도 낮음으로 P2, shopItems 데이터 검증은 inline 정의로 수동 책임 P2.

### InsectSpawner Spawn/Despawn 다중 호출 검증 (clean, 2026-05-22)

진짜 P0/P1 회귀 없음. 직전 라운드 InsectEntity.despawnedThisCycle 플래그 동작과 일관. DespawnAllActiveInsects snapshot + Clear 패턴 안전(콜백 Remove no-op), DespawnFarInsects 역순 for + Remove 안전(i-- 인덱스 보존), 풀 재사용 시 Initialize의 cache/플래그 리셋으로 안전, OnSubAreaChanged 빠른 Exit는 currentSubArea != subArea 가드(line 698), DespawnEntity 외부 직접 호출 없음, activeInsects 중복 추가 불가, Unity 단일 스레드로 모든 race 거짓양성. SpawnSubAreaInsects maxActiveTotal 검사 부재는 DespawnAll 직후 호출이라 P2 수용. Covered 이동만.

### RegionMapUI 깊은 점검 (clean P0, P1 보류, 2026-05-27)

Explore P0:2(이벤트 누락/null 가드) 모두 거짓양성. 라인 143 `regionManager == null || regionManager.Regions == null` early return 가드 있음. CurrentRegion stale은 OnGUI 매 프레임 regionManager.CurrentRegion 직접 조회로 정합. 30+ new GUIStyle 매 OnGUI는 진짜 P0이나 변경 폭 크고 RegionMapUI는 모달 토글 시만 진입(매 프레임 아님) → P1 별도 라운드 권고.

### TutorialQuestUI DrawQuestPanel 캐시 (P0:1, 2026-05-27)

DrawQuestPanel은 OnGUI에서 매 프레임 호출되어 핫스팟. 5 GUIStyle(doneStyle/title/desc/prog/hint) 매 프레임 new → InitQuestPanelStyles + 5 캐시 필드 + 3 static readonly Color (DoneTextCol/QuestTitleCol/QuestHintBaseCol). hintStyle은 alpha 동적이라 textColor만 갱신 (BattleScreenUI 패턴). AutoWire 이벤트 중복 구독은 라인 499-504 `-=` + `+=` idempotent로 거짓양성. 잔존 8곳 new GUIStyle은 일시 표시(DrawCompletionNotification 3초 / DrawNewQuestNotification 2초 / DrawDetailPanel 수동 토글)이라 별도 라운드.

### WorldChannelManager 깊은 점검 (clean P0/P1, 2026-05-27)

Explore P1:3 모두 거짓양성/P2 강등. BestEffortLeaveWorld dispose 누락은 OnApplicationQuit 직후 OS 프로세스 정리. LeaveWorld race는 leaveInProgress 플래그 미적용이나 LeaveWorld 호출 빈도 매우 낮음(사용자 의도 액션 1회) + isUpdatingPresence 이전 처리로 PATCH 일반 race 차단됨 → P2 수용. 401 자동 갱신은 WorldChannel 영역 외(이전 AuthManager TryRefreshTokenForRetry 처리는 CloudSaveManager 한정 의도된 범위).

### **RegionTerrainBuilder c.y 이중 합산 버그

진짜 원인** (P0:21, 2026-05-27) — 사용자 명시 "고대유적/숲 layer 층이 위에 떠있어 캐릭터/곤충 가려짐" 보고. WorldTerrainBuilder.ApplyElevation(라인 22-39)이 region center.y를 forest:4 / ruins:8 / mountain:12 / pond:-3 / swamp:-2로 변경한 후, BuildXxxTerrain의 `c + new Vector3(x, c.y + offset, z)` 패턴이 c.y를 또 더해 결과 Y = **2 × c.y + offset**. ruins(c.y=8) 환경 모두 Y=16m로 솟아 캐릭터(Y=8.2 머리) 위 8m 떠있던 게 "층이 위에" 인상의 진짜 원인. 21곳 일괄 수정: `, c.y, ` → `, 0f,`, `, c.y + ` → `,`, `, c.y - ` → `, -`. Pond/Swamp/Mountain/Garden 동일 버그 함께 수정. Forest/Ruins 환경 축소 이전 라운드들(P1:4 + P0:11 누적)은 효과 거의 없었음 — 진짜 원인은 이 이중 합산 버그.

### Forest/Ruins 환경 추가 축소

"layer 층 전체 위에" (P1:2, 2026-05-27) — 사용자 추가 보고 "layer 층 전체가 위에 떠있어 캐릭터/곤충이 가려짐". 이전 절반 축소(treeH 1.2~2.5, wh 0.5~1.5, ph 1~2)로 부족 → 추가 강제 축소. (a) Forest treeH 0.8~1.5(낮은 관목), leafS 1~1.8 → 잎사귀 Y 1~2.5m로 캐릭터 머리(2.2) 이하/근처. (b) Ruins wh 0.3~0.8(잔해), ph 0.7~1.3(캐릭터 어깨 아래). 25 트리/8 기둥이 평면 layer로 모이지 않음.

### 메인 월드 Forest/Ruins region 환경 높이 (P1:2, 2026-05-27)

사용자 명시 "고대유적과 숲이 메인 월드에서 하늘에 있는 것처럼 보임" 보고. RegionTerrainBuilder.BuildForestTerrain/BuildRuinsTerrain (이전 SubArea 변경은 사용자 의도 영역 외였음, 영향 없으나 무관). (a) **Forest**: treeH 3~6→1.2~2.5(절반 축소), leafS 2~4→1.5~2.5. 트리 trunk Y 0~6m→0~2.5m, 잎사귀 Y 3.6~7.2m→1.8~3.5m로 캐릭터 머리(2.2) 부근. 잎사귀 ShadowsOnly 제거 — 옛 ShadowsOnly가 leaf mesh 안 보이고 그림자만 공중에 떠 보이는 "환경이 하늘에 떠있어" 인상의 진짜 원인이었음. 정상 렌더링 + leafS 축소로 카메라 시야 차단 최소화. (b) **Ruins**: wh 1~3→0.5~1.5, ph 2~4→1~2. 벽 Y 0~3m→0~1.5m, 기둥 standing Y 0~4m→0~2m로 캐릭터와 비슷한 평면감.

### SubArea 덮개 가시성 (P1:2, 2026-05-27)

사용자 명시 "고대유적/숲이 하늘처럼 덮여서 캐릭터 안 보임" 보고. (a) DeepForest 잎사귀 ShadowsOnly 적용 — 그늘 인상 유지하며 카메라(Y=10)와 캐릭터(Y=1.04) 사이 시야선 차단 해제 (Cave 천장 동일 패턴). (b) CameraFollower SubAreaOffset (0,16,-12)→(0,10,-14) 측면화 — 부감 53도→36도로 줄이고 z 거리 -12→-14로 늘려 캐릭터 옆에서 보이도록. 환경 위 평면에서 캐릭터 가려지던 회귀 차단. Temple 기둥은 양옆 배치라 측면 시야로 충분히 보임 (ShadowsOnly 미적용).

### 어깨 추가 + SubArea 부유/환경 높이 후속 (P1:4, 2026-05-27)

사용자 명시 후속 보고. (a) **어깨 추가 하향**: ArmL/R Y 1.55→1.40 (캡슐 범위 1.15~1.65m, Body 중간 위치). HandL/R Y 0.98→0.95. (b) **SubArea 부유 해결**: SubAreaOrigin Y 0→0.5로 한 줄 변경 → 8개 모든 환경(subAreaRoot 자식)이 world Y=0.5로 일괄 올라가서 캐릭터/곤충(Y=0.5 텔레포트→Y=1.0)과 같은 평면감. (c) **DeepForest 높이 축소**: 트리 기둥 Y 3→1.5 scale 3→1.5, 잎사귀 Y 6.5→3 scale (3.5,2.5,3.5)→(2.5,1.5,2.5). (d) **Temple 기둥 축소**: Y 3→1.8 scale 3→1.8. 카메라 SubAreaOffset (0,16,-12) 유지 — 환경이 낮아져 차폐 가능성 추가 감소. 다른 6개 환경(Cave/Underwater/FogSwamp/MountainPeak/Reeds/Greenhouse/GenericArea) 높이 축소는 사용자 추가 보고 시 별도 라운드.

### 캐릭터 시각 버그 3종 (P1:3+구조 변경, 2026-05-27)

사용자 명시 요청. (a) **어깨**: ArmL/R Y 1.62→1.55 + scale Y 0.55→0.50 + HandL/R Y 1.0→0.98. 캡슐 상단 1.895→1.80m로 Body 상단(1.79)과 일치, 머리(2.20) 영역 침투 차단. (b) **발 swing**: BootL/R을 Player 직접 자식 → LegLPivot/LegRPivot 빈 컨테이너 자식으로 구조 변경 (Capsule scale 비균등이라 Pivot scale=1 유지 필수). PlayerMovement cachedLegL/R → cachedLegPivotL/R, AnimateWalk 회전 대상 Pivot으로 교체 → Leg+Boot 둘 다 자동 전파. CharacterOutfitManager.ApplyPartColor는 FindDeep 재귀 검색이라 호환. (c) **SubArea 차폐**: SubAreaWorldBuilder에 GetSubAreaEnvLayer(Layer 31 fallback) + SetLayerRecursively 헬퍼 추가, EnterSubArea의 8개 환경 빌드 끝에 1회 호출(개별 빌드 메서드 미수정). CameraFollower.ResolveObstruction hit loop에 layer 제외 1줄. CameraFollower.SetSubAreaMode(bool) 추가 (NormalOffset (0,12,-8) ↔ SubAreaOffset (0,16,-12)). SubAreaWorldBuilder EnterSubArea/ExitSubArea에서 SetSubAreaMode(true/false) 호출 (ResetBaseline 내장).

### TutorialQuestUI 잔존 캐시 (P1:8, 2026-05-27)

DrawCompletionNotification 3 + DrawNewQuestNotification 1 + DrawDetailPanel 4 (header/close/row/status) GUIStyle 매 호출 new → InitNotifStyles + InitDetailStyles + 8 캐시 필드 + 9 static readonly Color. 알림 4 textColor alpha 동적, row/status textColor 분기 동적(완료/활성/잠금/대기). 17 quest×2(row+status)=34 GUIStyle/프레임 차단. OnGUI 본문 잔존 new GUIStyle=0.

### CapturePopupUI 캐시 (P1:12, 2026-05-27)

DrawSuccessPopup 8 + DrawFailPopup 2 + DrawMiniIVBar 2 GUIStyle 매 OnGUI(popupTimer>0 5초) new → InitPopupStyles + 12 캐시 필드 + 7 static readonly Color (SubGrayBase/GradeTitleGrayBase/RewardCandyBase/RewardExpBase/FailMsgBase/FailSubBase/IvLblBase). 12 textColor alpha+rarityCol+gc 동적 갱신. OnGUI 본문 잔존 new GUIStyle=0.

### RegionMapUI 캐시 + OnDisable 정리 (P1:28+1, 2026-05-27)

(a) DrawMap 5 + DrawErrorMessage 1 + DrawMiniMap 5 + DrawRegionList 5 + DrawRegionDetail 6 + DrawDexItem 6 + DrawMapTerrain symStyle 1 = 28 GUIStyle 매 OnGUI new → InitMapStyles + 28 캐시 필드 + 3 static readonly Color. DrawMap/DrawRegionDetail 진입에 InitMapStyles 호출. textColor 분기/alpha/themeColor 동적 갱신. (b) OnDisable에 isOpen=false + selectedRegionId=null + ModalUIRegistry.Unregister 추가 (CharacterOutfitUI 동일 P1 패턴, GO SetActive 토글 시 stale 모달 차단). OnGUI 본문 잔존 new GUIStyle=0.

### WorldLobbyUI MakeTex 누수 (P0:7, 2026-05-27)

DrawWorldSelectPanel/DrawWorldRow/DrawJoiningPanel/DrawInWorldPanel의 OnGUI 매 프레임 7곳 `MakeTex(1,1,...)` 새 Texture2D 생성 → UIHelper.GetCachedTex 교체. static readonly Color 10개 추출(BgOverlayCol/RowFull/Almost/OkCol/BarBgCol/FillFull/Almost/OkCol/LineSepCol/MeRowBgCol). 월드 1개당 4 Texture2D(rowTex/barBg/fillTex)/프레임 + 배경 1 = 월드 수×4+1 객체/프레임 누적 누수 차단. InitStyles의 4곳 MakeTex(라인 397/426/429/430)은 1회만 호출이라 유지.

### CharacterOutfitUI infoStyle 캐시 + OnDisable 정리 (P0:1+P1:1, 2026-05-27)

(a) P0: OnGUI 라인 234/239의 infoStyle/infoNameStyle 매 프레임 new GUIStyle 생성 → InitStyles에 infoStyleCache/infoNameStyleCache 캐시 추가 + DrawPanel에서 직접 참조. InfoLabelCol static readonly로 추출. (b) P1: OnDisable이 ModalUIRegistry.Unregister만 호출하고 isOpen=true 잔존 → 같은 GO SetActive 토글 시 isOpen=true이지만 Registry 미등록 → HandleEscape가 모달 무시. isOpen=false 추가.

### CapturePopupUI 점검 (clean P0, P1:1 보류, 2026-05-27)

알파 계산 라인 117은 의도된 fade-in(animTime/0.3, 0.3초)/fade-out(popupTimer/0.5, 마지막 0.5초) 분기로 P2 수용. OnGUI 매 프레임 new GUIStyle 14개+new Color 다수는 popupTimer>0(약 5초 표시 시간)에만 진입하여 일시적 → P1이나 다른 핫패스 처리 후 별도 라운드 권고. 이벤트 구독/null 가드/UIHelper 사용 모두 정합.

### CharacterPortraitRenderer 깊은 정합성 (clean, 2026-05-27)

Explore P0:2+P1:2 모두 거짓양성. hatId 처리 분기는 hat_none 시 customHat=true(라인 332)→Draw 내부 모자 투명 + 라인 501 hat_none 조건 거짓→형태 분기 안 들어감 → 모자 미표시 일관. cutlass(라인 435-441) DrawCharacterTool은 손잡이+칼날 2단계, DrawToolPreview의 차이는 별도 미리보기 컨텍스트(아이템 카드 vs 캐릭터)이라 의도된 시각적 차이. InvalidateCache 트리거는 LoginUI에서 직접 호출(라인 590)로 처리됨. 9종 도구 결합도는 구조적 P2.

### PlaySceneBootstrap 깊은 점검 (clean, 2026-05-27)

Explore P0:1+P1:2 모두 거짓양성. RegionChanged 람다 누수는 RegionManager가 DontDestroyOnLoad 아님(grep 결과 AudioManager/Auth/CloudSave 3개만) → 씬 재로드 시 옛 RegionManager+옛 람다 함께 GC. AuthManager.Instance 라인 401은 != null 가드 있음. playerMovement.AutoWire는 라인 330에서 실제 호출됨(Awake → Build() → Update 라이프사이클상 안전).

### SubAreaWorldBuilder 깊은 점검 (clean, 2026-05-27)

Explore P0:1+P1:6 모두 거짓양성. EnterSubArea race는 Unity Input 단일 프레임 폴링이라 같은 프레임 F2 불가능. ShowMainWorld 복원은 hiddenMainObjects 기반(HideMainWorld가 add한 것만)이라 이름 규칙 벗어난 오브젝트는 add도 안 되어 복원 필요 없음(정합). sticky 회피는 0.01s < 1.5s 쿨다운으로 유지. Destroy 비동기는 Unity 표준 동작. 코루틴 누수는 현재 횃불 애니메이션 없음(future bug). cachedPlayerTransform은 Player Transform 자체라 위치만 변하지 stale 참조 아님. InsectSpawner 부모 mismatch는 SubAreaWorldBuilder 영역 외(별도 라운드 후보).

### TutorialQuestManager 깊은 점검 (clean, 2026-05-27)

Explore P0:1+P1:3 모두 거짓양성. NotifyAction 비활성 무시는 prerequisite 체인 강제 순차 진행으로 비순차 시나리오 발생 불가. CloudSave race는 pendingSave 플래그로 단일 대기 처리. 옛 세이브 activeQuestId 불일치는 LoadProgress가 completedQuests 함께 로드 후 ApplySaveData 적용 + Start의 ActivateNextQuest 흐름이 일관. 마스터 계정 activeQuestId=null은 17 퀘스트 모두 완료 상태에서 의도된 동작(퀘스트 다 끝낸 유저는 UI 표시 안 하는 게 자연스러움). 의도 변경 필요 시 별도 디자인 작업.

### BattleScreenUI 깊은 로직 (clean, 2026-05-27)

Explore P0:2+P1:3 모두 거짓양성. cachedRegionMgr는 라인 313에 `regionMgr == null` 가드 있음. EndBattle 라인 3094-3099 finally 블록에 ExitBattleMode + SetFrozen(false) + Time.timeScale 복구 완비. SwapSelect↔PlayerTurn 키 처리는 phase별 격리(라인 379-384/405-412) + OnGUI Event 라인 726-748 phase 체크 후 set + Update phase 체크 후 consume + 즉시 reset(라인 411-412)으로 안전. InsectBattleUIController.ShowResult는 별개 Canvas UI 시스템, BattleScreenUI(OnGUI)와 무관. lastDamageToEnemy/Player phase 전환 미초기화는 다음 OnBattleUpdated에서 덮어쓰니 P2 수용. nameTagCache.fontSize 동적 할당은 GUIStyle 자체 캐시 + 프로퍼티만 갱신이라 P2.

### RaidBattleUI 깊은 로직 (clean, 2026-05-27)

Explore P0:3+P1:5 모두 거짓양성. EndRaid 라인 2380-2384 displayTeamHp/teamShake/selectedSlot/cameraFollower/playerMovement 모두 정리(이전 라운드 P0:2 처리분과 정합). AutoWire 라인 3005 `raidController != rc` 가드로 동일 rc 재호출 시 중복 구독 차단, null rc 시 라인 3005 false라 raidController=null 미설정 안전. Result→None은 EndRaid에서만 세팅, 라인 309 None 진입은 새 레이드 시작이라 정상. lastDmgToBoss 게이지 누적은 RaidController 턴 단위 관리로 race 없음. SelectInsect KO 차단은 TrySelectInsect 라인 585-586 CurrentHp > 0 체크 있음.

### BattleArenaController 이펙트 (clean, 2026-05-27)

Explore P0:3+P1:3 모두 거짓양성. Material 누수는 Unity가 GameObject Destroy 시 attached Renderer.material 인스턴스 자동 정리. StopAllCoroutines 후 arenaRoot Destroy하면 child GameObjects + 그 Material 인스턴스 일괄 정리. ExitBattleMode/SetFrozen(false)는 호출자(BattleScreenUI EndBattle:3097-3098, RaidBattleUI EndRaid:2383-2384)에서 책임 분리 패턴으로 처리. 다중 SetupNormalBattle 호출은 라인 57/92에서 CleanupArena가 진입 첫 줄이라 안전. 투사체는 라인 623 arenaRoot.SetParent로 자동 정리. 팀 배치 정적 계산은 게임이 runtime 팀 변경 미지원으로 비해당.

### PlayerMovement 모달 차단 + InputAction 흐름 (clean, 2026-05-27)

559줄, 8 책임 매트릭스 점검 결과 진짜 P0/P1 회귀 0건. 모달 차단: HandleEscape() + IsAnyOpen() 이중 방어(Line 137-143 ESC return + Line 169 마우스 클릭 차단). Frozen ESC 처리는 모달 우선(Line 121-131). 이중 ESC 회귀는 ModalUIRegistry.IsAnyOpen 자동 정리(Line 37-40)로 거짓양성. InputAction 흐름은 Unity 구식 Input.GetKey + OnGUI Event 백업(guiKey*/guiEsc/guiClick) 이중 경로로 F9/F11 OS 인터셉트·Editor·접근성 장치 우회. KeyUp에서 guiKey* 명시 false로 누적 없음(Line 318-330). OutfitChanged 동기는 InvalidateToolBase 즉시 호출로 stale base 1프레임 지연 차단(2026-05-20 P1:1 완료). CameraFollower 점프는 SetTarget baselineValid 리셋(2026-05-21 P1:2 완료). FindFirstObjectByType 호출 0건. cachedArm/Leg/Body/HeadPivot은 transform.Find 1회 후 캐싱(Line 42-46) 정상. outfitBonus null 가드(Line 240) 있음. **Covered 이동만**.

### InsectSpawner (P1:2, 2026-05-20)

OnGUI debugStyleCache lazy 캐싱(매 프레임 new GUIStyle 차단), GetPlayerTransform lazy 캐싱(매 호출 FindWithTag+Find 회피). Explore P0:2 거짓양성 (CleanupDeadEntities는 외부 SetActive(false) 직접 호출 없음 + DespawnFarInsects null entity는 이미 destroy 상태라 RemoveAt만 정상), P1 Sin/Cos는 8초당 8회 = 초당 1회 거짓양성.

### RegionManager (P1:3, 2026-05-20)

IsRegionAccessible 마스터 우회 (AuthManager.IsMasterAccount race 차단), DefeatGuardian 중복 가드 (HashSet idempotent 명시 + SaveUnlockState 중복 PlayerPrefs.Save 회피), LoadUnlockState unlockedRegions Split RemoveEmptyEntries (옛은 빈 항목 누적). P2 Time.time vs realtimeSinceStartup은 일반 게임 timeScale 영향 적어 수용.

### RegionTerrainBuilder + RegionMapUI (UI:2, 2026-05-21)

사용자 명시 요청. BuildBoundaries 신규 (60-segment fence + 자동 인접 검출 gateway gap + 노란 마커, 환경별 fence shape). RegionData.connections 선택 필드 추가. RegionMapUI: 사각 테두리 → 원형 ring(32 sample) + 현재 리전 펄스 alpha + gateway 노란 점.

### ItemEffectManager (clean, 2026-05-21)

Explore P0:1+P1:2+P2:1 모두 거짓양성/자체 모순. ActivateItem 옛 item 덮어쓰기는 의도된 동작, 게임 종료 시 메모리 해제, ActiveItemChanged 구독자가 OnDisable에서 해제.

### WorldStateProvider (clean, 2026-05-21)

Explore P0:2+P1:1+P2:1 모두 거짓양성. WorldState는 struct(stack 할당, GC 없음), pull 모델이라 이벤트 구독 디자인상 불필요, null 가드 fallback(DayPhase.Day/hour 12/Clear) 정상.

### WorldChannelManager (P1:1, 2026-05-21)

Update 코루틴 중복 실행 차단(isRefreshingWorld/isUpdatingPresence 플래그). 네트워크 지연으로 30초 timeout 초과 시 매 프레임 새 코루틴 시작 → CurrentWorld 동시 접근/PATCH race. Explore P0 AuthManager null 거짓양성(모든 public API에 IsFirebaseReady 가드), WorldLobbyUI OnDestroy 거짓양성(Unity OnDestroy 전 OnDisable 자동), BestEffortLeaveWorld 중복 거짓양성(RemoveAll idempotent).

### BattleTeamManager (P1:2, 2026-05-21)

SetSlot no-op 가드(같은 값 재설정 시 TeamChanged 발화 안 함), MigrateLegacySlots TeamChanged 발화 제거(시스템 내부 정리는 사용자 액션 아님). 옛은 TutorialQuestManager가 TeamChanged 구독해 q_team 진행도 자동/중복 가산 위험. Explore P0:2+P1:1+P2:1 모두 거짓양성(슬롯 부족 자동 채움, saveData fallback, 사본 반환, single execution).

### BattleTeamUI (P1:16+22, 2026-05-21)

16개 GUIStyle 캐시 필드 + InitTeamStyles + 21개 static readonly Color. DrawTeamPanel(3) + DrawSlot(7) + DrawInsectPicker(2) + DrawPickerItem(4) 모두 캐시 사용. railCol/pickerNameCache.textColor 동적 갱신. OnGUI 본문 잔존 new GUIStyle/Color = 0. 매 프레임 owned.Count × 5+ 객체 → 0.

### PlayerInsectCollection (P1:3, 2026-05-21)

(a) Awake에서 instanceId 중복 시 새 GUID 발급 + dirty mark — 옛은 dictionary가 1개만 보존되어 BattleTeam.GetByInstanceId가 나머지 인스턴스 못 찾는 회귀(옛 세이브 마이그레이션 시). (b) GainXp(data, insect, amount)에 insect null 가드 — 형제 오버로드(line 169)와 비대칭으로 line 185 NRE 위험. 호출자 0건 확인했으나 방어 코드 유지. (c) GetAllOwned null 항목 sanitize — 손상 세이브 시 호출자 6곳(BattleTeamUI/CollectionUI/DexScreenUI/TrainingUI/PlayerStatusHUD/PlayerInsectSelectionUIController) NRE 차단. 슬롯 인덱스 race는 Unity 단일 스레드 + BattleTeamManager.SetSlot 중복 가드(line 59-63)로 거짓양성.

### PlayerProgressController (P1:3, 2026-05-21)

(a) Awake sanitize: 손상 세이브 시 level 1~maxLevel 클램프 + currentXp ≥ 0 클램프 + dirty 시 즉시 Save. 옛은 외부 JSON 편집/손상 파일로 GetXpToNextLevel 음수 진입 위험. (b) 만렙 도달 시 currentXp = 0 클램프 — `Lv 50 / XP 99999` UI 불일치 차단. (c) 만렙 후 GainXp 호출 시 early return — 잉여 XP 누적 + 무의미한 디스크 쓰기/SaveCloud 차단. level/xp 동기 race는 단일 writer(GainXp) + private data로 거짓양성. 디스크 IO 디바운스는 호출 빈도 낮아(배틀/캡처/튜토리얼 시점만, burst 없음) P2 수용. CloudSaveManager.Instance null 가드는 SaveToCloud 내부 3중 가드(IsFirebaseConfigured + IsLoggedIn + IsSaving)로 거짓양성.

### PlayerProgressSaveService (P1:1, 2026-05-21)

Save(null) 가드 추가. static service라 임의 호출자가 진입 가능, 옛은 JsonUtility.ToJson(null)이 빈 문자열 → WriteAllText로 빈 파일 → 다음 Load 데이터 손실. atomic write 부재(P2)는 다른 SaveService(PlayerInsectCollection/BattleTeamManager/CashShopManager 등) 모두 동일 패턴이라 일관성 차원에서 별도 글로벌 라운드로 이관. 단일 writer(PlayerProgressController.GainXp)라 동시 쓰기 race 없음, FromJson 손상 회복은 catch + ?? new로 충분.

### PlayerCandyInventory (P1:2, 2026-05-21)

(a) AddCandy/SpendCandy 내부 data null 가드 추가 — Candies 프로퍼티(line 17)는 null 가드 있으나 두 변경 메서드는 비대칭, Awake 실패/순서 race 시 NRE. (b) Save(null) 가드 — PlayerProgressSaveService와 일관성. 호출자 6곳(InsectBattleController/CaptureController/RaidBattleController/CashShopManager/GachaBoxManager/UI)은 외부 `candyInventory?.` 가드 사용 중. 디스크 IO 디바운스는 호출 빈도 낮음(배틀/캡처/가챠 보상만)으로 P2 수용, atomic write는 글로벌 라운드로 이관.

### PlayerCurrencyWallet (P1:5, 2026-05-21)

(a-d) AddGems/SpendGems/AddCoins/SpendCoins 4개 변경 메서드 data null 가드 추가 — Gems/Coins 프로퍼티(line 18-19)와 비대칭 회귀. (e) Save() 내부 data null 가드 — 빈 파일 쓰기 + CurrencyChanged 구독자에게 null 전파 차단. CashShopManager gems 단일 소스 패턴(이전 audit)과 정합 유지. atomic write는 글로벌 라운드로 이관.

### PlayerItemInventory (P1:3, 2026-05-21)

(a) Awake sanitize: 손상 세이브 null record / 빈 itemId / 중복 itemId 처리. 중복 시 count 합치고 stale record 제거 — 옛은 lookup이 마지막 record만 보존해 AddItem 변경이 stale로 동기화 깨짐. (b) GetSnapshot 얕은 복사 반환 — DexScreenUI:659 등 호출자가 save.items 직접 변경 시 lookup race 차단. (c) Save(null) 가드 — 다른 SaveService와 일관성. UseItem 0 잔량 제거 로직(line 78-82)은 정상, GetCount 음수 클램프(line 96)도 정상.

### PlayerInsectLevelUpUIController (P1:3, 2026-05-21)

(a) Refresh에서 current null 가드 — selectedInstanceId가 stale(곤충 삭제/강화 후 변경)이면 GetByInstanceId null 반환 → line 75 current.insectId NRE. (b) OnEnable/AutoWire 이중 구독 차단 — subscribed 플래그 + Subscribe/Unsubscribe 헬퍼. 옛은 AutoWire 먼저 → 구독 → OnEnable → 또 구독 시나리오에서 HandleInsectUpdated 중복 발화, Refresh 2회 호출. (c) AutoWire가 isActiveAndEnabled 시에만 Subscribe — OnDisable 동안 구독 잔존 차단. 호출자 곤충 변경 시 자동 갱신 정상.

### ShopUIController (P1:1, 2026-05-21)

Start line 65 itemDatabase null 가드 추가 — EnsureDatabase의 Resources.Load("ItemDatabase") 실패 시 itemDatabase는 여전히 null → FindById NRE. UpdatePriceLabels(line 188-205) dead code (호출처 없음, allowCoinPayment [SerializeField]라 런타임 불변)이나 제거 risky로 P2 보류. 토글 무한 루프는 SetCoinPayment/SetGemPayment의 `if (!enabled) return;` first guard로 거짓양성. CashShopManager.Instance race는 단일 스레드로 거짓양성, wallet 동기화는 이전 CashShopManager audit에서 단일 소스 정리 완료.

### GachaBoxUI → CashShopUI 가챠 영역 (P0:11, 2026-05-21)

GachaBoxUI 클래스는 존재하지 않음(가챠 코드가 CashShopUI에 통합). DrawGachaTab/DrawBoxCard/DrawGachaResultScreen 매 OnGUI new GUIStyle 11개 + new Color 5개 회귀. InitGachaStyles + 12개 캐시 필드 + 5개 static readonly Color (BoxBronzeCol/BoxSilverCol/BoxGoldCol/GachaFlashBlue/GachaFlashGold/GachaPriceAffordCol/GachaRateGrayCol/GachaCandyPinkCol). boxPriceStyle/gachaTitleStyle은 textColor만 동적 갱신(BattleScreenUI 패턴). Phase 2의 new Color(alpha 동적)와 new Rect는 struct stack 할당으로 거짓양성. **잔여 작업**: CashShopUI 보석 충전(Tab 0)/아이템 상점(Tab 1) 영역도 동일 회귀 가능성, 별도 라운드 권고.

### ModalUIRegistry (clean, 2026-05-21)

진짜 회귀 없음. IModalUI 구현체 7개(BattleTeamUI/CaptureChoiceUI/CharacterOutfitUI/CashShopUI/CharacterViewerUI/CollectionUI/DexScreenUI) 모두 OnDisable에서 Unregister 호출 일관. HandleEscape는 PlayerMovement:127 단일 진입점. Register LRU 패턴 정상(Remove + Add), IsAnyOpen/TopModal null/IsOpen 자동 정리, Unregister null 가드 있음. static state scene 잔존은 IsOpen 체크로 회복(P2 수용), race는 Unity 단일 스레드로 불가, TopModal 역순 반복은 stack 변경 안전. Covered 이동만.

### CashShopUI 보석 충전/아이템 상점 영역 (P0:16, 2026-05-21)

Tab 0/Tab 1 + OnGUI 본문 new GUIStyle 16개 + new Color 다수 → InitMainStyles + 16 캐시 필드 + 14 static readonly Color (Backdrop/Panel/CharArea/ResTitle/CoinGold/BonusGreen×2/GemLabel/BuyButton/GemGlow/Border/GradTop/GradBot/Highlight + ItemBuyBlue). resStyle/itemPriceStyle textColor 동적 갱신 패턴(BattleScreenUI 동일). Phase 2 new Color(alpha 동적)와 new Rect(좌표 동적, struct stack)는 거짓양성으로 수용. OnGUI 본문 잔존 new GUIStyle = 0.

### UIHelper (P0:1, 2026-05-21)

GetCachedTex 캐시 가득(256개) 시 texCache.Clear()만 하고 styleCache 미invalidate 회귀. styleCache의 GUIStyle.normal.background이 Destroy된 텍스처를 참조 → 다음 렌더링에서 MissingReferenceException 또는 invisible 버튼. GetButtonStyle(line 189)이 정확히 그 패턴(`style.normal.background = GetCachedTex(bgColor)`). GetCachedTex 캐시 클리어 분기에 styleCache.Clear() 동반 호출 추가. new Color/Rect는 struct stack 거짓양성, Color32 정밀도 손실은 시각적 무해, 동시 호출 race는 Unity 단일 스레드로 거짓양성.

### AudioManager (P1:1, 2026-05-21)

StopBGM이 crossfadeCoroutine만 중단하고 deferredBgmCoroutine 미중단 회귀. PlayBGM(X) → deferred yield 대기 → 즉시 StopBGM 호출 시 fade out 시작 그 후 deferred가 다음 프레임에 CrossfadeBGM(X) 시작 → StopBGM 의도 깨짐 + 페이드 충돌. StopBGM에 deferredBgmCoroutine 중단 추가. FindFirstObjectByType(GameClock) 1초 폴링은 캐시 후 안 호출(P2 수용), ApplyVolumes fade 중 덮어쓰기는 사용자 설정 변경 빈도 낮음(P2 수용), gameClock stale은 Unity == null overload로 자동 재캐시(거짓양성), 다중 PlayOneShot은 AudioSource 특성상 동시 재생(거짓양성).

### ProceduralAudioGenerator (clean, 2026-05-21)

1957줄 모놀리스이나 구조 단순. public 진입점 3개(GetBGM/GetSFX/GetAmbient) + 단일 Dictionary 캐시 + 나머지는 PCM 생성 알고리즘(audit 범위 외, 캐시 hit 시 호출 안 됨). 모든 진입점이 TryGetValue → switch → cache 저장 패턴 일관, default LogWarning + null 반환, 호출자(AudioManager) null 처리 OK. Unknown key LogWarning spam은 정상 흐름에서 발생 안 함(P2), 캐시 무제한 증가는 총 ~64개로 안정 상태 도달 후 변동 없음(P2), Unity 단일 스레드로 동시 호출 race 거짓양성. Covered 이동만.

### InsectEntity 외부 로직 (P0:1, P1:3, 2026-05-21)

BuildModel은 visual-dev 영역으로 제외. (a) P0: Update의 Camera.main 매 프레임 호출 → static cachedMainCam lazy 캐싱. 최대 20마리×매 프레임 FindGameObjectWithTag 핫패스 회피. (b) P1: Despawn 다중 호출 가드(despawnedThisCycle 플래그) — Battle/Capture 동시 Despawn 시 풀 중복 반환 + onDespawn 두 번 발화 차단. (c) P1: Initialize/BuildForBattle에 cachedNameLabel/cachedShinySparkle/despawnedThisCycle 리셋 추가 — 풀 재사용 시 stale Transform 참조 회피. (d) P1: cachedMainCam과 일괄 처리로 NameLabel 미존재 인스턴스 매 프레임 Find 절감. new Vector3/Color는 struct stack 거짓양성, BuildModel/AnimateWings/AnimateShinySparkle 내부 알고리즘은 visual-dev 영역.

### CameraFollower (P1:2, 2026-05-21)

(a) SetTarget에 baselineValid=false 리셋 추가 — SubArea 좌표 전환 시 옛 baselinePos 기준 Lerp로 첫 프레임 카메라 점프 회귀. (b) EnterBattleMode의 battleTransition=0 무조건 리셋 제거 — fade-out 진행 중 재진입 시 transition 보존으로 시각적 끊김 차단(배틀→이탈→배틀 빠른 전환). new Vector3 다수는 struct stack 거짓양성, Shake 다중 호출 timer 리셋은 의도된 동작.

### InsectDatabase (P1:1, 2026-05-21)

GetWeightedRandom/GetWeightedRandomWithRareBoost 4곳 foreach에 `if (data == null) continue;` 가드 추가. GetCandidates는 null 필터링하나 외부에서 직접 candidates 전달(테스트/디버그 코드) 시 data.spawnWeight NRE 위험. insects 중복 insectId/null/빈 ID 검증은 ScriptableObject Editor 시점 보장(P2), Weight 두 번 계산은 결과에 영향 없음(P2), 부동소수점 누적 오차는 일반 범위에서 무해 + fallback 있음, new List 매 호출은 InsectSpawner 8초 burst 없음.

### OutfitSetData (clean, 2026-05-21)

진짜 P0/P1 회귀 없음. GetAllSets()는 OutfitBonusProvider.AutoWire에서 1회만 호출되어 allSets 필드에 캐시(매 프레임 호출 거짓양성). 12개 세트 정적 데이터 정합성 시각 review 결과 정상: requiredItemIds 중복/null 없음, partialThreshold ≤ requiredItemIds.Length 모두 만족(set_wizard threshold=2 items=4), partial/full Bonus 모두 정의, setColor 시각용 OK. GetAllSets static readonly 필드화는 변경 risky(P2 보류). Editor 시점 검증 도구는 별도 디자인 작업. Covered 이동만.

### AccountLinkUI (P1:3, P2:2, 2026-07-17)

**P1 — 처리 중 "닫기"가 죽어 해제 불가 모달.** `GUI.enabled = !isProcessing`이 "연동하기"와
"닫기" **양쪽**을 덮어, `if (!isProcessing) SetOpen(false)`가 실행될 수 없는 죽은 코드였다
(`GUI.enabled=false`면 `GUI.Button`은 항상 false). 여기에 `LinkEmailCoroutine`은 타임아웃이
없었다 — Unity 기본값 0은 무제한이라 모바일 네트워크가 물리면 OS TCP 타임아웃까지 수 분간
전체화면 딤 모달이 "처리 중..."에 갇히고 탈출구는 ESC뿐(모바일에선 사실상 없음).
수정: 닫기를 `GUI.enabled` 블록 밖으로 빼 항상 누르게 하고(처리 중 닫아도 응답은
`OnLinkCompleted`가 받아 토스트로 표시), `prevEnabled` 보존/복원으로 바꿔 호출부 상태를
덮지 않게 했다. **AuthManager의 UnityWebRequest 7곳 전부에 `timeout = 15` 추가** —
코드베이스에서 타임아웃을 걸던 곳은 `WorldChannelManager`(12초) 하나뿐이었고, 로그인·회원가입·
토큰갱신·연동이 모두 무한 대기였다.

**P1 — 배지가 모달 위에 그려지고 클릭을 가로챔.** `OnGUI`에 모달 가드가 없어 게스트인 동안
배지가 전체화면 모달 위에 매 프레임 그려졌다. 같은 프로젝트의 `MinimapUI:52`,
`QuickAccessBarUI:113`은 `if (ModalUIRegistry.IsAnyOpen()) return;`으로 자신을 숨기는데 여기만
빠져 있었다. 또 `GUI.depth` 미설정(기본 0)이라 `DexScreenUI`(-10)/`SaveConflictUI`(-50)에 가려
보이지 않으면서도 입력은 먹는 상태가 가능했다. 수정: 배지에 `IsAnyOpen()` 가드,
`DrawForm` 진입 시 `GUI.depth = -20`.

**P1 — `isProcessing` 리셋 경로 부재.** false로 되돌리는 곳이 `OnLinkCompleted` 하나뿐이라,
요청 중 GameObject가 토글되면 `OnDisable`이 구독을 끊어 응답을 놓치고 `isProcessing=true`가
남아 "연동하기"가 세션 내내 비활성이 된다. `OnEnable`에 초기화 추가(1줄).

**P2 — 닉네임 무검증으로 이메일이 공개 표시명이 됨.** `ValidateCredentials`는 email/password만
보고, 닉네임이 비면 `AuthManager`가 `DisplayName = email`로 채운다 → 월드/친구 목록에 이메일이
그대로 노출. `Submit()`에 공백 검증 추가.

**P2 — 버튼 눌림 상태가 반투명.** `bg * 1.15f` / `bg * 0.85f`가 알파까지 곱했다. RGB만 조절하고
알파는 보존하도록 수정.

**Explore 거짓양성(검증 후 제외)**: `MakeTex` 11개는 `stylesReady` 가드로 1회만 호출 —
`WorldLobbyUI MakeTex 누수(P0:7)` 라운드가 *"InitStyles의 4곳 MakeTex는 1회만 호출이라 유지"*로
이미 판정한 것과 같은 패턴. `msgStyle.normal.textColor` 매 프레임 갱신은 프로젝트 표준 해법.
이벤트 짝 정합, Bootstrap 등록(:156), 싱글턴 8건 전부 가드 존재. 연동은 uid를 보존하므로
`CloudSaveManager`의 `LocalOwnerKey`/`targetUid` 캡처와 충돌 없고, `LinkGuestWithEmail`의 실패
경로 3개 전부 `LinkCompleted`를 발화해 데이터 유실 벡터 없음.

### UIHelper 텍스처 캐시 오버플로 (P1:1, 2026-07-17)

`AccountLinkUI` 라운드 중 횡단 이슈로 발견. `DrawRarityBorder`는 `pulse`로 RGB를,
`DrawRarityGlow`는 `breathe`로 alpha를 **연속 변조**한 뒤 그 색을 `GetCachedTex`에 넣었다.
프레임마다 새 `Color32` 키가 생겨 `MaxCacheSize`(256)를 실제로 넘긴다. 넘치면 캐시된 텍스처를
전부 `Destroy`하는데, UIHelper가 무효화할 수 있는 건 자기 `styleCache`뿐이다 — 각 UI가 자기
필드로 들고 있는 `GUIStyle.normal.background`는 그대로 파괴된 텍스처를 가리키고, 그 UI들은
`stylesReady` 가드 때문에 재생성되지 않아 **영구히 배경이 깨진다**.

수정: 동적 색은 텍스처를 만들지 않고 빌트인 `Texture2D.whiteTexture`에 `GUI.color`로 입히는
`DrawTinted`로 전환(`DrawBorder`/`DrawProgressBar`/`DrawDimOverlay`/`DrawRarityGlow`).
`GetCachedTex`는 `GUIStyle.normal.background`용 정적 색 전용으로 한정하고, 오버플로 시
`Destroy`를 제거해 stale 참조 가능성 자체를 없앴다(1x1 RGBA32는 개당 4바이트라 정적 색만
담기는 구조에서 누적량은 무시할 수준).

**중요**: `GUI.color`는 대입이 아니라 **곱셈**이어야 한다. `AccountLinkUI:124`,
`AccountSettingsUI:105` 등이 딤 오버레이로 `GUI.color`를 미리 설정하고, 색 텍스처를 그리던
옛 동작도 그 값과 곱해졌다. 대입했다면 그 페이드가 전부 사라지는 회귀였다.

### WorldFieldMultiplayerUI (P0:1, P1:1, 2026-07-17)

**P0 — 채팅 대상이 uid로 고정되지 않아 입력 소프트락 + 사설 메시지 오배송.**
`chatOpen`은 bool인데 대상은 매 Update 재계산되는 `nearestPlayer`였다(Update 첫 줄이
무조건 `nearestPlayer = null`). 결과 두 가지: (a) 상대가 대화 범위를 벗어나거나 접속을
끊으면 composer는 사라지는데 `chatOpen`이 true로 남아 `IsOpen`→`ModalUIRegistry` 등록이
유지되고, `IsAnyOpen()`이 PlayerMovement/VirtualJoystickUI/WorldInteractionController/
CaptureInputController의 입력을 전부 차단 — **화면에 아무것도 없는데 입력만 잠긴다**
(모바일은 조이스틱까지 죽어 ESC/백버튼 외 복구 불가). (b) 작성 중 다른 탐험가가 더
가까워지면 `SendPrivateChat(nearestPlayer.uid, ...)`가 **엉뚱한 상대에게 전송**.
수정: `chatTargetUid` 필드로 "대화" 클릭 시점의 상대를 고정하고, 신설 `ResolveChatTarget()`이
매 프레임 `remoteAvatars[chatTargetUid]`로 해석(없음/범위밖/차단이면 null) → null이면
`CloseModal()`로 모달 잠금 해제. 모달 전환 지점(친구 패널 토글, 차단 확정, 전송 완료)을
전부 `CloseModal()` 경유로 통일해 `chatTargetUid`/`chatInput` 초기화 누락을 구조적으로 차단.

**P1 — OnGUI 매 프레임 문자열 보간.** OnGUI는 프레임당 Layout/Repaint/입력 이벤트로 여러 번
호출되는데 `DrawFieldStatus`(월드 타이틀), `DrawNearbyInteraction`(근처 탐험가 라벨),
`DrawMessages`(최대 5줄)가 매번 `$"..."`로 string을 힙 할당했다(struct인 Rect/Color와 달리
진짜 GC 압박). 내용은 서버 이벤트 시점에만 바뀌므로 `cachedWorldTitle`(HandleWorldState),
`messageLines`(HandleMessages), `cachedNearbyLabel`(Update에서 대상 uid 변경 시에만) 캐싱으로
전환. Draw 경로의 문자열 보간 0건.

**자체 발견 회귀 1건**: `messageLines` 도입 후 `HandleWorldLeft`가 `messages.Clear()`만 하고
`messageLines.Clear()`를 빠뜨려 두 리스트의 인덱스가 어긋날 수 있었다. 같은 커밋에서 수정하고
`cachedWorldTitle`/`cachedNearbyLabel`/`nearbyLabelUid` 초기화도 함께 추가.

**Explore 거짓양성(검증 후 제외)**: `InitStyles()`는 이미 `stylesReady` 가드 + 캐시 필드 +
`UIHelper.GetCachedTex` 표준 패턴이라 정상(자동 채점의 "프레임 할당 51"은 대부분 struct인
`new Rect`/`new Color`). 싱글턴 5곳 전부 null 가드 있음. 이벤트 `+=`/`-=` 짝 정상(Subscribe가
idempotent). AutoWire는 PlaySceneBootstrap:398에 존재. 200줄+ 메서드 없음. 세이브 표면 없음.

### PlayerItemInventoryGridUIController (P1:1, 2026-05-21)

HandleActiveChanged에서 UpdateRemainingTime() 즉시 호출 + remainingTextTimer=0 리셋 추가. 옛은 activeItemText만 갱신하고 남은 시간 표시는 Update의 1초 디바운스 대기 → 만료 시 "남은 시간: 00:00" 표시 최대 1초 지연. 주석(line 56)이 "만료 시점은 ActiveItemChanged 즉시 처리" 명시했으나 실제 호출 누락 회귀. OnEnable/AutoWire 이중 구독은 -=/+= 패턴으로 안전(거짓양성), BuildGrid Destroy+Instantiate 1프레임 중복은 정상 Unity 동작, EnsureDatabase 매 진입은 null 가드로 1회만 Load.
