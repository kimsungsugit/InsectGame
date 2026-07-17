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

## Uncovered (우선순위순)

가장 위가 다음 `/audit`에서 자동 선택됩니다.
`- [ ]`가 0건이 되면 훅 2개가 함께 침묵해 자동 플로우가 멈추므로, 소진 시
`audit_candidates.py --emit-md`로 채웁니다.

- [ ] KeyGuideHUD (UI/KeyGuideHUD.cs, 234줄, score 138) — 프레임 할당 46
- [ ] AccountSettingsUI (UI/AccountSettingsUI.cs, 236줄, score 122) — 프레임 할당 37, 싱글턴 참조 11
- [ ] SaveConflictUI (UI/SaveConflictUI.cs, 188줄, score 117) — 프레임 할당 37, 싱글턴 참조 6
- [ ] SocialPvpUI (UI/SocialPvpUI.cs, 433줄, score 104) — 프레임 할당 34, 싱글턴 참조 2
- [ ] QuickAccessBarUI (UI/QuickAccessBarUI.cs, 345줄, score 98) — 프레임 할당 32, 싱글턴 참조 2
- [ ] SceneAutoWire (Core/SceneAutoWire.cs, 106줄, score 80) — 미캐싱 조회 40
- [ ] CaptureInputController (Capture/CaptureInputController.cs, 375줄, score 70) — 프레임 할당 23, 싱글턴 참조 1
- [ ] MinimapUI (UI/MinimapUI.cs, 126줄, score 60) — 프레임 할당 19, 미캐싱 조회 1, 싱글턴 참조 1
- [ ] NpcDialogueUI (UI/NpcDialogueUI.cs, 183줄, score 48) — 프레임 할당 16
- [ ] WorldInteractionController (UI/WorldInteractionController.cs, 322줄, score 39) — 프레임 할당 13
- [ ] VirtualJoystickUI (UI/VirtualJoystickUI.cs, 201줄, score 26) — 프레임 할당 8, 미캐싱 조회 1
- [ ] ItemInventoryGridItem (Core/ItemInventoryGridItem.cs, 295줄, score 21) — 프레임 할당 7

## Round Log

최근 5건. 이전 이력 전체는 `.claude/audit-archive/round-log-2026H1.md`.
- 2026-06-27: **2차 심층 리뷰 — 플로우/생명주기/성능/익스플로잇 통합레벨 (P0:1 + P1:12 전부 수정)** — 사용자 "문제점 심층 분석 및 개선" 재요청. 41에이전트(7 플로우 finder + 3렌즈 검증 + 완전성비평)로 파일경계 넘는 통합버그 발굴, 확정 P0:1+P1:12(거짓양성 0, 전부 3/3 real). 격리 도메인 4건은 전문에이전트 위임(battle/game-designer/capture/ui-dev), 영속성·모바일입력은 메인 처리. ① **[P0] 계정삭제 PII부활**: DeleteAccountCoroutine 2-step(문서DELETE→AuthDELETE) 사이 IsLoggedIn=true라 자동/Pause저장이 삭제된 Firestore 문서 재생성 → CloudSaveManager.SetDeletionInProgress 플래그(SaveToCloud 최상단 차단) + AuthManager.DeleteAccount 진입 set/실패시 복원. ② **로그아웃 무플러시**: Logout이 ClearAuth만 하고 클라우드 저장 안 함(마지막 120초 유실) → ClearAuth 전 SaveToCloud()(동기 코루틴 시작, DontDestroyOnLoad라 씬리로드 후 완료), 마스터 제외. ③ **충돌'클라우드선택' 혼합세이브**: ApplyResolved의 빈필드 보존 가드가 명시적 충돌해소에도 적용돼 빈 클라우드가 로컬 곤충 못 지움 → ApplyResolved(forceReplace) + ResolveConflict(useCloud:true)에서 true, 빈 컬렉션이면 ApplyCloudFile→DeleteLocalFile 치환. ④ **오프라인 동일ts 덮어쓰기**: localTs==cloud(오프라인진행, LastSaveTs 미갱신)에서 `localTs>cloud`만 봐 클라우드가 로컬 덮음 → `localTs>=cloud`로 동일ts 로컬우선. ⑤ **곤충 디바운스 누락**: 포획 0.5s 디바운스 중 Pause→CloudSave가 stale player_insects 읽음 → AddInsectInternal을 SaveNow()(즉시저장)로(빈번한 XP는 디바운스 유지). ⑥ **401→CharCreate 오진입**: 토큰갱신 실패 로그아웃 상태인데 LoadCompleted(false)가 CharacterCreate로 → LoginUI.OnCloudLoadCompleted에 IsLoggedIn 가드(phase=Login). ⑦ **부스터 배틀/레이드 미적용**(위임 battle-dev): ItemEffectManager AutoWire 주입 + 승리 캔디/경험치에 GetCandy/ExpMultiplier×아웃핏 적용(포획과 일치, 표기=지급). ⑧ **가챠 확률 표기 불일치**(위임 game-designer, 공시위반급): 골드 전설 실제45%인데 5%표기 → GachaBoxManager.GetRates/GetRateText(임계상수 단일출처) + CashShopUI 파생표기. ⑨ **SubArea 가드부재**(위임 capture-dev): [E]/F2/진입버튼에 모달/frozen 가드 없어 포획모달·배틀 중 강제진입 → IsSubAreaActionBlocked(ModalUIRegistry.IsAnyOpen||IsFrozen). ⑩ **QuickBar 전투중 가드부재**(위임 ui-dev): 핫키/OnClick에 배틀/frozen 가드 없어 전투중 모달오픈 → IsInputBlocked + 모바일바 숨김. ⑪ **멀티터치 잡기버튼**: 조이스틱이 finger0 점유시 IMGUI 합성마우스로 잡기버튼 안눌림 → 신규 FieldHudInput(가상↔화면 변환) raw터치 히트테스트, 터치기기는 GUI.Button 대신 Update raw처리. ⑫ **클릭이동 오발**: IMGUI 버튼 위 탭이 월드 클릭-이동 동시발화(캐릭터 화면밖 이동) → FieldHudInput.RegisterBlockingRect(잡기버튼) + PlayerMovement 클릭가드에 IsScreenPointOverHud 추가. **완전성비평 추가발굴**: 곤충 디바운스 flush(⑤로 처리). **P2 잔여**: 회원가입 후 ReloadAllLocalFromDisk 비대칭, ClearMasterDataIfNeeded UserId 세팅전 호출, QuickBar/SubArea 외 버튼 클릭이동(모달가드로 대부분 커버).
- 2026-06-27: **3차 심층 리뷰 — 세션 수정 회귀검증 + 미답영역 (P0:1 + P1:5 전부 수정)** — 사용자 "문제점 심층 분석 및 개선" 3차. 26에이전트(7클러스터: 회귀검증+데이터/밸런스/미니게임/비주얼/진행/UI레이아웃, 3렌즈 검증). 확정 P0:1+P1:5(거짓양성 0). 메인 직접 처리(세션한도로 위임 없이). ① **[P0] 레이드 가디언 격파 미등록 → 영구 진행차단**: CaptureChoiceUI가 Epic/Legendary는 [B]1v1 숨기고 [R]레이드만 제공하는데, DefeatGuardian 호출이 BattleScreenUI.CheckGuardianDefeat(1v1)에만 있어 연못/숲/습지/산 4개 수문장이 레이드로 처치해도 다음리전 미해금·무한리스폰 → RaidBattleUI.OnRaidEnded 승리분기에 CheckRaidGuardianDefeat 추가(BossStats 스냅샷으로 보스id/레벨, regionMgr.DefeatGuardian+NotifyGuardianDefeated). ② **[P1 회귀] 로그아웃 플러시 LocalOwner stale**: R2의 Logout 전 SaveToCloud가 SaveCoroutineInternal success에서 LocalOwnerKey를 라이브 UserId(=ClearAuth로 null)로 덮어써 계정전환 시 sameOwner 오판정→B 데이터 거부·유실 → targetUid를 첫 yield 전 캡처, success에서 IsLoggedIn&&UserId==targetUid일 때만 전역키 기록. ③ **[P1] 미니게임 입력 이중처리**: Update의 Input.GetKeyDown/GetMouseButtonDown 폴링 + OnGUI 이벤트가 같은 누름을 이중 큐잉, 프레임당 1소비로 누름1회당 ConfirmCapture 2회→2번째가 cursor=0 miss→즉시종료, 3단계 콤보·퍼펙트보너스 영구불가 → Update 폴링 제거, OnGUI 이벤트 단일 소스. ④ **[P1] 곤충 모델 머티리얼 누수(필드+도감)**: ApplyColorRaw가 파트마다 new Material(.material)인데 ClearChildren/Destroy(modelGo)가 인스턴스 머티리얼 미해제 → 풀재사용/리스폰·도감회전마다 수십개 누수(모바일 OOM) → InsectEntity.ClearChildren + InsectModelPreviewRenderer에서 자식 렌더러 .material Destroy(.material은 인스턴스만 반환→공유에셋 안전, root 제외). ⑤ **[P1] 가챠 풀등급↔DB rarity 불일치**: normalPool 티어배치가 DB rarity와 손동기화 어긋나(브론즈박스 전설누출/팝업≠수집 등급) → 풀 재배치(13종 이동: atlas_moth_giant·beetle_hercules→Legendary 등) + OpenBox에서 GetDbRarity로 결과등급 단일출처 보정(레벨/팝업/도감 일치). **P2 잔여(미처리)**: RollIV IV=16 off-by-one, q_visit_pond 임의리전 완료, garden 가디언레벨13<입장18, CashShopUI UIScale 미적용, TrainingUI scrollPos 공유, 대형모달 노치인셋. **완전성비평 에이전트는 세션한도로 미완**.
- 2026-06-27: **4차 심층 리뷰 — R3회귀 + P2백로그선별 + 미답영역(오디오/튜토리얼/안정성/Firestore/대형화면) (P1:5 + P2:1 수정)** — 사용자 "문제점 심층 분석 및 개선" 4차. 23에이전트(7클러스터, 3렌즈). 확정 P1:5(거짓양성 0) + critic P1:1(q_equip, medium 보류). CashShopUI는 ui-dev 위임, 나머지 직접. ① **[P1 R3회귀] 미니게임 이중확정 재현**: R3가 Update폴링은 제거했으나 OnGUI 내 전역 MouseDown(line262)+캡처 GUI.Button(line355) 두 confirm소스 잔존 → 모바일 버튼탭이 MouseDown(누름)+MouseUp(뗌) 이중확정(R3가 고치려던 바로 그 증상 재현) → 캡처버튼 시각전용화(wantConfirm 제거)+전역 MouseDown 단일소스+취소버튼 rect 제외(가상좌표 변환). ② **[P1 R3회귀] 가챠 water_strider_pond**: R3 재배치 때 1종(DB=Common)이 Uncommon풀 잔존 → GetDbRarity가 Common 보정하나 GetRates표기와 전달분포 괴리 → Common풀로 이동(13+exclusive10 전수 일치 확인). ③ **[P1] 볼륨 슬라이더 무반응**: SettingsPanel.ApplyVolume은 AudioMixer만 세팅하나 AudioManager는 믹서 미경유(masterVolume/sfxVolume 필드 직접믹싱)+DontDestroyOnLoad라 세션중 재초기화 안됨 → SetMasterVolume/SetSFXVolume이 데드메서드였음 → OnMaster/SfxVolumeChanged+LoadSettings에서 AudioManager.Instance.SetMaster/SetSFXVolume 호출 동기화. ④ **[P1] 튜토리얼 가디언 선격파 영구정지**: q_guardian1 활성 전 meadow 수문장 격파하면 NotifyGuardianDefeated가 ActiveQuest.type 불일치로 no-op+RegionManager 격파영구기록 → 재격파 불가로 q_guardian1+후속6퀘스트 연쇄정지 → ActivateNextQuest/ReloadFromDisk/BeginTutorial에 ReconcileActiveGuardianQuest(AnyGuardianDefeated면 자동완료) 추가. ⑤ **[P1] CashShopUI UIScale 미적용**(위임 ui-dev): 결제화면만 원시픽셀→저해상도 글자과대/오버플로 → UIScale.Begin/End(2 return경로 균형)+Screen.width/height 8곳→VirtualScreen, 세션변경(가격/확률/스타일캐싱) 보존. ⑥ **[P2] RollIV off-by-one**: Random.value=1.0시 IV=16 → Mathf.Min(MaxIV) 클램프. **P2 잔여**: q_visit_pond 임의리전(게이팅 마스킹), garden 게이트역전, TrainingUI scrollPos, 노치인셋, 다중머티리얼 2번째+(프로시저럴은 단일머티리얼이라 실무영향無), q_equip 자동장착(critic medium). **누적 4라운드: P0:2 + P1:25 수정**.
- 2026-06-27: **5차 심층 리뷰 — R4회귀+IAP/멀티/빌드/리전/상점/P2 (P1:1 + P2:1 수정, 세션한도로 일부 검증 중단)** — 사용자 "문제점 심층 분석 및 개선" 5차. 17에이전트(세션한도 8:20pm 리셋으로 find:region/CashShopManager·TutorialQuest verify·critic 중단). 확정 P1:1(거짓양성 0) + 다수 P2. 추가 워크플로 없이 직접 처리. ① **[P1] WorldChannelManager 무한 소프트락**: SendRequest의 UnityWebRequest에 timeout 미설정(코드베이스 전체 0건)+IsBusy/syncInFlight 리셋이 코루틴 본문끝(try/finally 아님) → half-open 연결/이벤트핸들러 예외 시 플래그 영구 true로 월드 나가기/새로고침/동기/채팅 전부 소프트락(앱재시작 외 복구불가) → SendRequest request.timeout=12 + 6개 라우틴(Refresh/Join/Leave/Sync/Mutation/RespondInvite) try/finally로 플래그 복구 + ClearWorldState invites.Clear. ② **[P2] garden 게이트 역전**: guardianLevel(13)<requiredLevel(18) → 이웃패턴(forest28/swamp37) 맞춰 33으로 상향. **P2 잔여(직접검토 후 보류)**: q_visit_pond 임의리전(게이팅 마스킹), TrainingUI scrollPos 공유, 노치인셋, IAPManager 검증 timeout부재(P2), BestEffortLeaveWorld Dispose누락(앱종료시 OS정리), 오프라인 listWorlds 폴링, PlayerItemInventoryGrid 검증전 소비, ItemEffect 부스터 비영속. **누적 5라운드: P0:2 + P1:26 + P2:2 수정**. 5라운드째 신규 발견 급감(주로 회귀·P2) — 수렴 단계 도달.
- 2026-06-27: **[실기기 버그] 산 서브지역 이탈 후 갇힘 (P0:1 + P1:2)** — 사용자 실기기 보고 "산에서 서브지역 들어갔다 나오면 겹쳐서 못 움직이는 구간들이 있다, 들어갔다 나갔다 반복할 때". 4가설 워크플로(28에이전트, 3렌즈) 확정 8건이 3원인으로 수렴. ① **[P0] ExitSubArea 충돌검사/지면스냅 부재**: 진입측 EnterSubArea는 FindSafeSpawnPosition+IsSpawnPositionClear로 충돌검사하나 ExitSubArea는 비대칭 — dest=center+dir*(radius+2)(~14m 밀어내기)+dest.y=savedPlayerPos.y(진입 고도 고정), 검사·스냅 전무. 산 region의 Scenery_MountainRock(8~30m 배치, 폭~7.2m, 콜라이더 활성) 안에 박혀 PlayerMovement.IsBlockedPosition이 전 nextPos 차단→move.x/z=0 영구갇힘. 진입방향마다 14m 원주 다른 지점→'구간들'. → FindClearGroundPositionNear(SnapToGroundY 지면 raycast + IsSpawnPositionClear 8방향×3반경 spiral) 추가, ExitSubArea가 호출(진입측과 대칭). ② **[P1] 25m 자동이탈 RequestExit 우회**: Update 25m이탈(145)이 ExitSubArea() 직접호출→RegionManager.currentSubArea 미정리(자기 currentSubArea만 null)→걸어서 나가면 RequestEnterSubArea의 currentSubArea!=null 조기return으로 [E] 재진입 불가+메인월드 region가드 비활성. Y추락안전망(137)은 RequestExit 쓰는데 25m만 직접호출 불일치 = '반복 시' 원인 → RequestExit()로 통일(ForceExitSubArea가 currentSubArea 정리+SubAreaChanged(null)+오디오 단일경로). ③ **[P1] 끼임탈출 F9 키 전용**: UnstickToSafePosition 트리거가 F9 키+OnGUI F9 백업뿐, 모바일 키보드 없어 복구수단 0 → PlayerMovement에 stuckTimer 추가, hasMovement중 IsBlockedPosition(transform.position)(현재위치 embedded, 본인콜라이더 제외)이 1.5초 연속이면 자동 UnstickToSafePosition(벽 걷기는 현재위치 clear라 미트리거). **누적: P0:3 + P1:28 + P2:2**.
- 2026-07-17: WorldFieldMultiplayerUI — P0:1, P1:1 처리. **P0**: 채팅 대상이 uid로 고정되지 않고 매 Update 재계산되는 `nearestPlayer`를 따라가, (a) 상대가 범위를 벗어나면 composer만 사라지고 `chatOpen`/모달 등록이 남아 화면이 빈 채로 입력이 전부 잠기고(모바일은 조이스틱까지), (b) 작성 중 다른 탐험가가 가까워지면 사설 메시지가 오배송됐다. `chatTargetUid` 고정 + `ResolveChatTarget()`(없음/범위밖/차단 시 null → `CloseModal()`)로 수정하고, 모달 전환 지점을 전부 `CloseModal()` 경유로 통일. **P1**: OnGUI 매 프레임 `$"..."` 문자열 힙 할당(월드 타이틀/근처 라벨/메시지 5줄) → 이벤트 구동 캐싱(`cachedWorldTitle`/`messageLines`/`cachedNearbyLabel`). **자체 발견 회귀 1건**: `HandleWorldLeft`가 `messages.Clear()`만 하고 `messageLines.Clear()` 누락 → 같은 커밋에서 수정. **Explore 거짓양성 6건 제외**: InitStyles는 이미 `stylesReady`+캐시+`GetCachedTex` 표준 패턴, 싱글턴 5곳 전부 가드, 이벤트 짝 정상(Subscribe idempotent), AutoWire는 Bootstrap:398에 존재 — 자동 채점의 "프레임 할당 51"은 대부분 struct인 `new Rect`/`new Color`였다(진짜 문제는 string 보간). 상세: `audit-archive/covered-detail.md`. **P2 5건 미처리**(보고만): Shader.Find 스트리핑 시 null, `color * k`가 알파까지 곱함, UIHelper 캐시 오버플로 시 destroyed 텍스처 참조(횡단), DrawInvitePopup 모달 미등록, 메시지 8개 보관 대비 5개만 렌더.
- 2026-07-17: **P2 처리 라운드** (사용자 명시 요청) — WorldFieldMultiplayerUI P2:2 + **UIHelper 횡단 P1:1**. (a) UIHelper 텍스처 캐시 오버플로: `DrawRarityBorder`가 pulse로 RGB를, `DrawRarityGlow`가 breathe로 alpha를 연속 변조한 색을 `GetCachedTex`에 넣어 프레임마다 새 Color32 키가 쌓였고, 256 초과 시 텍스처를 전부 Destroy하면서 각 UI가 필드로 들고 있는 `GUIStyle.normal.background`는 무효화하지 못해(`stylesReady` 가드로 재생성 안 됨) **해당 UI 배경이 영구 손상**될 수 있었다. 동적 색을 빌트인 whiteTexture + `GUI.color` 곱셈(`DrawTinted`)으로 전환해 캐시에서 제거하고, 오버플로 시 Destroy도 없앴다. **GUI.color는 대입이 아니라 곱셈** — AccountLinkUI:124/AccountSettingsUI:105가 딤 오버레이로 이미 설정하므로 대입하면 페이드가 사라진다(검증으로 확인). (b) `Shader.Find` 둘 다 null이면 `new Material(null)` 예외 → 프리미티브 기본 머티리얼 폴백(`ApplyAvatarMaterial`). (c) `color * k`가 알파까지 곱해 눌림 버튼이 반투명 → RGB만 조절. **미처리 P2 2건**: DrawInvitePopup 모달 미등록(초대 팝업이 플레이 중 입력을 막아야 하는지는 게임 디자인 판단이라 보류), 메시지 8개 보관 대비 5개 렌더(h 계산이 min(190)으로 클램프돼 실질 영향 없음).
- 2026-07-17: AccountLinkUI — P1:3, P2:2 처리. **P1**: (a) 처리 중 "닫기"가 `GUI.enabled=!isProcessing` 블록에 함께 덮여 죽은 코드였고(`GUI.enabled=false`면 Button은 항상 false) `LinkEmailCoroutine`에 타임아웃도 없어, 네트워크가 물리면 전체화면 딤 모달에 갇혔다(탈출구 ESC뿐 — 모바일 복구 불가). 닫기를 블록 밖으로 빼고 **AuthManager의 UnityWebRequest 7곳 전부에 timeout=15 추가**(코드베이스에서 타임아웃을 걸던 곳은 WorldChannelManager 12초 하나뿐이었고 로그인·회원가입·토큰갱신·연동이 전부 무한 대기였다). (b) 배지에 `IsAnyOpen()` 가드가 없어 전체화면 모달 위에 그려지고 클릭을 가로챘다(MinimapUI:52/QuickAccessBarUI:113 관례 위반) + `GUI.depth` 미설정 → 가드 추가 + DrawForm에 depth -20. (c) `isProcessing` 리셋 경로가 `OnLinkCompleted` 하나뿐이라 요청 중 GO 토글 시 영구 비활성 → OnEnable 초기화. **P2**: 닉네임 무검증으로 빈 값이면 `DisplayName = email`이 되어 **이메일이 공개 표시명으로 노출** → Submit 검증 추가. 버튼 눌림 알파 곱셈 → RGB만. **Explore 거짓양성 제외**: MakeTex 11개는 stylesReady로 1회만(WorldLobbyUI 라운드가 이미 동일 판정), 이벤트 짝/Bootstrap 등록/싱글턴 8건 가드 정상, 연동은 uid 보존이라 세이브 유실 벡터 없음. 검증: Unity PlayMode 38/38, error CS 0건.
- 2026-07-17: SubAreaEnvironment — P1:1 처리. **cameraBg 파이프라인 전체가 무동작이었다**: 11개 프리셋이 동굴(0.1,0.08,0.06)·수중(0.1,0.2,0.34) 같은 어두운 배경을 정의하고 ApplyLerp가 매 전환 프레임 `mainCamera.backgroundColor`에 보간해 넣지만, Unity는 clearFlags가 Skybox면 backgroundColor를 무시한다. PlaySceneBootstrap:565가 카메라를 Skybox로 고정하고 되돌리는 코드가 코드베이스에 없어 서브지역 배경색이 한 번도 렌더된 적이 없었다. 동굴 말고는 천장이 없어 하늘이 노출되고, 내장 fog는 skybox에 적용되지 않아 정밀 튜닝한 fog로도 가릴 수 없었다(파일 주석의 "너무 어두워 안 보임" 개선 의도와 정면 배치). 수정: defaultClearFlags를 CaptureDefaults에서 캡처하고 OnSubAreaChanged에서 진입 시 SolidColor / 복귀 시 원래 플래그로 전환 — ambientMode를 Skybox↔Flat로 전환하는 기존 패턴과 동일한 이유·동일한 자리. PlaySceneBootstrap:566이 backgroundColor를 하늘색(0.5,0.8,1)으로 설정해두므로 진입 첫 프레임 깜빡임 없이 0.5초 페이드된다(확인). **자동 채점 46건은 전부 거짓양성** — EnvironmentProfile이 struct라 프리셋의 new Color/Quaternion이 스택 할당이고 Update는 전환 0.5초만 도는 데다 힙 할당 0건, 미캐싱 조회 1건도 initialized 래치로 Start 1회. 진입/이탈 정합·좌표/갇힘 로직도 clean(후자는 SubAreaWorldBuilder 소유). **미처리 P2 4건**(전부 현재 도달 불가로 격하, 잠재 위험 기록): fogMode 미복원, OnEnable 재구독 부재, FindFirstObjectByType<Light>에 Directional 필터 없음(PlaySceneBootstrap:592는 같은 호출에 가드 있음), defaultCameraBg 초기자(이번에 함께 추가). 검증: Unity PlayMode 38/38, error CS 0건.
