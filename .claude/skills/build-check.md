---
name: build-check
description: C# 스크립트의 컴파일 오류를 검사합니다
user_invocable: true
---

# 빌드 검사

C# 코드의 컴파일 오류를 검사합니다.

## 절차

1. LSP 진단(csharp-lsp)을 활용하여 현재 열린 파일들의 에러를 확인합니다
2. 에러가 있으면 파일명, 라인, 메시지를 정리하여 보고합니다
3. 에러 원인을 분석하고 수정 방안을 제시합니다

## 대안 (LSP 미사용 시)

Unity CLI 배치모드로 컴파일 검증:
```
"$UNITY_EDITOR_PATH" -batchmode -nographics -projectPath "C:/Project/곤충게임" -logFile - -quit
```
- 환경변수 `UNITY_EDITOR_PATH`가 settings.json에 설정되어 있습니다
- 로그에서 `error CS` 패턴을 검색하여 컴파일 에러를 추출합니다
- 경고(`warning CS`)도 함께 보고합니다
