---
description: 테스트 프레임워크, 컨벤션, 필수 기준
---

# 테스트 규칙

## 프레임워크
- NUnit (`using NUnit.Framework`)
- 테스트 파일 위치: `Assets/Tests/EditMode/`

## 러너는 PlayMode다 (폴더 이름에 속지 말 것)

폴더 이름은 `EditMode`지만 **EditMode 러너로는 0건이 잡힌다.** 이 프로젝트엔
`.asmdef`가 하나도 없어서 테스트가 별도 에디터 테스트 어셈블리가 아니라
런타임 어셈블리(`Assembly-CSharp`)로 컴파일되고, EditMode 러너는 그걸 보지 못한다.

```
-testPlatform PlayMode -testFilter InsectGame.Tests
```

`-testPlatform EditMode`를 쓰면 **0건을 실행하고 "성공"이라 보고한다.** 실행 개수를
반드시 확인할 것 — 현재 `[Test]`는 38개다. 0건 보고는 통과가 아니라 실패다.

EditMode 러너를 되살리려면 `Assets/Scripts`·`Assets/Editor`·`Assets/Tests`에 asmdef를
도입해야 한다(asmdef는 `Assembly-CSharp`를 참조할 수 없어 게임 코드 쪽도 함께 필요).
출시 후 별건.

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
- OnGUI 렌더링 코드 (IMGUI는 렌더 루프 없이 검증 불가)
- MonoBehaviour 생명주기 의존 로직 (`[UnityTest]` + `yield`가 필요. 현재 38개는 전부
  씬 없이 도는 순수 로직 `[Test]`다)
- 외부 서비스 호출 (Firebase, Firestore)
