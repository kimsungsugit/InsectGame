# 에이전트 간 조율 규칙

## 공유 파일 수정 경계

여러 에이전트가 담당하는 파일은 아래 표의 **수정 경계**를 따릅니다.
에이전트에 작업을 위임할 때, 해당 파일의 수정 범위를 프롬프트에 명시하세요.

| 파일 | 에이전트 | 수정 경계 |
|------|----------|-----------|
| `BattleScreenUI.cs` | ui-dev | OnGUI 레이아웃, Rect 좌표, 색상, 화면 전환 |
| | battle-dev | Phase 로직, 데미지 표시 계산, 턴 진행 |
| | visual-dev | 쉐이크 효과, HP바 보간, 속성 이펙트 렌더링 |
| `RaidBattleUI.cs` | ui-dev | OnGUI 레이아웃, 팀 선택 패널, 결과 화면 |
| | battle-dev | 레이드 Phase 로직, 유나이트 게이지, 보스 턴 |
| | visual-dev | AOE 연출, 유나이트 이펙트, HP바 |
| `BattleTeamUI.cs` | ui-dev | 슬롯 레이아웃, 드래그 상호작용 |
| | battle-dev | 팀 유효성 검증, 전투력 표시 로직 |
| `CaptureChoiceUI.cs` | ui-dev | 선택지 레이아웃, 키 안내 |
| | capture-dev | 포획/배틀/레이드 분기 조건 로직 |
| `InsectEntity.cs` | capture-dev | 스폰/디스폰, 풀 관리, 월드 배치 |
| | visual-dev | BuildModel() 프로시저럴 모델, 애니메이션, 샤이니 |
| `BattleArenaController.cs` | battle-dev | 아레나 상태, 전투 환경 설정 |
| | visual-dev | 지형/조명/파티클 시각 연출 |
| `InsectSkill.cs` | battle-dev | 스킬 효과 로직, 쿨다운, 데미지 타입 |
| | data-architect | 스킬 데이터 모델, 직렬화, SO 구조 |
| `InsectLearnableSkill.cs` | battle-dev | 습득 조건 로직 |
| | data-architect | 데이터 모델, 레벨 매핑 |
| `InsectElement.cs` | battle-dev | 속성 상성 로직 |
| | data-architect | enum 정의, 확장 |
| `InsectSpawnCondition.cs` | capture-dev | 스폰 필터링 로직 |
| | data-architect | 조건 데이터 구조 |
| `CaptureItemData.cs` | capture-dev | 아이템 효과 적용 로직 |
| | data-architect | 아이템 데이터 모델 |
| `ItemRarityPalette.cs` | data-architect | 팔레트 데이터 구조 |
| | visual-dev | 색상값, 그라디언트 |
| `RarityIconProvider.cs` | data-architect | 아이콘 매핑 데이터 |
| | visual-dev | 아이콘 렌더링, 크기/위치 |

## 충돌 방지 절차

1. **단일 에이전트 원칙**: 하나의 공유 파일은 한 번에 하나의 에이전트만 수정
2. **경계 외 수정 필요 시**: 메인 모델이 해당 파일의 주담당 에이전트에게 위임
3. **교차 수정 감지**: 에이전트가 자신의 경계 밖 코드를 수정해야 할 때, 변경하지 말고 메인 모델에 보고하여 적절한 에이전트에 재위임
