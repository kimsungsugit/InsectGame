---
name: test
description: Unity EditMode 테스트를 실행합니다
user_invocable: true
---

# Unity EditMode 테스트 실행

Unity EditMode 테스트를 실행하고 결과를 보고합니다.

## 절차

1. Unity 에디터 경로를 확인합니다:
   - 환경변수 `UNITY_EDITOR_PATH` 사용 (settings.json에 설정됨)
   - 없으면 기본 경로 시도: `C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe`
   - 그래도 없으면 사용자에게 Unity 설치 경로를 물어봅니다
2. Unity CLI로 EditMode 테스트를 실행합니다:
   ```
   "$UNITY_EDITOR_PATH" -runTests -batchmode -nographics -projectPath "C:/Project/곤충게임" -testPlatform EditMode -testResults "C:/Project/곤충게임/TestResults.xml"
   ```
3. 테스트 결과 XML을 읽어 성공/실패를 요약 보고합니다
4. 실패한 테스트가 있으면 원인을 분석합니다

## 특정 테스트만 실행
인자로 테스트 클래스/메서드명이 주어지면 `-testFilter` 옵션을 추가합니다:
```
-testFilter "ClassName" 또는 -testFilter "ClassName.MethodName"
```
