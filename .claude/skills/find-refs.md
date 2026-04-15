---
name: find-refs
description: 클래스/메서드의 참조를 프로젝트 전체에서 검색합니다
user_invocable: true
args: "<검색할 심볼명>"
---

# 참조 검색

지정된 클래스, 메서드, 필드가 프로젝트 내 어디에서 사용되는지 찾습니다.

## 절차

1. 먼저 해당 심볼이 **정의된 위치**를 찾습니다 (class, method, field 선언)
2. 프로젝트 전체 `.cs` 파일에서 해당 심볼의 **모든 참조**를 검색합니다
3. 참조 종류별로 분류합니다:
   - 직접 호출
   - 상속/구현
   - SerializeField 연결 (Inspector)
   - GetComponent / FindFirstObjectByType 조회
4. 결과를 파일:라인 형태로 정리합니다

참고: `.prefab`, `.unity`, `.asset` 파일에서도 GUID 기반으로 참조 가능성을 언급하세요.
