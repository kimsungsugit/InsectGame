using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    public class LoginUI : MonoBehaviour
    {
        private enum LoginPhase { Login, Register, Loading, CharacterCreate, Done }

        private LoginPhase phase = LoginPhase.Login;
        private string emailInput = "";
        private string passwordInput = "";
        private string confirmPasswordInput = "";
        private string nicknameInput = "";
        private string errorMessage = "";
        private float errorTimer;
        private bool isProcessing;

        // 캐릭터 생성
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
        }

        private void OnDisable()
        {
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.LoginCompleted -= OnLoginCompleted;
                AuthManager.Instance.RegisterCompleted -= OnRegisterCompleted;
                AuthManager.Instance.AuthFailed -= OnAuthFailed;
            }
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
            phase = LoginPhase.Login;
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

            // 로그인 버튼
            btnGreenStyle.fontSize = Mathf.RoundToInt(38f * layoutScale);
            GUI.enabled = !isProcessing;
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH), isProcessing ? "로딩 중..." : "로그인", btnGreenStyle))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
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
                GUI.Label(new Rect(cx, cy, fieldW, 55f), errorMessage, errorStyle);
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
                GUI.Label(new Rect(cx, cy, fieldW, 55f), errorMessage, errorStyle);
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

        private void DrawCharacterCreatePanel()
        {
            float pw = Mathf.Min(940f, Screen.width * 0.88f);
            float ph = Mathf.Min(1313f, Screen.height * 0.95f);
            float px = (Screen.width - pw) * 0.5f;
            float py = SafeCenterY(ref ph);

            DrawDecoratedPanel(new Rect(px, py, pw, ph));

            float cx = px + 63f;
            float cy = py + 44f;
            float fieldW = pw - 125f;

            // 타이틀
            GUI.Label(new Rect(px, cy, pw, 50f), "캐릭터 생성", subtitleStyle);
            cy += 63f;

            // 캐릭터 미리보기 (간단한 사각형 조합 실루엣)
            DrawCharacterPreview(px + pw * 0.5f - 50f, cy, 100f, 175f);
            cy += 194f;

            // 이름
            GUI.Label(new Rect(cx, cy, 75f, 30f), "이름:", labelStyle);
            characterName = GUI.TextField(new Rect(cx + 82f, cy, fieldW - 82f, 35f), characterName, 12, fieldStyle);
            cy += 50f;

            // 성별
            string[] genderLabels = { "남자", "여자" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "성별:", sectionLabelStyle);
            cy += 35f;
            selectedGender = DrawRadioRow(cx, cy, fieldW, genderLabels, selectedGender);
            cy += UIScale.IsMobileLayout ? 58f : 45f;

            // 피부색
            string[] skinLabels = { "밝은", "보통", "어두운", "진한" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "피부색:", sectionLabelStyle);
            cy += 35f;
            selectedSkinColor = DrawRadioRow(cx, cy, fieldW, skinLabels, selectedSkinColor);
            cy += UIScale.IsMobileLayout ? 58f : 45f;

            // 머리 스타일
            string[] hairLabels = { "짧은", "중간", "긴", "올림" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "머리:", sectionLabelStyle);
            cy += 35f;
            selectedHairStyle = DrawRadioRow(cx, cy, fieldW, hairLabels, selectedHairStyle);
            cy += UIScale.IsMobileLayout ? 58f : 45f;

            // 머리 색상
            string[] hairColorLabels = { "검정", "갈색", "금발", "빨강", "보라", "파랑" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "머리색:", sectionLabelStyle);
            cy += 35f;
            selectedHairColor = DrawRadioRow(cx, cy, fieldW, hairColorLabels, selectedHairColor);
            cy += UIScale.IsMobileLayout ? 58f : 45f;

            // 표정
            string[] faceLabels = { "미소", "활짝", "차분", "무표정" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "표정:", sectionLabelStyle);
            cy += 35f;
            selectedFaceType = DrawRadioRow(cx, cy, fieldW, faceLabels, selectedFaceType);
            cy += UIScale.IsMobileLayout ? 58f : 45f;

            // 의상
            string[] outfitLabels = { "탐험가", "연구원", "자유" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "의상:", sectionLabelStyle);
            cy += 35f;
            selectedOutfit = DrawRadioRow(cx, cy, fieldW, outfitLabels, selectedOutfit);
            cy += 63f;

            // 모험 시작 버튼
            if (GUI.Button(new Rect(cx, cy, fieldW, 58f), "모험 시작!", btnGreenStyle))
            {
                SaveCharacterCreation();
                ApplyCharacterOutfitPreset();
                OnGameReady();
            }
        }

        private int DrawRadioRow(float x, float y, float totalW, string[] labels, int selected)
        {
            float btnW = (totalW - (labels.Length - 1) * 8f) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                float bx = x + i * (btnW + 8f);
                GUIStyle style = (i == selected) ? radioSelectedStyle : radioStyle;
                if (GUI.Button(new Rect(bx, y, btnW, UIScale.IsMobileLayout ? 50f : 35f), labels[i], style))
                {
                    selected = i;
                }
            }
            return selected;
        }

        private void DrawCharacterPreview(float x, float y, float w, float h)
        {
            // 새 7등신 포트레이트: 캐릭터 시각 높이 ≈ 212·s, 가로 ≈ 65·s. 박스에 90% 마진으로 fit.
            float previewScale = Mathf.Min(w / 70f, h / 220f);
            float cx = x + w * 0.5f;
            float cy = y + h * 0.5f;
            CharacterPortraitRenderer.DrawForCreation(cx, cy, previewScale,
                selectedGender, selectedSkinColor, selectedHairColor, selectedHairStyle, selectedFaceType, selectedOutfit);
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
                    CloudSaveManager.Instance.LoadCompleted += OnCloudLoadCompleted;
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
                phase = LoginPhase.CharacterCreate;
            }
            else
            {
                errorMessage = error ?? "회원가입에 실패했습니다.";
                errorTimer = 5f;
            }
        }

        private void OnCloudLoadCompleted(bool hasData)
        {
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
                phase = LoginPhase.CharacterCreate;
            }
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

            phase = LoginPhase.CharacterCreate;
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

        private void ApplyCharacterOutfitPreset()
        {
            if (CharacterOutfitManager.Instance == null) return;

            switch (selectedOutfit)
            {
                case 0: // 탐험가
                    CharacterOutfitManager.Instance.Equip("hat_cap");
                    CharacterOutfitManager.Instance.Equip("top_shirt");
                    CharacterOutfitManager.Instance.Equip("outer_jacket");
                    CharacterOutfitManager.Instance.Equip("bot_pants");
                    CharacterOutfitManager.Instance.Equip("shoe_boots");
                    CharacterOutfitManager.Instance.Equip("bag_basic");
                    CharacterOutfitManager.Instance.Equip("tool_net");
                    break;
                case 1: // 연구원
                    CharacterOutfitManager.Instance.Equip("hat_none");
                    CharacterOutfitManager.Instance.Equip("top_shirt");
                    CharacterOutfitManager.Instance.Equip("outer_labcoat");
                    CharacterOutfitManager.Instance.Equip("bot_pants");
                    CharacterOutfitManager.Instance.Equip("shoe_sneakers");
                    CharacterOutfitManager.Instance.Equip("bag_science");
                    CharacterOutfitManager.Instance.Equip("tool_magnify");
                    break;
                case 2: // 자유
                    CharacterOutfitManager.Instance.Equip("hat_none");
                    CharacterOutfitManager.Instance.Equip("top_polo");
                    CharacterOutfitManager.Instance.Equip("outer_none");
                    CharacterOutfitManager.Instance.Equip("bot_shorts");
                    CharacterOutfitManager.Instance.Equip("shoe_sandals");
                    CharacterOutfitManager.Instance.Equip("bag_none");
                    CharacterOutfitManager.Instance.Equip("tool_none");
                    break;
            }
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
