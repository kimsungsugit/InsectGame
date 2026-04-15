---
name: save-migration
description: 세이브 데이터 구조 변경 시 호환성을 검증하고 마이그레이션 코드를 생성합니다
user_invocable: true
args: "<변경된 세이브 클래스명 또는 파일명>"
---

# 세이브 마이그레이션

세이브 데이터 구조 변경 시 기존 유저 데이터 호환성을 검증하고 필요한 마이그레이션 코드를 생성합니다.

## 절차

### 1. 변경 감지
- `git diff`로 세이브 관련 클래스의 변경사항을 분석합니다
- 대상 파일: GameConstants.SaveFiles에 정의된 7개 세이브 파일에 대응하는 클래스
  - `PlayerProgressData`, `PlayerInsectCollection`, `PlayerCandyInventory`
  - `PlayerCurrencyWallet`, `PlayerItemInventory`, `BattleTeamManager`
  - `DexSaveData`

### 2. 호환성 분석
각 변경에 대해 판정:
- **안전**: 새 필드 추가 + 기본값 설정 (JsonUtility는 누락 필드를 기본값으로 역직렬화)
- **주의**: 필드 타입 변경, 필드명 변경 (기존 데이터 유실 가능)
- **위험**: 필드 삭제, 구조 변경 (기존 데이터 파싱 실패 가능)

### 3. 마이그레이션 코드 생성
위험/주의 변경이 있으면:
1. 세이브 파일 버전 필드 추가 (없는 경우)
2. 이전 버전 데이터를 새 구조로 변환하는 마이그레이션 메서드 작성
3. 로드 시 버전 확인 후 자동 마이그레이션 적용

### 4. 클라우드 동기화 검증
- Firestore REST API의 PATCH 필드가 로컬 세이브 구조와 일치하는지 확인
- `CloudSaveManager.cs`의 직렬화/역직렬화 로직 갱신 필요 여부 판단

## 출력 형식
```
[세이브 마이그레이션 리포트]
├─ 변경 파일: filename.cs
├─ 변경 유형: 안전/주의/위험
├─ 변경 내용:
│   ├─ + 추가 필드: fieldName (기본값: X)
│   ├─ ~ 변경 필드: oldName → newName
│   └─ - 삭제 필드: fieldName
├─ 로컬 세이브: 호환/마이그레이션 필요
├─ 클라우드 세이브: 호환/업데이트 필요
└─ 필요 작업: [구체적 조치사항]
```
