using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 의상 화면의 3D 캐릭터 그림 담당. 마네킹(<see cref="PlayerVisualBuilder.BuildForPreview"/>)을
    /// 전용 리그에서 RenderTexture로 찍어 OnGUI가 합성한다.
    ///
    /// 두 경로가 한 리그를 공유한다.
    /// - <see cref="GetPreview"/>: 큰 패널. 단일 RT를 재사용하며 드래그 각도·로드아웃이 바뀔 때만 재렌더.
    /// - <see cref="GetThumbnail"/>: 카드 아이콘. 종류별 소형 RT를 LRU로 들고 있는다.
    ///
    /// <b>렌더는 Update에서만 한다</b> — OnGUI 도중 Camera.Render를 부르면 IMGUI가 깨진다
    /// (InsectModelPreviewRenderer가 같은 이유로 같은 구조다).
    ///
    /// 리그는 곤충 프리뷰와 <b>레이어·원점을 반드시 분리</b>한다. 같은 레이어를 쓰면 두 카메라가
    /// 서로의 모델을 찍고 두 광원이 겹쳐 도감 조명이 두 배가 된다.
    /// </summary>
    public class CharacterModelPreviewRenderer : MonoBehaviour
    {
        private const int PreviewLayer = 29;   // 곤충 프리뷰 30 / SubAreaWorldBuilder 31
        private static readonly Vector3 RigOrigin = new Vector3(0f, -5200f, 0f);  // 곤충 리그는 -5000
        private const float FrameFill = 0.88f;

        private const int PreviewW = 512;
        private const int PreviewH = 768;   // 캐릭터는 세로로 길다
        private const int ThumbSize = 160;  // 카드 프리뷰 영역이 가상좌표 100px
        private const int ThumbCacheMax = 24;   // 24 × 160² × 4B ≈ 2.5MB. 한 슬롯 최대 16장 + 탭 전환 여유

        // ── 리그 ──
        private Camera previewCam;
        private bool rigBuilt;

        // ── 마네킹 (풀링: 외형이 바뀔 때만 다시 짓는다) ──
        private GameObject mannequin;
        private int mannequinLookHash = int.MinValue;
        /// <summary>true일 때만 PlayerPrefs에서 외형을 다시 읽는다. <see cref="InvalidatePreview"/>가 세운다.</summary>
        private bool appearanceDirty = true;

        /// <summary>
        /// 설정되면 PlayerPrefs 대신 이 외형으로 마네킹을 짓는다.
        /// 캐릭터 생성 화면 전용 — 그 화면은 저장하기 <b>전에</b> 결과를 보여줘야 한다.
        /// </summary>
        private AppearanceSpec? appearanceOverride;

        // ── 큰 패널 ──
        private RenderTexture currentRT;
        private OutfitLoadout requestLoadout;
        private float requestAngle;
        private int shownLoadoutHash = int.MinValue;
        private float shownAngle = float.NaN;
        /// <summary>마네킹이 <b>지금 입고 있는</b> 조합. RT에 찍힌 것(<see cref="shownLoadoutHash"/>)과는 별개다.</summary>
        private int appliedLoadoutHash = int.MinValue;

        // ── 카드 썸네일 ──
        private readonly Dictionary<ThumbId, RenderTexture> thumbs = new Dictionary<ThumbId, RenderTexture>();
        private readonly List<ThumbId> thumbOrder = new List<ThumbId>();     // 앞이 가장 오래됨(LRU)
        private readonly List<OutfitSlot> queueSlots = new List<OutfitSlot>();
        private readonly List<string> queueIds = new List<string>();
        private readonly OutfitLoadout soloLoadout = new OutfitLoadout();    // 썸네일용 재사용 버퍼

        // 전신 경계 계산용 재사용 버퍼 — GetComponentsInChildren의 배열 반환판을 쓰면
        // 렌더할 때마다 60여 개짜리 배열이 새로 난다(드래그 중엔 매 프레임).
        private readonly List<Renderer> boundsBuffer = new List<Renderer>();

        /// <summary>
        /// 썸네일 캐시 키. <b>문자열이 아니라 구조체다</b> — 호출부(<c>CharacterOutfitUI</c>의 카드 루프)가
        /// 카드마다, 그리고 OnGUI 패스마다 조회하므로 문자열 키였을 땐 그때마다 새 문자열이 났다
        /// (도감·지역맵 라운드가 같은 형태를 P1으로 잡았다).
        ///
        /// <b>외형 해시는 키에 넣지 않는다</b> — 외형이 바뀌면 <see cref="EnsureMannequin"/>이
        /// <see cref="ReleaseThumbs"/>로 캐시를 통째로 비우므로 키가 그걸 또 표현할 필요가 없다.
        /// <b>로드아웃도 넣지 않는다</b> — 넣으면 아이템 하나 장착할 때마다 그리드 전체가 무효화돼
        /// 카드들이 2D로 돌아갔다 차례로 3D로 복귀하는 깜빡임이 난다.
        /// </summary>
        internal readonly struct ThumbId : System.IEquatable<ThumbId>
        {
            public readonly OutfitSlot Slot;
            public readonly string ItemId;

            public ThumbId(OutfitSlot slot, string itemId)
            {
                Slot = slot;
                ItemId = itemId ?? "";
            }

            public bool Equals(ThumbId other)
            {
                return Slot == other.Slot && ItemId == other.ItemId;
            }

            public override bool Equals(object obj)
            {
                return obj is ThumbId other && Equals(other);
            }

            public override int GetHashCode()
            {
                return ((int)Slot * 397) ^ ItemId.GetHashCode();
            }
        }

        // ── 공개 API ──

        /// <summary>
        /// 큰 미리보기. 아직 한 번도 안 그렸으면 null(호출부가 2D 폴백), 그 뒤로는 항상 직전 프레임 그림을
        /// 돌려준다 — 드래그 중에 null을 내면 회전할 때마다 2D로 깜빡인다.
        /// </summary>
        public Texture GetPreview(OutfitLoadout loadout, float yAngle)
        {
            requestLoadout = loadout;
            requestAngle = yAngle;
            return currentRT;
        }

        /// <summary>
        /// 카드 썸네일. 캐시에 있으면 즉시, 없으면 렌더 큐에 넣고 null(호출부는 2D 폴백을 그린다).
        /// 중립 기준(다른 슬롯 미장착)으로 렌더하므로 "이 아이템이 어떻게 생겼나"를 답한다 —
        /// "내 옷과 어울리나"는 큰 패널의 입어보기가 답한다.
        /// </summary>
        public Texture GetThumbnail(OutfitSlot slot, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            ThumbId key = new ThumbId(slot, itemId);   // 구조체 — 조회에 할당이 없다
            if (thumbs.TryGetValue(key, out RenderTexture rt) && rt != null)
            {
                Touch(key);
                return rt;
            }

            // 캡처 람다를 쓰지 않는다 — OnGUI에서 카드마다 불리므로 클로저가 매 패스 할당된다.
            for (int i = 0; i < queueIds.Count; i++)
                if (queueSlots[i] == slot && queueIds[i] == itemId) return null;

            queueSlots.Add(slot);
            queueIds.Add(itemId);
            return null;
        }

        /// <summary>
        /// 의상 화면을 열고 닫을 때 호출한다 — 큰 패널을 다시 그리게 하고,
        /// <b>외형(PlayerPrefs)을 이때만 다시 읽도록</b> 표시한다.
        ///
        /// 외형은 캐릭터 생성 화면에서만 바뀌므로 의상 모달 밖에서만 변한다. 그래서 여기가 유일한
        /// 재확인 지점이면 충분하고, 매 프레임 PlayerPrefs를 두드릴 이유가 없다.
        /// (이 메서드는 한동안 <b>호출부가 0이었다</b> — <c>CharacterOutfitUI.Toggle</c>에 배선했다.)
        /// </summary>
        public void InvalidatePreview()
        {
            shownLoadoutHash = int.MinValue;
            shownAngle = float.NaN;
            appearanceDirty = true;
        }

        /// <summary>
        /// 아직 저장되지 않은 편집 중 외형을 강제한다. <c>null</c>이면 PlayerPrefs로 되돌아간다.
        ///
        /// <b>다 쓰면 반드시 null로 되돌릴 것.</b> 안 그러면 나중에 의상 화면을 열었을 때
        /// 캐릭터 생성 당시 만지던 외형이 계속 보인다 — <see cref="appearanceDirty"/>가
        /// <see cref="InvalidatePreview"/>에서만 서기 때문에 스스로 풀리지 않는다.
        /// (<c>LoginUI</c>는 게임 시작과 <c>OnDisable</c> 양쪽에서 해제한다.)
        /// </summary>
        public void SetAppearanceOverride(AppearanceSpec? spec)
        {
            appearanceOverride = spec;
            appearanceDirty = true;   // EnsureMannequin이 다시 판정하게
        }

        // ── 렌더 루프 ──

        private void Update()
        {
            // 큰 패널이 우선 — 사용자가 보고 있는 그림이 먼저 나와야 한다.
            if (requestLoadout != null)
            {
                int h = requestLoadout.Hash();
                if (h != shownLoadoutHash || !Mathf.Approximately(shownAngle, requestAngle))
                {
                    EnsureRig();
                    EnsureMannequin();
                    if (mannequin != null)
                    {
                        // **각도만 바뀐 프레임엔 옷을 다시 입히지 않는다.** 드래그로 돌리는 동안엔
                        // 각도가 매 프레임 바뀌는데, ApplyToCharacter는 8슬롯 색 적용 + 5슬롯 레시피
                        // 재배치라 그때마다 마네킹 계층을 여러 번 훑는다(OutfitShapeLibrary.FindDeep).
                        // 마네킹이 이미 그 옷을 입고 있으면 카메라만 다시 찍으면 된다.
                        if (appliedLoadoutHash != h) ApplyLoadout(requestLoadout);
                        if (currentRT == null) currentRT = CreateRT(PreviewW, PreviewH);
                        RenderMannequin(currentRT, requestAngle, null);
                        shownLoadoutHash = h;
                        shownAngle = requestAngle;
                    }
                    return;   // 같은 프레임에 썸네일까지 렌더하지 않는다
                }
            }

            RenderOneQueuedThumbnail();
        }

        private void RenderOneQueuedThumbnail()
        {
            if (queueIds.Count == 0) return;

            OutfitSlot slot = queueSlots[0];
            string itemId = queueIds[0];
            queueSlots.RemoveAt(0);
            queueIds.RemoveAt(0);

            EnsureRig();
            EnsureMannequin();
            if (mannequin == null) return;

            ThumbId key = new ThumbId(slot, itemId);
            if (thumbs.ContainsKey(key)) { Touch(key); return; }

            soloLoadout.Clear();
            soloLoadout.Set(slot, itemId);
            ApplyLoadout(soloLoadout);

            // 썸네일은 한 장씩 구워져 캐시에 남는다 — 하필 눈을 감은 프레임에 찍히면 그 카드는
            // 계속 감은 눈으로 보인다. 굽기 직전에 눈을 뜬 상태로 되돌린다.
            CharacterFaceAnimator face = mannequin.GetComponent<CharacterFaceAnimator>();
            if (face != null) face.ResetToNeutral();

            RenderTexture rt = CreateRT(ThumbSize, ThumbSize);
            RenderMannequin(rt, ThumbAngleFor(slot), FocusNodesFor(slot));

            thumbs[key] = rt;
            Touch(key);
            EvictOverflow();

            // 큰 패널이 다음 프레임에 자기 로드아웃으로 되돌리도록 표시 —
            // 마네킹이 방금 중립 조합을 입었기 때문이다.
            shownLoadoutHash = int.MinValue;
        }

        /// <summary>
        /// LRU 갱신 — key를 가장 최근으로 옮긴다. 키 타입이 문자열에서 구조체로 바뀌면서 제네릭이 됐다
        /// (순수 리스트 조작이라 타입과 무관하고, 기존 문자열 테스트도 그대로 돈다).
        /// </summary>
        internal static void TouchKey<T>(List<T> order, T key)
        {
            if (order == null) return;
            int i = order.IndexOf(key);
            if (i >= 0) order.RemoveAt(i);
            order.Add(key);
        }

        /// <summary>상한 초과분을 <b>가장 오래된 것부터</b> 떼어 돌려준다. 반대로 하면 방금 구운 걸 버린다.</summary>
        internal static List<T> EvictKeys<T>(List<T> order, int cap)
        {
            List<T> evicted = new List<T>();
            if (order == null || cap < 0) return evicted;
            while (order.Count > cap)
            {
                evicted.Add(order[0]);
                order.RemoveAt(0);
            }
            return evicted;
        }

        private void Touch(ThumbId key)
        {
            TouchKey(thumbOrder, key);
        }

        private void EvictOverflow()
        {
            List<ThumbId> evicted = EvictKeys(thumbOrder, ThumbCacheMax);
            for (int i = 0; i < evicted.Count; i++)
            {
                if (thumbs.TryGetValue(evicted[i], out RenderTexture rt)) DisposeRT(rt);
                thumbs.Remove(evicted[i]);
            }
        }

        /// <summary>
        /// 렌더 텍스처를 <b>완전히</b> 놓는다.
        ///
        /// <c>Release()</c>는 GPU 리소스만 반환하고 <b>객체 자체는 남긴다</b>(다시 <c>Create()</c>하면
        /// 되살아나는 게 그 설계다). 캐시에서 버리는 텍스처는 다시 쓰지 않으므로 객체까지 파기해야
        /// 한다 — 안 그러면 참조만 끊긴 껍데기가 쌓여 씬 전환(UnloadUnusedAssets) 전까지 남는다.
        /// 축출은 24장 상한을 넘을 때마다, 전체 무효화는 외형을 바꿀 때마다 일어난다.
        /// </summary>
        private void DisposeRT(RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Destroy(rt);
        }

        // ── 리그 / 마네킹 ──

        private void EnsureRig()
        {
            if (rigBuilt) return;

            GameObject camGo = new GameObject("CharacterPreviewCam");
            camGo.transform.SetParent(transform, false);
            camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);   // 살짝 내려다보기
            previewCam = camGo.AddComponent<Camera>();
            previewCam.orthographic = true;
            previewCam.cullingMask = 1 << PreviewLayer;
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f);    // 투명 — 패널 위에 합성
            previewCam.nearClipPlane = 0.05f;
            previewCam.farClipPlane = 30f;
            previewCam.enabled = false;                                 // 수동 Render만

            GameObject lightGo = new GameObject("CharacterPreviewLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(32f, -25f, 0f);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.cullingMask = 1 << PreviewLayer;   // 프리뷰만 비춤(월드 이중조명 방지)
            l.intensity = 1.1f;
            l.color = new Color(1f, 0.98f, 0.94f);

            rigBuilt = true;
        }

        /// <summary>
        /// 마네킹은 <b>하나만 두고 재사용</b>한다. BuildAll이 CreatePrimitive를 60번 가까이 부르므로
        /// 썸네일마다 새로 지으면 확실히 튄다. 외형(성별·머리·얼굴)이 바뀔 때만 다시 짓는다.
        /// </summary>
        private void EnsureMannequin()
        {
            // 외형을 다시 읽어야 할 때만 PlayerPrefs로 내려간다. 예전엔 렌더할 때마다 무조건 읽어서,
            // 드래그로 회전하는 동안 **매 프레임** GetInt 4회 + 키 문자열 4개가 났다
            // (OpeningSceneController 라운드가 "매 프레임 PlayerPrefs 조회"를 P1으로 잡았던 것과 같은 형태).
            if (mannequin != null && !appearanceDirty) return;
            appearanceDirty = false;

            AppearanceSpec spec = appearanceOverride ?? AppearanceSpec.FromPlayerPrefs();
            int h = spec.Hash();
            if (mannequin != null && mannequinLookHash == h) return;

            if (mannequin != null) Destroy(mannequin);
            ReleaseThumbs();   // 외형이 바뀌면 구운 썸네일이 전부 낡는다

            GameObject go = new GameObject("OutfitMannequin");
            go.SetActive(false);   // ← Awake 억제. 활성 상태로 AddComponent하면 PlayerPrefs 외형으로 먼저 지어진다
            go.transform.position = RigOrigin;
            go.AddComponent<PlayerVisualBuilder>().BuildForPreview(spec);
            go.SetActive(true);

            mannequin = go;
            mannequinLookHash = h;
            shownLoadoutHash = int.MinValue;
            appliedLoadoutHash = int.MinValue;   // 새 마네킹은 아무것도 안 입고 있다
        }

        private void ApplyLoadout(OutfitLoadout loadout)
        {
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            if (mgr == null) return;

            // 마네킹이 지금 입고 있는 조합. 큰 패널이 "각도만 바뀐 프레임"을 건너뛰는 판단에 쓴다.
            // 썸네일 경로가 중립 조합(soloLoadout)을 입히면 여기서 그 해시로 바뀌므로,
            // 다음 큰 패널 프레임이 자기 조합으로 자연히 되돌린다.
            appliedLoadoutHash = loadout != null ? loadout.Hash() : int.MinValue;

            mgr.ApplyToCharacter(mannequin, loadout);
            // 레시피의 spawn 파츠는 방금 새로 만들어진 GameObject라 레이어가 0이다 —
            // 여기서 다시 칠하지 않으면 왕관·망토가 프리뷰 카메라에 안 잡힌다.
            SetLayerRecursive(mannequin, PreviewLayer);
        }

        // ── 프레이밍 ──

        private void RenderMannequin(RenderTexture target, float yAngle, string[] focusNodes)
        {
            mannequin.transform.position = RigOrigin;
            mannequin.transform.rotation = Quaternion.Euler(0f, yAngle, 0f);

            Bounds b;
            if (!TryComputeBounds(mannequin.transform, focusNodes, out b)) return;

            float aspect = (float)target.width / target.height;
            float halfH = b.size.y * 0.5f / FrameFill;
            float halfW = b.size.x * 0.5f / FrameFill;
            previewCam.orthographicSize = Mathf.Max(0.05f, Mathf.Max(halfH, halfW / Mathf.Max(0.01f, aspect)));
            previewCam.transform.position = b.center - previewCam.transform.forward * 6f;

            previewCam.targetTexture = target;
            previewCam.Render();
            previewCam.targetTexture = null;
        }

        /// <summary>
        /// <paramref name="focusNodes"/>가 null이면 전신, 아니면 그 노드들의 서브트리만 감싼다.
        /// 모자 썸네일을 전신으로 찍으면 100px 카드에서 모자가 15px가 된다.
        /// </summary>
        /// <remarks>
        /// <see cref="GetComponentsInChildren{T}(bool, List{T})"/>의 <b>리스트 채우기 판</b>을 쓴다 —
        /// 배열 반환판은 호출마다 새 배열을 만드는데 마네킹 렌더러가 60여 개이고, 드래그 중엔
        /// 이 경로가 매 프레임 돈다.
        /// </remarks>
        private bool TryComputeBounds(Transform root, string[] focusNodes, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;

            if (focusNodes == null)
            {
                root.GetComponentsInChildren(false, boundsBuffer);
                for (int i = 0; i < boundsBuffer.Count; i++)
                {
                    if (boundsBuffer[i] == null) continue;
                    if (!any) { bounds = boundsBuffer[i].bounds; any = true; }
                    else bounds.Encapsulate(boundsBuffer[i].bounds);
                }
                return any;
            }

            for (int n = 0; n < focusNodes.Length; n++)
            {
                Transform t = OutfitShapeLibrary.FindDeep(root, focusNodes[n]);
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                t.GetComponentsInChildren(false, boundsBuffer);
                for (int i = 0; i < boundsBuffer.Count; i++)
                {
                    if (boundsBuffer[i] == null) continue;
                    if (!any) { bounds = boundsBuffer[i].bounds; any = true; }
                    else bounds.Encapsulate(boundsBuffer[i].bounds);
                }
            }

            // 파츠가 하나도 안 잡히면(레시피 없는 아이템 등) 전신으로 물러난다.
            if (!any) return TryComputeBounds(root, null, out bounds);

            bounds.Expand(0.12f);   // 몸의 맥락이 조금 보이도록 여유
            return true;
        }

        /// <summary>슬롯별로 카드에서 봐야 할 부위. 레시피 컨테이너(OP_*)도 함께 잡는다.</summary>
        internal static string[] FocusNodesFor(OutfitSlot slot)
        {
            switch (slot)
            {
                case OutfitSlot.Hat: return HatFocus;
                case OutfitSlot.Top: return TopFocus;
                case OutfitSlot.Bottom: return BottomFocus;
                case OutfitSlot.Outerwear: return OuterFocus;
                case OutfitSlot.Shoes: return ShoesFocus;
                case OutfitSlot.Backpack: return BackpackFocus;
                case OutfitSlot.Tool: return ToolFocus;
                default: return AccessoryFocus;
            }
        }

        private static readonly string[] HatFocus = { "HatRoot", "OP_Hat", "Head" };
        private static readonly string[] TopFocus = { "Shirt", "Body" };
        private static readonly string[] BottomFocus = { "LegL", "LegR" };
        private static readonly string[] OuterFocus = { "Body", "ArmL", "ArmR", "OP_Outerwear" };
        private static readonly string[] ShoesFocus = { "BootL", "BootR" };
        private static readonly string[] BackpackFocus = { "Backpack", "OP_Backpack" };
        private static readonly string[] ToolFocus = { "NetHandle", "NetRing" };
        private static readonly string[] AccessoryFocus = { "OP_Accessory" };

        /// <summary>
        /// 얼굴이 보이는 기본 각도. 프리뷰 카메라는 -Z에서 +Z를 바라보는데 캐릭터는 +Z를 향하므로,
        /// 회전 0°면 <b>뒤통수</b>가 잡힌다. 180°가 정면이고 여기서 20° 더 돌려 입체감을 준다.
        /// </summary>
        public const float FrontYaw = 200f;

        /// <summary>
        /// 썸네일 각도. 가방·겉옷 자락처럼 등에 붙는 것은 정면에서 안 보이므로 뒤쪽을 비스듬히 잡는다.
        /// </summary>
        internal static float ThumbAngleFor(OutfitSlot slot)
        {
            switch (slot)
            {
                case OutfitSlot.Backpack: return 25f;
                case OutfitSlot.Outerwear: return 35f;
                default: return FrontYaw;
            }
        }

        // ── 유틸 ──

        private static RenderTexture CreateRT(int w, int h)
        {
            RenderTexture rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            rt.Create();
            return rt;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private void ReleaseThumbs()
        {
            foreach (KeyValuePair<ThumbId, RenderTexture> pair in thumbs)
                DisposeRT(pair.Value);
            thumbs.Clear();
            thumbOrder.Clear();
            queueSlots.Clear();
            queueIds.Clear();
        }

        private void OnDestroy()
        {
            DisposeRT(currentRT);
            currentRT = null;
            ReleaseThumbs();

            // 마네킹은 씬 루트에 서 있다(부모가 없다) — 이 컴포넌트가 죽어도 따라 사라지지 않아
            // 고아 GameObject로 남는다. 외형이 바뀔 때만 `Destroy(mannequin)`이 있고 수명 종료
            // 경로엔 없었다. 파괴하면 그쪽 PlayerVisualBuilder.OnDestroy가 머티리얼과
            // spawn 의상 파츠까지 연쇄로 정리한다.
            if (mannequin != null) Destroy(mannequin);
            mannequin = null;
        }
    }
}
