---
trigger: always
---

# 아키텍처 규칙

## Bootstrap 시스템
- PlaySceneBootstrap가 65개 시스템을 순차 생성
- 새 시스템 추가 시 반드시 Build() 메서드에 등록
- 초기화 순서 의존성 주의: Auth → Cloud → World → Data → Player → Battle → Capture → UI

## 의존성 규칙
- 순환 의존성 금지
- 하위 모듈이 상위 모듈 참조 금지:
  - UI → Core (O), Core → UI (X)
  - Battle → Data (O), Data → Battle (X)
- 모듈 간 통신은 이벤트(Action<T>)로

## 세이브 호환성
- 기존 세이브 파일 구조 변경 시 마이그레이션 고려
- 새 필드 추가는 기본값 설정 필수 (JsonUtility는 누락 필드를 기본값으로)
- 클라우드 세이브(Firestore) 필드도 동기화

## 모놀리스 주의
- BattleScreenUI (2,950줄), RaidBattleUI (2,875줄), Bootstrap (4,987줄)
- 이 파일들 수정 시 Phase 단위로 영향 범위 확인
- 가능하면 새 메서드로 추출, 파일 분할은 신중히

## 성능 가이드
- FindFirstObjectByType는 AutoWire 캐싱으로 대체 권장
- Update()에서 매 프레임 할당(new) 지양
- 오브젝트 풀 활용 (SimpleObjectPool)
- 거리 기반 컬링 활용 (DistanceCulling: 25m/20m)
