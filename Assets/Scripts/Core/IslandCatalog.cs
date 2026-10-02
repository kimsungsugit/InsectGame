using System.Collections.Generic;

namespace InsectGame.Core
{
    /// <summary>
    /// 섬 물건 카탈로그 — 코드 정의(의상 카탈로그 <c>CharacterOutfitManager.BuildCatalog</c>와 같은 방식).
    /// 3D 모양은 <c>IslandObjectBuilder</c>가 같은 id로 짓는다 — id를 늘리면 그쪽 switch도 늘릴 것
    /// (빠뜨리면 상자 모양 폴백으로 뜬다. <c>IslandCatalogTests</c>가 잡는다).
    /// </summary>
    public static class IslandCatalog
    {
        private static IslandObjectDef[] all;
        private static Dictionary<string, IslandObjectDef> lookup;

        public static IReadOnlyList<IslandObjectDef> All
        {
            get
            {
                EnsureBuilt();
                return all;
            }
        }

        public static IslandObjectDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureBuilt();
            return lookup.TryGetValue(id, out IslandObjectDef def) ? def : null;
        }

        /// <summary>섬 첫 진입 때 주는 물건 — 가이드가 "보관함의 벤치를 놓아 보세요"로 바로 쓴다.</summary>
        public static readonly (string id, int count)[] StarterKit =
        {
            ("f_bench", 1),
            ("f_flowerpot", 2),
            ("t_sapling", 1),
        };

        // ── 확장 가격 ── 코인으로도 다이아로도 살 수 있다(다이아가 유료 전용이라 코인 길을 막지 않는다).

        private static readonly int[] SizeCoinPrices = { 500, 1500, 3500 };
        private static readonly int[] SizeGemPrices = { 100, 250, 450 };
        private static readonly int[] SlotCoinPrices = { 200, 350, 550, 800, 1100, 1500, 2000 };
        private static readonly int[] SlotGemPrices = { 40, 60, 80, 110, 140, 180, 220 };

        /// <summary>현재 단계에서 다음 섬 크기로 가는 값. 더 못 늘리면 false.</summary>
        public static bool SizePrice(int currentSizeLevel, out int coins, out int gems)
        {
            coins = gems = 0;
            if (currentSizeLevel < 0 || currentSizeLevel >= GameConstants.Island.MaxSizeLevel) return false;
            coins = SizeCoinPrices[currentSizeLevel];
            gems = SizeGemPrices[currentSizeLevel];
            return true;
        }

        /// <summary>방목 슬롯을 하나 더 여는 값. <paramref name="extraSlots"/>는 지금까지 산 수.</summary>
        public static bool SlotPrice(int extraSlots, out int coins, out int gems)
        {
            coins = gems = 0;
            int max = GameConstants.Island.MaxInsectSlots - GameConstants.Island.BaseInsectSlots;
            if (extraSlots < 0 || extraSlots >= max) return false;
            coins = SlotCoinPrices[extraSlots];
            gems = SlotGemPrices[extraSlots];
            return true;
        }

        private static void EnsureBuilt()
        {
            if (all != null) return;
            all = Build();
            lookup = new Dictionary<string, IslandObjectDef>(all.Length);
            for (int i = 0; i < all.Length; i++) lookup[all[i].id] = all[i];
        }

        private static IslandObjectDef[] Build()
        {
            return new[]
            {
                // ── 건물 ──
                Coin("b_cabin", "곤충 오두막", "곤충들이 쉬어 가는 집. 친밀도가 더 빨리 쌓입니다.",
                    IslandObjectCategory.Building, 3, 3, 600, 12, IslandEffectKind.BondSpeed, 0.25f),
                Coin("b_storage", "수확 창고", "수확물을 더 오래 쌓아 둡니다.",
                    IslandObjectCategory.Building, 3, 2, 900, 8, IslandEffectKind.CapHours, 4f),
                Gem("b_greenhouse", "유리 온실", "곤충이 좋아하는 풀을 사철 기릅니다. 생산량이 늘어납니다.",
                    IslandObjectCategory.Building, 3, 3, 180, 14, IslandEffectKind.YieldBonus, 0.08f),
                Coin("b_windmill", "풍차", "바닷바람에 천천히 도는 풍차.",
                    IslandObjectCategory.Building, 3, 3, 1200, 20, sizeLevel: 1),
                Gem("b_lighthouse", "등대", "밤바다를 비추는 등대. 섬의 얼굴이 됩니다.",
                    IslandObjectCategory.Building, 2, 2, 320, 30, sizeLevel: 2),

                // ── 가구 ──
                Coin("f_bench", "나무 벤치", "앉아서 섬을 바라보기 좋은 벤치.",
                    IslandObjectCategory.Furniture, 2, 1, 60, 4),
                Coin("f_table", "통나무 탁자", "통나무를 잘라 만든 탁자와 그루터기 의자.",
                    IslandObjectCategory.Furniture, 2, 2, 120, 6),
                Coin("f_lantern", "등불", "해가 져도 은은하게 빛납니다.",
                    IslandObjectCategory.Furniture, 1, 1, 80, 4),
                Coin("f_fence", "나무 울타리", "길을 내거나 구역을 나눌 때 씁니다.",
                    IslandObjectCategory.Furniture, 2, 1, 40, 2),
                Coin("f_flowerpot", "화분", "작은 꽃이 핀 화분.",
                    IslandObjectCategory.Furniture, 1, 1, 40, 3),
                Coin("f_mailbox", "우체통", "빨간 우체통. 아직 편지는 오지 않습니다.",
                    IslandObjectCategory.Furniture, 1, 1, 90, 4),
                Coin("f_sign", "나무 팻말", "섬 입구에 세워 두기 좋은 팻말.",
                    IslandObjectCategory.Furniture, 1, 1, 50, 3),
                Coin("f_hammock", "해먹", "두 기둥 사이에 건 해먹.",
                    IslandObjectCategory.Furniture, 3, 1, 220, 8),
                Coin("f_campfire", "모닥불", "둘러앉기 좋은 모닥불.",
                    IslandObjectCategory.Furniture, 2, 2, 180, 7),
                Gem("f_fountain", "분수", "물줄기가 솟는 돌 분수.",
                    IslandObjectCategory.Furniture, 3, 3, 150, 16),

                // ── 지형지물 ──
                Coin("t_sapling", "어린 나무", "이제 막 자라는 나무.",
                    IslandObjectCategory.Terrain, 1, 1, 50, 3),
                Coin("t_oak", "큰 참나무", "그늘이 넓은 참나무.",
                    IslandObjectCategory.Terrain, 2, 2, 200, 8),
                Gem("t_blossom", "벚나무", "분홍 꽃이 가득 핀 나무.",
                    IslandObjectCategory.Terrain, 2, 2, 90, 12),
                Coin("t_rock", "바위", "이끼 낀 작은 바위.",
                    IslandObjectCategory.Terrain, 1, 1, 45, 2),
                Coin("t_boulder", "큰 바위", "곤충이 숨기 좋은 큰 바위.",
                    IslandObjectCategory.Terrain, 2, 2, 110, 4),
                Coin("t_pond", "연못", "수면에 하늘이 비치는 작은 연못.",
                    IslandObjectCategory.Terrain, 3, 3, 320, 10),
                Coin("t_flowerbed", "꽃밭", "색색의 꽃이 핀 꽃밭. 지나다닐 수 있습니다.",
                    IslandObjectCategory.Terrain, 2, 2, 90, 6, blocks: false),
                Coin("t_bush", "덤불", "둥근 덤불. 지나다닐 수 있습니다.",
                    IslandObjectCategory.Terrain, 1, 1, 35, 2, blocks: false),

                // ── 도구(설비) ──
                Coin("o_feeder", "먹이통", "과일 조각을 담아 두는 먹이통. 생산량이 늘어납니다.",
                    IslandObjectCategory.Tool, 1, 1, 150, 2, IslandEffectKind.YieldBonus, 0.05f),
                Coin("o_water", "급수대", "맑은 물이 고이는 급수대. 생산량이 늘어납니다.",
                    IslandObjectCategory.Tool, 1, 1, 150, 2, IslandEffectKind.YieldBonus, 0.05f),
                Coin("o_basket", "수확 바구니", "수확물을 조금 더 오래 쌓아 둡니다.",
                    IslandObjectCategory.Tool, 1, 1, 250, 2, IslandEffectKind.CapHours, 2f),
                Coin("o_honeypot", "꿀단지", "달콤한 꿀단지. 생산량이 늘어납니다.",
                    IslandObjectCategory.Tool, 1, 1, 400, 3, IslandEffectKind.YieldBonus, 0.06f),
                Gem("o_sprinkler", "물뿌리개", "섬에 물을 골고루 뿌립니다. 친밀도가 훨씬 빨리 쌓입니다.",
                    IslandObjectCategory.Tool, 2, 1, 120, 4, IslandEffectKind.BondSpeed, 0.5f),
            };
        }

        private static IslandObjectDef Coin(string id, string name, string desc, IslandObjectCategory category,
            int width, int depth, int coinPrice, int comfort,
            IslandEffectKind effect = IslandEffectKind.None, float effectValue = 0f,
            int sizeLevel = 0, bool blocks = true)
        {
            return new IslandObjectDef
            {
                id = id, displayName = name, description = desc, category = category,
                width = width, depth = depth, coinPrice = coinPrice, gemPrice = 0, comfort = comfort,
                effect = effect, effectValue = effectValue, requiredSizeLevel = sizeLevel, blocksMovement = blocks,
            };
        }

        private static IslandObjectDef Gem(string id, string name, string desc, IslandObjectCategory category,
            int width, int depth, int gemPrice, int comfort,
            IslandEffectKind effect = IslandEffectKind.None, float effectValue = 0f,
            int sizeLevel = 0, bool blocks = true)
        {
            return new IslandObjectDef
            {
                id = id, displayName = name, description = desc, category = category,
                width = width, depth = depth, coinPrice = 0, gemPrice = gemPrice, comfort = comfort,
                effect = effect, effectValue = effectValue, requiredSizeLevel = sizeLevel, blocksMovement = blocks,
            };
        }
    }
}
