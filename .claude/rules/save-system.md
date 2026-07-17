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
