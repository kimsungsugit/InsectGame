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
        private const string CharNameKey = "InsectGame.Character.Name";
        private const string CharSkinKey = "InsectGame.Character.SkinColor";
        private const string CharHairKey = "InsectGame.Character.HairStyle";
        private const string CharOutfitKey = "InsectGame.Character.OutfitPreset";
        private const string CharCreatedKey = "InsectGame.Character.Created";
        private const string CharGenderKey = "InsectGame.Character.Gender";
        private const string CharHairColorKey = "InsectGame.Character.HairColor";
        private const string CharFaceTypeKey = "InsectGame.Character.FaceType";

        // 스타일 캐시
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle fieldStyle;
        private GUIStyle labelStyle;
        private GUIStyle errorStyle;
        private GUIStyle btnGreenStyle;
        private GUIStyle btnBlueStyle;
        private GUIStyle btnYellowStyle;
        private GUIStyle btnBrownStyle;
        private GUIStyle btnGrayStyle;
        private GUIStyle separatorStyle;
        private GUIStyle radioStyle;
        private GUIStyle radioSelectedStyle;
        private GUIStyle sectionLabelStyle;
        private bool stylesInitialized;

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

            switch (phase)
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
        }

        // ── 배경 ──

        private void DrawBackground()
        {
            // 위쪽 절반: 진한 남색
            Texture2D topTex = MakeTex(1, 1, new Color(0.05f, 0.05f, 0.18f, 1f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height / 2), topTex);

            // 아래쪽 절반: 짙은 녹색
            Texture2D botTex = MakeTex(1, 1, new Color(0.03f, 0.12f, 0.05f, 1f));
            GUI.DrawTexture(new Rect(0, Screen.height / 2, Screen.width, Screen.height / 2), botTex);

            // 중간 블렌드 영역
            Texture2D midTex = MakeTex(1, 1, new Color(0.04f, 0.08f, 0.12f, 0.8f));
            GUI.DrawTexture(new Rect(0, Screen.height * 0.35f, Screen.width, Screen.height * 0.3f), midTex);
        }

        // ── 로그인 패널 ──

        private void DrawLoginPanel()
        {
            float pw = Mathf.Min(1125f, Screen.width * 0.9f);
            float ph = Mathf.Min(1250f, Screen.height * 0.95f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            float cx = px + 88f;
            float cy = py + 63f;
            float fieldW = pw - 175f;
            float fieldH = 70f;
            float btnH = 85f;

            // 타이틀 — base 캐시 + fontSize 동적 갱신 (옛 매 프레임 new GUIStyle 회귀 차단)
            titleStyle.fontSize = 80;
            GUI.Label(new Rect(px, cy, pw, 100f), "곤충탐험 온라인", titleStyle);
            cy += 125f;

            // 이메일
            labelStyle.fontSize = 35;
            GUI.Label(new Rect(cx, cy, 250f, 48f), "이메일:", labelStyle);
            cy += 50f;
            fieldStyle.fontSize = 35;
            fieldStyle.fixedHeight = fieldH;
            emailInput = GUI.TextField(new Rect(cx, cy, fieldW, fieldH), emailInput, 128, fieldStyle);
            cy += fieldH + 23f;

            // 비밀번호
            GUI.Label(new Rect(cx, cy, 250f, 48f), "비밀번호:", labelStyle);
            cy += 50f;
            passwordInput = GUI.PasswordField(new Rect(cx, cy, fieldW, fieldH), passwordInput, '*', 64, fieldStyle);
            cy += fieldH + 33f;

            // 로그인 버튼
            btnGreenStyle.fontSize = 40;
            GUI.enabled = !isProcessing;
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH), isProcessing ? "로딩 중..." : "로그인", btnGreenStyle))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginWithEmail(emailInput, passwordInput);
                }
            }
            cy += btnH + 18f;

            // 회원가입 버튼
            btnBlueStyle.fontSize = 35;
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH - 8), "이메일 회원가입", btnBlueStyle))
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
            cy += btnH + 13f;

            // 구분선
            separatorStyle.fontSize = 30;
            GUI.Label(new Rect(px, cy, pw, 40f), "───── 또는 ─────", separatorStyle);
            cy += 53f;

            // 소셜 로그인
            float socialBtnH = 73f;
            GUI.enabled = !isProcessing;
            btnYellowStyle.fontSize = 33;
            if (GUI.Button(new Rect(cx, cy, fieldW, socialBtnH), "Google로 로그인", btnYellowStyle))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginWithGoogle();
                }
            }
            cy += socialBtnH + 13f;

            btnBrownStyle.fontSize = 33;
            if (GUI.Button(new Rect(cx, cy, fieldW, socialBtnH), "카카오로 로그인", btnBrownStyle))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginWithKakao();
                }
            }
            cy += socialBtnH + 13f;

            btnGrayStyle.fontSize = 33;
            if (GUI.Button(new Rect(cx, cy, fieldW, socialBtnH), "게스트로 시작", btnGrayStyle))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginAsGuest();
                }
            }
            GUI.enabled = true;
            cy += socialBtnH + 23f;

            // 에러 메시지
            if (errorTimer > 0 && !string.IsNullOrEmpty(errorMessage))
            {
                errorStyle.fontSize = 30;
                GUI.Label(new Rect(cx, cy, fieldW, 55f), errorMessage, errorStyle);
            }
        }

        // ── 회원가입 패널 ──

        private void DrawRegisterPanel()
        {
            float pw = Mathf.Min(1125f, Screen.width * 0.9f);
            float ph = Mathf.Min(1125f, Screen.height * 0.92f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

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
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

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
                Texture2D fadeTex = MakeTex(1, 1, new Color(0.4f, 0.8f, 0.3f, alpha));
                GUI.DrawTexture(new Rect(dotX, dotY, 18f, 18f), fadeTex);
            }
        }

        // ── 캐릭터 생성 패널 ──

        private void DrawCharacterCreatePanel()
        {
            float pw = Mathf.Min(940f, Screen.width * 0.88f);
            float ph = Mathf.Min(1313f, Screen.height * 0.95f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

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
            cy += 45f;

            // 피부색
            string[] skinLabels = { "밝은", "보통", "어두운", "진한" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "피부색:", sectionLabelStyle);
            cy += 35f;
            selectedSkinColor = DrawRadioRow(cx, cy, fieldW, skinLabels, selectedSkinColor);
            cy += 45f;

            // 머리 스타일
            string[] hairLabels = { "짧은", "중간", "긴", "올림" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "머리:", sectionLabelStyle);
            cy += 35f;
            selectedHairStyle = DrawRadioRow(cx, cy, fieldW, hairLabels, selectedHairStyle);
            cy += 45f;

            // 머리 색상
            string[] hairColorLabels = { "검정", "갈색", "금발", "빨강", "보라", "파랑" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "머리색:", sectionLabelStyle);
            cy += 35f;
            selectedHairColor = DrawRadioRow(cx, cy, fieldW, hairColorLabels, selectedHairColor);
            cy += 45f;

            // 표정
            string[] faceLabels = { "미소", "활짝", "차분", "무표정" };
            GUI.Label(new Rect(cx, cy, 75f, 30f), "표정:", sectionLabelStyle);
            cy += 35f;
            selectedFaceType = DrawRadioRow(cx, cy, fieldW, faceLabels, selectedFaceType);
            cy += 45f;

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
                if (GUI.Button(new Rect(bx, y, btnW, 35f), labels[i], style))
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
            if (hasData)
            {
                // 기존 유저 -> 바로 게임 시작
                OnGameReady();
            }
            else
            {
                // 새 유저 -> 캐릭터 생성
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
            string progressPath = System.IO.Path.Combine(
                Application.persistentDataPath, GameConstants.SaveFiles.PlayerProgress);
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
            }
        }

        // ── GUI 스타일 초기화 ──

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            // 패널 배경: 반투명 검정
            Texture2D panelTex = MakeTex(1, 1, new Color(0.08f, 0.08f, 0.12f, 0.92f));
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTex;

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

            // 입력 필드
            fieldStyle = new GUIStyle(GUI.skin.textField);
            fieldStyle.fontSize = 30;
            fieldStyle.normal.textColor = Color.white;
            Texture2D fieldBg = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.2f, 1f));
            fieldStyle.normal.background = fieldBg;
            fieldStyle.focused.background = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.28f, 1f));
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
                Texture2D tex = MakeTex(1, 1, bgColor);
                GUIStyle s = new GUIStyle(GUI.skin.button);
                s.normal.background = tex;
                s.hover.background = MakeTex(1, 1, bgColor * 1.15f);
                s.active.background = MakeTex(1, 1, bgColor * 0.85f);
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

            // 노란색 (Google)
            btnYellowStyle = BaseBtnStyle(new Color(0.75f, 0.65f, 0.1f, 1f));
            btnYellowStyle.fontSize = 28;
            btnYellowStyle.normal.textColor = Color.white;

            // 갈색 (카카오)
            btnBrownStyle = BaseBtnStyle(new Color(0.55f, 0.35f, 0.1f, 1f));
            btnBrownStyle.fontSize = 28;

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
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}
