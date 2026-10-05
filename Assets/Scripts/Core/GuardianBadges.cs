using System;
using System.Collections.Generic;

namespace InsectGame.Core
{
    /// <summary>
    /// 수문장 배지 — 리전마다 하나, 그 리전의 수문장을 처음 이기면 얻는다.
    ///
    /// <b>배지는 저장하지 않는다.</b> 수문장 격파 기록(<c>RegionManager</c>의 <c>DefeatedGuardians</c>)이
    /// 곧 배지 목록이다 — 그 집합은 이미 클라우드에 오르므로 배지도 기기를 따라간다. 따로 적으면 두 기록이
    /// 어긋나는 순간(클라우드 병합·계정 전환) "이겼는데 배지가 없다"가 생긴다.
    ///
    /// 저장하는 건 <b>이정표 보상 수령 상태</b> 하나다(<see cref="GuardianBadgeService"/>) — 그건 파생이 안 된다.
    ///
    /// 그림은 <c>Tools/Badges/guardian_badges.py</c>가 굽는다(<c>Resources/UI/Badges/badge_{regionId}.png</c>).
    /// 여기 리전을 늘리면 그 스크립트의 표도 함께 고친다 — <c>GuardianBadgeTests</c>가 그림 누락을 잡는다.
    /// 순수 데이터 + 순수 계산이다.
    /// </summary>
    public static class GuardianBadges
    {
        public const string ArtFolder = "UI/Badges/";

        public struct Badge
        {
            public string regionId;
            public string name;
            /// <summary>배지에 새겨진 한 줄 — 그 수문장과의 일을 떠올리게 한다(gd_* 대사와 결이 같다).</summary>
            public string flavor;
        }

        public struct Reward
        {
            public string itemId;
            public int count;
        }

        public struct Milestone
        {
            /// <summary>필요한 배지 수.</summary>
            public int count;
            public string title;
            public Reward[] rewards;
        }

        // 표시 순서 = 리전 요구 레벨 순서(꽃밭은 숲과 습지 사이). 진행 순서대로 채워지는 게 보여야 한다.
        private static readonly Badge[] Table =
        {
            new Badge { regionId = "meadow", name = "풀잎 배지", flavor = "처음으로 길을 내어 준 초원의 낫." },
            new Badge { regionId = "pond", name = "물결 배지", flavor = "수면을 스치던 날개가 길을 비켰다." },
            new Badge { regionId = "forest", name = "뿔 배지", flavor = "진 게 아니라 물러난 뿔의 무게." },
            new Badge { regionId = "garden", name = "꽃잎 배지", flavor = "들어오는 것을 막던 문지기의 허락." },
            new Badge { regionId = "swamp", name = "안개 배지", flavor = "안개가 걷힌 자리는 비어 있지 않았다." },
            new Badge { regionId = "mountain", name = "능선 배지", flavor = "산바람을 뒤집은 날개가 길을 열었다." },
            new Badge { regionId = "ruins", name = "태양 배지", flavor = "등껍질을 접은 파수꾼이 건넨 증표." },
            new Badge { regionId = "hollow", name = "침묵 배지", flavor = "길을 잃은 낫을 놓아준 기억." },
            new Badge { regionId = "dunes", name = "모래 배지", flavor = "모래바람 속으로 날아오른 침." },
            new Badge { regionId = "frostline", name = "서리 배지", flavor = "얼음 벽의 글자가 우리를 알아보았다." },
            new Badge { regionId = "emberfall", name = "잿불 배지", flavor = "재 밑에서 다시 움직이는 온기." },
            new Badge { regionId = "canopy", name = "우듬지 배지", flavor = "시험을 마친 세계수의 날갯짓." },
            new Badge { regionId = "nameless", name = "빈칸 배지", flavor = "칸은 있는데, 안이 비어 있다." },
        };

        // 이정표 보상 — 한 번만 받는다. 4는 2막 전(숲·꽃밭 무렵), 8은 2막 초입, 13은 전부.
        // 기술 디스크 등급이 이정표를 따라 오른다(Rare → Epic → Legendary). 가격표(젬)로 치면
        // 4: 약 350, 8: 약 400, 13: 약 1,200 — 캐시 상점을 대체하지 않을 만큼, 수집을 보람 있게 할 만큼.
        private static readonly Milestone[] Milestones =
        {
            new Milestone
            {
                count = 4, title = "길잡이",
                rewards = new[]
                {
                    new Reward { itemId = "net_gold", count = 3 },
                    new Reward { itemId = "disc_frenzy", count = 1 },
                },
            },
            new Milestone
            {
                count = 8, title = "먼 길의 여행자",
                rewards = new[]
                {
                    new Reward { itemId = "disc_mega_strike", count = 1 },
                    new Reward { itemId = "guardian_totem", count = 1 },
                },
            },
            new Milestone
            {
                count = 13, title = "모든 길을 연 자",
                rewards = new[]
                {
                    new Reward { itemId = "disc_doom_sting", count = 1 },
                    new Reward { itemId = "golden_censer", count = 1 },
                },
            },
        };

        public static int Total => Table.Length;

        /// <summary>표 전체(값 복사) — 표시 순서.</summary>
        public static Badge[] All()
        {
            Badge[] copy = new Badge[Table.Length];
            Array.Copy(Table, copy, Table.Length);
            return copy;
        }

        public static int MilestoneCount => Milestones.Length;

        /// <summary>할당 없는 조회 — OnGUI가 매 프레임 부른다(퀵메뉴의 받을 보상 표시).</summary>
        public static Milestone MilestoneAt(int index) => Milestones[index];

        public static Milestone[] AllMilestones()
        {
            Milestone[] copy = new Milestone[Milestones.Length];
            Array.Copy(Milestones, copy, Milestones.Length);
            return copy;
        }

        public static int IndexOf(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return -1;
            for (int i = 0; i < Table.Length; i++)
                if (Table[i].regionId == regionId) return i;
            return -1;
        }

        public static bool TryGet(string regionId, out Badge badge)
        {
            int i = IndexOf(regionId);
            badge = i >= 0 ? Table[i] : default;
            return i >= 0;
        }

        public static bool TryGetMilestone(int count, out Milestone milestone)
        {
            for (int i = 0; i < Milestones.Length; i++)
            {
                if (Milestones[i].count != count) continue;
                milestone = Milestones[i];
                return true;
            }
            milestone = default;
            return false;
        }

        public static string ArtPath(string regionId) => ArtFolder + "badge_" + regionId;

        /// <summary>얻은 배지 수 — 표에 있는 리전만 센다(격파 집합에 낯선 ID가 섞여도 13을 넘지 않는다).</summary>
        public static int CountEarned(Func<string, bool> isDefeated)
        {
            if (isDefeated == null) return 0;
            int n = 0;
            for (int i = 0; i < Table.Length; i++)
                if (isDefeated(Table[i].regionId)) n++;
            return n;
        }

        /// <summary>
        /// 도달했지만 아직 안 받은 이정표(오름차순). 한 번에 여럿일 수 있다 — 이 기능이 생기기 전에
        /// 이미 배지를 모아 둔 세이브, 다른 기기에서 격파가 넘어온 세이브.
        /// </summary>
        public static List<int> Claimable(int earned, ICollection<int> claimed)
        {
            List<int> list = new List<int>();
            for (int i = 0; i < Milestones.Length; i++)
            {
                int c = Milestones[i].count;
                if (earned >= c && (claimed == null || !claimed.Contains(c))) list.Add(c);
            }
            return list;
        }

        /// <summary>"4,8" → {4, 8}. 숫자가 아니거나 이정표가 아닌 값은 버린다(조작·옛 포맷에 안전).</summary>
        public static HashSet<int> ParseClaimed(string csv)
        {
            HashSet<int> set = new HashSet<int>();
            if (string.IsNullOrEmpty(csv)) return set;
            foreach (string raw in csv.Split(','))
            {
                if (int.TryParse(raw.Trim(), out int v) && TryGetMilestone(v, out _)) set.Add(v);
            }
            return set;
        }

        public static string FormatClaimed(IEnumerable<int> claimed)
        {
            List<int> list = claimed != null ? new List<int>(claimed) : new List<int>();
            list.Sort();
            return string.Join(",", list);
        }
    }
}
