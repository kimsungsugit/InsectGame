---
name: new-script
description: Unity C# 스크립트를 프로젝트 컨벤션에 맞게 생성합니다
user_invocable: true
args: "<ModuleName> <ScriptName> -- 예: Core PlayerHealth"
---

# 새 C# 스크립트 생성

프로젝트 컨벤션에 맞는 Unity C# 스크립트를 생성합니다.

## 컨벤션

- 네임스페이스: `InsectGame.{ModuleName}` (Core, Data, Battle, Capture, Dex, Spawning, UI)
- 경로: `Assets/Scripts/{ModuleName}/{ScriptName}.cs`
- MonoBehaviour 기반 클래스
- `[SerializeField] private` 패턴으로 필드 선언
- `[Header("섹션명")]`으로 Inspector 그룹핑
- PascalCase 클래스/메서드, camelCase private 필드

## 템플릿

```csharp
using UnityEngine;

namespace InsectGame.{ModuleName}
{
    public class {ScriptName} : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float exampleValue;

        private void Awake()
        {
        }
    }
}
```

사용자가 지정한 모듈과 이름으로 위 템플릿을 기반으로 스크립트를 생성하세요.
ScriptableObject, static 클래스 등 다른 타입이 필요하면 사용자에게 확인 후 적절히 변형하세요.
