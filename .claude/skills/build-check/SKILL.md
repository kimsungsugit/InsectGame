---
name: build-check
description: C# 스크립트의 컴파일 오류를 검사합니다 (정적 점검 + Unity CLI fallback)
---

# 빌드 검사

C# 코드의 컴파일 오류를 검사합니다. 두 가지 방식 — 빠른 정적 점검 + Unity CLI 정확 검증.

## 자동 점검 (이미 활성)

`.claude/hooks/quick_compile_check.py`가 PostToolUse hook으로 매 Edit/Write 후 자동 실행:
- 중괄호 매칭 (열림 vs 닫힘 카운트)
- 미선언 식별자 사용 의심 (`x +=` 같은 증감 좌변인데 클래스 필드/로컬 선언 없음)
- 흔한 using 누락 (`IEnumerator`, `List<>`, `Dictionary<>` 등)

이미 작은 컴파일 에러는 변경 직후 권유 컨텍스트로 알림. 별도 호출 불필요.

## 명시적 호출 절차

빠른 점검으로 부족할 때 (대규모 변경 후, 새 시스템 추가 후):

### 1. LSP 진단 (가장 빠름)

LSP 도구가 사용 가능한 경우 (csharp-lsp) 변경된 .cs 파일들의 진단을 즉시 확인.

### 2. Unity CLI 배치모드 (가장 정확, ~30초)

```
"$UNITY_EDITOR_PATH" -batchmode -nographics \
  -projectPath "C:/Project/곤충게임" \
  -logFile - -quit
```

- `UNITY_EDITOR_PATH`는 `.claude/settings.json` env에 정의됨
- 로그에서 `error CS\d+:` 패턴 grep으로 추출
- `warning CS\d+:`도 함께 보고 (정보용)
- 결과를 `.claude/cache/last-build.log`에 캐싱 (재실행 비용 절감)

### 3. dotnet build (선택)

`.csproj`가 있으면 `dotnet build` 가능. Unity 종속성 때문에 일반적으로 Unity CLI가 더 정확.

## 결과 보고 형식

```
=== 컴파일 검사 결과 ===
파일: <상대경로>
에러: N개
  - <파일>:<라인> error CS<번호>: <메시지>
경고: M개
  - <파일>:<라인> warning CS<번호>: <메시지>
```

에러 0개면 "통과". 에러 있으면 각 에러의 원인을 분석하고 수정 방안 제시.

## 캐싱

- `.claude/cache/last-build.log` (있다면)
- 직전 빌드 시점이 변경된 .cs 파일 mtime보다 새것이면 재사용
- 그렇지 않으면 Unity CLI 재실행
