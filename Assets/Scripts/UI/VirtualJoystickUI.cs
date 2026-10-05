using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 모바일 터치 이동용 가상 조이스틱(플로팅). 화면 좌하단 영역을 누르면 그 지점에 베이스가 뜨고
    /// 손가락 방향/거리로 이동 입력을 만든다. PlayerMovement.SetMoveInput으로 푸시(UI→Core).
    ///
    /// - 멀티터치: fingerId로 조이스틱 손가락을 추적(다른 손가락 탭/메뉴와 독립).
    /// - 모달 열림/프리즈 시 비활성(이동 차단). 에디터에선 마우스로도 동작(테스트).
    /// - 아날로그: 부분 기울임=부분 속도(PlayerMovement에서 크기 보존).
    /// - 필드 HUD(<see cref="FieldHudInput"/>에 등록된 자리) 위에서는 시작하지 않는다(유휴 힌트 원 안은 예외) — <see cref="CanBeginAt"/>.
    /// - <b>모바일 배치에서만 켠다</b>(<see cref="UIScale.IsMobileLayout"/>). 데스크톱 배치는 좌하단에 퀘스트 칩이 서고(조이스틱 자리를
    ///   비워 두는 건 모바일 배치뿐이다) 이동은 키보드·클릭으로 한다 — 예전엔 데스크톱에서도 안내 원이 퀘스트 칩 위에 그려지고
    ///   마우스로 좌하단을 누르면 조이스틱이 켜졌다.
    /// </summary>
    public class VirtualJoystickUI : MonoBehaviour
    {
        private PlayerMovement player;

        private int activeFinger = -1;   // -1=없음, -2=마우스(에디터), >=0=touch fingerId
        private Vector2 originScreen;    // Y-up 스크린 좌표(베이스 중심)
        private Vector2 knobScreen;      // Y-up 스크린 좌표(노브, 반경 클램프)
        private bool active;

        private Texture2D baseTex, knobTex;

        private float BaseRadius => HintRadius(Screen.width, Screen.height);

        public void AutoWire(PlayerMovement pm)
        {
            if (player == null) player = pm;
        }

        private void Update()
        {
            if (player == null) player = FindFirstObjectByType<PlayerMovement>();

            // 모달/프리즈 중엔 조이스틱 비활성(이동 차단) — 메뉴 조작과 충돌 방지. 데스크톱 배치에서는 아예 쓰지 않는다.
            bool blocked = !UIScale.IsMobileLayout || ModalUIRegistry.IsAnyOpen() || (player != null && player.IsFrozen);
            if (blocked)
            {
                Deactivate();
                return;
            }

            if (!active) TryBegin();
            else UpdateActive();

            if (player == null) return;
            if (active)
            {
                Vector2 delta = knobScreen - originScreen;
                player.SetMoveInput(delta / BaseRadius, true); // -1..1
            }
            else
            {
                player.SetMoveInput(Vector2.zero, false);
            }
        }

        // 좌하단 사분면만 활성화 영역 — 상단 HUD/퀘스트, 중앙 하단 퀵바와 충돌 회피.
        // 좌/하단 세이프 에어리어(노치/제스처바)는 제외 — OS 제스처에 먹히는 데드존에서 시작 방지.
        private bool InZone(Vector2 p)
        {
            return CanBeginAt(p, Screen.width, Screen.height, SafeArea.Left, SafeArea.Bottom,
                FieldHudInput.IsScreenPointOverHud(p));
        }

        /// <summary>
        /// 이 화면 점(Y-up 픽셀)에서 조이스틱을 <b>시작</b>해도 되는가 — 좌하단 사분면(왼쪽·아래 세이프 에어리어 제외) 안이고
        /// <b>필드 HUD 위가 아닐 것</b>(<paramref name="overHud"/> = <see cref="FieldHudInput.IsScreenPointOverHud"/>).
        /// 단 <b>유휴 힌트 원 안은 HUD가 있어도 시작한다</b> — "여기를 누르면 움직인다"고 그려 둔 자리다.
        ///
        /// HUD 판정을 더한 이유: 사분면에 걸친 HUD 버튼(대화 버튼, 동굴 입구 버튼, 섬 안내 배너)을 누르면 그 버튼과 함께 조이스틱이
        /// 켜져 캐릭터가 움직였다 — 클릭-이동(<c>PlayerMovement</c>)은 이미 같은 등록 목록으로 그 탭을 거르는데 조이스틱만 안 걸렀다.
        /// 힌트 원을 예외로 둔 이유: 잠깐 뜨는 안내가 힌트 자리를 덮더라도 거기서는 켜져야 처음 걷는 사람에게 조작이 고장 난 것처럼 안 보인다
        /// (2026-10-03 전엔 꿈속 섬 안내 카드가 세로 화면에서 그 자리를 덮었다 — 지금은 잡기 글자 위로 올렸다). 늘 떠 있는 HUD는 힌트 원을
        /// 덮지 않는다(<c>HudOverlapSweepTests</c>가 잰다).
        /// <b>시작만</b> 막는다 — 이미 잡은 조이스틱은 손가락이 HUD 위를 지나가도 놓치지 않는다. 사분면 자체는 줄이지 않는다(입력 데드존).
        /// </summary>
        public static bool CanBeginAt(Vector2 screenPoint, float screenWidth, float screenHeight, float safeLeft, float safeBottom,
            bool overHud)
        {
            bool inQuadrant = screenPoint.x > safeLeft && screenPoint.y > safeBottom
                && screenPoint.x < screenWidth * 0.5f && screenPoint.y < screenHeight * 0.5f;
            if (!inQuadrant) return false;
            if (!overHud) return true;
            float r = HintRadius(screenWidth, screenHeight);
            return (screenPoint - HintCenter(screenWidth, screenHeight, safeLeft, safeBottom)).sqrMagnitude <= r * r;
        }

        /// <summary>베이스 원 반지름(픽셀) — 짧은 변의 14%. 유휴 힌트 원도 같은 크기다.</summary>
        public static float HintRadius(float screenWidth, float screenHeight)
        {
            return Mathf.Min(screenWidth, screenHeight) * 0.14f;
        }

        /// <summary>유휴 힌트 원의 가운데(Y-up 픽셀) — 좌하단 모서리에서 반지름의 1.25배 안쪽(세이프 에어리어 안).</summary>
        public static Vector2 HintCenter(float screenWidth, float screenHeight, float safeLeft, float safeBottom)
        {
            float r = HintRadius(screenWidth, screenHeight);
            return new Vector2(r * 1.25f + safeLeft, r * 1.25f + safeBottom);
        }

        /// <summary>이 화면에서 조이스틱을 쓰는가 — 모바일 배치만.</summary>
        public static bool EnabledFor(HudFrame f) => f.Mobile;

        /// <summary>유휴 힌트 원을 감싸는 사각형(가상 좌표, GUI의 아래로 자라는 y) — 겹침 전수 검사가 읽는다.</summary>
        public static Rect HintRect(HudFrame f)
        {
            float r = HintRadius(f.PixelWidth, f.PixelHeight);
            Vector2 c = HintCenter(f.PixelWidth, f.PixelHeight, f.PixelSafeLeft, f.PixelSafeBottom);
            return f.ToVirtual(new Rect(c.x - r, f.PixelHeight - c.y - r, r * 2f, r * 2f));
        }

        /// <summary>조이스틱을 시작할 수 있는 사분면(가상 좌표) — 왼쪽·아래 세이프 에어리어를 뺀 좌하단 사분면.</summary>
        public static Rect ZoneRect(HudFrame f)
        {
            return f.ToVirtual(new Rect(f.PixelSafeLeft, f.PixelHeight * 0.5f,
                f.PixelWidth * 0.5f - f.PixelSafeLeft, f.PixelHeight * 0.5f - f.PixelSafeBottom));
        }

        // 베이스 원이 화면(세이프 에어리어) 안에 완전히 들어오도록 원점을 클램프 — 가장자리에서 눌러도
        // 노브/베이스가 화면 밖으로 잘려 그려지지 않게 한다. (좌표는 Y-up 스크린 픽셀)
        private Vector2 ClampOriginToSafe(Vector2 p)
        {
            float r = BaseRadius;
            float minX = SafeArea.Left + r;
            float maxX = Screen.width - SafeArea.Right - r;
            float minY = SafeArea.Bottom + r;
            float maxY = Screen.height - SafeArea.Top - r;
            p.x = Mathf.Clamp(p.x, minX, Mathf.Max(minX, maxX));
            p.y = Mathf.Clamp(p.y, minY, Mathf.Max(minY, maxY));
            return p;
        }

        private void TryBegin()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && InZone(t.position))
                {
                    activeFinger = t.fingerId;
                    originScreen = knobScreen = ClampOriginToSafe(t.position);
                    active = true;
                    return;
                }
            }

            // 에디터/PC 마우스(터치 없을 때만) — 테스트 편의.
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
            {
                Vector2 m = Input.mousePosition;
                if (InZone(m))
                {
                    activeFinger = -2;
                    originScreen = knobScreen = ClampOriginToSafe(m);
                    active = true;
                }
            }
        }

        private void UpdateActive()
        {
            Vector2 pos;
            if (activeFinger == -2)
            {
                if (!Input.GetMouseButton(0)) { Deactivate(); return; }
                pos = Input.mousePosition;
            }
            else
            {
                bool found = false;
                pos = knobScreen;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.fingerId != activeFinger) continue;
                    found = true;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { Deactivate(); return; }
                    pos = t.position;
                    break;
                }
                if (!found) { Deactivate(); return; }
            }

            // 반경 클램프(시각/입력 공용)
            Vector2 delta = pos - originScreen;
            float r = BaseRadius;
            if (delta.magnitude > r) delta = delta.normalized * r;
            knobScreen = originScreen + delta;
        }

        private void Deactivate()
        {
            if (active && player != null) player.SetMoveInput(Vector2.zero, false);
            active = false;
            activeFinger = -1;
        }

        private void OnGUI()
        {
            if (!UIScale.IsMobileLayout) return;
            EnsureTex();
            float r = BaseRadius;

            if (active)
            {
                DrawCircle(originScreen, r, baseTex, new Color(1f, 1f, 1f, 0.22f));
                DrawCircle(knobScreen, r * 0.5f, knobTex, new Color(0.6f, 0.85f, 1f, 0.55f));
            }
            else if (!ModalUIRegistry.IsAnyOpen() && (player == null || !player.IsFrozen))
            {
                // 유휴 힌트(좌하단 코너) — 조이스틱 위치 발견성. 세이프 에어리어 안쪽으로.
                // 조이스틱이 꺼진 동안(창이 열렸거나 조작이 묶임)은 그리지 않는다 — 창 위에 비치고, 눌러도 안 움직인다.
                Vector2 hint = HintCenter(Screen.width, Screen.height, SafeArea.Left, SafeArea.Bottom);
                DrawCircle(hint, r, baseTex, new Color(1f, 1f, 1f, 0.10f));
                DrawCircle(hint, r * 0.5f, knobTex, new Color(1f, 1f, 1f, 0.14f));
            }
        }

        private void DrawCircle(Vector2 screenPos, float radius, Texture2D tex, Color col)
        {
            float guiY = Screen.height - screenPos.y; // Y-up → GUI Y-down
            GUI.color = col;
            GUI.DrawTexture(new Rect(screenPos.x - radius, guiY - radius, radius * 2f, radius * 2f), tex);
            GUI.color = Color.white;
        }

        private void EnsureTex()
        {
            if (baseTex == null) baseTex = MakeCircle(96, true);
            if (knobTex == null) knobTex = MakeCircle(64, false);
        }

        // 원형 텍스처 1회 생성. ring=true면 테두리 강조(베이스), false면 꽉 찬 원(노브).
        // 런타임 Texture2D는 씬 재로드로 사라지지 않는다(WorldInteractionController와 같은 계열).
        private void OnDestroy()
        {
            if (baseTex != null) Destroy(baseTex);
            if (knobTex != null) Destroy(knobTex);
            baseTex = null;
            knobTex = null;
        }

        private static Texture2D MakeCircle(int size, bool ring)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0=중심,1=가장자리
                    float a;
                    if (ring) a = d > 0.8f && d <= 1f ? 1f : (d <= 0.8f ? 0.3f : 0f);
                    else a = d <= 1f ? 1f : 0f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            t.Apply();
            return t;
        }
    }
}
