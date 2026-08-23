"""text-fit-lint — 고정 상자에 안 들어가는 IMGUI 라벨 검출.

IMGUI `GUI.Label`은 넘치는 글자를 **조용히 자른다.** 예외도 로그도 없다. 두 방향으로 난다:

  세로 — `wordWrap = true` 스타일을 고정 높이 Rect에 그리면, 줄바꿈이 일어나는 순간
         넘치는 줄이 통째로 사라진다.
  가로 — `wordWrap = false`면 줄바꿈이 없으니 높이는 늘 한 줄이고, 대신 넘치는 글자가
         가로로 잘린다(가운데 정렬이면 앞뒤가 같이 잘려 더 나쁘다).

한국어는 같은 뜻을 더 긴 글자수로 쓰고 모바일에선 기준 폰트가 커져서, 데스크톱 Game View에서
멀쩡하던 라벨이 기기에서 잘린다. 2026-08-03에 실제로 7곳이 그렇게 잘리고 있었다
(도감 설명 144px / 아이템 설명 40px에 34pt / 보유 곤충 설명 84px / NPC 대사 88px /
팀 슬롯·픽커 이름 / 가이드 배너).

고치는 법은 하나다 — `UIHelper.LabelFit(rect, text, style)`. 상자는 그대로 두고 글자를
줄여 맞춘다(상자를 키우면 그 아래 요소가 전부 밀린다). 규칙 문서는 `rules/ui-layout.md`.

무엇을 잡나
-----------
핵심 조건은 **"텍스트 길이를 코드가 모르는데 상자가 고정"** 하나다.

처음엔 "wordWrap=true인데 높이 < fontSize×1.6(두 줄 불가)"로 잡아 봤더니 **247건**이 나왔다.
한 줄짜리 라벨은 원래 높이가 fontSize의 1.2배쯤이므로, 그 규칙은 **정상적인 한 줄 라벨을
전부** 잡는다. 오탐이 그만큼이면 아무도 안 보는 검사가 된다.

그래서 텍스트 출처를 먼저 좁힌다 — 데이터가 길이를 정하는 것만 본다(UNBOUNDED_TEXT):

  ① 무한정 텍스트 + wordWrap=true + 높이 < fontSize × 1.6  → 두 줄째가 잘린다
  ② 무한정 텍스트 + wordWrap=false                          → 폭 초과 시 가로로 잘린다

`$"Lv {n}"` 같은 보간은 길이가 사실상 묶여 있어 제외한다. 문자열 리터럴도 제외한다
(코드에 길이가 박혀 있어 사람이 이미 맞춰본 것).

exit 0 = 통과, 1 = 위반, 2 = 파싱 실패(구조가 바뀌었으니 파서부터 확인할 것).
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

ROOT = os.environ.get("CLAUDE_PROJECT_DIR") or os.path.dirname(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
)

# 두 줄이 들어가려면 줄간격까지 쳐서 대략 이 배수는 필요하다.
TWO_LINE_RATIO = 1.6

# 길이를 **데이터가** 정하는 텍스트. 여기 없는 건 코드가 길이를 알거나 사실상 묶여 있다.
# 늘릴 때는 "왜 무한정인가"를 함께 남길 것 — 넓히면 곧바로 오탐이 는다.
#   description  : 종·리전·아이템 설명 프로즈. 가장 길고 가장 잘 잘린다
#   displayName  : 종·아이템 이름. 데이터가 정하고 한국어라 폭을 많이 먹는다
#   lines[       : NPC/스토리 대사 배열
#   GetOwnedDisplayName / activeGuidedText : 런타임 조립 표시명·가이드 문구
# 길이를 데이터가 정하는 텍스트 출처. **넓히면 안 된다** — 한 줄 라벨은 원래 높이가
# fontSize의 1.2배쯤이라 출처를 안 좁히면 정상 라벨까지 247건이 걸린다(모듈 주석 참조).
#
# LastResultText: NPC 대결 결과 토스트. 상대 이름 + 보상 아이템 + 거점 이름이 붙어 길이가
# 데이터로 정해지는데 800×44 고정 상자였다 — 2026-08-23 오염 거점 라운드에서 문구가
# 길어지며 손으로 잡았다. 지금은 LabelFit이라 안 잡히지만 GUI.Label로 되돌아가면 다시 잡는다.
UNBOUNDED_TEXT = re.compile(
    r"\.description\b|\.displayName\b|\blines\s*\[|GetOwnedDisplayName\s*\(|activeGuidedText\b"
    r"|\.LastResultText\b"
)

# `이름 = new GUIStyle(...) { ... }` 또는 `이름 = Label(24, ...)` 형태에서 스타일 속성을 읽는다.
STYLE_ASSIGN = re.compile(r"\b(\w+)\s*=\s*(?:new\s+GUIStyle\s*\(|(\w+)\s*\()", re.S)

# 면제 — 이 파일들은 자체 맞춤 로직이 있어 LabelFit이 오히려 방해다.
EXEMPT_FILES = {
    # OpeningSceneController: CalcSize로 폭을 직접 재서 폰트를 맞추는 FitFontSize를 이미 갖고 있고,
    # 그 결과가 OpeningSequenceTests로 고정돼 있다.
    "OpeningSceneController.cs",
}


def style_table(src):
    """{스타일 변수명: (fontSize, wordWrap)} — 선언 블록에서 읽는다.

    두 가지 형태를 덮는다:
      inline : x = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true }
      factory: x = Label(24, FontStyle.Bold, TextAnchor.MiddleLeft, col)   ← RegionMapUI 관례
    """
    table = {}

    # 팩토리 함수가 wordWrap을 고정하는지 파악(RegionMapUI.Label은 항상 false).
    factory_wrap = {}
    for m in re.finditer(r"static\s+GUIStyle\s+(\w+)\s*\([^)]*\)\s*\{(.{0,600}?)\n\s*\}", src, re.S):
        body = m.group(2)
        if "wordWrap = false" in body or "wordWrap=false" in body:
            factory_wrap[m.group(1)] = False
        elif "wordWrap = true" in body or "wordWrap=true" in body:
            factory_wrap[m.group(1)] = True

    for m in re.finditer(r"\b(\w+)\s*=\s*new\s+GUIStyle\s*\([^;]*?;", src, re.S):
        blob = m.group(0)
        fs = re.search(r"fontSize\s*=\s*(\d+)", blob)
        wrap = None
        if "wordWrap = true" in blob or "wordWrap=true" in blob:
            wrap = True
        elif "wordWrap = false" in blob or "wordWrap=false" in blob:
            wrap = False
        else:
            # GUI.skin.label 기본은 wordWrap=true, GUI.skin.button은 false.
            wrap = True if "GUI.skin.label" in blob else None
        table[m.group(1)] = (int(fs.group(1)) if fs else 0, wrap)

    for m in re.finditer(r"\b(\w+)\s*=\s*(\w+)\s*\(\s*(\d+)\s*,", src):
        fname = m.group(2)
        if fname in factory_wrap:
            table[m.group(1)] = (int(m.group(3)), factory_wrap[fname])

    return table


# GUI.Label(new Rect(...), <text>, <style>)  — 중첩 괄호 때문에 수동으로 인자를 가른다.
LABEL_HEAD = re.compile(r"GUI\.Label\s*\(\s*new\s+Rect\s*\(")


def label_calls(src):
    """(줄번호, rect인자, 텍스트인자, 스타일명) 목록."""
    out = []
    for m in LABEL_HEAD.finditer(src):
        i = m.end()
        depth = 1
        while i < len(src) and depth:                 # new Rect( ... ) 닫기
            if src[i] == "(":
                depth += 1
            elif src[i] == ")":
                depth -= 1
            i += 1
        rect_arg = src[m.end():i - 1]

        # 남은 인자들을 최상위 콤마로 가른다.
        j, depth, args, cur = i, 0, [], ""
        while j < len(src):
            c = src[j]
            if c in "([{":
                depth += 1
            elif c in ")]}":
                if depth == 0:
                    break
                depth -= 1
            if c == "," and depth == 0:
                args.append(cur)
                cur = ""
            else:
                cur += c
            j += 1
        args.append(cur)
        args = [a.strip() for a in args if a.strip()]
        if len(args) < 2:
            continue
        out.append((src[:m.start()].count("\n") + 1, rect_arg, args[0], args[1]))
    return out


def last_literal(rect_arg):
    """new Rect(x, y, w, h)의 h가 리터럴이면 그 값, 아니면 None."""
    depth, parts, cur = 0, [], ""
    for c in rect_arg:
        if c in "([{":
            depth += 1
        elif c in ")]}":
            depth -= 1
        if c == "," and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += c
    parts.append(cur)
    if len(parts) < 4:
        return None
    m = re.fullmatch(r"\s*(\d+(?:\.\d+)?)f?\s*", parts[3])
    return float(m.group(1)) if m else None


def main():
    files = sorted(glob.glob(os.path.join(ROOT, "Assets/Scripts/**/*.cs"), recursive=True))
    if not files:
        print("ERROR: Assets/Scripts에서 .cs를 못 찾음 — 경로가 바뀌었는가", file=sys.stderr)
        return 2

    scanned, violations = 0, []
    for path in files:
        name = os.path.basename(path)
        if name in EXEMPT_FILES:
            continue
        src = io.open(path, encoding="utf-8", errors="replace").read()
        if "GUI.Label" not in src:
            continue
        src_nc = re.sub(r"//.*", "", src)          # 주석 안 예시 코드를 잡지 않는다
        styles = style_table(src_nc)
        rel = os.path.relpath(path, ROOT).replace(os.sep, "/")

        for line, rect_arg, text_arg, style_arg in label_calls(src_nc):
            scanned += 1
            info = styles.get(style_arg)
            if not info:
                continue
            font, wrap = info
            # 텍스트 길이를 데이터가 정하는 경우만 본다 — 이 조건이 오탐을 가른다.
            if font <= 0 or not UNBOUNDED_TEXT.search(text_arg):
                continue

            if wrap is True:
                h = last_literal(rect_arg)
                if h is not None and h < font * TWO_LINE_RATIO:
                    violations.append((rel, line, style_arg,
                                       f"래핑(fontSize {font}) + 데이터 텍스트인데 높이 {h:g} — 둘째 줄이 잘린다"))
            elif wrap is False:
                violations.append((rel, line, style_arg,
                                   f"wordWrap=false(fontSize {font}) + 데이터 텍스트 — 폭 초과 시 가로로 잘린다"))

    print("# text-fit-lint — 고정 상자에 안 들어가는 라벨 검사\n")
    print(f"검사한 GUI.Label 호출: {scanned}개")
    if not violations:
        print("\n결과: **PASS** (위반 0건)")
        return 0

    print(f"\n결과: **FAIL** — {len(violations)}건\n")
    print("| 파일:줄 | 스타일 | 문제 |")
    print("|---|---|---|")
    for rel, line, style, why in violations:
        print(f"| {rel}:{line} | `{style}` | {why} |")
    print("\n고치는 법: `GUI.Label(rect, text, style)` → `UIHelper.LabelFit(rect, text, style)`")
    print("상자를 키우는 쪽이 맞는 자리면 `UIHelper.MeasureWrappedHeight`로 높이를 받아 레이아웃을 늘린다.")
    print("배경: `.claude/rules/ui-layout.md`")
    return 1


if __name__ == "__main__":
    sys.exit(main())
