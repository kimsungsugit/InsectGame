using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class CharacterOutfitUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private CharacterOutfitManager outfitManager;
        [SerializeField] private OutfitBonusProvider bonusProvider;
        [SerializeField] private CharacterModelPreviewRenderer modelPreview;

        private bool isOpen;

        /// <summary>
        /// 아이템별 보너스 문구 캐시. <c>GetPrimaryBonusText()</c>는 <c>$"포획 +{x*100:0}%"</c>처럼
        /// 문자열을 만드는데 호출부가 <b>카드 루프 안</b>이라 카드마다·OnGUI 패스마다 새로 났다
        /// (바로 위 줄의 GUIStyle 회귀는 막아뒀으면서 문자열은 남아 있던 자리다).
        /// 카탈로그는 세션 내내 불변이라 한 번 구우면 무효화가 필요 없다.
        /// </summary>
        private readonly Dictionary<string, string> bonusTextCache = new Dictionary<string, string>();

        /// <summary>세트 진행도 별 문자열 캐시 — 키는 (채운 수, 전체 수). 필요한 조합이 몇 개뿐이다.</summary>
        private static readonly Dictionary<int, string> StarCache = new Dictionary<int, string>();

        private string BonusTextFor(OutfitItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) return "";
            if (!bonusTextCache.TryGetValue(item.itemId, out string text))
            {
                text = item.statBonus.GetPrimaryBonusText();
                bonusTextCache[item.itemId] = text;
            }

            return text;
        }

        /// <summary>
        /// "★★☆☆" — 예전엔 <c>stars += ...</c>를 루프로 돌려 세트마다·패스마다 total개의 문자열이 났다
        /// (덧붙이기 루프라 할당이 제곱으로 는다). 조합 수가 적으니 구워 둔다.
        /// </summary>
        private static string StarsFor(int equipped, int total)
        {
            int safeTotal = Mathf.Clamp(total, 0, 32);
            int safeEquipped = Mathf.Clamp(equipped, 0, safeTotal);
            int key = safeTotal * 64 + safeEquipped;
            if (StarCache.TryGetValue(key, out string cached)) return cached;

            System.Text.StringBuilder sb = new System.Text.StringBuilder(safeTotal);
            for (int i = 0; i < safeTotal; i++) sb.Append(i < safeEquipped ? '★' : '☆');
            string built = sb.ToString();
            StarCache[key] = built;
            return built;
        }
        private OutfitSlot selectedSlot = OutfitSlot.Hat;
        private Vector2 scrollPos;
        private readonly UIDirectScroll directScroll = new UIDirectScroll();

        // 장착 피드백
        private float equipFlashTimer;
        private string lastEquippedId;
        private float setCompleteFlashTimer;
        private readonly System.Collections.Generic.Dictionary<string, bool> prevSetStates =
            new System.Collections.Generic.Dictionary<string, bool>();

        // 패널 페이드
        private TweenHandle openFade;
        private bool wasOpen;

        // 캐릭터 미리보기 — 3D 마네킹이 있으면 그걸, 없으면 2D 도트 폴백.
        private float previewRotate;      // 2D 폴백의 좌우 흔들림 위상
        private float previewYaw = CharacterModelPreviewRenderer.FrontYaw;   // 3D 마네킹 Y 회전(도). 드래그로 바뀐다
        private bool previewDragging;
        private float previewDragLastX;
        // 지금 그릴 조합. 실장착을 복사해 두고 입어보기(try-on) 시 한 슬롯만 덮어쓴다.
        private readonly OutfitLoadout previewLoadout = new OutfitLoadout();
        private OutfitItem tryOnItem;        // 호버 중인 카드. 실장착은 건드리지 않는다
        private bool hoverFoundThisPass;
        // 슬롯별로 **클릭(탭)해서 고른** 카드 — 입어보기가 호버에만 의존하면 터치 기기에서 아예 안 됐다.
        // 슬롯마다 따로 담아 두어 여러 벌을 한꺼번에 맞춰 볼 수 있다. 창을 열 때 비운다.
        private readonly Dictionary<OutfitSlot, OutfitItem> trySelections = new Dictionary<OutfitSlot, OutfitItem>();
        private bool previewCloseUp;

        internal enum ItemFilter { All, Owned, NotOwned }
        private ItemFilter filter = ItemFilter.All;
        private static readonly string[] FilterLabels = { "전체", "보유", "미보유" };
        private readonly List<OutfitItem> visibleItems = new List<OutfitItem>();
        private OutfitSlot visibleSlot = (OutfitSlot)(-1);
        private ItemFilter visibleFilter;
        private int visibleVersion = -1;

        public bool IsOpen => isOpen;

        private readonly string[] slotLabels = new string[]
        {
            "모자", "상의", "하의", "겉옷", "신발", "가방", "도구", "악세서리"
        };

        // 스타일 캐시
        private GUIStyle panelStyle;
        private GUIStyle tabNormalStyle;
        private GUIStyle tabSelectedStyle;
        private GUIStyle cardStyle;
        private GUIStyle cardEquippedStyle;
        private GUIStyle cardLockedStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle coinStyle;
        private GUIStyle coinRightStyle;      // 데스크톱: 오른쪽 카드 열 아래에 오른쪽 정렬
        private GUIStyle buttonStyle;
        private GUIStyle closeStyle;
        private GUIStyle bonusStyle;
        private GUIStyle setStyle;
        private GUIStyle setActiveStyle;
        private GUIStyle infoStyleCache;
        private GUIStyle infoNameStyleCache;
        private GUIStyle detailNameStyle;
        private GUIStyle detailDescStyle;
        private GUIStyle detailBonusStyle;
        private GUIStyle detailSetStyle;
        private bool stylesInitialized;

        // 매 OnGUI 프레임 FindFirstObjectByType 회귀 차단 — 첫 조회 1회만.
        private PlayerCurrencyWallet walletCache;

        private static readonly Color InfoLabelCol = new Color(0.85f, 0.9f, 1f);

        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen)
            {
                scrollPos = Vector2.zero;
                // 지난번에 돌려 둔 각도·입어보기가 남아 있으면 "내 옷이 이상하다"로 보인다 — 열 때마다 정면·실장착으로.
                previewYaw = CharacterModelPreviewRenderer.FrontYaw;
                previewCloseUp = false;
                trySelections.Clear();
                tryOnItem = null;
            }
            // 외형(성별·머리·얼굴)은 캐릭터 생성 화면에서만 바뀌므로 이 모달 밖에서만 변한다.
            // 여기서 한 번 표시해 주면 렌더러가 매 프레임 PlayerPrefs를 두드리지 않아도 된다.
            if (modelPreview != null) modelPreview.InvalidatePreview();
            directScroll.Reset();
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        public void CloseModal()
        {
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable()
        {
            // 옛은 isOpen=true 그대로 두고 Unregister만 호출 → 같은 GO SetActive 토글 시
            // isOpen=true이지만 Registry 미등록 상태로 stale. HandleEscape가 이 모달을 무시.
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        public void AutoWire(CharacterOutfitManager manager)
        {
            if (outfitManager == null) outfitManager = manager;
        }

        public void AutoWire(CharacterOutfitManager manager, OutfitBonusProvider bonus)
        {
            if (outfitManager == null) outfitManager = manager;
            if (bonusProvider == null) bonusProvider = bonus;
        }

        public void AutoWire(CharacterModelPreviewRenderer preview)
        {
            if (modelPreview == null) modelPreview = preview;
        }

        // ── 캐릭터 미리보기 ──

        /// <summary>
        /// 3D 마네킹이 준비돼 있으면 그것을, 아직이면 2D 도트 폴백을 그린다. 드래그로 돌리고,
        /// 오른쪽 위 버튼으로 정면 복귀·상반신 확대·입어보기 되돌리기를 한다. 아래는 상세 패널.
        /// </summary>
        private void DrawCharacterPreview(Rect area, bool mobile)
        {
            UITheme theme = UITheme.Instance;
            UISurface.Card(area, theme.surfaceBase, theme.surfaceBorder);

            // 아래 상세(이름·설명·보너스 전체·세트). 예전엔 "현재 모자: 이름" 한 줄이라 설명과 보너스 상세는
            // **이미 가진** 아이템의 호버 툴팁에서만 보였다 — 사기 전에 무엇이 좋은지 알 수 없었다.
            float stageH = PreviewStageHeight(area.width, area.height, mobile);
            float infoH = area.height - 16f - stageH;
            Rect stage = new Rect(area.x + 8f, area.y + 8f, area.width - 16f, stageH);

            Texture preview = null;
            if (modelPreview != null)
            {
                SyncPreviewLoadout();
                preview = modelPreview.GetPreview(previewLoadout, previewYaw, previewCloseUp);
            }

            if (preview != null)
            {
                GUI.DrawTexture(stage, preview, ScaleMode.ScaleToFit, true);
            }
            else
            {
                // 콜드 캐시(첫 프레임)나 렌더러 미배선 — 기존 2D 도트 캐릭터로 버틴다.
                float charScale = mobile ? 1.7f : 2.9f;
                float swayX = Mathf.Sin(previewRotate * Mathf.Deg2Rad) * 12f * charScale;
                CharacterPortraitRenderer.DrawWithOutfit(
                    stage.center.x, stage.y + stage.height * 0.5f, charScale, swayX);
            }

            // 버튼을 **드래그 처리보다 먼저** — 버튼이 MouseDown을 쓰면(Use) 드래그가 시작되지 않는다.
            // 그림(텍스처)은 이미 위에서 그렸으니 버튼이 그 위에 얹힌다.
            if (modelPreview != null) DrawPreviewControls(stage, mobile);
            if (modelPreview != null) HandlePreviewDrag(stage);

            Rect detail = new Rect(area.x + 16f, area.yMax - infoH, area.width - 32f, infoH - 8f);
            float used = DrawItemDetail(detail, mobile);

            // 모바일엔 탭 아래 세트 목록 칸이 없다(탭이 상단 4열이다). 상세 아래가 남으면 거기에 둔다.
            if (mobile)
                DrawActiveSets(detail.x, used + 12f, detail.width, detail.yMax);
        }

        /// <summary>
        /// 미리보기 칸에서 그림(stage)이 차지할 높이. 그림은 텍스처 비율(2:3)까지만 키운다 — 세로 화면처럼
        /// 칸이 길면 그 이상은 위아래 여백일 뿐이라 남는 높이를 상세에 준다(설명·입은 세트 진행이 들어간다).
        /// 상세는 최소 <c>150</c>(모바일)·<c>200</c>(데스크톱)을 보장한다.
        /// </summary>
        internal static float PreviewStageHeight(float areaW, float areaH, bool mobile)
        {
            float infoMinH = mobile ? 150f : 200f;
            float stageMaxH = (areaW - 16f) * CharacterModelPreviewRenderer.PreviewHeightPerWidth;
            return Mathf.Clamp(areaH - 16f - infoMinH, 1f, Mathf.Max(1f, stageMaxH));
        }

        /// <summary>정면 복귀 · 전신/상반신 · 입어보기 되돌리기.</summary>
        private void DrawPreviewControls(Rect stage, bool mobile)
        {
            UITheme theme = UITheme.Instance;
            float bw = mobile ? 132f : 120f, bh = mobile ? 54f : 42f, gap = 8f;   // "원래대로" 네 글자가 안 잘리는 폭
            float bx = stage.xMax - bw - 6f;
            float by = stage.y + 6f;

            if (UISurface.Button(new Rect(bx, by, bw, bh), "정면", theme.surfaceRaised, tabNormalStyle))
                previewYaw = CharacterModelPreviewRenderer.FrontYaw;
            by += bh + gap;
            if (UISurface.Button(new Rect(bx, by, bw, bh), previewCloseUp ? "전신" : "확대", theme.surfaceRaised, tabNormalStyle, previewCloseUp))
                previewCloseUp = !previewCloseUp;
            by += bh + gap;
            if (HasTryOn() && UISurface.Button(new Rect(bx, by, bw, bh), "원래대로", theme.accentCoral, tabNormalStyle))
                trySelections.Clear();
        }

        /// <summary>
        /// 실장착을 복사한 뒤 슬롯별로 **고른 카드**(입어보기)를 덮고, 마지막으로 호버 중인 카드를 덮는다.
        /// 슬롯마다 따로 고를 수 있어 "이 모자에 이 상의" 같은 조합을 사기 전에 맞춰 볼 수 있다.
        /// 호버만 있던 예전 방식은 터치 기기에서 입어보기가 아예 안 됐다.
        /// </summary>
        private void SyncPreviewLoadout()
        {
            previewLoadout.CopyFrom(outfitManager);
            foreach (KeyValuePair<OutfitSlot, OutfitItem> pick in trySelections)
                if (pick.Value != null) previewLoadout.Set(pick.Key, pick.Value.itemId);
            if (tryOnItem != null) previewLoadout.Set(tryOnItem.slot, tryOnItem.itemId);
        }

        /// <summary>입어보기가 실장착과 하나라도 다른가(되돌리기 버튼 표시용).</summary>
        private bool HasTryOn()
        {
            foreach (KeyValuePair<OutfitSlot, OutfitItem> pick in trySelections)
                if (pick.Value != null && !outfitManager.IsEquipped(pick.Value.itemId)) return true;
            return false;
        }

        /// <summary>상세 패널이 보여 줄 아이템 — 호버 > 이 슬롯에서 고른 카드 > 이 슬롯의 실장착.</summary>
        private OutfitItem DetailItem()
        {
            if (tryOnItem != null && tryOnItem.slot == selectedSlot) return tryOnItem;
            if (trySelections.TryGetValue(selectedSlot, out OutfitItem picked) && picked != null) return picked;
            return outfitManager.GetEquipped(selectedSlot);
        }

        /// <summary>
        /// 입고 있는 세트의 진행(별·발동 보너스). <paramref name="maxY"/>를 넘는 항목은 그리지 않는다 —
        /// 데스크톱 탭 열은 세트가 셋이면 아래 보너스 요약 줄에 겹쳤다.
        /// </summary>
        private void DrawActiveSets(float x, float setY, float w, float maxY)
        {
            if (bonusProvider == null) return;
            ActiveSetInfo[] activeSets = bonusProvider.GetActiveSets();
            foreach (ActiveSetInfo setInfo in activeSets)
            {
                bool active = setInfo.isPartialActive || setInfo.isFullActive;
                GUIStyle sStyle = active ? setActiveStyle : setStyle;
                // 발동하면 보너스 줄이 붙어 3줄, 아니면 2줄 — 한글 줄높이 ≈ fontSize × 1.35 (16px이면 68·46)
                float setH = sStyle.fontSize * 1.35f * (active ? 3 : 2) + 3f;
                if (setY + setH > maxY) break;

                Color prevColor = sStyle.normal.textColor;
                if (active) sStyle.normal.textColor = setInfo.set.setColor;

                string setLabel = ActiveSetLabel(setInfo);
                Rect setRect = new Rect(x, setY, w, setH);

                // 세트 완성 글로우
                if (setCompleteFlashTimer > 0f && active)
                {
                    float glowAlpha = Mathf.Clamp01(setCompleteFlashTimer / 1f) * 0.6f;
                    UIHelper.DrawRarityGlow(setRect, setInfo.set.setColor, glowAlpha, Time.time);
                }

                GUI.Label(setRect, setLabel, sStyle);
                sStyle.normal.textColor = prevColor;
                setY += setH + 4;
            }
        }

        private readonly Dictionary<(string, int), string> activeSetLabelCache = new Dictionary<(string, int), string>();

        /// <summary>
        /// "이름 (n/총)\n별\n발동 보너스" — 입은 벌 수와 발동 단계로만 바뀌므로 그 둘로 캐시한다
        /// (예전엔 OnGUI 패스마다 세트당 문자열 3~4개를 이어 붙였다).
        /// </summary>
        private string ActiveSetLabel(ActiveSetInfo setInfo)
        {
            int stage = setInfo.isFullActive ? 2 : setInfo.isPartialActive ? 1 : 0;
            (string, int) key = (setInfo.set.setId, setInfo.equippedCount * 4 + stage);
            if (activeSetLabelCache.TryGetValue(key, out string label)) return label;

            int total = setInfo.set.requiredItemIds.Length;
            label = $"{setInfo.set.displayName} ({setInfo.equippedCount}/{total})\n{StarsFor(setInfo.equippedCount, total)}";
            if (setInfo.isFullActive) label += "\n" + setInfo.set.fullBonus.GetPrimaryBonusText();
            else if (setInfo.isPartialActive) label += "\n" + setInfo.set.partialBonus.GetPrimaryBonusText();
            activeSetLabelCache[key] = label;
            return label;
        }

        private string measuredDescId;
        private float measuredDescW = -1f, measuredDescH;

        /// <summary>상세 설명이 줄바꿈 후 차지할 높이 — 아이템·폭이 같으면 다시 재지 않는다.</summary>
        private float DescriptionHeight(OutfitItem item, float width)
        {
            if (item.itemId != measuredDescId || !Mathf.Approximately(width, measuredDescW))
            {
                measuredDescId = item.itemId;
                measuredDescW = width;
                measuredDescH = UIHelper.MeasureWrappedHeight(detailDescStyle, item.description, width);
            }
            return measuredDescH;
        }

        /// <returns>그린 내용의 아래 끝 y — 남는 자리에 세트 진행을 이어 그린다.</returns>
        private float DrawItemDetail(Rect r, bool mobile)
        {
            UITheme theme = UITheme.Instance;
            OutfitItem item = DetailItem();
            if (item == null)
            {
                GUI.Label(new Rect(r.x, r.y, r.width, 36f), "아이템을 골라 보세요", detailDescStyle);
                return r.y + 36f;
            }

            bool owned = outfitManager.IsOwned(item.itemId);
            bool equipped = outfitManager.IsEquipped(item.itemId);

            // 1줄: 이름 + 상태 칩
            float chipW = mobile ? 150f : 132f;
            float nameH = 40f;
            UIHelper.LabelFit(new Rect(r.x, r.y, r.width - chipW - 8f, nameH), item.displayName, detailNameStyle);
            string status = StatusTextFor(item, owned, equipped);
            if (!string.IsNullOrEmpty(status))
            {
                Color chipBg = equipped ? theme.accentAmber : owned ? theme.accentMint
                    : item.gemPrice > 0 ? new Color(0.45f, 0.3f, 0.75f) : theme.surfaceRaised;
                Color chipFg = equipped || owned ? theme.surfaceBase : theme.textPrimary;
                UISurface.Chip(new Rect(r.xMax - chipW, r.y + 5f, chipW, 30f), status, chipBg, chipFg);
            }
            float y = r.y + nameH + 2f;

            // 2줄: 설명 — 칸이 좁으면(가로 모바일) 세트/획득 줄을 우선하고 설명은 뺀다.
            // 세로 화면처럼 칸이 길면 설명을 세 줄까지 준다(데스크톱은 두 줄 52 그대로).
            const float bonusAndSetH = 34f + 32f;
            float descMax = Mathf.Min(r.yMax - y - bonusAndSetH - 2f, mobile ? 110f : 52f);
            if (descMax >= 50f)
            {
                // 데스크톱은 두 줄 칸 고정(예전 그대로). 모바일 긴 칸은 글 높이만큼만 — 한 줄 설명 밑이 비지 않게.
                float descH = mobile ? Mathf.Clamp(DescriptionHeight(item, r.width), 28f, descMax) : descMax;
                UIHelper.LabelFit(new Rect(r.x, y, r.width, descH), item.description, detailDescStyle);
                y += descH + 2f;
            }

            // 3줄: 보너스 전체
            UIHelper.LabelFit(new Rect(r.x, y, r.width, 32f), FullBonusTextFor(item), detailBonusStyle);
            y += 34f;

            // 4줄: 세트 진행 또는 획득 방법
            OutfitSetDefinition set = SetOf(item.itemId);
            if (set != null)
            {
                Color prev = detailSetStyle.normal.textColor;
                detailSetStyle.normal.textColor = Color.Lerp(set.setColor, Color.white, 0.35f);
                UIHelper.LabelFit(new Rect(r.x, y, r.width, 32f), SetLineFor(set), detailSetStyle);
                detailSetStyle.normal.textColor = prev;
                y += 34f;
            }
            else if (!owned && item.price <= 0 && item.gemPrice <= 0 && !string.IsNullOrEmpty(item.unlockCondition))
            {
                UIHelper.LabelFit(new Rect(r.x, y, r.width, 32f), DescribeUnlockCondition(item.unlockCondition), detailSetStyle);
                y += 34f;
            }
            return y;
        }

        private readonly Dictionary<string, string> statusTextCache = new Dictionary<string, string>();
        private readonly Dictionary<string, string> fullBonusCache = new Dictionary<string, string>();
        private readonly Dictionary<string, string> setLineCache = new Dictionary<string, string>();
        private int setLineVersion = -1;

        private string StatusTextFor(OutfitItem item, bool owned, bool equipped)
        {
            if (equipped) return "장착중";
            if (owned) return "보유";
            if (!string.IsNullOrEmpty(item.unlockCondition) && item.price <= 0 && item.gemPrice <= 0) return "잠김";
            if (!statusTextCache.TryGetValue(item.itemId, out string text))
            {
                text = PriceLabel(item);
                statusTextCache[item.itemId] = text;
            }
            return text;
        }

        private readonly Dictionary<string, string> priceLabelCache = new Dictionary<string, string>();

        /// <summary>
        /// 가격 표기 — "보석 800" / "코인 300". 🪙·💎 이모지는 스탠드얼론 기본 폰트에 없어 **□로 깨졌다**
        /// (2026-09-30 검수 빌드 캡처). 카드마다·패스마다 보간 문자열을 만들지 않게 캐시한다.
        /// </summary>
        private string PriceLabel(OutfitItem item)
        {
            if (!priceLabelCache.TryGetValue(item.itemId, out string text))
            {
                text = item.gemPrice > 0 ? $"보석 {item.gemPrice}" : item.price > 0 ? $"코인 {item.price}" : "";
                priceLabelCache[item.itemId] = text;
            }
            return text;
        }

        private string footerText;
        private int footerCoins = -1, footerGems = -1;

        private static GUIStyle BuildSummaryStyle()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = 19;
            s.fontStyle = FontStyle.Bold;
            s.normal.textColor = new Color(0.4f, 0.9f, 0.4f);
            s.alignment = TextAnchor.MiddleLeft;
            return s;
        }

        private static readonly GUIContent summaryProbe = new GUIContent();
        private string summaryLinesSource, summaryLines;
        private float summaryLinesWidth = -1f;

        /// <summary>
        /// 데스크톱 요약 줄은 미리보기 왼쪽 폭(~540)이라 보너스가 많으면 넘친다. 줄바꿈을 IMGUI에 맡기면
        /// 한글 사이 아무 데서나 끊겨 "캔디 / +2%"처럼 항목이 갈렸다(검수 캡처) — 항목 경계(공백)에서
        /// 가운데에 가장 가까운 곳을 골라 직접 두 줄로 나눈다. 한 줄에 들어가면 그대로 둔다.
        /// </summary>
        private string SummaryForWidth(string summary, GUIStyle style, float width)
        {
            if (ReferenceEquals(summary, summaryLinesSource) && Mathf.Approximately(width, summaryLinesWidth))
                return summaryLines;
            summaryLinesSource = summary;
            summaryLinesWidth = width;
            summaryProbe.text = summary;
            summaryLines = style.CalcSize(summaryProbe).x <= width ? summary : SplitAtMiddleSpace(summary);
            return summaryLines;
        }

        /// <summary>가운데에 가장 가까운 공백 하나를 줄바꿈으로 — 공백이 없으면 그대로.</summary>
        internal static string SplitAtMiddleSpace(string text)
        {
            int mid = text.Length / 2, best = -1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == ' ' && (best < 0 || Mathf.Abs(i - mid) < Mathf.Abs(best - mid))) best = i;
            return best > 0 ? text.Substring(0, best) + "\n" + text.Substring(best + 1) : text;
        }

        private string equippedSummary;
        private OutfitStatBonus equippedSummaryFor;

        /// <summary>
        /// 하단 "장비 보너스: 포획+2% …" — 장착이 바뀔 때만 다시 만든다(예전엔 OnGUI 패스마다 7번 이어 붙였다).
        /// </summary>
        internal string EquippedSummaryText(OutfitStatBonus total)
        {
            if (equippedSummary != null && SameBonus(total, equippedSummaryFor)) return equippedSummary;
            equippedSummaryFor = total;
            string summary = "장비 보너스:";
            if (total.captureChanceBonus > 0f) summary += $" 포획+{total.captureChanceBonus * 100f:0}%";
            if (total.atkBonus > 0f) summary += $" ATK+{total.atkBonus * 100f:0}%";
            if (total.defBonus > 0f) summary += $" DEF+{total.defBonus * 100f:0}%";
            if (total.moveSpeedBonus > 0f) summary += $" 이속+{total.moveSpeedBonus * 100f:0}%";
            if (total.expMultiplier > 0f) summary += $" 경험치+{total.expMultiplier * 100f:0}%";
            if (total.candyMultiplier > 0f) summary += $" 캔디+{total.candyMultiplier * 100f:0}%";
            if (total.rareSpawnBonus > 0f) summary += $" 레어+{total.rareSpawnBonus * 100f:0}%";
            equippedSummary = summary;
            return summary;
        }

        // 구조체 기본 Equals는 박싱(할당)이라 필드를 직접 비교한다.
        private static bool SameBonus(OutfitStatBonus a, OutfitStatBonus b) =>
            a.captureChanceBonus == b.captureChanceBonus && a.atkBonus == b.atkBonus && a.defBonus == b.defBonus
            && a.moveSpeedBonus == b.moveSpeedBonus && a.expMultiplier == b.expMultiplier
            && a.candyMultiplier == b.candyMultiplier && a.rareSpawnBonus == b.rareSpawnBonus;

        private string FooterText(int coins, int gems)
        {
            if (coins != footerCoins || gems != footerGems || footerText == null)
            {
                footerCoins = coins;
                footerGems = gems;
                footerText = $"보유 코인 {coins}  ·  보석 {gems}";
            }
            return footerText;
        }

        private string FullBonusTextFor(OutfitItem item)
        {
            if (!fullBonusCache.TryGetValue(item.itemId, out string text))
            {
                text = FullBonusText(item.statBonus);
                fullBonusCache[item.itemId] = text;
            }
            return text;
        }

        /// <summary>"포획 +2% · 경험치 +1%" — 카드의 대표 1개와 달리 전부. 없으면 "보너스 없음".</summary>
        internal static string FullBonusText(OutfitStatBonus b)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            void Add(string label, float v)
            {
                if (v <= 0f) return;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(label).Append(" +").Append(Mathf.RoundToInt(v * 100f)).Append('%');
            }
            Add("포획", b.captureChanceBonus);
            Add("ATK", b.atkBonus);
            Add("DEF", b.defBonus);
            Add("이속", b.moveSpeedBonus);
            Add("경험치", b.expMultiplier);
            Add("캔디", b.candyMultiplier);
            Add("레어", b.rareSpawnBonus);
            return sb.Length > 0 ? sb.ToString() : "보너스 없음";
        }

        /// <summary>"◆ 서부의 총잡이 세트 2/6 보유 · 3벌부터 포획 +3%". 소유가 바뀌면 다시 만든다.</summary>
        private string SetLineFor(OutfitSetDefinition set)
        {
            if (setLineVersion != outfitManager.OwnershipVersion)
            {
                setLineCache.Clear();
                setLineVersion = outfitManager.OwnershipVersion;
            }
            if (setLineCache.TryGetValue(set.setId, out string text)) return text;

            int owned = 0;
            for (int i = 0; i < set.requiredItemIds.Length; i++)
                if (outfitManager.IsOwned(set.requiredItemIds[i])) owned++;
            text = $"◆ {set.displayName} 세트 {owned}/{set.requiredItemIds.Length} 보유 · {set.partialThreshold}벌부터 {set.partialBonus.GetPrimaryBonusText()}";
            setLineCache[set.setId] = text;
            return text;
        }

        private static Dictionary<string, OutfitSetDefinition> setByItem;

        /// <summary>
        /// 아이템이 속한 세트(없으면 null). 예전 카드의 세트 점은 **이미 한 벌 이상 입은** 세트에만 찍혀서,
        /// 시작하지 않은 세트는 어떤 옷이 한 벌인지 알 수 없었다.
        /// </summary>
        internal static OutfitSetDefinition SetOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (setByItem == null)
            {
                setByItem = new Dictionary<string, OutfitSetDefinition>();
                foreach (OutfitSetDefinition s in OutfitSetCatalog.GetAllSets())
                    foreach (string id in s.requiredItemIds)
                        if (!setByItem.ContainsKey(id)) setByItem[id] = s;
            }
            return setByItem.TryGetValue(itemId, out OutfitSetDefinition set) ? set : null;
        }

        /// <summary>필터를 거친 이 슬롯의 목록. 슬롯·필터·소유 버전이 같으면 재사용한다(OnGUI마다 거르지 않는다).</summary>
        private List<OutfitItem> VisibleItems()
        {
            int ver = outfitManager.OwnershipVersion;
            if (visibleSlot == selectedSlot && visibleFilter == filter && visibleVersion == ver) return visibleItems;
            visibleSlot = selectedSlot;
            visibleFilter = filter;
            visibleVersion = ver;
            visibleItems.Clear();
            foreach (OutfitItem item in outfitManager.GetItemsForSlot(selectedSlot))
            {
                if (PassesFilter(filter, outfitManager.IsOwned(item.itemId))) visibleItems.Add(item);
            }
            return visibleItems;
        }

        internal static bool PassesFilter(ItemFilter f, bool owned)
        {
            switch (f)
            {
                case ItemFilter.Owned: return owned;
                case ItemFilter.NotOwned: return !owned;
                default: return true;
            }
        }

        private void HandlePreviewDrag(Rect stage)
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (stage.Contains(e.mousePosition))
                    {
                        previewDragging = true;
                        previewDragLastX = e.mousePosition.x;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (previewDragging)
                    {
                        // 가상좌표 기준이라 화면 해상도가 달라도 같은 손맛이 난다.
                        previewYaw -= (e.mousePosition.x - previewDragLastX) * 0.6f;
                        previewDragLastX = e.mousePosition.x;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (previewDragging)
                    {
                        previewDragging = false;
                        e.Use();
                    }
                    break;
            }
        }

        // P키는 QuickAccessBarUI에서 처리

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;
            bool mobile = UIScale.IsMobileLayout;

            Texture2D panelTex = UIHelper.GetCachedTex(new Color(0.08f, 0.1f, 0.2f, 0.92f));
            Texture2D tabNormalTex = UIHelper.GetCachedTex(new Color(0.25f, 0.28f, 0.35f, 1f));
            Texture2D tabSelTex = UIHelper.GetCachedTex(new Color(0.3f, 0.5f, 0.9f, 1f));
            Texture2D cardTex = UIHelper.GetCachedTex(new Color(0.15f, 0.17f, 0.25f, 1f));
            Texture2D cardEqTex = UIHelper.GetCachedTex(new Color(0.2f, 0.25f, 0.35f, 1f));
            Texture2D cardLockTex = UIHelper.GetCachedTex(new Color(0.1f, 0.1f, 0.15f, 0.9f));
            Texture2D btnTex = UIHelper.GetCachedTex(new Color(0.2f, 0.5f, 0.2f, 1f));
            Texture2D closeTex = UIHelper.GetCachedTex(new Color(0.7f, 0.15f, 0.15f, 1f));

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTex;

            tabNormalStyle = new GUIStyle(GUI.skin.button);
            tabNormalStyle.normal.background = tabNormalTex;
            tabNormalStyle.normal.textColor = Color.white;
            tabNormalStyle.fontSize = mobile ? 26 : 24;
            tabNormalStyle.fontStyle = FontStyle.Bold;
            tabNormalStyle.alignment = TextAnchor.MiddleCenter;

            tabSelectedStyle = new GUIStyle(tabNormalStyle);
            tabSelectedStyle.normal.background = tabSelTex;

            cardStyle = new GUIStyle(GUI.skin.box);
            cardStyle.normal.background = cardTex;
            cardStyle.normal.textColor = Color.white;
            cardStyle.alignment = TextAnchor.UpperCenter;
            cardStyle.padding = new RectOffset(4, 4, 4, 4);

            cardEquippedStyle = new GUIStyle(cardStyle);
            cardEquippedStyle.normal.background = cardEqTex;

            cardLockedStyle = new GUIStyle(cardStyle);
            cardLockedStyle.normal.background = cardLockTex;
            cardLockedStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 1f);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 22;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
            titleStyle.alignment = TextAnchor.MiddleLeft;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 22;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.wordWrap = true;

            coinStyle = new GUIStyle(GUI.skin.label);
            coinStyle.fontSize = 26;
            coinStyle.fontStyle = FontStyle.Bold;
            coinStyle.normal.textColor = new Color(1f, 0.84f, 0f, 1f);
            coinStyle.alignment = TextAnchor.MiddleLeft;
            coinRightStyle = new GUIStyle(coinStyle) { alignment = TextAnchor.MiddleRight };

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = btnTex;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.fontSize = mobile ? 23 : 20;
            buttonStyle.fontStyle = FontStyle.Bold;

            closeStyle = new GUIStyle(GUI.skin.button);
            closeStyle.normal.background = closeTex;
            closeStyle.normal.textColor = Color.white;
            closeStyle.fontSize = mobile ? 24 : 20;
            closeStyle.fontStyle = FontStyle.Bold;

            bonusStyle = new GUIStyle(GUI.skin.label);
            bonusStyle.fontSize = 10;
            bonusStyle.normal.textColor = new Color(0.4f, 0.9f, 0.4f);
            bonusStyle.alignment = TextAnchor.MiddleCenter;

            setStyle = new GUIStyle(GUI.skin.label);
            // 13은 검수 빌드 캡처에서 읽히지 않았다. 모바일은 미리보기 칸(폭 ~430)에 그려서 더 크게 둔다.
            setStyle.fontSize = UIScale.IsMobileLayout ? 20 : 16;
            setStyle.normal.textColor = new Color(0.6f, 0.6f, 0.7f);
            setStyle.alignment = TextAnchor.MiddleLeft;
            setStyle.wordWrap = true;

            setActiveStyle = new GUIStyle(setStyle);
            setActiveStyle.fontStyle = FontStyle.Bold;

            // OnGUI 매 프레임 new GUIStyle 회귀 차단 — DrawPanel 캐릭터 아래 슬롯 정보 라벨용.
            infoStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 19, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            infoStyleCache.normal.textColor = InfoLabelCol;

            infoNameStyleCache = new GUIStyle(infoStyleCache)
            { fontSize = 24, fontStyle = FontStyle.Bold };
            infoNameStyleCache.normal.textColor = Color.white;

            // 상세 패널 — 한글 줄높이 ≈ fontSize × 1.35에 맞춘 상자(40·52·32)다.
            UITheme theme = UITheme.Instance;
            detailNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailNameStyle.normal.textColor = theme.textPrimary;
            detailDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperLeft, wordWrap = true };
            detailDescStyle.normal.textColor = theme.textSecondary;
            detailBonusStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailBonusStyle.normal.textColor = theme.accentMint;
            detailSetStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailSetStyle.normal.textColor = theme.accentAmber;
        }

        private void OnGUI()
        {
            // 패널 페이드
            float panelAlpha = UIHelper.AnimatePanelOpen(ref openFade, isOpen, ref wasOpen);
            if (!isOpen && panelAlpha <= 0.01f) return;
            if (outfitManager == null) return;

            // 고DPI 세로 모바일에서 UI가 물리 픽셀로 그려져 과도하게 작아지던 근본 문제 해결 —
            // 가상 캔버스(세로 1080x1920 / 가로 1920x1080) 좌표계로 통일. End()는 OnGUI 말미 1곳.
            UIScale.Begin();

            GUI.color = new Color(1f, 1f, 1f, panelAlpha);

            // 타이머 감소 (OnGUI는 프레임당 여러번 호출되므로 Repaint에서만)
            if (Event.current.type == EventType.Repaint)
            {
                if (equipFlashTimer > 0f) equipFlashTimer -= Time.deltaTime;
                if (setCompleteFlashTimer > 0f) setCompleteFlashTimer -= Time.deltaTime;
            }

            InitStyles();

            // 회전 애니메이션 갱신 (Repaint에서만 누적)
            if (Event.current.type == EventType.Repaint)
                previewRotate += Time.deltaTime * 30f;

            // 데스크톱은 미리보기를 가운데 크게 두고 아이템을 양옆에 나눠 거는 3분할이라 폭이 더 든다
            // (탭 + 좌 카드열 + 미리보기 + 우 카드열).
            // 모바일은 높이도 길게 청한다(하네스가 안전 영역으로 자른다) — 820 고정이던 때 세로 화면(1920)에서
            // 창이 가운데 43%만 쓰고 위아래가 비었다(2026-09-30 검수 캡처). 캐시샵과 같은 1560이다.
            Rect panelRect = UISafeLayout.CenteredPanel(
                UIScale.IsMobileLayout ? 1200f : 1560f,
                UIScale.IsMobileLayout ? 1560f : 820f);
            float panelW = panelRect.width;
            float panelH = panelRect.height;
            bool mobile = UIScale.IsMobileLayout;
            float x = panelRect.x;
            float y = panelRect.y;

            GUI.Box(panelRect, "", panelStyle);

            // 제목 — UIHelper.CachedStyle로 1회 캐싱 (옛 매 OnGUI new GUIStyle 회귀 차단)
            GUIStyle bigTitle = UIHelper.CachedStyle("outfit_big_title", () =>
            {
                GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                s.normal.textColor = Color.white;
                return s;
            });
            GUI.Label(new Rect(x + 24, y + 14, 540, 50), "캐릭터 꾸미기", bigTitle);

            // 필터(전체·보유·미보유) — 제목 줄 오른쪽. 그리드 위에 줄을 새로 내면 카드가 한 줄 줄어든다.
            {
                UITheme theme = UITheme.Instance;
                float chipW = mobile ? 150f : 120f, chipH = mobile ? 52f : 42f;
                // 닫기 버튼 왼쪽에서 끝나게 — 세로 화면은 패널이 1032로 줄어 고정 x+560이면 "미보유"가 X에 깔렸다(검수 캡처).
                float chipsW = FilterLabels.Length * chipW + (FilterLabels.Length - 1) * 8f;
                float closeW = mobile ? 58f : 44f;
                float chipX = Mathf.Min(x + 600f, x + panelW - closeW - 24f - chipsW);
                for (int f = 0; f < FilterLabels.Length; f++)
                {
                    Rect fr = new Rect(chipX + f * (chipW + 8f), y + (mobile ? 12f : 16f), chipW, chipH);
                    bool on = (int)filter == f;
                    if (UISurface.Button(fr, FilterLabels[f], on ? theme.accentMint : theme.surfaceRaised, tabNormalStyle, on))
                    {
                        filter = (ItemFilter)f;
                        scrollPos = Vector2.zero;
                        directScroll.Reset();
                    }
                }
            }

            // 닫기 버튼
            float closeSize = mobile ? 58f : 44f;
            if (GUI.Button(new Rect(x + panelW - closeSize - 12f, y + 10f, closeSize, mobile ? 56f : 40f), "X", closeStyle))
            {
                CloseModal();
            }

            // ── 슬롯 탭 치수 (미리보기가 이 값에 의존하므로 먼저 정한다) ──
            // 모바일 세로: 슬롯 8개를 4열 2행으로 배치. 옛 1행 8열은 tabW가 5열 기준이라
            // 7·8번 탭(도구·악세서리)이 화면 밖으로 잘려 접근 불가였음. 데스크톱은 우측 세로 1열 유지.
            float tabY = y + 70f;
            float tabGap = 6f;
            int tabsPerRow = 4;
            float tabW = mobile ? (panelW - 40f - tabGap * (tabsPerRow - 1)) / tabsPerRow : 140f;
            float tabH = mobile ? 64f : 50f;

            OutfitSlot[] slots = (OutfitSlot[])System.Enum.GetValues(typeof(OutfitSlot));
            int tabRows = mobile ? Mathf.CeilToInt(slots.Length / (float)tabsPerRow) : slots.Length;
            float tabBlockH = tabRows * (tabH + tabGap);

            // ── 레이아웃 축 ──
            // [좌 카드열][미리보기 중앙][우 카드열] — 세로·가로 공통이다. 미리보기를 구석에 두면
            // 아이템을 볼 때 시선이 좌우로 크게 움직인다. 가운데 두고 카드가 감싸면 한눈에 든다.
            // 다른 건 탭 위치뿐: 데스크톱은 좌측 세로 1열, 세로 모바일은 상단 4열 2행(폭이 없다).
            float tabX = x + 20f;
            float gridX = mobile ? x + 20f : tabX + tabW + 16f;
            float gridW = mobile ? panelW - 40f : panelW - (gridX - x) - 20f;

            // 미리보기가 그리드 영역의 정중앙을 차지한다. 세로는 폭이 1032뿐이라 조금 좁게 잡아야
            // 양옆에 카드 한 열씩이 남는다(400 + 296×2).
            float previewW = Mathf.Min(gridW * 0.46f, mobile ? 460f : 560f);
            float sideW = Mathf.Max(0f, (gridW - previewW) * 0.5f);

            // ── 캐릭터 미리보기 ──
            // 예전엔 `if (!mobile)` 안에만 있어 모바일에서는 미리보기가 아예 없었다.
            // 모바일 y는 tabBlockH에서 파생한다 — 탭 높이를 바꿔도 겹치지 않게(값을 두 곳에 적지 않는다).
            float charAreaX = gridX + sideW;
            float charAreaY = mobile ? tabY + tabBlockH + 8f : y + 70f;
            float charAreaW = previewW;

            // ── 하단 띠(보너스 요약 −76 · 재화 −44) ── 카드는 이 위에서 **여백을 두고** 끝난다.
            // 예전 데스크톱 그리드는 요약 줄 4px 위에서 끝나 잘린 카드 모서리가 글자에 붙었다(2026-09-30 검수 캡처).
            const float FooterBandH = 96f;
            float contentBottom = y + panelH - FooterBandH;

            // 모바일도 카드 열과 같은 높이다(옛 720 상한은 카드가 미리보기 **아래**에 오던 때의 것 — 지금은 옆이다).
            // 그림은 텍스처 비율까지만 커지고 남는 높이는 상세·세트 진행이 받는다(DrawCharacterPreview).
            // 데스크톱 미리보기는 패널 바닥까지 간다 — 하단 띠는 미리보기 **왼쪽**에만 그린다(아래 요약 폭).
            float charAreaH = mobile
                ? Mathf.Max(1f, contentBottom - charAreaY)
                : panelH - 90f;

            Rect charArea = new Rect(charAreaX, charAreaY, charAreaW, charAreaH);
            // **그리드보다 먼저 그린다** — IMGUI는 먼저 그린 쪽이 이벤트를 먼저 받으므로,
            // 뒤에 오는 스크롤뷰가 미리보기 위의 드래그(캐릭터 회전)를 가로채지 않는다.
            DrawCharacterPreview(charArea, mobile);

            // ── 슬롯 탭 (데스크톱: 좌측 세로 / 모바일: 상단) ──
            for (int i = 0; i < slots.Length; i++)
            {
                Rect tabRect = mobile
                    ? new Rect(tabX + (i % tabsPerRow) * (tabW + tabGap), tabY + (i / tabsPerRow) * (tabH + tabGap), tabW, tabH)
                    : new Rect(tabX, tabY + i * (tabH + tabGap), tabW, tabH);
                GUIStyle style = (slots[i] == selectedSlot) ? tabSelectedStyle : tabNormalStyle;
                if (GUI.Button(tabRect, slotLabels[i], style))
                {
                    selectedSlot = slots[i];
                    scrollPos = Vector2.zero;
                    directScroll.Reset();
                }
                if (outfitManager.HasNewInSlot(slots[i]))
                    UISurface.Chip(new Rect(tabRect.xMax - 22f, tabRect.y + 4f, 18f, 18f), "", UITheme.Instance.accentMint, UITheme.Instance.accentMint);
            }

            // ── 세트 정보 패널 (데스크톱: 탭 아래 / 모바일: 미리보기 칸의 상세 아래 — DrawCharacterPreview) ──
            if (!mobile)
                DrawActiveSets(tabX, tabY + slots.Length * (tabH + tabGap) + 8, tabW, contentBottom);

            // ── 아이템 그리드 ──
            // 스크롤뷰가 미리보기까지 덮는 폭을 갖되 **가운데 구간엔 카드를 두지 않는다** —
            // 그래야 좌우 카드가 한 스크롤을 공유한다(스크롤뷰를 둘로 쪼개면 따로 논다).
            // 그리드는 미리보기와 **같은 줄에서** 시작한다: 세로에서도 카드가 미리보기 옆에 선다.
            // 하단은 contentBottom(하단 띠 위 여백)에서 끝난다.
            float gridY = mobile ? charAreaY : y + 70f;
            float gridH = Mathf.Max(1f, contentBottom - gridY);

            List<OutfitItem> items = VisibleItems();

            // 세로는 한쪽 폭이 ~296px뿐이라 카드를 데스크톱보다 조금만 크게 잡는다(한 열).
            float cardW = mobile ? 230f : 200f;
            float cardH = mobile ? 316f : 284f;
            float cardGap = 14f;
            // 한쪽 열 수를 세고 카드를 좌·우로 번갈아 채운다(세로·가로 공통).
            int cols = Mathf.Max(1, Mathf.FloorToInt((sideW - 10) / (cardW + cardGap)));
            int perSide = Mathf.CeilToInt(items.Count / 2f);
            int rows = Mathf.CeilToInt((float)perSide / cols);
            float contentH = rows * (cardH + cardGap) + 10;

            hoverFoundThisPass = false;

            Rect viewRect = new Rect(gridX, gridY, gridW, gridH);
            Rect contentRect = new Rect(0, 0, gridW, contentH);
            // 미리보기 위에서는 스크롤을 잡지 않는다 — 그 자리 드래그는 캐릭터 회전이다.
            bool pointerOverPreview = charArea.Contains(UIScale.VirtualMousePosition);
            directScroll.Handle(ref scrollPos, viewRect, contentH, cardH * 0.3f,
                interactive: !pointerOverPreview);
            scrollPos = GUI.BeginScrollView(
                viewRect,
                scrollPos,
                contentRect,
                GUIStyle.none,
                GUIStyle.none);

            for (int i = 0; i < items.Count; i++)
            {
                OutfitItem item = items[i];
                // 짝수는 왼쪽, 홀수는 오른쪽 — 미리보기를 사이에 두고 감싼다.
                // 한쪽 열 수는 폭에서 나온다: 세로 1열 / 데스크톱 2열.
                int seat = i / 2;
                int col = seat % cols;
                int row = seat / cols;
                float bandX = (i % 2) == 0 ? 0f : sideW + previewW;
                float cx = bandX + col * (cardW + cardGap) + 5;
                float cy = row * (cardH + cardGap) + 5;
                Rect cardRect = new Rect(cx, cy, cardW, cardH);
                // 스크롤 뷰포트 밖 카드는 3D 썸네일을 요청하지 않는다 — 목록 전체를 굽느라
                // 프레임당 1렌더 예산을 화면에 안 보이는 카드에 쓰지 않게. (곤충 도감엔 없는 최적화)
                bool cardVisible = cardRect.yMax >= scrollPos.y - 8f && cardRect.y <= scrollPos.y + gridH + 8f;

                bool owned = outfitManager.IsOwned(item.itemId);
                bool equipped = outfitManager.IsEquipped(item.itemId);

                // 카드 배경
                GUIStyle cStyle = owned ? (equipped ? cardEquippedStyle : cardStyle) : cardLockedStyle;
                GUI.Box(cardRect, "", cStyle);

                // 호버 하이라이트 (ScrollView 내부에서는 mousePosition이 이미 로컬 좌표)
                bool isHovered = Event.current.type == EventType.Repaint
                    && cardRect.Contains(Event.current.mousePosition);
                if (isHovered)
                {
                    GUI.DrawTexture(cardRect, UIHelper.GetCachedTex(new Color(1f, 1f, 1f, 0.08f)));
                    UIHelper.DrawBorder(cardRect, new Color(0.7f, 0.8f, 1f, 0.5f), 1);
                    outfitManager.MarkSeen(item.itemId);
                    // 입어보기 — 미보유 아이템도 포함한다. "사기 전에 어떻게 보이나"가 핵심이다.
                    // 미리보기는 이 값을 다음 패스에서 읽으므로 한 프레임 늦는데, 체감되지 않는다.
                    tryOnItem = item;
                    hoverFoundThisPass = true;
                }

                // 장착중 금색 테두리
                if (equipped)
                {
                    UIHelper.DrawBorder(cardRect, new Color(1f, 0.84f, 0f, 1f), 2);
                }
                // 입어보기로 고른 카드 — 장착 테두리(금색)와 구별되는 민트
                if (!equipped && trySelections.TryGetValue(item.slot, out OutfitItem picked) && picked == item)
                {
                    UIHelper.DrawBorder(cardRect, UITheme.Instance.accentMint, 3);
                }
                if (outfitManager.IsNew(item.itemId))
                {
                    UISurface.Chip(new Rect(cx + cardW - 64f, cy + 6f, 58f, 26f), "NEW", UITheme.Instance.accentMint, UITheme.Instance.surfaceBase);
                }

                // 장착 플래시 오버레이
                if (equipFlashTimer > 0f && item.itemId == lastEquippedId)
                {
                    float flashAlpha = Mathf.Clamp01(equipFlashTimer / 0.4f) * 0.6f;
                    GUI.DrawTexture(cardRect, UIHelper.GetCachedTex(new Color(1f, 1f, 1f, flashAlpha)));
                }

                // 프로시저럴 의상 아이콘 — 슬롯별 실제 형태 (모자/도구/신발/...)
                // 옛 GetSlotSymbol("^", "T" 등 텍스트)은 어떤 아이템인지 시각적 구별 불가
                float previewSize = 100f;
                Rect previewRect = new Rect(cx + (cardW - previewSize) * 0.5f, cy + 10, previewSize, previewSize);
                if (item.primaryColor.a > 0.01f)
                {
                    // 배경 (어두운 톤)
                    Color bgCol = new Color(item.primaryColor.r * 0.3f + 0.05f, item.primaryColor.g * 0.3f + 0.05f, item.primaryColor.b * 0.3f + 0.05f, 0.8f);
                    GUI.DrawTexture(previewRect, UIHelper.GetCachedTex(bgCol));
                    // 테두리
                    GUI.DrawTexture(new Rect(previewRect.x, previewRect.y, previewRect.width, 2), UIHelper.GetCachedTex(item.primaryColor));
                    GUI.DrawTexture(new Rect(previewRect.x, previewRect.yMax - 2, previewRect.width, 2), UIHelper.GetCachedTex(item.primaryColor));
                    GUI.DrawTexture(new Rect(previewRect.x, previewRect.y, 2, previewRect.height), UIHelper.GetCachedTex(item.primaryColor));
                    GUI.DrawTexture(new Rect(previewRect.xMax - 2, previewRect.y, 2, previewRect.height), UIHelper.GetCachedTex(item.primaryColor));
                    // 3D 마네킹 썸네일이 준비됐으면 그것, 아직이면 레시피를 정사영한 2D.
                    // 둘 다 OutfitShapeLibrary 하나를 읽으므로 어느 쪽이 나와도 착용 모습과 일치한다.
                    Texture thumb = (modelPreview != null && cardVisible)
                        ? modelPreview.GetThumbnail(item.slot, item.itemId)
                        : null;
                    if (thumb != null)
                        GUI.DrawTexture(previewRect, thumb, ScaleMode.ScaleToFit, true);
                    else
                        CharacterPortraitRenderer.DrawItemPreview(previewRect, item.slot, item.itemId, item.primaryColor, item.secondaryColor);
                }
                else
                {
                    GUI.DrawTexture(previewRect, UIHelper.GetCachedTex(new Color(0.15f, 0.15f, 0.15f, 0.5f)));
                    GUIStyle emptyStyle = UIHelper.CachedStyle("outfit_empty", () =>
                    {
                        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                        s.normal.textColor = new Color(0.4f, 0.4f, 0.4f);
                        return s;
                    });
                    GUI.Label(previewRect, "---", emptyStyle);
                }

                // 이름 — 길이는 데이터가 정하고 상자는 고정이라 LabelFit으로 줄여 맞춘다.
                Rect nameRect = new Rect(cx + 4, cy + 112, cardW - 8, 44);
                UIHelper.LabelFit(nameRect, item.displayName, labelStyle);

                // 보너스 표시 — CachedStyle로 1회 캐싱 (카드 12개 × 30FPS = 360회/초 new GUIStyle 회귀 차단)
                string bonusText = BonusTextFor(item);
                if (!string.IsNullOrEmpty(bonusText))
                {
                    Rect bonusRect = new Rect(cx + 4, cy + 158, cardW - 8, 24);
                    GUIStyle bigBonus = UIHelper.CachedStyle("outfit_big_bonus", () =>
                    {
                        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
                        s.normal.textColor = new Color(0.4f, 0.9f, 0.4f);
                        return s;
                    });
                    GUI.Label(bonusRect, bonusText, bigBonus);
                }

                // 세트 표시 — **모든** 세트 옷에. 예전엔 이미 한 벌 이상 입은 세트에만 찍혀 시작하지 않은 세트의
                // 구성품을 알 수 없었고, 카드마다 세트 목록 전체를 두 겹으로 훑었다(OnGUI 패스마다).
                OutfitSetDefinition cardSet = SetOf(item.itemId);
                if (cardSet != null)
                {
                    UISurface.Flat(new Rect(cx + 6f, cy + 6f, 8f, 26f), cardSet.setColor);
                }

                // 버튼 영역
                float itemButtonH = mobile ? 56f : 42f;
                Rect btnRect = new Rect(cx + 14, cy + cardH - itemButtonH - 12f, cardW - 28, itemButtonH);

                if (owned)
                {
                    if (equipped)
                    {
                        GUI.enabled = false;
                        GUI.Button(btnRect, "장착중", buttonStyle);
                        GUI.enabled = true;
                    }
                    else
                    {
                        if (GUI.Button(btnRect, "장착", buttonStyle))
                        {
                            EquipWithFeedback(item);
                        }
                    }
                }
                else
                {
                    if (item.gemPrice > 0)
                    {
                        // 보석 구매 (프리미엄)
                        int currentGems = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0;
                        bool canAfford = currentGems >= item.gemPrice;
                        GUI.backgroundColor = canAfford ? new Color(0.3f, 0.2f, 0.6f) : new Color(0.3f, 0.3f, 0.3f);
                        GUI.enabled = canAfford;
                        if (GUI.Button(btnRect, PriceLabel(item), buttonStyle))
                        {
                            if (outfitManager.TryPurchaseWithGems(item.itemId))
                            {
                                EquipWithFeedback(item);
                            }
                        }
                        GUI.enabled = true;
                        GUI.backgroundColor = Color.white;

                        // 프리미엄 표시
                        Rect premRect = new Rect(cx + 4, cy + 186, cardW - 8, 26);
                        GUIStyle premStyle = UIHelper.CachedStyle("outfit_prem", () =>
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.fontSize = 17;
                            s.normal.textColor = new Color(0.9f, 0.7f, 1f);
                            s.alignment = TextAnchor.MiddleCenter;
                            return s;
                        });
                        GUI.Label(premRect, "★ 프리미엄", premStyle);
                    }
                    else if (item.price > 0)
                    {
                        // 코인 구매. 라벨이 오래 🍬(캔디)였는데 TryPurchase가 실제로 빼는 건 **코인**이다
                        // (CharacterOutfitManager.TryPurchase → wallet.SpendCoins). 잔액이 모자라도 눌려서
                        // 아무 반응 없이 끝났다 — 보석 버튼처럼 비활성화한다.
                        bool canAfford = CanAffordCoins(item.price);
                        GUI.enabled = canAfford;
                        if (GUI.Button(btnRect, PriceLabel(item), buttonStyle))
                        {
                            if (outfitManager.TryPurchase(item.itemId))
                            {
                                EquipWithFeedback(item);
                            }
                        }
                        GUI.enabled = true;
                    }
                    else if (!string.IsNullOrEmpty(item.unlockCondition))
                    {
                        GUI.enabled = false;
                        GUI.Button(btnRect, "잠김", buttonStyle);
                        GUI.enabled = true;

                        Rect hintRect = new Rect(cx + 4, cy + 186, cardW - 8, 26);
                        GUIStyle hintStyle = UIHelper.CachedStyle("outfit_hint", () =>
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.fontSize = 16;
                            s.normal.textColor = new Color(0.7f, 0.6f, 0.3f);
                            s.alignment = TextAnchor.MiddleCenter;
                            s.wordWrap = true;
                            return s;
                        });
                        // 원문 토큰("region_garden"·"level_15")을 그대로 그리면 한국어 게임에
                        // 영문 식별자가 노출된다. 사람이 읽는 문장으로 바꾸고, 길이가 데이터에서
                        // 오므로 고정 26px 상자에 맞춰 폰트를 줄인다(rules/ui-layout.md의 LabelFit).
                        UIHelper.LabelFit(hintRect, DescribeUnlockCondition(item.unlockCondition), hintStyle);
                    }
                }

                // 카드 본문을 누르면 그 슬롯의 입어보기로 고른다(터치 기기의 유일한 입어보기 경로).
                // 장착/구매 버튼은 **먼저** 그렸으므로 그 버튼 위를 누르면 그쪽이 이벤트를 가져간다.
                if (GUI.Button(cardRect, GUIContent.none, GUIStyle.none))
                {
                    trySelections[item.slot] = item;
                    outfitManager.MarkSeen(item.itemId);
                }
            }

            GUI.EndScrollView();

            // 카드에서 마우스가 벗어나면 입어보기를 풀고 실장착으로 돌아간다.
            if (Event.current.type == EventType.Repaint && !hoverFoundThisPass) tryOnItem = null;

            // ── 하단 보너스 요약 + 코인 표시 ──
            // 데스크톱은 미리보기가 바닥까지 가므로 하단 띠가 미리보기 양옆으로 갈린다: 왼쪽(탭 열 + 왼쪽 카드 열)에
            // 요약을 두 줄까지, 오른쪽 카드 열 아래에 재화를 둔다. 예전엔 둘 다 패널 폭으로 잡혀 보너스가 많으면
            // 요약 줄이 미리보기 칸 위로 뻗었다. 길이는 보너스 개수가 정하니 LabelFit으로 줄여 맞춘다.
            float footerW = mobile ? panelW - 48f : Mathf.Max(1f, charArea.x - (x + 24f) - 16f);
            if (bonusProvider != null)
            {
                OutfitStatBonus total = bonusProvider.GetTotalBonus();
                if (total.HasAnyBonus())
                {
                    string summary = EquippedSummaryText(total);
                    Rect summaryRect = mobile
                        ? new Rect(x + 24, y + panelH - 76, footerW, 30)
                        : new Rect(x + 24, y + panelH - 86, footerW, 72);
                    GUIStyle summaryStyle = UIHelper.CachedStyle("outfit_summary", BuildSummaryStyle);
                    if (!mobile) summary = SummaryForWidth(summary, summaryStyle, footerW);
                    UIHelper.LabelFit(summaryRect, summary, summaryStyle);
                }
            }

            float coinX = charArea.xMax + 16f;
            Rect coinRect = mobile
                ? new Rect(x + 24, y + panelH - 44, footerW, 36)
                : new Rect(coinX, y + panelH - 68, Mathf.Max(1f, x + panelW - 24f - coinX), 36);
            PlayerCurrencyWallet footerWallet = ResolveWallet();
            int coinCount = (footerWallet != null) ? footerWallet.Coins : 0;
            int gemCount = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0;
            UIHelper.LabelFit(coinRect, FooterText(coinCount, gemCount), mobile ? coinStyle : coinRightStyle);

            // GUI.color 복원
            GUI.color = Color.white;

            UIScale.End();
        }

        /// <summary>
        /// 장착 + 피드백(번쩍임·효과음·세트 완성 판정). 구매 직후 자동 장착도 여기로 온다 —
        /// 예전엔 구매 경로만 <c>Equip</c>을 직접 불러 연출이 없었고, 세트 기억값(<see cref="prevSetStates"/>)이
        /// 낡아서 <b>다음</b> 장착에 엉뚱하게 "세트 완성"이 울렸다.
        /// </summary>
        private void EquipWithFeedback(OutfitItem item)
        {
            if (item == null) return;
            outfitManager.Equip(item.itemId);
            trySelections.Remove(item.slot);
            equipFlashTimer = 0.4f;
            lastEquippedId = item.itemId;
            if (InsectGame.Core.AudioManager.Instance != null)
                InsectGame.Core.AudioManager.Instance.PlaySFX(InsectGame.Core.SfxType.Equip);
            CheckSetCompletion();
        }

        private PlayerCurrencyWallet ResolveWallet()
        {
            if (walletCache == null && outfitManager != null)
                walletCache = outfitManager.GetComponent<PlayerCurrencyWallet>() ??
                    FindFirstObjectByType<PlayerCurrencyWallet>();
            return walletCache;
        }

        private bool CanAffordCoins(int price)
        {
            if (AuthManager.Instance != null && AuthManager.Instance.MasterPrivilegesActive) return true;
            PlayerCurrencyWallet w = ResolveWallet();
            return w != null && w.Coins >= price;
        }

        private void CheckSetCompletion()
        {
            if (bonusProvider == null) return;

            ActiveSetInfo[] sets = bonusProvider.GetActiveSets();
            foreach (ActiveSetInfo setInfo in sets)
            {
                bool nowActive = setInfo.isPartialActive || setInfo.isFullActive;
                bool wasActive = prevSetStates.TryGetValue(setInfo.set.setId, out bool prev) && prev;

                if (nowActive && !wasActive)
                {
                    setCompleteFlashTimer = 1f;
                    if (InsectGame.Core.AudioManager.Instance != null)
                        InsectGame.Core.AudioManager.Instance.PlaySFX(InsectGame.Core.SfxType.SetComplete);
                }

                prevSetStates[setInfo.set.setId] = nowActive;
            }
        }

        // ── 해금 조건 문구 ──

        // regionId → 표시명. RegionDefinitions에서 1회만 파생한다(이름을 여기 박으면 낡는다).
        private static Dictionary<string, string> regionNameCache;

        /// <summary>
        /// <c>OutfitItem.unlockCondition</c>의 원문 토큰을 사람이 읽는 문장으로 바꾼다.
        ///
        /// 이 값은 UI에 그대로 그려지던 자리다 — 한국어 게임에서 "region_garden"·"level_15"가
        /// 카드에 노출됐다. 알 수 없는 형식은 토큰을 그대로 돌려주므로, 새 조건 형식을 추가해도
        /// 화면이 비지는 않는다(대신 여기 분기를 늘려 문장을 붙일 것).
        ///
        /// <b>해금 판정은 여기서 하지 않는다</b> — <see cref="OutfitUnlockRules"/>가 하고
        /// <c>CharacterOutfitManager.EvaluateUnlocks</c>가 소유를 준다. 형식을 늘리면 두 곳을 함께 고친다.
        /// </summary>
        internal static string DescribeUnlockCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return "";
            // 잠긴 카드마다·OnGUI 패스마다 불린다 — 보간 문자열을 매번 만들지 않는다.
            if (UnlockTextCache.TryGetValue(condition, out string cached)) return cached;

            string text;
            bool cacheable = true;
            if (condition.StartsWith(OutfitUnlockRules.RegionPrefix))
            {
                string regionId = condition.Substring(OutfitUnlockRules.RegionPrefix.Length);
                text = $"{RegionDisplayName(regionId)} 도달 시 해금";
            }
            else if (condition.StartsWith(OutfitUnlockRules.LevelPrefix))
            {
                text = OutfitUnlockRules.TryParseLevel(condition, out int n) ? $"Lv.{n} 달성 시 해금" : condition;
            }
            else if (condition.StartsWith(OutfitUnlockRules.QuestPrefix))
            {
                // 어느 퀘스트인지 말해 준다. 퀘스트 매니저가 아직 없으면(테스트·부팅 직후) 일반 문구로
                // 물러나고 캐시하지 않는다 — 한 번 굳으면 제목이 영영 안 뜬다.
                string questId = condition.Substring(OutfitUnlockRules.QuestPrefix.Length);
                string title = TutorialQuestManager.Instance != null
                    ? TutorialQuestManager.Instance.GetQuestTitle(questId) : null;
                if (string.IsNullOrEmpty(title)) { text = "특정 퀘스트 완료 시 해금"; cacheable = false; }
                else text = $"'{title}' 완료 시 해금";
            }
            else
            {
                text = condition;   // 미지의 형식 — 토큰이라도 보여 준다
            }

            if (cacheable) UnlockTextCache[condition] = text;
            return text;
        }

        private static readonly Dictionary<string, string> UnlockTextCache = new Dictionary<string, string>();

        private static string RegionDisplayName(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return "특정 지역";
            if (regionNameCache == null)
            {
                regionNameCache = new Dictionary<string, string>();
                foreach (Data.RegionData r in RegionDefinitions.CreateAll())
                {
                    if (r != null && !string.IsNullOrEmpty(r.regionId))
                        regionNameCache[r.regionId] = r.displayName;
                }
            }
            return regionNameCache.TryGetValue(regionId, out string name) && !string.IsNullOrEmpty(name)
                ? name : regionId;
        }
    }
}
