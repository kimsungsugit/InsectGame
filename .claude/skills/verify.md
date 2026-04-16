---
name: verify
description: 기능 구현 후 코드 품질 검증을 실행합니다
user_invocable: true
---

# 구현 후 코드 검증

기능 구현이 완료된 후 잠재 버그와 코드 품질을 점검합니다.

## 검증 항목 (순서대로 실행)

### 1. 이벤트 구독 안전성
변경된 파일에서 `event Action` 또는 `+=` / `-=` 패턴 검색:
- **OnEnable + AutoWire 이중 구독**: 둘 다 같은 이벤트를 구독하면 2번 호출됨. 원칙: AutoWire에서만 구독, OnDisable에서 해제
- OnDisable에서 해제 누락
- null 체크 없는 구독/해제
- 구독 전 -= 로 기존 구독 해제하는지 확인

### 2. null 가드 패턴
변경된 코드에서:
- FindFirstObjectByType 결과에 null 체크
- AutoWire로 주입받는 필드의 사용 전 null 체크
- Singleton.Instance 사용 시 null 가능성 (UITheme 같은 케이스)
- GetComponent 결과 null 체크

### 3. 성능 패턴 (Update/OnGUI에서)
매 프레임 호출되는 코드에서:
- **transform.Find()**: 캐싱 필수 (private Transform 필드에 저장)
- **GameObject.Find()**: 캐싱 필수
- **Physics.OverlapSphere/Raycast**: 이동 없을 때 스킵, 또는 N프레임마다 호출
- **GetComponent/GetComponentsInChildren**: 캐싱 필수
- **new 배열/리스트/GUIStyle**: GC 압력. GUIStyle은 필드로 캐싱, 배열은 재사용
- **FindObjectsByType**: Update에서 금지, Start/이벤트에서만

### 4. Bootstrap 등록
새 MonoBehaviour가 추가되었으면:
- PlaySceneBootstrap.Build()에 등록되었는지
- 초기화 순서가 의존성 그래프에 맞는지
- AutoWire 호출이 누락되지 않았는지

### 5. 세이브 호환성
세이브 관련 클래스가 변경되었으면:
- 새 필드에 기본값이 있는지
- CloudSaveManager 동기화 필요 여부
- 기존 데이터 역직렬화 호환성

### 6. 모놀리스 영향
BattleScreenUI / RaidBattleUI / PlaySceneBootstrap 수정 시:
- 변경이 최소한인지
- Phase별 영향 범위 확인

### 7. 코드 컨벤션
- `[SerializeField] private` 패턴 준수
- PascalCase / camelCase 네이밍
- public 필드 직접 노출 여부
- MonoBehaviour 생성자 사용 여부
- `InsectGame.*` 네임스페이스 사용 여부

### 8. 데이터 매칭 무결성
ID 기반 매칭 로직이 변경되었으면:
- 모든 데이터 ID가 매칭 조건에 포함되는지 (곤충 ID → Build 메서드, 아이템 ID → 처리 분기)
- contains 순서로 인한 오매칭 (예: "antlion"이 "ant"에 먼저 매칭)
- 새 데이터 추가 시 매칭 분기 추가 여부

## 출력 형식
```
=== 구현 후 검증 결과 ===
[PASS] 이벤트 구독: 중복/누락 없음
[PASS] null 가드: 모든 외부 참조 체크 완료
[PASS] 성능: Update 내 캐싱 확인, 불필요한 호출 없음
[PASS] Bootstrap: 새 시스템 정상 등록
[PASS] 세이브: 변경 없음
[PASS] 모놀리스: 최소 수정
[PASS] 컨벤션: 위반 없음
[PASS] 데이터 매칭: 전체 ID 매칭 확인

총 8항목 중 8 PASS / 0 WARN / 0 FAIL
```

## 규칙
- FAIL이 있으면 즉시 수정
- WARN은 사용자에게 보고 후 판단
- 모든 항목 PASS일 때만 구현 완료로 보고
