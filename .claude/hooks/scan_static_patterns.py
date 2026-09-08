"""PostToolUse hook: 4가지 정적 회귀 패턴을 변경 영역에서 검출.

직전 라운드들에서 반복 발견된 회귀:
1. OnGUI/Update 안 `new Color(...)` / `new Rect(...)` / `new GUIStyle` (매 프레임 할당)
2. OnGUI/Update 안 `FindFirstObjectByType<X>()` / `GetComponent<X>()` (캐싱 누락)
3. `EventName += Handler` 후 같은 클래스에 `EventName -= Handler` 짝 없음
4. 4000줄+ 모놀리스의 편집 영역 — method 분해 후보 제안

quick_compile_check.py와 동일 패턴 (UTF-8, JSON I/O, false positive 가드).
"""

import sys, json, re, os, io

try:
    sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
except Exception:
    pass

# 공용 C# 전처리기. 이 파일은 .codex/hooks/로도 무변환 복사되므로 __file__ 기준 상대경로가
# 양쪽에서 성립해야 한다 — .claude/hooks/ 와 .codex/hooks/ 둘 다 루트 2단계 아래다.
sys.path.insert(
    0,
    os.path.join(
        os.environ.get("CLAUDE_PROJECT_DIR")
        or os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
        ".claude", "scripts",
    ),
)
try:
    from cs_strip import strip_cs
except ImportError:
    # 전처리기를 못 찾으면 침묵. 원시 텍스트로 스캔하면 주석·문자열 안 패턴까지 잡는다.
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

raw = sys.stdin.read()
try:
    d = json.loads(raw)
except Exception:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)
# 최상위가 dict가 아니거나(null/배열) tool_input이 명시적 null이면 조용히 나간다.
# d.get("k", {})는 키가 **없을 때만** {}를 주지 실제 null에는 null을 준다.
if not isinstance(d, dict):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)
if not isinstance(d.get("tool_input"), dict):
    d["tool_input"] = {}
if not isinstance(d.get("tool_response"), dict):
    d["tool_response"] = {}


tool_input = d.get("tool_input", {})
file_path = (
    d.get("tool_response", {}).get("filePath", "")
    or tool_input.get("file_path", "")
)

if not file_path or not file_path.endswith(".cs"):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

if not os.path.exists(file_path):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

try:
    with open(file_path, "r", encoding="utf-8") as fh:
        text = fh.read()
except Exception:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 변경 영역 (new_string)을 추출 — false positive 차단 (파일 다른 곳의 패턴은 보고 안 함)
changed_text = tool_input.get("new_string", "") or tool_input.get("content", "")

warnings = []

# 주석/문자열 제거는 cs_strip이 소유한다. 예전엔 여기 사본이 있었고 주석을 문자열보다
# 먼저 지웠다 — "https://..."의 //가 주석으로 오인돼 닫는 따옴표가 사라지고, 뒤이은
# 문자열 규칙이 진짜 코드를 중괄호째 삼켰다. 그러면 extract_method_body의 중괄호 매칭이
# 어긋나 엉뚱한 메서드까지 "OnGUI 본문"으로 잡힌다(CharacterOutfitUI에서 InitStyles의
# new GUIStyle이 OnGUI 것으로 오보고됐다).
strip_comments_and_strings = strip_cs

# 메서드 본문 영역 추출 — `private void OnGUI()` ~ 매칭 닫는 `}`
def extract_method_body(text_cleaned, method_name_pattern):
    """method_name_pattern: 예 r'\\bOnGUI\\b' — 클린된 텍스트에서 매칭. 본문 문자열 반환."""
    matches = []
    for m in re.finditer(method_name_pattern + r"\s*\([^)]*\)", text_cleaned):
        start = m.end()
        # 다음 { 찾기
        brace_start = text_cleaned.find("{", start)
        if brace_start < 0:
            continue
        # 중첩 brace 매칭
        depth = 1
        i = brace_start + 1
        while i < len(text_cleaned) and depth > 0:
            c = text_cleaned[i]
            if c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
            i += 1
        if depth == 0:
            matches.append(text_cleaned[brace_start + 1: i - 1])
    return matches

cleaned_full = strip_comments_and_strings(text)
cleaned_changed = strip_comments_and_strings(changed_text)

# ── 1. OnGUI/Update 안 new Color/new Rect/new GUIStyle 매 프레임 할당 ──
# changed_text 안에 OnGUI 또는 Update 메서드가 있으면 그 본문에서 패턴 검색.
# 또는 changed_text 자체가 OnGUI 본문의 일부일 수도 있으니 전체 파일의 OnGUI/Update 본문도 검사.

# 매 프레임 **힙** 할당(= GC 압박)만 보고한다.
#
# new Color / new Rect / new Vector3 / new Quaternion 은 전부 struct라 스택에 잡히고
# GC를 유발하지 않는다. 옛 목록엔 Color와 Rect가 있었는데, audit 라운드들이 그걸
# 매번 "struct라 거짓양성"으로 기각해왔다(2026-07-17까지 6라운드 연속). 훅이 이미
# 알려진 오탐을 반복 보고하면 진짜 경고까지 무시당하므로 목록에서 뺀다.
#
# 아래는 전부 class다 — 매 프레임 생성하면 실제로 GC가 돈다.
# 한계: 문자열 보간 $"..."도 진짜 힙 할당이지만 cleaned_full이 문자열 리터럴을
# 통째로 지우므로 이 방식으로는 검출할 수 없다(라운드에서 사람이 잡아야 함).
perf_patterns = [
    (r"\bnew\s+GUIStyle\s*\(", "new GUIStyle"),
    (r"\bnew\s+GUIContent\s*\(", "new GUIContent"),
    (r"\bnew\s+Texture2D\s*\(", "new Texture2D"),
    (r"\bnew\s+Material\s*\(", "new Material"),
    (r"\bnew\s+(?:List|Dictionary|HashSet|Queue|Stack)\s*<", "new 컬렉션"),
]

# changed_text가 OnGUI/Update 메서드 본문 안에 위치하는지 추정.
# 정확한 위치 매칭 어렵지만 changed_text 안에 OnGUI/Update 시그니처가 포함되면
# 해당 method 본문이 새로 추가됐을 가능성 큼.
# 또는 changed_text가 method 시그니처 없이 본문 일부만 변경 → 전체 파일 method 본문에서 검사.

frame_methods = ["OnGUI", "Update", "LateUpdate", "FixedUpdate"]


def null_guard_spans(body):
    """매 프레임 실행되지 않는 구간 목록 — 여기 있는 할당/조회는 경고 대상이 아니다.

    두 종류를 덮는다:

    1. `if (x == null) { ... }` — lazy 캐싱. 이 훅이 **권장하는 바로 그 패턴**인데
       정작 그 안의 `new GUIStyle`을 "매 프레임 할당"으로 경고하고 있었다. 프레임 메서드
       안 매칭 30건 중 21건(70%)이 이 가드 뒤였다 — RegionManager는 주석에 "매 프레임
       GameObject.Find 회귀 차단"이라 써둔 자리에서 발화했다. 가드 안은 첫 호출 1회만 돈다.

    2. `if (GUI.Button(...)) { ... }` — IMGUI 클릭 핸들러. OnGUI 안에 있지만 **클릭한
       프레임에만** 실행된다. (지금은 삭제된) CharacterViewerUI가 버튼 콜백에서
       FindFirstObjectByType을 부르는 걸 매 프레임 조회로 오보고했다.
       GUILayout.Button / RepeatButton도 같다.

    3. `UIHelper.CachedStyle("key", () => { ... })` — 캐시 팩토리 람다. UIHelper.cs의
       CachedStyle은 캐시 미스일 때만 factory()를 부르므로 키당 1회 실행이다. 이 훅이
       경고 문구에서 "UIHelper.CachedStyle 패턴 참고"라고 **권장하면서** 정작 그 사용을
       매 프레임 할당으로 신고하고 있었다(CharacterOutfitUI).

    4. `#if DEVELOPMENT_BUILD || UNITY_EDITOR` — 출시 빌드에서 스트립되는 진단 코드.
       이 훅은 **출시 성능 회귀**를 잡으려고 있으므로 배포되지 않는 코드는 대상이 아니다.
       InsectSpawner의 OnGUI는 통째로 이 안에 있는 렌더 진단 오버레이다.
       (`Assets/Editor/`의 에디터 전용 스크립트와는 별개 얘기다 — 여기선 런타임 스크립트
       안에 조건부로 낀 진단 블록만 뺀다.)
    """
    guard_re = re.compile(
        r"==\s*null|(?:GUI|GUILayout)\.(?:Button|RepeatButton)\s*\("
    )
    spans = []
    for m in re.finditer(r"\bif\s*\(", body):
        # 조건절은 중첩 괄호를 가질 수 있다: if (GUI.Button(new Rect(...), "x")).
        # 깊이로 읽어야 닫는 괄호를 정확히 찾는다.
        i, depth = m.end(), 1
        while i < len(body) and depth:
            if body[i] == "(":
                depth += 1
            elif body[i] == ")":
                depth -= 1
            i += 1
        if depth:
            continue
        if not guard_re.search(body[m.end():i - 1]):
            continue
        j = i
        while j < len(body) and body[j].isspace():
            j += 1
        if j < len(body) and body[j] == "{":
            d, k = 0, j
            while k < len(body):
                if body[k] == "{":
                    d += 1
                elif body[k] == "}":
                    d -= 1
                    if d == 0:
                        break
                k += 1
            spans.append((m.start(), min(k + 1, len(body))))
        else:
            k = body.find(";", j)
            spans.append((m.start(), (k + 1) if k >= 0 else len(body)))
    # `cache ??= new GUIStyle(...)` — lazy 캐싱의 축약형. 문장 끝까지 가드로 본다.
    for m in re.finditer(r"\?\?=", body):
        k = body.find(";", m.end())
        spans.append((m.start(), (k + 1) if k >= 0 else len(body)))
    # `UIHelper.CachedStyle("key", () => { ... })` — 팩토리 람다는 캐시 미스 때만 돈다.
    # `() =>` 부터 그 블록의 닫는 중괄호까지가 가드 구간.
    for m in re.finditer(r"Cached\w*\s*\([^;{]*?\(\s*\)\s*=>\s*\{", body):
        d, k = 0, body.rfind("{", m.start(), m.end())
        while k < len(body):
            if body[k] == "{":
                d += 1
            elif body[k] == "}":
                d -= 1
                if d == 0:
                    break
            k += 1
        spans.append((m.start(), min(k + 1, len(body))))
    # `#if DEVELOPMENT_BUILD || UNITY_EDITOR ... #endif` — 출시 빌드에서 스트립되는 진단 코드.
    for m in re.finditer(r"^[^\S\n]*#if\b[^\n]*\b(?:DEVELOPMENT_BUILD|UNITY_EDITOR)\b", body, re.M):
        e = re.search(r"^[^\S\n]*#endif\b", body[m.end():], re.M)
        spans.append((m.start(), m.end() + e.end() if e else len(body)))
    return spans


def unguarded_hit(pat, body):
    """가드 밖에서 pat이 매칭되면 True. 가드 안에만 있으면 False."""
    spans = null_guard_spans(body)
    return any(
        not any(s <= m.start() < e for s, e in spans) for m in re.finditer(pat, body)
    )


# 전략: 전체 파일의 frame method 본문을 추출 후, 그 본문에 changed_text가 포함되거나 일부 겹치면 검사
# 단순화: 전체 파일의 frame method 본문에서 패턴 검색하되, changed_text에도 같은 패턴이 있어야 보고
perf_warnings_set = set()
for method in frame_methods:
    bodies = extract_method_body(cleaned_full, r"\b" + method + r"\b")
    for body in bodies:
        for pat, label in perf_patterns:
            if unguarded_hit(pat, body):
                # changed_text에도 같은 패턴이 있는지 확인 (false positive 차단)
                if unguarded_hit(pat, cleaned_changed):
                    perf_warnings_set.add((method, label))

for method, label in sorted(perf_warnings_set):
    warnings.append(
        f"{method}() 안 매 프레임 `{label}(...)` 할당 — 필드 캐싱 권장 "
        f"(UIHelper.CachedStyle 또는 static readonly 패턴 참고)"
    )

# ── 2. OnGUI/Update 안 FindFirstObjectByType/GetComponent ──
# GetComponent<T>가 캐싱되지 않고 매 프레임 호출되면 비싼 작업.
# 단, GetComponent(...)는 Awake/Start에서 1회 호출이 정상 패턴. frame method 본문에서만 검사.

lookup_patterns = [
    (r"\bFindFirstObjectByType\s*<", "FindFirstObjectByType"),
    (r"\bFindObjectOfType\s*<", "FindObjectOfType"),
    (r"\bGameObject\.Find\s*\(", "GameObject.Find"),
    # GetComponent는 frame method에 진짜 캐싱 안 한 경우만 — 너무 많은 false positive 우려로 일단 제외
]

lookup_warnings_set = set()
for method in frame_methods:
    bodies = extract_method_body(cleaned_full, r"\b" + method + r"\b")
    for body in bodies:
        for pat, label in lookup_patterns:
            # 할당 패턴과 같은 이유로 null 가드 안은 제외 — 그게 권장하는 캐싱이다.
            if unguarded_hit(pat, body) and unguarded_hit(pat, cleaned_changed):
                lookup_warnings_set.add((method, label))

for method, label in sorted(lookup_warnings_set):
    warnings.append(
        f"{method}() 안 `{label}(...)` — 캐싱 권장 (AutoWire 또는 Awake/Start 1회 호출 패턴)"
    )

# ── 3. 이벤트 += 후 -= 짝 누락 ──
# changed_text 안에 `EventName += HandlerName` 발견 시 전체 파일에서 `EventName -= HandlerName` 검색.
# 람다 식 `EventName += () =>` 또는 `EventName += (x) =>`는 해제 불가 — 별도 경고.

subscribe_re = re.compile(
    r"(\w+(?:\.\w+)*)\s*\+=\s*(?:\([^)]*\)\s*=>|\w+(?:\.\w+)*)"
)
lambda_subscribe_re = re.compile(
    r"(\w+(?:\.\w+)*)\s*\+=\s*\([^)]*\)\s*=>"
)
method_subscribe_re = re.compile(
    r"(\w+(?:\.\w+)*)\s*\+=\s*(\w+(?:\.\w+)*)\s*;"
)

# changed_text 안 + 패턴
for m in method_subscribe_re.finditer(cleaned_changed):
    event_name = m.group(1)
    handler_name = m.group(2)
    # 매우 흔한 false positive 화이트리스트 (단일 문자 변수)
    if event_name in ("a", "b", "x", "y", "i", "j", "n"):
        continue
    # 좌변 first segment가 camelCase 시작이면 산술 누적으로 간주 (event는 PascalCase 컨벤션).
    # 예: `totalDmg += actual;`, `count += 1;`은 이벤트 아님.
    # `obj.EventName += ...`처럼 점 접근이면 마지막 segment가 PascalCase여야 이벤트.
    last_segment = event_name.split(".")[-1]
    if not last_segment[0].isupper():
        continue
    # 우변 handler가 PascalCase 메서드 이름이어야 이벤트 핸들러 패턴
    last_handler = handler_name.split(".")[-1]
    if not last_handler[0].isupper():
        continue
    # 짝 -= 검색 (전체 파일).
    # 수신자까지 통째로 매칭하면 안 된다 — 구독과 해제의 수신자 표기가 다른 게 정상이다.
    # 예: PlayerMovement는 `mgr.OutfitChanged += X`로 구독하고
    #     `CharacterOutfitManager.Instance.OutfitChanged -= X`로 해제한다.
    #     둘은 같은 이벤트인데 문자열이 달라 전수 3건이 전부 오탐이었다.
    # 이벤트·핸들러 모두 마지막 세그먼트로 비교한다.
    unsub_pat = (
        r"(?:^|[^\w.])(?:[\w\.\[\]\(\)]+\.)?"
        + re.escape(last_segment)
        + r"\s*-=\s*(?:[\w\.]+\.)?"
        + re.escape(last_handler)
        + r"\b"
    )
    if not re.search(unsub_pat, cleaned_full, re.MULTILINE):
        warnings.append(
            f"이벤트 구독 `{event_name} += {handler_name}` 짝 `-=` 누락 — "
            f"OnDisable/OnDestroy에서 해제 권장 (메모리 누수 + 죽은 ref 호출 방지)"
        )
        break  # 1건만 보고

# 람다 구독 (해제 불가)
for m in lambda_subscribe_re.finditer(cleaned_changed):
    event_name = m.group(1)
    if event_name in ("a", "b", "x", "y", "i", "j", "n"):
        continue
    warnings.append(
        f"람다 이벤트 구독 `{event_name} += (...) => ...` — 해제 불가. "
        f"메서드 참조로 변경 후 OnDisable에서 -=로 해제 권장"
    )
    break

# ── 4. 모놀리스 method 분해 제안 ──
# 2500줄+ 파일에서 편집된 method 본문이 200줄+이면 분해 후보로 제안.
# 임계값 2500은 모놀리스 3종(RaidBattleUI/BattleScreenUI/PlaySceneBootstrap)을
# 모두 포함하고 4위 파일(~2000줄)은 제외하는 경계다. 옛 4000은 Bootstrap
# 하나만 걸러 warn_monolith가 규정한 3종과 어긋나 있었다.
line_count = text.count("\n")
if line_count > 2500:
    # 시그니처 탐색과 중괄호 깊이 계산 모두 cleaned_full(주석·문자열 제거본)로 한다.
    # raw text로 하면 주석이나 $"{value}" 보간 문자열 안의 { } 가 깊이를 깨뜨리고,
    # 주석 속 유령 메서드 시그니처까지 매칭된다.
    method_sig_re = re.compile(
        r"(?:public|private|protected|internal|static|\s)+(?:void|IEnumerator|[\w<>]+)\s+"
        r"(\w+)\s*\([^)]*\)\s*\{"
    )
    long_methods = []
    for m in method_sig_re.finditer(cleaned_full):
        method_name = m.group(1)
        if method_name not in changed_text:
            continue  # 편집과 무관한 메서드는 비싼 중괄호 탐색 전에 거른다
        start = m.end()
        depth = 1
        i = start
        while i < len(cleaned_full) and depth > 0:
            c = cleaned_full[i]
            if c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
            i += 1
        body_lines = cleaned_full[start:i].count("\n")
        if body_lines > 200:
            long_methods.append((method_name, body_lines))

    if long_methods:
        sample = sorted(long_methods, key=lambda x: -x[1])[:2]
        names = ", ".join(f"{n}({l}줄)" for n, l in sample)
        warnings.append(
            f"모놀리스 {line_count}줄 — 편집한 메서드 {names}은 별도 파일/클래스로 추출 후보. "
            f"Phase enum 또는 기능별 grouping 검토"
        )

if not warnings:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

short = file_path
try:
    root = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
    short = os.path.relpath(file_path, root).replace("\\", "/")
except Exception:
    pass

ctx = (
    f"STATIC-SCAN: {short}\n"
    + "\n".join(f"  - {w}" for w in warnings)
    + "\n(정적 정규식 점검 — 오탐 가능. 검토 후 진행.)"
)

print(json.dumps({
    "hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": ctx
    }
}))
