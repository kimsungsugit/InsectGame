"""PostToolUse hook: 변경된 .cs 파일에 대한 regex 기반 빠른 컴파일 점검.

Unity Editor 호출 없이 정적 패턴으로 흔한 컴파일 에러를 감지한다. 100% 정확하지 않지만
빠른 피드백 제공. 직전 라운드 `subAreaRespawnTimer` 미선언 사용 같은 케이스를 잡는다.

점검 항목:
1. 중괄호 매칭 (열림 vs 닫힘 카운트)
2. 미선언 식별자 사용 의심 (private/[SerializeField]/public 선언 없이 사용)
3. using 누락 (IEnumerator/List<T>/StringBuilder 등 사용 시)

의심이 명확하지 않으면 침묵. 노이즈 방지."""

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
    # 전처리기를 못 찾으면 침묵한다. 원시 텍스트로 검사하면 주석·문자열 안의 중괄호까지
    # 세어 오탐이 쏟아진다 — 조용한 오탐보다 검사를 거르는 편이 낫다.
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
    # 깨진 경로/존재하지 않음 — 침묵 (mojibake 노이즈 방지)
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

warnings = []

# C# 타입 토큰: 내장 타입 또는 대문자로 시작하는 사용자 타입(제네릭·배열 포함).
TYPE_TOKEN = (
    r"\b(?:var|int|uint|long|ulong|short|ushort|byte|sbyte|float|double|decimal|"
    r"bool|string|char|object|"
    r"[A-Z]\w*(?:<[^<>()]*>)?(?:\[\])?)"
)

try:
    with open(file_path, "r", encoding="utf-8") as fh:
        text = fh.read()
except Exception:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 1. 중괄호 매칭 — 문자열/주석/char literal 제외 후 카운트 (false positive 차단)
# 예전엔 여기서 주석을 문자열보다 먼저 지웠다. "https://..."의 //가 주석으로 오인돼
# 닫는 따옴표가 사라지고 뒤이은 문자열 규칙이 진짜 코드를 중괄호째 삼켰다.
# cs_strip이 단일 패스로 처리한다 — 먼저 열리는 쪽이 이긴다.
cleaned = strip_cs(text)

opens = cleaned.count("{")
closes = cleaned.count("}")
diff = abs(opens - closes)
if diff > 2:
    warnings.append(
        f"중괄호 불균형: {{ {opens}개 vs }} {closes}개 (차이 {diff}, 주석/문자열 제외 후)"
    )

# 2. 미선언 식별자 사용 의심
# 클래스 본문 안의 필드 선언 패턴 수집
declared = set()
# private/protected/public/internal + type + name 패턴 (배열, generic 포함)
field_re = re.compile(
    r"^\s*(?:\[[^\]]+\]\s*)*"  # attribute (e.g., [SerializeField])
    r"(?:public|private|protected|internal|static|readonly|const|"
    r"new|virtual|override|abstract|sealed|partial|extern|unsafe|volatile|\s)+"
    r"[\w<>\[\],\s\.\?]+?\s+"  # type
    r"(\w+)\s*[=;\(]",  # name followed by =, ;, or ( (method)
    re.MULTILINE
)
# 모든 매칭 — 메서드 이름도 포함되지만 무해 (사용 시 declared 안에 있으면 OK)
for m in field_re.finditer(text):
    declared.add(m.group(1))

# 로컬 변수 선언도 포함 (var x, int y, string s 등)
local_re = re.compile(
    r"\b(?:var|int|float|double|bool|string|long|short|byte|char|"
    r"void|object|decimal|uint|ulong|ushort|sbyte|"
    r"[A-Z]\w*(?:<[\w<>,\s\.\?]+>)?(?:\[\])?)\s+"
    r"(\w+)\s*[=;)]"
)
for m in local_re.finditer(text):
    declared.add(m.group(1))

# foreach 변수
foreach_re = re.compile(r"\bforeach\s*\(\s*\S+\s+(\w+)\s+in\b")
for m in foreach_re.finditer(text):
    declared.add(m.group(1))

# 파라미터 (대부분의 메서드 시그니처에서 잡힘)
param_re = re.compile(r"[,(]\s*(?:ref\s+|out\s+|in\s+|params\s+|this\s+)?\S+\s+(\w+)\s*[,)=]")
for m in param_re.finditer(text):
    declared.add(m.group(1))

# C# 키워드/내장 식별자/매우 흔한 Unity API는 화이트리스트 (검증 면제)
WHITELIST = {
    "true", "false", "null", "this", "base", "value", "var",
    "Time", "Mathf", "Vector2", "Vector3", "Vector4", "Quaternion", "Color",
    "GameObject", "Component", "Transform", "Camera", "Light", "Material",
    "Debug", "Input", "Screen", "Application", "Resources", "Object",
    "MonoBehaviour", "ScriptableObject", "PlayerPrefs", "Rect", "Random",
    "Texture", "Texture2D", "TextureFormat", "Shader", "GUI", "GUIStyle",
    "FontStyle", "TextAnchor", "Event", "EventType", "KeyCode",
    "Coroutine", "WaitForSeconds", "WaitForEndOfFrame", "WaitUntil",
    "Physics", "Collider", "Rigidbody", "MeshRenderer", "MeshFilter",
    "AudioSource", "AudioClip", "Animator", "Animation",
    "System", "UnityEngine", "Math", "Convert", "Activator",
    "Mathf", "DateTime", "TimeSpan", "Guid",
    "Encoding", "File", "Directory", "Path", "FileStream", "StreamReader",
    "StreamWriter", "BinaryReader", "BinaryWriter",
    # 흔한 namespace
    "InsectGame", "Core", "Data", "Battle", "Capture", "Dex", "Spawning", "UI",
}

# 식별자 사용 검사: 메서드 본문 안에서 increment/decrement 좌변 식별자 추출.
# `(?<![\.\w])` 가드로 멤버 접근 차단. 주석 안 ASCII art `// --- X ---`의 `X --` 매칭 차단을 위해
# cleaned text(주석/문자열 제거)에서 매칭한다.
used_assigns = set()
inc_re = re.compile(
    r"(?<![\.\w])([a-zA-Z_][a-zA-Z0-9_]*)\s*(?:\+=|-=|\*=|/=|\+\+|--)\s*"
)
for m in inc_re.finditer(cleaned):
    used_assigns.add(m.group(1))

# 미선언 의심 = used_assigns - declared - WHITELIST - method 호출 결과(arr.Count 등 .뒤)
suspicious = []
for name in used_assigns:
    if name in declared:
        continue
    if name in WHITELIST:
        continue
    if name[0].isupper():
        # 대문자 시작은 보통 클래스/타입/상수 — 화이트리스트로 간주
        continue
    if len(name) <= 1:
        continue
    # 선언 문맥(타입 토큰 뒤 식별자)이 파일 어디에도 없으면 진짜 미선언.
    # 빈도 기반 가드(name_count >= 2)는 쓰지 않는다 — `timer += x`처럼 실제 미선언
    # 변수도 초기화·증감으로 2회 이상 등장하는 게 보통이라 탐지력이 0이 됐었다.
    # (declared 패턴이 못 잡는 List<GameObject>/Dictionary<K,V> 등은 아래 제네릭
    #  분기가 커버한다.)
    if re.compile(TYPE_TOKEN + r"\s+" + re.escape(name) + r"\b\s*(?:=|;|,|\)|\bin\b)").search(cleaned):
        continue
    # 다중 선언자: `int discovered = 0, captured = 0;`의 둘째 이후는 앞에 타입이 아니라
    # 콤마가 온다. 위 패턴만으로는 captured를 미선언으로 오탐한다(DexScreenUI.cs,
    # RegionMapUI.cs 등 8개 파일에서 실제 발화했다). 선언문 안에 있는지로 판정한다.
    # `[^;\n]*`가 세미콜론·줄바꿈을 못 넘으므로 다른 문장까지 번지지 않고,
    # name 뒤에 =/,/; 를 요구하므로 `Foo(captured)` 같은 단순 사용은 걸리지 않는다.
    if re.compile(
        r"^[^\S\n]*(?:\[[^\]]+\][^\S\n]*)*"
        r"(?:(?:public|private|protected|internal|static|readonly|const)\s+)*"
        + TYPE_TOKEN + r"\s+\w+[^;\n]*\b" + re.escape(name) + r"\b\s*(?:=|,|;)",
        re.MULTILINE,
    ).search(cleaned):
        continue
    suspicious.append(name)

if suspicious:
    # 최대 3개만 보고 (노이즈 방지)
    sample = sorted(suspicious)[:3]
    warnings.append(
        f"미선언 의심 식별자 사용: {', '.join(sample)} "
        f"(증감 연산자 좌변인데 클래스 필드 선언이 없는 듯 — 컴파일 에러 가능성)"
    )

# 3. using 누락 의심
needs_using = {
    "IEnumerator": "System.Collections",
    "IEnumerable": "System.Collections",
    "List<": "System.Collections.Generic",
    "Dictionary<": "System.Collections.Generic",
    "HashSet<": "System.Collections.Generic",
    "StringBuilder": "System.Text",
    "Encoding": "System.Text",
    "Stopwatch": "System.Diagnostics",
    "File.": "System.IO",
    "Path.": "System.IO",
    "Directory.": "System.IO",
    "Action<": "System",
    "Action ": "System",
    "Func<": "System",
    "Task<": "System.Threading.Tasks",
    "UnityWebRequest": "UnityEngine.Networking",
    "Image ": "UnityEngine.UI",
    "Button ": "UnityEngine.UI",
    "Text ": "UnityEngine.UI",
}
# using 선언 추출
using_re = re.compile(r"^\s*using\s+([\w.]+)\s*;", re.MULTILINE)
usings = set(using_re.findall(text))

for token, ns in needs_using.items():
    # 확실한 케이스만 본다. 나머지는 오탐 위험 대비 이득이 없다.
    if token not in ("IEnumerator", "List<", "Dictionary<", "HashSet<"):
        continue
    # using이 부분 매치 (System.Collections.Generic ⊃ System.Collections) 허용
    if any(u.startswith(ns) for u in usings):
        continue
    # cleaned를 본다 — 원시 text를 보면 주석·문자열 안의 토큰까지 센다.
    # 앞에 `.`이 붙은 완전 수식(System.Collections.Generic.List<>)은 using이 필요 없다.
    # 그걸 무시해서 RegionTerrainBuilder.cs·CharacterOutfitUI.cs가 오탐됐었다.
    if not re.search(r"(?<![.\w])" + re.escape(token), cleaned):
        continue
    warnings.append(f"using 누락 의심: {token} 사용 — `using {ns};` 필요")
    break  # 한 번에 1개만 보고

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
    f"QUICK-COMPILE-CHECK: {short}\n"
    + "\n".join(f"  - {w}" for w in warnings)
    + "\n(정적 점검이라 오탐 가능. Unity Editor 컴파일로 최종 확인 권장.)"
)

print(json.dumps({
    "hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": ctx
    }
}))
