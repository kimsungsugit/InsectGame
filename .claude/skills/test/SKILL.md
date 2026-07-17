---
name: test
description: Unity 유닛 테스트를 실행합니다 (PlayMode 러너)
---

# Unity 테스트 실행

`Assets/Tests/EditMode/`의 NUnit 테스트를 실행하고 결과를 보고합니다.

## 러너는 PlayMode다 — EditMode는 0건을 잡는다

폴더 이름이 `EditMode`라 헷갈리지만, **`-testPlatform EditMode`는 0건을 실행하고
"성공"이라 보고한다.** 이 프로젝트엔 `.asmdef`가 하나도 없어 테스트가 별도 에디터
테스트 어셈블리가 아니라 런타임 어셈블리(`Assembly-CSharp`)로 컴파일되고, EditMode
러너는 그걸 보지 못한다. 배경은 `.claude/rules/testing.md`.

## 절차

1. Unity 경로는 환경변수 `UNITY_EDITOR_PATH`를 쓴다
   (`.claude/settings.json`이 단일 출처). 없으면 사용자에게 묻는다 —
   **경로 사본을 이 문서에 적어두지 않는다.** 적어두면 반드시 썩는다.
2. 테스트 실행:
   ```
   "$UNITY_EDITOR_PATH" -runTests -batchmode -nographics \
     -projectPath "$CLAUDE_PROJECT_DIR" \
     -testPlatform PlayMode -testFilter InsectGame.Tests \
     -testResults "$CLAUDE_PROJECT_DIR/TestResults.xml"
   ```
3. `TestResults.xml`을 읽어 요약 보고한다.
4. 실패가 있으면 원인을 분석한다.

## 보고 전 필수 확인 — 실행 개수

**`total="0"`은 통과가 아니라 실패다.** 결과 XML의 `total` 속성을 반드시 읽고 보고에
포함할 것. 실행 개수가 0이거나 실제 `[Test]` 수보다 크게 적으면 필터·러너·컴파일 중
하나가 깨진 것이므로 PASS를 보고하지 말고 원인을 보고한다.

기대 개수는 코드가 단일 출처다:
```
grep -c "\[Test\]" Assets/Tests/EditMode/*.cs
```

## 특정 테스트만 실행

인자로 클래스/메서드명이 주어지면 `-testFilter`를 좁힌다:
```
-testFilter "InsectGame.Tests.GameConstantsTests"
-testFilter "InsectGame.Tests.GameConstantsTests.Player_MaxIV_Is15"
```
