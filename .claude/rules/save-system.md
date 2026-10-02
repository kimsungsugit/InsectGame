---
description: 로컬 7개 JSON·Firestore 세이브 구조와 필드 추가·마이그레이션 규칙 (세이브 관련 파일 수정 시 필독)
---

# 세이브 시스템 규칙

## 로컬 세이브
- 경로: Application.persistentDataPath
- 직렬화: JsonUtility.ToJson() / FromJson<T>()
- 파일명: GameConstants.SaveFiles에 정의

## 파일 목록
- player_progress.json (level, xp)
- player_insects.json (보유 곤충 전체 — currentHp/isPoisoned/isParalyzed 포함, 아래 지속 HP 참조)
- player_candies.json (캔디)
- player_currency.json (코인, 젬)
- player_items.json (아이템)
- battle_team.json (5슬롯 팀)
- dex_save.json (도감 기록)
- story_progress.json (스토리 진행)
- island.json (나의 섬 — 보관함·배치·방목·누적 수확·가이드 진행. 아래 「섬」 참조)

## 퀘스트 세이브 (PlayerPrefs — JSON 아님)

퀘스트 진행은 위 7개 JSON에 **없다.** `TutorialQuestManager`가 **PlayerPrefs 4키**로 저장한다
(`GameConstants.cs`의 ProgressKey/CompletedKey/ActiveKey/UnseenKey). 계정 스코핑은
`AuthManager.ScopedKey`.

| PlayerPrefs 키 | 내용 | 클라우드 동기 |
|---|---|---|
| QuestProgress | 퀘스트별 진행 카운트 | O |
| QuestCompleted | 완료된 questId 집합 | O |
| ActiveQuest | 현재 활성 questId | O |
| QuestSideProgress | 서브 퀘스트별 진행 카운트 | O |
| QuestSideRepeat | 서브 퀘스트별 반복 완료 횟수(목표 상승 티어) | O |
| QuestUnseen | 미확인 완료 알림 | **X (로컬 전용)** |

**주의:** QuestUnseen은 클라우드에 안 올라간다 — 기기 간 알림 상태가 다를 수 있다.
퀘스트 세이브 필드를 늘리면 `CloudSaveManager` DTO(questProgress/questCompleted/activeQuest/
questSideProgress/questSideRepeat)와 직렬화/파싱/업로드/복원 4곳을 함께 고쳐야 클라우드에 반영된다.

## 클라우드 세이브
- Firestore REST API (PATCH /users/{userId})
- 자동저장: 120초 간격
- Bearer 토큰: AuthManager.Instance.IdToken
- 에러 처리: 404=신규유저, 401=인증실패, 기타=경고후 계속

## 섬 (island.json)

`IslandManager`가 쓰고 클라우드엔 블롭 하나(`GameSaveData.islandData`)로 올라간다 — 곤충 블롭과 같은 방식이라
`IslandSave`에 필드를 늘릴 때 DTO 4점 변경이 필요 없다.
- **컴팩트 JSON**(`ToJson(save, false)`)으로 쓴다. 이 파일이 통째로 Firestore 문서의 문자열 필드가 되고, 문서 한도(1MiB)를
  곤충 블롭과 나눠 쓴다.
- 좌표는 **섬 중심 기준**이라 섬을 넓혀도 기존 배치가 그대로 유효하다(마이그레이션 없음).
- 구세이브(파일 없음·빈 블롭)는 필드 기본값만으로 기본 섬이 된다. 손상·구버전·신버전 혼용은 `IslandSaveRules.Sanitize`가
  고친다 — 놓인 수가 보유 수를 넘으면 **보유 수를 올리고**(놓인 물건을 지우지 않는다), 모르는 물건 id는 **보존한다**.
- 충돌은 세이브 단위 last-write-wins다(블롭 내부 병합 없음).

## Firestore 문서 파서

`CloudSaveManager`의 복원은 `FirestoreDocParser`로 필드를 꺼낸다. **공백 없는 형식과 줄바꿈·들여쓰기된 형식을 둘 다 읽는다.**
예전 구현은 `"키":{"stringValue":"`라는 공백 없는 마커를 통째로 찾아서, 응답이 들여쓰기돼 오면 한 필드도 못 찾고
전부 기본값(레벨 0·캔디 0·빈 문자열)으로 복원했다. 필드를 늘릴 때 마커 문자열을 손으로 만들지 말 것.

## 수정 규칙
- 새 세이브 필드 추가 시 기본값 필수
- GameSaveData에 클라우드 필드 추가 시 Firestore 포맷도 수정
- 기존 데이터 호환성 유지: **JsonUtility는 JSON에 없는 필드를 건드리지 않으므로
  C# 필드의 초기값이 그대로 남는다.** 따라서 새 필드는 반드시 의미 있는 기본값을
  갖도록 선언할 것. (옛 문서에 "누락 필드 무시" / "기본값으로 채움" 두 표현이
  갈라져 있었으나 동작은 이 한 가지다.)
- 기존 세이브 구조를 바꾼다면 마이그레이션 경로를 먼저 설계할 것

## 곤충 지속 HP·상태 (player_insects.json)

`PlayerInsectData`는 전투 간 유지되는 `currentHp`/`isPoisoned`/`isParalyzed`를 저장한다.
- **`currentHp` 기본값 `-1`(미초기화 센티넬)** — 구세이브엔 이 필드가 없어 로드 시 -1로 남는다.
  `PlayerInsectCollection` 로드 루프(EnsureInstanceId 인근)의 `EnsureHp`가 -1이면 실제 MaxHp로 채워
  **구세이브 곤충이 0 HP 기절로 뜨는 것을 방지**한다. `IsFainted`는 `currentHp == 0`(초기화 후에만 유효).
- `isPoisoned`/`isParalyzed` 기본 `false`(무상태) — 마이그레이션 무해.
- **클라우드는 자동**: `CloudSaveManager`가 player_insects.json 블롭 전체를 저장하므로 PlayerInsectData
  필드 추가에 DTO 4점 변경 불필요(퀘스트 세이브와 다름).

## 곤충 크기·포획시각 (player_insects.json)

- **`sizeRoll` 기본값 `-1`(미초기화 센티넬)** — `currentHp`와 같은 방식이다. 로드 루프의
  `EnsureSize`가 `-1`이면 **`instanceId` 해시**로 채운다(`InsectSizeCalculator.RollFromInstanceId`).
  0으로 채우면 구세이브 곤충이 전부 최소 크기가 되고, 매번 새로 굴리면 볼 때마다 크기가 바뀐다.
- **`capturedUnix` 기본 `0`(미상)** — 주간 크기 대결 집계에서 "이번 주 아님"으로 자연히 걸러지므로
  마이그레이션이 필요 없다.
- **주간 대결 기록은 저장하지 않는다.** 보유 곤충 중 대상 종이면서 `capturedUnix`가 이번 주에 든
  개체의 최대 크기를 매번 파생한다 — 블롭이 이미 클라우드 동기라 기록도 기기 간에 따라온다.
  PlayerPrefs에 저장하는 건 보상 수령 상태(`WeeklyContestClaimed`, "주차:등급") 하나뿐이다.
- 전투 종료/교체 시 `PlayerInsectCollection.SetAfterBattle`이 남은 HP·감염을 기록(무료 전체치료 없음).
  치료는 병원/치료 아이템의 `HealInsect`/`FullHeal`/`CurePoison`/`CureParalysis`로만.
