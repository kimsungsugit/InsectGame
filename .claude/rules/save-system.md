---
trigger: glob
globs:
  - "Assets/Scripts/Core/PlayerProgress*.cs"
  - "Assets/Scripts/Core/CloudSaveManager.cs"
  - "Assets/Scripts/Core/Player*Inventory.cs"
  - "Assets/Scripts/Core/PlayerCurrencyWallet.cs"
  - "Assets/Scripts/Dex/DexSave*.cs"
  - "Assets/Scripts/Core/BattleTeamManager.cs"
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
- 기존 데이터 호환성 유지 (JsonUtility는 누락 필드 무시)
