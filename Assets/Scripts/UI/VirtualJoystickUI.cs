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
    /// </summary>
    public class VirtualJoystickUI : MonoBehaviour
    {
        private PlayerMovement player;

        private int activeFinger = -1;   // -1=없음, -2=마우스(에디터), >=0=touch fingerId
        private Vector2 originScreen;    // Y-up 스크린 좌표(베이스 중심)
        private Vector2 knobScreen;      // Y-up 스크린 좌표(노브, 반경 클램프)
        private bool active;

        private Texture2D baseTex, knobTex;

        private float BaseRadius => Mathf.Min(Screen.width, Screen.height) * 0.14f;

        public void AutoWire(PlayerMovement pm)
        {
            if (player == null) player = pm;
        }

        private void Update()
        {
            if (player == null) player = FindFirstObjectByType<PlayerMovement>();

            // 모달/프리즈 중엔 조이스틱 비활성(이동 차단) — 메뉴 조작과 충돌 방지.
            bool blocked = ModalUIRegistry.IsAnyOpen() || (player != null && player.IsFrozen);
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
        private bool InZone(Vector2 p)
        {
            return p.x < Screen.width * 0.5f && p.y < Screen.height * 0.5f;
        }

        private void TryBegin()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && InZone(t.position))
                {
                    activeFinger = t.fingerId;
                    originScreen = knobScreen = t.position;
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
                    originScreen = knobScreen = m;
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
            EnsureTex();
            float r = BaseRadius;

            if (active)
            {
                DrawCircle(originScreen, r, baseTex, new Color(1f, 1f, 1f, 0.22f));
                DrawCircle(knobScreen, r * 0.5f, knobTex, new Color(0.6f, 0.85f, 1f, 0.55f));
            }
            else
            {
                // 유휴 힌트(좌하단 코너) — 조이스틱 위치 발견성. 세이프 에어리어 안쪽으로.
                Vector2 hint = new Vector2(r * 1.25f + SafeArea.Left, r * 1.25f + SafeArea.Bottom);
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
