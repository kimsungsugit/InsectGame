"""PostToolUse hook: C# 네임스페이스 검증"""
import sys, json, re

raw = sys.stdin.read()
# Windows 역슬래시 경로를 슬래시로 치환하여 JSON 파싱 오류 방지
raw = raw.replace("\\", "/")
d = json.loads(raw)
f = d.get("tool_response", {}).get("filePath", "") or d.get("tool_input", {}).get("file_path", "")

if not f.endswith(".cs"):
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
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PostToolUse",
            "additionalContext": f"WARNING: Failed to verify namespace in {f}: {e}"
        }
    }))
