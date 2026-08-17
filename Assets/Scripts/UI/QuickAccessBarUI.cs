using InsectGame.Core;
using InsectGame.Dex;
using UnityEngine;

namespace InsectGame.UI
{
    public class QuickAccessBarUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private DexScreenUI dexScreen;
        [SerializeField] private BattleTeamUI battleTeamUI;
        [SerializeField] private TrainingUI trainingUI;
        [SerializeField] private CollectionUI collectionUI;
        [SerializeField] private RegionMapUI regionMapUI;
        [SerializeField] private CharacterOutfitUI outfitUI;
        [SerializeField] private CashShopUI cashShopUI;
        [SerializeField] private TutorialQuestUI questUI;
        [SerializeField] private SocialPvpUI socialPvpUI;
        [SerializeField] private StoryJournalUI storyJournalUI;

        // 전투/포획/미니게임 등 입력 차단용 신호 (CaptureInputController와 동일).
        [SerializeField] private BattleScreenUI battleScreen;
        [SerializeField] private RaidBattleUI raidScreen;
        [SerializeField] private PlayerMovement playerMovement;

        private struct ButtonDef
        {
            public string label;
            public string key;
            public Color color;
        }

        private GUIStyle cachedBtnStyle;
        private GUIStyle mobileGridButtonStyle;
        private GUIStyle badgeStyleCache;
        private bool mobileMenuOpen;

        // buttons[] 중 '퀘스트' 항목 인덱스 — 미확인 완료 배지를 이 버튼에만 그린다.
        private const int QuestButtonIndex = 4;

        /// <summary>데스크톱 하단 바의 버튼 높이. 배치의 단일 출처다.</summary>
        public const float BarButtonHeight = 64f;

        /// <summary>
        /// 이 바가 화면 하단에서 실제로 먹는 높이(버튼 + 배경 여백 8 + 시각적 간격 4).
        /// <b>다른 하단 UI는 이 값만큼 띄워야 한다</b> — `PlayerHintOverlay`가 그냥
        /// <c>BottomY(40)</c>을 쓰다가 바 안쪽에 통째로 들어가 버튼 위에 글자가 찍혔다.
        /// 여기 값을 고치면 그쪽도 따라온다(사본을 두지 않는다).
        /// </summary>
        public const float BarReservedHeight = BarButtonHeight + 8f + 4f;

        public bool IsOpen => mobileMenuOpen;

        public void CloseModal()
        {
            mobileMenuOpen = false;
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable()
        {
            ModalUIRegistry.Unregister(this);
        }

        private readonly ButtonDef[] buttons = new ButtonDef[]
        {
            new ButtonDef { label = "도감", key = "N", color = new Color(1f, 0.85f, 0.3f) },
            new ButtonDef { label = "배틀팀", key = "T", color = new Color(1f, 0.5f, 0.2f) },
            new ButtonDef { label = "훈련", key = "G", color = new Color(0.4f, 0.85f, 0.4f) },
            new ButtonDef { label = "컬렉션", key = "C", color = new Color(0.4f, 0.6f, 1f) },
            new ButtonDef { label = "퀘스트", key = "Q", color = new Color(0.3f, 0.9f, 0.7f) },
            new ButtonDef { label = "지도", key = "M", color = new Color(0.7f, 0.5f, 0.9f) },
            // '캐릭터'[V] 항목이 여기 있었다. 여는 화면(CharacterViewerUI)이 상점[F4]·의상[P]·
            // 좌상단 PlayerStatusHUD의 완전한 중복이라 화면째 제거했다(사용자 요청).
            new ButtonDef { label = "의상", key = "P", color = new Color(0.8f, 0.7f, 1f) },
            new ButtonDef { label = "상점", key = "F4", color = new Color(1f, 0.4f, 0.4f) },
            new ButtonDef { label = "PVP", key = "F6", color = new Color(0.25f, 0.75f, 1f) },
            // 스토리 저널 — 60비트로 늘어난 서사의 진행 상황을 보는 유일한 창구.
            // 인덱스가 TryHotkey의 switch case 번호다(9). 배열·Update·OnGUI 3곳이 짝이다.
            new ButtonDef { label = "이야기", key = "J", color = new Color(1f, 0.79f, 0.3f) },
        };

        private void Update()
        {
            if (IsInputBlocked()) return;

            if (Input.GetKeyDown(KeyCode.N)) TryHotkey(0);
            if (Input.GetKeyDown(KeyCode.T)) TryHotkey(1);
            if (Input.GetKeyDown(KeyCode.G)) TryHotkey(2);
            if (Input.GetKeyDown(KeyCode.C)) TryHotkey(3);
            if (Input.GetKeyDown(KeyCode.Q)) TryHotkey(4);
            if (Input.GetKeyDown(KeyCode.M)) TryHotkey(5);
            if (Input.GetKeyDown(KeyCode.P)) TryHotkey(6);
            if (Input.GetKeyDown(KeyCode.F4)) TryHotkey(7);
            if (Input.GetKeyDown(KeyCode.F6)) TryHotkey(8);
            if (Input.GetKeyDown(KeyCode.J)) TryHotkey(9);
        }

        private void OnGUI()
        {
            // 전투/포획/미니게임 중에는 핫키 처리도, 퀵바 렌더도 하지 않는다.
            if (IsInputBlocked()) return;

            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown)
            {
                // handled를 TryHotkey의 반환값으로 받는다. 모달 때문에 무시된 키를
                // e.Use()로 소비하면 그 모달의 텍스트필드가 글자를 못 받는다.
                bool handled = false;
                switch (e.keyCode)
                {
                    case KeyCode.N: handled = TryHotkey(0); break;
                    case KeyCode.T: handled = TryHotkey(1); break;
                    case KeyCode.G: handled = TryHotkey(2); break;
                    case KeyCode.C: handled = TryHotkey(3); break;
                    case KeyCode.Q: handled = TryHotkey(4); break;
                    case KeyCode.M: handled = TryHotkey(5); break;
                    case KeyCode.P: handled = TryHotkey(6); break;
                    case KeyCode.F4: handled = TryHotkey(7); break;
                    case KeyCode.F6: handled = TryHotkey(8); break;
                    case KeyCode.J: handled = TryHotkey(9); break;
                }
                if (handled) e.Use();
            }

            UIScale.Begin();

            if (UIScale.IsMobileLayout)
            {
                // 모달(도감/팀/배틀 등)이 열려 있지 않을 때만 — 필드 탐험 중 직접 접근용 우측 퀵바.
                if (!ModalUIRegistry.IsAnyOpen())
                    DrawMobileQuickBar();
                UIScale.End();
                return;
            }

            float btnW = Mathf.Min(140f, (UIScale.VirtualScreenWidth - 100f) / buttons.Length);
            const float btnH = BarButtonHeight;
            float gap = 6f;
            float totalW = buttons.Length * btnW + (buttons.Length - 1) * gap;
            float startX = (UIScale.VirtualScreenWidth - totalW) / 2f;
            // 제스처바(하단 세이프 인셋) + 세로 마진 위로.
            float y = UISafeLayout.BottomY(btnH);

            Rect barRect = new Rect(startX - 14, y - 8, totalW + 28, btnH + 16);

            // **클릭-이동 억제 등록**(`CaptureInputController`의 '잡기' 버튼과 같은 이유).
            // `PlayerMovement`는 `Input.GetMouseButtonDown(0)`을 Update에서 따로 폴링하는데,
            // 탭한 프레임엔 아직 모달이 안 열려 `IsAnyOpen()`이 false이고 IMGUI라 `pointerOverUI`도
            // false다. 등록이 없으면 버튼 아래 월드 지점이 클릭 목표로 잡혀, 모달을 닫는 순간
            // 캐릭터가 거기로 걸어간다 — 메뉴를 열 때마다 매번.
            FieldHudInput.RegisterBlockingRect(barRect);

            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            for (int i = 0; i < buttons.Length; i++)
            {
                float bx = startX + i * (btnW + gap);
                ButtonDef def = buttons[i];

                bool isActive = IsActive(i);

                GUI.backgroundColor = isActive
                    ? new Color(def.color.r * 0.6f, def.color.g * 0.6f, def.color.b * 0.6f)
                    : new Color(def.color.r * 0.2f, def.color.g * 0.2f, def.color.b * 0.2f);

                if (cachedBtnStyle == null)
                    cachedBtnStyle = new GUIStyle(GUI.skin.button)
                    { fontSize = 20, fontStyle = FontStyle.Bold, richText = true };
                cachedBtnStyle.normal.textColor = isActive ? Color.white : def.color;
                cachedBtnStyle.hover.textColor = Color.white;

                // **`OnClick`이 아니라 `TryHotkey`다.** 데스크톱 바는 모달이 열려 있어도 계속
                // 그려지므로(위 렌더 경로 주석 참조), 클릭이 가드를 안 거치면 도감을 켜 둔 채
                // 상점을 눌러 두 모달이 동시에 등록·렌더된다 — 뒤에 깔린 쪽이 "안 보이는데 입력만
                // 먹고" ESC도 그쪽부터 닫힌다. `TryHotkey`는 열려 있는 화면 자신의 버튼(=IsActive)만
                // 통과시키므로 토글로 닫는 동작은 그대로 산다.
                if (GUI.Button(new Rect(bx, y, btnW, btnH), $"{def.label}\n<size=14>[{def.key}]</size>", cachedBtnStyle))
                    TryHotkey(i);

                if (isActive)
                {
                    GUI.color = def.color;
                    GUI.DrawTexture(new Rect(bx, y, btnW, 4), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                if (i == QuestButtonIndex) DrawQuestBadge(new Rect(bx, y, btnW, btnH));
            }

            GUI.backgroundColor = Color.white;
            UIScale.End();
        }

        // 모바일: 우측 가장자리에 10개 기능을 '직접' 노출하는 세로 퀵바.
        // (기존엔 메뉴 버튼→팝업 2탭이라 번거로웠음. 메뉴 단계 제거.)
        private void DrawMobileQuickBar()
        {
            EnsureMobileStyles();
            int n = buttons.Length;
            float safeR = UIScale.VirtualSafeRight;
            float gap = 8f;
            float colW = 152f;
            // 우측 상단 정렬 — 우하단의 원형 '잡기' 버튼 + '계정' 버튼 공간을 비운다.
            // 상단 ~60% 영역에 10개를 배치(셀 높이 적응, 터치 최소 48).
            float regionH = UISafeLayout.ContentHeight * 0.6f;
            float cellH = Mathf.Clamp((regionH - 12f - (n - 1) * gap) / n, 48f, 88f);
            float totalH = n * cellH + (n - 1) * gap;
            float colX = UIScale.VirtualScreenWidth - safeR - colW - 14f;
            float colY = UISafeLayout.ContentTop;

            Rect colRect = new Rect(colX - 8f, colY - 8f, colW + 16f, totalH + 16f);

            // 데스크톱 바와 같은 이유의 클릭-이동 억제 등록.
            // **모바일이 더 나쁘다** — 키보드가 없어 `PlayerMovement`의 키 입력 기반 자동 해제도
            // 안 걸리므로, 한 번 잡힌 목표가 그대로 남아 모달을 닫자마자 걸어간다.
            FieldHudInput.RegisterBlockingRect(colRect);

            // 컬럼 배경(가독성, 필드 가림 최소화를 위해 옅게)
            GUI.color = new Color(0f, 0f, 0f, 0.3f);
            GUI.DrawTexture(colRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            for (int i = 0; i < n; i++)
            {
                float by = colY + i * (cellH + gap);
                ButtonDef def = buttons[i];
                bool active = IsActive(i);

                GUI.backgroundColor = active
                    ? new Color(def.color.r * 0.62f, def.color.g * 0.62f, def.color.b * 0.62f, 1f)
                    : new Color(def.color.r * 0.26f, def.color.g * 0.26f, def.color.b * 0.26f, 0.92f);
                mobileGridButtonStyle.normal.textColor = active ? Color.white : def.color;
                if (GUI.Button(new Rect(colX, by, colW, cellH), def.label, mobileGridButtonStyle))
                    TryHotkey(i);   // 데스크톱과 같은 이유 — 모달 중첩 차단

                if (active)
                {
                    GUI.color = def.color;
                    GUI.DrawTexture(new Rect(colX, by, colW, 4f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                if (i == QuestButtonIndex) DrawQuestBadge(new Rect(colX, by, colW, cellH));
            }
            GUI.backgroundColor = Color.white;
        }

        // 퀘스트 버튼 우상단에 '미확인 완료' 개수 배지(빨간 사각 + 흰 숫자). 0이면 안 그린다.
        // UIScale.Begin 매트릭스 안에서 호출되므로 좌표는 가상 캔버스 기준.
        private void DrawQuestBadge(Rect btnRect)
        {
            int count = TutorialQuestManager.Instance != null
                ? TutorialQuestManager.Instance.UnseenCompletedCount : 0;
            if (count <= 0) return;

            if (badgeStyleCache == null)
            {
                badgeStyleCache = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                badgeStyleCache.normal.textColor = Color.white;
            }

            float d = Mathf.Clamp(btnRect.height * 0.45f, 24f, 32f);
            Rect badge = new Rect(btnRect.xMax - d - 4f, btnRect.y + 4f, d, d);

            Color prev = GUI.color;
            GUI.color = new Color(0.9f, 0.18f, 0.18f, 1f);
            GUI.DrawTexture(badge, Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(badge, count > 9 ? "9+" : count.ToString(), badgeStyleCache);
        }

        private void EnsureMobileStyles()
        {
            if (mobileGridButtonStyle != null) return;
            mobileGridButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            mobileGridButtonStyle.normal.textColor = Color.white;
            mobileGridButtonStyle.hover.textColor = Color.white;
        }

        private bool IsActive(int index)
        {
            switch (index)
            {
                case 0: return dexScreen != null && dexScreen.IsOpen;
                case 1: return battleTeamUI != null && battleTeamUI.IsOpen;
                case 2: return trainingUI != null && trainingUI.IsOpen;
                case 3: return collectionUI != null && collectionUI.IsOpen;
                case 4: return questUI != null && questUI.IsOpen;
                case 5: return regionMapUI != null && regionMapUI.IsOpen;
                case 6: return outfitUI != null && outfitUI.IsOpen;
                case 7: return cashShopUI != null && cashShopUI.IsOpen;
                case 8: return socialPvpUI != null && socialPvpUI.IsOpen;
                case 9: return storyJournalUI != null && storyJournalUI.IsOpen;
                default: return false;
            }
        }

        private int lastToggleFrame = -1;

        // 전투(1v1/레이드)·포획·미니게임 중에는 메뉴 토글을 막는다.
        // 배틀/레이드 진입 시 playerMovement.SetFrozen(true)가 호출되므로 frozen 하나로도 커버되나,
        // CaptureInputController와 동일하게 명시적 신호도 함께 검사한다.
        private bool IsInputBlocked()
        {
            if (battleScreen != null && battleScreen.IsBattleActive) return true;
            if (raidScreen != null && raidScreen.IsRaidActive) return true;
            if (playerMovement != null && playerMovement.IsFrozen) return true;
            return false;
        }

        /// <summary>
        /// 핫키로 index를 토글한다. 다른 화면이 열려 있으면 무시하고 false를 반환한다 —
        /// 호출부가 이벤트를 소비하지 않아야 그 화면의 텍스트필드가 글자를 받는다.
        /// 지금 열려 있는 화면 자신의 키는 통과시켜 같은 키로 닫는 토글을 유지한다.
        /// </summary>
        /// <remarks>
        /// IsInputBlocked는 battle/raid/frozen만 본다. "모든 모달이 SetFrozen을 거니
        /// frozen 하나로 커버된다"는 전제가 깨져 있었다 — SocialPvpUI와
        /// WorldFieldMultiplayerUI는 모달로 등록하면서 SetFrozen을 부르지 않는다.
        /// 그래서 친구코드·채팅 입력에 N/T/G/C/Q/M/P를 치면 글자마다 화면이 토글되고,
        /// OnGUI의 e.Use()가 그 글자를 삼켜 입력조차 되지 않았다.
        /// (렌더 경로는 이 가드를 쓰면 안 된다 — 데스크톱 바가 통째로 사라지고
        ///  active 하이라이트 설계가 죽는다.)
        /// </remarks>
        private bool TryHotkey(int index)
        {
            if (ModalUIRegistry.IsAnyOpen() && !IsActive(index)) return false;
            OnClick(index);
            return true;
        }

        private void OnClick(int index)
        {
            if (IsInputBlocked()) return;
            if (Time.frameCount == lastToggleFrame) return;
            lastToggleFrame = Time.frameCount;

            switch (index)
            {
                case 0: if (dexScreen != null) dexScreen.Toggle(); break;
                case 1: if (battleTeamUI != null) battleTeamUI.Toggle(); break;
                case 2: if (trainingUI != null) trainingUI.Toggle(); break;
                case 3: if (collectionUI != null) collectionUI.Toggle(); break;
                case 4: if (questUI != null) questUI.Toggle(); break;
                case 5: if (regionMapUI != null) regionMapUI.Toggle(); break;
                case 6: if (outfitUI != null) outfitUI.Toggle(); break;
                case 7: if (cashShopUI != null) cashShopUI.Toggle(); break;
                case 8: if (socialPvpUI != null) socialPvpUI.Toggle(); break;
                case 9: if (storyJournalUI != null) storyJournalUI.Toggle(); break;
            }
        }

        public void AutoWire(DexScreenUI dex, BattleTeamUI team, TrainingUI training,
            CollectionUI collection, RegionMapUI map)
        {
            if (dexScreen == null) dexScreen = dex;
            if (battleTeamUI == null) battleTeamUI = team;
            if (trainingUI == null) trainingUI = training;
            if (collectionUI == null) collectionUI = collection;
            if (regionMapUI == null) regionMapUI = map;
        }

        public void AutoWire(CharacterOutfitUI outfit, CashShopUI cashShop)
        {
            if (outfitUI == null) outfitUI = outfit;
            if (cashShopUI == null) cashShopUI = cashShop;
        }

        public void AutoWire(TutorialQuestUI quest)
        {
            if (questUI == null) questUI = quest;
        }

        public void AutoWire(StoryJournalUI journal)
        {
            if (storyJournalUI == null) storyJournalUI = journal;
        }

        public void AutoWire(SocialPvpUI social)
        {
            if (socialPvpUI == null) socialPvpUI = social;
        }

        // 전투/포획/미니게임 중 입력 가드용 신호 주입.
        public void AutoWire(BattleScreenUI battle, RaidBattleUI raid, PlayerMovement movement)
        {
            if (battleScreen == null) battleScreen = battle;
            if (raidScreen == null) raidScreen = raid;
            if (playerMovement == null) playerMovement = movement;
        }
    }
}
