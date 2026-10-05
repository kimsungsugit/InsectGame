# 모델·UI·전투 통합 개선 — 구현 및 검증 기록

2026-09-18. 구현은 저장소 작업 트리에 적용했다. Unity 라이선스 오류 때문에 실제 실행 화면과 Android 성능의 최종 합격 판정은 보류한다.

## 적용된 변경

| 영역 | 이전 경로/문제 | 적용한 변경 |
|---|---|---|
| 1대1 구도 | 아레나 구도를 CameraFollower가 다른 측면 구도로 덮어씀 | 아군 왼쪽·적 오른쪽 3/4 구도, 프레이밍을 카메라에 그대로 전달 |
| 화면 대응 | 고정 모델 간격·카메라 거리 | 모델 렌더 경계, 화면 비율, 시야각, 비대칭 안전 영역으로 모델을 하단 조작부 위에 배치; 교체·화면 변화 시 재계산 |
| 레이드 구도 | 고정 팀·보스 프레이밍 | 기존 정면 방향을 유지하면서 팀 전체·보스 경계를 안전 영역 안에 맞춤 |
| 카메라 전환 | 먼 전투 아레나로 필드를 가로질러 보간 가능 | 명시적 아레나 프레임은 즉시 적용; 확대된 프레이밍에 맞춰 경계벽 이동 |
| 타격 | 계산 직후 효과·쓰러짐, 개별 고정 타이머 | 기본 1.8초·스킬 2.5초, 40% 명중 시점에 HP·숫자·효과 반영 |
| 전투 종료 | 치명적 적 반격이 결과/교체에 가려질 수 있음 | 적 반격 → 쓰러짐 → 결과/교체; 종료 처리·보상 계산은 기존 경로 유지 |
| 상태 효과 | 독 피해와 적의 직접 피해가 같은 숫자에 포함 | 플레이어 행동 후·적 행동 후·턴 종료 HP 스냅샷 분리 |
| 교전 길이 | 중립 수식 대리 실험 중앙값 3라운드 | 일반 야생전 직접 피해 계수 0.7, 대리 실험 중앙값 4라운드; 저장 스탯·듀얼·수문장·레이드 계수 유지 |
| 접근성 | 전투별 시간·효과 설정이 분산 | 공통 1×/2× 표시 시간, 전투 중 배속 버튼, 흔들림·섬광 감소; 전역 timeScale 조작 없음 |
| 곤충 | 날개와 무늬 분리 운동, 일반 딱정벌레 초과 다리, 잠자리 다리 누락 | 날개 힌지와 장식 연결, 다리·관절·뿔·턱·더듬이 연결, 재질 구분 |
| 캐릭터 | NPC 목/모자 비례 불일치, 손과 도구의 별도 회전 | 공유 비례 기준, NPC 목·모자 보정, 손·도구의 공통 팔 회전, 기본 채집 도구 연결 |
| 탐험 UI | 11개 바로가기 상시 노출 | 도감·보유 곤충·지도·메뉴 4개, 전체 메뉴 12개 목적지, 기존 11개 단축키·퀘스트 배지 보존 |
| 모달 입력 | 클릭은 막지만 키보드·조이스틱·기존 자동이동이 계속됨 | 모달 중 모든 이동 입력과 남은 이동 목표 차단 |
| 화면 디자인 | 기존 버튼색과 공통 테마 혼재 | 공통 의미 색상 연결, 보유 곤충·훈련·상점/가챠·가방·퀘스트·대화·로그인 색상 통일, 도감·의상 미리보기 확대 |

## 변경 계약

- `BattleFraming.Compute`: 경계·시야각·비율·안전 영역으로 카메라 위치와 시선 계산. 기존 레이드 방향은 보존.
- `BattleArenaController.PlaySkillEffect`: 기존 오버로드 유지, 마지막 선택 매개변수 `duration` 추가. 기존 명중 콜백과 `IsPlayingSkill`을 완료 판정에 사용.
- `BattlePresentation`: 표시 전용 `Speed`, `DeltaTime`, `ReducedMotion`, `ReducedFlashes`. 설정은 로컬 PlayerPrefs에 저장하며 개체/클라우드 세이브 스키마는 변경하지 않음.
- `InsectBattleController`: 행동별 HP 스냅샷·행동 유무·표시 지연 소유권 추가. 실제 피해·턴 계산은 동기 실행을 유지.
- 빠른 메뉴는 목적지 enum으로 연결. 의상 슬롯·모델 노드 계약·곤충 ID는 유지.

## 검증 결과와 한계

### 2026-09-18 라이선스 복구 후 실제 검증

후속 실행에서 실제 IMGUI 전투 영상 3개와 모델 전후 비교를 추가했고 최종 PlayMode 1,054건이 통과했다. 세부 증거와 남은 범위는 `Docs/PolishFollowupPlan.md`의 실행 결과를 참조한다. 아래 1,047건/전투 미검증 기록은 후속 실행 이전 상태다.

- Unity 6000.3.10f1 라이선스 인증, 패키지 로딩, D3D11 초기화 및 실제 스크립트 컴파일 정상. `polish-upm-envfix.log` 종료 코드 0.
- Codex 자식 프로세스에 `ALLUSERSPROFILE`이 누락되어 UPM이 경로 오류를 냈다. 자식 환경에 `CommonApplicationData` 경로를 복원해 해결했으며 시스템 환경설정은 변경하지 않았다. 재현 도구: `Tools/Start-UnityValidation.ps1 -Mode Compile|Test|Models`.
- 초기 PlayMode 1,044/1,044 통과. 숲 공터·종별 동작 연결 및 듀얼 카메라 영역 조정 후 최종 PlayMode **1,047/1,047 통과**, 실패/건너뜀 0. 새 결과: `.claude/cache/design-validation/polish-integrated-playmode.xml`.
- 저장소 CI 325개 C# 검사 통과, 담당 커버리지 241/241, `git diff --check` 통과.
- `Artifacts/model-design-20260918`에 모델 전용 실제 렌더 27장 생성. 장수풍뎅이·사마귀 측면 및 플레이어 정면을 직접 검수했다. 곤충 연결부, 플레이어 모자·의상의 단순한 형상은 추가 개선 대상으로 남는다. 이 이미지는 전후 비교나 IMGUI 화면이 아니다.
- 전체 전투 화면/영상, Android 프레임 시간, 모든 모델·의상 조합의 시각적 합격은 아직 미검증이다. 아래 라이선스 실패 기록은 복구 전 이력이다.

### 복구 전 기록

- Unity 설치 경로의 실제 Roslyn 및 기존 Bee 참조로 런타임 코드(에디터 테스트 포함)와 에디터 도구 컴파일을 수행했다. 이는 Unity import·실행·플레이어 빌드 검증을 대체하지 않는다.
- 저장소 CI 검사와 에이전트 담당 커버리지를 실행했다. 최종 명령 출력은 `.claude/cache/design-validation`에 기록한다.
- `Docs/battle_pacing_measure.py`: 합성 중립 조건 60개, 중앙값 3→4라운드, 범위 1–4→2–5. 실종 데이터 전수/실플레이 결과가 아니다. 상세는 `BattlePacingValidation.md`.
- 추가 회귀 테스트: 카메라 모서리/안전 영역, 전투 순서·독·회복·기절·도주·재활성화, 배속과 수치 독립성, 메뉴 목적지·모달·입력, 날개 연결·도구 회전.
- Unity batch 실행은 **exit 198**, `No valid Unity Editor license found. Please activate your license.`로 종료했다. 이번 테스트 런의 XML은 생성되지 않았으며 기존 `TestResults.xml`을 이번 결과로 사용하지 않았다.
- 새 모델의 실제 렌더, 전체 의상 조합, 16:9·4:3·모바일 IMGUI, 전후 영상, Android 프레임 시간은 **미검증**이다. 이번 작업에서 생성된 비교 PNG/영상은 없다.
- 전체 종은 공용 형상/재질 개선을 적용했고 대표 종별 형태를 보강했다. 전체 종·모든 의상 조합의 시각적 완성도 인증은 아직 하지 않았다.

## 재현 명령

저장소 루트에서:

```powershell
python -X utf8 Tools/Verify-DesignCompile.py
python -X utf8 .claude/scripts/ci_check.py
python -X utf8 .claude/scripts/verify_coverage.py
python -X utf8 Docs/battle_pacing_measure.py
```

라이선스 활성화 후, 기존 에디터가 프로젝트를 점유하지 않는 상태에서 새 결과 파일명으로 실행한다:

```powershell
& 'D:/Unity/Unity Hub/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Project/곤충게임' -runTests -testPlatform PlayMode -testFilter InsectGame.Tests -testResults 'C:/Project/곤충게임/.claude/cache/design-validation/playmode-results.xml' -logFile 'C:/Project/곤충게임/.claude/cache/design-validation/playmode.log'
```

러너 종료 후 XML 생성 시각과 실행 건수가 0보다 큰지 확인한다. `-quit` 또는 EditMode 러너를 사용하지 않는다.

대표 곤충 6종 + 플레이어·성인 NPC·아동 NPC의 정면/측면/후면 **모델 전용** 27장 캡처:

```powershell
& 'D:/Unity/Unity Hub/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Project/곤충게임' -executeMethod InsectGame.EditorTools.ModelDesignCapture.Run -modelCaptureOut 'C:/Project/곤충게임/.claude/cache/design-validation/models' -logFile 'C:/Project/곤충게임/.claude/cache/design-validation/models.log'
```

이 캡처는 IMGUI를 포함하지 않는다. HUD·메뉴·전투의 화면 합격은 실제 Game view 또는 standalone 전체 화면 캡처로 별도 검증해야 한다. Android는 같은 기기·설정·장면에서 전후 프레임 시간·드로우콜·메모리를 측정하며, 10% 초과 악화 여부는 아직 판정할 수 없다.
