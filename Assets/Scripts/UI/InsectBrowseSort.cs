using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;

namespace InsectGame.UI
{
    /// <summary>
    /// 보유 곤충 목록의 정렬 기준. 배틀팀 피커와 보유 곤충 화면이 <b>같은 순서</b>를 쓴다 —
    /// 두 화면이 다르게 정렬하면 "방금 팀에서 본 그 곤충"을 목록에서 다시 찾아야 한다.
    /// </summary>
    public enum InsectSortMode
    {
        /// <summary>등급 높은 순 → 같으면 CP 높은 순. 기본값이다(강한 것부터 보는 게 편성의 기본).</summary>
        Rarity,
        /// <summary>레벨 높은 순 → 같으면 CP.</summary>
        Level,
        /// <summary>CP 높은 순.</summary>
        Cp,
        /// <summary>최근에 잡은 순. 방금 잡은 개체를 찾을 때 쓴다.</summary>
        Recent
    }

    /// <summary>
    /// 보유 곤충 정렬의 <b>단일 출처</b>. 순수 계산이라 씬 없이 테스트된다.
    ///
    /// 정렬은 <b>안정적</b>이어야 한다 — 같은 키를 가진 개체의 상대 순서가 패스마다 바뀌면
    /// 목록이 미세하게 떨리고, 스크롤 위치가 가리키던 항목이 달라진다. 그래서 모든 비교는
    /// 마지막에 <c>instanceId</c>로 동점을 깬다(전역 유일하므로 순서가 확정된다).
    /// </summary>
    public static class InsectBrowseSort
    {
        public static string Label(InsectSortMode mode)
        {
            switch (mode)
            {
                case InsectSortMode.Level: return "레벨";
                case InsectSortMode.Cp: return "전투력";
                case InsectSortMode.Recent: return "최근";
                default: return "등급";
            }
        }

        /// <summary>칩으로 그릴 순서. UI 두 곳이 같은 배열을 돌아 항목이 어긋나지 않게 한다.</summary>
        public static readonly InsectSortMode[] Order =
        {
            InsectSortMode.Rarity, InsectSortMode.Level, InsectSortMode.Cp, InsectSortMode.Recent
        };

        /// <summary>
        /// <paramref name="target"/>을 비우고 정렬 결과를 채운다. <b>새 리스트를 만들지 않는다</b> —
        /// 호출부가 캐시 버퍼를 넘겨 OnGUI 패스마다 할당이 나지 않게 한다.
        /// </summary>
        /// <param name="teamFirst">
        /// true면 배틀팀 소속을 앞으로 올린다(그 안에서는 아래 정렬 기준을 따른다).
        /// 보유 곤충 화면이 쓴다 — 팀을 확인하러 목록을 스크롤하지 않아도 되게.
        /// </param>
        public static void Sort(
            IReadOnlyList<PlayerInsectData> source,
            PlayerInsectCollection collection,
            InsectSortMode mode,
            List<PlayerInsectData> target,
            System.Func<PlayerInsectData, bool> isInTeam = null,
            bool teamFirst = false)
        {
            if (target == null) return;
            target.Clear();
            if (source == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null) target.Add(source[i]);
            }

            target.Sort((a, b) => Compare(a, b, collection, mode, isInTeam, teamFirst));
        }

        internal static int Compare(
            PlayerInsectData a, PlayerInsectData b,
            PlayerInsectCollection collection,
            InsectSortMode mode,
            System.Func<PlayerInsectData, bool> isInTeam,
            bool teamFirst)
        {
            if (teamFirst && isInTeam != null)
            {
                bool ta = isInTeam(a);
                bool tb = isInTeam(b);
                if (ta != tb) return ta ? -1 : 1;
            }

            int result;
            switch (mode)
            {
                case InsectSortMode.Level:
                    result = b.level.CompareTo(a.level);
                    if (result != 0) return result;
                    result = Cp(b, collection).CompareTo(Cp(a, collection));
                    break;

                case InsectSortMode.Cp:
                    result = Cp(b, collection).CompareTo(Cp(a, collection));
                    break;

                case InsectSortMode.Recent:
                    // capturedUnix 기본값 0은 "미상"(구세이브)이라 자연히 뒤로 간다.
                    result = b.capturedUnix.CompareTo(a.capturedUnix);
                    break;

                default:
                    result = RarityRank(b, collection).CompareTo(RarityRank(a, collection));
                    if (result != 0) return result;
                    result = Cp(b, collection).CompareTo(Cp(a, collection));
                    break;
            }

            if (result != 0) return result;
            // 동점은 instanceId로 확정한다 — 안 그러면 같은 키끼리 순서가 흔들려 목록이 떨린다.
            return string.CompareOrdinal(a.instanceId ?? "", b.instanceId ?? "");
        }

        private static int Cp(PlayerInsectData pid, PlayerInsectCollection collection)
        {
            InsectData data = collection != null ? collection.GetInsectData(pid.insectId) : null;
            return data != null ? PlayerInsectCombatPower.Calculate(data, pid) : 0;
        }

        /// <summary>등급을 숫자로. DB에서 종을 못 찾으면 최하위로 둔다(정렬이 예외로 죽지 않게).</summary>
        private static int RarityRank(PlayerInsectData pid, PlayerInsectCollection collection)
        {
            InsectData data = collection != null ? collection.GetInsectData(pid.insectId) : null;
            return data != null ? (int)data.rarity : -1;
        }
    }
}
