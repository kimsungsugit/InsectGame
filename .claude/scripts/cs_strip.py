"""C# 소스에서 주석·문자열·문자 리터럴을 지운다. 하네스 훅들의 공용 전처리.

왜 있나 — 순서를 뒤집으면 코드를 삼킨다
----------------------------------------
훅 3개(quick_compile_check, scan_static_patterns, audit_candidates)가 같은 코드를
각자 복사해 갖고 있었고, 셋 다 **주석을 문자열보다 먼저** 지웠다:

    s = re.sub(r"//[^\n]*", "", s)          # ← 먼저
    ...
    s = re.sub(r'"(?:\\.|[^"\\])*"', "", s)  # ← 나중

C# 렉싱은 그 반대다. `string url = "https://example.com/x";`에서 `//`는 주석이 아니라
문자열 안이다. 그런데 주석 규칙이 먼저 돌면 `//example.com/x";`가 통째로 잘리고
**닫는 따옴표가 사라진다**. 그 다음 문자열 규칙이 남은 여는 `"`를 파일 저 아래의
다음 `"`와 짝지어 그 사이의 **진짜 코드를 중괄호째 삼킨다**. 결과는 중괄호 불균형
오경고. FirebaseConfig.cs에서 { 23개 vs } 20개로 실제 발화했다.

해결은 순서 교체가 아니라 **단일 패스**다. 주석과 문자열은 서로 배타적 문맥이므로,
왼쪽에서 오른쪽으로 훑으며 먼저 열리는 쪽이 이기게 하면 양쪽 다 옳다:
  - `"https://..."` → 따옴표가 먼저 열리므로 문자열이 이긴다
  - `// "인용"`     → 슬래시가 먼저 오므로 주석이 이긴다
정규식 교대(alternation)가 정확히 이 규칙이다.
"""
import re

# 한 번의 스캔으로 처리한다. 위치가 같을 때의 우선순위가 아니라 **먼저 열리는 쪽**이
# 이긴다는 게 핵심 — re.finditer가 왼쪽부터 훑으므로 자연히 그렇게 된다.
#
# @"..." 를 "..." 보다 앞에 둔다: `@`에서 시작하는 축자 문자열은 백슬래시를 이스케이프로
# 보지 않으므로(`@"C:\path"`) 규칙이 다르다.
_TOKEN = re.compile(
    r"""
      @"(?:[^"]|"")*"          # 축자 문자열 @"..."  ($@"..." 는 @ 에서 걸린다)
    | "(?:\\.|[^"\\])*"        # 일반/보간 문자열 "..." ($"..." 는 " 에서 걸린다)
    | '(?:\\.|[^'\\])*'        # 문자 리터럴 '{' 등
    | //[^\n]*                 # 한 줄 주석
    | /\*[\s\S]*?\*/           # 블록 주석
    """,
    re.VERBOSE,
)


def _blank(m: "re.Match") -> str:
    """매치를 같은 줄 수의 공백으로 바꾼다 — 라인 번호와 오프셋을 보존."""
    return "".join("\n" if ch == "\n" else " " for ch in m.group(0))


def strip_cs(src: str) -> str:
    """주석·문자열·문자 리터럴을 공백으로 치환. 줄 번호는 보존된다.

    지우지 않고 공백으로 바꾸는 이유: 훅들이 `파일:라인`으로 보고하므로 줄이 밀리면
    엉뚱한 라인을 가리킨다.
    """
    return _TOKEN.sub(_blank, src)


if __name__ == "__main__":
    import io
    import sys

    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

    cases = [
        ('string u = "https://a.com/x"; { }', "URL의 //가 주석으로 오인되면 안 됨"),
        ('// 주석 안의 "따옴표" 는 문자열이 아님\n{ }', "주석이 이기는 경우"),
        (r'string p = @"C:\temp\x"; { }', "축자 문자열의 백슬래시"),
        ('string s = "a\\"b"; { }', "이스케이프된 따옴표"),
        ("char c = '{'; { }", "문자 리터럴 중괄호"),
        ('/* 블록\n주석 { */ { }', "블록 주석"),
        ('string i = $"{x}/{y}"; { }', "보간 문자열"),
    ]
    ok = 0
    for src, label in cases:
        out = strip_cs(src)
        o, c = out.count("{"), out.count("}")
        balanced = o == c
        same_lines = out.count("\n") == src.count("\n")
        mark = "OK  " if balanced and same_lines else "실패"
        if balanced and same_lines:
            ok += 1
        print(f"  [{mark}] {label}: {{ {o}개 / }} {c}개, 줄 보존 {same_lines}")
    print(f"\n  {ok}/{len(cases)} 통과")
    sys.exit(0 if ok == len(cases) else 1)
