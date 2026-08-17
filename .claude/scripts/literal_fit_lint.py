"""리터럴 문구가 폰트보다 작은 상자에 그려지는 자리를 찾는다 — text_fit_lint의 사각지대.

`text_fit_lint.py`는 **길이를 데이터가 정하는데 상자가 고정**인 자리만 본다(".description" 등).
그래서 `GUI.Label(rect, "고정 문구", style)`처럼 리터럴을 쓰는 자리는 아무도 안 본다 —
36pt를 28px 상자에 그리던 곳이 실제로 있었다(2026-08-08 커밋 a523726이 손으로 둘 찾아 고쳤다).

판정: 한글 줄높이 ≈ fontSize × 1.35 (`DexScreenUI.LineH` / `TutorialQuestUI.RowH`와 같은 계산).
그보다 낮은 Rect면 위아래가 깎인다. **상자 높이 ≤ fontSize인 자리는 논란의 여지가 없다** —
글립 상자는 언제나 폰트 크기보다 크다.

**FAIL 기준은 10% 이상 부족이다.** 그보다 작은 차이는 폰트 패딩에 묻혀 실제로 안 잘릴 수 있어
정보로만 센다 — 전부를 기준으로 삼으면 정상 라벨까지 잡아 `text_fit_lint`가 247건에서
31건으로 좁혀졌던 것과 같은 실수를 되풀이한다.

고치는 방법은 `UIHelper.LabelFit`으로 바꾸는 것이다. **상자는 그대로 두고 글자를 줄인다** —
상자를 키우면 아래 요소가 전부 밀려 회귀 범위가 커진다(rules/ui-layout.md). IMGUI는 배치모드로
캡처할 수 없어 눈으로 확인할 수 없으므로(rules/testing.md), 레이아웃을 안 건드리는 쪽이 맞다.
상자를 키우는 건 아래가 밀려도 되는 자리(스크롤 목록 등)에서, 이웃 y를 검산한 뒤에만.

    python -X utf8 .claude/scripts/literal_fit_lint.py

한계: 스타일 변수의 fontSize가 리터럴로 정해진 것만 본다(동적 대입은 제외).
Rect 높이도 숫자 리터럴인 것만 — 변수·수식은 판정하지 않으므로 실제 모수는 이보다 크다.
"""
import io, re, glob, os, collections

def sweep(path):
    src = io.open(path, encoding='utf-8').read()
    sizes = {}
    # `new GUIStyle(...) { ... }` 와 `new GUIStyle { ... }` 두 형태를 모두 본다 —
    # 괄호 없는 초기화를 빠뜨려 검출력 실측에서 주입한 결함을 놓쳤다(2026-08-17).
    for m in re.finditer(r'(\w+)\s*=\s*new GUIStyle\s*(?:\([^)]*\))?\s*\{([^}]*)\}', src, re.S):
        fm = re.search(r'fontSize\s*=\s*(\d+)', m.group(2))
        if fm:
            sizes[m.group(1)] = int(fm.group(1))
    for m in re.finditer(r'(\w+)\.fontSize\s*=\s*(\d+)\s*;', src):
        sizes[m.group(1)] = int(m.group(2))
    dynamic = set(re.findall(r'(\w+)\.fontSize\s*=\s*(?!\d)', src))
    out = []
    for m in re.finditer(r'GUI\.Label\(\s*new Rect\(', src):
        i = m.end()
        d = 1
        while i < len(src) and d:
            if src[i] == '(':
                d += 1
            elif src[i] == ')':
                d -= 1
            i += 1
        args = src[m.end():i - 1]
        dd, cur, parts = 0, '', []
        for ch in args:
            if ch == '(':
                dd += 1
            if ch == ')':
                dd -= 1
            if ch == ',' and dd == 0:
                parts.append(cur)
                cur = ''
            else:
                cur += ch
        parts.append(cur)
        if len(parts) != 4:
            continue
        try:
            h = float(parts[3].strip().rstrip('f'))
        except ValueError:
            continue
        j, d2, rest = i, 1, ''
        while j < len(src) and d2:
            if src[j] == '(':
                d2 += 1
            elif src[j] == ')':
                d2 -= 1
            if d2:
                rest += src[j]
            j += 1
        tail = [p.strip() for p in re.split(r',(?![^()]*\))', rest) if p.strip()]
        if len(tail) < 2:
            continue
        text, style = tail[-2], tail[-1]
        if not (text.startswith('"') or text.startswith('$"')):
            continue
        if style in dynamic or style not in sizes:
            continue
        need = sizes[style] * 1.35
        if need > h + 0.5:
            rel = os.path.relpath(path).replace(os.sep, '/')
            out.append((rel, src[:m.start()].count('\n') + 1, sizes[style], h,
                        round(need, 1), round((need - h) / h * 100)))
    return out

allhits = []
for f in glob.glob('Assets/Scripts/**/*.cs', recursive=True):
    allhits += sweep(f)
allhits.sort(key=lambda r: -r[5])
sev = [h for h in allhits if h[5] >= 10]
print('# literal-fit-lint — 리터럴 문구 잘림 검사')
print()
print('| 항목 | 임계값 | 측정값 | 판정 |')
print('|------|--------|--------|------|')
print('| 리터럴 라벨 상자 부족(10%% 이상) | 0건 | %d건 | **%s** |'
      % (len(sev), 'FAIL' if sev else 'PASS'))
print('| 경미(10%% 미만, 정보) | — | %d건 | — |' % (len(allhits) - len(sev)))
print()
print()
for f, line, fs, h, need, pct in allhits[:20]:
    print('%s %s:%d  %dpt 상자%g 필요%g (%d%% 부족)'
          % ('**' if pct >= 10 else '  ', f, line, fs, h, need, pct))
print()
print('파일별:', dict(collections.Counter(h[0].split('/')[-1] for h in allhits)))

import sys
sys.exit(1 if sev else 0)
