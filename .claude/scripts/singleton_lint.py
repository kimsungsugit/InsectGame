"""singleton-lint — 파기된 싱글턴이 static에 남는 것을 검출.

이 저장소의 매니저 9종은 `public static T Instance`를 들고 있고, 전부 `PlaySceneBootstrap`이
`EnsureComponent<T>("World/…")`로 만든다 — **경로에 부모가 있어 씬 스코프다.** 로그아웃과
계정 삭제가 씬을 통째로 재로드하므로(`AccountSettingsUI.ReloadScene`) 이들은 실제로 파기된다.

파기됐는데 static이 그 참조를 붙들고 있으면 **두 관용구가 서로 다른 답을 낸다**:

    if (X.Instance != null) X.Instance.Foo();   // UnityEngine.Object의 오버로드된 == → 안 부른다
    X.Instance?.Foo();                          // 진짜 null 검사 → **부른다** → MissingReferenceException

저장소 안에 `Instance?.`가 19곳 있다. 그래서 호출부를 하나씩 고치는 대신 뿌리에서 막는다 —
싱글턴은 `OnDestroy`에서 자기 static을 비운다.

    private void OnDestroy()
    {
        if (ReferenceEquals(Instance, this)) Instance = null;
    }

`ReferenceEquals`를 쓰는 이유: `Instance == this`는 파괴 검사를 타서 파기 중에는 false가 될 수
있고, 그러면 정작 비워야 할 때 안 비운다.

2026-08-23 감사 시점엔 9종 중 **1종(WorldChannelManager)만** 이 처리를 하고 있었다.

exit 0 = 통과, 1 = 위반, 2 = 파싱 실패(구조가 바뀌었으니 파서부터 확인할 것).
"""
import glob
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# 싱글턴이지만 MonoBehaviour가 아니라 파기 대상이 아닌 것 — 근거와 함께 적는다.
EXEMPT = {
    # UITheme: ScriptableObject 기반 테마 홀더. 씬 오브젝트가 아니라 OnDestroy 수명이 다르다.
    "UITheme",
}


def main() -> int:
    files = sorted(glob.glob(os.path.join(ROOT, "Assets/Scripts/**/*.cs"), recursive=True))
    if not files:
        print("Assets/Scripts에서 .cs를 하나도 못 찾았다 — 경로가 바뀌었는가?", file=sys.stderr)
        return 2

    singletons = []          # (클래스, 파일, 해제하는가)
    for path in files:
        src = io.open(path, encoding="utf-8", errors="replace").read()
        # `public static Foo Instance { get; private set; }` / `public static Foo Instance;`
        decl = re.search(r"public\s+static\s+\w+\s+Instance\s*[{;=]", src)
        if not decl:
            continue
        # **Instance를 선언한 클래스**를 고른다. 파일 첫 `public class`를 집으면 헬퍼 타입이
        # 먼저 선언된 파일에서 엉뚱한 이름을 보고한다(GachaBoxManager.cs가 `GachaResult`로 나왔다).
        before = [m.group(1) for m in
                  re.finditer(r"public\s+(?:sealed\s+)?class\s+(\w+)", src[:decl.start()])]
        if not before:
            continue
        cls = before[-1]
        if cls in EXEMPT:
            continue
        if ": MonoBehaviour" not in src and ":MonoBehaviour" not in src:
            continue   # 씬 오브젝트가 아니면 OnDestroy가 없다
        clears = re.search(r"Instance\s*=\s*null", src) is not None
        singletons.append((cls, os.path.relpath(path, ROOT).replace("\\", "/"), clears))

    if not singletons:
        print("MonoBehaviour 싱글턴을 하나도 못 찾았다 — `public static T Instance` 형태가 바뀌었는가?",
              file=sys.stderr)
        return 2

    violations = [(c, p) for c, p, ok in singletons if not ok]

    print("# singleton-lint — 파기된 싱글턴이 static에 남는가\n")
    print(f"검사 대상: MonoBehaviour 싱글턴 {len(singletons)}개")
    if not violations:
        print("\n결과: **PASS** (위반 0건)")
        return 0

    print(f"\n결과: **FAIL** — {len(violations)}건\n")
    print("| 클래스 | 파일 |")
    print("|---|---|")
    for cls, path in violations:
        print(f"| {cls} | `{path}` |")
    print("\n고치는 법: `OnDestroy`에 `if (ReferenceEquals(Instance, this)) Instance = null;`")
    print("안 그러면 `Instance != null`과 `Instance?.`가 서로 다른 답을 낸다(위 모듈 주석 참조).")
    return 1


if __name__ == "__main__":
    sys.exit(main())
