using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    public class LoginUI : MonoBehaviour
    {
        private enum LoginPhase { Login, Register, Loading, CharacterCreate, Done }

        /// <summary>
        /// 캐릭터 생성의 단계. <see cref="LoginPhase.CharacterCreate"/> 안에서만 의미가 있다.
        ///
        /// 왜 한 화면에 다 안 넣나: 프리셋 카드 + 3D 프리뷰 + 세부 항목을 세로로 쌓으면
        /// 약 1,490px이 되는데 패널 상한이 1,313px이다. 짧은 화면에서는 더 줄어든다.
        /// 스크롤은 3D 프리뷰가 화면 밖으로 나가 존재 이유가 사라지고, 탭은 "다 골랐나"를
        /// 사용자가 추적해야 한다. 프리셋 → 세부는 본래 순차적이라 단계 분할이 맞다.
        /// </summary>
        internal enum CreateStep { Preset, Customize, Starter }

        private LoginPhase phase = LoginPhase.Login;
        private string emailInput = "";
        private string passwordInput = "";
        private string confirmPasswordInput = "";
        private string nicknameInput = "";
        private string errorMessage = "";
        private float errorTimer;
        private bool isProcessing;

        /// <summary>
        /// 의상 프리셋 라디오 라벨 = 프리셋 이름. <see cref="CharacterPresetLibrary.DisplayNames"/>가
        /// 배열을 새로 만들므로 <b>OnGUI에서 직접 부르지 않는다</b> — 여기서 1회만 굽는다.
        /// </summary>
        private static string[] outfitLabelsCache;

        internal static string[] OutfitLabels =>
            outfitLabelsCache ?? (outfitLabelsCache = CharacterPresetLibrary.DisplayNames());

        // 캐릭터 생성
        private CreateStep createStep = CreateStep.Preset;
        private int selectedStarter;
        private string characterName = "탐험가";
        private int selectedSkinColor;  // 0~3
        private int selectedHairStyle;  // 0~3
        private int selectedOutfit;     // 0~2
        private int selectedGender;     // 0=남, 1=여
        private int selectedHairColor;  // 0~5
        private int selectedFaceType;   // 0~3

        // PlayerPrefs 키
        private static string CharNameKey => InsectGame.Core.SaveScope.PrefsKey("InsectGame.Character.Name");
        private static string CharSkinKey => InsectGame.Core.SaveScope.PrefsKey("InsectGame.Character.SkinColor");
        private static string CharHairKey => InsectGame.Core.SaveScope.PrefsKey("InsectGame.Character.HairStyle");
        private static string CharOutfitKey => InsectGame.Core.SaveScope.PrefsKey("InsectGame.Character.OutfitPreset");
        private static string CharCreatedKey => InsectGame.Core.SaveScope.PrefsKey("InsectGame.Character.Created");
        private static string CharGenderKey => InsectGame.Core.SaveScope.PrefsKey("InsectGame.Character.Gender");
        private static string CharHairColorKey => InsectGame.Core.SaveScope.PrefsKey("InsectGame.Character.HairColor");
        private static string CharFaceTypeKey => InsectGame.Core.SaveScope.PrefsKey("InsectGame.Character.FaceType");

        // 스타일 캐시
        // ── 마스터 "특권 없이 (처음부터)" 체크박스 ──
        // 화면에 그리는 값은 여기 들고, 저장은 AuthManager.MasterPlainMode가 한다.
        // 매 프레임 PlayerPrefs를 읽으면 방금 누른 값이 곧바로 덮인다 — 1회만 읽는다.
        private bool masterPlainMode;
        private bool masterPlainLoaded;

        /// <summary>
        /// 3D 마네킹 프리뷰. <b>없어도 동작한다</b> — null이면 2D 초상화로 물러난다.
        /// 그래서 배선이 실패해도 회귀가 아니라 옛 모습으로 돌아갈 뿐이다.
        /// </summary>
        private InsectGame.Core.CharacterModelPreviewRenderer modelPreview;

        /// <summary>프리뷰에 넘기는 조합. 매 프레임 새로 만들지 않으려고 재사용한다.</summary>
        private readonly InsectGame.Core.OutfitLoadout previewLoadout = new InsectGame.Core.OutfitLoadout();

        private float previewYaw = InsectGame.Core.CharacterModelPreviewRenderer.FrontYaw;
        private bool draggingPreview;

        private GUIStyle panelStyle;
        private GUIStyle panelShadowStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle taglineStyle;
        private GUIStyle fieldStyle;
        private GUIStyle labelStyle;
        private GUIStyle errorStyle;
        private GUIStyle btnGreenStyle;
        private GUIStyle btnBlueStyle;
        private GUIStyle btnYellowStyle;
        private GUIStyle btnGrayStyle;
        private GUIStyle separatorStyle;
        private GUIStyle radioStyle;
        private GUIStyle radioSelectedStyle;
        private GUIStyle sectionLabelStyle;

        /// <summary>클라우드 로드 결과를 기다리는 중인가 — OnEnable 재구독의 조건.</summary>
        private bool waitingForCloudLoad;
        private GUIStyle brandEyebrowStyle;
        private GUIStyle helperStyle;
        private GUIStyle linkStyle;
        private GUIStyle versionStyle;
        private GUIStyle logoFrameStyle;
        private bool stylesInitialized;
        private Texture2D appIconTexture;
        private Texture2D backgroundTexture;
        private Texture2D backgroundGlowTexture;
        private Texture2D panelHeaderTexture;
        private Texture2D panelAccentTexture;
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly Texture2D[] loadingDotTextures = new Texture2D[6];

        private const string AppIconResourcePath = "UI/insect-game-icon";
        private static readonly Vector2[] AmbientLightPositions =
        {
            new Vector2(0.08f, 0.18f),
            new Vector2(0.18f, 0.72f),
            new Vector2(0.83f, 0.16f),
            new Vector2(0.92f, 0.58f),
            new Vector2(0.72f, 0.86f),
            new Vector2(0.35f, 0.08f)
        };

        // 로딩 애니메이션
        private float loadingAngle;

        // ── Lifecycle ──

        private void OnEnable()
        {
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.LoginCompleted += OnLoginCompleted;
                AuthManager.Instance.RegisterCompleted += OnRegisterCompleted;
                AuthManager.Instance.AuthFailed += OnAuthFailed;
            }
            // 로드를 기다리던 중에 꺼졌다 켜졌다면 되살린다(기다리는 중이 아니면 아무것도 안 한다).
            SubscribeCloudLoad();
        }

        private void OnDisable()
        {
            // 생성 화면을 벗어나며 비활성화되는 경우, 프리뷰가 저장 안 된 외형을 계속 들고 있지
            // 않게 한다 — 안 그러면 나중에 의상 화면이 생성 당시 만지던 얼굴을 보여준다.
            ClearPreviewOverride();

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.LoginCompleted -= OnLoginCompleted;
                AuthManager.Instance.RegisterCompleted -= OnRegisterCompleted;
                AuthManager.Instance.AuthFailed -= OnAuthFailed;
            }
            // 유일한 해제가 콜백 안에 있었는데, 세이브 충돌로 로드가 멈추면(CloudSaveManager가
            // SaveConflictUI 해결까지 LoadCompleted를 미룬다) 그 콜백이 영영 안 온다 —
            // 비활성 LoginUI에 핸들러가 남아 재로그인 시 ResetForNewAccount가 두 번 돈다.
            if (CloudSaveManager.Instance != null)
                CloudSaveManager.Instance.LoadCompleted -= OnCloudLoadCompleted;
        }

        /// <summary>
        /// 클라우드 로드 결과 구독. <b>무조건 걸면 안 된다</b> — 로드를 기다리는 동안에만
        /// 유효한 구독이라, 대기 중이 아닐 때 붙여 두면 남의 로드 완료에 반응해
        /// 캐릭터 생성 판정이 엉뚱한 시점에 돈다. 그래서 대기 플래그를 함께 본다.
        /// <c>-=</c> 뒤 <c>+=</c>라 중복 구독은 되지 않는다.
        /// </summary>
        private void SubscribeCloudLoad()
        {
            if (!waitingForCloudLoad || CloudSaveManager.Instance == null) return;
            CloudSaveManager.Instance.LoadCompleted -= OnCloudLoadCompleted;
            CloudSaveManager.Instance.LoadCompleted += OnCloudLoadCompleted;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < generatedTextures.Count; i++)
            {
                if (generatedTextures[i] != null) Destroy(generatedTextures[i]);
            }
            generatedTextures.Clear();
        }

        // 토큰 갱신 실패 등 silent ClearAuth 시 사용자에게 메시지 표시 + 로그인 화면 복귀
        private void OnAuthFailed(string reason)
        {
            errorMessage = reason ?? "인증 실패 — 다시 로그인 해주세요";
            errorTimer = 5f;

            // **회원가입 화면은 건드리지 않는다.** RegisterWithEmail은 클라이언트 검증 실패에도
            // AuthFailed를 먼저 쏘는데, 옛 코드는 그때도 로그인 패널로 되돌려 **닉네임과 비밀번호
            // 확인이 통째로 날아갔다** — 비밀번호가 짧다는 안내를 받으려고 폼을 잃는 셈이었다.
            if (phase != LoginPhase.Register)
            {
                // 생성 화면에서 토큰이 끊기면 이 경로로 나간다 — 프리뷰 override를 여기서 풀지
                // 않으면 세션 내내 남는다. OnDisable에 기대면 안 된다: LoginUI를
                // SetActive(false)하는 코드가 저장소에 없어 그 콜백은 씬 teardown에서만 돌고,
                // 그때는 렌더러도 함께 죽어 의미가 없다.
                // 남으면 이후 의상 화면의 큰 패널과 썸네일 24장이 전부 '버려진 얼굴'로 구워진다
                // (CharacterOutfitUI는 InvalidatePreview만 부르는데 그건 override를 안 건드린다).
                if (phase == LoginPhase.CharacterCreate) ClearPreviewOverride();
                phase = LoginPhase.Login;
            }

            // 토큰 갱신 실패 경로는 AuthFailed만 쏘고 LoginCompleted를 안 준다 —
            // 여기서 안 풀면 모든 버튼이 GUI.enabled=false로 굳는다.
            isProcessing = false;
        }

        private void Update()
        {
            if (errorTimer > 0) errorTimer -= Time.deltaTime;

            // 이미 로그인 상태면 바로 게임
            if (phase == LoginPhase.Login && AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
            {
                CheckCharacterCreation();
            }
        }

        // ── OnGUI ──

        private void OnGUI()
        {
            if (phase == LoginPhase.Done) return;

            InitStyles();

            // 배경 그라데이션 (위: 진한남색, 아래: 짙은녹색)
            DrawBackground();

            // 자동 로그인(캐시 세션 복원) 진행 중이면 로그인 폼 대신 로딩 표시 — 폼 깜빡임 방지.
            // 성공 시 LoginCompleted, 실패 시 AuthFailed가 phase를 전환한다.
            LoginPhase effectivePhase = phase;
            if (phase == LoginPhase.Login && AuthManager.Instance != null
                && (AuthManager.Instance.AutoLoginPending || AuthManager.Instance.IsLoggedIn))
            {
                effectivePhase = LoginPhase.Loading;
            }

            switch (effectivePhase)
            {
                case LoginPhase.Login:
                    DrawLoginPanel();
                    break;
                case LoginPhase.Register:
                    DrawRegisterPanel();
                    break;
                case LoginPhase.Loading:
                    DrawLoadingPanel();
                    break;
                case LoginPhase.CharacterCreate:
                    DrawCharacterCreatePanel();
                    break;
            }

            DrawPrivacyPolicyLink();
        }

        private void DrawPrivacyPolicyLink()
        {
            bool mobile = UIScale.IsMobileLayout;
            float width = Mathf.Min(mobile ? 320f : 260f, Screen.width * (mobile ? 0.5f : 0.34f));
            float height = mobile ? 56f : 44f;
            float x = Screen.width - width - 16f - SafeArea.Right;
            float y = UISafeLayout.Px.BottomY(height);

            linkStyle.fontSize = mobile ? 22 : 20;
            if (!GUI.Button(new Rect(x, y, width, height), "개인정보처리방침", linkStyle)) return;

            if (FirebaseConfig.IsPrivacyPolicyConfigured)
            {
                Application.OpenURL(FirebaseConfig.PrivacyPolicyUrl);
                return;
            }

            errorMessage = "개인정보처리방침 URL이 설정되지 않았습니다.";
            errorTimer = 5f;
        }

        // ── 배경 ──

        private void DrawBackground()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), backgroundTexture,
                ScaleMode.StretchToFill);

            float glowSize = Mathf.Max(Screen.width, Screen.height) * 0.62f;
            GUI.color = new Color(0.55f, 0.95f, 0.65f, 0.34f);
            GUI.DrawTexture(new Rect(-glowSize * 0.35f, Screen.height * 0.35f,
                glowSize, glowSize), backgroundGlowTexture, ScaleMode.StretchToFill, true);
            GUI.color = new Color(0.35f, 0.62f, 1f, 0.24f);
            GUI.DrawTexture(new Rect(Screen.width - glowSize * 0.72f, -glowSize * 0.28f,
                glowSize, glowSize), backgroundGlowTexture, ScaleMode.StretchToFill, true);

            // 천천히 떠다니는 작은 빛으로 정적인 로그인 화면에 깊이감을 줍니다.
            float time = Time.unscaledTime;
            for (int i = 0; i < AmbientLightPositions.Length; i++)
            {
                Vector2 anchor = AmbientLightPositions[i];
                float driftX = Mathf.Sin(time * 0.22f + i * 1.7f) * 14f;
                float driftY = Mathf.Cos(time * 0.18f + i * 1.3f) * 18f;
                float size = 24f + (i % 3) * 12f;
                GUI.color = (i % 2 == 0)
                    ? new Color(0.55f, 1f, 0.58f, 0.22f)
                    : new Color(1f, 0.82f, 0.26f, 0.18f);
                GUI.DrawTexture(new Rect(
                    Screen.width * anchor.x + driftX - size * 0.5f,
                    Screen.height * anchor.y + driftY - size * 0.5f,
                    size, size), backgroundGlowTexture, ScaleMode.StretchToFill, true);
            }
            GUI.color = Color.white;
        }

        private void DrawDecoratedPanel(Rect rect)
        {
            GUI.Box(new Rect(rect.x + 12f, rect.y + 16f, rect.width, rect.height), "", panelShadowStyle);
            GUI.Box(rect, "", panelStyle);

            float inset = Mathf.Min(30f, rect.width * 0.04f);
            float headerH = Mathf.Min(145f, rect.height * 0.22f);
            GUI.DrawTexture(new Rect(rect.x + inset, rect.y + 8f, rect.width - inset * 2f, headerH),
                panelHeaderTexture, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(rect.x + inset, rect.y + 15f, rect.width - inset * 2f, 4f),
                panelAccentTexture, ScaleMode.StretchToFill);

            // 작은 코너 표식으로 탐험 장비 패널 같은 프레임감을 줍니다.
            GUI.DrawTexture(new Rect(rect.x + 18f, rect.y + 18f, 12f, 12f), panelAccentTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 30f, rect.y + 18f, 12f, 12f), panelAccentTexture);
        }

        // 세이프 에어리어 + 세로 마진 안에서 패널을 중앙 배치 — 짧은/노치 화면에서 상단 클립 방지.
        // ph가 안전 높이를 넘으면 줄여서 안에 맞춤. (LoginUI는 픽셀 좌표계라 Px 파사드를 쓴다)
        private static float SafeCenterY(ref float ph)
        {
            ph = UISafeLayout.Px.ClampHeight(ph);
            return UISafeLayout.Px.CenteredY(ph);
        }

        // ── 로그인 패널 ──

        private void DrawLoginPanel()
        {
            float pw = Mathf.Min(1125f, Screen.width * 0.9f);
            float ph = Mathf.Min(1200f, Screen.height * 0.95f);
            float px = (Screen.width - pw) * 0.5f;
            float py = SafeCenterY(ref ph);

            DrawDecoratedPanel(new Rect(px, py, pw, ph));

            float layoutScale = Mathf.Clamp(pw / 1125f, 0.72f, 1f);
            float sidePadding = Mathf.Clamp(pw * 0.078f, 48f, 88f);
            float cx = px + sidePadding;
            float cy = py + 28f;
            float fieldW = pw - sidePadding * 2f;
            float fieldH = Mathf.Lerp(60f, 70f, layoutScale);
            float btnH = Mathf.Lerp(72f, 82f, layoutScale);

            DrawLoginBrandHeader(px, py, pw, sidePadding, layoutScale, ref cy);

            // 이메일
            labelStyle.fontSize = Mathf.RoundToInt(32f * layoutScale);
            GUI.Label(new Rect(cx, cy, 250f, 42f), "이메일", labelStyle);
            cy += 42f;
            fieldStyle.fontSize = Mathf.RoundToInt(33f * layoutScale);
            fieldStyle.fixedHeight = fieldH;
            emailInput = GUI.TextField(new Rect(cx, cy, fieldW, fieldH), emailInput, 128, fieldStyle);
            cy += fieldH + 18f;

            // 비밀번호
            GUI.Label(new Rect(cx, cy, 250f, 42f), "비밀번호", labelStyle);
            cy += 42f;
            passwordInput = GUI.PasswordField(new Rect(cx, cy, fieldW, fieldH), passwordInput, '*', 64, fieldStyle);
            cy += fieldH + 25f;

            // ── 마스터 전용 스위치 ──
            // 마스터 계정 자체가 에디터/개발 빌드에서만 컴파일되므로(MasterAccount.IsEnabled)
            // 이 줄도 그때만 그린다. 프로덕션 빌드에는 존재하지 않는다.
            if (MasterAccount.IsEnabled)
            {
                if (!masterPlainLoaded)
                {
                    masterPlainMode = AuthManager.MasterPlainMode;
                    masterPlainLoaded = true;
                }

                float toggleH = Mathf.Lerp(48f, 56f, layoutScale);
                btnGrayStyle.fontSize = Mathf.RoundToInt(26f * layoutScale);
                if (GUI.Button(new Rect(cx, cy, fieldW, toggleH),
                        (masterPlainMode ? "[V]  " : "[  ]  ") + "마스터 특권 없이 (처음부터)",
                        btnGrayStyle))
                    masterPlainMode = !masterPlainMode;
                cy += toggleH + 4f;

                helperStyle.fontSize = Mathf.RoundToInt(20f * layoutScale);
                GUI.Label(new Rect(cx, cy, fieldW, 30f), MasterPlainHint(), helperStyle);
                cy += 32f;
            }

            // 로그인 버튼
            btnGreenStyle.fontSize = Mathf.RoundToInt(38f * layoutScale);
            GUI.enabled = !isProcessing;
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH), isProcessing ? "로딩 중..." : "로그인", btnGreenStyle))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    // 마스터 자격 증명일 때만 읽힌다. 일반 계정 로그인에는 영향이 없다.
                    AuthManager.Instance.PendingMasterPlainMode = masterPlainMode;
                    AuthManager.Instance.LoginWithEmail(emailInput, passwordInput);
                }
            }
            cy += btnH + 14f;

            // 회원가입 버튼
            btnBlueStyle.fontSize = Mathf.RoundToInt(32f * layoutScale);
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH - 8f), "이메일로 새 계정 만들기", btnBlueStyle))
            {
                if (!isProcessing)
                {
                    errorMessage = "";
                    confirmPasswordInput = "";
                    nicknameInput = "";
                    phase = LoginPhase.Register;
                }
            }
            GUI.enabled = true;
            cy += btnH + 10f;

            // 구분선
            separatorStyle.fontSize = Mathf.RoundToInt(25f * layoutScale);
            GUI.Label(new Rect(px, cy, pw, 38f), "또는 간편하게 계속", separatorStyle);
            cy += 43f;

            // 소셜 로그인은 Google 단일 진입점만 제공합니다.
            float socialBtnH = Mathf.Lerp(62f, 70f, layoutScale);
            GUI.enabled = !isProcessing;
            btnYellowStyle.fontSize = Mathf.RoundToInt(31f * layoutScale);
            if (GUI.Button(new Rect(cx, cy, fieldW, socialBtnH), "G   Google 계정으로 계속하기", btnYellowStyle))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginWithGoogle();
                }
            }
            cy += socialBtnH + 10f;

            btnGrayStyle.fontSize = Mathf.RoundToInt(30f * layoutScale);
            if (GUI.Button(new Rect(cx, cy, fieldW, socialBtnH), "게스트로 둘러보기", btnGrayStyle))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginAsGuest();
                }
            }
            GUI.enabled = true;
            cy += socialBtnH + 5f;

            helperStyle.fontSize = Mathf.RoundToInt(21f * layoutScale);
            GUI.Label(new Rect(cx, cy, fieldW, 32f),
                "게스트 진행 정보는 현재 기기에 먼저 저장됩니다.", helperStyle);
            cy += 34f;

            // 에러 메시지
            if (errorTimer > 0 && !string.IsNullOrEmpty(errorMessage))
            {
                errorStyle.fontSize = 30;
                UIHelper.LabelFit(new Rect(cx, cy, fieldW, 55f), errorMessage, errorStyle);
            }

            versionStyle.fontSize = Mathf.RoundToInt(18f * layoutScale);
            GUI.Label(new Rect(cx, py + ph - 46f, fieldW, 28f),
                "v" + Application.version + "  ·  Firebase 보안 로그인", versionStyle);
        }

        private void DrawLoginBrandHeader(float px, float py, float pw, float sidePadding,
            float layoutScale, ref float cy)
        {
            float iconSize = Mathf.Lerp(112f, 148f, layoutScale);
            float frameInset = 6f;
            float iconX = px + sidePadding;
            float iconY = py + 31f;
            Rect frameRect = new Rect(iconX, iconY, iconSize, iconSize);

            GUI.color = new Color(0.42f, 1f, 0.58f, 0.42f);
            GUI.DrawTexture(new Rect(iconX - 18f, iconY - 18f, iconSize + 36f, iconSize + 36f),
                backgroundGlowTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.Box(frameRect, "", logoFrameStyle);

            Rect iconRect = new Rect(frameRect.x + frameInset, frameRect.y + frameInset,
                frameRect.width - frameInset * 2f, frameRect.height - frameInset * 2f);
            if (appIconTexture != null)
            {
                GUI.DrawTexture(iconRect, appIconTexture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                subtitleStyle.fontSize = Mathf.RoundToInt(26f * layoutScale);
                GUI.Label(iconRect, "INSECT", subtitleStyle);
            }

            float textX = frameRect.xMax + Mathf.Lerp(20f, 30f, layoutScale);
            float textRight = px + pw - sidePadding;
            float textW = Mathf.Max(180f, textRight - textX);

            brandEyebrowStyle.fontSize = Mathf.RoundToInt(18f * layoutScale);
            GUI.Label(new Rect(textX, iconY + 4f, textW, 28f),
                "INSECT EXPLORATION", brandEyebrowStyle);

            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.fontSize = Mathf.RoundToInt(62f * layoutScale);
            GUI.Label(new Rect(textX, iconY + 27f, textW, 72f), "곤충탐험", titleStyle);

            taglineStyle.alignment = TextAnchor.UpperLeft;
            taglineStyle.fontSize = Mathf.RoundToInt(24f * layoutScale);
            taglineStyle.wordWrap = true;
            GUI.Label(new Rect(textX, iconY + 96f, textW, 50f),
                "발견하고, 성장시키고, 함께 모험하세요", taglineStyle);

            cy = frameRect.yMax + 24f;
        }

        // ── 회원가입 패널 ──

        private void DrawRegisterPanel()
        {
            float pw = Mathf.Min(1125f, Screen.width * 0.9f);
            float ph = Mathf.Min(1125f, Screen.height * 0.92f);
            float px = (Screen.width - pw) * 0.5f;
            float py = SafeCenterY(ref ph);

            DrawDecoratedPanel(new Rect(px, py, pw, ph));

            float cx = px + 88f;
            float cy = py + 63f;
            float fieldW = pw - 175f;
            float fieldH = 70f;

            // 타이틀 — base 스타일 동적 fontSize 갱신
            subtitleStyle.fontSize = 60;
            GUI.Label(new Rect(px, cy, pw, 80f), "회원가입", subtitleStyle);
            cy += 100f;

            labelStyle.fontSize = 35;
            fieldStyle.fontSize = 35;
            fieldStyle.fixedHeight = fieldH;

            // 이메일
            GUI.Label(new Rect(cx, cy, 250f, 48f), "이메일:", labelStyle);
            cy += 50f;
            emailInput = GUI.TextField(new Rect(cx, cy, fieldW, fieldH), emailInput, 128, fieldStyle);
            cy += fieldH + 18f;

            // 비밀번호
            GUI.Label(new Rect(cx, cy, 250f, 48f), "비밀번호:", labelStyle);
            cy += 50f;
            passwordInput = GUI.PasswordField(new Rect(cx, cy, fieldW, fieldH), passwordInput, '*', 64, fieldStyle);
            cy += fieldH + 18f;

            // 비밀번호 확인
            GUI.Label(new Rect(cx, cy, 300f, 48f), "비밀번호 확인:", labelStyle);
            cy += 50f;
            confirmPasswordInput = GUI.PasswordField(new Rect(cx, cy, fieldW, fieldH), confirmPasswordInput, '*', 64, fieldStyle);
            cy += fieldH + 18f;

            // 닉네임
            GUI.Label(new Rect(cx, cy, 250f, 48f), "닉네임:", labelStyle);
            cy += 50f;
            nicknameInput = GUI.TextField(new Rect(cx, cy, fieldW, fieldH), nicknameInput, 20, fieldStyle);
            cy += fieldH + 30f;

            // 가입하기 버튼
            float btnH = 85f;
            btnGreenStyle.fontSize = 40;
            GUI.enabled = !isProcessing;
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH), isProcessing ? "로딩 중..." : "가입하기", btnGreenStyle))
            {
                if (!isProcessing)
                {
                    if (passwordInput != confirmPasswordInput)
                    {
                        errorMessage = "비밀번호가 일치하지 않습니다.";
                        errorTimer = 5f;
                    }
                    else if (AuthManager.Instance != null)
                    {
                        isProcessing = true;
                        AuthManager.Instance.RegisterWithEmail(emailInput, passwordInput, nicknameInput);
                    }
                }
            }
            cy += btnH + 18f;

            // 뒤로가기 버튼
            btnGrayStyle.fontSize = 35;
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH - 8), "뒤로가기", btnGrayStyle))
            {
                if (!isProcessing)
                {
                    errorMessage = "";
                    phase = LoginPhase.Login;
                }
            }
            GUI.enabled = true;
            cy += btnH + 13f;

            // 에러 메시지
            if (errorTimer > 0 && !string.IsNullOrEmpty(errorMessage))
            {
                errorStyle.fontSize = 30;
                UIHelper.LabelFit(new Rect(cx, cy, fieldW, 55f), errorMessage, errorStyle);
            }
        }

        // ── 로딩 패널 ──

        private void DrawLoadingPanel()
        {
            float pw = 600f;
            float ph = 375f;
            float px = (Screen.width - pw) * 0.5f;
            float py = SafeCenterY(ref ph);

            DrawDecoratedPanel(new Rect(px, py, pw, ph));

            subtitleStyle.fontSize = 50;
            GUI.Label(new Rect(px, py + 63f, pw, 75f), "로딩 중...", subtitleStyle);

            // 회전하는 곤충 아이콘 (sin/cos 원형 이동 점)
            loadingAngle += Time.deltaTime * 3f;
            float centerX = px + pw * 0.5f;
            float centerY = py + ph * 0.65f;
            float radius = 50f;

            for (int i = 0; i < 6; i++)
            {
                float angle = loadingAngle + i * Mathf.PI * 2f / 6f;
                float dotX = centerX + Mathf.Cos(angle) * radius - 9f;
                float dotY = centerY + Mathf.Sin(angle) * radius - 9f;
                float alpha = 0.3f + 0.7f * ((i + 1f) / 6f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(dotX, dotY, 18f, 18f), loadingDotTextures[i]);
            }
            GUI.color = Color.white;
        }

        // ── 캐릭터 생성 패널 ──

        /// <summary>
        /// 프리뷰를 <b>뺀</b> 고정 콘텐츠 높이(px) — 타이틀·입력·라디오·버튼.
        ///
        /// 프리뷰만 따로 빼는 이유: 화면이 짧으면 프리뷰를 줄여서 나머지를 지킬 수 있지만,
        /// 버튼과 라디오는 줄일 수 없기 때문이다. 하단 "모험 시작" 버튼이 패널 밖으로 밀리면
        /// 캐릭터 생성을 끝낼 수 없어 게임에 진입조차 못 한다 — 그건 세로가 짧은 가로모드에서만
        /// 나타나 개발 중에는 보이지 않는다. <c>CharacterCreateFlowTests</c>가 이 값을 고정한다.
        /// </summary>
        internal static float FixedContentHeight(CreateStep step, bool mobile)
        {
            float radioRow = mobile ? 58f : 45f;
            float titleBlock = 44f + 63f;
            float afterPreview = 24f;
            float buttonBlock = 20f + 58f + 24f;

            if (step == CreateStep.Preset)
            {
                // 세로 배치: 프리뷰 아래에 이름 입력 + 프리셋 2행
                return titleBlock + afterPreview + 50f + 2f * (35f + radioRow) + buttonBlock;
            }

            if (step == CreateStep.Starter)
            {
                // 곤충 카드 3장이 가로로 놓이고 그 아래 버튼. 프리뷰는 이 단계에 없다.
                return titleBlock + StarterCardHeight + 24f + buttonBlock;
            }

            // 세부 조정은 <b>좌우 2열</b>이다 — 프리뷰가 왼쪽, 항목 5개가 오른쪽.
            // 세로로 쌓으면 5행(라벨 35 + 라디오 45~58)이 400~465px라 프리뷰를 최소로 줄여도
            // 가로모드 화면을 넘겼다. 2열이면 그 5행이 프리뷰와 같은 세로를 공유한다.
            // 여기서 세는 건 프리뷰 <b>바깥</b>의 고정 높이뿐이다.
            return titleBlock + afterPreview + buttonBlock;
        }

        /// <summary>
        /// 이 패널 높이에서 3D 프리뷰에 줄 높이. 남는 만큼 쓰되
        /// <see cref="MinPreviewH"/> 아래로는 내려가지 않는다(그보다 작으면 캐릭터를 알아볼 수 없다).
        /// </summary>
        internal static float PreviewHeightFor(CreateStep step, bool mobile, float panelHeight)
        {
            // 스타터 단계에는 3D 프리뷰가 없다 — 곤충 카드가 화면을 채운다.
            if (step == CreateStep.Starter) return 0f;

            float spare = panelHeight - FixedContentHeight(step, mobile);

            if (step == CreateStep.Customize)
            {
                // 2열에서는 오른쪽 항목 5행이 세로를 지배한다 — 프리뷰가 그보다 커질 이유가 없다.
                spare = Mathf.Min(spare, CustomizeRowsHeight(mobile));
            }

            float cap = step == CreateStep.Preset ? PreviewH : PreviewH * 1.1f;
            return Mathf.Clamp(spare, MinPreviewH, cap);
        }

        /// <summary>세부 조정 오른쪽 열의 5행 높이 — 이 값이 그 단계의 세로를 정한다.</summary>
        internal static float CustomizeRowsHeight(bool mobile)
        {
            float rowH = mobile ? 58f : 45f;
            return 5f * (rowH + 12f);
        }

        /// <summary>
        /// 이 단계가 실제로 차지하는 총 세로. 패널 높이를 넘으면 하단 버튼이 밖으로 밀린다.
        ///
        /// 세부 조정은 2열이라 <b>프리뷰와 항목 중 큰 쪽</b>이 몸통 높이가 된다 —
        /// 둘을 더하면 안 된다(그게 세로 배치였던 시절의 계산이고, 그래서 넘쳤다).
        /// </summary>
        internal static float TotalContentHeight(CreateStep step, bool mobile, float panelHeight)
        {
            // 스타터 단계는 고정 높이(카드가 이미 FixedContentHeight에 들어 있다).
            if (step == CreateStep.Starter) return FixedContentHeight(step, mobile);

            float previewH = PreviewHeightFor(step, mobile, panelHeight);
            float body = step == CreateStep.Customize
                ? Mathf.Max(previewH, CustomizeRowsHeight(mobile))
                : previewH;
            return FixedContentHeight(step, mobile) + body;
        }

        /// <summary>스타터 곤충 카드 높이. 이름 + 설명 두 줄이 들어간다.</summary>
        internal const float StarterCardHeight = 210f;

        private const float PreviewH = 300f;
        private const float PreviewW = 200f;

        /// <summary>프리뷰 최소 높이. 이보다 작으면 3D를 보여주는 의미가 없다.</summary>
        internal const float MinPreviewH = 96f;

        private void DrawCharacterCreatePanel()
        {
            ApplyCreateStyles();

            float pw = Mathf.Min(940f, Screen.width * 0.88f);
            float ph = Mathf.Min(1313f, Screen.height * 0.95f);
            float px = (Screen.width - pw) * 0.5f;
            float py = SafeCenterY(ref ph);

            DrawDecoratedPanel(new Rect(px, py, pw, ph));

            if (createStep == CreateStep.Preset) DrawPresetStep(px, py, pw, ph);
            else if (createStep == CreateStep.Customize) DrawCustomizeStep(px, py, pw, ph);
            else DrawStarterStep(px, py, pw, ph);
        }

        /// <summary>
        /// <b>단계마다 폰트 크기를 다시 정해야 한다.</b> GUIStyle이 공유 객체라, 회원가입
        /// (subtitle 60 / label 35)이나 로딩(subtitle 50)을 거쳐 오면 그 값이 그대로 남아
        /// 50f·30f 상자에 큰 글자가 들어가 제목과 항목 이름이 잘린다.
        /// 설정이 다른 메서드에 있어 정적 검사기도 못 잡는 함정이라 한 곳에 모아 둔다.
        /// 한글 줄높이 ≈ fontSize × 1.35 기준으로 상자에 맞춘 값이다.
        /// </summary>
        private void ApplyCreateStyles()
        {
            subtitleStyle.fontSize = 36;      // 50f 상자
            labelStyle.fontSize = 22;         // 30f 상자
            fieldStyle.fontSize = 25;         // 35f 상자
            sectionLabelStyle.fontSize = 22;  // 30f 상자 (InitStyles의 28은 여기서 잘린다)
            btnGreenStyle.fontSize = 34;      // 58f 버튼
            btnGrayStyle.fontSize = 30;       // 58f 버튼(뒤로)
        }

        // ── 1단계: 프리셋 ──

        private void DrawPresetStep(float px, float py, float pw, float ph)
        {
            float cx = px + 63f;
            float cy = py + 44f;
            float fieldW = pw - 125f;

            GUI.Label(new Rect(px, cy, pw, 50f), "캐릭터 선택", subtitleStyle);
            cy += 63f;

            float previewH = PreviewHeightFor(CreateStep.Preset, UIScale.IsMobileLayout, ph);
            float previewW = PreviewW * (previewH / PreviewH);
            DrawLivePreview(px + pw * 0.5f - previewW * 0.5f, cy, previewW, previewH);
            cy += previewH + 24f;

            GUI.Label(new Rect(cx, cy, 75f, 30f), "이름:", labelStyle);   // 오른쪽 필드가 cx+82f — 넓히면 겹친다
            characterName = GUI.TextField(new Rect(cx + 82f, cy, fieldW - 82f, 35f), characterName, 12, fieldStyle);
            cy += 50f;

            // 프리셋은 개수가 5개라 한 줄에 넣으면 라벨이 잘린다 — 두 줄로 나눈다.
            string[] names = OutfitLabels;
            int firstRow = Mathf.CeilToInt(names.Length * 0.5f);
            cy = DrawPresetRow(cx, cy, fieldW, names, 0, firstRow);
            cy = DrawPresetRow(cx, cy, fieldW, names, firstRow, names.Length);
            cy += 20f;

            if (GUI.Button(new Rect(cx, cy, fieldW, 58f), "다음 — 세부 조정", btnGreenStyle))
            {
                createStep = CreateStep.Customize;
            }
        }

        /// <summary>
        /// 프리셋 버튼 한 줄. 고르면 <b>외형 전체</b>가 그 프리셋 값으로 바뀐다 —
        /// 프리셋은 의상만이 아니라 사람 하나를 표현하기 때문이다.
        /// </summary>
        private float DrawPresetRow(float x, float y, float totalW, string[] names, int from, int to)
        {
            int count = to - from;
            if (count <= 0) return y;

            float btnW = (totalW - (count - 1) * 8f) / count;
            float h = UIScale.IsMobileLayout ? 58f : 45f;

            for (int i = 0; i < count; i++)
            {
                int index = from + i;
                float bx = x + i * (btnW + 8f);
                GUIStyle style = (index == selectedOutfit) ? radioSelectedStyle : radioStyle;
                if (GUI.Button(new Rect(bx, y, btnW, h), names[index], style))
                {
                    ApplyPreset(index);
                }
            }
            return y + h + 35f;
        }

        /// <summary>프리셋의 외형을 현재 선택으로 끌어온다. 이후 세부 조정에서 바꿀 수 있다.</summary>
        private void ApplyPreset(int index)
        {
            selectedOutfit = index;
            InsectGame.Core.CharacterPresetLibrary.Preset p = InsectGame.Core.CharacterPresetLibrary.Get(index);
            selectedGender = p.Gender;
            selectedHairStyle = p.HairStyle;
            selectedHairColor = p.HairColor;
            selectedFaceType = p.FaceType;
            selectedSkinColor = p.SkinColor;
        }

        // ── 2단계: 세부 조정 ──

        /// <summary>
        /// 세부 조정 — <b>좌우 2열</b>. 왼쪽에 3D 프리뷰, 오른쪽에 항목 5개.
        ///
        /// 세로로 쌓으면 5행(라벨 35 + 라디오 45~58)만 400~465px라, 프리뷰를 최소로 줄여도
        /// 가로모드 화면에서 하단 버튼이 밀려났다. 패널 폭이 940px이라 좌우로 나눌 여유가 있다.
        /// </summary>
        private void DrawCustomizeStep(float px, float py, float pw, float ph)
        {
            bool mobile = UIScale.IsMobileLayout;
            float cx = px + 63f;
            float cy = py + 44f;
            float fieldW = pw - 125f;

            GUI.Label(new Rect(px, cy, pw, 50f), "세부 조정", subtitleStyle);
            cy += 63f;

            float rowsH = CustomizeRowsHeight(mobile);
            float previewH = PreviewHeightFor(CreateStep.Customize, mobile, ph);
            float previewW = Mathf.Min(PreviewW, fieldW * 0.34f);

            DrawLivePreview(cx, cy + (rowsH - previewH) * 0.5f, previewW, previewH);

            // 오른쪽 열 — 라벨과 라디오를 한 줄에 둔다(세로로 나누면 행마다 35px가 더 든다).
            float colX = cx + previewW + 24f;
            float colW = fieldW - previewW - 24f;
            float labelW = 96f;
            float rowH = mobile ? 58f : 45f;
            float ry = cy;

            selectedGender = DrawLabeledRadio(colX, ry, colW, labelW, rowH, "성별",
                GenderLabels, selectedGender);
            ry += rowH + 12f;

            selectedSkinColor = DrawLabeledRadio(colX, ry, colW, labelW, rowH, "피부색",
                SkinLabels, selectedSkinColor);
            ry += rowH + 12f;

            selectedHairStyle = DrawLabeledRadio(colX, ry, colW, labelW, rowH, "머리",
                HairStyleLabels, selectedHairStyle);
            ry += rowH + 12f;

            selectedHairColor = DrawLabeledRadio(colX, ry, colW, labelW, rowH, "머리색",
                HairColorLabels, selectedHairColor);
            ry += rowH + 12f;

            selectedFaceType = DrawLabeledRadio(colX, ry, colW, labelW, rowH, "표정",
                FaceLabels, selectedFaceType);

            cy += rowsH + 24f + 20f;

            float backW = fieldW * 0.32f;
            if (GUI.Button(new Rect(cx, cy, backW, 58f), "◀ 뒤로", btnGrayStyle))
            {
                createStep = CreateStep.Preset;
            }
            if (GUI.Button(new Rect(cx + backW + 12f, cy, fieldW - backW - 12f, 58f), "다음 — 첫 파트너", btnGreenStyle))
            {
                createStep = CreateStep.Starter;
            }
        }

        // ── 3단계: 첫 파트너 곤충 ──

        /// <summary>
        /// 첫 파트너를 고른다. 지급 자체는 여전히 <c>ch1_intro</c> 비트가 하고, 여기서는
        /// 선택만 저장한다 — 그래서 Story.json을 건드리지 않고도 선택식이 된다.
        /// </summary>
        private void DrawStarterStep(float px, float py, float pw, float ph)
        {
            float cx = px + 63f;
            float cy = py + 44f;
            float fieldW = pw - 125f;

            GUI.Label(new Rect(px, cy, pw, 50f), "첫 파트너 선택", subtitleStyle);
            cy += 63f;

            int count = InsectGame.Data.StarterInsectCatalog.Count;
            float gap = 14f;
            float cardW = (fieldW - (count - 1) * gap) / count;

            for (int i = 0; i < count; i++)
            {
                InsectGame.Data.StarterInsectCatalog.Choice c = InsectGame.Data.StarterInsectCatalog.Get(i);
                Rect card = new Rect(cx + i * (cardW + gap), cy, cardW, StarterCardHeight);

                GUIStyle style = (i == selectedStarter) ? radioSelectedStyle : radioStyle;
                if (GUI.Button(card, GUIContent.none, style))
                {
                    selectedStarter = i;
                }

                // 이름과 설명은 버튼 위에 따로 그린다 — 버튼 라벨 하나로는 두 줄 서식을 못 준다.
                UIHelper.LabelFit(new Rect(card.x + 12f, card.y + 18f, card.width - 24f, 40f),
                    c.DisplayName, subtitleStyle);
                UIHelper.LabelFit(new Rect(card.x + 12f, card.y + 74f, card.width - 24f, StarterCardHeight - 92f),
                    c.Blurb, labelStyle);
            }
            cy += StarterCardHeight + 24f;

            float backW = fieldW * 0.32f;
            if (GUI.Button(new Rect(cx, cy, backW, 58f), "◀ 뒤로", btnGrayStyle))
            {
                createStep = CreateStep.Customize;
            }
            if (GUI.Button(new Rect(cx + backW + 12f, cy, fieldW - backW - 12f, 58f), "모험 시작!", btnGreenStyle))
            {
                SaveCharacterCreation();
                ApplyCharacterOutfitPreset();
                InsectGame.Data.StarterInsectCatalog.SaveChoice(
                    InsectGame.Data.StarterInsectCatalog.Get(selectedStarter).InsectId);
                PlayerPrefs.Save();
                ClearPreviewOverride();
                OnGameReady();
            }
        }

        // 라디오 라벨은 static으로 둔다 — OnGUI에서 매 프레임 배열을 새로 만들지 않기 위해서다.
        private static readonly string[] GenderLabels = { "남자", "여자" };
        private static readonly string[] SkinLabels = { "밝은", "보통", "어두운", "진한" };
        private static readonly string[] HairStyleLabels = { "짧은", "중간", "긴", "올림" };
        private static readonly string[] HairColorLabels = { "검정", "갈색", "금발", "빨강", "보라", "파랑" };
        private static readonly string[] FaceLabels = { "미소", "활짝", "차분", "무표정" };

        /// <summary>라벨 + 라디오를 한 줄에. 세로로 나누면 행마다 35px가 더 든다.</summary>
        private int DrawLabeledRadio(float x, float y, float totalW, float labelW, float rowH,
            string label, string[] options, int selected)
        {
            GUI.Label(new Rect(x, y + (rowH - 30f) * 0.5f, labelW, 30f), label, sectionLabelStyle);
            return DrawRadioRow(x + labelW, y, totalW - labelW, options, selected, rowH);
        }

        // ── 3D 라이브 프리뷰 ──

        /// <summary>
        /// 지금 고른 외형·의상 그대로의 3D 캐릭터.
        ///
        /// <b>여기서 <c>Camera.Render</c>를 부르지 않는다</b> — 렌더러의 <c>Update</c>가 그린다.
        /// OnGUI 도중 카메라를 렌더하면 IMGUI가 깨진다(<c>CharacterOutfitUI</c>와 같은 규약).
        /// 첫 프레임은 아직 그린 게 없어 <c>null</c>이 오므로 2D 초상화로 물러난다 —
        /// 렌더러 배선이 실패한 경우의 안전망도 겸한다.
        /// </summary>
        private void DrawLivePreview(float x, float y, float w, float h)
        {
            Rect box = new Rect(x, y, w, h);

            if (modelPreview != null)
            {
                modelPreview.SetAppearanceOverride(new InsectGame.Core.AppearanceSpec
                {
                    gender = selectedGender,
                    hairStyle = selectedHairStyle,
                    hairColor = selectedHairColor,
                    faceType = selectedFaceType,
                    skinColor = selectedSkinColor,
                });

                previewLoadout.Clear();
                string[] items = InsectGame.Core.CharacterPresetLibrary.Get(selectedOutfit).OutfitItemIds;
                InsectGame.Core.CharacterOutfitManager mgr = InsectGame.Core.CharacterOutfitManager.Instance;
                if (items != null && mgr != null)
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        InsectGame.Core.OutfitItem item = mgr.FindItem(items[i]);
                        if (item != null) previewLoadout.Set(item.slot, items[i]);
                    }
                }

                HandlePreviewDrag(box);

                Texture tex = modelPreview.GetPreview(previewLoadout, previewYaw);
                if (tex != null)
                {
                    GUI.DrawTexture(box, tex, ScaleMode.ScaleToFit);
                    return;
                }
            }

            // 2D 폴백 — 새 7등신 포트레이트: 시각 높이 ≈ 212·s, 가로 ≈ 65·s. 박스에 90% 마진으로 fit.
            float previewScale = Mathf.Min(w / 70f, h / 220f);
            CharacterPortraitRenderer.DrawForCreation(x + w * 0.5f, y + h * 0.5f, previewScale,
                selectedGender, selectedSkinColor, selectedHairColor, selectedHairStyle, selectedFaceType, selectedOutfit);
        }

        /// <summary>
        /// 프리뷰를 드래그해 돌린다. 전체화면 모달이라 <c>FieldHudInput.RegisterBlockingRect</c>는
        /// 필요 없다 — 그 규칙은 플레이어가 자유롭게 움직이는 동안 그려지는 필드 버튼이 대상이다.
        /// </summary>
        private void HandlePreviewDrag(Rect box)
        {
            Event e = Event.current;
            if (e == null) return;

            // 누른 곳이 프리뷰 밖이면 명시적으로 false로 되돌린다.
            // "안이면 true"만 두면, 패널 밖에서 버튼을 뗐을 때 MouseUp이 오지 않아
            // draggingPreview가 true로 굳는다 — 그 뒤로는 패널 어디를 끌어도 모델이 돌고
            // e.Use()가 드래그 이벤트를 삼켜 다른 조작이 먹지 않는다.
            if (e.type == EventType.MouseDown)
            {
                draggingPreview = box.Contains(e.mousePosition);
            }
            else if (e.type == EventType.MouseUp)
            {
                draggingPreview = false;
            }
            else if (e.type == EventType.MouseDrag && draggingPreview)
            {
                previewYaw += e.delta.x * 0.5f;
                e.Use();
            }
        }

        /// <param name="rowH">0이면 기존 기본 높이를 쓴다(다른 화면의 호출부가 그대로 돌게).</param>
        private int DrawRadioRow(float x, float y, float totalW, string[] labels, int selected, float rowH = 0f)
        {
            float h = rowH > 0f ? rowH : (UIScale.IsMobileLayout ? 50f : 35f);
            float btnW = (totalW - (labels.Length - 1) * 8f) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                float bx = x + i * (btnW + 8f);
                GUIStyle style = (i == selected) ? radioSelectedStyle : radioStyle;
                if (GUI.Button(new Rect(bx, y, btnW, h), labels[i], style))
                {
                    selected = i;
                }
            }
            return selected;
        }

        /// <summary>
        /// 3D 프리뷰 렌더러를 받는다. Bootstrap이 렌더러를 만든 <b>뒤에</b> 불러야 한다 —
        /// LoginUI는 그보다 먼저 생성되므로 생성자에서 찾을 수 없다.
        /// </summary>
        public void AutoWire(InsectGame.Core.CharacterModelPreviewRenderer preview)
        {
            modelPreview = preview;
        }

        /// <summary>
        /// 생성 화면을 벗어날 때 프리뷰가 <b>저장 안 된 외형</b>을 계속 들고 있지 않게 한다.
        /// 이걸 빠뜨리면 나중에 의상 화면이 생성 당시 만지던 얼굴을 보여준다.
        /// </summary>
        private void ClearPreviewOverride()
        {
            if (modelPreview == null) return;
            modelPreview.SetAppearanceOverride(null);
            modelPreview.InvalidatePreview();
        }

        // ── 이벤트 핸들러 ──

        private void OnLoginCompleted(bool success, string error)
        {
            isProcessing = false;
            if (success)
            {
                // 마스터 계정은 클라우드 스킵, 바로 진행
                bool isMaster = AuthManager.Instance != null && AuthManager.Instance.IsMasterAccount;
                if (isMaster)
                {
                    CheckCharacterCreation();
                    return;
                }

                // 일반 계정: 클라우드 데이터 확인
                if (CloudSaveManager.Instance != null)
                {
                    waitingForCloudLoad = true;
                    SubscribeCloudLoad();
                    CloudSaveManager.Instance.LoadFromCloud();
                    phase = LoginPhase.Loading;
                }
                else
                {
                    CheckCharacterCreation();
                }
            }
            else
            {
                errorMessage = error ?? "로그인에 실패했습니다.";
                errorTimer = 5f;
            }
        }

        private void OnRegisterCompleted(bool success, string error)
        {
            isProcessing = false;
            if (success)
            {
                // 새 유저이므로 캐릭터 생성으로
                TutorialQuestManager.Instance?.ResetForNewAccount();
                EnterCharacterCreate();
            }
            else
            {
                errorMessage = error ?? "회원가입에 실패했습니다.";
                errorTimer = 5f;
            }
        }

        private void OnCloudLoadCompleted(bool hasData)
        {
            waitingForCloudLoad = false;
            if (CloudSaveManager.Instance != null)
                CloudSaveManager.Instance.LoadCompleted -= OnCloudLoadCompleted;
            // 토큰 갱신 실패로 이미 로그아웃된 상태면(AuthFailed가 phase=Login으로 전환) 신규 유저로 오인하지
            // 않는다. 로그아웃 상태에서 CharacterCreate로 가면 비로그인 전역 스코프에 캐릭터가 쌓여
            // 다음 정상 로그인 시 계정 데이터와 어긋난다.
            if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn)
            {
                phase = LoginPhase.Login;
                return;
            }
            if (hasData)
            {
                // 기존 유저 -> 바로 게임 시작
                OnGameReady();
            }
            else
            {
                // 새 유저 -> 캐릭터 생성
                if (CloudSaveManager.Instance != null && CloudSaveManager.Instance.LastLoadWasNotFound)
                    TutorialQuestManager.Instance?.ResetForNewAccount();
                EnterCharacterCreate();
            }
        }

        /// <summary>
        /// 체크박스 아래 한 줄. <b>세 상태를 구분해야 한다</b> — 특히 "지금 누르면 세이브가
        /// 지워지는가"를 눌러 보기 전에 알 수 있어야 한다.
        /// 초기화는 스위치를 <b>켜는 그 로그인</b>에서만 일어나고, 켠 채로 다시 로그인하면
        /// 진행이 이어진다(AuthManager.BeginMasterFreshStart).
        /// </summary>
        private string MasterPlainHint()
        {
            if (!masterPlainMode)
                return "전 지역 해금 · 수문장 격파 처리 · 재화 999999로 시작합니다";
            if (AuthManager.MasterPlainMode)
                return "특권 없음 — 진행 중인 마스터 세이브를 이어서 시작합니다";
            return "로그인하면 마스터 세이브를 지우고 처음부터 시작합니다";
        }

        // ── 캐릭터 생성 확인 ──

        private void CheckCharacterCreation()
        {
            if (PlayerPrefs.GetInt(CharCreatedKey, 0) == 1)
            {
                OnGameReady();
                return;
            }

            // PlayerPrefs는 비어있지만 이전 진행 세이브가 있으면 = 이전에 플레이한 적 있음.
            // (마스터 계정이 다른 환경 접속, PlayerPrefs 손실 등). 기본 캐릭터로 즉시 시작.
            string progressPath = InsectGame.Core.SaveScope.FilePath(GameConstants.SaveFiles.PlayerProgress);
            if (System.IO.File.Exists(progressPath))
            {
                PlayerPrefs.SetInt(CharCreatedKey, 1);
                PlayerPrefs.Save();
                OnGameReady();
                return;
            }

            EnterCharacterCreate();
        }

        /// <summary>
        /// 생성 화면에 들어갈 때는 <b>항상 1단계부터</b>다. 리셋을 빠뜨리면 로그아웃 후 다시
        /// 만들 때 지난번 세부 조정 화면이 먼저 뜬다.
        /// 기본 프리셋의 외형도 함께 끌어와 첫 프리뷰가 빈 기본값이 아니게 한다.
        /// </summary>
        private void EnterCharacterCreate()
        {
            phase = LoginPhase.CharacterCreate;
            createStep = CreateStep.Preset;
            previewYaw = InsectGame.Core.CharacterModelPreviewRenderer.FrontYaw;
            // 재진입(인증 실패 후 재로그인 등)에서 지난 선택이 남지 않게 함께 되돌린다.
            selectedStarter = 0;
            draggingPreview = false;
            ApplyPreset(selectedOutfit);
        }

        // ── 캐릭터 데이터 저장 ──

        private void SaveCharacterCreation()
        {
            PlayerPrefs.SetString(CharNameKey, characterName);
            PlayerPrefs.SetInt(CharSkinKey, selectedSkinColor);
            PlayerPrefs.SetInt(CharHairKey, selectedHairStyle);
            PlayerPrefs.SetInt(CharOutfitKey, selectedOutfit);
            PlayerPrefs.SetInt(CharGenderKey, selectedGender);
            PlayerPrefs.SetInt(CharHairColorKey, selectedHairColor);
            PlayerPrefs.SetInt(CharFaceTypeKey, selectedFaceType);
            PlayerPrefs.SetInt(CharCreatedKey, 1);
            PlayerPrefs.Save();
            // CharacterPortraitRenderer 캐시는 OutfitChanged만 구독하므로 외형 변경 시 명시적 무효화
            CharacterPortraitRenderer.InvalidateCache();
        }

        // ── 의상 프리셋 적용 ──

        /// <summary>
        /// 고른 프리셋의 의상을 실제로 입힌다. 프리셋 정의는
        /// <see cref="CharacterPresetLibrary"/>가 단일 출처다.
        /// </summary>
        private void ApplyCharacterOutfitPreset()
        {
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            if (mgr == null) return;

            string[] items = CharacterPresetLibrary.Get(selectedOutfit).OutfitItemIds;
            if (items == null) return;
            for (int i = 0; i < items.Length; i++) mgr.Equip(items[i]);
        }

        // ── 게임 시작 ──

        private void OnGameReady()
        {
            phase = LoginPhase.Done;
            // 월드 로비 표시 (월드 시스템이 있으면 로비로, 없으면 바로 게임)
            WorldLobbyUI lobby = FindFirstObjectByType<WorldLobbyUI>();
            if (lobby != null)
            {
                lobby.ShowLobby();
            }
            else
            {
                PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null) pm.SetFrozen(false);
                TutorialQuestManager.Instance?.BeginTutorialForCurrentAccount();
            }
        }

        // ── GUI 스타일 초기화 ──

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            appIconTexture = Resources.Load<Texture2D>(AppIconResourcePath);

            backgroundTexture = MakeGradientTex(2, 128,
                new Color(0.035f, 0.055f, 0.16f, 1f),
                new Color(0.025f, 0.15f, 0.095f, 1f));
            backgroundGlowTexture = MakeRadialTex(64,
                new Color(0.36f, 0.95f, 0.55f, 0.72f), Color.clear);

            // 짙은 유리 질감 + 이중 테두리. GUIStyle.border로 모서리를 늘리지 않고 유지합니다.
            Texture2D panelTex = MakePanelTex(64, 11f,
                new Color(0.075f, 0.115f, 0.17f, 0.97f),
                new Color(0.035f, 0.07f, 0.09f, 0.97f),
                new Color(0.32f, 0.82f, 0.5f, 0.92f));
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTex;
            panelStyle.border = new RectOffset(14, 14, 14, 14);

            panelShadowStyle = new GUIStyle(GUI.skin.box);
            panelShadowStyle.normal.background = MakePanelTex(64, 11f,
                new Color(0f, 0f, 0f, 0.48f), new Color(0f, 0f, 0f, 0.48f), Color.clear);
            panelShadowStyle.border = new RectOffset(14, 14, 14, 14);

            logoFrameStyle = new GUIStyle(GUI.skin.box);
            logoFrameStyle.normal.background = MakePanelTex(64, 10f,
                new Color(0.12f, 0.24f, 0.2f, 1f),
                new Color(0.035f, 0.07f, 0.1f, 1f),
                new Color(0.78f, 0.68f, 0.23f, 0.95f));
            logoFrameStyle.border = new RectOffset(12, 12, 12, 12);

            panelHeaderTexture = MakeGradientTex(2, 64,
                new Color(0.12f, 0.48f, 0.32f, 0.34f),
                new Color(0.08f, 0.2f, 0.3f, 0f));
            panelAccentTexture = MakeGradientTex(64, 1,
                new Color(0.36f, 0.95f, 0.56f, 0.35f),
                new Color(0.42f, 0.7f, 1f, 0.92f));

            // 타이틀: 금색 Bold
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 70;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(1f, 0.84f, 0f, 1f);
            titleStyle.alignment = TextAnchor.MiddleCenter;

            // 서브타이틀
            subtitleStyle = new GUIStyle(GUI.skin.label);
            subtitleStyle.fontSize = 45;
            subtitleStyle.fontStyle = FontStyle.Bold;
            subtitleStyle.normal.textColor = Color.white;
            subtitleStyle.alignment = TextAnchor.MiddleCenter;

            taglineStyle = new GUIStyle(GUI.skin.label);
            taglineStyle.fontSize = 29;
            taglineStyle.fontStyle = FontStyle.Normal;
            taglineStyle.normal.textColor = new Color(0.68f, 0.82f, 0.78f, 1f);
            taglineStyle.alignment = TextAnchor.MiddleCenter;

            brandEyebrowStyle = new GUIStyle(GUI.skin.label);
            brandEyebrowStyle.fontSize = 18;
            brandEyebrowStyle.fontStyle = FontStyle.Bold;
            brandEyebrowStyle.normal.textColor = new Color(0.44f, 0.98f, 0.6f, 1f);
            brandEyebrowStyle.alignment = TextAnchor.MiddleLeft;

            helperStyle = new GUIStyle(GUI.skin.label);
            helperStyle.fontSize = 21;
            helperStyle.normal.textColor = new Color(0.58f, 0.7f, 0.72f, 1f);
            helperStyle.alignment = TextAnchor.MiddleCenter;

            versionStyle = new GUIStyle(GUI.skin.label);
            versionStyle.fontSize = 18;
            versionStyle.normal.textColor = new Color(0.42f, 0.56f, 0.58f, 1f);
            versionStyle.alignment = TextAnchor.MiddleCenter;

            linkStyle = new GUIStyle(GUI.skin.label);
            linkStyle.fontSize = 20;
            linkStyle.normal.textColor = new Color(0.65f, 0.8f, 0.82f, 1f);
            linkStyle.hover.textColor = Color.white;
            linkStyle.active.textColor = new Color(0.44f, 0.98f, 0.6f, 1f);
            linkStyle.alignment = TextAnchor.MiddleRight;

            // 입력 필드
            fieldStyle = new GUIStyle(GUI.skin.textField);
            fieldStyle.fontSize = 30;
            fieldStyle.normal.textColor = Color.white;
            Texture2D fieldBg = MakeGradientTex(2, 24,
                new Color(0.1f, 0.16f, 0.2f, 0.98f), new Color(0.06f, 0.1f, 0.14f, 0.98f));
            fieldStyle.normal.background = fieldBg;
            fieldStyle.focused.background = MakeGradientTex(2, 24,
                new Color(0.13f, 0.25f, 0.28f, 1f), new Color(0.08f, 0.16f, 0.22f, 1f));
            fieldStyle.focused.textColor = Color.white;
            fieldStyle.padding = new RectOffset(15, 15, 10, 10);

            // 라벨
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 28;
            labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f, 1f);

            // 에러
            errorStyle = new GUIStyle(GUI.skin.label);
            errorStyle.fontSize = 25;
            errorStyle.normal.textColor = new Color(1f, 0.3f, 0.3f, 1f);
            errorStyle.alignment = TextAnchor.MiddleCenter;
            errorStyle.wordWrap = true;

            // 버튼 공통 베이스
            GUIStyle BaseBtnStyle(Color bgColor)
            {
                GUIStyle s = new GUIStyle(GUI.skin.button);
                s.normal.background = MakeGradientTex(2, 24, Lighten(bgColor, 0.12f), Darken(bgColor, 0.11f));
                s.hover.background = MakeGradientTex(2, 24, Lighten(bgColor, 0.22f), bgColor);
                s.active.background = MakeGradientTex(2, 24, bgColor, Darken(bgColor, 0.2f));
                s.normal.textColor = Color.white;
                s.hover.textColor = Color.white;
                s.active.textColor = Color.white;
                s.fontSize = 33;
                s.fontStyle = FontStyle.Bold;
                s.alignment = TextAnchor.MiddleCenter;
                return s;
            }

            // 녹색 (로그인)
            btnGreenStyle = BaseBtnStyle(new Color(0.15f, 0.55f, 0.15f, 1f));

            // 파란색 (회원가입)
            btnBlueStyle = BaseBtnStyle(new Color(0.2f, 0.35f, 0.7f, 1f));
            btnBlueStyle.fontSize = 30;

            // Google 브랜드에 맞춘 밝은 단일 소셜 버튼
            btnYellowStyle = BaseBtnStyle(new Color(0.93f, 0.95f, 0.98f, 1f));
            btnYellowStyle.fontSize = 28;
            Color googleText = new Color(0.11f, 0.16f, 0.22f, 1f);
            btnYellowStyle.normal.textColor = googleText;
            btnYellowStyle.hover.textColor = googleText;
            btnYellowStyle.active.textColor = googleText;

            // 회색 (게스트)
            btnGrayStyle = BaseBtnStyle(new Color(0.35f, 0.35f, 0.38f, 1f));
            btnGrayStyle.fontSize = 28;

            // 구분선
            separatorStyle = new GUIStyle(GUI.skin.label);
            separatorStyle.fontSize = 25;
            separatorStyle.normal.textColor = new Color(0.5f, 0.5f, 0.55f, 1f);
            separatorStyle.alignment = TextAnchor.MiddleCenter;

            // 라디오 버튼
            radioStyle = BaseBtnStyle(new Color(0.25f, 0.25f, 0.3f, 1f));
            radioStyle.fontSize = 25;
            radioStyle.fontStyle = FontStyle.Normal;

            radioSelectedStyle = BaseBtnStyle(new Color(0.2f, 0.5f, 0.8f, 1f));
            radioSelectedStyle.fontSize = 25;
            radioSelectedStyle.fontStyle = FontStyle.Bold;

            // 섹션 라벨
            sectionLabelStyle = new GUIStyle(GUI.skin.label);
            sectionLabelStyle.fontSize = 28;
            sectionLabelStyle.fontStyle = FontStyle.Bold;
            sectionLabelStyle.normal.textColor = new Color(0.9f, 0.85f, 0.6f, 1f);

            for (int i = 0; i < loadingDotTextures.Length; i++)
            {
                float t = (i + 1f) / loadingDotTextures.Length;
                loadingDotTextures[i] = MakeRadialTex(24,
                    Color.Lerp(new Color(0.32f, 0.7f, 1f, 1f), new Color(0.42f, 1f, 0.55f, 1f), t),
                    Color.clear);
            }
        }

        private Texture2D MakeGradientTex(int w, int h, Color start, Color end)
        {
            Color[] pix = new Color[w * h];
            bool horizontal = w > h;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float t = horizontal ? x / Mathf.Max(1f, w - 1f) : y / Mathf.Max(1f, h - 1f);
                    pix[y * w + x] = Color.Lerp(start, end, t);
                }
            }
            Texture2D tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            generatedTextures.Add(tex);
            return tex;
        }

        private Texture2D MakeRadialTex(int size, Color center, Color edge)
        {
            Color[] pix = new Color[size * size];
            Vector2 midpoint = new Vector2((size - 1f) * 0.5f, (size - 1f) * 0.5f);
            float maxDistance = Mathf.Max(1f, midpoint.x);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), midpoint) / maxDistance);
                    t = t * t * (3f - 2f * t);
                    pix[y * size + x] = Color.Lerp(center, edge, t);
                }
            }
            Texture2D tex = new Texture2D(size, size);
            tex.SetPixels(pix);
            tex.Apply();
            generatedTextures.Add(tex);
            return tex;
        }

        private Texture2D MakePanelTex(int size, float radius, Color top, Color bottom, Color border)
        {
            Color[] pix = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = IsInsideRoundedRect(x, y, size, radius);
                    if (!inside)
                    {
                        pix[y * size + x] = Color.clear;
                        continue;
                    }

                    bool isBorder = !IsInsideRoundedRect(x, y, size, radius, 2f);
                    float t = y / Mathf.Max(1f, size - 1f);
                    Color fill = Color.Lerp(top, bottom, t);
                    if (((x + y) / 7) % 2 == 0) fill = Lighten(fill, 0.018f);
                    pix[y * size + x] = isBorder ? border : fill;
                }
            }
            Texture2D tex = new Texture2D(size, size);
            tex.SetPixels(pix);
            tex.Apply();
            generatedTextures.Add(tex);
            return tex;
        }

        private static bool IsInsideRoundedRect(float x, float y, float size, float radius, float inset = 0f)
        {
            float min = inset;
            float max = size - 1f - inset;
            if (x < min || x > max || y < min || y > max) return false;

            float innerRadius = Mathf.Max(0f, radius - inset);
            float nearestX = Mathf.Clamp(x, min + innerRadius, max - innerRadius);
            float nearestY = Mathf.Clamp(y, min + innerRadius, max - innerRadius);
            float dx = x - nearestX;
            float dy = y - nearestY;
            return dx * dx + dy * dy <= innerRadius * innerRadius;
        }

        private static Color Lighten(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a);
        }

        private static Color Darken(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r - amount),
                Mathf.Clamp01(color.g - amount),
                Mathf.Clamp01(color.b - amount),
                color.a);
        }
    }
}
