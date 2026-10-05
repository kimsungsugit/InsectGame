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

**2026-10-02 기준 운영 프로젝트(`insect-exploration-8f0ca`)에는 배포된 함수가 하나도 없다** —
`firebase functions:list`가 0건이고 `socialPvpApi` 주소는 404다(`verifyGooglePlayPurchase`도 미배포).
그래서 아래 명령은 "액션 추가"가 아니라 **온라인 기능 전체의 첫 운영 배포**다:
- 친구·랭크 PvP·5인 월드 채널·섬 공유가 **함께** 켜진다. 이미 설치된 클라이언트도 로그인하면 이 서버를 쓰기 시작한다.
- Cloud Functions 배포는 종량제(Blaze) 요금제가 필요하다. 월드 채널은 접속자마다 초당 1회, PvP는 2.5초마다 호출한다.
- 단위·에뮬레이터 테스트는 통과했지만 운영에서 돌아간 적은 없다.

같은 날 사용자가 **배포하지 않기로** 정했다. 다시 배포를 검토할 때 이 절부터 읽을 것.

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

첫 실행은 프로젝트 전용 Java 21 런타임과 Firebase 에뮬레이터를 내려받기 때문에 시간이 걸린다. 시스템 Java는 변경하지 않으며 다운로드 파일은 `.codex/tools/`에만 저장된다. 테스트는 두 가상 계정 생성, 친구 요청/수락, 친선 3:3, 랭크 매칭, 승패·Elo 반영, 리더보드, 섬 공유(올리기·방문·좋아요·차단·삭제)를 순서대로 검증하고 종료 시 모든 로컬 데이터를 폐기한다.

오랜만에 돌리면 Functions 로드(`googleapis` 초기화)가 60초를 넘겨 `Cannot determine backend specification. Timeout after 60000`으로 실패할 수 있다. 코드 결함이 아니라 디스크 캐시가 식어서다 — 한 번 더 실행하면 통과한다(2026-10-02 실측: 첫 실행 실패, 재실행 통과).

## 실기기 검증

- 서로 다른 Firebase 계정 두 개와 각각 3마리 이상 편성된 배틀 팀이 필요하다.
- 친구 요청/수락 양방향 표시, 친선전 도전 수락, 랭크 매칭, 세 마리 전멸, 기권, 앱 재접속 후 진행 중 매치 복구를 확인한다.
- Functions 로그에서 `not_your_turn`, `processedActionIds`, 랭크전 종료 시 `ratingApplied=true`를 확인한다.

## 섬 공유

"나의 섬"의 공개·방문·좋아요는 `socialPvpApi`에 얹은 액션 5개로 한다. Firestore 규칙상 클라이언트는
타인 문서를 못 읽으므로 전부 이 함수를 거친다. 기획은 `Docs/IslandDesign.md`, 검증 로직의 단일 출처는
`functions/island.js`(순수 함수 — `functions/test/island.test.js`가 에뮬레이터 없이 돈다).

### 요청·응답

요청은 다른 액션과 같다(POST, `Authorization: Bearer <Firebase ID token>`). 본문 필드는
`action`, `island`(섬 스냅샷 JSON을 **문자열로**), `isPublic`(bool), `friendCode`, `targetUid`.
클라이언트는 액션과 무관하게 다섯 필드를 전부 보내므로 **빈 문자열(공백만 있는 것 포함)은 없는 값**이다.
`isPublic`은 진짜 `true`일 때만 공개다.

응답은 `{ success: true, island: IslandInfo }`(`deleteIsland`만 `{ success: true }`).

```
IslandInfo = {
  ownerUid: string,
  ownerName: string,      // 서버가 정한다 — 클라이언트 값을 믿지 않는다
  friendCode: string,
  isPublic: boolean,
  likes: number,
  visits: number,
  likedByMe: boolean,     // 요청자가 오늘(UTC) 이미 좋아요를 눌렀는가
  updatedAtMs: number,    // 마지막 publish 시각(ms). 없으면 0
  snapshot: string        // 섬 스냅샷 JSON 문자열. getIsland만 채우고 나머지는 ""
}
```

| action | 입력 | 동작 |
|---|---|---|
| `publishIsland` | `island`, `isPublic` | 내 `islands/{uid}`를 저장(merge). `likes`/`visits`는 건드리지 않는다(없을 때만 0) |
| `getMyIsland` | 없음 | 내 IslandInfo(`snapshot: ""`). 올린 적이 없어도 성공 — `isPublic:false`, 수치 0, 이름·친구 코드는 계산해서 채운다 |
| `getIsland` | `friendCode` 또는 `targetUid`(둘 다 있으면 `targetUid`) | 대상의 IslandInfo + `snapshot`. 본인이 아니면 `visits` +1(매 호출마다) |
| `likeIsland` | `targetUid` | `islands/{target}/likes/{요청자}`에 오늘 날짜를 적고 `likes` +1. 같은 UTC 날짜엔 한 번만 |
| `deleteIsland` | 없음 | 내 섬 문서와 `likes` 서브컬렉션(앞 400건)을 지운다. 없어도 성공 |

- `friendCode`는 대소문자를 가리지 않는다(앞뒤 공백 제거 후 대문자). `islands`의 `friendCode` 필드로 찾으므로
  주인이 PVP 창을 한 번도 안 열어 `socialProfiles`가 없어도 찾힌다. 단일 필드 색인은 자동이라 색인 설정이 따로 없다.
- `ownerName`은 `syncProfile`과 같은 규칙이다 — `socialProfiles/{uid}.displayName`, 없으면 토큰의 `name`,
  그것도 없으면 "탐험가". publish 때 문서에 저장하고 읽을 때는 저장된 값을 쓴다(이름을 바꾸면 다시 올려야 반영된다).
- 하루의 경계(`dayKey`)는 UTC `YYYY-MM-DD`다 — 한국 시간 오전 9시에 좋아요가 다시 열린다.

### 오류 코드

| 코드 | HTTP | 언제 |
|---|---|---|
| `island_invalid` | 400 | `island`가 문자열이 아니거나, JSON 파싱 실패, 48,000자 초과, 최상위가 객체가 아님 |
| `island_target_required` | 400 | `getIsland`에 `targetUid`·`friendCode`가 둘 다 없음 / `likeIsland`에 `targetUid` 없음 |
| `island_not_found` | 400 | 그 친구 코드·uid의 섬 문서가 없음(올린 적 없음·삭제됨) |
| `cannot_like_self` | 400 | 자기 섬에 좋아요 |
| `island_private` | 409 | `isPublic`이 false이고 본인이 아님 |
| `user_blocked` | 409 | 어느 방향이든 차단 관계(`socialBlocks`) |
| `already_liked_today` | 409 | 같은 UTC 날짜에 이미 누름 |

판정 순서는 `island_not_found` → `island_private` → `user_blocked` → `already_liked_today`다.
새 코드를 늘리면 `createSocialPvpHandler`의 `conflicts`/`clientErrors` 집합에도 넣을 것 — 빠뜨리면 500 `social_pvp_failed`로 뭉개진다.

### 스냅샷 검증

서버는 카탈로그를 모른다. **형식만** 보고 정리해 저장하며, 모르는 id는 클라이언트가 스스로 버린다.

- `version`은 1로 다시 쓴다. `sizeLevel` 0~3, `comfort` 0~9999(정수 clamp). `ownerName`과 모르는 필드는 버린다.
- `placed`: 앞 400개까지. `id`가 `/^[a-z][a-z0-9_]{1,31}$/`이 아니면 그 항목을 버린다. `x`·`z`는 반올림 후 -16~15,
  `rot`은 0~3으로 정규화(음수 포함).
- `insects`: 앞 10개까지. `insectId`는 `/^[a-z][a-z0-9_]{1,47}$/`, `level` 1~80, `shiny`는 진짜 `true`만.
- 숫자 칸은 숫자와 숫자 문자열만 읽는다. 그 외(null·bool·배열·NaN)는 기본값(좌표·회전 0, 레벨 1)이다.

### 저장 구조

```
islands/{uid}
  uid, ownerName, friendCode, isPublic,
  snapshot        // 정리한 스냅샷을 JSON.stringify한 문자열
  likes, visits,
  updatedAt       // serverTimestamp
  updatedAtMs     // Date.now()
islands/{uid}/likes/{likerUid}
  dayKey          // 마지막으로 누른 UTC 날짜. 누른 사람당 문서 하나를 덮어쓴다
  likedAtMs
```

둘 다 `firestore.rules`에서 `allow read, write: if false`다(Admin SDK 전용).

### 계정 삭제

**계정을 지울 때 클라이언트가 `deleteIsland`를 먼저 불러야 한다** — ID 토큰이 살아 있을 때만 호출할 수 있고,
안 부르면 공개 섬이 주인 없이 남아 친구 코드로 계속 찾힌다. 서버에는 액션만 있고 자동 정리는 없다.

한계 둘:
- `likes`는 앞 400건까지만 섬 문서와 한 배치로 지운다. 좋아요를 누른 사람이 그보다 많으면 나머지는 고아
  문서로 남는다(uid와 날짜뿐이고 클라이언트가 읽을 수 없다).
- 탈퇴자가 **남의 섬에 누른** 좋아요 문서(`islands/*/likes/{탈퇴자 uid}`)는 지우지 않는다.

### 배포 (사용자가 직접 실행)

섬 액션은 함수 `socialPvpApi`에 들어 있고 `islands` 규칙은 `firestore.rules`에 있으므로 명령은 위 「배포」와 같다.
**코드만 작성된 상태다.** `socialPvpApi` 자체가 운영에 올라간 적이 없으므로(위 「배포」 머리말) 섬만 따로 켤 수는 없다 —
섬 공유를 켜면 친구·PvP·월드가 함께 켜진다. 프로젝트 루트에서:

```powershell
npx --yes firebase-tools@15.22.1 login
npx --yes firebase-tools@15.22.1 deploy --only functions:socialPvpApi,firestore:rules --project insect-exploration-8f0ca
```

배포 전 확인: `cd functions; npm test`(단위)와 `./Tools/Test-SocialPvpEmulator.ps1`(에뮬레이터 통합, 결과에 `"islandShare": "passed"`).
배포 전에는 함수 주소 자체가 없어 **404**가 온다(클라이언트는 "섬 공유 서버가 아직 준비되지 않았습니다"로 보여 준다).
`unknown_action`(400)은 섬 액션을 모르는 **옛 버전의 함수**가 올라가 있을 때만 나온다.
