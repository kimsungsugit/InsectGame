---
name: add-region
description: 새 리전을 추가하고 19개 항목(필수 8 + 시나리오 6 + 선택 5) 누락을 강제 검증합니다
argument-hint: "<regionId> <displayName> <requiredLevel>"
---

# 새 리전 추가

리전 1개 추가는 코드 9곳, 데이터 6곳, 비주얼 1곳을 동시에 손대야 하는 가장 복잡한 콘텐츠 작업.
**원칙: 필수 8개 grep 중 0건이 하나라도 있으면 PASS 보고 금지. 비관적 시나리오 A~F 모두 사전 점검.**

## Phase 1: 필요 정보

| 필드 | 설명 | 예시 |
|------|------|------|
| regionId | 고유 ID (영문 소문자) | `tundra` |
| displayName | 한글 표시명 | "툰드라" |
| description | 설명 | "..." |
| themeColor | 맵 UI 색상 | new Color(0.7, 0.85, 1.0) |
| centerPosition | 월드 중심 좌표 | new Vector3(-150, 0, 200) |
| radius | 리전 범위 (40~55 권장) | 50f |
| requiredLevel | 진입 필요 레벨 | 1~50 |
| insectIds[] | 출현 곤충 ID 목록 (≥1 필수) | { "ice_beetle", "frost_moth" } |
| guardianInsectId | 수문장 곤충 ID (meadow 제외 필수) | "ice_titan" |
| guardianLevel | 수문장 레벨 | 30 |
| subAreas[] | 서브에리어 배열 (선택) | { tundra_cave, tundra_glacier } |
| environmentType | 서브에리어 환경 (기존 11종 권장) | cave/deep_forest/underwater/pond/fog/reeds/peak/flower_maze/greenhouse/temple/underground |

⚠️ **environmentType은 기존 11종에서 선택 권장**. 신규 추가 시 `SubAreaWorldBuilder.cs` + `SubAreaEnvironment.GetProfileForType()` 양쪽에 분기 추가 필요.

## Phase 2: 자동화 작업

### 2-1. RegionData 코드 스니펫 (RegionDefinitions.CreateAll() 끝에 추가)
```csharp
// RegionDefinitions.cs CreateAll() 끝
new RegionData {
    regionId = "<regionId>",
    displayName = "<displayName>",
    centerPosition = new Vector3(...),
    radius = 50f,
    requiredLevel = <requiredLevel>,
    insectIds = new[] { ... },
    guardianInsectId = "...",
    guardianLevel = ...,
    subAreas = new SubAreaData[] { ... },
}
```

### 2-2. RegionManager switch 분기 추가 위치
- `RegionManager.cs` GetNextRegionId()에 `case "<previousRegionId>": return "<regionId>";`
- `RegionManager.cs` GetPreviousRegionId()에 `case "<regionId>": return "<previousRegionId>";`

### 2-3. BuildXxxTerrain() 시그니처
```csharp
// RegionTerrainBuilder.cs
private void Build<RegionId>Terrain()
{
    // 기존 BuildMeadowTerrain / BuildPondTerrain 패턴 참조
    // 실제 지형 디자인은 visual-dev 에이전트 위임
}
```

## Phase 3: 필수 8개 등록 지점

| # | 등록 지점 | 누락 시 증상 | grep 검증 |
|---|---|---|---|
| 1 | `RegionDefinitions.cs` CreateAll() RegionData 추가 | 리전 자체가 존재 안 함 | `Grep "regionId = \"<regionId>\"" Assets/Scripts/Core/RegionDefinitions.cs` |
| 2 | RegionData 7필드 (insectIds, centerPosition, radius, requiredLevel, guardianInsectId, subAreas, environmentType) | 빈 맵 / 가디언 없음 / 잘못된 좌표 | RegionData 인스턴스 필드 사람 검토 |
| 3 | `RegionManager.cs` GetNextRegionId() switch | 진입 후 다음 리전 해금 안 됨 | `Grep "case .*return \"<regionId>\"" Assets/Scripts/Core/RegionManager.cs` |
| 4 | `RegionManager.cs` GetPreviousRegionId() switch | 가디언 좌표 계산 오류(시나리오 E) | `Grep "case \"<regionId>\":" Assets/Scripts/Core/RegionManager.cs` |
| 5 | `RegionTerrainBuilder.cs` BuildAllRegions switch + Build`<리전>`Terrain() 메서드 | 지형 안 그려짐 → 평지 | `Grep "case \"<regionId>\":\|Build<RegionPascal>Terrain" Assets/Scripts/Core/RegionTerrainBuilder.cs` |
| 6 | SpawnPoint.regionId / regionInsectIds 할당 | 곤충 스폰 0마리 | `Grep "<regionId>" Assets/Scripts/Spawning Assets/Scripts/Core/PlaySceneBootstrap.cs` |
| 7 | `RegionMapUI.cs` connections + `:670-683` GetRegionSymbol switch | 미니맵 길/심볼 표시 안 됨 → "???" | `Grep -c "<regionId>" Assets/Scripts/UI/RegionMapUI.cs` (≥ 2 요구) |
| 8 | AudioManager.PlayBGMForRegion 리전별 BGM 키 | 무음 또는 이전 BGM 잔류 | `Grep "<regionId>" Assets/Scripts/Core/AudioManager.cs` |

## Phase 4: 비관적 시나리오 6개 (사전 점검)

각 시나리오는 위 필수 8개의 부분집합을 누락했을 때 발생하는 구체적 증상. 매 추가 시 6개 모두 grep 점검:

| 시나리오 | 증상 | 검증 grep |
|---|---|---|
| **A. 스폰 0마리** | 진입했는데 잡을 곤충 없음 | `Grep "insectIds = new\[\]" RegionDefinitions.cs` 영역에서 `<regionId>` insectIds 길이 ≥ 1 + 모든 ID가 InsectDatabase에 존재 |
| **B. 진입 불가 (dead-end)** | NextRegionId 무한 루프 또는 도달 불가 | `Grep "return \"<regionId>\"" Assets/Scripts/Core/RegionManager.cs` (1+ 있어야 어딘가에서 해금됨) |
| **C. 빈 맵** | 시각적으로 황무지 | `Grep "case \"<regionId>\":\|Build<RegionPascal>Terrain" Assets/Scripts/Core/RegionTerrainBuilder.cs` 모두 매칭 |
| **D. 서브에리어 깡통** | 서브에리어 진입했더니 빈 공간 | environmentType이 11종 중 하나여야 함. 신규 타입이면 `SubAreaWorldBuilder.cs` + `SubAreaEnvironment.cs`에 분기 추가됐는지 검증 |
| **E. 가디언 좌표 오류** | 가디언이 맵 원점(0,0,0)과 리전 중점 사이에 잘못 스폰 | GetPreviousRegionId()에 `<regionId>` case 누락 시 `prevId == null` → fromCenter = Vector3.zero. Phase 3 #4 grep 통과 필수 |
| **F. 세이브 오염** | 기존 PlayerPrefs(`InsectGame.UnlockedRegions` / `InsectGame.DefeatedGuardians`) 깨짐 | **기존 regionId 변경 절대 금지**. 새 ID만 추가 |

## Phase 5: 선택 5개 등록 지점 (gameplay 향상)

| # | 항목 | 효과 |
|---|---|---|
| 9 | `TutorialQuestManager` 리전별 퀘스트 추가 | 신규 리전 진입 튜토리얼 |
| 10 | environmentType 신규 정의 (D 회피하려면 11종 사용 권장) | 새로운 시각/환경 효과 |
| 11 | `DexController` 리전별 도감 필터 | 도감에서 리전별 진행률 표시 |
| 12 | `BattleArenaController` 리전 환경 효과 | 리전별 배틀 배경 변경 |
| 13 | `InsectSpawner` 디스폰 거리 60m → 리전별 조정 | 넓은 리전에서 디스폰 조기 발생 방지 |

## Phase 6: 자동 grep 검증 시퀀스 (예: regionId=`tundra`)

```bash
# 필수 8개
Grep "regionId = \"tundra\"" Assets/Scripts/Core/RegionDefinitions.cs
Grep "case .*return \"tundra\"" Assets/Scripts/Core/RegionManager.cs
Grep "case \"tundra\":" Assets/Scripts/Core/RegionManager.cs
Grep "case \"tundra\":\|BuildTundraTerrain" Assets/Scripts/Core/RegionTerrainBuilder.cs
Grep "tundra" Assets/Scripts/Spawning Assets/Scripts/Core/PlaySceneBootstrap.cs
Grep -c "tundra" Assets/Scripts/UI/RegionMapUI.cs   # ≥ 2 요구
Grep "tundra" Assets/Scripts/Core/AudioManager.cs

# 시나리오 A (스폰)
Grep "ice_beetle\|frost_moth" Assets/Scripts/Data Assets/Scripts/Core   # insectIds 모두 존재 확인

# 시나리오 D (서브에리어)
Grep "environmentType = \"<envType>\"" Assets/Scripts/Core/RegionDefinitions.cs   # 11종에 포함되는지

# 시나리오 F (세이브)
Grep "tundra" "$env:APPDATA\..\LocalLow\<Company>\InsectGame"   # PlayerPrefs 잔존 확인 (선택)
```

0건 매칭이 하나라도 있으면 → **FAIL 표 작성 + 누락 위치별 담당 에이전트 안내**.

## Phase 7: 에이전트 위임 가이드

| 영역 | 주담당 | 부수 |
|---|---|---|
| RegionData / SubAreaData SO 데이터 모델 | data-architect | — |
| RegionManager 진행 로직 (switch 분기) | game-designer | — |
| RegionTerrainBuilder Build`<리전>`Terrain() 시각 디자인 | visual-dev | — |
| RegionMapUI connections / GetRegionSymbol | ui-dev | — |
| SpawnPoint.regionId 할당 + 곤충 풀 | capture-dev | — |
| AudioManager BGM 키 등록 | visual-dev | — |
| BattleArenaController 리전 환경 효과 (선택) | battle-dev | visual-dev |

`.claude/rules/agent-coordination.md` 갱신 — `RegionData/RegionManager/RegionTerrainBuilder` 행 신규 추가 (Phase D에서 일괄).

## Phase 8: 세이브 호환성 경고

⚠️ **기존 regionId 변경 금지**. 다음 PlayerPrefs 키가 쉼표 구분 문자열로 저장:
- `InsectGame.UnlockedRegions` ("meadow,pond,garden,...")
- `InsectGame.DefeatedGuardians` ("meadow,pond,...")

기존 ID 변경 시 unlockedRegions HashSet에 매칭되는 RegionData가 없어 사실상 잠금 해제 무효.

⚠️ **regionId 삭제 금지**: PlayerPrefs에 잔존하면 `GetRegionById(deletedId)` → null. 사용처 null 가드 확인.

마이그레이션 필요 시 → `/save-migration` 호출.

## Phase 9: 완료 후 /verify 강제 호출

```
/verify
```
특히 다음 항목 직격:
- 항목 4 (Bootstrap 등록: RegionData가 RegionDefinitions.CreateAll()에 들어갔는지)
- 항목 5 (세이브 호환성: PlayerPrefs 영향)
- 항목 8 (데이터 매칭 무결성: regionId↔switch 5곳, insectIds↔InsectDatabase)

## 체크리스트 요약

- [ ] Phase 1 정보 수집 + environmentType 11종에서 선택
- [ ] Phase 2 RegionData 스니펫 + switch 분기 위치 + Build`<리전>`Terrain 시그니처 준비
- [ ] Phase 3 필수 8개 grep 모두 통과
- [ ] Phase 4 비관적 시나리오 A~F 모두 사전 점검
- [ ] Phase 5 선택 5개 중 적용 항목 결정
- [ ] Phase 6 grep 시퀀스 0건 매칭 0건
- [ ] Phase 7 적절한 에이전트 위임 (특히 BuildXxxTerrain은 visual-dev)
- [ ] Phase 8 기존 regionId 변경/삭제 없음
- [ ] Phase 9 `/verify` 호출 완료
