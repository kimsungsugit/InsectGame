using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InsectGame.Core
{
    public class CharacterOutfitManager : MonoBehaviour
    {
        public static CharacterOutfitManager Instance { get; private set; }

        private OutfitItem[] allOutfits;
        private Dictionary<string, OutfitItem> outfitLookup;
        private Dictionary<OutfitSlot, string> equippedItems;
        private HashSet<string> ownedItems;

        private PlayerCurrencyWallet wallet;

        public event System.Action OutfitChanged;

        private const string EquipKey = "InsectGame.Equipped";
        private const string OwnedKey = "InsectGame.OwnedOutfits";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            Initialize();
            LoadOwnership();
            LoadEquipment();
        }

        public void AutoWire(PlayerCurrencyWallet w)
        {
            if (wallet == null) wallet = w;
        }

        private void Initialize()
        {
            allOutfits = new OutfitItem[]
            {
                // ── Hat (6) ──
                MakeItem("hat_cap", "탐험가 캡", "기본 탐험용 모자", OutfitSlot.Hat,
                    new Color(1.0f, 0.6f, 0.2f), Color.white, 0, true, "",
                    new OutfitStatBonus { captureChanceBonus = 0.01f }),
                MakeItem("hat_straw", "밀짚모자", "시원한 밀짚모자", OutfitSlot.Hat,
                    new Color(0.95f, 0.9f, 0.5f), Color.white, 150, false, "",
                    new OutfitStatBonus { captureChanceBonus = 0.01f, expMultiplier = 0.01f }),
                MakeItem("hat_safari", "사파리 헬멧", "전문 탐험가의 헬멧", OutfitSlot.Hat,
                    new Color(0.6f, 0.55f, 0.4f), Color.white, 300, false, "",
                    new OutfitStatBonus { captureChanceBonus = 0.02f }),
                MakeItem("hat_flower", "꽃 왕관", "정원에서 얻은 꽃 왕관", OutfitSlot.Hat,
                    new Color(1.0f, 0.6f, 0.7f), Color.white, 0, false, "region_garden",
                    new OutfitStatBonus { captureChanceBonus = 0.01f, rareSpawnBonus = 0.01f }),
                MakeItem("hat_beetle", "장수풍뎅이 투구", "멋진 투구 모양 모자", OutfitSlot.Hat,
                    new Color(0.45f, 0.3f, 0.15f), Color.white, 600, false, "",
                    new OutfitStatBonus { captureChanceBonus = 0.02f, defBonus = 0.01f }),
                MakeItem("hat_none", "모자 없음", "모자를 벗습니다", OutfitSlot.Hat,
                    Color.clear, Color.clear, 0, true, ""),

                // ── Top (5) ──
                MakeItem("top_shirt", "기본 셔츠", "깔끔한 흰 셔츠", OutfitSlot.Top,
                    Color.white, Color.white, 0, true, "",
                    new OutfitStatBonus { atkBonus = 0.01f }),
                MakeItem("top_polo", "폴로 셔츠", "시원한 하늘색 폴로", OutfitSlot.Top,
                    new Color(0.53f, 0.81f, 0.98f), Color.white, 180, false, "",
                    new OutfitStatBonus { atkBonus = 0.01f, defBonus = 0.01f }),
                MakeItem("top_vest", "탐험 조끼", "주머니가 많은 조끼", OutfitSlot.Top,
                    new Color(0.6f, 0.55f, 0.4f), Color.white, 250, false, "",
                    new OutfitStatBonus { atkBonus = 0.02f }),
                MakeItem("top_stripe", "줄무늬 티", "파란 줄무늬 티셔츠", OutfitSlot.Top,
                    new Color(0.2f, 0.4f, 0.8f), Color.white, 200, false, "",
                    new OutfitStatBonus { atkBonus = 0.01f, moveSpeedBonus = 0.02f }),
                MakeItem("top_lab", "연구원 가운", "곤충 연구원의 가운", OutfitSlot.Top,
                    Color.white, new Color(0.8f, 0.8f, 0.8f), 0, false, "level_15",
                    new OutfitStatBonus { expMultiplier = 0.03f, atkBonus = 0.01f }),

                // ── Bottom (5) ──
                MakeItem("bot_pants", "기본 바지", "무난한 바지", OutfitSlot.Bottom,
                    new Color(0.25f, 0.25f, 0.28f), Color.white, 0, true, "",
                    new OutfitStatBonus { defBonus = 0.01f }),
                MakeItem("bot_shorts", "반바지", "활동적인 반바지", OutfitSlot.Bottom,
                    new Color(0.87f, 0.82f, 0.7f), Color.white, 120, false, "",
                    new OutfitStatBonus { moveSpeedBonus = 0.02f }),
                MakeItem("bot_cargo", "카고 팬츠", "수납이 많은 카고 팬츠", OutfitSlot.Bottom,
                    new Color(0.6f, 0.55f, 0.4f), Color.white, 250, false, "",
                    new OutfitStatBonus { defBonus = 0.02f }),
                MakeItem("bot_jeans", "청바지", "튼튼한 청바지", OutfitSlot.Bottom,
                    new Color(0.15f, 0.25f, 0.55f), Color.white, 150, false, "",
                    new OutfitStatBonus { defBonus = 0.01f, atkBonus = 0.01f }),
                MakeItem("bot_overalls", "멜빵바지", "귀여운 멜빵바지", OutfitSlot.Bottom,
                    new Color(0.5f, 0.35f, 0.2f), Color.white, 300, false, "",
                    new OutfitStatBonus { defBonus = 0.02f, expMultiplier = 0.01f }),

                // ── Outerwear (5) ──
                MakeItem("outer_jacket", "탐험가 자켓", "기본 탐험 자켓", OutfitSlot.Outerwear,
                    new Color(0.16f, 0.32f, 0.72f), Color.white, 0, true, "",
                    new OutfitStatBonus { atkBonus = 0.01f, defBonus = 0.01f }),
                MakeItem("outer_raincoat", "비옷", "비 오는 날의 필수품", OutfitSlot.Outerwear,
                    new Color(1.0f, 0.9f, 0.2f), Color.white, 250, false, "",
                    new OutfitStatBonus { defBonus = 0.02f }),
                MakeItem("outer_windbreaker", "바람막이", "가벼운 바람막이", OutfitSlot.Outerwear,
                    new Color(0.9f, 0.2f, 0.2f), Color.white, 300, false, "",
                    new OutfitStatBonus { moveSpeedBonus = 0.03f }),
                MakeItem("outer_labcoat", "연구원 코트", "연구소의 흰 코트", OutfitSlot.Outerwear,
                    Color.white, new Color(0.9f, 0.9f, 0.9f), 500, false, "",
                    new OutfitStatBonus { expMultiplier = 0.03f }),
                MakeItem("outer_none", "겉옷 없음", "겉옷을 벗습니다", OutfitSlot.Outerwear,
                    Color.clear, Color.clear, 0, true, ""),

                // ── Shoes (4) ──
                MakeItem("shoe_boots", "탐험 부츠", "든든한 탐험용 부츠", OutfitSlot.Shoes,
                    new Color(0.35f, 0.22f, 0.1f), Color.white, 0, true, "",
                    new OutfitStatBonus { moveSpeedBonus = 0.03f }),
                MakeItem("shoe_sneakers", "운동화", "가벼운 운동화", OutfitSlot.Shoes,
                    Color.white, new Color(0.9f, 0.2f, 0.2f), 150, false, "",
                    new OutfitStatBonus { moveSpeedBonus = 0.05f }),
                MakeItem("shoe_sandals", "샌들", "편안한 샌들", OutfitSlot.Shoes,
                    new Color(0.5f, 0.35f, 0.2f), Color.white, 90, false, "",
                    new OutfitStatBonus { moveSpeedBonus = 0.03f }),
                MakeItem("shoe_waders", "장화", "물가 탐험용 장화", OutfitSlot.Shoes,
                    new Color(0.2f, 0.6f, 0.2f), Color.white, 0, false, "region_pond",
                    new OutfitStatBonus { moveSpeedBonus = 0.04f, captureChanceBonus = 0.01f }),

                // ── Backpack (5) ──
                MakeItem("bag_basic", "기본 배낭", "가벼운 기본 배낭", OutfitSlot.Backpack,
                    new Color(1.0f, 0.6f, 0.2f), Color.white, 0, true, "",
                    new OutfitStatBonus { candyMultiplier = 0.02f }),
                MakeItem("bag_big", "대형 배낭", "넉넉한 대형 배낭", OutfitSlot.Backpack,
                    new Color(0.2f, 0.5f, 0.2f), Color.white, 300, false, "",
                    new OutfitStatBonus { candyMultiplier = 0.03f }),
                MakeItem("bag_satchel", "어깨가방", "클래식 어깨가방", OutfitSlot.Backpack,
                    new Color(0.5f, 0.35f, 0.2f), Color.white, 250, false, "",
                    new OutfitStatBonus { candyMultiplier = 0.02f, expMultiplier = 0.01f }),
                MakeItem("bag_science", "연구 장비함", "정밀 장비 수납함", OutfitSlot.Backpack,
                    new Color(0.5f, 0.5f, 0.55f), Color.white, 500, false, "",
                    new OutfitStatBonus { expMultiplier = 0.03f, candyMultiplier = 0.02f }),
                MakeItem("bag_none", "가방 없음", "가방을 내려놓습니다", OutfitSlot.Backpack,
                    Color.clear, Color.clear, 0, true, ""),

                // ── Tool (5) ──
                MakeItem("tool_net", "잠자리채", "기본 잠자리채", OutfitSlot.Tool,
                    new Color(0.6f, 0.4f, 0.2f), new Color(0.8f, 0.8f, 0.8f), 0, true, "",
                    new OutfitStatBonus { captureChanceBonus = 0.01f }),
                MakeItem("tool_golden_net", "황금 잠자리채", "빛나는 황금 잠자리채", OutfitSlot.Tool,
                    new Color(1.0f, 0.84f, 0.0f), new Color(1.0f, 0.9f, 0.4f), 1000, false, "",
                    new OutfitStatBonus { captureChanceBonus = 0.02f, rareSpawnBonus = 0.01f }),
                MakeItem("tool_magnify", "돋보기", "관찰용 돋보기", OutfitSlot.Tool,
                    new Color(0.75f, 0.75f, 0.8f), new Color(0.6f, 0.85f, 1.0f), 300, false, "",
                    new OutfitStatBonus { expMultiplier = 0.02f }),
                MakeItem("tool_camera", "관찰 카메라", "곤충 촬영용 카메라", OutfitSlot.Tool,
                    new Color(0.15f, 0.15f, 0.15f), new Color(0.3f, 0.3f, 0.3f), 600, false, "",
                    new OutfitStatBonus { captureChanceBonus = 0.01f, expMultiplier = 0.01f }),
                MakeItem("tool_none", "도구 없음", "도구를 내려놓습니다", OutfitSlot.Tool,
                    Color.clear, Color.clear, 0, true, ""),

                // ── Accessory (5) ──
                MakeItem("acc_none", "없음", "악세서리 없음", OutfitSlot.Accessory,
                    Color.clear, Color.clear, 0, true, ""),
                MakeItem("acc_glasses", "뿔테 안경", "지적인 뿔테 안경", OutfitSlot.Accessory,
                    new Color(0.1f, 0.1f, 0.1f), Color.white, 150, false, "",
                    new OutfitStatBonus { expMultiplier = 0.02f }),
                MakeItem("acc_scarf", "스카프", "빨간 스카프", OutfitSlot.Accessory,
                    new Color(0.9f, 0.15f, 0.15f), Color.white, 180, false, "",
                    new OutfitStatBonus { defBonus = 0.01f, atkBonus = 0.01f }),
                MakeItem("acc_badge", "곤충박사 배지", "곤충박사 인증 배지", OutfitSlot.Accessory,
                    new Color(1.0f, 0.84f, 0.0f), Color.white, 0, false, "quest_q_complete",
                    new OutfitStatBonus { captureChanceBonus = 0.02f, expMultiplier = 0.02f }),
                MakeItem("acc_pendant", "곤충 펜던트", "곤충 모양 펜던트", OutfitSlot.Accessory,
                    new Color(0.2f, 0.7f, 0.3f), Color.white, 300, false, "",
                    new OutfitStatBonus { rareSpawnBonus = 0.02f }),

                // ══════════════════════════════════════
                //  프리미엄 의상 (보석으로 구매)
                // ══════════════════════════════════════

                // ── 프리미엄 모자 (3) ──
                MakePremiumItem("hat_crown", "곤충왕 왕관", "곤충왕의 황금 왕관", OutfitSlot.Hat,
                    new Color(1f, 0.84f, 0f), new Color(0.9f, 0.6f, 0.1f), 1000,
                    new OutfitStatBonus { captureChanceBonus = 0.03f, atkBonus = 0.02f }),
                MakePremiumItem("hat_butterfly_wing", "나비 날개 머리띠", "나비 날개가 달린 머리띠", OutfitSlot.Hat,
                    new Color(0.4f, 0.6f, 1f), new Color(0.8f, 0.4f, 1f), 600,
                    new OutfitStatBonus { captureChanceBonus = 0.02f, rareSpawnBonus = 0.02f }),
                MakePremiumItem("hat_explorer_pro", "프로 탐험가 모자", "최고급 탐험가의 증표", OutfitSlot.Hat,
                    new Color(0.3f, 0.15f, 0.05f), new Color(1f, 0.7f, 0.2f), 800,
                    new OutfitStatBonus { captureChanceBonus = 0.03f }),

                // ── 프리미엄 상의 (3) ──
                MakePremiumItem("top_galaxy", "갤럭시 티셔츠", "별이 빛나는 우주 패턴", OutfitSlot.Top,
                    new Color(0.1f, 0.05f, 0.3f), new Color(0.5f, 0.3f, 0.9f), 800,
                    new OutfitStatBonus { atkBonus = 0.02f, rareSpawnBonus = 0.01f }),
                MakePremiumItem("top_nature", "숲의 수호자 상의", "자연의 힘이 깃든 상의", OutfitSlot.Top,
                    new Color(0.15f, 0.5f, 0.2f), new Color(0.4f, 0.8f, 0.3f), 600,
                    new OutfitStatBonus { atkBonus = 0.02f, defBonus = 0.01f }),
                MakePremiumItem("top_flame", "화염 셔츠", "불꽃 패턴의 강렬한 셔츠", OutfitSlot.Top,
                    new Color(0.9f, 0.3f, 0.05f), new Color(1f, 0.6f, 0.1f), 700,
                    new OutfitStatBonus { atkBonus = 0.03f }),

                // ── 프리미엄 하의 (2) ──
                MakePremiumItem("bot_galaxy", "갤럭시 팬츠", "우주 패턴과 어울리는 바지", OutfitSlot.Bottom,
                    new Color(0.08f, 0.05f, 0.25f), new Color(0.3f, 0.2f, 0.6f), 600,
                    new OutfitStatBonus { defBonus = 0.02f, rareSpawnBonus = 0.01f }),
                MakePremiumItem("bot_golden", "황금 바지", "빛나는 금빛 바지", OutfitSlot.Bottom,
                    new Color(0.85f, 0.7f, 0.15f), new Color(1f, 0.9f, 0.3f), 800,
                    new OutfitStatBonus { defBonus = 0.03f, candyMultiplier = 0.02f }),

                // ── 프리미엄 겉옷 (3) ──
                MakePremiumItem("outer_legendary", "전설의 망토", "전설의 곤충 사냥꾼 망토", OutfitSlot.Outerwear,
                    new Color(0.5f, 0.1f, 0.6f), new Color(0.8f, 0.3f, 1f), 1200,
                    new OutfitStatBonus { atkBonus = 0.03f, defBonus = 0.02f }),
                MakePremiumItem("outer_crystal", "크리스탈 자켓", "수정처럼 빛나는 자켓", OutfitSlot.Outerwear,
                    new Color(0.6f, 0.85f, 1f), new Color(0.8f, 0.95f, 1f), 1000,
                    new OutfitStatBonus { defBonus = 0.03f, rareSpawnBonus = 0.01f }),
                MakePremiumItem("outer_shadow", "그림자 코트", "어둠의 기운이 감도는 코트", OutfitSlot.Outerwear,
                    new Color(0.1f, 0.1f, 0.15f), new Color(0.2f, 0.15f, 0.3f), 900,
                    new OutfitStatBonus { atkBonus = 0.02f, moveSpeedBonus = 0.03f }),

                // ── 프리미엄 신발 (2) ──
                MakePremiumItem("shoe_rocket", "로켓 부츠", "이동속도가 빨라보이는 부츠", OutfitSlot.Shoes,
                    new Color(0.8f, 0.2f, 0.1f), new Color(1f, 0.5f, 0.1f), 800,
                    new OutfitStatBonus { moveSpeedBonus = 0.08f }),
                MakePremiumItem("shoe_crystal", "크리스탈 구두", "투명하게 빛나는 구두", OutfitSlot.Shoes,
                    new Color(0.7f, 0.9f, 1f), new Color(0.9f, 0.95f, 1f), 600,
                    new OutfitStatBonus { moveSpeedBonus = 0.05f, rareSpawnBonus = 0.02f }),

                // ── 프리미엄 가방 (2) ──
                MakePremiumItem("bag_dragon", "드래곤 배낭", "용 모양의 멋진 배낭", OutfitSlot.Backpack,
                    new Color(0.6f, 0.15f, 0.1f), new Color(0.9f, 0.3f, 0.1f), 1000,
                    new OutfitStatBonus { candyMultiplier = 0.05f, atkBonus = 0.01f }),
                MakePremiumItem("bag_fairy", "요정 날개 가방", "요정 날개가 달린 가방", OutfitSlot.Backpack,
                    new Color(0.7f, 1f, 0.8f), new Color(0.9f, 1f, 0.95f), 900,
                    new OutfitStatBonus { expMultiplier = 0.04f, candyMultiplier = 0.03f }),

                // ── 프리미엄 도구 (2) ──
                MakePremiumItem("tool_diamond_net", "다이아몬드 잠자리채", "최상급 다이아몬드 잠자리채", OutfitSlot.Tool,
                    new Color(0.7f, 0.9f, 1f), new Color(1f, 1f, 1f), 2000,
                    new OutfitStatBonus { captureChanceBonus = 0.04f, rareSpawnBonus = 0.02f }),
                MakePremiumItem("tool_laser", "레이저 포인터", "첨단 곤충 관찰 장비", OutfitSlot.Tool,
                    new Color(0.2f, 0.2f, 0.2f), new Color(1f, 0.1f, 0.1f), 1200,
                    new OutfitStatBonus { captureChanceBonus = 0.02f, expMultiplier = 0.03f }),

                // ── 프리미엄 악세서리 (3) ──
                MakePremiumItem("acc_wings", "곤충 날개 장식", "등에 달린 반짝이는 곤충 날개", OutfitSlot.Accessory,
                    new Color(0.5f, 0.8f, 1f), new Color(0.3f, 0.6f, 0.9f), 1000,
                    new OutfitStatBonus { moveSpeedBonus = 0.03f, captureChanceBonus = 0.01f }),
                MakePremiumItem("acc_aura", "신비의 오라", "몸 주위에 빛나는 오라 효과", OutfitSlot.Accessory,
                    new Color(1f, 0.9f, 0.3f), new Color(1f, 0.7f, 0.1f), 1500,
                    new OutfitStatBonus { rareSpawnBonus = 0.03f, atkBonus = 0.02f }),
                MakePremiumItem("acc_halo", "천사의 후광", "머리 위에 빛나는 후광", OutfitSlot.Accessory,
                    new Color(1f, 1f, 0.7f), new Color(1f, 0.95f, 0.5f), 1200,
                    new OutfitStatBonus { expMultiplier = 0.03f, defBonus = 0.02f }),
            };

            outfitLookup = new Dictionary<string, OutfitItem>();
            foreach (OutfitItem item in allOutfits)
            {
                outfitLookup[item.itemId] = item;
            }

            equippedItems = new Dictionary<OutfitSlot, string>();
            ownedItems = new HashSet<string>();

            // 기본 제공 아이템 소유 처리
            foreach (OutfitItem item in allOutfits)
            {
                if (item.unlockedByDefault)
                {
                    ownedItems.Add(item.itemId);
                }
            }
        }

        private static OutfitItem MakeItem(string id, string name, string desc, OutfitSlot slot,
            Color primary, Color secondary, int price, bool defaultOwned, string condition,
            OutfitStatBonus bonus = default)
        {
            return new OutfitItem
            {
                itemId = id,
                displayName = name,
                description = desc,
                slot = slot,
                primaryColor = primary,
                secondaryColor = secondary,
                price = price,
                gemPrice = 0,
                isPremium = false,
                unlockedByDefault = defaultOwned,
                unlockCondition = condition,
                statBonus = bonus
            };
        }

        private static OutfitItem MakePremiumItem(string id, string name, string desc, OutfitSlot slot,
            Color primary, Color secondary, int gemPrice, OutfitStatBonus bonus = default)
        {
            return new OutfitItem
            {
                itemId = id,
                displayName = name,
                description = desc,
                slot = slot,
                primaryColor = primary,
                secondaryColor = secondary,
                price = 0,
                gemPrice = gemPrice,
                isPremium = true,
                unlockedByDefault = false,
                unlockCondition = "",
                statBonus = bonus
            };
        }

        // ── 장착 ──

        public void Equip(string itemId)
        {
            if (!outfitLookup.ContainsKey(itemId)) return;
            OutfitItem item = outfitLookup[itemId];
            if (!ownedItems.Contains(itemId)) return;
            equippedItems[item.slot] = itemId;
            SaveEquipment();
            ApplyToCharacter();
            OutfitChanged?.Invoke();
        }

        public bool TryPurchase(string itemId)
        {
            if (!outfitLookup.ContainsKey(itemId)) return false;
            OutfitItem item = outfitLookup[itemId];
            if (ownedItems.Contains(itemId)) return false;

            bool isMaster = AuthManager.Instance != null && AuthManager.Instance.IsMasterAccount;
            if (!isMaster)
            {
                if (item.price <= 0) return false;
                if (wallet == null || wallet.Coins < item.price) return false;
                if (!wallet.SpendCoins(item.price)) return false;
            }
            ownedItems.Add(itemId);
            SaveOwnership();
            return true;
        }

        public bool TryPurchaseWithGems(string itemId)
        {
            if (!outfitLookup.ContainsKey(itemId)) return false;
            OutfitItem item = outfitLookup[itemId];
            if (ownedItems.Contains(itemId)) return false;

            bool isMaster = AuthManager.Instance != null && AuthManager.Instance.IsMasterAccount;
            if (!isMaster)
            {
                if (item.gemPrice <= 0) return false;
                if (CashShopManager.Instance == null || CashShopManager.Instance.Gems < item.gemPrice) return false;
                CashShopManager.Instance.AddGems(-item.gemPrice);
            }
            ownedItems.Add(itemId);
            SaveOwnership();
            return true;
        }

        public OutfitItem GetEquipped(OutfitSlot slot)
        {
            if (equippedItems.TryGetValue(slot, out string itemId))
            {
                if (outfitLookup.TryGetValue(itemId, out OutfitItem item))
                {
                    return item;
                }
            }
            return null;
        }

        public OutfitItem[] GetItemsForSlot(OutfitSlot slot)
        {
            return allOutfits.Where(o => o.slot == slot).ToArray();
        }

        public bool IsOwned(string itemId)
        {
            return ownedItems.Contains(itemId);
        }

        public bool IsEquipped(string itemId)
        {
            if (!outfitLookup.ContainsKey(itemId)) return false;
            OutfitItem item = outfitLookup[itemId];
            return equippedItems.TryGetValue(item.slot, out string equipped) && equipped == itemId;
        }

        public void UnlockItem(string itemId)
        {
            if (outfitLookup.ContainsKey(itemId) && !ownedItems.Contains(itemId))
            {
                ownedItems.Add(itemId);
                SaveOwnership();
            }
        }

        // ── 캐릭터 적용 ──

        public void ApplyToCharacter()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) return;

            // 모자
            OutfitItem hat = GetEquipped(OutfitSlot.Hat);
            ApplyPartColor(player, "Cap", hat != null ? hat.primaryColor : Color.clear);
            ApplyPartColor(player, "CapBrim", hat != null ? hat.primaryColor : Color.clear);

            // 상의
            OutfitItem top = GetEquipped(OutfitSlot.Top);
            ApplyPartColor(player, "Shirt", top != null ? top.primaryColor : Color.white);

            // 겉옷
            OutfitItem outer = GetEquipped(OutfitSlot.Outerwear);
            ApplyPartColor(player, "Body", outer != null ? outer.primaryColor : Color.blue);
            ApplyPartColor(player, "ArmL", outer != null ? outer.primaryColor : Color.blue);
            ApplyPartColor(player, "ArmR", outer != null ? outer.primaryColor : Color.blue);

            // 하의
            OutfitItem bot = GetEquipped(OutfitSlot.Bottom);
            ApplyPartColor(player, "LegL", bot != null ? bot.primaryColor : Color.gray);
            ApplyPartColor(player, "LegR", bot != null ? bot.primaryColor : Color.gray);

            // 신발
            OutfitItem shoe = GetEquipped(OutfitSlot.Shoes);
            ApplyPartColor(player, "BootL", shoe != null ? shoe.primaryColor : Color.black);
            ApplyPartColor(player, "BootR", shoe != null ? shoe.primaryColor : Color.black);

            // 가방
            OutfitItem bag = GetEquipped(OutfitSlot.Backpack);
            ApplyPartColor(player, "Backpack", bag != null ? bag.primaryColor : Color.clear);

            // 도구
            OutfitItem tool = GetEquipped(OutfitSlot.Tool);
            ApplyPartColor(player, "NetHandle", tool != null ? tool.primaryColor : Color.clear);
            ApplyPartColor(player, "NetRing", tool != null ? tool.secondaryColor : Color.clear);
        }

        private void ApplyPartColor(GameObject root, string partName, Color color)
        {
            Transform part = FindDeep(root.transform, partName);
            if (part == null) return;

            if (color.a < 0.01f)
            {
                part.gameObject.SetActive(false);
                return;
            }

            part.gameObject.SetActive(true);
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = renderer.material;
                mat.color = color;
            }
        }

        private Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ── 저장 / 로드 ──

        private void SaveEquipment()
        {
            List<string> items = new List<string>();
            foreach (var kvp in equippedItems)
            {
                items.Add(kvp.Value);
            }
            PlayerPrefs.SetString(EquipKey, string.Join(",", items));
            PlayerPrefs.Save();
        }

        private void LoadEquipment()
        {
            string saved = PlayerPrefs.GetString(EquipKey, "");
            if (string.IsNullOrEmpty(saved))
            {
                // 기본 장착
                Equip("hat_cap");
                Equip("top_shirt");
                Equip("bot_pants");
                Equip("outer_jacket");
                Equip("shoe_boots");
                Equip("bag_basic");
                Equip("tool_net");
                Equip("acc_none");
                return;
            }

            string[] ids = saved.Split(',');
            foreach (string id in ids)
            {
                string trimmed = id.Trim();
                if (trimmed.Length > 0 && outfitLookup.ContainsKey(trimmed))
                {
                    OutfitItem item = outfitLookup[trimmed];
                    equippedItems[item.slot] = trimmed;
                }
            }
            ApplyToCharacter();
        }

        private void SaveOwnership()
        {
            string joined = string.Join(",", ownedItems);
            PlayerPrefs.SetString(OwnedKey, joined);
            PlayerPrefs.Save();
        }

        private void LoadOwnership()
        {
            string saved = PlayerPrefs.GetString(OwnedKey, "");
            if (string.IsNullOrEmpty(saved)) return;

            string[] ids = saved.Split(',');
            foreach (string id in ids)
            {
                string trimmed = id.Trim();
                if (trimmed.Length > 0)
                {
                    ownedItems.Add(trimmed);
                }
            }
        }
    }
}
