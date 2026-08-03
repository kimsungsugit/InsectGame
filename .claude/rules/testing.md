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
반드시 확인할 것 — 현재 `[Test]` 메서드는 **242개**, 러너가 실제 실행하는 케이스는 **268개**다
(`[TestCase]` 파라미터화가 여러 케이스로 펼쳐진다). 단일 출처는 코드다 —
`grep -c "\[Test\]" Assets/Tests/EditMode/*.cs`의 합과 TestResults.xml의 `total`.
문서에 박아둔 숫자는 늘 낡는다(실제로 62로 적혀 있다가 147까지 벌어져 있었다).
0건 보고는 통과가 아니라 실패다.

**`-runTests`에 `-quit`를 같이 붙이지 말 것.** 붙이면 Unity가 테스트를 시작하기 전에 종료하는데
**exit 0에 `Exiting batchmode successfully now!`까지 찍어** 성공처럼 보이고, `TestResults.xml`은
아예 쓰이지 않는다. 이전 실행의 파일이 남아 있으면 그 낡은 `total`을 이번 결과로 착각하기 딱 좋다
(2026-08-03에 실제로 그렇게 254/254를 잘못 읽었다). 테스트 러너가 스스로 종료하므로 `-quit`은 불필요하다.

그래서 결과는 **두 가지를 함께** 봐야 한다 — `total`뿐 아니라 `TestResults.xml`의 **mtime이
이번 실행 시각인지**. 확실히 하려면 실행 전에 기존 파일을 치워 없는 상태에서 시작한다.

EditMode 러너를 되살리려면 `Assets/Scripts`·`Assets/Editor`·`Assets/Tests`에 asmdef를
도입해야 한다(asmdef는 `Assembly-CSharp`를 참조할 수 없어 게임 코드 쪽도 함께 필요).
출시 후 별건.

## 테스트 파일은 반드시 `#if UNITY_EDITOR`로 감쌀 것 (안 그러면 APK/AAB 빌드가 깨진다)

`.asmdef`가 없어 테스트가 `Assembly-CSharp`(런타임 어셈블리)로 컴파일되므로, 가드 없이 두면
테스트의 `nunit.framework` 참조가 IL2CPP 플레이어(APK/AAB) 빌드로 **새어 나가 링크가 실패한다**
(`Mono.Cecil.AssemblyResolutionException: Failed to resolve assembly: 'nunit.framework'`).

그래서 모든 EditMode 테스트 `.cs`는 **첫 줄 `#if UNITY_EDITOR`, 마지막 줄 `#endif`로 파일
전체를 감싼다.** 에디터 PlayMode 러너에선 `UNITY_EDITOR`가 정의돼 전부 그대로 돌고, 기기 빌드에선
통째로 제외된다. 기존 파일엔 이미 이 가드가 있으니 **새 테스트 추가 시 빠뜨리지 말 것** —
2026-07 실제로 새 테스트 4개가 가드 누락으로 APK 빌드 링크를 멈췄다(그 4개만으로 전체 빌드 실패).

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
- MonoBehaviour 생명주기 의존 로직 (`[UnityTest]` + `yield`가 필요. 현재 테스트는 전부
  씬 없이 도는 순수 로직 `[Test]`다)
- 외부 서비스 호출 (Firebase, Firestore)
