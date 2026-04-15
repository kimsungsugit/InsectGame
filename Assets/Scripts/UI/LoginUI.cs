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
            }
        }

        private void OnDisable()
        {
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.LoginCompleted -= OnLoginCompleted;
                AuthManager.Instance.RegisterCompleted -= OnRegisterCompleted;
            }
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
            float pw = Mathf.Min(720f, Screen.width * 0.85f);
            float ph = Mathf.Min(820f, Screen.height * 0.9f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            float cx = px + 60f;
            float cy = py + 40f;
            float fieldW = pw - 120f;
            float fieldH = 44f;
            float btnH = 54f;

            // 타이틀
            GUIStyle bigTitle = new GUIStyle(titleStyle) { fontSize = 52 };
            GUI.Label(new Rect(px, cy, pw, 64f), "곤충탐험 온라인", bigTitle);
            cy += 80f;

            // 이메일
            GUIStyle bigLabel = new GUIStyle(labelStyle) { fontSize = 22 };
            GUI.Label(new Rect(cx, cy, 120f, 30f), "이메일:", bigLabel);
            cy += 32f;
            GUIStyle bigField = new GUIStyle(fieldStyle) { fontSize = 22 };
            bigField.fixedHeight = fieldH;
            emailInput = GUI.TextField(new Rect(cx, cy, fieldW, fieldH), emailInput, 128, bigField);
            cy += fieldH + 14f;

            // 비밀번호
            GUI.Label(new Rect(cx, cy, 120f, 30f), "비밀번호:", bigLabel);
            cy += 32f;
            passwordInput = GUI.PasswordField(new Rect(cx, cy, fieldW, fieldH), passwordInput, '*', 64, bigField);
            cy += fieldH + 20f;

            // 로그인 버튼
            GUIStyle bigBtn = new GUIStyle(btnGreenStyle) { fontSize = 26 };
            GUI.enabled = !isProcessing;
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH), isProcessing ? "로딩 중..." : "로그인", bigBtn))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginWithEmail(emailInput, passwordInput);
                }
            }
            cy += btnH + 12f;

            // 회원가입 버튼
            GUIStyle bigBtnBlue = new GUIStyle(btnBlueStyle) { fontSize = 22 };
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH - 6), "이메일 회원가입", bigBtnBlue))
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
            cy += btnH + 8f;

            // 구분선
            GUIStyle bigSep = new GUIStyle(separatorStyle) { fontSize = 20 };
            GUI.Label(new Rect(px, cy, pw, 28f), "───── 또는 ─────", bigSep);
            cy += 38f;

            // 소셜 로그인
            float socialBtnH = 48f;
            GUI.enabled = !isProcessing;
            GUIStyle bigYellow = new GUIStyle(btnYellowStyle) { fontSize = 22 };
            if (GUI.Button(new Rect(cx, cy, fieldW, socialBtnH), "Google로 로그인", bigYellow))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginWithGoogle();
                }
            }
            cy += socialBtnH + 8f;

            GUIStyle bigBrown = new GUIStyle(btnBrownStyle) { fontSize = 22 };
            if (GUI.Button(new Rect(cx, cy, fieldW, socialBtnH), "카카오로 로그인", bigBrown))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginWithKakao();
                }
            }
            cy += socialBtnH + 8f;

            GUIStyle bigGray = new GUIStyle(btnGrayStyle) { fontSize = 22 };
            if (GUI.Button(new Rect(cx, cy, fieldW, socialBtnH), "게스트로 시작", bigGray))
            {
                if (!isProcessing && AuthManager.Instance != null)
                {
                    isProcessing = true;
                    AuthManager.Instance.LoginAsGuest();
                }
            }
            GUI.enabled = true;
            cy += socialBtnH + 14f;

            // 에러 메시지
            if (errorTimer > 0 && !string.IsNullOrEmpty(errorMessage))
            {
                GUIStyle bigError = new GUIStyle(errorStyle) { fontSize = 20 };
                GUI.Label(new Rect(cx, cy, fieldW, 36f), errorMessage, bigError);
            }
        }

        // ── 회원가입 패널 ──

        private void DrawRegisterPanel()
        {
            float pw = Mathf.Min(720f, Screen.width * 0.85f);
            float ph = Mathf.Min(720f, Screen.height * 0.85f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            float cx = px + 60f;
            float cy = py + 40f;
            float fieldW = pw - 120f;
            float fieldH = 44f;

            // 타이틀
            GUIStyle bigSub = new GUIStyle(subtitleStyle) { fontSize = 38 };
            GUI.Label(new Rect(px, cy, pw, 50f), "회원가입", bigSub);
            cy += 60f;

            GUIStyle bigLabel = new GUIStyle(labelStyle) { fontSize = 22 };
            GUIStyle bigField = new GUIStyle(fieldStyle) { fontSize = 22 };
            bigField.fixedHeight = fieldH;

            // 이메일
            GUI.Label(new Rect(cx, cy, 120f, 30f), "이메일:", bigLabel);
            cy += 32f;
            emailInput = GUI.TextField(new Rect(cx, cy, fieldW, fieldH), emailInput, 128, bigField);
            cy += 38f;

            // 비밀번호
            GUI.Label(new Rect(cx, cy, 100f, 24f), "비밀번호:", labelStyle);
            cy += 26f;
            passwordInput = GUI.PasswordField(new Rect(cx, cy, fieldW, fieldH), passwordInput, '*', 64, bigField);
            cy += fieldH + 14f;

            // 비밀번호 확인
            GUI.Label(new Rect(cx, cy, 160f, 30f), "비밀번호 확인:", bigLabel);
            cy += 32f;
            confirmPasswordInput = GUI.PasswordField(new Rect(cx, cy, fieldW, fieldH), confirmPasswordInput, '*', 64, bigField);
            cy += fieldH + 14f;

            // 닉네임
            GUI.Label(new Rect(cx, cy, 120f, 30f), "닉네임:", bigLabel);
            cy += 32f;
            nicknameInput = GUI.TextField(new Rect(cx, cy, fieldW, fieldH), nicknameInput, 20, bigField);
            cy += fieldH + 20f;

            // 가입하기 버튼
            float btnH = 54f;
            GUIStyle bigBtnGreen = new GUIStyle(btnGreenStyle) { fontSize = 26 };
            GUI.enabled = !isProcessing;
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH), isProcessing ? "로딩 중..." : "가입하기", bigBtnGreen))
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
            cy += btnH + 12f;

            // 뒤로가기 버튼
            GUIStyle bigBtnGray = new GUIStyle(btnGrayStyle) { fontSize = 22 };
            if (GUI.Button(new Rect(cx, cy, fieldW, btnH - 6), "뒤로가기", bigBtnGray))
            {
                if (!isProcessing)
                {
                    errorMessage = "";
                    phase = LoginPhase.Login;
                }
            }
            GUI.enabled = true;
            cy += btnH + 8f;

            // 에러 메시지
            if (errorTimer > 0 && !string.IsNullOrEmpty(errorMessage))
            {
                GUIStyle bigError = new GUIStyle(errorStyle) { fontSize = 20 };
                GUI.Label(new Rect(cx, cy, fieldW, 36f), errorMessage, bigError);
            }
        }

        // ── 로딩 패널 ──

        private void DrawLoadingPanel()
        {
            float pw = 300f;
            float ph = 200f;
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            GUI.Label(new Rect(px, py + 30f, pw, 40f), "로딩 중...", subtitleStyle);

            // 회전하는 곤충 아이콘 (sin/cos 원형 이동 점)
            loadingAngle += Time.deltaTime * 3f;
            float centerX = px + pw * 0.5f;
            float centerY = py + ph * 0.6f;
            float radius = 25f;

            Texture2D dotTex = MakeTex(1, 1, new Color(0.4f, 0.8f, 0.3f, 1f));
            for (int i = 0; i < 6; i++)
            {
                float angle = loadingAngle + i * Mathf.PI * 2f / 6f;
                float dotX = centerX + Mathf.Cos(angle) * radius - 4f;
                float dotY = centerY + Mathf.Sin(angle) * radius - 4f;
                float alpha = 0.3f + 0.7f * ((i + 1f) / 6f);
                Texture2D fadeTex = MakeTex(1, 1, new Color(0.4f, 0.8f, 0.3f, alpha));
                GUI.DrawTexture(new Rect(dotX, dotY, 8f, 8f), fadeTex);
            }
        }

        // ── 캐릭터 생성 패널 ──

        private void DrawCharacterCreatePanel()
        {
            float pw = Mathf.Min(750f, Screen.width * 0.88f);
            float ph = Mathf.Min(1050f, Screen.height * 0.95f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            float cx = px + 50f;
            float cy = py + 35f;
            float fieldW = pw - 100f;

            // 타이틀
            GUI.Label(new Rect(px, cy, pw, 40f), "캐릭터 생성", subtitleStyle);
            cy += 50f;

            // 캐릭터 미리보기 (간단한 사각형 조합 실루엣)
            DrawCharacterPreview(px + pw * 0.5f - 40f, cy, 80f, 140f);
            cy += 155f;

            // 이름
            GUI.Label(new Rect(cx, cy, 60f, 24f), "이름:", labelStyle);
            characterName = GUI.TextField(new Rect(cx + 65f, cy, fieldW - 65f, 28f), characterName, 12, fieldStyle);
            cy += 40f;

            // 성별
            string[] genderLabels = { "남자", "여자" };
            GUI.Label(new Rect(cx, cy, 60f, 24f), "성별:", sectionLabelStyle);
            cy += 28f;
            selectedGender = DrawRadioRow(cx, cy, fieldW, genderLabels, selectedGender);
            cy += 36f;

            // 피부색
            string[] skinLabels = { "밝은", "보통", "어두운", "진한" };
            GUI.Label(new Rect(cx, cy, 60f, 24f), "피부색:", sectionLabelStyle);
            cy += 28f;
            selectedSkinColor = DrawRadioRow(cx, cy, fieldW, skinLabels, selectedSkinColor);
            cy += 36f;

            // 머리 스타일
            string[] hairLabels = { "짧은", "중간", "긴", "올림" };
            GUI.Label(new Rect(cx, cy, 60f, 24f), "머리:", sectionLabelStyle);
            cy += 28f;
            selectedHairStyle = DrawRadioRow(cx, cy, fieldW, hairLabels, selectedHairStyle);
            cy += 36f;

            // 머리 색상
            string[] hairColorLabels = { "검정", "갈색", "금발", "빨강", "보라", "파랑" };
            GUI.Label(new Rect(cx, cy, 60f, 24f), "머리색:", sectionLabelStyle);
            cy += 28f;
            selectedHairColor = DrawRadioRow(cx, cy, fieldW, hairColorLabels, selectedHairColor);
            cy += 36f;

            // 표정
            string[] faceLabels = { "미소", "활짝", "차분", "무표정" };
            GUI.Label(new Rect(cx, cy, 60f, 24f), "표정:", sectionLabelStyle);
            cy += 28f;
            selectedFaceType = DrawRadioRow(cx, cy, fieldW, faceLabels, selectedFaceType);
            cy += 36f;

            // 의상
            string[] outfitLabels = { "탐험가", "연구원", "자유" };
            GUI.Label(new Rect(cx, cy, 60f, 24f), "의상:", sectionLabelStyle);
            cy += 28f;
            selectedOutfit = DrawRadioRow(cx, cy, fieldW, outfitLabels, selectedOutfit);
            cy += 50f;

            // 모험 시작 버튼
            if (GUI.Button(new Rect(cx, cy, fieldW, 46f), "모험 시작!", btnGreenStyle))
            {
                SaveCharacterCreation();
                ApplyCharacterOutfitPreset();
                OnGameReady();
            }
        }

        private int DrawRadioRow(float x, float y, float totalW, string[] labels, int selected)
        {
            float btnW = (totalW - (labels.Length - 1) * 6f) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                float bx = x + i * (btnW + 6f);
                GUIStyle style = (i == selected) ? radioSelectedStyle : radioStyle;
                if (GUI.Button(new Rect(bx, y, btnW, 28f), labels[i], style))
                {
                    selected = i;
                }
            }
            return selected;
        }

        private void DrawCharacterPreview(float x, float y, float w, float h)
        {
            // 피부색
            Color[] skinColors =
            {
                new Color(1.0f, 0.87f, 0.75f),
                new Color(0.9f, 0.75f, 0.6f),
                new Color(0.65f, 0.5f, 0.35f),
                new Color(0.4f, 0.28f, 0.18f)
            };
            Color skin = skinColors[Mathf.Clamp(selectedSkinColor, 0, 3)];

            // 머리색
            Color[] hairColors =
            {
                new Color(0.12f, 0.08f, 0.05f),   // 검정
                new Color(0.35f, 0.2f, 0.1f),     // 갈색
                new Color(0.85f, 0.7f, 0.3f),     // 금발
                new Color(0.6f, 0.15f, 0.1f),     // 빨강
                new Color(0.2f, 0.15f, 0.35f),    // 보라
                new Color(0.15f, 0.3f, 0.5f)      // 파랑
            };
            Color hair = hairColors[Mathf.Clamp(selectedHairColor, 0, hairColors.Length - 1)];

            // 의상색
            Color[] outfitColors =
            {
                new Color(0.16f, 0.32f, 0.72f),  // 탐험가: 파랑
                new Color(0.9f, 0.9f, 0.92f),     // 연구원: 흰
                new Color(0.6f, 0.2f, 0.2f)       // 자유: 빨강
            };
            Color outfit = outfitColors[Mathf.Clamp(selectedOutfit, 0, 2)];

            // 머리 (머리카락 또는 모자)
            Texture2D hairTex = MakeTex(1, 1, hair);
            if (selectedHairStyle == 3)
            {
                // 올림 머리
                Texture2D hatTex = MakeTex(1, 1, hair);
                // 베이스 볼륨
                GUI.DrawTexture(new Rect(x + 2f, y - 6f, w - 4f, 20f), hatTex);
                if (selectedGender == 0)
                {
                    // 남자: 뾰족하게 위로
                    GUI.DrawTexture(new Rect(x + w * 0.3f, y - 16f, w * 0.15f, 14f), hatTex);
                    GUI.DrawTexture(new Rect(x + w * 0.55f, y - 12f, w * 0.12f, 10f), hatTex);
                }
                else
                {
                    // 여자: 번 (둥근 볼륨)
                    GUI.DrawTexture(new Rect(x + w * 0.25f, y - 18f, w * 0.5f, 16f), hatTex);
                    GUI.DrawTexture(new Rect(x + w * 0.3f, y - 22f, w * 0.4f, 10f), hatTex);
                }
            }
            else if (selectedHairStyle == 0)
            {
                // 짧은 머리: 상단만
                GUI.DrawTexture(new Rect(x + 5f, y, w - 10f, 14f), hairTex);
            }
            else if (selectedHairStyle == 1)
            {
                // 중간 머리: 상단 + 옆으로 약간 내려옴
                GUI.DrawTexture(new Rect(x + 5f, y, w - 10f, 16f), hairTex);
                GUI.DrawTexture(new Rect(x + 2f, y + 10f, 8f, 18f), hairTex);
                GUI.DrawTexture(new Rect(x + w - 10f, y + 10f, 8f, 18f), hairTex);
            }
            else
            {
                // 긴 머리 (style 2): 어깨까지
                GUI.DrawTexture(new Rect(x + 5f, y, w - 10f, 18f), hairTex);
                GUI.DrawTexture(new Rect(x, y + 10f, 10f, 42f), hairTex);
                GUI.DrawTexture(new Rect(x + w - 10f, y + 10f, 10f, 42f), hairTex);
            }

            // 얼굴
            Texture2D skinTex = MakeTex(1, 1, skin);
            GUI.DrawTexture(new Rect(x + 10f, y + 10f, w - 20f, 35f), skinTex);

            // 눈
            Texture2D whiteTex = MakeTex(1, 1, Color.white);
            Texture2D blackTex = MakeTex(1, 1, new Color(0.1f, 0.08f, 0.05f));
            float eyeY = y + 18f;
            GUI.DrawTexture(new Rect(x + 18f, eyeY, 10f, 10f), whiteTex);       // 왼눈
            GUI.DrawTexture(new Rect(x + w - 28f, eyeY, 10f, 10f), whiteTex);    // 오른눈
            GUI.DrawTexture(new Rect(x + 21f, eyeY + 3f, 4f, 4f), blackTex);     // 왼동공
            GUI.DrawTexture(new Rect(x + w - 25f, eyeY + 3f, 4f, 4f), blackTex); // 오른동공

            // 눈썹
            Texture2D browTex = MakeTex(1, 1, new Color(hair.r * 0.6f, hair.g * 0.6f, hair.b * 0.6f));
            GUI.DrawTexture(new Rect(x + 16f, eyeY - 4f, 14f, 2f), browTex);
            GUI.DrawTexture(new Rect(x + w - 30f, eyeY - 4f, 14f, 2f), browTex);

            // 코
            GUI.DrawTexture(new Rect(x + w * 0.5f - 3f, y + 28f, 6f, 5f), skinTex);

            // 입
            Texture2D mouthTex = MakeTex(1, 1, new Color(0.8f, 0.4f, 0.35f));
            float mouthW = 8f + selectedFaceType * 2f;
            GUI.DrawTexture(new Rect(x + w * 0.5f - mouthW * 0.5f, y + 35f, mouthW, 3f), mouthTex);

            // 볼터치 (여자)
            if (selectedGender == 1)
            {
                Texture2D blushTex = MakeTex(1, 1, new Color(1f, 0.6f, 0.6f, 0.5f));
                GUI.DrawTexture(new Rect(x + 12f, y + 30f, 8f, 5f), blushTex);
                GUI.DrawTexture(new Rect(x + w - 20f, y + 30f, 8f, 5f), blushTex);
            }

            // 몸통 (의상) — 여자는 약간 가늘게
            float bodyInset = (selectedGender == 1) ? 12f : 8f;
            Texture2D outfitTex = MakeTex(1, 1, outfit);
            GUI.DrawTexture(new Rect(x + bodyInset, y + 48f, w - bodyInset * 2f, 50f), outfitTex);

            // 팔
            GUI.DrawTexture(new Rect(x - 6f, y + 48f, 14f, 40f), outfitTex);
            GUI.DrawTexture(new Rect(x + w - 8f, y + 48f, 14f, 40f), outfitTex);

            // 다리
            Color pantsColor = new Color(0.25f, 0.25f, 0.28f);
            Texture2D pantsTex = MakeTex(1, 1, pantsColor);
            GUI.DrawTexture(new Rect(x + 12f, y + 100f, 20f, 35f), pantsTex);
            GUI.DrawTexture(new Rect(x + w - 32f, y + 100f, 20f, 35f), pantsTex);
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
            }
            else
            {
                phase = LoginPhase.CharacterCreate;
            }
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

            // 타이틀: 금색 44px Bold
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 44;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(1f, 0.84f, 0f, 1f);
            titleStyle.alignment = TextAnchor.MiddleCenter;

            // 서브타이틀
            subtitleStyle = new GUIStyle(GUI.skin.label);
            subtitleStyle.fontSize = 28;
            subtitleStyle.fontStyle = FontStyle.Bold;
            subtitleStyle.normal.textColor = Color.white;
            subtitleStyle.alignment = TextAnchor.MiddleCenter;

            // 입력 필드
            fieldStyle = new GUIStyle(GUI.skin.textField);
            fieldStyle.fontSize = 16;
            fieldStyle.normal.textColor = Color.white;
            Texture2D fieldBg = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.2f, 1f));
            fieldStyle.normal.background = fieldBg;
            fieldStyle.focused.background = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.28f, 1f));
            fieldStyle.focused.textColor = Color.white;
            fieldStyle.padding = new RectOffset(8, 8, 4, 4);

            // 라벨
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 15;
            labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f, 1f);

            // 에러
            errorStyle = new GUIStyle(GUI.skin.label);
            errorStyle.fontSize = 14;
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
                s.fontSize = 18;
                s.fontStyle = FontStyle.Bold;
                s.alignment = TextAnchor.MiddleCenter;
                return s;
            }

            // 녹색 (로그인)
            btnGreenStyle = BaseBtnStyle(new Color(0.15f, 0.55f, 0.15f, 1f));

            // 파란색 (회원가입)
            btnBlueStyle = BaseBtnStyle(new Color(0.2f, 0.35f, 0.7f, 1f));
            btnBlueStyle.fontSize = 16;

            // 노란색 (Google)
            btnYellowStyle = BaseBtnStyle(new Color(0.75f, 0.65f, 0.1f, 1f));
            btnYellowStyle.fontSize = 15;
            btnYellowStyle.normal.textColor = Color.white;

            // 갈색 (카카오)
            btnBrownStyle = BaseBtnStyle(new Color(0.55f, 0.35f, 0.1f, 1f));
            btnBrownStyle.fontSize = 15;

            // 회색 (게스트)
            btnGrayStyle = BaseBtnStyle(new Color(0.35f, 0.35f, 0.38f, 1f));
            btnGrayStyle.fontSize = 15;

            // 구분선
            separatorStyle = new GUIStyle(GUI.skin.label);
            separatorStyle.fontSize = 14;
            separatorStyle.normal.textColor = new Color(0.5f, 0.5f, 0.55f, 1f);
            separatorStyle.alignment = TextAnchor.MiddleCenter;

            // 라디오 버튼
            radioStyle = BaseBtnStyle(new Color(0.25f, 0.25f, 0.3f, 1f));
            radioStyle.fontSize = 14;
            radioStyle.fontStyle = FontStyle.Normal;

            radioSelectedStyle = BaseBtnStyle(new Color(0.2f, 0.5f, 0.8f, 1f));
            radioSelectedStyle.fontSize = 14;
            radioSelectedStyle.fontStyle = FontStyle.Bold;

            // 섹션 라벨
            sectionLabelStyle = new GUIStyle(GUI.skin.label);
            sectionLabelStyle.fontSize = 15;
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
