---
description: ScriptableObject 생성 규칙, Database 패턴, 세이브 직렬화 분리 (Data/ 및 Resources/ 수정 시 필독)
---

# ScriptableObject 규칙

## 생성 규칙
- 데이터 SO에는 반드시 `[CreateAssetMenu(fileName = "New...", menuName = "InsectGame/...")]` 추가
- SO 인스턴스는 `Assets/Resources/` 하위에 배치하여 `Resources.Load<T>()` 로드

## Database 패턴
- `InsectDatabase`, `ItemDatabase` 등 목록형 SO는 배열/리스트로 엔트리 관리
- ID 기반 조회 메서드 제공: `GetById(int id)` 또는 `GetByName(string name)`

## 직렬화
- 세이브 데이터는 SO가 아닌 일반 클래스로 정의 (`[System.Serializable]`)
- JSON 직렬화: `JsonUtility.ToJson()` / `JsonUtility.FromJson<T>()`
- 파일명은 `GameConstants.SaveFiles`에서 관리
