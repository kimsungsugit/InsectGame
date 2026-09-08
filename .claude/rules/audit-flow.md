---
description: audit 자동 실행 트리거·금지 조건·거부 처리
---

# Audit 자동 플로우 규칙

`.claude/audit-progress.md`의 Uncovered 영역을 매 작업 흐름에 자연스럽게 포함시키는 규칙.

## 자동 트리거 조건

다음 모두 충족 시 사용자 작업이 끝나면 **자동으로 `/audit` 1회 실행**:

1. 사용자 요청 작업이 완료됨 (코드 수정 / 기능 구현 / 버그 수정 등)
2. `.claude/audit-progress.md`의 Uncovered ≥ 1개
3. 같은 응답 안에서 이미 `/audit`이 실행된 적 없음 (이중 실행 차단)
4. Auto Mode 활성 또는 사용자가 audit을 명시적으로 거부하지 않음

## 자동 실행 금지 조건

다음 중 하나라도 해당하면 자동 audit **금지**:

1. 사용자가 다음 단어 포함: "audit 안 해", "그만", "건너뛰", "skip audit"
2. 사용자가 plan 모드에 있음
3. 다음 사용자 메시지가 대기 중 (인터럽트된 작업)
4. 사용자가 명시적으로 다른 작업을 즉시 요청 ("다음", "계속" 등은 작업 신호이지 audit trigger 아님)
5. 직전 응답에서 audit 처리한 영역의 후속 검증 작업 중

## 실행 방식

1. 사용자 요청 작업 완료 후 라운드 결과 보고
2. 라운드 결과 보고 직후 **자동으로 audit skill 실행** (별도 응답 아님, 같은 응답 연장)
3. audit이 처리한 영역을 Covered로 이동, Round Log 갱신
4. Stop hook이 `audit_reminder.py`로 남은 Uncovered 카운트 알림

## 사용자 흐름 예

**Before** (audit 자동화 없음):
```
사용자: "기능 X 구현해"
Claude: 구현 완료, 라운드 결과 보고
사용자: "/audit" (수동 입력)
Claude: audit 처리
사용자: "/audit" (또 수동 입력)
```

**After** (audit 자동 플로우):
```
사용자: "기능 X 구현해"
Claude: 구현 완료, 라운드 결과 보고
       ↓ (자동 연장)
       audit-progress.md Uncovered ≥ 1 확인 → /audit 자동 실행
       RaidBattleUI 처리 완료, 진척 갱신
       Stop hook: "audit 미검토 N-1개 남음"
```

사용자 입력 부담: 0회. 매 작업 완료마다 audit 1영역씩 자동 진행 → 누적 회귀 자동 검출/처리.

## 거부 의사 표명 후 동작

사용자가 한 번 "audit 안 해" 라고 말하면:
- 같은 세션 동안 자동 audit 비활성
- 명시적 `/audit` 호출은 여전히 동작
- Stop hook reminder는 유지 (사용자가 다시 원할 때 알림)

세션 종료 후 재진입 시 다시 활성.

## 룰 위반 방지

이 규칙은 다음과 충돌하지 않음:
- `agent-coordination.md`: audit이 다른 에이전트 영역 침범 금지 — Explore가 거부 또는 main이 재위임
- `architecture.md`: audit이 모놀리스 깊은 변경 금지 — 표면 회귀만 자동 처리
- `unity-csharp.md`: 컨벤션 위반 자동 수정 금지 — 보고만

audit이 처리하는 범위는 P0/P1만. P2는 보고 후 사용자 결정 위임.
