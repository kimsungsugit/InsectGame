---
name: add-skill
description: 새 스킬을 InsectSkill DB에 추가하고 6개 등록 지점 누락을 강제 검증합니다
argument-hint: "<skillId> <effectType: Damage|BuffAttack|DebuffAttack|새타입> <element>"
---

# 새 스킬 추가

InsectSkill SO를 생성하고 곤충 데이터에 연결한 뒤, 등록 누락을 grep으로 강제 점검합니다.
**원칙: grep 결과 0건이 하나라도 있으면 PASS 보고 금지.**

## Phase 1: 필요 정보

| 필드 | 설명 | 예시 |
|------|------|------|
| skillId | 고유 ID (영문 소문자_스네이크) | `ember_strike` |
| displayName | 한글 표시명 | "불꽃 일격" |
| description | 도감/UI 설명 | "..." |
| element | InsectElement enum 값 | Bug/Poison/Water/Leaf/Wind/Electric/Earth/Light/Dark/Metal |
| power | 데미지 위력 (Damage 타입 시) | 10~80 |
| cooldownTurns | 쿨다운 턴 | 0~5 |
| effectType | SkillEffectType enum | Damage / BuffAttack / DebuffAttack |
| effectValue | 버프/디버프 배율 | 0.2~0.6 |
| effectDurationTurns | 효과 지속 턴 | 1~5 |

밸런스 기준: 새 스킬 power가 기존 분포(15~60)에서 벗어나면 `/balance-sim battle` 실행 권장.

## Phase 2: 자동화 작업

### 2-1. ScriptableObject 생성
2가지 방식:
- **코드 헬퍼**: `PlaySceneBootstrap.CreateSkill(skillId, displayName, ...)` 패턴 사용 (런타임 생성)
- **에셋 SO**: Unity Editor → Create → InsectGame/Insect Skill → Resources/Skills/ 배치

`iconResourcePath`가 비어 있으면 `Resources/SkillIcons/{skillId}.png`를 자동 로드 (`InsectSkill.cs` 참조).

### 2-2. effectType 신규 여부 자동 판정
```bash
Grep -n "SkillEffectType\s*\{" Assets/Scripts/Data/InsectSkill.cs
```
열거형 멤버 목록에 인자 effectType이 없으면 → **신규 effectType**. Phase 4의 5곳 분기 검증 강제 발동.

### 2-3. skillId 중복 검사
```bash
Grep -c "\"<skillId>\"" Assets/Scripts
```
2건 이상이면 의심. CreateSkill 호출 외 등장 위치를 모두 사람이 검토.

## Phase 3: 6개 등록 지점 (비관적 체크리스트)

| # | 등록 지점 | 누락 시 증상 | grep 검증 |
|---|---|---|---|
| A | InsectSkill SO 생성 (`PlaySceneBootstrap.CreateSkill`) | 게임 내 스킬 미존재 | `Grep "CreateSkill\(\"<skillId>\"\|skillId = \"<skillId>\"" Assets/Scripts` |
| B | InsectData.skills 또는 learnset 배치 | 어떤 곤충도 학습 불가 → UI 빈 슬롯 | `Grep "<skillId>" Assets/Scripts/Data Assets/Scripts/Core` (B-2 외 1+ 매칭 필수) |
| C | skillId 중복 0건 | learnset HashSet에서 두 번째 무시 → 학습 안 됨 | `Grep -c "\"<skillId>\"" Assets/Scripts` ≤ 의도된 횟수 |
| D | **새 effectType이면 5곳 switch 분기** (Phase 4 참조) | 데미지 0, 효과 무반응, UI 회색 | Phase 4 grep 시퀀스 |
| E | UI 효과 이름 문자열 (`BattleScreenUI`/`RaidBattleUI`/`TrainingUI`) | "Unknown" 또는 빈 문자열 | `Grep "GetSkillColor\|effectType ==" Assets/Scripts/UI` |
| F | TrainingMethod.skillPool (훈련 습득 시) | 훈련으로 영구 미해금 | `Grep "<skillId>" Assets/Scripts/Data/TrainingMethod.cs Assets/Scripts/Core/TrainingManager.cs` |

## Phase 4: 새 effectType 시 5곳 switch 분기

신규 effectType 감지 시, 자동 코드 생성하지 **않고** 다음 5곳의 정확한 위치만 출력합니다.
실제 분기 추가는 **battle-dev 에이전트**에 위임 (잘못된 데미지 공식 위험 회피).

| # | 파일:라인 | 추가할 분기 | 누락 시 증상 |
|---|---|---|---|
| 1 | `Assets/Scripts/Battle/InsectBattleController.cs` ApplySkill() switch | 새 case | 1v1 default 분기로 빠져 의도와 다른 처리 |
| 2 | `Assets/Scripts/Battle/RaidBattleController.cs` UseSkill() if/else | 새 분기 | 레이드 보스 ATK DOWN 등으로 잘못 매칭 |
| 3 | `Assets/Scripts/UI/BattleScreenUI.cs, 3049-3055` 타입문자열 + GetSkillColor | 표시 문자열 + 색상 | 효과 이름 빈 문자열, 색상 회색 |
| 4 | `Assets/Scripts/Battle/BattleArenaController.cs` PlaySkillEffect() | 시각 이펙트 분기 | 이펙트 미재생 |
| 5 | `Assets/Scripts/UI/RaidBattleUI.cs, 2855-2861` + `Assets/Scripts/UI/TrainingUI.cs, 537-538, 569-575` | 표시 문자열/색상 | 레이드/훈련 UI 누락 |

자동 검증 grep (예: `effectType=Heal`):
```bash
Grep "SkillEffectType\.Heal\|case Heal" Assets/Scripts/Battle/InsectBattleController.cs
Grep "SkillEffectType\.Heal" Assets/Scripts/Battle/RaidBattleController.cs
Grep "SkillEffectType\.Heal" Assets/Scripts/UI/BattleScreenUI.cs
Grep "SkillEffectType\.Heal" Assets/Scripts/Battle/BattleArenaController.cs
Grep "SkillEffectType\.Heal" Assets/Scripts/UI/RaidBattleUI.cs Assets/Scripts/UI/TrainingUI.cs
```
5개 grep 중 0건이 하나라도 있으면 → **FAIL: "신규 effectType 분기 누락. battle-dev 에이전트 위임 필수."**

## Phase 5: 에이전트 위임 가이드

`.claude/rules/agent-coordination.md` 표에 명시된 수정 경계 준수.

| 영역 | 주담당 | 부수 |
|---|---|---|
| InsectSkill SO 데이터/직렬화 | data-architect | — |
| effectType 분기 로직 (5곳) | battle-dev | — |
| BattleScreenUI/RaidBattleUI 표시 문자열 | ui-dev | — |
| BattleArenaController 시각 이펙트 | visual-dev | — |
| power/cooldown 밸런스 검토 | game-designer | — |

여러 에이전트 협업 시: 메인 모델이 파일별 경계에 따라 분리 위임.

## Phase 6: 세이브 호환성 경고

⚠️ **기존 skillId 변경 금지**. PlayerInsectData.learnedSkillIds / equippedSkillIds가 JSON에 문자열로 저장되어 있어, 변경 시 모든 유저 곤충의 스킬이 사라집니다 (`ResolveSkill()` 실패 → null).

⚠️ **SkillEffectType enum 정수 값 변경 금지**. 기존 값에 새 값을 끼워넣지 말고 enum 끝에만 추가. JsonUtility 직렬화는 정수 기반.

마이그레이션 필요 시 → `/save-migration` 호출.

## Phase 7: 완료 후 /verify 강제 호출

스킬 1개 추가 작업 완료 후 반드시:
```
/verify
```
사용자 메모리 룰(`feedback_post_impl_verify.md`)에 따라 8항목 검증 루프 자동 실행. 특히 다음 항목이 신규 스킬에 직격:
- 항목 5 (세이브 호환성)
- 항목 8 (데이터 매칭 무결성: skillId↔learnset, effectType↔switch)

## 체크리스트 요약

- [ ] Phase 1 정보 수집 완료
- [ ] Phase 2 ID 중복 0건, effectType 분류 확정
- [ ] Phase 3 A~F 6항목 grep 모두 통과
- [ ] Phase 4 (신규 effectType 시) 5곳 분기 추가됨 — battle-dev 위임 결과 확인
- [ ] Phase 5 적절한 에이전트 위임
- [ ] Phase 6 기존 ID/enum 값 변경 없음
- [ ] Phase 7 `/verify` 호출 완료
