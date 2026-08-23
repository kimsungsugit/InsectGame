using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    public class CharacterOutfitManager : MonoBehaviour, ICloudReloadable
    {
        public static CharacterOutfitManager Instance { get; private set; }

        private OutfitItem[] allOutfits;
        private Dictionary<string, OutfitItem> outfitLookup;
        // 슬롯별 사전 분류 — GetItemsForSlot이 OnGUI 경로라 매 패스 LINQ를 돌 수 없다.
        private Dictionary<OutfitSlot, OutfitItem[]> slotLookup;
        private Dictionary<OutfitSlot, string> equippedItems;
        private HashSet<string> ownedItems;

        private PlayerCurrencyWallet wallet;

        public event System.Action OutfitChanged;

        private static string EquipKey => SaveScope.PrefsKey("InsectGame.Equipped");
        private static string OwnedKey => SaveScope.PrefsKey("InsectGame.OwnedOutfits");

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
        // 파기될 때 static을 비운다. 안 그러면 `Instance != null`(UnityEngine.Object의 파괴 검사)과
        // `Instance?.`(진짜 null 검사)가 서로 다른 답을 내고, 후자는 파기된 객체로 호출이 들어간다.
        // `singleton_lint.py`가 이 짝을 강제한다.
        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        public void AutoWire(PlayerCurrencyWallet w)
        {
            if (wallet == null) wallet = w;
        }

        // 클라우드 로드 후 PlayerPrefs(소유/장착 의상)를 다시 읽어 인메모리 갱신 + 외형 재적용.
        // OutfitChanged 발화 → PlayerVisualBuilder/PortraitRenderer가 클라우드 의상으로 재구성.
        public void ReloadFromDisk()
        {
            LoadOwnership();
            LoadEquipment();
            OutfitChanged?.Invoke();
        }

        private void Initialize()
        {
            allOutfits = BuildCatalog();

            outfitLookup = new Dictionary<string, OutfitItem>();
            foreach (OutfitItem item in allOutfits)
            {
                outfitLookup[item.itemId] = item;
            }

            // 슬롯별 캐시 1회 구성 (GetItemsForSlot의 매 프레임 LINQ 제거).
            Dictionary<OutfitSlot, List<OutfitItem>> bySlot = new Dictionary<OutfitSlot, List<OutfitItem>>();
            foreach (OutfitItem item in allOutfits)
            {
                if (!bySlot.TryGetValue(item.slot, out List<OutfitItem> list))
                {
                    list = new List<OutfitItem>();
                    bySlot[item.slot] = list;
                }
                list.Add(item);
            }
            slotLookup = new Dictionary<OutfitSlot, OutfitItem[]>(bySlot.Count);
            foreach (KeyValuePair<OutfitSlot, List<OutfitItem>> pair in bySlot)
            {
                slotLookup[pair.Key] = pair.Value.ToArray();
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

        /// <summary>
        /// 의상 카탈로그 95벌. MonoBehaviour 상태에 손대지 않는 순수 팩토리다 —
        /// EditMode 테스트가 씬 없이 이걸 읽어 <see cref="OutfitShapeLibrary"/>의 레시피가
        /// 실재하지 않는 itemId를 물고 있는지 검증한다(옛 2D 카드의 `hat_beanie`처럼
        /// 존재하지 않는 id를 분기하던 dead code를 구조적으로 차단).
        /// </summary>
        internal static OutfitItem[] BuildCatalog()
        {
            return new OutfitItem[]
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

                // ══════════════════════════════════════
                //  코스튬 세트 (프리미엄)
                // ══════════════════════════════════════

                // ── 카우보이 세트 ──
                MakePremiumItem("hat_cowboy", "카우보이 모자", "서부의 바람이 느껴지는 가죽 모자", OutfitSlot.Hat,
                    new Color(0.45f, 0.3f, 0.15f), new Color(0.35f, 0.22f, 0.1f), 800,
                    new OutfitStatBonus { captureChanceBonus = 0.02f, moveSpeedBonus = 0.02f }),
                MakePremiumItem("top_cowboy", "카우보이 조끼", "프린지 장식의 가죽 조끼", OutfitSlot.Top,
                    new Color(0.5f, 0.35f, 0.15f), new Color(0.4f, 0.25f, 0.1f), 700,
                    new OutfitStatBonus { atkBonus = 0.02f, defBonus = 0.01f }),
                MakePremiumItem("bot_cowboy", "카우보이 팬츠", "가죽 챕스가 달린 청바지", OutfitSlot.Bottom,
                    new Color(0.2f, 0.3f, 0.5f), new Color(0.45f, 0.3f, 0.15f), 600,
                    new OutfitStatBonus { defBonus = 0.02f, moveSpeedBonus = 0.02f }),
                MakePremiumItem("shoe_cowboy", "카우보이 부츠", "박차가 달린 가죽 부츠", OutfitSlot.Shoes,
                    new Color(0.4f, 0.25f, 0.1f), new Color(0.7f, 0.7f, 0.7f), 600,
                    new OutfitStatBonus { moveSpeedBonus = 0.06f }),
                MakePremiumItem("tool_lasso", "올가미", "곤충을 잡는 카우보이 올가미", OutfitSlot.Tool,
                    new Color(0.6f, 0.5f, 0.3f), new Color(0.5f, 0.4f, 0.2f), 900,
                    new OutfitStatBonus { captureChanceBonus = 0.03f }),
                MakePremiumItem("acc_bandana", "빨간 반다나", "서부 스타일 반다나", OutfitSlot.Accessory,
                    new Color(0.85f, 0.15f, 0.1f), new Color(0.7f, 0.1f, 0.05f), 500,
                    new OutfitStatBonus { atkBonus = 0.02f }),

                // ── 히어로 세트 (스파이더맨 오마주) ──
                MakePremiumItem("hat_hero_mask", "히어로 마스크", "정의의 거미줄 마스크", OutfitSlot.Hat,
                    new Color(0.8f, 0.1f, 0.1f), new Color(0.15f, 0.15f, 0.4f), 1000,
                    new OutfitStatBonus { atkBonus = 0.03f, moveSpeedBonus = 0.02f }),
                MakePremiumItem("top_hero_suit", "히어로 슈트 상의", "거미줄 패턴의 강화 슈트", OutfitSlot.Top,
                    new Color(0.8f, 0.1f, 0.1f), new Color(0.1f, 0.1f, 0.35f), 900,
                    new OutfitStatBonus { atkBonus = 0.03f, defBonus = 0.02f }),
                MakePremiumItem("bot_hero_suit", "히어로 슈트 하의", "탄력 있는 강화 타이츠", OutfitSlot.Bottom,
                    new Color(0.1f, 0.1f, 0.35f), new Color(0.8f, 0.1f, 0.1f), 800,
                    new OutfitStatBonus { defBonus = 0.02f, moveSpeedBonus = 0.04f }),
                MakePremiumItem("tool_web_shooter", "거미줄 발사기", "곤충을 잡는 거미줄 발사 장치", OutfitSlot.Tool,
                    new Color(0.15f, 0.15f, 0.15f), new Color(0.8f, 0.1f, 0.1f), 1500,
                    new OutfitStatBonus { captureChanceBonus = 0.04f, atkBonus = 0.01f }),
                MakePremiumItem("acc_spider_emblem", "거미 엠블럼", "가슴에 빛나는 거미 문양", OutfitSlot.Accessory,
                    new Color(0.1f, 0.1f, 0.1f), new Color(0.9f, 0.1f, 0.1f), 700,
                    new OutfitStatBonus { atkBonus = 0.02f, defBonus = 0.01f }),

                // ── 닌자 세트 ──
                MakePremiumItem("hat_ninja", "닌자 두건", "그림자에 녹아드는 검은 두건", OutfitSlot.Hat,
                    new Color(0.08f, 0.08f, 0.1f), new Color(0.15f, 0.15f, 0.2f), 900,
                    new OutfitStatBonus { moveSpeedBonus = 0.04f, captureChanceBonus = 0.01f }),
                MakePremiumItem("top_ninja", "닌자 상의", "어둠의 닌자 도복 상의", OutfitSlot.Top,
                    new Color(0.1f, 0.1f, 0.12f), new Color(0.2f, 0.15f, 0.25f), 800,
                    new OutfitStatBonus { atkBonus = 0.03f, moveSpeedBonus = 0.02f }),
                MakePremiumItem("bot_ninja", "닌자 하의", "가볍고 빠른 닌자 바지", OutfitSlot.Bottom,
                    new Color(0.1f, 0.1f, 0.12f), new Color(0.15f, 0.15f, 0.18f), 700,
                    new OutfitStatBonus { moveSpeedBonus = 0.05f, defBonus = 0.01f }),
                MakePremiumItem("tool_shuriken", "수리검", "곤충을 기절시키는 닌자 수리검", OutfitSlot.Tool,
                    new Color(0.6f, 0.6f, 0.65f), new Color(0.1f, 0.1f, 0.1f), 1200,
                    new OutfitStatBonus { captureChanceBonus = 0.03f, atkBonus = 0.02f }),
                MakePremiumItem("acc_ninja_scarf", "닌자 머플러", "바람에 휘날리는 보라색 머플러", OutfitSlot.Accessory,
                    new Color(0.4f, 0.15f, 0.5f), new Color(0.3f, 0.1f, 0.4f), 600,
                    new OutfitStatBonus { moveSpeedBonus = 0.03f }),

                // ── 해적 세트 ──
                MakePremiumItem("hat_pirate", "해적 삼각모", "해골 마크가 새겨진 삼각모", OutfitSlot.Hat,
                    new Color(0.1f, 0.1f, 0.1f), new Color(1f, 1f, 1f), 800,
                    new OutfitStatBonus { atkBonus = 0.02f, captureChanceBonus = 0.02f }),
                MakePremiumItem("top_pirate", "해적 코트", "금장 단추의 해적 코트", OutfitSlot.Top,
                    new Color(0.5f, 0.1f, 0.1f), new Color(0.85f, 0.7f, 0.15f), 900,
                    new OutfitStatBonus { atkBonus = 0.03f, defBonus = 0.01f }),
                MakePremiumItem("bot_pirate", "해적 바지", "줄무늬 해적 바지", OutfitSlot.Bottom,
                    new Color(0.15f, 0.15f, 0.15f), new Color(0.3f, 0.3f, 0.3f), 600,
                    new OutfitStatBonus { defBonus = 0.02f }),
                MakePremiumItem("tool_cutlass", "해적 곡도", "곤충을 놀라게 하는 곡도", OutfitSlot.Tool,
                    new Color(0.7f, 0.7f, 0.75f), new Color(0.4f, 0.25f, 0.1f), 1100,
                    new OutfitStatBonus { atkBonus = 0.03f, captureChanceBonus = 0.01f }),
                MakePremiumItem("acc_eyepatch", "해적 안대", "한쪽 눈을 가리는 안대", OutfitSlot.Accessory,
                    new Color(0.1f, 0.1f, 0.1f), new Color(0.3f, 0.3f, 0.3f), 400,
                    new OutfitStatBonus { captureChanceBonus = 0.02f }),

                // ── 사이버펑크 세트 ──
                MakePremiumItem("hat_cyber_visor", "사이버 바이저", "AR 기능이 탑재된 미래형 바이저", OutfitSlot.Hat,
                    new Color(0.1f, 0.1f, 0.15f), new Color(0f, 0.9f, 1f), 1200,
                    new OutfitStatBonus { rareSpawnBonus = 0.03f, expMultiplier = 0.02f }),
                MakePremiumItem("top_cyber", "사이버 자켓", "네온 라인이 빛나는 자켓", OutfitSlot.Top,
                    new Color(0.1f, 0.1f, 0.15f), new Color(1f, 0f, 0.8f), 1000,
                    new OutfitStatBonus { atkBonus = 0.02f, rareSpawnBonus = 0.02f }),
                MakePremiumItem("bot_cyber", "사이버 팬츠", "홀로그램 라인이 달린 바지", OutfitSlot.Bottom,
                    new Color(0.1f, 0.1f, 0.12f), new Color(0f, 1f, 0.5f), 800,
                    new OutfitStatBonus { defBonus = 0.02f, moveSpeedBonus = 0.03f }),
                MakePremiumItem("tool_blaster", "포톤 블래스터", "곤충을 마비시키는 광선총", OutfitSlot.Tool,
                    new Color(0.15f, 0.15f, 0.2f), new Color(0f, 0.9f, 1f), 1800,
                    new OutfitStatBonus { captureChanceBonus = 0.04f, atkBonus = 0.02f }),
                MakePremiumItem("acc_neon_ring", "네온 팔찌", "빛나는 네온 LED 팔찌", OutfitSlot.Accessory,
                    new Color(0f, 1f, 0.5f), new Color(1f, 0f, 0.8f), 600,
                    new OutfitStatBonus { rareSpawnBonus = 0.02f }),

                // ── 마법사 세트 ──
                MakePremiumItem("hat_wizard", "마법사 모자", "별이 수놓인 뾰족한 마법사 모자", OutfitSlot.Hat,
                    new Color(0.15f, 0.1f, 0.35f), new Color(0.8f, 0.7f, 0.2f), 900,
                    new OutfitStatBonus { rareSpawnBonus = 0.03f, captureChanceBonus = 0.01f }),
                MakePremiumItem("outer_wizard", "마법사 로브", "신비로운 보라색 마법사 로브", OutfitSlot.Outerwear,
                    new Color(0.2f, 0.1f, 0.4f), new Color(0.6f, 0.4f, 0.9f), 1100,
                    new OutfitStatBonus { atkBonus = 0.02f, rareSpawnBonus = 0.02f, defBonus = 0.01f }),
                MakePremiumItem("tool_wand", "마법 지팡이", "곤충을 매혹하는 마법 지팡이", OutfitSlot.Tool,
                    new Color(0.4f, 0.25f, 0.12f), new Color(0.6f, 0.3f, 0.9f), 1400,
                    new OutfitStatBonus { captureChanceBonus = 0.05f }),
                MakePremiumItem("acc_crystal_orb", "수정구", "미래를 보여주는 수정 오브", OutfitSlot.Accessory,
                    new Color(0.6f, 0.4f, 0.9f), new Color(0.8f, 0.7f, 1f), 800,
                    new OutfitStatBonus { rareSpawnBonus = 0.03f, expMultiplier = 0.01f }),

                // ── 군인 세트 ──
                MakePremiumItem("hat_military", "군용 헬멧", "위장 패턴의 전투 헬멧", OutfitSlot.Hat,
                    new Color(0.3f, 0.35f, 0.2f), new Color(0.25f, 0.3f, 0.15f), 700,
                    new OutfitStatBonus { defBonus = 0.03f }),
                MakePremiumItem("top_military", "군용 전투복 상의", "위장 패턴 전투복", OutfitSlot.Top,
                    new Color(0.3f, 0.35f, 0.2f), new Color(0.2f, 0.25f, 0.15f), 800,
                    new OutfitStatBonus { atkBonus = 0.02f, defBonus = 0.02f }),
                MakePremiumItem("bot_military", "군용 카고 팬츠", "수납 가능한 군용 카고 팬츠", OutfitSlot.Bottom,
                    new Color(0.3f, 0.33f, 0.2f), new Color(0.25f, 0.28f, 0.15f), 600,
                    new OutfitStatBonus { defBonus = 0.02f, candyMultiplier = 0.02f }),
                MakePremiumItem("tool_tranq_gun", "마취총", "곤충을 안전하게 포획하는 마취총", OutfitSlot.Tool,
                    new Color(0.2f, 0.2f, 0.22f), new Color(0.3f, 0.35f, 0.2f), 1600,
                    new OutfitStatBonus { captureChanceBonus = 0.05f, atkBonus = 0.01f }),
                MakePremiumItem("acc_dog_tag", "군번줄", "전투 경험의 증표", OutfitSlot.Accessory,
                    new Color(0.6f, 0.6f, 0.65f), new Color(0.5f, 0.5f, 0.55f), 400,
                    new OutfitStatBonus { atkBonus = 0.02f, defBonus = 0.01f }),
            };
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
            if (!outfitLookup.ContainsKey(itemId))
            {
                Debug.LogWarning($"[Outfit] Equip 실패 — 알 수 없는 itemId: {itemId}");
                return;
            }
            OutfitItem item = outfitLookup[itemId];
            if (!ownedItems.Contains(itemId))
            {
                // TryPurchase → ownedItems.Add → Equip 순서가 정상. 그 외 경로에서 미보유 장착 시도 시 로깅.
                Debug.LogWarning($"[Outfit] Equip 실패 — 미보유 itemId: {itemId} (slot={item.slot})");
                return;
            }
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

            bool isMaster = AuthManager.Instance != null && AuthManager.Instance.MasterPrivilegesActive;
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

            bool isMaster = AuthManager.Instance != null && AuthManager.Instance.MasterPrivilegesActive;
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

        /// <summary>
        /// 슬롯별 의상 목록. <b>CharacterOutfitUI.OnGUI가 매 패스 부른다</b>(Layout+Repaint라
        /// 프레임당 2회 이상). 옛 구현은 여기서 <c>allOutfits.Where(...).ToArray()</c>를 돌려
        /// 95벌을 매번 훑고 배열을 새로 할당했다 — 의상 패널을 열어 둔 동안 계속 GC를 만든다.
        /// 카탈로그는 <see cref="BuildCatalog"/>가 만든 뒤 런타임에 바뀌지 않으므로 Initialize에서
        /// 한 번만 갈라 캐시한다. 반환 배열은 읽기 전용으로 다룰 것(호출부가 정렬·수정하면 캐시가 오염된다).
        /// </summary>
        public OutfitItem[] GetItemsForSlot(OutfitSlot slot)
        {
            if (slotLookup != null && slotLookup.TryGetValue(slot, out OutfitItem[] cached))
            {
                return cached;
            }
            return System.Array.Empty<OutfitItem>();
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

        // GameObject.Find는 씬 전체 스캔이라 OutfitChanged마다 부르면 아깝다. 플레이어는 영구 객체다.
        private GameObject cachedPlayer;

        /// <summary>실제 플레이어에 현재 장착을 적용한다.</summary>
        public void ApplyToCharacter()
        {
            if (cachedPlayer == null) cachedPlayer = GameObject.Find("Player");
            ApplyToCharacter(cachedPlayer, null);
        }

        /// <summary>
        /// 임의의 캐릭터 루트에 임의의 조합을 입힌다. <paramref name="loadout"/>이 null이면 현재 장착.
        /// 미리보기 마네킹과 입어보기(try-on)가 이 오버로드를 쓴다.
        ///
        /// 색은 <c>renderer.material</c>(인스턴스)로 칠한다 — Body/ArmL/ArmR이 같은 슬롯 머티리얼을
        /// 공유하는데 outer_none일 때만 몸통과 팔의 색이 갈리기 때문이다. sharedMaterial로 칠하면
        /// 마지막 값이 이겨 팔이 피부색으로 안 돌아온다. 마네킹은 풀링되어 한 개만 살아있으므로
        /// 인스턴스 머티리얼 수는 렌더러 수(~20)로 고정된다.
        /// </summary>
        public void ApplyToCharacter(GameObject player, OutfitLoadout loadout)
        {
            if (player == null) return;

            // 안전한 default 색상 (의상 미장착 시에도 캐릭터가 보이도록)
            Color defaultJacket = new Color(0.2f, 0.4f, 0.85f);
            Color defaultShirt = new Color(0.98f, 0.96f, 0.92f);
            Color defaultPants = new Color(0.25f, 0.25f, 0.28f);
            Color defaultBoot = new Color(0.35f, 0.22f, 0.1f);
            Color skinColor = new Color(0.92f, 0.78f, 0.62f);

            // 모자 — 색을 먼저 적용해 Cap/CapBrim을 되살린 뒤, 레시피가 있으면 그때 다시 숨긴다.
            // 순서가 뒤집히면 왕관 → 탐험가 캡으로 갈아입을 때 Cap이 숨겨진 채 남아 맨머리가 된다.
            OutfitItem hat = Resolve(loadout, OutfitSlot.Hat);
            ApplyPartColor(player, "Cap", hat != null ? hat.primaryColor : Color.clear);
            ApplyPartColor(player, "CapBrim", hat != null ? hat.primaryColor : Color.clear);
            ApplyShapeRecipe(player, OutfitSlot.Hat, hat);

            // 상의
            OutfitItem top = Resolve(loadout, OutfitSlot.Top);
            ApplyPartColor(player, "Shirt", top != null ? top.primaryColor : defaultShirt);

            // 겉옷: outer_none이면 Body는 셔츠 색, 팔은 피부색으로 (몸통/팔이 사라지지 않게)
            OutfitItem outer = Resolve(loadout, OutfitSlot.Outerwear);
            Color bodyCol, armCol;
            if (outer == null)
            {
                bodyCol = defaultJacket; armCol = defaultJacket;
            }
            else if (outer.primaryColor.a < 0.01f)
            {
                // outer_none: 외피 벗음 → Body는 셔츠 색, 팔은 피부색
                Color shirtCol = top != null ? top.primaryColor : defaultShirt;
                bodyCol = shirtCol; armCol = skinColor;
            }
            else
            {
                bodyCol = outer.primaryColor; armCol = outer.primaryColor;
            }
            ApplyPartColor(player, "Body", bodyCol);
            ApplyPartColor(player, "ArmL", armCol);
            ApplyPartColor(player, "ArmR", armCol);
            ApplyShapeRecipe(player, OutfitSlot.Outerwear, outer);   // 망토·로브 자락 등 덧붙임 파츠

            // 하의
            OutfitItem bot = Resolve(loadout, OutfitSlot.Bottom);
            ApplyPartColor(player, "LegL", bot != null ? bot.primaryColor : defaultPants);
            ApplyPartColor(player, "LegR", bot != null ? bot.primaryColor : defaultPants);

            // 신발
            OutfitItem shoe = Resolve(loadout, OutfitSlot.Shoes);
            ApplyPartColor(player, "BootL", shoe != null ? shoe.primaryColor : defaultBoot);
            ApplyPartColor(player, "BootR", shoe != null ? shoe.primaryColor : defaultBoot);

            // 가방
            OutfitItem bag = Resolve(loadout, OutfitSlot.Backpack);
            Color bagCol = bag != null ? bag.primaryColor : Color.clear;
            ApplyPartColor(player, "Backpack", bagCol);
            // 어깨끈은 Backpack의 자식이라 부모가 꺼지면 같이 사라진다. 색만 따로 이어준다 —
            // 옛 코드가 이 한 줄을 빠뜨려 배낭을 어떤 색으로 바꿔도 어깨끈은 늘 초기 갈색이었다.
            // 알파 0(bag_none)일 때 부르지 않는 게 중요하다: 그러면 끈이 SetActive(false)로 남아
            // 다음에 가방을 메도 끈만 사라진 채로 보인다.
            if (bagCol.a >= 0.01f) ApplyPartColor(player, "BackpackStrap", OutfitShapeLibrary.Darken(bagCol));
            ApplyShapeRecipe(player, OutfitSlot.Backpack, bag);

            // 도구 (종류별 형태 변경) — 색은 ApplyPartColor가, 형태는 OutfitShapeLibrary 레시피가 맡는다.
            OutfitItem tool = Resolve(loadout, OutfitSlot.Tool);
            ApplyPartColor(player, "NetHandle", tool != null ? tool.primaryColor : Color.clear);
            ApplyPartColor(player, "NetRing", tool != null ? tool.secondaryColor : Color.clear);
            ApplyShapeRecipe(player, OutfitSlot.Tool, tool);

            // 악세서리 — 전량 레시피다. 옛 PlayerVisualBuilder.ApplyAccessory는 미리 만든 4노드 중
            // 하나만 켜는 방식이라 15종 중 8종(날개·오라·후광·스카프 등)이 전부 가슴팍 큐브였다.
            ApplyShapeRecipe(player, OutfitSlot.Accessory, Resolve(loadout, OutfitSlot.Accessory));
        }

        /// <summary>로드아웃이 있으면 그쪽 itemId를, 없으면 실제 장착을 쓴다.</summary>
        private OutfitItem Resolve(OutfitLoadout loadout, OutfitSlot slot)
        {
            if (loadout == null) return GetEquipped(slot);

            string id = loadout.Get(slot);
            if (string.IsNullOrEmpty(id)) return null;
            return outfitLookup.TryGetValue(id, out OutfitItem item) ? item : null;
        }

        /// <summary>
        /// 슬롯의 형태 레시피를 적용한다. 레시피가 없으면 이 슬롯이 남긴 spawn 파츠만 정리하므로
        /// <b>레시피 유무와 무관하게 매번 불러야 한다</b> — 안 그러면 왕관을 벗어도 뿔이 남는다.
        /// </summary>
        private static void ApplyShapeRecipe(GameObject player, OutfitSlot slot, OutfitItem item)
        {
            Color primary = item != null ? item.primaryColor : Color.clear;
            Color secondary = item != null ? item.secondaryColor : Color.clear;

            // 미장착(null)과 *_none(알파 0)은 형태도 없다 — ApplyPartColor가 이미 노드를 껐다.
            OutfitRecipe recipe = null;
            if (item != null && primary.a >= 0.01f)
                OutfitShapeLibrary.TryGet(slot, item.itemId ?? "", out recipe);

            OutfitShapeLibrary.Apply(player.transform, slot, recipe, primary, secondary);
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
            // unlockedByDefault=true 아이템은 항상 ownedItems에 자동 등록
            // (Equip은 ownedItems 가드가 있어서 누락 시 기본 장착이 silent fail됨)
            bool addedDefault = false;
            if (allOutfits != null)
            {
                foreach (var item in allOutfits)
                {
                    if (item != null && item.unlockedByDefault && ownedItems.Add(item.itemId))
                        addedDefault = true;
                }
            }

            string saved = PlayerPrefs.GetString(OwnedKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                string[] ids = saved.Split(',');
                foreach (string id in ids)
                {
                    string trimmed = id.Trim();
                    if (trimmed.Length > 0)
                        ownedItems.Add(trimmed);
                }
            }

            // 신규 유저(저장 없음) 또는 기본 아이템 신규 추가 시 PlayerPrefs와 동기화
            if (string.IsNullOrEmpty(saved) || addedDefault)
                SaveOwnership();
        }
    }
}
