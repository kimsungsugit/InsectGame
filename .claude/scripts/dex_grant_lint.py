#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""곤충을 지급하면 도감에도 올렸는가 — `AddCapturedInsect` ↔ `RegisterCapture` 짝 검사.

왜 필요한가
-----------
곤충 지급 경로는 여섯 곳이다(포획·전투·레이드·가챠·튜토리얼 보상·스토리 보상). 각각이
`PlayerInsectCollection.AddCapturedInsect`와 `DexController.RegisterCapture`를 **따로** 불러야
하는데, 이건 컴파일러도 런타임도 잡아주지 않는 배선이라 조용히 빠진다.

2026-08-17 audit에서 **두 곳이 동시에 빠져 있는 것**을 찾았다:
  - `TutorialQuestManager` — 튜토리얼 보상 곤충(첫 파트너)이 소유·출전까지 하는데 도감엔
    영원히 미발견. 100% 완주가 불가능하고, `DexController.CapturedSpeciesCount`가 전
    플레이어에게 1 낮게 잡혀 스토리 DexProgress 비트가 한 종 늦게 열렸다.
    `dexController` 필드가 선언·대입만 되고 **한 번도 읽히지 않는** 죽은 필드였다.
  - `StoryDirector` — 같은 형태. 지금은 Story.json 전 비트가 `rewardInsectId: ""`라 휴면이지만,
    첫 곤충 보상 비트를 저작하는 순간 "준 곤충이 자기 트리거를 못 밀어올리는" 상태가 된다.

증상이 조용한 게 핵심이다 — 예외도 경고도 없고, 곤충은 멀쩡히 손에 들어온다.
도감을 열어 세어 보기 전까지 아무도 모른다.

검사 규칙
---------
`.AddCapturedInsect(` 호출이 있는 파일은 같은 파일 안 ±`WINDOW`줄 이내에
`RegisterCapture(`가 있어야 한다. 없으면 FAIL.

`PlayerInsectCollection.cs`는 면제다 — 선언부이고 내부 재사용(`EnsureOwned` 등)이라
도감 등록의 주체가 아니다.

이 검사는 **호출 지점의 근접성**만 본다. 정적으로 "실제로 실행되는가"까지는 알 수 없으므로,
가짜 통과를 만들려면 주석에 `RegisterCapture(`를 적으면 된다 — 그건 검사기를 속이는 것이지
검사기의 결함이 아니다. 주석·문자열은 지급 호출 쪽에서만 제외한다.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCAN_DIR = os.path.join(ROOT, "Assets", "Scripts")

# 선언부 + 내부 재사용. 도감 등록의 주체가 아니다.
EXEMPT_FILES = {"PlayerInsectCollection.cs"}

GRANT = re.compile(r"\.AddCapturedInsect\s*\(")
REGISTER = re.compile(r"RegisterCapture\s*\(")

# 지급과 등록이 같은 블록 안에 있는지 보는 창. 실측 최대 간격은 12줄
# (TutorialQuestManager: 772 지급 → 784 등록)이라 40줄이면 충분히 넉넉하다.
WINDOW = 40


def is_code(line):
    """주석 한 줄인지. 지급 호출을 셀 때만 쓴다(주석 속 예시가 FAIL을 만들지 않게)."""
    s = line.strip()
    return not (s.startswith("//") or s.startswith("*") or s.startswith("/*"))


def main():
    if not os.path.isdir(SCAN_DIR):
        print(f"[dex_grant_lint] 스캔 대상 없음: {SCAN_DIR}", file=sys.stderr)
        return 2

    failures = []
    grant_sites = 0

    for dirpath, _, filenames in os.walk(SCAN_DIR):
        for name in sorted(filenames):
            if not name.endswith(".cs") or name in EXEMPT_FILES:
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, ROOT).replace("\\", "/")
            try:
                lines = open(path, encoding="utf-8").read().splitlines()
            except (OSError, UnicodeDecodeError) as e:
                print(f"[dex_grant_lint] 읽기 실패 {rel}: {e}", file=sys.stderr)
                return 2

            reg_lines = [i for i, l in enumerate(lines) if REGISTER.search(l)]

            for i, line in enumerate(lines):
                if not GRANT.search(line) or not is_code(line):
                    continue
                grant_sites += 1
                if not any(abs(r - i) <= WINDOW for r in reg_lines):
                    failures.append((rel, i + 1, line.strip()[:90]))

    for rel, ln, src in failures:
        print(f"FAIL {rel}:{ln} — 곤충을 지급하는데 {WINDOW}줄 안에 "
              f"DexController.RegisterCapture 호출이 없다: {src}")

    print(f"\n요약: 지급 지점 {grant_sites}곳, 도감 등록 누락 {len(failures)}건")
    if failures:
        print("도감에 안 올라간 곤충은 100% 완주를 막고 CapturedSpeciesCount "
              "기반 스토리 트리거를 늦춘다. 지급 옆에 RegisterEncounter + RegisterCapture를 붙일 것.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
