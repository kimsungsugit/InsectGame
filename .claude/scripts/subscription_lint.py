"""subscription-lint — UI 루트 토글로 소실되는 이벤트 구독 검출.

`OpeningReplayCoordinator`가 오프닝 다시보기 중 `playUiRoot.SetActive(false/true)`로
UI 루트를 통째로 껐다 켠다. 그래서 UI 루트 아래 컴포넌트가 `OnDisable`에서 `-=`로
해지한 구독을 `OnEnable`에서 되살리지 않으면, **다시보기 한 번에 그 기능이 영구히 죽는다.**

실제로 세 번 났다:
  - HospitalUI       : InsectUpdated 해지만 있고 재구독 없음 (2026-07-19 audit, 후속 수정)
  - BattleScreenUI   : OnEnable이 빈 메서드 → 다시보기 후 **배틀 화면이 안 열림** (2026-08-03)
  - RaidBattleUI     : 같은 형태 (2026-08-03)
  - RegionMapUI      : OnEnable 자체가 없음 → 레이드 보스 마커 소실 (2026-08-03)

검사 대상은 `PlaySceneBootstrap`이 `EnsureComponent<T>("UI/...")`로 만드는 컴포넌트다 —
그것들이 UI 루트의 자식이라 토글에 휘말린다.

exit 0 = 통과, 1 = 위반, 2 = 파싱 실패(구조가 바뀌었으니 파서부터 확인할 것).
"""
import glob
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BOOTSTRAP = os.path.join(ROOT, "Assets/Scripts/Core/PlaySceneBootstrap.cs")

# 해지해도 무해한 이벤트 — 재구독이 없어도 기능이 죽지 않는 경우만 여기 적는다.
# 늘릴 때는 "왜 무해한가"를 반드시 함께 남길 것.
EXEMPT = {
    # (클래스, "대상.핸들러")
}


def method_body(src, name):
    """void <name>() { ... } 본문을 중괄호 깊이로 떠낸다. 없으면 None."""
    m = re.search(r"\bvoid\s+" + name + r"\s*\(\s*\)\s*\{", src)
    if not m:
        return None
    depth, i = 1, m.end()
    while i < len(src) and depth:
        if src[i] == "{":
            depth += 1
        elif src[i] == "}":
            depth -= 1
        i += 1
    return src[m.end():i]


def ui_root_components():
    """Bootstrap이 "UI/..." 경로로 만드는 컴포넌트 이름 집합."""
    if not os.path.exists(BOOTSTRAP):
        print(f"ERROR: {BOOTSTRAP} 없음 — 경로가 바뀌었는지 확인", file=sys.stderr)
        sys.exit(2)
    src = io.open(BOOTSTRAP, encoding="utf-8").read()
    names = {m.group(1).split(".")[-1]
             for m in re.finditer(r'EnsureComponent<([\w\.]+)>\("UI/', src)}
    if not names:
        print("ERROR: UI 루트 컴포넌트를 하나도 못 찾음 — 정규식이 낡았다", file=sys.stderr)
        sys.exit(2)
    return names


def main():
    targets = ui_root_components()
    violations = []

    for path in glob.glob(os.path.join(ROOT, "Assets/Scripts/**/*.cs"), recursive=True):
        cls = os.path.basename(path)[:-3]
        if cls not in targets:
            continue

        src = io.open(path, encoding="utf-8").read()
        # 주석 제거 — 주석 처리된 `+=`를 재구독으로 오인하면 검사 자체가 무의미해진다.
        src = re.sub(r"//.*", "", src)

        disable = method_body(src, "OnDisable")
        if not disable:
            continue
        unsubscribed = set(re.findall(r"(\w+)\s*-=\s*(\w+)", disable))
        if not unsubscribed:
            continue

        # OnEnable 본문 + 그 안에서 인자 없이 부르는 메서드 본문까지 훑는다
        # (Subscribe() 같은 헬퍼로 빼는 게 이 저장소의 관례라 한 겹 따라간다).
        enable = method_body(src, "OnEnable") or ""
        extra = ""
        for called in re.findall(r"\b(\w+)\s*\(\s*\)\s*;", enable):
            body = method_body(src, called)
            if body:
                extra += body
        subscribed = set(re.findall(r"(\w+)\s*\+=\s*(\w+)", enable + extra))

        for owner, handler in sorted(unsubscribed - subscribed):
            if (cls, f"{owner}.{handler}") in EXEMPT:
                continue
            violations.append((cls, owner, handler, "OnEnable" if enable else "OnEnable 없음"))

    print("# subscription-lint — UI 루트 토글 시 구독 소실 검사\n")
    print(f"검사 대상: UI 루트 하위 컴포넌트 {len(targets)}개")
    if not violations:
        print("\n결과: **PASS** (위반 0건)")
        return 0

    print(f"\n결과: **FAIL** — {len(violations)}건\n")
    print("| 클래스 | 해지했으나 재구독 없음 | 상태 |")
    print("|---|---|---|")
    for cls, owner, handler, state in violations:
        print(f"| {cls} | `{owner} -= {handler}` | {state} |")
    print("\n고치는 법: 구독을 `Subscribe___()` 메서드로 빼고 AutoWire와 OnEnable이 함께 부른다.")
    print("`-=` 뒤 `+=` 형태면 중복 구독이 되지 않는다.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
