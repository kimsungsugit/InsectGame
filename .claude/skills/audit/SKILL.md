---
name: audit
description: 미검토 영역 1개 자동 탐색·개선. "문제점 분석 및 개선해" 수동 요청 부담 제거.
---

# Audit — 자동 영역 탐색·개선

`.claude/audit-progress.md`의 Uncovered 목록에서 다음 1개 영역을 자동으로 골라 P0/P1 후보를 탐색하고 자동 수정합니다.

## 실행 순서

### Step 1. 진척 파일 읽기

```
Read .claude/audit-progress.md
```

- `## Covered` 섹션 → 누적 처리 영역 목록 추출 (Explore 프롬프트 거짓양성 가드용).
  이름만 있는 인덱스이며, 각 라운드의 서술 원문은 `.claude/audit-archive/covered-detail.md`에 있다.
  **Explore에는 이름만 주입한다** — 서술까지 넣으면 매 라운드 40KB+가 딸려간다.
- `## Uncovered` 섹션 → **첫 번째 `- [ ]` 항목**을 대상으로 선택 (우선순위 정렬은 파일 작성 시점에 이미 적용됨)

**`- [ ]`가 0건이면 종료하지 말고 큐를 재생성한다:**

```
python -X utf8 .claude/scripts/audit_candidates.py --emit-md
```

출력 상위 15건을 `## Uncovered`에 Edit으로 기록한 뒤 1순위로 진행한다.
이 스크립트는 Covered/아카이브에 이름이 없는 `.cs`를 프레임 할당·미캐싱 조회·
구독 해제 누락·싱글턴 참조로 채점해 후보를 낸다(하드코딩 목록이 없어 stale해지지 않음).

스크립트가 후보 0건을 반환할 때만 "미검토 영역 없음 — audit 완료"를 보고하고 종료한다.

> 큐를 비운 채 두면 `audit_flow_inject`와 `audit_reminder` 훅이 함께 침묵해
> 자동 플로우 전체가 멈춘다. 2026-05-27 ~ 2026-07-17 사이에 실제로 그렇게 멈춰 있었다.

### Step 2. Explore 위임

단일 Explore agent (subagent_type: Explore) 호출. 프롬프트에 반드시 포함:

```
점검 대상: <Uncovered 1순위 영역>
파일: Assets/Scripts/<해당 경로>

이미 처리된 영역 (재보고 금지):
<Covered 목록 전체 자동 주입>

점검 항목:
1. OnGUI/Update 안 new GUIStyle/new Color/new Rect (매 프레임 할당)
2. OnGUI/Update 안 FindFirstObjectByType/GetComponent (캐싱 누락)
3. 이벤트 += 후 -= 짝 누락 (OnEnable/OnDisable)
4. Singleton.Instance 즉시 사용 (null 가드 누락)
5. 모놀리스이면 method 그룹 분해 후보 식별
6. 데이터 모델 변경에 따른 세이브 호환성
7. AutoWire 누락 / Bootstrap 등록 누락

거짓양성 보고 금지 — 실제 회귀/누락만. 보고 형식: 파일:라인 + 1줄 문제 + 1줄 수정 권고. 최대 P0/P1 3건.
700단어 이내.
```

### Step 3. 자동 처리 (P0/P1만)

Explore 보고에서:
- **P0/P1**: Edit/Write로 즉시 수정. auto_verify_trigger.py가 자동 발화 → 8항목 점검
- **P2**: 보고만 하고 사용자에게 결정 요청 ("P2 N건 발견 — 처리할까요?")
- **거짓양성**: 검증 후 보고에서 제외

각 수정 후 scan_static_patterns.py가 회귀 즉시 검출. 회귀 발생 시 추가 수정 1회 시도 후 보고.

### Step 4. 진척 파일 갱신

처리 완료 후 `.claude/audit-progress.md`를 Edit:

1. **Covered 섹션 끝에 추가**:
   ```
   - [x] <영역명> (P0:N, P1:M, <오늘 날짜>) — <변경 요약 한 줄>
   ```

2. **Uncovered 섹션에서 해당 줄 제거**

3. **Round Log 끝에 한 줄 추가**:
   ```
   - YYYY-MM-DD: <영역명> — P0:N, P1:M 처리. <자체 발견 회귀 K건>
   ```

날짜는 시스템 reminder의 `currentDate` 값 사용.

### Step 5. 결과 보고

라운드 결과 표준 보고 형식 (verify.md와 일관):

```
## /audit 결과 — <영역명>

| # | 우선순위 | 이슈 | 파일 | 변경 |
|---|---------|------|------|------|
| 1 | P0/P1 | ... | ... | ... |

### Explore 거짓양성 N건 (있으면)
### 자체 발견·즉시 수정 N건 (있으면)
### 다음 라운드 대상: <Uncovered 1순위>
```

## 우선순위 정렬 (Uncovered)

audit-progress.md의 Uncovered는 다음 기준으로 정렬되어 있음:

1. **모놀리스 (2000줄+)** 최상위 — 회귀 위험 최대
2. **사용자 직접 영향 시스템** 다음 — Battle/Capture/Cloud 등
3. **데이터 모델 + Save** — 마이그레이션 위험
4. **UI 컨트롤러** — OnGUI 회귀 검출 우선
5. **인프라/유틸** — 최후

사용자가 특정 영역을 우선시키고 싶으면 Uncovered 항목 순서를 수동 편집 가능.

## 거짓양성 차단 메커니즘

1. **Covered 자동 주입** — Explore 프롬프트에 누적 처리 영역 목록 포함. 같은 영역 재보고 차단.
2. **explore-standard.md 가드** — "거짓양성 보고 금지" 규칙 (이미 정의됨)
3. **scan_static_patterns hook** — Edit 후 즉시 새 회귀 검출. Explore가 미탐지한 패턴 보완.

## 비-자동 트리거 모드

Auto Mode가 비활성이고 사용자가 결정을 보류하고 싶으면:
- `/audit dry-run` → Step 1~2만 실행 (보고만, 수정 안 함)
- 인자 무시하고 일반 실행 — `/audit`

## 호출 예

```
사용자: /audit
Claude:
  Step 1. audit-progress.md 읽음 → Uncovered[0] = RaidBattleUI
  Step 2. Explore 위임 (Covered 15개 자동 주입)
  Step 3. P0:1 P1:2 발견 → 즉시 수정. 회귀 1건 자체 발견·수정
  Step 4. progress.md 갱신 (Covered에 RaidBattleUI 추가, Uncovered에서 제거)
  Step 5. 보고 + "다음 라운드: BattleScreenUI 나머지"

Stop hook 자동 발화: "audit 미검토 31개 — /audit으로 계속"
```

## 관련 자산

- `.claude/audit-progress.md` — 단일 진척 파일
- `.claude/hooks/audit_reminder.py` — Stop reminder
- `.claude/hooks/scan_static_patterns.py` — 회귀 패턴 즉시 검출
- `.claude/hooks/auto_verify_trigger.py` — 8항목 자동 권유
- `.claude/skills/explore-standard.md` — Explore 가드
- `.claude/skills/verify.md` — 8항목 검증 템플릿
