"""PostToolUse hook: C# 네임스페이스 검증"""
import sys, json, re, os, io

# Windows cp949 stdin/stdout이 한글 경로를 mojibake로 만들어 Failed to verify 경고가 매번
# 출력되는 문제 차단. claude harness가 JSON을 UTF-8로 보내므로 강제.
try:
    sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
except Exception:
    pass

raw = sys.stdin.read()
# 주의: raw.replace("\\", "/")는 JSON escape (\n, \uXXXX, \\) 모두 망가뜨려 라인 카운트/문자열
# 파싱 오류 유발. json.loads는 표준 escape를 정상 처리하므로 치환 불필요.
try:
    d = json.loads(raw)
except Exception:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

f = d.get("tool_response", {}).get("filePath", "") or d.get("tool_input", {}).get("file_path", "")

if not f or not f.endswith(".cs"):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 파일 존재 검증: 경로가 mojibake로 깨졌으면 실제로 못 열리므로 침묵 (잘못된 경고 차단).
if not os.path.exists(f):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

try:
    with open(f, "r", encoding="utf-8") as fh:
        for line in fh:
            m = re.match(r"^namespace\s+([\w.]+)", line)
            if m:
                ns = m.group(1)
                if not ns.startswith("InsectGame"):
                    print(json.dumps({
                        "hookSpecificOutput": {
                            "hookEventName": "PostToolUse",
                            "additionalContext": f'WARNING: {f} uses namespace "{ns}" - should follow InsectGame.* convention.'
                        }
                    }))
                else:
                    print(json.dumps({"suppressOutput": True}))
                sys.exit(0)
        # No namespace found
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PostToolUse",
                "additionalContext": f"WARNING: {f} has no namespace declaration. Add namespace InsectGame.{{Module}}."
            }
        }))
except Exception as e:
    # 깨진 경로/권한 등 어떤 이유로든 실패하면 침묵 (mojibake 경고 노이즈 차단).
    print(json.dumps({"suppressOutput": True}))
