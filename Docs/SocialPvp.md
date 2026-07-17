# 친구 및 3:3 PvP

## 사용자 흐름

1. PlayScene 하단의 `PVP [F6]` 버튼을 연다.
2. 친구 탭에서 8자리 친구 코드를 교환하고 요청을 수락한다.
3. 친구에게 3:3 친선전을 신청하거나 랭크 탭에서 매칭을 시작한다.
4. 서버가 비슷한 레이팅(현재 ±350 RP)의 대기 사용자를 연결한다.
5. 기본 공격, 보유 기술, 교체, 기권 중 하나를 턴마다 선택한다.
6. 랭크전 종료 시 Elo 방식으로 RP와 승/패가 서버 트랜잭션에서 반영된다.

랭크 구간은 브론즈(0), 실버(1200), 골드(1400), 플래티넘(1600), 다이아몬드(1800), 마스터(2000+)다.

## 서버 권한 범위

- Firebase ID 토큰이 있어야 모든 소셜/PvP 요청을 사용할 수 있다.
- 팀은 정확히 3마리, 기술은 마리당 최대 4개로 검증한다.
- 공격 계산, 턴 소유권, HP, 교체, 승패, 레이팅은 Cloud Functions에서만 변경한다.
- 클라이언트가 Firestore의 친구/매칭/배틀/레이팅 문서를 직접 읽거나 쓰는 것은 보안 규칙으로 차단한다.
- `clientActionId`를 기록해 같은 전투 요청이 재전송되어도 중복 적용하지 않는다.

## 배포

Firebase CLI 로그인 후 프로젝트 루트에서 실행한다.

```powershell
npx --yes firebase-tools@15.22.1 login
npx --yes firebase-tools@15.22.1 deploy --only functions:socialPvpApi,firestore:rules --project insect-exploration-8f0ca
```

배포 URL은 프로젝트 ID에서 자동 생성되므로 `firebase_config.json`에 별도 URL을 넣지 않아도 된다. 다른 리전이나 프록시를 쓸 때만 `socialPvpApiUrl`을 지정한다.

## 무료 로컬 통합 테스트

Blaze 요금제 없이 Firebase Auth·Firestore·Functions 에뮬레이터에서 두 계정의 전체 PvP 흐름을 검증할 수 있다. 프로젝트 루트에서 실행한다.

```powershell
./Tools/Test-SocialPvpEmulator.ps1
```

첫 실행은 프로젝트 전용 Java 21 런타임과 Firebase 에뮬레이터를 내려받기 때문에 시간이 걸린다. 시스템 Java는 변경하지 않으며 다운로드 파일은 `.codex/tools/`에만 저장된다. 테스트는 두 가상 계정 생성, 친구 요청/수락, 친선 3:3, 랭크 매칭, 승패·Elo 반영, 리더보드를 순서대로 검증하고 종료 시 모든 로컬 데이터를 폐기한다.

## 실기기 검증

- 서로 다른 Firebase 계정 두 개와 각각 3마리 이상 편성된 배틀 팀이 필요하다.
- 친구 요청/수락 양방향 표시, 친선전 도전 수락, 랭크 매칭, 세 마리 전멸, 기권, 앱 재접속 후 진행 중 매치 복구를 확인한다.
- Functions 로그에서 `not_your_turn`, `processedActionIds`, 랭크전 종료 시 `ratingApplied=true`를 확인한다.
