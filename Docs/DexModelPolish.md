I now have a complete understanding of the file. Let me produce the design proposal.

---

# 곤충 프로시저럴 모델 품질 개선안 (InsectEntity.cs 단일 작성자)

대상 파일: `C:/Project/곤충게임/Assets/Scripts/Spawning/InsectEntity.cs` 전체를 정독함. 34종 빌더 + 공용 헬퍼(`MakePart` L988, `MakeWing` L1006, `MakeLegs` L1018, `MakeAntennae` L1042, `MakeEyes` L1055, `ApplyColor` L1069) 분석 완료.

## 1. 진단 — 왜 "퀄리티 떨어짐"으로 보이는가

| # | 원인 | 근거 (코드 위치) |
|---|------|------------------|
| A | **셰이딩 평면·무광·림 부재** | `ApplyColor`(L1069)가 `mat.color`만 세팅. Standard 셰이더의 `_Glossiness`/`_Metallic`/`_EmissionColor` 미설정 → Unity 기본 Smoothness 0.5에 스펙큘러 약함. 곤충 외골격의 광택/림라이트 전무. 모든 파트가 매트한 점토처럼 보임. |
| B | **눈 생기 불균형** | `MakeEyes`(L1055)에는 흰자+동공+하이라이트 3중 구성이 있어 좋음. 그러나 `BuildGenericBeetle`(L245-248), `BuildHornBeetle`(L271-274), `BuildRhinocerosBeetle`(L574-577), `BuildDragonfly`(L374), `BuildCicada`(L440) 등은 `MakeEyes`를 안 쓰고 흰자+검은 동공만 직접 만듦 → **하이라이트 없음 → 죽은 눈**. 치비 톤 핵심인 "큰 눈 반짝임"이 종마다 들�쭉날쭉. |
| C | **다리 단순·관절 빈약** | `MakeLegs`(L1018)는 대퇴+무릎구+경절 3분절이라 구조는 괜찮으나, 좌우 대칭 직선이고 발끝(tarsus) 없음, 모든 다리가 동일 각도(30°/10°)·동일 z 간격(0.2)이라 기계적. 접지감(발끝 구) 부재로 공중에 떠 보임. |
| D | **더듬이 직선·곡선감 부재** | `MakeAntennae`(L1042)는 캡슐 1개 + 끝 구 1개. 굴절(곡선)이 없어 막대기 느낌. 치비 곤충의 부드러운 안테나 곡선 부재. |
| E | **거친 비례** | 다수 빌더가 `Body` 1개 구에 머리를 바로 붙임. `BuildGenericBeetle`(L240) 몸통 (0.7,0.4,0.9)에 가슴(prothorax) 분절 없음 → 머리-몸 연결이 뭉툭. 치비 톤은 머리가 크고 몸이 둥글어야 하는데 머리 비율이 작음(Head 0.4 vs Body 0.7). |
| F | **하이라이트/그림자 색 단조** | `dark` 계산(L167)이 단순 0.5배라 음영이 칙칙. 상단 광택(top rim) 없음. 셸 글로스가 일부 비틀(Jewel L549, Rhino L562)에만 있고 공용화 안 됨. |
| G | **그림자 부재 가능성** | 프리미티브 기본 MeshRenderer는 그림자 캐스팅 ON이지만, 무광 셰이딩 탓에 접지 그림자 빼면 입체감 부족(GroundMarker L1146는 평면 원판이라 그림자 대체 안 됨). |

핵심: **개별 빌더는 이미 정성껏 만들어져 있고**, 가장 큰 ROI는 **공용 헬퍼 6개를 손보면 34종 전체가 동시에 좋아지는 것**이다. 그 다음 대표 종 비례 보강.

---

## 2. 공용 개선 (전 종 동시 적용)

### 2-1. `ApplyColor` — 매끈한 셰이딩 + 약한 광택 + 림 느낌 + 이미션

가장 영향이 큰 변경. 색만 칠하던 것을 PBR 파라미터까지 설정. **API 시그니처는 보존**(기본 인자 추가로 하위 호환).

**변경 위치**: L1069 `private void ApplyColor(GameObject go, Color color)`

**구체값**:
- 시그니처를 `ApplyColor(GameObject go, Color color, float smoothness = 0.55f, float metallic = 0.15f, float emission = 0f)` 로 확장 (기존 모든 호출은 인자 3개라 그대로 동작).
- 불투명(`color.a >= 1`) 경로에서 Standard 셰이더에:
  - `mat.SetFloat("_Glossiness", smoothness)` — 외골격 광택. 기본 0.55 (현재 암묵 0.5보다 약간 위, 플라스틱 치비 톤).
  - `mat.SetFloat("_Metallic", metallic)` — 0.15로 미세 금속감(딱정벌레 키틴). 셸/보석류는 호출부에서 0.6 이상 override.
  - 미세 림 느낌: `_EmissionColor`를 베이스색의 8%로 깔아 음영부가 완전히 죽지 않게 — `if (emission > 0f) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * emission); }`. 발광 파트(Firefly LightOrgan, Glow)는 호출부에서 emission 0.8~1.2 전달.
- 반투명 경로(날개)는 현행 유지하되 `_Glossiness`만 0.2로 낮춰 날개가 번들거리지 않게.
- URP fallback 경로(`Universal Render Pipeline/Lit`)에서는 키 이름이 다르므로: `mat.SetFloat("_Smoothness", smoothness); mat.SetFloat("_Metallic", metallic);` 를 셰이더 이름 분기로 같이 세팅(이미 `Shader.Find` 분기 존재하므로 셰이더 이름 비교 한 줄 추가).

이 한 변경으로 A·F·G 원인 대부분이 전 종에서 개선됨.

### 2-2. 림 라이트 / 상단 글로스 공용 헬퍼 추가

곤충 등껍질 상단에 흰색 반투명 글로스 한 줄을 자동으로 얹는 헬퍼를 신설해 주요 빌더가 1줄로 호출.

**신규 메서드**: `private void MakeTopGloss(Vector3 bodyCenter, Vector3 bodyScale, float intensity = 0.14f)`
- 내부: `MakePart("TopGloss", PrimitiveType.Sphere, bodyCenter + new Vector3(0f, bodyScale.y*0.35f, bodyScale.z*0.05f), new Vector3(bodyScale.x*0.7f, bodyScale.y*0.18f, bodyScale.z*0.75f), new Color(1f,1f,1f,intensity))`.
- Jewel(L549)·Rhino(L562)에 이미 있는 `ShellGloss` 패턴을 공용화. 딱정벌레 계열(Generic/Horn/Jewel/Longhorn/Dung/Click/Diving/Ladybug) `Body` 직후 1줄 추가로 광택 통일.

### 2-3. `MakeEyes` — 치비 톤 큰 눈 + 강한 하이라이트로 강화, 그리고 전 종 적용

**변경 위치**: `MakeEyes` L1055.

**구체값**:
- 흰자 크기 키움: 흰자는 `size`, 하지만 동공 비율을 0.6→**0.66**, 하이라이트 비율 0.2→**0.28**로 키우고 하이라이트를 동공 위 안쪽으로 더 또렷하게(`zPos+0.06`, y `+0.04`).
- **두 번째 작은 하이라이트(서브 글린트)** 추가 — 동공 우하단에 `size*0.12` 흰점. 치비 캐릭터의 "촉촉한 눈" 시그니처.
- 흰자에 `ApplyColor(..., smoothness:0.8)` 효과를 주기 위해, `MakeEyes` 내부 흰자 생성을 `MakePart` 직후 `ApplyColor(eye, Color.white, 0.85f, 0f)` 재호출 — 눈만 매끈하게(촉촉).

**더 중요**: 직접 흰자/동공을 만드는 빌더들을 `MakeEyes` 호출로 교체하여 하이라이트를 일괄 부여:
- `BuildGenericBeetle` L245-248 → `MakeEyes(0.62f, 0.13f)` (z는 머리 0.5+offset).
- `BuildHornBeetle` L271-274 → `MakeEyes(0.7f, 0.14f)`.
- `BuildRhinocerosBeetle` L574-577 → `MakeEyes(0.75f, 0.14f)` (현재 눈 위치 x=±0.22가 MakeEyes의 ±0.12보다 넓으므로 — 아래 2-6 참고로 `MakeEyes`에 x간격 인자 추가).
- `BuildDragonfly` L374-375, `BuildCicada` L440-443: 복안은 컬러 유지하되 하이라이트 글린트 1개씩 추가(`MakePart Highlight Sphere size*0.25 white`). 곤충다움 위해 흰자 교체는 안 함, 하이라이트만.

### 2-4. `MakeEyes` x간격 파라미터화 (대형 종 대응)

**변경**: 시그니처 `MakeEyes(float zPos, float size, float xSpread = 0.12f)`. 내부 `-0.12f`/`0.12f`를 `-xSpread`/`xSpread`로. 기본값으로 기존 호출 전부 무변경. Rhino/Horn처럼 머리가 넓은 종은 `xSpread:0.2f` 전달.

### 2-5. `MakeLegs` — 발끝(tarsus) + 자연스런 굴절·비대칭

**변경 위치**: `MakeLegs` L1018.

**구체값**:
- 경절 끝에 **발끝 구(tarsus)** 추가: 각 다리 하단에 `MakePart($"FootL{i}"/$"FootR{i}", Sphere, (∓0.345, -0.4, z), Vector3.one*0.03f, joint)`. 접지감 부여(원인 C).
- 다리 각도에 z기반 변주: 앞·중·뒷다리가 같은 각도라 기계적 → 대퇴 z회전을 `26f + i*4f`로(앞다리 살짝 앞으로 뻗고 뒷다리 뒤로). 경절 각도도 `8f + i*3f`.
- 대퇴를 z축으로 살짝 앞뒤 펼침: 앞다리쌍(i=0)은 `localPos.z`에 `+0.02`, 뒷다리쌍은 `-0.02` 가산해 부채꼴 배치.
- 무릎 구 약간 키움 0.045→**0.05**, 발끝과 함께 관절 가시성 향상.
- 다리 굵기 미세 테이퍼: 대퇴 0.055 유지, 경절 0.04→끝으로 갈수록 `0.035`. (캡슐 스케일 x/z `0.035`)

이 변경만으로 `MakeLegs`를 쓰는 ~20종이 동시에 접지·생동감 향상.

### 2-6. `MakeAntennae` — 곡선감(3분절 굴절) + 끝 볼 강조

**변경 위치**: `MakeAntennae` L1042.

**구체값**:
- 현재 캡슐 1개 → **2분절(베이스+팁)로 굴절**. 베이스 캡슐 각도 `Euler(-35, 0, ±15)`, 그 끝에서 팁 캡슐 `Euler(-8, 0, ±8)`로 꺾어 부드러운 S곡선. 팁 위치 y를 베이스보다 살짝 위.
  - `AntBaseL/R` (0.03,0.16,0.03) at `(∓0.1, 0.18, zBase+0.13)`, rot `(-38,0,±16)`.
  - `AntMidL/R` (0.025,0.14,0.025) at `(∓0.15, 0.36, zBase+0.2)`, rot `(-10,0,±8)`.
  - 끝 볼 `AntTipL/R` 위치를 Mid 끝으로 이동, 크기 feathered 0.08 / 일반 0.055.
- `feathered=true`(나방/모기)일 때 팁에 양옆 작은 깃 큐브 2개씩 추가(나방 더듬이 깃털감) — `MakePart FeatherL/R Cube (0.06,0.012,0.025)`.

이로써 D 원인이 전 종에서 해소(곡선 더듬이).

### 2-7. `dark` 음영색 보정 (호출 측, BuildModel L167)

**변경 위치**: L167 `Color dark = new Color(col.r*0.5f+0.05f, ...)`.

**구체값**: 단순 절반이 칙칙 → 채도 유지 음영으로. `col`을 HSV로 풀어 `val*0.62`, `sat*1.08`(상한 1)로 어둡지만 색감 살아있는 dark 생성. 무채색화 방지 → 전 종 음영이 생기있어짐(원인 F).

---

## 3. 대표 종 비례·파트 보강

### 3-1. `BuildGenericBeetle` (L238) — 치비 비례 재조정

문제: 머리(0.4)가 몸(0.7) 대비 작아 곤충스럽지만 치비답지 않음. 가슴 분절 없음.

**구체값**:
- `Body` (0.7,0.4,0.9) → **(0.72,0.46,0.86)** 약간 통통·짧게.
- **Prothorax(가슴마디) 신규**: `MakePart("Prothorax", Sphere, (0,0.12,0.32), (0.5,0.34,0.32), dark)` — 머리와 몸 사이 연결 자연화.
- `Head` (0.4,0.35,0.4)→**(0.46,0.42,0.42)** 키우고 z 0.5→**0.56**.
- 눈: 직접 생성(L245-248) 삭제 → `MakeEyes(0.68f, 0.13f)`.
- `Body` 직후 `MakeTopGloss((0,0,0),(0.72,0.46,0.86))` 추가.
- 셸 색에 metallic 강조: `ShellL/R` 생성 후 `ApplyColor(shell, dark, 0.6f, 0.4f)`로 키틴 광택.

### 3-2. `BuildButterfly` (L278) — 큰 날개 곡선 + 또렷한 눈

문제: 날개가 평평 Cube/Sphere 조합이라 가장자리 각짐, 몸이 가늘어 치비감 약함.

**구체값**:
- `Body` capsule (0.15,0.4,0.15)→**(0.18,0.34,0.18)** 짧고 통통.
- `Head` (0.25)³→**(0.3,0.3,0.28)**, 큰 눈 강조 위해 `MakeEyes(0.45f, 0.16f)` 이미 호출(L301) — z 0.4→0.45, 크기 0.18→0.16은 유지하되 2-3의 강화 하이라이트 자동 적용.
- 윗날개 Cube(MakeWing) 모서리 완화: 윗날개 끝을 둥글게 — `WingTipL/R`(L294) Sphere를 더 크게 (0.12,0.02,0.18)→**(0.18,0.025,0.24)**, 위치 바깥으로 -0.8→**-0.88**.
- **날개 살짝 위로 꺾기(dihedral)**: `MakeWing`에 회전 인자가 없으므로 윗날개 z스케일은 유지하되 y를 0.1→0.13으로 올려 V자 느낌. (MakeWing 시그니처 보존 위해 위치만 조정)
- 날개 그라데이션: 바깥쪽 `wingEdge` 띠를 좀 더 넓게(WingTip 확대로 처리), 안쪽 spot 색 채도 +0.1.
- 날개에 smoothness 낮게: 윗/아랫날개 생성 후 `ApplyColor(wing, wingCol, 0.25f, 0f)`로 천 느낌(번들거림 제거).

### 3-3. `BuildRhinocerosBeetle` (L557) — 뿔 곡선 + 넓은 눈 + 셸 광택 공용화

문제: 뿔이 직선 실린더 2개라 뚝뚝 끊김. 눈 하이라이트 없음(직접 생성).

**구체값**:
- 뿔 굴절 자연화: `HornMain` 각도 25°, `HornMid`(현 구) 사이에 **중간 캡슐 1개** 추가해 3분절 곡선 — `MakePart("HornCurve", Cylinder, (0,0.55,0.8), (0.08,0.18,0.08), body, Euler(38,0,0))`. `HornTip` 각도 40→**48**로 더 위로 휨.
- 뿔 끝 **분기(이중 뿔)**: `HornForkL/R` 작은 실린더 2개 `(∓0.05,0.78,1.0),(0.04,0.1,0.04), Euler(50,0,±12)` — 장수풍뎅이 Y자 뿔 실루엣.
- 눈 직접생성(L574-577) 삭제 → `MakeEyes(0.78f, 0.14f, 0.2f)` (2-4의 xSpread 활용, 넓은 머리 대응).
- `ShellGloss`(L562) 유지 + `MakeTopGloss` 호출은 중복이므로 생략, 대신 `Shell` 생성 후 `ApplyColor(shell, dark, 0.7f, 0.5f)`로 강한 키틴 광택(원인 A를 대표 종에서 극대화).
- `Body` (0.9,0.55,1.1) 양호, `Head` z 0.6→**0.62**로 뿔 베이스와 정렬.

### 3-4. (보너스) `BuildBee` (L378) — 퍼즈·줄무늬는 양호, 눈/광택만

이미 `MakeEyes`(L401) 사용 중이라 2-3 강화 자동 적용. 추가로:
- `Body` 생성 후 `MakeTopGloss((0,0,0),(0.55,0.45,0.7), 0.1f)`로 노란 몸 광택.
- `ThoraxFuzz`(L389)는 smoothness 낮게 `ApplyColor(..., 0.2f, 0f)` 호출해 퍼지(보송) 대비 강조.

---

## 4. 보존 사항 (회귀 방지 — 검증 완료)

- **`MakePart`/`MakeWing` API 시그니처 유지**: 모든 신규 인자는 옵셔널 기본값. 기존 34종 빌더 호출 무변경.
- **풀 재사용 흐름 무영향**: 변경은 전부 `BuildModel`(L162) 하위 시각 파트 생성에 한정. `ClearChildren`(L77)이 매 `Initialize`/`BuildForBattle`마다 자식 전부 파괴하므로 새 파트도 동일하게 정리됨. 캐시(`cachedNameLabel`/`cachedShinySparkle`)·`despawnedThisCycle` 가드 무관.
- **`forBattle` 동작 보존**: 회전 정지(L88-89), bob/wing 애니메이션(L91)은 파트 *이름*(`WingL`/`WingR`, `NameLabel`)에만 의존. 신규 파트 이름이 이 예약어와 겹치지 않게 명명(`TopGloss`, `Foot*`, `AntBase/Mid`, `HornCurve` 등). `AnimateWings`(L137)의 `transform.Find("WingL/R")`는 그대로 동작.
- **셰이더 fallback 체인 유지**: `ApplyColor`의 `Shader.Find` 4단 fallback(L1073-1077) 보존. PBR 프로퍼티 세팅은 `Standard`/`URP Lit`에서만(셰이더 이름 분기), `Unlit/Color` fallback 시 `_Glossiness` 미존재여도 `SetFloat`는 무해(존재하지 않는 프로퍼티는 무시)하나 안전하게 셰이더 이름 가드 권장.
- **성능**: 추가 파트는 종당 +2~8개(발끝 6, 더듬이 +2, 글로스 1 수준). 최대 20마리 동시 스폰이므로 드로우콜 증가 미미. 단, `ApplyColor`가 파트마다 `new Material`(L1078) 생성하는 기존 구조는 **개선 범위 밖**(별도 머티리얼 캐싱은 큰 리팩터라 본 제안서에서 제외, 보고만).

## 5. 우선순위 (ROI 순)

1. **2-1 `ApplyColor` PBR** — 단일 변경으로 전 종 셰이딩/광택/림 동시 개선. 최고 ROI.
2. **2-3/2-4 `MakeEyes` 강화 + 직접생성 빌더 6곳 교체** — 치비 톤 핵심(큰 눈 반짝임) 통일.
3. **2-5 `MakeLegs` 발끝·굴절** — ~20종 접지감.
4. **2-6 `MakeAntennae` 곡선** — 막대기 → 부드러운 안테나.
5. **2-2 `MakeTopGloss` + 2-7 dark 보정** — 입체감·음영 색감.
6. **3-1~3-4 대표 종 비례** — 핵심 종 실루엣 마감.

전부 `InsectEntity.cs` 한 파일 내 변경이라 단일 작성자로 충돌 없음. 수치 변경(밸런스/세이브/공식)은 없으므로 EditMode 테스트 영향 없음(시각 전용, `.claude/rules/testing.md`의 OnGUI/시각 제외 대상에 해당).