using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 섬 꾸미기 — 보관함에서 골라 칸에 놓고, 놓인 것을 눌러 옮기기·돌리기·넣기.
    ///
    /// <b>모달이다.</b> 지면을 누르는 탭이 「놓을 자리 고르기」인데, 같은 탭을 <c>PlayerMovement</c>가
    /// 클릭-이동으로도 읽는다(그쪽은 Update에서 마우스를 따로 폴링한다). 모달로 등록하면 이동 입력이 통째로
    /// 막히므로 두 뜻이 겹치지 않는다. 대신 캐릭터가 서 있으니 카메라를 캐릭터에서 떼어 화면을 끌어 옮긴다
    /// (<see cref="IslandWorldBuilder.BeginEditCamera"/>).
    /// </summary>
    public class IslandEditUI : MonoBehaviour, IModalUI
    {
        /// <summary>이만큼(가상 px) 넘게 끌면 탭이 아니라 화면 끌기다.</summary>
        private const float DragThreshold = 14f;
        private const float KeyPanSpeed = 9f;
        private const float FeedbackSeconds = 2.2f;

        [SerializeField] private IslandManager island;
        [SerializeField] private IslandWorldBuilder world;
        [SerializeField] private IslandShareClient share;

        private bool isOpen;
        // 들고 있는 물건 — 보관함에서 꺼낸 새 물건이면 carryIndex가 -1, 놓인 물건을 집었으면 그 인덱스.
        private string carryId;
        private int carryIndex = -1;
        private int carryX;
        private int carryZ;
        private int carryRot;
        private bool carryValid;
        private bool ghostDirty;

        private int trayPage;
        private readonly List<IslandObjectDef> trayItems = new List<IslandObjectDef>();
        private bool trayDirty = true;
        private readonly List<Rect> uiRects = new List<Rect>();

        private bool pressing;
        private bool dragging;
        private Vector2 pressVirtual;
        private Vector2 lastScreen;

        private string feedback;
        private float feedbackUntil;
        // 보관함 칸의 "×3" 문구 — 수량이 바뀔 때만 다시 만든다.
        private readonly Dictionary<string, string> countLabels = new Dictionary<string, string>();
        private readonly Dictionary<string, int> countShown = new Dictionary<string, int>();

        public bool IsOpen => isOpen;

        public void AutoWire(IslandManager islandManager, IslandWorldBuilder worldBuilder, IslandShareClient shareClient)
        {
            if (island == null) island = islandManager;
            if (world == null) world = worldBuilder;
            if (share == null) share = shareClient;
        }

        public void Open()
        {
            if (isOpen || island == null || world == null || world.Mode != IslandMode.Own) return;
            isOpen = true;
            carryId = null;
            carryIndex = -1;
            trayPage = 0;
            trayDirty = true;
            pressing = dragging = false;
            ModalUIRegistry.Register(this);
            world.BeginEditCamera();
            world.SetGridVisible(true);
            island.ReportEditOpened();
        }

        public void CloseModal()
        {
            if (!isOpen) return;
            isOpen = false;
            DropCarry();
            ModalUIRegistry.Unregister(this);
            if (world != null)
            {
                world.SetGridVisible(false);
                world.EndEditCamera();
            }
            // 꾸미기를 마친 모습을 올린다 — 방문자는 마지막으로 올라간 모습을 본다.
            if (share != null) share.Publish();
        }

        /// <summary>검수 캡처 전용 — 보관함의 물건을 지정한 칸·회전으로 든 상태를 만든다.</summary>
        internal void CarryForCapture(string id, int x, int z, int rot)
        {
            DropCarry();
            carryId = id;
            carryIndex = -1;
            carryX = x;
            carryZ = z;
            carryRot = rot;
            ghostDirty = true;
        }

        /// <summary>검수 캡처 전용 — 놓인 물건을 집은 상태를 만든다.</summary>
        internal void PickPlacedForCapture(int index) => PickPlaced(index);

        // UI 루트가 통째로 꺼질 때(오프닝 다시보기)도 카메라와 격자를 되돌려야 한다.
        private void OnDisable() => CloseModal();

        private void Update()
        {
            if (!isOpen) return;
            // 섬을 떠났으면(다른 경로로 강제 종료 포함) 닫는다.
            if (world == null || world.Mode != IslandMode.Own) { CloseModal(); return; }
            if (!ReferenceEquals(ModalUIRegistry.TopModal, this)) return;

            float h = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                      - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float v = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                      - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            if (h != 0f || v != 0f)
                world.PanEditCamera(new Vector3(h, 0f, v) * (KeyPanSpeed * Time.unscaledDeltaTime));

            if (carryId != null)
            {
                if (Input.GetKeyDown(KeyCode.R)) Rotate();
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
            }
        }

        private void OnGUI()
        {
            if (!isOpen || world == null || island == null) return;
            // 위에 다른 모달(상점 등)이 떠 있으면 그리기만 멈춘다 — 입력을 같이 받으면 뒤 화면이 눌린다.
            if (!ReferenceEquals(ModalUIRegistry.TopModal, this)) return;

            UIScale.Begin();
            uiRects.Clear();
            DrawTopBar();
            DrawTray();
            if (carryId != null) DrawCarryActions();
            DrawFeedback();
            UIScale.End();

            // 버튼이 먼저 이벤트를 가져간 뒤 남은 것만 섬 입력으로 읽는다.
            HandleWorldInput();
            RefreshGhost();
        }

        private void DrawTopBar()
        {
            UITheme t = UITheme.Instance;
            bool mobile = UIScale.IsMobileLayout;
            Rect bar = UISafeLayout.TopPanel(Mathf.Min(1100f, UISafeLayout.ContentWidth), 76f);
            uiRects.Add(bar);
            UISurface.HudCard(bar);
            float doneW = 180f;
            IslandUiKit.Label(new Rect(bar.x + 20f, bar.y + 4f, 160f, 68f), "꾸미기", IslandUiKit.Title, t.textPrimary);
            string hint = carryId != null
                ? "놓을 칸을 누르세요. 화면을 끌면 섬을 둘러봅니다."
                : mobile ? "보관함에서 고르거나, 놓인 물건을 누르세요."
                         : "보관함에서 고르거나, 놓인 물건을 누르세요.  [R] 돌리기 · [Enter] 놓기 · 방향키 이동";
            IslandUiKit.Label(new Rect(bar.x + 180f, bar.y + 4f, bar.width - 180f - doneW - 24f, 68f), hint,
                IslandUiKit.Small, t.textSecondary);
            if (UISurface.Button(new Rect(bar.xMax - doneW - 10f, bar.y + 8f, doneW, 60f), "완료",
                    t.accentMint, IslandUiKit.Button))
                CloseModal();
        }

        private void RebuildTray()
        {
            trayItems.Clear();
            IReadOnlyList<IslandObjectDef> all = IslandCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (island.GetStorageCount(all[i].id) > 0) trayItems.Add(all[i]);
            trayDirty = false;
        }

        private void DrawTray()
        {
            if (trayDirty) RebuildTray();
            UITheme t = UITheme.Instance;
            bool mobile = UIScale.IsMobileLayout;
            float trayH = mobile ? 190f : 150f;
            Rect tray = UISafeLayout.BottomPanel(Mathf.Min(1500f, UISafeLayout.ContentWidth), trayH);
            uiRects.Add(tray);
            UISurface.HudCard(tray);

            if (trayItems.Count == 0)
            {
                IslandUiKit.Label(new Rect(tray.x + 20f, tray.y + 10f, tray.width - 40f, trayH - 20f),
                    "보관함이 비었습니다. 섬 [상점]에서 물건을 사 보세요.", IslandUiKit.BodyCenter, t.textSecondary);
                return;
            }

            const float arrowW = 64f;
            float cardW = mobile ? 200f : 190f;
            const float gap = 8f;
            float inner = tray.width - 16f - (arrowW + gap) * 2f;
            int perPage = Mathf.Max(1, Mathf.FloorToInt((inner + gap) / (cardW + gap)));
            int pages = (trayItems.Count + perPage - 1) / perPage;
            trayPage = Mathf.Clamp(trayPage, 0, pages - 1);

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && trayPage > 0;
            if (UISurface.Button(new Rect(tray.x + 8f, tray.y + 8f, arrowW, trayH - 16f), "◀", t.surfaceRaised,
                    IslandUiKit.Button))
                trayPage--;
            GUI.enabled = enabled && trayPage < pages - 1;
            if (UISurface.Button(new Rect(tray.xMax - 8f - arrowW, tray.y + 8f, arrowW, trayH - 16f), "▶",
                    t.surfaceRaised, IslandUiKit.Button))
                trayPage++;
            GUI.enabled = enabled;

            float x = tray.x + 8f + arrowW + gap;
            int start = trayPage * perPage;
            for (int i = start; i < Mathf.Min(start + perPage, trayItems.Count); i++)
            {
                IslandObjectDef def = trayItems[i];
                Rect card = new Rect(x, tray.y + 8f, cardW, trayH - 16f);
                x += cardW + gap;
                bool selected = carryIndex < 0 && carryId == def.id;
                UISurface.Card(card, selected ? t.surfaceRaised : t.surfaceCard,
                    selected ? t.accentMint : t.surfaceBorder);
                UISurface.Flat(new Rect(card.x + UITheme.Radius.Card, card.y + 3f, card.width - UITheme.Radius.Card * 2f, 6f),
                    IslandUiKit.CategoryColor(def.category));
                IslandUiKit.Label(new Rect(card.x + 10f, card.y + 14f, card.width - 20f, 44f), def.displayName,
                    IslandUiKit.BodyCenter, t.textPrimary);
                IslandUiKit.Label(new Rect(card.x + 10f, card.y + 58f, card.width - 20f, 32f),
                    CountLabel(def), IslandUiKit.SmallCenter, t.textSecondary);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) PickFromTray(def);
            }
        }

        private string CountLabel(IslandObjectDef def)
        {
            int count = island.GetStorageCount(def.id);
            if (!countShown.TryGetValue(def.id, out int shown) || shown != count)
            {
                countShown[def.id] = count;
                countLabels[def.id] = def.width + "×" + def.depth + "칸  ·  " + count + "개";
            }
            return countLabels[def.id];
        }

        private void DrawCarryActions()
        {
            UITheme t = UITheme.Instance;
            bool moving = carryIndex >= 0;
            int count = moving ? 4 : 3;
            const float w = 170f;
            const float h = 68f;
            const float gap = 8f;
            bool mobile = UIScale.IsMobileLayout;
            float trayH = mobile ? 190f : 150f;
            float totalW = count * w + (count - 1) * gap + 16f;
            Rect tray = UISafeLayout.BottomPanel(totalW, h + 16f);
            // 보관함 바로 위 — 자리는 보관함 높이에서 파생한다.
            Rect bar = new Rect(tray.x, tray.y - trayH - 10f, tray.width, tray.height);
            uiRects.Add(bar);
            UISurface.HudCard(bar);

            float x = bar.x + 8f;
            Rect Next() { var r = new Rect(x, bar.y + 8f, w, h); x += w + gap; return r; }

            if (UISurface.Button(Next(), "돌리기", t.surfaceRaised, IslandUiKit.Button)) Rotate();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && carryValid;
            if (UISurface.Button(Next(), "놓기", t.accentMint, IslandUiKit.Button)) Confirm();
            GUI.enabled = enabled;
            if (moving && UISurface.Button(Next(), "보관함에 넣기", t.accentAmber, IslandUiKit.ButtonSmall)) StoreCarried();
            if (UISurface.Button(Next(), "취소", t.surfaceCard, IslandUiKit.Button)) DropCarry();
        }

        private void DrawFeedback()
        {
            if (string.IsNullOrEmpty(feedback) || Time.unscaledTime >= feedbackUntil) return;
            Rect r = UISafeLayout.TopPanel(Mathf.Min(720f, UISafeLayout.ContentWidth), 56f);
            r.y += 92f;
            UISurface.HudCard(r);
            IslandUiKit.Label(new Rect(r.x + 16f, r.y + 6f, r.width - 32f, r.height - 12f), feedback,
                IslandUiKit.BodyCenter, UITheme.Instance.accentAmber);
        }

        private void ShowFeedback(string message)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTime + FeedbackSeconds;
        }

        // ── 섬 입력: 탭 = 칸 고르기, 끌기 = 화면 옮기기 ──

        private void HandleWorldInput()
        {
            Event e = Event.current;
            if (e == null) return;
            Vector2 virtualPos = UIScale.VirtualMousePosition;
            Vector2 screenPos = Input.mousePosition;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0 || OverUi(virtualPos)) return;
                    pressing = true;
                    dragging = false;
                    pressVirtual = virtualPos;
                    lastScreen = screenPos;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!pressing) return;
                    if (!dragging && (virtualPos - pressVirtual).sqrMagnitude > DragThreshold * DragThreshold)
                        dragging = true;
                    if (dragging)
                    {
                        // 손가락 밑의 땅이 손가락을 따라오게 — 앞뒤 화면 좌표가 가리키는 지면 점의 차이만큼 옮긴다.
                        if (GroundPoint(lastScreen, out Vector3 before) && GroundPoint(screenPos, out Vector3 after))
                            world.PanEditCamera(before - after);
                        lastScreen = screenPos;
                    }
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (!pressing) return;
                    pressing = false;
                    if (!dragging && !OverUi(virtualPos) && world.ScreenToCell(screenPos, out int cx, out int cz))
                        OnCellTapped(cx, cz);
                    dragging = false;
                    e.Use();
                    break;
            }
        }

        private bool OverUi(Vector2 virtualPos)
        {
            for (int i = 0; i < uiRects.Count; i++)
                if (uiRects[i].Contains(virtualPos)) return true;
            // 이 화면 위에 다른 컴포넌트가 그리는 것(가이드 배너)도 피한다. OnGUI 호출 순서는 정해져 있지 않아,
            // 이쪽이 먼저 돌면 배너 버튼을 누른 탭을 여기서 먹어 버린다. 배너는 자기 자리를 FieldHudInput에 등록한다.
            return FieldHudInput.IsScreenPointOverHud(Input.mousePosition);
        }

        private static bool GroundPoint(Vector2 screenPos, out Vector3 point)
        {
            point = Vector3.zero;
            Camera cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, IslandWorldBuilder.Origin);
            if (!plane.Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        private void OnCellTapped(int cellX, int cellZ)
        {
            if (carryId == null)
            {
                // 아무것도 안 들었다 — 누른 칸의 물건을 집는다.
                int index = IslandGrid.FindAt(island.Placed, cellX, cellZ);
                if (index >= 0) PickPlaced(index);
                return;
            }
            MoveCarryTo(cellX, cellZ);
        }

        private void PickFromTray(IslandObjectDef def)
        {
            DropCarry();
            carryId = def.id;
            carryIndex = -1;
            carryRot = 0;
            // 화면 가운데가 가리키는 칸에서 시작한다 — 고르자마자 어디 놓일지 보이게.
            if (!world.ScreenToCell(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out int cx, out int cz))
                cx = cz = 0;
            MoveCarryTo(cx, cz);
        }

        private void PickPlaced(int index)
        {
            IslandPlacedRecord p = island.Placed[index];
            if (IslandCatalog.Get(p.id) == null) return;
            DropCarry();
            carryId = p.id;
            carryIndex = index;
            carryX = p.x;
            carryZ = p.z;
            carryRot = p.rot;
            world.SetObjectHidden(index);
            ghostDirty = true;
        }

        // 누른 칸이 물건의 가운데쯤 오도록 최소 모서리를 잡는다(큰 건물을 누른 칸의 오른쪽 위로만 뻗게 두면 어색하다).
        private void MoveCarryTo(int cellX, int cellZ)
        {
            IslandObjectDef def = IslandCatalog.Get(carryId);
            if (def == null) return;
            IslandGrid.Footprint(def, carryRot, out int w, out int d);
            carryX = cellX - w / 2;
            carryZ = cellZ - d / 2;
            ghostDirty = true;
        }

        private void Rotate()
        {
            if (carryId == null) return;
            IslandObjectDef def = IslandCatalog.Get(carryId);
            if (def == null) return;
            // 중심 칸을 유지한 채 돈다 — 최소 모서리를 그대로 두면 긴 물건이 돌 때마다 옆으로 밀려난다.
            IslandGrid.Footprint(def, carryRot, out int w, out int d);
            int centerX = carryX + w / 2;
            int centerZ = carryZ + d / 2;
            carryRot = (carryRot + 1) % 4;
            MoveCarryTo(centerX, centerZ);
        }

        private void Confirm()
        {
            if (carryId == null) return;
            IslandPlaceResult result = carryIndex >= 0
                ? island.TryMove(carryIndex, carryX, carryZ, carryRot)
                : island.TryPlace(carryId, carryX, carryZ, carryRot);
            if (result != IslandPlaceResult.Ok)
            {
                ShowFeedback(IslandUiKit.PlaceResultText(result));
                return;
            }
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.Equip);

            string placedId = carryId;
            bool wasNew = carryIndex < 0;
            // 배치가 바뀌면 월드가 물건을 다시 지어 숨김 표시도 풀린다 — 들고 있던 상태만 비운다.
            carryId = null;
            carryIndex = -1;
            world.HideGhost();
            trayDirty = true;

            // 같은 물건이 보관함에 더 있으면 이어서 놓을 수 있게 다시 든다(울타리·화분을 여러 개 놓을 때).
            if (wasNew && island.GetStorageCount(placedId) > 0)
            {
                carryId = placedId;
                ghostDirty = true;
            }
        }

        private void StoreCarried()
        {
            if (carryIndex < 0) return;
            int index = carryIndex;
            carryId = null;
            carryIndex = -1;
            world.HideGhost();
            island.Store(index);
            trayDirty = true;
            ShowFeedback("보관함에 넣었습니다");
        }

        private void DropCarry()
        {
            carryId = null;
            carryIndex = -1;
            if (world != null)
            {
                world.SetObjectHidden(-1);
                world.HideGhost();
            }
        }

        private void RefreshGhost()
        {
            if (carryId == null || !ghostDirty) return;
            ghostDirty = false;
            carryValid = island.CanPlace(carryId, carryX, carryZ, carryRot, carryIndex) == IslandPlaceResult.Ok;
            world.ShowGhost(carryId, carryX, carryZ, carryRot, carryValid);
        }
    }
}
