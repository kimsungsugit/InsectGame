using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;   // DexBrowseLayout — 도감이 쓰는 순수 뷰포트 컬링 계산을 공유한다
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 훈련소 — 곤충 선택 → 훈련 방법 → (성장 훈련 | 기술 습득 | 기술 장착 | 기술 교체).
    ///
    /// 「성장 훈련」은 레벨업과 능력치(개체값 HP·공격·방어) 훈련을 한 화면에 둔다. 등급(S~D)은 개체값 합에서
    /// 파생되므로 능력치를 올리면 등급이 저절로 오른다. 가격은 전부 <c>TrainingManager</c> →
    /// <c>TrainingPricing</c>에서 받는다 — 이 화면은 값을 만들지 않는다.
    ///
    /// 표면·색은 배틀팀·보유 곤충과 같은 규칙(<c>UISurface</c> + <c>UITheme</c> 토큰)을 따른다. 한때 이 화면만
    /// 영어 제목("TRAINING CENTER"·"&lt; Back"·"Equip")에 기본 스킨 버튼 10개였고, 기술 설명이 3종만 알아
    /// 회복·기절·독·방어 기술을 "ATK DOWN"으로 적었다.
    /// </summary>
    public class TrainingUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private TrainingManager trainingManager;
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private PlayerCandyInventory candyInventory;

        private bool isOpen;

        private enum Page { InsectSelect, MethodSelect, Growth, SkillLearn, SkillEquip, SkillReplace }
        private Page page;
        private string selectedInstanceId;
        private int selectedMethodIndex = -1;
        private string pendingNewSkillId;
        private Vector2 scrollPos;
        private readonly UIDirectScroll directScroll = new UIDirectScroll();
        private string feedbackMsg;
        private float feedbackTimer;
        private bool feedbackBig;   // 등급 상승·습득 완료처럼 큰 소식은 앰버로 띄운다

        // ── 스타일 캐시 — OnGUI 매 프레임 new GUIStyle 금지. 동적인 건 textColor뿐이다. ──
        private bool stylesReady;
        private GUIStyle guideStyle;      // 헤더 아래 안내 한 줄
        private GUIStyle titleStyle;      // 카드 제목(색 동적)
        private GUIStyle infoStyle;       // 카드 둘째 줄
        private GUIStyle detailStyle;     // 셋째 줄·진척 숫자
        private GUIStyle descStyle;       // 방식 설명(두 줄 래핑)
        private GUIStyle sectionStyle;    // 소제목
        private GUIStyle buttonStyle;     // UISurface.Button 라벨(richText — 둘째 줄에 비용)
        private GUIStyle gradeStyle;      // 등급 큰 글자(색 동적)
        private GUIStyle valueStyle;      // "12 / 15"
        private GUIStyle slotNumStyle;
        private GUIStyle emptyStyle;
        private GUIStyle feedbackStyle;

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            UITheme t = UITheme.Instance;

            guideStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleLeft };
            guideStyle.normal.textColor = t.textSecondary;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                wordWrap = true, clipping = TextClipping.Clip
            };
            infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, alignment = TextAnchor.MiddleLeft };
            infoStyle.normal.textColor = t.textSecondary;
            detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            detailStyle.normal.textColor = t.textMuted;
            descStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, alignment = TextAnchor.UpperLeft, wordWrap = true };
            descStyle.normal.textColor = t.textSecondary;
            sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            sectionStyle.normal.textColor = t.textPrimary;
            buttonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true
            };
            buttonStyle.normal.textColor = t.textPrimary;
            gradeStyle = new GUIStyle(GUI.skin.label) { fontSize = 60, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            valueStyle.normal.textColor = t.textPrimary;
            slotNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            slotNumStyle.normal.textColor = t.textSecondary;
            emptyStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleLeft };
            emptyStyle.normal.textColor = t.textMuted;
            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                wordWrap = true, clipping = TextClipping.Clip
            };
        }

        // GetAllOwned 매 프레임 호출 회피 — InsectUpdated 이벤트로 invalidate (CollectionUI 패턴).
        private List<PlayerInsectData> cachedOwned;
        private bool ownedCacheDirty = true;

        // 기술 목록 캐시 — GetAvailableSkills는 호출마다 List·배열을 새로 만든다. 습득·교체·레벨업은
        // 전부 InsectUpdated로 오고, 방식·곤충을 바꾸는 건 페이지 전환이라 그 둘에서 비운다.
        private InsectSkill[] cachedSkills;
        private bool skillsCacheDirty = true;

        private List<PlayerInsectData> GetCachedOwned()
        {
            if (collection == null) return null;
            if (ownedCacheDirty || cachedOwned == null)
            {
                cachedOwned = collection.GetAllOwned();
                ownedCacheDirty = false;
            }
            return cachedOwned;
        }

        /// <summary>
        /// 목록 한 줄의 정보 문구("Lv.n · 개체값 S · 기술 a/6 · 크기") 캐시.
        /// 개체마다 문자열 2개와 크기 계산이 들던 자리인데 목록 루프 안이라 개체 수 × OnGUI 패스마다
        /// 반복됐다. 값은 레벨·개체값·습득 수·크기에서만 파생되고 그 변화는 전부 <c>InsectUpdated</c>로
        /// 오므로 보유 목록 캐시와 <b>같은 신호로 함께</b> 비운다.
        /// </summary>
        private readonly Dictionary<string, string> ownedInfoCache = new Dictionary<string, string>();

        private string OwnedInfoLine(PlayerInsectData pid, InsectData data)
        {
            string key = pid.instanceId;
            if (string.IsNullOrEmpty(key)) return BuildOwnedInfoLine(pid, data);
            if (ownedInfoCache.TryGetValue(key, out string cached)) return cached;

            string built = BuildOwnedInfoLine(pid, data);
            ownedInfoCache[key] = built;
            return built;
        }

        private static string BuildOwnedInfoLine(PlayerInsectData pid, InsectData data)
        {
            int learned = pid.learnedSkillIds != null ? pid.learnedSkillIds.Count : 0;
            string sizeStr = data != null
                ? "  ·  " + InsectSizeCalculator.SizeLabel(InsectSizeCalculator.SizeMm(data, pid))
                : string.Empty;
            return $"Lv.{pid.level}  ·  개체값 {CapturePopupUI.GetGradeLabel(pid.Grade)}"
                + $"  ·  기술 {learned}/{PlayerInsectData.MaxLearnedSkills}{sizeStr}";
        }

        // 헤더 부제("캔디 340")는 값이 바뀔 때만 다시 만든다 — OnGUI는 프레임당 여러 패스다.
        private string candySubtitle = string.Empty;
        private int candySubtitleValue = int.MinValue;

        private string CandySubtitle()
        {
            int candy = candyInventory != null ? candyInventory.Candies : 0;
            if (candy != candySubtitleValue)
            {
                candySubtitleValue = candy;
                candySubtitle = $"캔디 {candy}";
            }
            return candySubtitle;
        }

        private void HandleInsectUpdated(PlayerInsectData _)
        {
            ownedCacheDirty = true;
            skillsCacheDirty = true;
            ownedInfoCache.Clear();   // 레벨업·능력치·스킬 습득·장착 변경이 전부 이 신호로 온다
        }

        private void OnEnable()
        {
            if (collection != null)
            {
                collection.InsectUpdated -= HandleInsectUpdated;
                collection.InsectUpdated += HandleInsectUpdated;
            }
            ownedCacheDirty = true;
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen)
            {
                ChangePage(Page.InsectSelect);
                selectedInstanceId = null;
                selectedMethodIndex = -1;
            }
            else
            {
                directScroll.Reset();
                pendingNewSkillId = null;   // 교체 화면에서 닫으면 낡은 ID가 남지 않게
            }
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            pendingNewSkillId = null;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable()
        {
            isOpen = false;
            pendingNewSkillId = null;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
            if (collection != null)
                collection.InsectUpdated -= HandleInsectUpdated;
        }

        private void Update()
        {
            if (feedbackTimer > 0) feedbackTimer -= Time.deltaTime;
        }

        private void ChangePage(Page nextPage)
        {
            page = nextPage;
            scrollPos = Vector2.zero;
            directScroll.Reset();
            skillsCacheDirty = true;
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            InitStyles();
            UIScale.Begin();
            switch (page)
            {
                case Page.InsectSelect: DrawInsectSelect(); break;
                case Page.MethodSelect: DrawMethodSelect(); break;
                case Page.Growth: DrawGrowth(); break;
                case Page.SkillLearn: DrawSkillLearn(); break;
                case Page.SkillEquip: DrawSkillEquip(); break;
                case Page.SkillReplace: DrawSkillReplace(); break;
            }

            if (feedbackTimer > 0)
                DrawFeedback();
            UIScale.End();
        }

        // ── 공통 틀 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 창 자리 — 배틀팀·보유 곤충과 같은 크기. 세로 화면은 높이를 크게 잡는다(1000×1000 고정일 때
        /// 세로 화면 가운데 절반만 쓰고 위아래가 비었다). 안전 영역을 넘으면 하네스가 줄인다.
        /// </summary>
        private static Rect PanelRect()
        {
            bool mobile = UIScale.IsMobileLayout;
            return UISafeLayout.CenteredPanel(mobile ? 1000f : 1040f, mobile ? 1560f : 960f);
        }

        /// <summary>카드 + 코랄 헤더. 헤더 오른쪽 버튼(닫기/뒤로)을 눌렀으면 true.</summary>
        private static bool DrawShell(string title, string subtitle, string actionLabel, out Rect panel)
        {
            panel = PanelRect();
            UISurface.Card(panel);
            return UISurface.Header(new Rect(panel.x + 3f, panel.y + 3f, panel.width - 6f, 88f), title, subtitle, actionLabel);
        }

        private static float ActionHeight => SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 58f, 68f);

        /// <summary>세로로 긴 레일 — 얇은 것은 각지게, 세로를 카드 반경만큼 물린다(ui-layout.md).</summary>
        private static void DrawRail(Rect card, Color color)
        {
            UISurface.Flat(new Rect(card.x + 3f, card.y + 3f + UITheme.Radius.Card, 6f,
                Mathf.Max(4f, card.height - 6f - UITheme.Radius.Card * 2f)), color);
        }

        private void DrawThumb(Rect frame, InsectData data, PlayerInsectData pid)
        {
            UITheme t = UITheme.Instance;
            Color rc = data != null ? t.GetInsectRarityColor(data.rarity) : t.surfaceBorder;
            UISurface.Rounded(frame, Color.Lerp(t.surfaceBase, rc, 0.14f));
            if (data != null)
                InsectVisual.Draw(frame.center.x, frame.center.y, frame.width * 0.94f, data, pid != null && pid.isShiny, 1f);
        }

        /// <summary>
        /// 선택한 곤충 요약 카드 — 썸네일 · 이름 · Lv·타입 · 오른쪽에 개체값 등급. 방법 선택과 성장 훈련이 같이 쓴다.
        /// <paramref name="rightReserve"/>만큼 오른쪽을 비워 두고(호출부가 버튼을 둔다) 등급은 그 왼쪽에 그린다.
        /// </summary>
        private void DrawInsectSummary(Rect r, PlayerInsectData pid, InsectData data, float rightReserve)
        {
            UITheme t = UITheme.Instance;
            Color rc = data != null ? t.GetInsectRarityColor(data.rarity) : t.textSecondary;
            UISurface.Card(r, t.surfaceRaised, t.surfaceBorder);
            DrawRail(r, rc);

            float thumb = r.height - 24f;
            DrawThumb(new Rect(r.x + 20f, r.y + 12f, thumb, thumb), data, pid);

            float gradeW = 150f;
            float textX = r.x + 20f + thumb + 20f;
            float textW = Mathf.Max(80f, r.xMax - rightReserve - gradeW - 16f - textX);

            titleStyle.normal.textColor = SkillUILayout.GetReadableAccent(rc);
            UIHelper.LabelFit(new Rect(textX, r.y + r.height * 0.5f - 50f, textW, 46f),
                data != null ? (pid.isShiny ? "★ " + data.displayName : data.displayName) : pid.insectId, titleStyle);

            string typeLabel = data != null
                ? InsectTypeChart.GetDisplayName(data.primaryType)
                    + (data.secondaryType != InsectElement.None ? "/" + InsectTypeChart.GetDisplayName(data.secondaryType) : "")
                : "타입 미상";
            UIHelper.LabelFit(new Rect(textX, r.y + r.height * 0.5f + 2f, textW, 38f),
                data != null ? $"Lv.{pid.level}  ·  {data.rarity.Korean()}  ·  {typeLabel}" : $"Lv.{pid.level}", infoStyle);

            // 개체값 등급 — 능력치 훈련의 결과가 곧바로 보이는 자리.
            IVGrade grade = pid.Grade;
            Color gc = t.GetGradeColor(grade);
            Rect gradeBox = new Rect(r.xMax - rightReserve - gradeW - 8f, r.y + 12f, gradeW, r.height - 24f);
            UISurface.Rounded(gradeBox, Color.Lerp(t.surfaceBase, gc, 0.12f));
            detailStyle.alignment = TextAnchor.MiddleCenter;
            UIHelper.LabelFit(new Rect(gradeBox.x, gradeBox.y + 4f, gradeW, 30f), "개체값", detailStyle);
            detailStyle.alignment = TextAnchor.MiddleLeft;
            gradeStyle.normal.textColor = gc;
            GUI.Label(new Rect(gradeBox.x, gradeBox.y + 30f, gradeW, Mathf.Max(40f, gradeBox.height - 34f)),
                CapturePopupUI.GetGradeLabel(grade), gradeStyle);
        }

        // ── 1. 곤충 선택 ────────────────────────────────────────────────────────

        private void DrawInsectSelect()
        {
            UITheme t = UITheme.Instance;
            if (DrawShell("훈련소", CandySubtitle(), "× 닫기", out Rect panel)) { CloseModal(); return; }
            if (collection == null) return;
            List<PlayerInsectData> owned = GetCachedOwned();
            if (owned == null) return;

            UIHelper.LabelFit(new Rect(panel.x + 28f, panel.y + 102f, panel.width - 56f, 48f),
                "훈련할 곤충을 고르세요 — 기술·레벨·능력치를 키웁니다", guideStyle);

            float listY = panel.y + 160f;
            float itemH = UIScale.IsMobileLayout ? 168f : 130f;
            Rect area = new Rect(panel.x + 16f, listY, panel.width - 32f, Mathf.Max(1f, panel.yMax - listY - 16f));
            float contentHeight = owned.Count * itemH;
            Rect view = new Rect(0, 0, area.width - 12f, contentHeight);
            directScroll.Handle(ref scrollPos, area, contentHeight, itemH * 0.35f);
            scrollPos = GUI.BeginScrollView(area, scrollPos, view, GUIStyle.none, GUIStyle.none);

            // 화면에 걸치는 줄만 그린다. IMGUI 스크롤뷰엔 가상화가 없어서, 컬링하지 않으면 보유 곤충
            // 전부에 대해 `InsectVisual.Draw`가 3D 썸네일을 요청한다 — 캐시는 한 뷰포트 분량(24칸)이라
            // 60마리를 매 패스 훑으면 LRU가 안정되지 않고 렌더러가 프레임마다 곤충 모델을 만들었다
            // 부순다(도감에서 P0였던 것과 같은 구조, 2026-08-06 audit).
            DexBrowseLayout.GetVisibleRowRange(
                scrollPos.y, area.height, itemH - 8f, 8f, owned.Count,
                out int firstVisible, out int lastVisible);

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = collection.GetInsectData(pid.insectId);
                Rect r = new Rect(4f, i * itemH, view.width - 4f, itemH - 8f);
                Color rc = data != null ? t.GetInsectRarityColor(data.rarity) : t.textSecondary;

                UISurface.Card(r, t.surfaceRaised, t.surfaceBorder);
                DrawRail(r, rc);
                float thumb = Mathf.Min(r.height - 20f, UIScale.IsMobileLayout ? 140f : 104f);
                DrawThumb(new Rect(r.x + 20f, r.y + (r.height - thumb) / 2f, thumb, thumb), data, pid);

                float textX = r.x + 20f + thumb + 20f;
                float textW = Mathf.Max(80f, r.xMax - 200f - textX);
                // #코드 미표시 — 개체 구분은 아래 줄의 레벨·등급·크기가 맡는다.
                titleStyle.normal.textColor = SkillUILayout.GetReadableAccent(rc);
                UIHelper.LabelFit(new Rect(textX, r.y + r.height * 0.5f - 48f, textW, 46f),
                    data != null ? data.displayName : pid.insectId, titleStyle);
                UIHelper.LabelFit(new Rect(textX, r.y + r.height * 0.5f + 4f, textW, 38f), OwnedInfoLine(pid, data), infoStyle);

                float bh = ActionHeight;
                if (UISurface.Button(new Rect(r.xMax - 180f, r.y + (r.height - bh) / 2f, 160f, bh), "훈련", t.btnPrimary, buttonStyle)
                    && !directScroll.IsDragging)
                {
                    selectedInstanceId = pid.instanceId;
                    ChangePage(Page.MethodSelect);
                }
            }
            GUI.EndScrollView();
            UISurface.ScrollAffordance(area, scrollPos, contentHeight, t.accentCoral);
        }

        // ── 2. 훈련 방법 ────────────────────────────────────────────────────────

        private void DrawMethodSelect()
        {
            UITheme t = UITheme.Instance;
            if (DrawShell("훈련 방법", CandySubtitle(), "‹ 뒤로", out Rect panel)) { ChangePage(Page.InsectSelect); return; }

            PlayerInsectData pid = GetPid();
            if (pid == null) { ChangePage(Page.InsectSelect); return; }
            InsectData data = collection.GetInsectData(pid.insectId);

            Rect summary = new Rect(panel.x + 20f, panel.y + 104f, panel.width - 40f, 150f);
            DrawInsectSummary(summary, pid, data, 220f);
            // "기술 장착"은 surfaceBorder — btnSecondary는 surfaceRaised로 동기화돼 요약 카드와 같은 색이라 버튼이 사라진다.
            float bh = ActionHeight;
            if (UISurface.Button(new Rect(summary.xMax - 216f, summary.y + (summary.height - bh) / 2f, 196f, bh),
                    "기술 장착", t.surfaceBorder, buttonStyle))
            {
                ChangePage(Page.SkillEquip);
                return;
            }

            if (trainingManager == null || trainingManager.Methods == null) return;
            TrainingMethod[] methods = trainingManager.Methods;

            float cardH = UIScale.IsMobileLayout ? 186f : 164f;
            const float gap = 10f;
            float listY = summary.yMax + 16f;
            Rect area = new Rect(panel.x + 16f, listY, panel.width - 32f, Mathf.Max(1f, panel.yMax - listY - 16f));
            int cardCount = methods.Length + 1;   // 맨 위 「성장 훈련」 + 기술 방식들
            float contentHeight = cardCount * (cardH + gap);
            Rect view = new Rect(0, 0, area.width - 12f, contentHeight);
            directScroll.Handle(ref scrollPos, area, contentHeight, cardH * 0.35f);
            scrollPos = GUI.BeginScrollView(area, scrollPos, view, GUIStyle.none, GUIStyle.none);

            // 성장 훈련 — 레벨·능력치는 기술과 달리 방식 데이터(TrainingMethod)가 아니라 곤충 자신의 값이라
            // 카드만 같은 모양으로 두고 목록 맨 위에 고정한다.
            Rect growth = new Rect(4f, 0f, view.width - 4f, cardH);
            RefreshGrowthTexts(pid);
            if (DrawMethodCard(growth, "성장 훈련", "레벨업과 능력치(개체값) 훈련 — 능력치를 올리면 등급이 오릅니다",
                    t.accentAmber, growthStatusText, t.textSecondary, true))
            {
                ChangePage(Page.Growth);
                GUI.EndScrollView();
                return;
            }

            for (int i = 0; i < methods.Length; i++)
            {
                TrainingMethod m = methods[i];
                Rect card = new Rect(4f, (i + 1) * (cardH + gap), view.width - 4f, cardH);
                bool levelOk = pid.level >= m.requiredLevel;
                bool canTrain = trainingManager.CanTrain(m, pid);
                string status;
                Color statusColor;
                if (!levelOk)
                {
                    status = $"곤충 Lv.{m.requiredLevel} 필요";
                    statusColor = t.accentCoral;
                }
                else
                {
                    int count = trainingManager.GetAvailableSkillCount(m, pid);
                    status = m.methodId == TrainingManager.DiscMethodId ? $"디스크 기술 {count}개" : $"배울 수 있는 기술 {count}개";
                    statusColor = count > 0 ? t.textSecondary : t.textMuted;
                }

                if (DrawMethodCard(card, m.displayName, m.description, levelOk ? m.themeColor : t.textMuted,
                        status, statusColor, canTrain))
                {
                    selectedMethodIndex = i;
                    ChangePage(Page.SkillLearn);
                    GUI.EndScrollView();
                    return;
                }
            }
            GUI.EndScrollView();
            UISurface.ScrollAffordance(area, scrollPos, contentHeight, t.accentCoral);
        }

        /// <summary>방법 카드 한 장 — 레일 · 이름 · 설명(두 줄) · 상태 줄 · [시작]. 눌렀으면 true.</summary>
        private bool DrawMethodCard(Rect r, string name, string description, Color accent, string status, Color statusColor, bool enabled)
        {
            UITheme t = UITheme.Instance;
            UISurface.Card(r, enabled ? t.surfaceRaised : t.surfaceCard, t.surfaceBorder);
            DrawRail(r, accent);

            float textX = r.x + 28f;
            float textW = Mathf.Max(80f, r.width - 250f);
            titleStyle.normal.textColor = SkillUILayout.GetReadableAccent(accent);
            UIHelper.LabelFit(new Rect(textX, r.y + 12f, textW, 44f), name, titleStyle);
            UIHelper.LabelFit(new Rect(textX, r.y + 58f, textW, Mathf.Max(34f, r.height - 58f - 46f)), description, descStyle);
            infoStyle.normal.textColor = statusColor;
            UIHelper.LabelFit(new Rect(textX, r.yMax - 42f, textW, 34f), status, infoStyle);
            infoStyle.normal.textColor = t.textSecondary;

            float bh = ActionHeight;
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && enabled;
            bool pressed = UISurface.Button(new Rect(r.xMax - 196f, r.y + (r.height - bh) / 2f, 176f, bh), "시작",
                enabled ? t.btnPrimary : t.btnDisabled, buttonStyle);
            GUI.enabled = prevEnabled;
            return pressed && enabled && !directScroll.IsDragging;
        }

        // ── 3. 성장 훈련 ────────────────────────────────────────────────────────

        private static readonly GrowthStat[] GrowthStats = { GrowthStat.Hp, GrowthStat.Attack, GrowthStat.Defense };

        // 버튼 라벨("훈련\n캔디 124")은 비용이 바뀔 때만 다시 만든다 — 줄 넷 × OnGUI 패스마다 보간하지 않는다.
        private readonly string[] growthLabels = new string[4];
        private readonly int[] growthLabelCosts = { -1, -1, -1, -1 };

        private string GrowthButtonLabel(int row, string verb, int cost)
        {
            if (growthLabelCosts[row] != cost || growthLabels[row] == null)
            {
                growthLabelCosts[row] = cost;
                growthLabels[row] = $"{verb}\n<size=22>캔디 {cost}</size>";
            }
            return growthLabels[row];
        }

        private static string StatName(GrowthStat stat)
        {
            switch (stat)
            {
                case GrowthStat.Attack: return "공격";
                case GrowthStat.Defense: return "방어";
                default: return "체력";
            }
        }

        private static Color StatColor(GrowthStat stat)
        {
            UITheme t = UITheme.Instance;
            switch (stat)
            {
                case GrowthStat.Attack: return t.accentCoral;
                case GrowthStat.Defense: return t.skillDefense;
                default: return t.accentMint;
            }
        }

        private static readonly string HpEffectText = $"훈련 1회: 최대 HP +{PlayerInsectData.HpPerIv}";
        private static readonly string LevelEffectText =
            $"레벨업: 최대 HP +{GameConstants.Battle.HpPerLevel} · 공격 +{PlayerInsectData.AtkPerLevel} · 방어 +{PlayerInsectData.DefPerLevel}";

        private static string StatEffect(GrowthStat stat)
        {
            switch (stat)
            {
                case GrowthStat.Attack: return "훈련 1회: 공격 +1";
                case GrowthStat.Defense: return "훈련 1회: 방어 +1";
                default: return HpEffectText;
            }
        }

        // 성장 화면의 파생 문구 — 레벨·개체값이 바뀔 때만 다시 만든다(OnGUI 패스마다 보간하지 않는다).
        private string growthTextsFor;
        private int growthTextsLevel = -1;
        private int growthTextsIvSum = -1;
        private int growthTextsHp = -1, growthTextsAtk = -1, growthTextsDef = -1;
        private string growthStatusText = string.Empty;
        private string growthLevelText = string.Empty;
        private string growthFooterText = string.Empty;
        private readonly string[] growthIvTexts = new string[3];

        private void RefreshGrowthTexts(PlayerInsectData pid)
        {
            if (growthTextsFor == pid.instanceId && growthTextsLevel == pid.level && growthTextsIvSum == pid.IvSum
                && growthTextsHp == pid.ivHp && growthTextsAtk == pid.ivAtk && growthTextsDef == pid.ivDef)
                return;
            growthTextsFor = pid.instanceId;
            growthTextsLevel = pid.level;
            growthTextsIvSum = pid.IvSum;
            growthTextsHp = pid.ivHp;
            growthTextsAtk = pid.ivAtk;
            growthTextsDef = pid.ivDef;

            IVGrade grade = pid.Grade;
            growthStatusText = $"Lv.{pid.level}  ·  개체값 등급 {CapturePopupUI.GetGradeLabel(grade)}";
            bool maxLevel = trainingManager != null && trainingManager.IsMaxLevel(pid);
            growthLevelText = maxLevel ? $"Lv.{pid.level} (최대)" : $"Lv.{pid.level} → {pid.level + 1}";
            // 다음 등급까지 개체값 합이 얼마나 남았나. 경계는 PlayerInsectData가 단일 출처.
            growthFooterText = grade == IVGrade.S
                ? $"최고 등급입니다 — 개체값 합 {pid.IvSum}/{PlayerInsectData.MaxIV * 3}"
                : $"다음 등급 {CapturePopupUI.GetGradeLabel(grade + 1)}까지 개체값 +{PlayerInsectData.MinIvSumFor(grade + 1) - pid.IvSum}"
                    + $"  ·  등급은 세 능력치의 합(0~{PlayerInsectData.MaxIV * 3})으로 정해집니다";
            for (int i = 0; i < GrowthStats.Length; i++)
                growthIvTexts[i] = $"{pid.GetIv(GrowthStats[i])} / {PlayerInsectData.MaxIV}";
        }

        private void DrawGrowth()
        {
            UITheme t = UITheme.Instance;
            if (DrawShell("성장 훈련", CandySubtitle(), "‹ 뒤로", out Rect panel)) { ChangePage(Page.MethodSelect); return; }

            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null) { ChangePage(Page.InsectSelect); return; }
            InsectData data = collection.GetInsectData(pid.insectId);

            Rect summary = new Rect(panel.x + 20f, panel.y + 104f, panel.width - 40f, 150f);
            DrawInsectSummary(summary, pid, data, 0f);
            RefreshGrowthTexts(pid);
            const float footerH = 50f;

            float rowsTop = summary.yMax + 14f;
            const float gap = 10f;
            float avail = panel.yMax - rowsTop - footerH - 20f;
            // 세로 화면은 줄이 창 끝까지 차게 둔다 — 240에서 멈추면 아래 1/6이 빈 채로 남았다.
            float rowH = Mathf.Clamp((avail - gap * 3f) / 4f, UIScale.MinTouchHeight + 40f, UIScale.IsMobileLayout ? 320f : 150f);

            // 레벨 — 보유 곤충 창과 같은 가격(TrainingManager가 컬렉션의 정본을 읽는다).
            bool maxLevel = trainingManager.IsMaxLevel(pid);
            int maxLv = collection != null ? collection.GetMaxLevel(pid.insectId) : pid.level;
            int levelCost = trainingManager.GetLevelUpCost(pid);
            Rect levelRow = new Rect(panel.x + 20f, rowsTop, panel.width - 40f, rowH);
            if (DrawGrowthRow(levelRow, "레벨", t.accentAmber, growthLevelText,
                    maxLv > 0 ? (float)pid.level / maxLv : 1f,
                    LevelEffectText,
                    maxLevel ? "최대 레벨" : GrowthButtonLabel(0, "레벨업", levelCost),
                    !maxLevel && trainingManager.CanTrainLevel(pid)))
            {
                int before = pid.level;
                if (trainingManager.TrainLevel(pid))
                    ShowFeedback($"레벨 업!  Lv.{before} → Lv.{pid.level}", true);
            }

            for (int i = 0; i < GrowthStats.Length; i++)
            {
                GrowthStat stat = GrowthStats[i];
                int iv = pid.GetIv(stat);
                bool maxed = iv >= PlayerInsectData.MaxIV;
                int cost = trainingManager.GetStatTrainingCost(pid, stat);
                Rect row = new Rect(panel.x + 20f, rowsTop + (i + 1) * (rowH + gap), panel.width - 40f, rowH);
                if (DrawGrowthRow(row, StatName(stat), StatColor(stat), growthIvTexts[i],
                        (float)iv / PlayerInsectData.MaxIV,
                        maxed ? "이 능력치는 최대입니다" : StatEffect(stat),
                        maxed ? "최대" : GrowthButtonLabel(i + 1, "훈련", cost),
                        !maxed && trainingManager.CanTrainStat(pid, stat)))
                {
                    if (trainingManager.TrainStat(pid, stat))
                    {
                        IVGrade before = trainingManager.LastStatGradeBefore;
                        IVGrade after = pid.Grade;
                        if (after > before)
                            ShowFeedback($"등급 상승!  {CapturePopupUI.GetGradeLabel(before)} → {CapturePopupUI.GetGradeLabel(after)}", true);
                        else
                            ShowFeedback($"{StatName(stat)} 개체값 {pid.GetIv(stat)}/{PlayerInsectData.MaxIV}", false);
                    }
                }
            }

            infoStyle.normal.textColor = t.textMuted;
            UIHelper.LabelFit(new Rect(panel.x + 28f, panel.yMax - footerH - 12f, panel.width - 56f, footerH), growthFooterText, infoStyle);
            infoStyle.normal.textColor = t.textSecondary;
        }

        /// <summary>성장 훈련 한 줄 — 이름 · 현재값 · 막대 · 효과 · [버튼]. 눌렀으면 true.</summary>
        private bool DrawGrowthRow(Rect r, string label, Color accent, string valueText, float ratio, string effect,
            string buttonLabel, bool canPress)
        {
            UITheme t = UITheme.Instance;
            UISurface.Card(r, t.surfaceRaised, t.surfaceBorder);
            DrawRail(r, accent);

            const float blockH = 124f;
            float top = r.y + Mathf.Max(10f, (r.height - blockH) / 2f);
            float textX = r.x + 28f;
            float barW = Mathf.Max(80f, r.width - 28f - 260f);

            titleStyle.normal.textColor = SkillUILayout.GetReadableAccent(accent);
            UIHelper.LabelFit(new Rect(textX, top, 120f, 44f), label, titleStyle);
            UIHelper.LabelFit(new Rect(textX + 124f, top, Mathf.Max(40f, barW - 124f), 44f), valueText, valueStyle);
            UISurface.Meter(new Rect(textX, top + 54f, barW, 16f), ratio, accent);
            UIHelper.LabelFit(new Rect(textX, top + 80f, barW, 36f), effect, infoStyle);

            float bh = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 76f, 84f);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && canPress;
            bool pressed = UISurface.Button(new Rect(r.xMax - 226f, r.y + (r.height - bh) / 2f, 206f, bh), buttonLabel,
                canPress ? t.btnPrimary : t.btnDisabled, buttonStyle);
            GUI.enabled = prevEnabled;
            return pressed && canPress;
        }

        // ── 4. 기술 습득 ────────────────────────────────────────────────────────

        private InsectSkill[] GetCachedSkills(TrainingMethod method, PlayerInsectData pid)
        {
            if (skillsCacheDirty || cachedSkills == null)
            {
                cachedSkills = trainingManager.GetAvailableSkills(method, pid);
                skillsCacheDirty = false;
            }
            return cachedSkills;
        }

        private void DrawSkillLearn()
        {
            UITheme t = UITheme.Instance;
            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null || selectedMethodIndex < 0
                || trainingManager.Methods == null || selectedMethodIndex >= trainingManager.Methods.Length)
            {
                ChangePage(Page.MethodSelect);
                return;
            }
            TrainingMethod method = trainingManager.Methods[selectedMethodIndex];
            if (DrawShell(method.displayName, CandySubtitle(), "‹ 뒤로", out Rect panel)) { ChangePage(Page.MethodSelect); return; }

            bool isDisc = method.methodId == TrainingManager.DiscMethodId;
            UIHelper.LabelFit(new Rect(panel.x + 28f, panel.y + 102f, panel.width - 56f, 48f),
                isDisc ? "디스크 1장으로 바로 배웁니다 — 캔디는 들지 않습니다"
                       : "필요 횟수만큼 훈련하면 배웁니다 — 캔디는 회마다 듭니다. 강한 기술일수록 비싸고 오래 걸립니다",
                guideStyle);

            InsectSkill[] skills = GetCachedSkills(method, pid);
            float listY = panel.y + 160f;
            float itemH = UIScale.IsMobileLayout ? 200f : 168f;
            Rect area = new Rect(panel.x + 16f, listY, panel.width - 32f, Mathf.Max(1f, panel.yMax - listY - 16f));
            float contentHeight = skills.Length * itemH;
            Rect view = new Rect(0, 0, area.width - 12f, contentHeight);
            directScroll.Handle(ref scrollPos, area, contentHeight, itemH * 0.35f);
            scrollPos = GUI.BeginScrollView(area, scrollPos, view, GUIStyle.none, GUIStyle.none);

            if (skills.Length == 0)
            {
                GUI.EndScrollView();
                UIHelper.LabelFit(new Rect(area.x + 12f, area.y + 12f, area.width - 24f, 48f),
                    isDisc ? "이 곤충에게 쓸 수 있는 디스크가 없습니다" : "지금 배울 수 있는 기술이 없습니다 — 레벨을 올려 보세요",
                    emptyStyle);
                return;
            }

            for (int i = 0; i < skills.Length; i++)
            {
                InsectSkill skill = skills[i];
                if (skill == null) continue;
                Rect r = new Rect(4f, i * itemH, view.width - 4f, itemH - 8f);
                bool learned = pid.HasLearnedSkill(skill.skillId);

                int required = trainingManager.GetRequiredSessions(method, skill);
                int trainingCost = trainingManager.GetTrainingCost(method, pid, skill.skillId);
                // 가격 줄 — 이 화면에서 가장 중요한 숫자다(요구: 기술 가격은 능력에 맞게). 배운 기술엔 가격을 안 적는다.
                string costLine = learned ? $"쿨다운 {skill.cooldownTurns}턴"
                    : isDisc ? $"쿨다운 {skill.cooldownTurns}턴  ·  보유 디스크 {trainingManager.GetDiscCount(skill.skillId)}장"
                    : $"쿨다운 {skill.cooldownTurns}턴  ·  습득까지 {required}회 · 총 {trainingCost * required} 캔디";
                DrawSkillCard(r, skill, learned, costLine, 230f);

                if (learned)
                {
                    UISurface.Chip(new Rect(r.xMax - 206f, r.y + r.height / 2f - 22f, 186f, 44f), "습득 완료",
                        Color.Lerp(t.surfaceBase, t.accentMint, 0.3f), t.accentMint);
                    continue;
                }

                int progress = pid.GetTrainingProgress(skill.skillId);
                if (required > 1) DrawTrainingProgressBar(r, progress, required, method.themeColor, 250f);

                bool canAfford = trainingManager.CanTrain(method, pid, skill.skillId);
                // **교체는 마지막 회차에만 묻는다.** 누적 훈련은 여러 번 눌러야 습득되는데
                // 슬롯이 찼다고 매 회차 교체 화면으로 보내면, 아직 배우지도 않은 기술 때문에
                // 멀쩡한 기술을 몇 번이고 버리라고 묻는 꼴이 된다.
                bool isFinalSession = progress + 1 >= required;
                bool needsReplace = isFinalSession && pid.IsSkillsFull();

                // 디스크 방식은 습득 회차에 디스크 1장이 **소모**된다 — 버튼에 그 사실을 적는다.
                // 옛은 캔디 1만 보여 줘서 Legendary 디스크가 아무 표시 없이 사라졌다.
                string verb = isDisc ? (needsReplace ? "교체" : "디스크 사용")
                    : needsReplace ? "교체"
                    : required > 1 ? $"훈련 {Mathf.Min(progress + 1, required)}/{required}"
                    : "습득";
                string btnLabel = isDisc ? $"{verb}\n<size=22>디스크 1장</size>" : $"{verb}\n<size=22>캔디 {trainingCost}</size>";

                float bh = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 76f, 84f);
                bool prevEnabled = GUI.enabled;
                GUI.enabled = prevEnabled && canAfford;
                bool pressed = UISurface.Button(new Rect(r.xMax - 226f, r.y + (r.height - bh) / 2f, 206f, bh), btnLabel,
                    canAfford ? (needsReplace ? t.accentAmber : t.btnPrimary) : t.btnDisabled, buttonStyle);
                GUI.enabled = prevEnabled;

                if (pressed && canAfford && !directScroll.IsDragging)
                {
                    if (needsReplace)
                    {
                        pendingNewSkillId = skill.skillId;
                        ChangePage(Page.SkillReplace);
                        GUI.EndScrollView();
                        return;
                    }
                    if (trainingManager.TrainSkill(method, pid, skill.skillId))
                    {
                        // 습득 회차인지 중간 회차인지 말해 준다 — 안 그러면 캔디만 나가고
                        // 아무 일도 안 일어난 것처럼 보인다(누적 훈련의 가장 큰 함정).
                        if (trainingManager.LastTrainingLearned)
                            ShowFeedback(isDisc ? $"{skill.displayName} 습득! (디스크 1장 소모)" : $"{skill.displayName} 습득!", true);
                        else
                            ShowFeedback($"{skill.displayName} 훈련 {trainingManager.LastTrainingProgress}/{trainingManager.LastTrainingRequired}", false);
                    }
                }
            }
            GUI.EndScrollView();
            UISurface.ScrollAffordance(area, scrollPos, contentHeight, t.accentCoral);
        }

        /// <summary>
        /// 누적 훈련 진행 바. <b>얇은 것은 각지게</b> 그린다(<c>UISurface.Flat</c>) —
        /// 둥근 배경은 9-slice라 짧은 변이 반경+4보다 작으면 슬라이스가 겹쳐 뭉개진다
        /// (<c>rules/ui-layout.md</c>).
        /// </summary>
        private void DrawTrainingProgressBar(Rect r, int progress, int required, Color accent, float rightReserve)
        {
            UITheme t = UITheme.Instance;
            float barW = r.width - 28f - rightReserve - 80f;
            if (barW < 40f) return;
            progress = Mathf.Clamp(progress, 0, required);
            Rect track = new Rect(r.x + 28f, r.yMax - 22f, barW, 8f);
            UISurface.Flat(track, Color.Lerp(t.surfaceBase, Color.black, 0.4f));
            if (progress > 0)
                UISurface.Flat(new Rect(track.x, track.y, track.width * progress / required, track.height), accent);
            UIHelper.LabelFit(new Rect(track.xMax + 12f, track.y - 12f, 70f, 30f), $"{progress}/{required}", detailStyle);
        }

        /// <summary>기술 카드 — 레일 · 이름 · 속성·효과 · 셋째 줄. 오른쪽 <paramref name="rightReserve"/>는 버튼 자리.</summary>
        private void DrawSkillCard(Rect r, InsectSkill skill, bool learned, string thirdLine, float rightReserve)
        {
            UITheme t = UITheme.Instance;
            Color sc = t.GetSkillColor(skill.effectType);
            UISurface.Card(r, learned ? t.surfaceBase : t.surfaceRaised, t.surfaceBorder);
            DrawRail(r, learned ? Color.Lerp(sc, t.surfaceBase, 0.5f) : sc);

            float textX = r.x + 28f;
            float textW = Mathf.Max(80f, r.width - 28f - rightReserve);
            titleStyle.normal.textColor = learned ? t.textSecondary : SkillUILayout.GetReadableAccent(sc);
            UIHelper.LabelFit(new Rect(textX, r.y + 10f, textW, 44f), skill.displayName, titleStyle);
            UIHelper.LabelFit(new Rect(textX, r.y + 56f, textW, 36f),
                $"{InsectTypeChart.GetDisplayName(skill.element)} 타입  ·  {SkillEffectText(skill)}", infoStyle);
            if (!string.IsNullOrEmpty(thirdLine))
            {
                // 셋째 줄은 가격·교체 안내라 흐린 textMuted로 두면 안 읽힌다(검수 캡처에서 확인).
                detailStyle.normal.textColor = learned ? t.textMuted : t.textSecondary;
                UIHelper.LabelFit(new Rect(textX, r.y + 94f, textW, 32f), thirdLine, detailStyle);
                detailStyle.normal.textColor = t.textMuted;
            }
        }

        /// <summary>효과 한 줄 — 7종 전부(옛 화면은 피해·공격 상승 외엔 전부 "ATK DOWN"으로 적었다).</summary>
        private static string SkillEffectText(InsectSkill skill)
        {
            switch (skill.effectType)
            {
                case SkillEffectType.BuffAttack:
                    return $"공격 +{skill.effectValue * 100f:0}% · {skill.effectDurationTurns}턴";
                case SkillEffectType.DebuffAttack:
                    return $"상대 공격 -{skill.effectValue * 100f:0}% · {skill.effectDurationTurns}턴";
                case SkillEffectType.DefenseBuff:
                    return $"방어 +{skill.effectValue * 100f:0}% · {skill.effectDurationTurns}턴";
                case SkillEffectType.Heal:
                    return $"HP {skill.effectValue * 100f:0}% 회복";
                case SkillEffectType.PoisonDot:
                    return $"독 {skill.power} × {skill.effectDurationTurns}턴";
                case SkillEffectType.Stun:
                    return "상대 행동 1회 봉인";
                default:
                    return skill.accuracy < 0.999f
                        ? $"위력 {skill.power} · 명중 {skill.accuracy * 100f:0}%"
                        : $"위력 {skill.power}";
            }
        }

        // ── 5. 기술 장착 ────────────────────────────────────────────────────────

        private string equipSubtitle = string.Empty;
        private int equipSubtitleCount = -1;

        private void DrawSkillEquip()
        {
            UITheme t = UITheme.Instance;
            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null) { ChangePage(Page.InsectSelect); return; }

            int equipped = pid.EquippedCount();
            if (equipped != equipSubtitleCount)
            {
                equipSubtitleCount = equipped;
                equipSubtitle = $"{equipped}/{PlayerInsectData.MaxEquipSlots} 장착";
            }
            if (DrawShell("기술 장착", equipSubtitle, "‹ 뒤로", out Rect panel)) { ChangePage(Page.MethodSelect); return; }

            InsectData data = collection.GetInsectData(pid.insectId);
            UIHelper.LabelFit(new Rect(panel.x + 28f, panel.y + 102f, panel.width - 56f, 44f),
                $"{(data != null ? data.displayName : pid.insectId)} — 전투에 들고 나갈 기술 {PlayerInsectData.MaxEquipSlots}개",
                sectionStyle);

            float slotY = panel.y + 154f;
            float slotH = UIScale.IsMobileLayout ? 128f : 100f;
            const float slotGap = 8f;
            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
            {
                Rect slot = new Rect(panel.x + 20f, slotY + i * (slotH + slotGap), panel.width - 40f, slotH);
                string eqId = pid.GetEquippedSkill(i);
                InsectSkill eqSkill = eqId != null ? trainingManager.GetSkill(eqId) : null;

                UISurface.Card(slot, eqSkill != null ? t.surfaceRaised : t.surfaceBase, t.surfaceBorder);
                Rect badge = new Rect(slot.x + 16f, slot.center.y - 22f, 44f, 44f);
                UISurface.Rounded(badge, t.surfaceBase, UITheme.Radius.Chip);
                GUI.Label(badge, (i + 1).ToString(), slotNumStyle);

                if (eqSkill == null)
                {
                    UIHelper.LabelFit(new Rect(slot.x + 80f, slot.center.y - 20f, slot.width - 120f, 40f), "빈 슬롯", emptyStyle);
                    continue;
                }

                Color sc = t.GetSkillColor(eqSkill.effectType);
                UISurface.Flat(new Rect(slot.x + 70f, slot.y + UITheme.Radius.Card, 5f, slot.height - UITheme.Radius.Card * 2f), sc);
                float textW = Mathf.Max(80f, slot.width - 80f - 200f);
                titleStyle.normal.textColor = SkillUILayout.GetReadableAccent(sc);
                UIHelper.LabelFit(new Rect(slot.x + 90f, slot.center.y - 44f, textW, 44f), eqSkill.displayName, titleStyle);
                UIHelper.LabelFit(new Rect(slot.x + 90f, slot.center.y + 2f, textW, 36f),
                    $"{SkillEffectText(eqSkill)}  ·  쿨다운 {eqSkill.cooldownTurns}턴", infoStyle);

                float bh = ActionHeight;
                if (UISurface.Button(new Rect(slot.xMax - 176f, slot.center.y - bh / 2f, 156f, bh), "해제",
                        Color.Lerp(t.btnDanger, t.surfaceRaised, 0.35f), buttonStyle))
                {
                    pid.EquipSkill(null, i);
                    // **`ForceSave`만으로는 부족하다** — 그건 디스크에만 쓰고 `InsectUpdated`를
                    // 쏘지 않는다. 보유 목록의 문자열 캐시(`ownedInfoCache`)는 그 이벤트로만
                    // 비워지므로, 여기서 알리지 않으면 이 화면 자신의 장착·해제만 목록에 반영되지 않는다.
                    collection.NotifyInsectChanged(pid);
                    collection.ForceSave();
                }
            }

            float learnedY = slotY + PlayerInsectData.MaxEquipSlots * (slotH + slotGap) + 10f;
            List<string> learned = pid.learnedSkillIds ?? new List<string>();
            UIHelper.LabelFit(new Rect(panel.x + 28f, learnedY, panel.width - 56f, 44f),
                $"배운 기술 {learned.Count}/{PlayerInsectData.MaxLearnedSkills}", sectionStyle);

            float listY = learnedY + 52f;
            float itemH = UIScale.IsMobileLayout ? 116f : 96f;
            Rect area = new Rect(panel.x + 16f, listY, panel.width - 32f, Mathf.Max(1f, panel.yMax - listY - 16f));
            float contentHeight = learned.Count * itemH;
            Rect view = new Rect(0, 0, area.width - 12f, contentHeight);
            directScroll.Handle(ref scrollPos, area, contentHeight, itemH * 0.35f);
            scrollPos = GUI.BeginScrollView(area, scrollPos, view, GUIStyle.none, GUIStyle.none);

            for (int i = 0; i < learned.Count; i++)
            {
                InsectSkill sk = trainingManager.GetSkill(learned[i]);
                if (sk == null) continue;
                Rect r = new Rect(4f, i * itemH, view.width - 4f, itemH - 8f);
                bool isEquipped = IsEquipped(pid, sk.skillId);
                Color sc = t.GetSkillColor(sk.effectType);

                UISurface.Card(r, isEquipped ? t.surfaceBase : t.surfaceRaised, t.surfaceBorder);
                DrawRail(r, isEquipped ? Color.Lerp(sc, t.surfaceBase, 0.5f) : sc);
                float textW = Mathf.Max(80f, r.width - 28f - 200f);
                titleStyle.normal.textColor = isEquipped ? t.textSecondary : SkillUILayout.GetReadableAccent(sc);
                UIHelper.LabelFit(new Rect(r.x + 28f, r.center.y - 42f, textW, 42f), sk.displayName, titleStyle);
                UIHelper.LabelFit(new Rect(r.x + 28f, r.center.y + 2f, textW, 34f), SkillEffectText(sk), infoStyle);

                if (isEquipped)
                {
                    UISurface.Chip(new Rect(r.xMax - 176f, r.center.y - 20f, 156f, 40f), "장착 중",
                        Color.Lerp(t.surfaceBase, t.accentMint, 0.3f), t.accentMint);
                }
                else if (pid.EquippedCount() < PlayerInsectData.MaxEquipSlots)
                {
                    float bh = ActionHeight;
                    if (UISurface.Button(new Rect(r.xMax - 176f, r.center.y - bh / 2f, 156f, bh), "장착", t.btnPrimary, buttonStyle)
                        && !directScroll.IsDragging)
                    {
                        for (int s = 0; s < PlayerInsectData.MaxEquipSlots; s++)
                        {
                            if (pid.GetEquippedSkill(s) == null)
                            {
                                pid.EquipSkill(sk.skillId, s);
                                collection.NotifyInsectChanged(pid);   // 위 '해제'와 같은 이유 — 목록 캐시 무효화
                                collection.ForceSave();
                                // q_equip 진행도 — 사용자 직접 장착만 카운트
                                // (TrainingManager 자동 장착/PlayerInsectCollection 마이그레이션은 제외)
                                TutorialQuestManager.Instance?.NotifySkillEquipped();
                                break;
                            }
                        }
                    }
                }
            }
            GUI.EndScrollView();
            UISurface.ScrollAffordance(area, scrollPos, contentHeight, t.accentCoral);
        }

        // ── 6. 기술 교체 ────────────────────────────────────────────────────────

        private void DrawSkillReplace()
        {
            UITheme t = UITheme.Instance;
            if (DrawShell("기술 교체", $"{PlayerInsectData.MaxLearnedSkills}/{PlayerInsectData.MaxLearnedSkills} 가득 참", "‹ 뒤로", out Rect panel))
            {
                pendingNewSkillId = null;
                ChangePage(Page.SkillLearn);
                return;
            }

            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null || string.IsNullOrEmpty(pendingNewSkillId))
            {
                ChangePage(Page.SkillLearn);
                return;
            }

            InsectSkill newSkill = trainingManager.GetSkill(pendingNewSkillId);
            if (newSkill == null) { ChangePage(Page.SkillLearn); return; }

            TrainingMethod method = selectedMethodIndex >= 0 && trainingManager.Methods != null
                && selectedMethodIndex < trainingManager.Methods.Length
                ? trainingManager.Methods[selectedMethodIndex] : null;
            int replaceCost = trainingManager.GetTrainingCost(method, pid, pendingNewSkillId);
            bool isDiscReplace = method != null && method.methodId == TrainingManager.DiscMethodId;

            // 새 기술 카드 — 무엇을 얻는지 먼저 보여 준다.
            Rect newCard = new Rect(panel.x + 20f, panel.y + 104f, panel.width - 40f, 136f);
            DrawSkillCard(newCard, newSkill, false,
                isDiscReplace ? "잊을 기술을 고르면 디스크 1장을 써서 바로 배웁니다"
                              : $"잊을 기술을 고르면 캔디 {replaceCost}로 마지막 훈련을 마치고 배웁니다",
                170f);
            UISurface.Chip(new Rect(newCard.xMax - 150f, newCard.y + 12f, 130f, 36f), "새 기술",
                Color.Lerp(t.surfaceBase, t.accentAmber, 0.35f), t.accentAmber);

            float cardH = 140f;
            List<string> learned = pid.learnedSkillIds ?? new List<string>();
            float cancelH = ActionHeight;
            float cancelY = panel.yMax - cancelH - 16f;
            float listTop = newCard.yMax + 16f;
            Rect listArea = new Rect(panel.x + 16f, listTop, panel.width - 32f, Mathf.Max(80f, cancelY - listTop - 12f));
            float contentHeight = GetSkillReplacementContentHeight(learned.Count);
            Rect viewRect = new Rect(0f, 0f, listArea.width - 12f, contentHeight);
            directScroll.Handle(ref scrollPos, listArea, contentHeight, cardH * 0.35f);
            scrollPos = GUI.BeginScrollView(listArea, scrollPos, viewRect, GUIStyle.none, GUIStyle.none);

            for (int i = 0; i < learned.Count; i++)
            {
                InsectSkill old = trainingManager.GetSkill(learned[i]);
                if (old == null) continue;

                Rect r = new Rect(4f, i * (cardH + 4f), viewRect.width - 4f, cardH);
                DrawSkillCard(r, old, false, IsEquipped(pid, old.skillId) ? "장착 중 — 잊으면 새 기술이 그 슬롯에 들어갑니다" : null, 210f);

                float bh = ActionHeight;
                if (UISurface.Button(new Rect(r.xMax - 186f, r.center.y - bh / 2f, 166f, bh), "잊기",
                        Color.Lerp(t.btnDanger, t.surfaceRaised, 0.35f), buttonStyle)
                    && !directScroll.IsDragging)
                {
                    if (method != null && trainingManager.TrainSkill(method, pid, pendingNewSkillId, old.skillId))
                    {
                        ShowFeedback($"{old.displayName}을(를) 잊고 {newSkill.displayName}을(를) 배웠습니다!", true);
                        pendingNewSkillId = null;
                        ChangePage(Page.SkillLearn);
                        GUI.EndScrollView();
                        return;
                    }
                }
            }
            GUI.EndScrollView();
            UISurface.ScrollAffordance(listArea, scrollPos, contentHeight, t.accentCoral);

            if (UISurface.Button(new Rect(panel.center.x - 120f, cancelY, 240f, cancelH), "취소", t.surfaceBorder, buttonStyle))
            {
                pendingNewSkillId = null;
                ChangePage(Page.SkillLearn);
            }
        }

        internal static float GetSkillReplacementContentHeight(int learnedSkillCount)
        {
            return Mathf.Max(0, learnedSkillCount) * 144f;
        }

        // ── 알림 ────────────────────────────────────────────────────────────────

        private void ShowFeedback(string message, bool big)
        {
            feedbackMsg = message;
            feedbackBig = big;
            feedbackTimer = big ? 2.5f : 1.8f;
        }

        /// <summary>헤더 바로 아래 가운데 토스트 — 창 안 목록을 가리지 않게 짧게 띄운다.</summary>
        private void DrawFeedback()
        {
            UITheme t = UITheme.Instance;
            float alpha = Mathf.Clamp01(feedbackTimer / 0.4f);
            Rect panel = PanelRect();
            float w = Mathf.Min(panel.width - 80f, 760f);
            Rect toast = new Rect(panel.center.x - w / 2f, panel.y + 100f, w, 76f);
            Color accent = feedbackBig ? t.accentAmber : t.accentMint;

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            UISurface.Card(toast, t.surfaceCard, Color.Lerp(t.surfaceBorder, accent, 0.7f));
            UISurface.Flat(new Rect(toast.x + UITheme.Radius.Card, toast.y + 3f, toast.width - UITheme.Radius.Card * 2f, 4f), accent);
            GUI.color = prev;

            feedbackStyle.normal.textColor = new Color(accent.r, accent.g, accent.b, alpha);
            UIHelper.LabelFit(new Rect(toast.x + 16f, toast.y + 8f, toast.width - 32f, toast.height - 12f), feedbackMsg, feedbackStyle);
        }

        private PlayerInsectData GetPid()
        {
            if (collection == null || string.IsNullOrEmpty(selectedInstanceId)) return null;
            return collection.GetByInstanceId(selectedInstanceId);
        }

        private static bool IsEquipped(PlayerInsectData pid, string skillId)
        {
            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
                if (pid.GetEquippedSkill(i) == skillId) return true;
            return false;
        }

        public void AutoWire(TrainingManager tm, PlayerInsectCollection col, PlayerCandyInventory candy)
        {
            if (trainingManager == null) trainingManager = tm;
            // collection 변경 시 InsectUpdated 구독 동기화 (OnEnable 이후 호출 케이스 대응).
            if (collection != col)
            {
                if (collection != null)
                    collection.InsectUpdated -= HandleInsectUpdated;
                collection = col;
                if (collection != null && isActiveAndEnabled)
                {
                    collection.InsectUpdated -= HandleInsectUpdated;
                    collection.InsectUpdated += HandleInsectUpdated;
                }
                ownedCacheDirty = true;
            }
            if (candyInventory == null) candyInventory = candy;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// QA 캡처(<c>FieldHudVisualCapture</c>)가 특정 페이지를 바로 띄우는 진입점. 게임 코드는 부르지 않는다 —
        /// 플레이어 흐름은 항상 곤충 선택에서 시작한다.
        /// </summary>
        internal void OpenForCapture(string instanceId, string pageName, int methodIndex = -1, string pendingSkillId = null)
        {
            if (!isOpen) Toggle();
            selectedInstanceId = instanceId;
            selectedMethodIndex = methodIndex;
            pendingNewSkillId = pendingSkillId;
            switch (pageName)
            {
                case "method": ChangePage(Page.MethodSelect); break;
                case "growth": ChangePage(Page.Growth); break;
                case "learn": ChangePage(Page.SkillLearn); break;
                case "equip": ChangePage(Page.SkillEquip); break;
                case "replace": ChangePage(Page.SkillReplace); break;
                default: ChangePage(Page.InsectSelect); break;
            }
        }

        internal void ShowFeedbackForCapture(string message, bool big) => ShowFeedback(message, big);
#endif
    }
}
