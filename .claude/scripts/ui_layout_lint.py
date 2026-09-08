"""UI 세로 마진 규칙 검사. 패널이 세이프에어리어 + 세로 마진 밖으로 나가는 배치를 잡는다.

왜 있나
-------
`UIScale`에는 가로 여백용 `ContentWidth`만 있고 세로 대응물이 없었다. 그래서 30여 개
OnGUI 화면이 세로 배치를 제각각 손으로 계산했고, 그 중 다수가

  ① 절대 높이 하드코딩      `panelH = 1000f; panelY = 24f;`
  ② 세이프 무시 중앙정렬    `(VirtualScreenHeight - panelH) * 0.5f`
  ③ 마진 없는 세이프 앵커   `VirtualSafeTop + 20f`

였다. 가로 캔버스의 `VirtualScreenHeight`는 정확히 1080이라(스케일이 Min(sx,sy))
①은 여백이 수십 px뿐이고, 노치 인셋이 들어오면 그대로 잘린다.

정답은 `UISafeLayout` 하나뿐이다. 이 검사기는 하네스를 우회한 배치를 막는다.

무엇을 잡나
-----------
세로축만 본다. 가로 배치(`VirtualScreenWidth - panelW`)는 대상이 아니다 — 잘림은
세로에서 나고, 가로 마진은 기존 24px 그대로 두기로 했다.

라인에 `UISafeLayout`이 있으면 하네스를 거친 것으로 보고 넘어간다.

종료 코드: 0 정상 / 1 위반 / 2 검사기 고장(스캔 대상 0건)
"""
import glob
import io
import os
import re
import sys

try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass

ROOT = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()

# 하네스 자신과, 세로 마진을 적용하면 오히려 해로운 파일.
EXEMPT_FILES = {
    "UISafeLayout.cs",    # 하네스 본체
    "UIScale.cs",         # 가상 좌표계 변환
    "SafeArea.cs",        # 인셋 원본
    "SafeAreaPanel.cs",   # uGUI용 세이프에어리어
    "FieldHudInput.cs",   # 터치 좌표 변환(배치 아님)
    # 조이스틱은 '화면 어디를 눌러 시작할 수 있나'를 정하는 입력 데드존이다.
    # 마진을 주면 조작 가능 영역이 좁아진다 — 잘림 문제와 무관.
    "VirtualJoystickUI.cs",
    # 오프닝은 shortSide 비율(16~28px) 자체 마진 체계를 갖고 있고
    # CalculateSkipButtonRect가 OpeningSequenceTests로 고정돼 있다.
    "OpeningSceneController.cs",
}

RULES = [
    (
        "세이프 무시 세로 중앙정렬",
        re.compile(r"\(\s*(?:UIScale\.)?(?:VirtualScreenHeight|Screen\.height)\s*-[^;]*?\)\s*[*/]\s*(?:0\.5f|2f)"),
        "UISafeLayout.CenteredPanel(w, h) 또는 UISafeLayout.CenteredY(h)",
    ),
    (
        "세이프 무시 세로 앵커",
        re.compile(r"float\s+\w*[yY]\s*=[^;]*?(?:VirtualScreenHeight|Screen\.height)\s*-"),
        "UISafeLayout.BottomY(h) / BottomPanel(w, h)",
    ),
    (
        "마진 없는 상단 앵커",
        re.compile(r"(?:UIScale\.)?(?:VirtualSafeTop|SafeArea\.Top)\s*\+"),
        "UISafeLayout.ContentTop",
    ),
    (
        "수동 가용 높이 계산",
        re.compile(r"(?:VirtualScreenHeight|Screen\.height)\s*-\s*(?:UIScale\.)?(?:VirtualSafeTop|SafeArea\.Top)"),
        "UISafeLayout.ContentHeight / ClampHeight(desired)",
    ),
]


# GUILayout.BeginArea 호출. Unity는 Area 중첩을 지원하지 않는데("Areas cannot be nested"),
# 이 저장소의 화면들은 OnGUI가 패널 영역을 열고 Draw___ 헬퍼가 그 안에서 또 여는 형태로
# 어긋났다 — 호출이 서로 다른 메서드에 흩어져 있어 라인 단위 규칙으로는 잡히지 않는다.
AREA_CALL = re.compile(r"GUILayout\.BeginArea\s*\(")

# 한 파일에서 Area를 순차로(열고 닫고, 또 열고 닫고) 두 번 쓰는 건 합법이다.
# 그런 파일이 생기면 여기에 근거와 함께 올린다 — 현재는 없다.
NESTED_AREA_EXEMPT = set()


def cs_files():
    out = []
    for pat in ("Assets/Scripts/**/*.cs", "Assets/Editor/**/*.cs"):
        out += glob.glob(os.path.join(ROOT, pat), recursive=True)
    return sorted(p for p in out if os.path.basename(p) not in EXEMPT_FILES)


def scan_nested_area(path):
    """
    한 파일의 GUILayout.BeginArea 호출 줄 목록 — 둘 이상이면 중첩 의심으로 돌려준다.

    2026-08-08에 실제로 두 화면을 동시에 못 쓰게 만든 결함이다. 같은 커밋이
    CashShopUI와 SocialPvpUI에 같은 구조를 심었고, 증상은 **탭 버튼은 그려지는데 그 아래
    내용이 통째로 안 나오는 것**이었다. 컴파일도 되고 예외도 없어서 조용히 지나간다.
    """
    if os.path.basename(path) in NESTED_AREA_EXEMPT:
        return []
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            lines = f.readlines()
    except OSError:
        return []

    hits = []
    for i, line in enumerate(lines, 1):
        code = line.split("//", 1)[0]
        if AREA_CALL.search(code):
            hits.append((i, line.strip()))
    return hits if len(hits) > 1 else []


def scan(path):
    """(라인번호, 규칙명, 힌트, 원문) 목록."""
    hits = []
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            lines = f.readlines()
    except OSError:
        return hits

    for i, line in enumerate(lines, 1):
        code = line.split("//", 1)[0]
        if not code.strip() or "UISafeLayout" in code:
            continue
        for name, pattern, hint in RULES:
            if pattern.search(code):
                hits.append((i, name, hint, line.strip()))
                break
    return hits


def main():
    files = cs_files()
    if not files:
        print("ui_layout_lint: 스캔 대상 .cs가 0건 — 경로 설정을 확인하세요.", file=sys.stderr)
        return 2

    violations = []
    nested = []
    for path in files:
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        for line_no, name, hint, text in scan(path):
            violations.append((rel, line_no, name, hint, text))
        area_hits = scan_nested_area(path)
        if area_hits:
            nested.append((rel, area_hits))

    if not violations and not nested:
        print(f"UI 레이아웃 마진: PASS ({len(files)}개 파일, 위반 0건)")
        return 0

    print(f"UI 레이아웃 마진: FAIL ({len(violations) + len(nested)}건)")
    for rel, line_no, name, hint, text in violations:
        print(f"  {rel}:{line_no}  [{name}]")
        print(f"      {text}")
        print(f"      → {hint}")
    for rel, area_hits in nested:
        lines_txt = ", ".join(str(n) for n, _ in area_hits)
        print(f"  {rel}  [Area 중첩 의심] GUILayout.BeginArea {len(area_hits)}회 — 줄 {lines_txt}")
        for _, text in area_hits:
            print(f"      {text}")
        print("      → 안쪽은 GUILayout.BeginScrollView / BeginVertical로. Area는 중첩할 수 없다")
    print()
    print("세로 배치는 UISafeLayout을 거쳐야 합니다 (.claude/rules/ui-layout.md).")
    return 1


if __name__ == "__main__":
    sys.exit(main())
