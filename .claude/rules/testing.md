---
description: 테스트 프레임워크, 컨벤션, 필수 기준
---

# 테스트 규칙

## 프레임워크
- NUnit (`using NUnit.Framework`)
- EditMode 테스트 위치: `Assets/Tests/EditMode/`

## 컨벤션
- 클래스: `[TestFixture]` 어트리뷰트
- 메서드: `[Test]` 어트리뷰트
- 네이밍: `MethodOrProperty_Condition_ExpectedResult` (예: `Player_MaxIV_Is15`)
- 네임스페이스: `InsectGame.Tests`

## Assert 패턴
- `Assert.AreEqual(expected, actual)` - 값 비교
- `Assert.IsTrue()` / `Assert.IsFalse()` - 불리언
- `Assert.IsNotNull()` - null 체크
- `Assert.Greater()` / `Assert.GreaterOrEqual()` - 범위 검증

## 테스트 필수 기준
다음 변경 시 반드시 테스트를 추가하거나 갱신:
- **수치 공식 변경**: 데미지, 포획률, IV, 스탯 계산 등 수학 공식
- **데이터 모델 변경**: 세이브/로드에 영향을 주는 필드 추가/삭제
- **GameConstants 상수 변경**: 밸런스에 영향을 주는 상수
- **새 시스템 추가**: 핵심 로직에 대한 단위 테스트 (UI 제외)

## 테스트 제외 대상
- OnGUI 렌더링 코드 (IMGUI는 EditMode 테스트 불가)
- MonoBehaviour 생명주기 의존 로직 (PlayMode 테스트 필요)
- 외부 서비스 호출 (Firebase, Firestore)
