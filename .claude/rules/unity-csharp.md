---
description: 네임스페이스·네이밍·필드 선언, AutoWire/이벤트/싱글턴/풀 패턴, 금지 사항
---

# Unity C# 코딩 규칙

## 네임스페이스
- 루트: `InsectGame`
- 모듈별: `InsectGame.Core`, `InsectGame.Data`, `InsectGame.Battle`, `InsectGame.Capture`, `InsectGame.Dex`, `InsectGame.Spawning`, `InsectGame.UI`
- 테스트: `InsectGame.Tests`

## 네이밍
- 클래스/메서드/프로퍼티: PascalCase
- private 필드: camelCase (언더스코어 접두사 안 씀)
- 상수: PascalCase (GameConstants에 집중)
- 이벤트: PascalCase 동사형 (`BattleEnded`, `CurrencyChanged`)

## 필드 선언
- Inspector 노출: `[SerializeField] private Type fieldName;`
- public 프로퍼티는 화살표: `public int Value => value;`
- `[Header("섹션명")]`으로 Inspector 그룹핑
- public 필드 직접 노출 금지 (SerializeField + private)

## 아키텍처 패턴
- **AutoWire**: 의존성은 `public void AutoWire(T dep)` 메서드로 주입. Bootstrap이 호출.
- **이벤트**: `public event Action<T> EventName` → OnEnable 구독, OnDisable 해제
- **Singleton**: `public static T Instance` (기존 9개만, 신규 추가 시 architect 에이전트 상담)
- **오브젝트 풀**: SimpleObjectPool.Get()/Return() (Instantiate/Destroy 대신)
- **서비스 조회**: FindFirstObjectByType<T>() (fallback용, AutoWire 우선)
- **상태 관리**: Phase enum 기반 (UI), bool 플래그 (컨트롤러)

## 새 시스템 추가 시
1. 네임스페이스에 맞는 폴더에 스크립트 생성
2. AutoWire() 메서드로 의존성 수신
3. PlaySceneBootstrap.Build()에 등록
4. 세이브 필요시 GameConstants.SaveFiles에 파일명 추가
5. 이벤트 정의하여 UI와 느슨하게 연결

## 금지
- `.meta` 파일 수정
- MonoBehaviour 생성자 사용 (Awake/Start 사용)
- `#region` 남용
- 새 싱글턴 무분별 추가
- Destroy() 직접 호출 (풀 Return 우선)
