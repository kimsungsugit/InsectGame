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

raw = sys.stdin.read()
try:
    d = json.loads(raw)
except Exception:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

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

try:
    with open(file_path, "r", encoding="utf-8") as fh:
        text = fh.read()
except Exception:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 1. 중괄호 매칭 — 문자열/주석/char literal 제외 후 카운트 (false positive 차단)
cleaned = text
# // 한 줄 주석 제거
cleaned = re.sub(r"//[^\n]*", "", cleaned)
# /* ... */ 블록 주석 제거
cleaned = re.sub(r"/\*[\s\S]*?\*/", "", cleaned)
# char literal '{', '}' 제거 (verbatim, interpolated 포함은 아님)
cleaned = re.sub(r"'(?:\\.|[^'\\])'", "", cleaned)
# string literal "..." 제거 (이스케이프 처리)
cleaned = re.sub(r'"(?:\\.|[^"\\])*"', "", cleaned)
# verbatim string @"..." 제거
cleaned = re.sub(r'@"(?:[^"]|"")*"', "", cleaned)

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
    # 식별자가 파일 안에 2회 이상 등장하면 어딘가 선언되어 있을 가능성 매우 높음 — false positive 차단.
    # (List<GameObject>, IEnumerator<T>, Dictionary<K,V> 등 declared 패턴이 못 잡는 케이스 보호.)
    name_count = len(re.findall(r"\b" + re.escape(name) + r"\b", text))
    if name_count >= 2:
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
    if token in text and ns not in usings:
        # System은 보통 빠져있어도 .NET implicit가 해결. 그러나 명시적 검증 위해 경고만 추가.
        # 다만 token이 주석/문자열에만 있을 수도 → 짧은 단순 검사: 코드 내 식별자 패턴인지
        # 100% 정확 어려움. P2 수준이므로 단일 사용처로 false positive 줄임.
        # 일단 확실한 케이스(IEnumerator, List<)만 보고
        if token in ("IEnumerator", "List<", "Dictionary<", "HashSet<"):
            # using이 부분 매치 (e.g., System.Collections.Generic > System.Collections) 허용
            if any(u.startswith(ns) for u in usings):
                continue
            warnings.append(f"using 누락 의심: {token} 사용 — `using {ns};` 필요")
            break  # 한 번에 1개만 보고

if not warnings:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

short = file_path
try:
    short = os.path.relpath(file_path, "C:/Project/곤충게임").replace("\\", "/")
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
