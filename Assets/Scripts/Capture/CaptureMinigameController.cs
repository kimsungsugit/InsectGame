using InsectGame.Data;
using InsectGame.Spawning;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Capture
{
    /// <summary>
    /// 포획 미니게임의 껍데기 — 게임을 고르고, 조작을 모아 넘기고, 화면을 그리고, 끝나면 포획을 판정한다.
    /// 규칙은 <see cref="CaptureMinigame"/>(순수)에 있다.
    ///
    /// 예전엔 좌우로 오가는 타이밍 바 하나였다. 곤충이 화면에 나오지 않았고 어떤 종을 잡든 판이 같았다.
    /// 지금은 살금살금·가두기·던지기 셋 중 하나가 포획마다 무작위로 걸리고(2026-10-02 결정), 잡으려는
    /// 곤충이 놀이판에 직접 나온다. 결과는 셋 다 0~3점이라 <see cref="CaptureMinigameProbability"/>와
    /// 채집망 보정은 그대로 이어진다.
    /// </summary>
    public class CaptureMinigameController : MonoBehaviour
    {
        [SerializeField] private CaptureController captureController;
        // 옛 uGUI 포획 패널. 부트스트랩이 리플렉션으로 꽂아 주는데 아무도 켜지 않는다 — 끝날 때 숨기기만 한다.
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private InsectGame.Core.PlayerMovement playerMovement;

        // 판이 열리고 조작을 받기까지. 채집망을 고른 그 누름이 첫 조작으로 새지 않게 하고, 무슨 게임인지 읽을 틈을 준다.
        private const float IntroSeconds = 0.9f;
        private const float ResultSeconds = 1.5f;
        private const float HeaderHeight = 148f;
        private const float BoardPad = 24f;
        // 가두기에서 그물을 손가락 위로 띄우는 높이(가상 px). 손가락이 곤충을 가리지 않게 한다.
        private const float TouchNetLift = 96f;

        private readonly System.Random random = new System.Random();

        private InsectEntity currentTarget;
        private CaptureMinigame game;
        private CaptureMinigameKind? lastKind;
        private bool isActive;
        public bool IsActive => isActive;
        private float introLeft;
        private float animTime;
        private float itemCaptureBonus;
        private string titleText = string.Empty;
        private bool wantCancel;
        // 옛 uGUI 포획 버튼(ConfirmCapture)의 누름 — 다음 Update가 한 번 소비한다.
        private bool legacyPress;

        private float resultTimer;
        private string resultMessage;
        private bool resultSuccess;

        // 이번 프레임의 배치. Update(조작 좌표)와 OnGUI(그리기)가 같은 값을 쓴다.
        private Rect panelRect;
        private Rect boardRect;
        private float boardScale = 1f;
        private Rect cancelButtonRect;

        private GUIStyle titleStyle, starStyle, hintStyle, markStyle, boardTextStyle, introStyle, cancelStyle, resultStyle;
        private bool stylesReady;

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            UITheme t = UITheme.Instance;

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            starStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleCenter };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            hintStyle.normal.textColor = t.textSecondary;
            markStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            boardTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            introStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            introStyle.normal.textColor = t.textPrimary;
            cancelStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            cancelStyle.normal.textColor = t.textPrimary;
            resultStyle = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        public void StartMinigame(InsectEntity target)
        {
            StartMinigame(target, 1f, 1f, 1f, 0f);
        }

        public void StartMinigame(InsectEntity target, float speedMult, float zoneMult, float timeMult, float captureBonus)
        {
            StartMinigame(target, speedMult, zoneMult, timeMult, captureBonus, CaptureMinigame.PickNext(lastKind, random));
        }

        private void StartMinigame(InsectEntity target, float speedMult, float zoneMult, float timeMult,
            float captureBonus, CaptureMinigameKind kind)
        {
            // 수문장 포획 금지의 단일 출처. CaptureChoiceUI의 버튼/키 분기에만 있었는데 근접·레이캐스트·
            // 입력 컨트롤러 세 경로는 여기로 바로 들어온다 — 수문장을 잡아 버리면 표식 개체가
            // 사라져 격파 판정이 영영 서지 못한다.
            if (target != null && target.IsGuardian)
            {
                Debug.Log("[Capture] 수문장은 포획할 수 없다 — 배틀로만 격파한다");
                return;
            }
            currentTarget = target;
            if (target != null) target.SetEngaged(true); // 미니게임 중 — 곤충 도주 방지
            isActive = true;
            if (playerMovement != null) playerMovement.SetFrozen(true);
            resultTimer = 0f;
            resultMessage = null;
            itemCaptureBonus = captureBonus;
            introLeft = IntroSeconds;
            animTime = 0f;
            wantCancel = false;
            legacyPress = false;

            // 지워진 개체는 포획 전까지 본명을 감춘다(`CaptureChoiceUI`와 같은 이유·같은 출처).
            // 제목은 판 내내 같으므로 여기서 한 번만 만든다(OnGUI 패스마다 이어 붙이지 않는다).
            string targetName = target != null ? target.DisplayNameForPlayer : "???";
            titleText = target != null && target.Data != null
                ? $"{targetName}  ·  {target.Data.rarity.Korean()}" : targetName;

            int rarity = target != null && target.Data != null ? (int)target.Data.rarity : 0;
            lastKind = kind;
            game = CaptureMinigame.Create(kind, new CaptureMinigameTuning(rarity, speedMult, zoneMult, timeMult), random);
        }

        private void Update()
        {
            if (resultTimer > 0f)
            {
                resultTimer -= Time.deltaTime;
                if (resultTimer <= 0f) resultMessage = null;
            }

            if (!isActive || game == null) return;

            if (wantCancel) { wantCancel = false; CancelCapture(); return; }
            // 대상이 사라졌으면(풀 회수·씬 정리) 판을 붙들고 있지 않는다 — 조작이 얼어붙은 채 남는다.
            if (currentTarget == null) { StopMinigame(); return; }

            float dt = Time.deltaTime;
            animTime += dt;

            if (introLeft > 0f)
            {
                introLeft -= dt;
                legacyPress = false;
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugFrozen) return;
#endif

            game.Tick(dt, ReadInput());
            if (game.Done) FinishCapture();
        }

        // 조작은 여기 한 곳에서만 읽는다. 예전 타이밍 바는 Update 폴링과 OnGUI 이벤트가 같은 누름을
        // 따로 세어 한 번 누르면 두 번 확정됐다(3단계 콤보가 영영 불가능했다) — 출처를 둘로 늘리지 말 것.
        private CaptureMinigameInput ReadInput()
        {
            Layout();
            Vector2 pointer = UIScale.VirtualMousePosition;
            bool overCancel = cancelButtonRect.Contains(pointer);
            bool pointerDown = Input.GetMouseButton(0) && !overCancel;
            bool pointerPressed = Input.GetMouseButtonDown(0) && !overCancel;
            bool keyDown = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E);
            bool keyPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)
                || Input.GetKeyDown(KeyCode.Return) || legacyPress;
            legacyPress = false;

            // 가두기는 손가락이 곤충을 가린다 — 터치일 때만 그물을 손가락 위로 띄운다.
            if (game.Kind == CaptureMinigameKind.Track && Input.touchCount > 0) pointer.y -= TouchNetLift;

            var input = new CaptureMinigameInput
            {
                Down = pointerDown || keyDown,
                Pressed = pointerPressed || keyPressed,
                Point = new Vector2((pointer.x - boardRect.x) / boardScale, (pointer.y - boardRect.y) / boardScale)
            };
            // 던지기는 놀이판 안을 눌렀을 때만 던진다 — 판 밖(제목·힌트)을 눌러 그물을 버리지 않게.
            if (game.Kind == CaptureMinigameKind.Toss && !boardRect.Contains(pointer)) input.Pressed = false;
            return input;
        }

        /// <summary>옛 uGUI 포획 버튼의 진입점 — 지금은 "한 번 누름"으로 넘긴다.</summary>
        public void ConfirmCapture()
        {
            if (isActive) legacyPress = true;
        }

        private void FinishCapture()
        {
            int hits = game != null ? game.Hits : 0;
            float timing01 = CaptureMinigameProbability.GetTiming01(hits);
            float extraBonus = CaptureMinigameProbability.GetExtraBonus(hits, itemCaptureBonus);

            if (captureController != null && currentTarget != null)
            {
                captureController.AttemptCapture(currentTarget, timing01, extraBonus);

                if (hits >= 3) ShowResult("PERFECT!", true);
                else if (hits >= 2) ShowResult("GREAT!", true);
                else if (hits >= 1) ShowResult("GOOD", false);
                else ShowResult("MISS...", false);
            }

            StopMinigame();
        }

        private void ShowResult(string msg, bool success)
        {
            resultMessage = msg;
            resultSuccess = success;
            resultTimer = ResultSeconds;
        }

        public void CancelCapture()
        {
            StopMinigame();
        }

        private void StopMinigame()
        {
            isActive = false;
            game = null;
            if (currentTarget != null) currentTarget.SetEngaged(false); // 미니게임 종료 — 도주 가능 상태 복귀
            currentTarget = null;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (playerMovement != null) playerMovement.SetFrozen(false);
        }

        private void OnDisable()
        {
            // 외부에서 컴포넌트 disable되어도 isActive/playerMovement.frozen이 잔존하지 않도록 보장.
            // 옛은 OnDisable 없음 → 씬 전환 시 player가 영구 멈춤 + 다음 활성화 시 OnGUI 미니게임 잔존.
            if (isActive) StopMinigame();
        }

        private void OnGUI()
        {
            if (isActive)
            {
                Event evt = Event.current;
                if (evt != null && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    wantCancel = true;
                    evt.Use();
                }
            }

            if (resultTimer <= 0f && !isActive) return;
            InitStyles();
            UIScale.Begin();
            if (resultTimer > 0f && resultMessage != null) DrawResult();
            if (isActive && game != null) DrawMinigame();
            GUI.color = Color.white;
            UIScale.End();
        }

        // ── 배치 ──

        private void Layout()
        {
            bool mobile = UIScale.IsMobileLayout;
            float footer = mobile ? 132f : 116f;
            float wantW = mobile ? Mathf.Min(900f, UIScale.ContentWidth(28f)) : 760f;
            float wantBoardH = (wantW - BoardPad * 2f) * (CaptureMinigame.BoardHeight / CaptureMinigame.BoardWidth);
            // 높이는 안전 영역 안으로 줄어들 수 있다 — 줄면 놀이판을 같은 비율로 줄인다.
            panelRect = UISafeLayout.CenteredPanel(wantW, HeaderHeight + wantBoardH + footer);
            float availH = Mathf.Max(1f, panelRect.height - HeaderHeight - footer);
            boardScale = Mathf.Max(0.1f, Mathf.Min(
                (panelRect.width - BoardPad * 2f) / CaptureMinigame.BoardWidth, availH / CaptureMinigame.BoardHeight));
            float bw = CaptureMinigame.BoardWidth * boardScale;
            float bh = CaptureMinigame.BoardHeight * boardScale;
            boardRect = new Rect(panelRect.x + (panelRect.width - bw) * 0.5f, panelRect.y + HeaderHeight, bw, bh);

            float cancelW = mobile ? 220f : 200f;
            float cancelH = mobile ? 64f : 56f;
            cancelButtonRect = new Rect(panelRect.x + (panelRect.width - cancelW) * 0.5f,
                panelRect.yMax - cancelH - 16f, cancelW, cancelH);
        }

        private Vector2 OnBoard(Vector2 p) => new Vector2(boardRect.x + p.x * boardScale, boardRect.y + p.y * boardScale);
        private Vector2 OnBoard(float x, float y) => new Vector2(boardRect.x + x * boardScale, boardRect.y + y * boardScale);
        private Rect OnBoardRect(float cx, float cy, float w, float h)
            => new Rect(boardRect.x + (cx - w * 0.5f) * boardScale, boardRect.y + (cy - h * 0.5f) * boardScale,
                w * boardScale, h * boardScale);

        // ── 그리기 ──

        private void DrawMinigame()
        {
            Layout();
            UITheme t = UITheme.Instance;
            InsectData data = currentTarget != null ? currentTarget.Data : null;
            Color rarityCol = data != null ? t.GetInsectRarityColor(data.rarity) : t.textPrimary;

            UISurface.Dim(0.35f);
            UISurface.Card(panelRect, t.surfaceCard, Color.Lerp(t.surfaceBorder, rarityCol, 0.55f));
            UISurface.Flat(new Rect(panelRect.x + UITheme.Radius.Card, panelRect.y + 3f,
                panelRect.width - UITheme.Radius.Card * 2f, 4f), rarityCol);

            titleStyle.normal.textColor = rarityCol;
            UIHelper.LabelFit(new Rect(panelRect.x + 24f, panelRect.y + 14f, panelRect.width - 48f, 46f),
                titleText, titleStyle);

            // 별 세 개 — 지금까지 얻은 점수.
            float starX = panelRect.center.x - 78f;
            for (int i = 0; i < CaptureMinigame.MaxHits; i++)
            {
                starStyle.normal.textColor = i < game.Hits ? t.accentAmber : t.surfaceBorder;
                GUI.Label(new Rect(starX + i * 52f, panelRect.y + 62f, 52f, 52f), "★", starStyle);
            }
            UISurface.Chip(new Rect(panelRect.x + 24f, panelRect.y + 70f, 148f, 36f), KindName(game.Kind),
                t.surfaceRaised, t.textPrimary);

            float ratio = game.TimeRatio;
            UISurface.Meter(new Rect(boardRect.x, panelRect.y + 124f, boardRect.width, 10f), ratio,
                ratio > 0.4f ? t.accentMint : (ratio > 0.2f ? t.accentAmber : t.accentCoral));

            Color boardBg = Color.Lerp(t.surfaceBase, t.accentMint, 0.14f);
            UISurface.Rounded(boardRect, boardBg);

            switch (game.Kind)
            {
                case CaptureMinigameKind.Track: DrawTrack((TrackMinigame)game, data, boardBg); break;
                case CaptureMinigameKind.Toss: DrawToss((TossMinigame)game, data, boardBg); break;
                default: DrawSneak((SneakMinigame)game, data, boardBg); break;
            }

            if (introLeft > 0f) DrawIntro();

            UIHelper.LabelFit(new Rect(panelRect.x + 24f, boardRect.yMax + 8f, panelRect.width - 48f, 40f),
                HintFor(game), hintStyle);

            bool mobile = UIScale.IsMobileLayout;
            if (UISurface.Button(cancelButtonRect, mobile ? "취소" : "취소  [ESC]", t.surfaceRaised, cancelStyle))
                wantCancel = true;
        }

        private static string KindName(CaptureMinigameKind kind)
        {
            switch (kind)
            {
                case CaptureMinigameKind.Track: return "가두기";
                case CaptureMinigameKind.Toss: return "던지기";
                default: return "살금살금";
            }
        }

        private static string HintFor(CaptureMinigame g)
        {
            bool mobile = UIScale.IsMobileLayout;
            switch (g.Kind)
            {
                case CaptureMinigameKind.Track:
                    return "누른 채 끌어서 곤충을 그물 안에 가두세요";
                case CaptureMinigameKind.Toss:
                    return "곤충이 갈 곳을 눌러 그물을 던지세요";
                default:
                    return mobile ? "누르고 있으면 다가갑니다 · 돌아보면 손을 떼세요"
                        : "[Space]나 클릭을 누르고 있으면 다가갑니다 · 돌아보면 떼세요";
            }
        }

        private void DrawIntro()
        {
            UITheme t = UITheme.Instance;
            float a = Mathf.Clamp01(introLeft / 0.25f);
            float w = Mathf.Min(420f, boardRect.width - 40f);
            Rect pill = new Rect(boardRect.center.x - w * 0.5f, boardRect.center.y - 44f, w, 88f);
            GUI.color = new Color(1f, 1f, 1f, a);
            UISurface.Rounded(pill, new Color(t.surfaceBase.r, t.surfaceBase.g, t.surfaceBase.b, 0.9f));
            GUI.color = Color.white;
            introStyle.normal.textColor = new Color(t.textPrimary.r, t.textPrimary.g, t.textPrimary.b, a);
            UIHelper.LabelFit(new Rect(pill.x + 16f, pill.y + 12f, pill.width - 32f, 64f), KindName(game.Kind), introStyle);
        }

        // 두 점을 잇는 막대. UIShapes.Capsule은 GUIUtility.RotateAroundPivot을 쓰는데 그 피벗은 화면 좌표라
        // UIScale 배율이 1이 아니면 엉뚱한 점을 축으로 돈다 — 지도 쐐기와 같은 행렬(부모 뒤에 곱한다)을 쓴다.
        private static void DrawBar(Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) return;
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = MapMarkerProjection.PivotMatrix(saved, (from + to) * 0.5f, UIShapes.AngleDegrees(delta));
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(-length * 0.5f, -thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.matrix = saved;
        }

        // 테두리 있는 원. IMGUI에는 고리 도형이 없어 큰 원 위에 속을 덮어 만든다 — 속은 불투명이어야 한다.
        private static void DrawRing(Vector2 center, float radius, float thickness, Color edge, Color fill)
        {
            UIShapes.Ellipse(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), edge);
            float inner = Mathf.Max(0f, radius - thickness);
            UIShapes.Ellipse(new Rect(center.x - inner, center.y - inner, inner * 2f, inner * 2f), fill);
        }

        private void DrawInsect(Vector2 center, float size, InsectData data, float alpha, bool mirrored = false)
        {
            if (data == null)
            {
                UIShapes.Ellipse(new Rect(center.x - size * 0.3f, center.y - size * 0.3f, size * 0.6f, size * 0.6f),
                    UITheme.Instance.accentAmber);
                return;
            }
            bool shiny = currentTarget != null && currentTarget.IsShiny;
            if (!mirrored)
            {
                InsectVisual.Draw(center.x, center.y, size, data, shiny, alpha);
                return;
            }
            // 돌아볼 때 좌우를 뒤집는다 — 등을 보이던 곤충이 이쪽을 향한다.
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = saved * Matrix4x4.TRS(new Vector3(center.x, center.y, 0f), Quaternion.identity, new Vector3(-1f, 1f, 1f));
            InsectVisual.Draw(0f, 0f, size, data, shiny, alpha);
            GUI.matrix = saved;
        }

        private void BoardText(float y, string text, Color color)
        {
            boardTextStyle.normal.textColor = color;
            UIHelper.LabelFit(new Rect(boardRect.x + 12f, boardRect.y + y * boardScale, boardRect.width - 24f, 44f),
                text, boardTextStyle);
        }

        // ── 살금살금 ──

        private const float SneakInsectX = 292f;
        private const float SneakInsectY = 138f;
        private const float SneakTrackY = 226f;

        private static float SneakNetX(float distance) => Mathf.Lerp(250f, 44f, distance);

        private void DrawSneak(SneakMinigame g, InsectData data, Color boardBg)
        {
            UITheme t = UITheme.Instance;
            float s = boardScale;
            bool looking = g.State == SneakMinigame.Watch.Look;
            bool spotted = g.FreezeLeft > 0f;

            // 땅과 다가가는 길
            UISurface.Flat(new Rect(boardRect.x + 3f, OnBoard(0f, 206f).y, boardRect.width - 6f, 3f),
                Color.Lerp(boardBg, t.textMuted, 0.5f));
            Vector2 pathFrom = OnBoard(SneakNetX(1f), SneakTrackY);
            Vector2 pathTo = OnBoard(SneakNetX(0f), SneakTrackY);
            UISurface.Flat(new Rect(pathFrom.x, pathFrom.y - 2f, pathTo.x - pathFrom.x, 4f), t.surfaceBorder);
            DrawFlag(SneakMinigame.FirstFlag, g.Distance, boardBg);
            DrawFlag(SneakMinigame.SecondFlag, g.Distance, boardBg);
            DrawFlag(0f, g.Distance, boardBg);

            // 돌아보는 동안 시선이 닿는 자리 — 여기서 움직이면 들킨다.
            if (looking && !g.Fled && g.Swoop01 < 0f)
            {
                Rect gaze = new Rect(boardRect.x + 10f, OnBoard(0f, 96f).y, OnBoard(SneakInsectX, 0f).x - boardRect.x - 10f, 104f * s);
                UISurface.Rounded(gaze, new Color(t.accentCoral.r, t.accentCoral.g, t.accentCoral.b, spotted ? 0.34f : 0.22f));
            }

            // 곤충
            float flee = g.Fled ? g.Flee01 : 0f;
            float twitch = g.State == SneakMinigame.Watch.Twitch ? Mathf.Sin(animTime * 42f) * 3f : 0f;
            Vector2 insectAt = OnBoard(SneakInsectX + twitch + flee * 90f, SneakInsectY - flee * 130f);
            DrawInsect(insectAt, 128f * s, data, 1f - flee, looking || g.Fled);
            if (!g.Fled)
            {
                Rect mark = new Rect(insectAt.x - 40f, insectAt.y - 70f * s - 60f, 80f, 60f);
                if (spotted) { markStyle.normal.textColor = t.accentCoral; GUI.Label(mark, "!", markStyle); }
                else if (g.State == SneakMinigame.Watch.Twitch) { markStyle.normal.textColor = t.accentAmber; GUI.Label(mark, "?", markStyle); }
            }

            // 채집망을 든 손 — 덮칠 때는 곤충에게 내리꽂는다.
            float swoop = Mathf.Max(0f, g.Swoop01);
            Vector2 net = OnBoard(Mathf.Lerp(SneakNetX(g.Distance), SneakInsectX - 6f, swoop),
                Mathf.Lerp(170f, SneakInsectY, swoop));
            DrawBar(net + new Vector2(-46f, 60f) * s, net, 6f * s, Color.Lerp(t.surfaceBase, t.accentAmber, 0.55f));
            DrawRing(net + new Vector2(6f, -12f) * s, 24f * s, 4f * s, t.textPrimary, Color.Lerp(boardBg, t.textPrimary, 0.16f));

            // 기척 — 두 번 들키면 달아난다.
            for (int i = 0; i < SneakMinigame.MaxSpots; i++)
            {
                Vector2 dot = OnBoard(20f + i * 18f, 258f);
                UIShapes.Ellipse(new Rect(dot.x - 6f * s, dot.y - 6f * s, 12f * s, 12f * s),
                    i < SneakMinigame.MaxSpots - g.Spots ? t.accentMint : t.surfaceBorder);
            }

            if (introLeft > 0f) return;
            if (g.Fled) BoardText(24f, "달아났다!", t.accentCoral);
            else if (spotted) BoardText(24f, "들켰다!", t.accentCoral);
            else if (g.Swoop01 >= 0f) BoardText(24f, "덮쳤다!", t.accentMint);
            else if (looking) BoardText(24f, "멈춰!", t.accentCoral);
            else if (g.State == SneakMinigame.Watch.Twitch) BoardText(24f, "돌아보려 한다", t.accentAmber);
            else BoardText(24f, "지금이야", t.accentMint);
        }

        private void DrawFlag(float flagDistance, float distance, Color boardBg)
        {
            UITheme t = UITheme.Instance;
            Vector2 at = OnBoard(SneakNetX(flagDistance), SneakTrackY);
            float r = 8f * boardScale;
            DrawRing(at, r, 3f * boardScale, distance <= flagDistance ? t.accentMint : t.textMuted, boardBg);
        }

        // ── 가두기 ──

        private void DrawTrack(TrackMinigame g, InsectData data, Color boardBg)
        {
            UITheme t = UITheme.Instance;
            float s = boardScale;

            Color edge = g.Inside ? t.accentMint : t.textPrimary;
            DrawRing(OnBoard(g.Net), g.NetRadius * s, 4f * s, edge, Color.Lerp(boardBg, edge, g.Inside ? 0.28f : 0.12f));

            Vector2 flutter = new Vector2(Mathf.Sin(animTime * 9f), Mathf.Cos(animTime * 7f)) * 3f;
            DrawInsect(OnBoard(g.Insect + flutter), 66f * s, data, 1f);

            // 게이지 — 3분의 1마다 별 하나.
            Rect gauge = new Rect(boardRect.x + 24f, boardRect.y + 16f, boardRect.width - 48f, 14f);
            UISurface.Meter(gauge, g.Gauge, t.accentMint);
            UISurface.Flat(new Rect(gauge.x + gauge.width / 3f - 1.5f, gauge.y, 3f, gauge.height), boardBg);
            UISurface.Flat(new Rect(gauge.x + gauge.width * 2f / 3f - 1.5f, gauge.y, 3f, gauge.height), boardBg);
        }

        // ── 던지기 ──

        private void DrawToss(TossMinigame g, InsectData data, Color boardBg)
        {
            UITheme t = UITheme.Instance;
            float s = boardScale;

            if (g.LandShowLeft > 0f)
            {
                Color land = g.LastHit ? t.accentMint : t.accentCoral;
                DrawRing(OnBoard(g.LastLanding), g.NetRadius * s, 4f * s, land, Color.Lerp(boardBg, land, 0.3f));
            }

            Vector2 launcher = OnBoard(TossMinigame.Launcher);
            if (g.InFlight)
            {
                // 떨어질 자리를 먼저 보여 준다 — 곤충이 그 안으로 들어올지를 보는 0.45초가 이 게임의 긴장이다.
                DrawRing(OnBoard(g.Target), g.NetRadius * s, 3f * s, t.textMuted, boardBg);
                float k = g.Flight01;
                Vector2 at = Vector2.Lerp(launcher, OnBoard(g.Target), k) + new Vector2(0f, -Mathf.Sin(k * Mathf.PI) * 40f * s);
                DrawRing(at, Mathf.Lerp(10f, g.NetRadius, k) * s, 3f * s, t.textPrimary, Color.Lerp(boardBg, t.textPrimary, 0.2f));
            }

            DrawInsect(OnBoard(g.Insect), 66f * s, data, 1f);

            // 남은 그물
            for (int i = 0; i < TossMinigame.Throws; i++)
            {
                Vector2 at = launcher + new Vector2((i - 1) * 34f * s, 0f);
                DrawRing(at, 11f * s, 3f * s, i < g.ThrowsLeft ? t.textPrimary : t.surfaceBorder, boardBg);
            }

            if (introLeft > 0f || g.LandShowLeft <= 0f) return;
            BoardText(14f, g.LastHit ? "잡았다!" : "빗나갔다", g.LastHit ? t.accentMint : t.accentCoral);
        }

        private void DrawResult()
        {
            UITheme t = UITheme.Instance;
            float alpha = Mathf.Clamp01(resultTimer / 0.3f);
            float cx = UIScale.VirtualScreenWidth / 2f;
            float baseY = UIScale.VirtualScreenHeight * 0.18f;

            float progress = 1f - (resultTimer / ResultSeconds);
            float bounce = 1f + Mathf.Sin(progress * Mathf.PI) * 0.12f;
            Color col = resultSuccess ? t.accentMint : t.accentCoral;

            if (resultSuccess)
            {
                float glow = (140f + progress * 60f) * bounce;
                UIShapes.Ellipse(new Rect(cx - glow, baseY + 36f - glow * 0.5f, glow * 2f, glow), new Color(col.r, col.g, col.b, 0.1f * alpha));
            }

            resultStyle.normal.textColor = new Color(col.r, col.g, col.b, alpha);
            UIHelper.LabelFit(new Rect(cx - 220f, baseY, 440f, 72f), resultMessage, resultStyle);
        }

        public void AutoWire(CaptureController controller)
        {
            if (captureController == null)
                captureController = controller;
        }

        public void AutoWire(InsectGame.Core.PlayerMovement pm)
        {
            if (playerMovement == null) playerMovement = pm;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ── 검수 빌드 전용 ── 실제 IMGUI를 찍으려면 판을 원하는 순간에 세워 둘 수 있어야 한다.
        private bool debugFrozen;

        /// <summary>
        /// 지정한 게임을 열고 <paramref name="seconds"/>만큼 가짜 조작으로 돌린 뒤 멈춰 둔다.
        /// <paramref name="untilLook"/>이면 살금살금에서 곤충이 돌아보는 순간까지만 돌린다.
        /// </summary>
        public void StartForCapture(InsectEntity target, CaptureMinigameKind kind, float seconds, bool hold,
            Vector2 boardPoint, bool untilLook = false)
        {
            StartMinigame(target, 1f, 1f, 1f, 0f, kind);
            if (game == null) return;
            introLeft = 0f;
            const float step = 1f / 60f;
            game.Tick(step, new CaptureMinigameInput());   // 손을 뗀 상태에서 시작(살금살금은 한 번 떼야 움직인다)
            bool pressed = true;
            for (float elapsed = 0f; elapsed < seconds && !game.Done; elapsed += step)
            {
                game.Tick(step, new CaptureMinigameInput { Down = hold, Pressed = pressed && hold, Point = boardPoint });
                pressed = false;
                if (untilLook && game is SneakMinigame sneak && sneak.State == SneakMinigame.Watch.Look) break;
            }
            debugFrozen = true;
        }

        public void EndCapture()
        {
            debugFrozen = false;
            StopMinigame();
        }
#endif
    }
}
