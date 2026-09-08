# Google Play 출시 체크리스트

클라이언트는 Unity IAP 5.3.1을 사용하며 포함된 Google Play Billing Client는 8.3.0이다.

## 1. 결제 백엔드 배포

1. Google Cloud에서 Android Publisher API를 활성화한다.
2. Firebase Functions 런타임 서비스 계정을 Play Console의 **사용자 및 권한**에 추가하고 주문/구독 조회 권한을 부여한다.
3. `firebase deploy --only functions:verifyGooglePlayPurchase,firestore:rules`를 실행한다.
4. 기본 엔드포인트는 프로젝트 ID에서 자동 계산된다. 함수 리전/이름을 바꾼 경우에만
   `Assets/Resources/firebase_config.json`에 아래 값을 추가한다.

```json
{
  "purchaseVerificationUrl": "https://asia-northeast3-PROJECT_ID.cloudfunctions.net/verifyGooglePlayPurchase"
}
```

프로젝트 ID와 HTTPS 엔드포인트를 둘 다 확인할 수 없으면 보석 구매는 자동으로 비활성화된다.

## 2. Play Console 상품

모두 **소비성 인앱 상품**으로 만들고 활성화한다.

| 상품 ID | 지급 보석 |
|---|---:|
| `gem_200` | 150 |
| `gem_550` | 400 |
| `gem_1200` | 900 |

가격 표시는 Play Console의 현지화 가격을 사용하므로 클라이언트의 `priceKRW`는 준비 중 폴백일 뿐 실제 청구 기준이 아니다.

## 3. 내부 테스트 필수 시나리오

- 정상 구매 후 보석 증가 및 앱 재시작 후 유지
- 구매 취소 시 미지급
- 결제 보류 후 승인 시 1회 지급
- 네트워크 단절 중 결제 후 재접속 시 재검증/지급
- 같은 구매 토큰 재전달 시 중복 지급 없음
- 다른 계정이 이미 처리된 토큰을 제출하면 거부
- 서버 검증 실패 시 구매가 확정/소비되지 않고 다음 실행에서 재시도
- Play Console 환불/취소 후 운영 대응 절차 확인

## 4. 현재 저장소 기준 출시 차단 항목

- 업로드 키스토어가 프로젝트에 설정되어 있지 않다. 비밀 키는 저장소에 커밋하지 말고 CI 또는 로컬 보안 저장소에서 주입한다.
- 일반/원형 앱 아이콘은 준비됐다. 적응형 아이콘 품질을 높이려면 전경/배경 분리 에셋을 추가로 준비한다.
- 앱 안의 개인정보처리방침 버튼은 구현됐다. 실제 공개 HTTPS URL과 Play Console 데이터 보안 양식, 외부 계정 삭제 요청 URL이 필요하다.
- 실제 AAB 빌드와 Play 내부 테스트 트랙 결제는 아직 수행되지 않았다.
- 스토어 설명, 스크린샷, 피처 그래픽, 콘텐츠 등급/대상 연령은 Play Console에서 별도 준비해야 한다.
- Firestore `worlds` 문서는 현재 인증 사용자 공동 쓰기 구조다. 출시 전 서버 트랜잭션과 정리 작업으로 옮기는 것이 권장된다.
- 클라이언트 세이브는 싱글플레이 편의를 우선한 구조다. 경쟁/거래 기능을 추가한다면 보석 차감과 아이템 지급도 서버 권위로 전환해야 한다.

## 5. 확인 완료 항목

- 패키지 ID `com.insectexploration.game`이 `google-services.json`과 일치한다.
- 설치된 Android SDK에 API 35/36이 있고 Target API는 자동 최고 버전이다.
- Android 아키텍처는 ARM64로 설정되어 64비트 요구사항을 만족한다.
- 앱 내 2단계 확인 계정 삭제 흐름이 구현되어 있다.
- Unity IAP 영수증은 Firebase 인증 서버에서 Google Play Developer API로 검증한 뒤에만 지급된다.
- 회사명은 `Insect Exploration`으로 설정되어 있다.
- Android 릴리스 자동화는 ARM64, IL2CPP, AAB를 강제하고 서명 비밀번호를 환경변수로만 받는다.

## 6. 개인정보처리방침 URL

Firebase 배포, Play Console 상품 생성, 공개 개인정보처리방침 URL, 업로드 키스토어는
외부 계정/비밀값이 필요하므로 저장소만으로 완료할 수 없다.

`Assets/Resources/firebase_config.json`에 공개 HTTPS 주소를 추가합니다.

```json
{
  "privacyPolicyUrl": "https://example.com/privacy"
}
```

로그인 화면의 `개인정보처리방침` 버튼이 이 주소를 엽니다. Play Console의
앱 콘텐츠 및 데이터 보안에도 같은 주소를 사용합니다.

## 7. 서명 AAB 생성

업로드 키스토어는 저장소 밖의 안전한 위치에 생성하고 별도로 백업한다.

```powershell
keytool -genkeypair -v -keystore C:\secure\insectgame-upload.jks `
  -alias insectgame-upload -keyalg RSA -keysize 2048 -validity 10000
```

빌드 직전 터미널/CI 비밀값으로 아래 환경변수를 설정한다. 실제 비밀번호는 문서,
스크립트, Unity 프로젝트 설정 파일에 기록하지 않는다.

- `INSECTGAME_KEYSTORE_PATH`
- `INSECTGAME_KEYSTORE_PASS`
- `INSECTGAME_KEYALIAS_NAME`
- `INSECTGAME_KEYALIAS_PASS`
- `INSECTGAME_VERSION_NAME` (선택, 예: `1.0.0`)
- `INSECTGAME_VERSION_CODE` (선택, Play 업로드마다 증가)
- `INSECTGAME_AAB_PATH` (선택)

Unity 메뉴 `Insect Game > Release > Build Signed AAB`를 실행한다. 기본 출력은
`Builds/Android/insect-game.aab`이다.
