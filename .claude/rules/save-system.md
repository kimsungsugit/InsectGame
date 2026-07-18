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
- player_insects.json (보유 곤충 전체)
- player_candies.json (캔디)
- player_currency.json (코인, 젬)
- player_items.json (아이템)
- battle_team.json (5슬롯 팀)
- dex_save.json (도감 기록)

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

## 수정 규칙
- 새 세이브 필드 추가 시 기본값 필수
- GameSaveData에 클라우드 필드 추가 시 Firestore 포맷도 수정
- 기존 데이터 호환성 유지: **JsonUtility는 JSON에 없는 필드를 건드리지 않으므로
  C# 필드의 초기값이 그대로 남는다.** 따라서 새 필드는 반드시 의미 있는 기본값을
  갖도록 선언할 것. (옛 문서에 "누락 필드 무시" / "기본값으로 채움" 두 표현이
  갈라져 있었으나 동작은 이 한 가지다.)
- 기존 세이브 구조를 바꾼다면 마이그레이션 경로를 먼저 설계할 것
