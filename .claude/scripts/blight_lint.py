"""blight-lint — 명부회 오염 거점이 **조용히** 깨지는 지점만 검사한다.

이 기능의 실패는 전부 무증상이다. 예외도 경고도 안 나고, 거점이 그냥 영원히 안 무너지거나
정화해도 아무것도 안 돌아온다. 기존 검사기가 못 보는 것만 여기서 본다 —
비트 무결성은 story_lint(21검사), 퀘스트 배선은 quest_lint(10검사)가 이미 본다.

가장 큰 급소 둘:

**1. 재도전 예외가 사라지면 기존 세이브에 기능이 안 보인다.**
`CanBossDuel`은 이미 이긴 간부에게 무조건 false를 돌려준다. 두 하수를 이미 이긴 세이브
(2막 진행자 대부분)는 그 예외가 없으면 산·유적의 거점을 영영 부수지 못한다.

**2. 스폰 상한이 0으로 내려가면 캠페인이 멈춘다.**
오염 리전에서의 포획·전투를 조건으로 건 비트가 넷이고, 같은 리전의 1막 비트도 특정 종
포획을 요구한다(산의 아폴로나비, 유적의 유물풍뎅이). 곤충이 안 뜨면 전부 도달 불가다.

exit 0 = 통과, 1 = 위반, 2 = 추출 실패(구조가 바뀌었으니 파서부터 확인할 것).
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import game_facts as gf  # noqa: E402

BEAT_ROLES = ("arrive", "sign", "confront", "clash", "restore")


def read(rel):
    path = os.path.join(ROOT, rel)
    if not os.path.exists(path):
        raise gf.ExtractorBroken(rel + ": 파일 없음 — 경로가 바뀌었으면 이 검사기도 고칠 것")
    with io.open(path, encoding="utf-8", errors="replace") as f:
        return f.read()


def code_only(src):
    """주석을 걷어낸 소스. 검사기는 코드를 봐야지 설명을 보면 안 된다 —
    "이건 쓰지 않는다"고 적어 둔 주석 때문에 FAIL이 났다(실제로 겪음)."""
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    return re.sub(r"//.*", "", src)


def _boss_duels():
    src = read("Assets/Scripts/NPC/NpcBossDuels.cs")
    out = []
    for block in re.findall(r"new\s+BossDuel\s*\{(.*?)\}", src, re.S):
        npc = re.search(r'storyNpcId\s*=\s*"([^"]+)"', block)
        if npc:
            out.append(npc.group(1))
    if not out:
        raise gf.ExtractorBroken("NpcBossDuels에서 대결을 하나도 못 읽었다 — 표 구조가 바뀌었는가?")
    return set(out)


def _village_anchors():
    """{(storyNpcId, regionId)} — VillageBuilder가 세우는 스토리 NPC 앵커."""
    src = read("Assets/Scripts/Core/VillageBuilder.cs")
    src = re.sub(r"//[^\n]*", "", src)   # 주석에 적힌 ID는 배치가 아니다
    out = set()
    for m in re.finditer(r'regionId\s*=\s*"(\w+)"\s*,\s*\n\s*storyNpcId\s*=\s*"(\w+)"', src):
        out.add((m.group(2), m.group(1)))
    if not out:
        raise gf.ExtractorBroken(
            "VillageBuilder에서 스토리 NPC 앵커를 못 읽었다 — 배치 형태가 바뀌었는가?")
    return out


def main():
    signals = []

    def add(name, expect, got, ok):
        signals.append((name, expect, got, "PASS" if ok else "FAIL"))

    sites = gf.blight_sites()
    site_regions = [r for r, _b, _n, _i in sites]

    # 1. 거점 보스가 대결 표에 있는가 — 없으면 도전 자체가 안 열려 영구 오염이다.
    duel_bosses = _boss_duels()
    missing = [r + ":" + b for r, b, _n, _i in sites if b not in duel_bosses]
    add("거점 보스 ↔ NpcBossDuels 표", "0건 미등재",
        str(len(missing)) + "건 " + str(missing) if missing else "0건 (거점 %d개)" % len(sites),
        not missing)

    # 2. 거점 보스가 그 리전에 실제로 서 있는가.
    anchors = _village_anchors()
    absent = [r + ":" + b for r, b, _n, _i in sites if (b, r) not in anchors]
    add("거점 보스가 그 리전에 배치됨", "0건 부재",
        str(len(absent)) + "건 " + str(absent) if absent else "0건 (앵커 %d개 대조)" % len(anchors),
        not absent)

    # 3. 한 보스가 거점 둘을 맡으면 "그자를 꺾으면 그 거점이 닫힌다"가 성립하지 않는다.
    seen, dup = set(), []
    for r, b, _n, _i in sites:
        if b in seen:
            dup.append(b + "(" + r + ")")
        seen.add(b)
    add("한 보스 = 한 거점", "0건 중복",
        str(len(dup)) + "건 " + str(dup) if dup else "0건", not dup)

    # 4. 귀환종이 곤충 DB + 그 리전 풀에 있는가 — 없으면 정화해도 아무것도 안 돌아온다.
    all_ids = gf.all_insect_ids()
    pools = {rid: ids for rid, _lv, ids in gf.region_pools()}
    bad = []
    for r, _b, _n, ins in sites:
        if not ins:
            bad.append(r + ": 귀환종 없음")
        elif ins not in all_ids:
            bad.append(r + ": '" + ins + "' DB에 없음")
        elif ins not in pools.get(r, []):
            bad.append(r + ": '" + ins + "' 그 리전 풀에 없음")
    add("귀환종 ↔ 곤충 DB · 리전 풀", "0건",
        str(len(bad)) + "건 " + str(bad) if bad else "0건 (곤충 %d종 대조)" % len(all_ids),
        not bad)

    # 5. 거점마다 bl_{리전}_* 5비트 전부 — 빠지면 그 거점만 서사 없이 침묵한다.
    beats = {b["beatId"]: b for b in gf.story_beats()}
    lack = []
    for r in site_regions:
        for role in BEAT_ROLES:
            bid = "bl_" + r + "_" + role
            if bid not in beats:
                lack.append(bid)
    add("거점당 bl_* 5비트", "0건 누락",
        str(len(lack)) + "건 " + str(lack) if lack else "0건 (거점 %d개 × 5)" % len(sites),
        not lack)

    # 6. 도착 비트의 진행 게이트.
    #    story_lint 검사 17(챕터 도달 순서)은 chapterId가 ch{N}/fin이 아니면 건너뛴다.
    #    'bl'이 정확히 그 사각지대라, 게이트가 없으면 시작 지역에서 뒷 이야기가 새어도 아무도 안 잡는다.
    ungated = []
    for r in site_regions:
        bid = "bl_" + r + "_arrive"
        b = beats.get(bid)
        if b is not None and not (b.get("requiredBeatId") or b.get("requiredQuestId")):
            ungated.append(bid)
    add("도착 비트 진행 게이트 (story_lint 17의 사각지대)", "0건 무게이트",
        str(len(ungated)) + "건 " + str(ungated) if ungated else "0건", not ungated)

    # 7. RegionCleansed param이 실제 거점 리전인가.
    #    story_lint 검사 3은 트리거별 if/elif 체인이라 이 타입 분기가 없어 **무조건 통과**한다.
    stray = []
    for bid, b in beats.items():
        t = b.get("trigger") or {}
        if t.get("type") != "RegionCleansed":
            continue
        if t.get("param") not in site_regions:
            stray.append(bid + ":" + str(t.get("param")))
    add("RegionCleansed param ↔ 거점 리전", "0건 불일치",
        str(len(stray)) + "건 " + str(stray) if stray else "0건 (거점 리전 %s)" % site_regions,
        not stray)

    # 8. 정화 발화가 DeferTrigger 경유인가 — 직접 쏘면 보상 패널을 덮고 모달 중엔 유실된다.
    director = code_only(read("Assets/Scripts/Story/StoryDirector.cs"))
    deferred = "DeferTrigger(TriggerRegionCleansed" in director
    add("정화 발화 = DeferTrigger 경유", "직접 EvaluateTriggers 금지",
        "DeferTrigger 사용" if deferred else "**직접 발화 — 보상 패널을 덮고 모달 중엔 유실**",
        deferred)

    # 9. 재진입 재발화 경로.
    #    정화 순간에 그 비트가 자격 미달이면(대치 비트 미열람) 트리거가 소비돼 사라진다.
    #    리전에 다시 들어올 때 다시 쏘는 것이 유일한 회복 경로다.
    refire = re.search(r"IsCleansed\(.{0,60}?EvaluateTriggers\(TriggerRegionCleansed",
                       director, re.S) is not None
    add("정화 리전 재진입 시 재발화", "존재",
        "있음" if refire else "**없음 — 놓친 세이브가 정화 비트를 영영 못 본다**", refire)

    # 10. 오염 리전 재도전 예외 — 이게 없으면 기존 세이브에 기능 자체가 안 보인다.
    duel = code_only(read("Assets/Scripts/NPC/NpcDuelController.cs"))
    rematch = "IsBlightBossHere" in duel and "IsBossDefeated" in duel
    add("오염 리전 보스 재도전 예외", "존재",
        "있음" if rematch else "**없음 — 이미 이긴 세이브는 거점을 영영 못 부순다**", rematch)

    # 11. 재도전 보상 재지급 가드.
    guard = (re.search(r"bool\s+firstWin\s*=\s*defeatedBosses\.Add", duel) is not None
             and re.search(r"if\s*\(\s*firstWin\s*&&", duel) is not None)
    add("재도전 승리 보상 재지급 가드", "존재",
        "있음(firstWin)" if guard else "**없음 — 재도전마다 보상이 다시 지급된다**", guard)

    # 12. 정화 호출부.
    cleanse = "CleanseByBoss" in duel
    add("정화 호출부(NpcDuelController)", "존재",
        "있음" if cleanse else "**없음 — 이겨도 정화되지 않는다**", cleanse)

    # 13. 스폰 상한이 전부 BlightPolicy 경유인가.
    #     CountActiveInRegion(...) 비교가 곧 "이 리전에 더 띄울까"다. 하나라도 날것의
    #     maxActivePerRegion을 쓰면 그 경로만 오염을 무시해 스폰 편향이 생긴다.
    spawner = code_only(read("Assets/Scripts/Spawning/InsectSpawner.cs"))
    raw_cmp = re.findall(r"CountActiveInRegion\([^)]*\)\s*[<>]=?\s*maxActivePerRegion", spawner)
    add("리전 상한 비교 = BlightPolicy 경유", "0건 날것",
        str(len(raw_cmp)) + "건 " + str(raw_cmp) if raw_cmp else "0건 (전부 RegionCap 경유)",
        not raw_cmp)

    # 14. 스폰 하한 — 0이면 그 리전의 포획·전투 비트가 전부 도달 불가가 된다.
    policy = code_only(read("Assets/Scripts/Core/BlightPolicy.cs"))
    m = re.search(r"MinActive\s*=\s*(\d+)", policy)
    min_active = int(m.group(1)) if m else -1
    add("스폰 하한 MinActive", ">= 1",
        str(min_active) if min_active >= 1 else "**%d — 캠페인이 영구 정지한다**" % min_active,
        min_active >= 1)

    # 15. BlightPolicy에 리전 ID 리터럴 0건.
    #     "어느 리전이 오염 대상인가"는 RegionData가 답한다. 하드코딩 목록은 이 저장소에서
    #     세 번 조용히 어긋났다(RegionDefinitions.cs 상단 주석).
    leaked = sorted([r for r in pools if '"' + r + '"' in policy])
    add("BlightPolicy에 리전 ID 리터럴", "0건",
        str(len(leaked)) + "건 " + str(leaked) if leaked else "0건", not leaked)

    # 16. 「지워진 개체」가 1막으로 새지 않는가.
    #     오염 표현에 erasedChance를 재사용하면 1막 리전이 2막 연출을 쓰게 된다. 그러면
    #     특정 종 포획 비트(ch5_apollo/ch6_relic)가 "???"에 묻혀 사실상 진행 불가가 된다.
    act2_gate = "IsAct2Region" in spawner
    add("GetErasedChance의 2막 게이트 유지", "유지",
        "유지" if act2_gate else "**해제됨 — 1막에 지워진 개체가 샌다**", act2_gate)

    # 17. 세이브 배선.
    scope = read("Assets/Scripts/Core/SaveScope.cs")
    cloud = read("Assets/Scripts/Core/CloudSaveManager.cs")
    save_bad = []
    if "BlightCleansed" not in scope:
        save_bad.append("SaveScope 스코핑 목록 누락(계정 삭제 후 재로그인 시 부활)")
    if "DefeatedLedgerBosses" not in scope:
        save_bad.append("DefeatedLedgerBosses 스코핑 목록 누락")
    n = cloud.count("blightCleansed")
    if n < 5:
        save_bad.append("클라우드 DTO 지점 %d/5 (선언·수집·적용·직렬화·파싱)" % n)
    add("정화 세이브 배선", "SaveScope 2키 + 클라우드 5지점",
        str(len(save_bad)) + "건 " + str(save_bad) if save_bad else "정상 (클라우드 %d/5)" % n,
        not save_bad)

    # 18. VFX가 전역 렌더 설정을 만지지 않는가.
    #     SubAreaEnvironment가 Start에 RenderSettings 기본값을 1회 스냅샷하고 매 프레임 덮는다.
    #     오염 fog를 켜면 서브에리어에 한 번 들어갔다 나오는 순간 조용히 지워진다.
    vfx_rel = "Assets/Scripts/Core/BlightVfx.cs"
    if os.path.exists(os.path.join(ROOT, vfx_rel)):
        vfx = code_only(read(vfx_rel))
        touches = "RenderSettings." in vfx
        add("BlightVfx가 RenderSettings 미사용", "0건",
            "**사용함 — 서브에리어 왕복에 조용히 지워진다**" if touches else "0건", not touches)

    fails = [s for s in signals if s[3] == "FAIL"]
    print("# blight-lint — 명부회 오염 거점 정합성\n")
    print("| 검사 | 기대 | 측정 | 판정 |")
    print("|---|---|---|---|")
    for name, expect, got, verdict in signals:
        print("| %s | %s | %s | **%s** |" % (name, expect, got, verdict))
    print("\n요약: **%d FAIL** / %d PASS" % (len(fails), len(signals) - len(fails)))
    print("\n## 가정 / 한계")
    print("- 거점 정의는 RegionData 필드에서 읽는다(별도 표 아님). 0건은 정상 상태로 본다 —")
    print("  필드 선언 자체가 사라지면 game_facts가 ExtractorBroken으로 죽는다.")
    print("- 비트 무결성·연출/컷신 배선은 story_lint(21검사), 퀘스트는 quest_lint(10검사) 담당.")
    print("- 검사 6·7은 story_lint의 사각지대를 메운다: 검사 17이 'bl' 챕터를 건너뛰고,")
    print("  검사 3은 RegionCleansed 분기가 없어 param을 안 본다.")
    print("- 소스 grep 기반 검사(8~13, 16~18)는 '경로가 존재하는가'만 본다. 그 경로가 실제로")
    print("  불리는지는 기기 확인 대상이다.")
    return 1 if fails else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except gf.ExtractorBroken as e:
        print("[blight-lint] 추출 실패: " + str(e), file=sys.stderr)
        sys.exit(2)
