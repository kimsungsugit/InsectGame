using System.Collections.Generic;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Dex
{
    /// <summary>
    /// 곤충 3D 모델(InsectEntity)을 RenderTexture로 렌더해 도감 OnGUI에 진짜 곤충 그림으로 표시.
    /// 좌우 회전(시점 변경)을 지원: 도감이 GetPreview(data, yAngle)로 원하는 각도를 요청하면
    /// (insectId, angle)이 바뀔 때만 단일 RenderTexture를 재렌더(재사용). 렌더는 OnGUI가 아닌
    /// Update에서 처리(GUI 도중 Camera.Render 회피).
    ///
    /// 구조: 플레이 영역 밖(RigOrigin)에 전용 레이어 직교 카메라 + 전용 광원 리그.
    /// </summary>
    public class InsectModelPreviewRenderer : MonoBehaviour
    {
        private const int PreviewLayer = 30;   // 전용 레이어 (SubAreaWorldBuilder는 31 사용)
        private const int TexSize = 512;  // 도감 대형 프리뷰(최대 ~870px)에서도 또렷하게 — 옛 256은 확대 시 흐릿
        private const float FrameFill = 0.9f;  // 곤충이 프레임을 채우는 비율(0.9=90%, 가장자리 여백 약간)
        private static readonly Vector3 RigOrigin = new Vector3(0f, -5000f, 0f); // 메인 카메라 시야 밖

        private Camera previewCam;
        private bool rigBuilt;

        private RenderTexture currentRT;       // 재사용 단일 RT
        private string currentId;
        private float currentAngle = float.NaN;
        private bool currentShiny;
        private InsectData requestData;
        private float requestAngle = 150f;
        private bool requestShiny;

        /// <summary>도감에서 호출. 같은 곤충(+같은 이로치 상태)이 이미 렌더돼 있으면 그 RT(각도는 다음 프레임 반영),
        /// 다르면 null(Update가 렌더할 때까지 1프레임 폴백). yAngle은 Y축 회전(도). wantShiny=이로치 표시 여부.</summary>
        public Texture GetPreview(InsectData data, float yAngle, bool wantShiny)
        {
            if (data == null || string.IsNullOrEmpty(data.insectId)) return null;
            requestData = data;
            requestAngle = yAngle;
            requestShiny = wantShiny;
            return (currentId == data.insectId && currentShiny == wantShiny) ? currentRT : null;
        }

        // ── 목록·타일용 썸네일 캐시 ──
        //
        // 상세 모달(GetPreview)은 512px 단일 RT를 회전까지 시키지만, 목록은 종이 여럿이라
        // 그 구조로는 못 쓴다. 종별 소형 RT를 LRU로 들고 있는다.
        //
        // **프레임당 1개만 렌더한다.** RenderInsect는 InsectEntity를 통째로 만들었다 파괴하므로
        // 도감을 여는 순간 20종을 한 프레임에 처리하면 확실히 튄다. 준비 전에는 호출부가
        // 2D 폴백(InsectVisual)을 그린다.
        private const int ThumbSize = 192;      // 타일 아이콘이 열 수 2~6에 따라 129~396px
        private const int ThumbCacheMax = 24;   // 24 × 192² × 4B ≈ 3.5MB
        private const float ThumbAngle = 150f;  // 상세 기본 각도와 같게 — 두 화면의 그림이 어긋나지 않게

        private readonly Dictionary<string, RenderTexture> thumbs = new Dictionary<string, RenderTexture>();
        private readonly List<string> thumbOrder = new List<string>();   // 앞이 가장 오래됨(LRU)
        private readonly List<InsectData> thumbQueue = new List<InsectData>();
        private readonly List<bool> thumbQueueShiny = new List<bool>();

        internal static string ThumbKey(string insectId, bool shiny)
        {
            return shiny ? insectId + "*" : insectId;
        }

        /// <summary>
        /// LRU 갱신 — <paramref name="key"/>를 목록 맨 뒤(가장 최근)로 옮긴다.
        /// RenderTexture와 무관한 순수 정책이라 테스트가 직접 부른다.
        /// </summary>
        internal static void TouchKey(List<string> order, string key)
        {
            if (order == null || string.IsNullOrEmpty(key)) return;
            int i = order.IndexOf(key);
            if (i >= 0) order.RemoveAt(i);
            order.Add(key);
        }

        /// <summary>
        /// 상한을 넘긴 만큼 **가장 오래된 것부터** 목록에서 떼어 돌려준다(호출부가 그 RT를 해제한다).
        /// 상한 이하면 빈 목록. 순수 함수 — 여기가 틀리면 캐시가 무한히 자라거나 방금 쓴 걸 버린다.
        /// </summary>
        internal static List<string> EvictKeys(List<string> order, int cap)
        {
            List<string> evicted = new List<string>();
            if (order == null || cap < 0) return evicted;
            while (order.Count > cap)
            {
                evicted.Add(order[0]);
                order.RemoveAt(0);
            }
            return evicted;
        }

        /// <summary>
        /// 목록·타일용 썸네일. 캐시에 있으면 즉시 돌려주고, 없으면 렌더 큐에 넣고 null을 낸다
        /// (호출부는 그동안 2D 폴백을 그린다).
        /// </summary>
        public Texture GetThumbnail(InsectData data, bool shiny)
        {
            if (data == null || string.IsNullOrEmpty(data.insectId)) return null;

            string key = ThumbKey(data.insectId, shiny);
            if (thumbs.TryGetValue(key, out RenderTexture rt) && rt != null)
            {
                Touch(key);
                return rt;
            }

            // 캡처 람다(List.Exists)를 쓰지 않는다 — 이 메서드는 OnGUI에서 항목마다 불리므로
            // 클로저가 매 패스 할당된다(같은 형태를 RegionMapUI에서 방금 걷어냈다).
            for (int i = 0; i < thumbQueue.Count; i++)
            {
                if (thumbQueue[i] != null && thumbQueue[i].insectId == data.insectId
                    && thumbQueueShiny[i] == shiny)
                    return null;
            }
            thumbQueue.Add(data);
            thumbQueueShiny.Add(shiny);
            return null;
        }

        private void Touch(string key)
        {
            TouchKey(thumbOrder, key);
        }

        private void Update()
        {
            // 상세 모달이 우선 — 사용자가 보고 있는 큰 그림이 먼저 나와야 한다.
            if (requestData != null
                && !(currentId == requestData.insectId && currentShiny == requestShiny
                     && Mathf.Approximately(currentAngle, requestAngle)))
            {
                EnsureRig();
                RenderInsect(requestData, requestAngle, requestShiny);
                currentId = requestData.insectId;
                currentAngle = requestAngle;
                currentShiny = requestShiny;
                return;   // 같은 프레임에 썸네일까지 렌더하지 않는다
            }

            RenderOneQueuedThumbnail();
        }

        private void RenderOneQueuedThumbnail()
        {
            if (thumbQueue.Count == 0) return;

            InsectData data = thumbQueue[0];
            bool shiny = thumbQueueShiny[0];
            thumbQueue.RemoveAt(0);
            thumbQueueShiny.RemoveAt(0);
            if (data == null || string.IsNullOrEmpty(data.insectId)) return;

            string key = ThumbKey(data.insectId, shiny);
            if (thumbs.ContainsKey(key)) { Touch(key); return; }

            EnsureRig();
            RenderTexture rt = new RenderTexture(ThumbSize, ThumbSize, 16, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };
            rt.Create();
            RenderInsectTo(data, ThumbAngle, shiny, rt);

            thumbs[key] = rt;
            Touch(key);
            EvictOverflow();
        }

        private void EvictOverflow()
        {
            List<string> evicted = EvictKeys(thumbOrder, ThumbCacheMax);
            for (int i = 0; i < evicted.Count; i++)
            {
                if (thumbs.TryGetValue(evicted[i], out RenderTexture rt) && rt != null)
                    rt.Release();
                thumbs.Remove(evicted[i]);
            }
        }

        private void EnsureRig()
        {
            if (rigBuilt) return;

            GameObject camGo = new GameObject("InsectPreviewCam");
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = RigOrigin + new Vector3(0f, 0.25f, -3f);
            camGo.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            previewCam = camGo.AddComponent<Camera>();
            previewCam.orthographic = true;
            previewCam.orthographicSize = 0.85f; // 곤충이 도감 박스를 크게 채우도록(옛 1.1은 작게 보임)
            previewCam.cullingMask = 1 << PreviewLayer;
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 투명 — 도감 박스 위에 합성
            previewCam.nearClipPlane = 0.05f;
            previewCam.farClipPlane = 20f;
            previewCam.enabled = false; // 자동 렌더 끔 — RenderInsect에서 수동 Render만

            GameObject lightGo = new GameObject("InsectPreviewLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.cullingMask = 1 << PreviewLayer; // 프리뷰 모델만 비춤(월드 이중조명 방지)
            l.intensity = 1.15f;
            l.color = new Color(1f, 0.98f, 0.92f);

            rigBuilt = true;
        }

        private void RenderInsect(InsectData data, float yAngle, bool wantShiny)
        {
            RenderInsectTo(data, yAngle, wantShiny, null);
        }

        /// <summary>
        /// 모델을 만들어 <paramref name="target"/>에 렌더한다. null이면 상세용 단일 RT(currentRT).
        /// 상세와 썸네일이 <b>같은 경로</b>를 타야 프레이밍·조명·머티리얼 정리가 어긋나지 않는다.
        /// </summary>
        private void RenderInsectTo(InsectData data, float yAngle, bool wantShiny, RenderTexture target)
        {
            GameObject modelGo = new GameObject("InsectPreviewModel");
            modelGo.transform.position = RigOrigin;
            modelGo.transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
            InsectEntity ent = modelGo.AddComponent<InsectEntity>();
            ent.BuildForBattle(data, Mathf.Max(1, data.minLevel), wantShiny);
            ent.enabled = false; // Update(bob/wing/회전) 정지 — 정적 프레임 렌더
            SetLayerRecursive(modelGo, PreviewLayer);

            // 자동 프레이밍: 곤충마다 크기가 달라 고정 줌이면 작은 종(개미·진딧물)이 박스에서 작게 보임.
            // 모델 바운드를 계산해 카메라를 중심에 맞추고 크기에 비례해 줌 → 모든 종이 박스를 꽉 채움.
            FrameModel(modelGo);

            if (currentRT == null)
            {
                currentRT = new RenderTexture(TexSize, TexSize, 16, RenderTextureFormat.ARGB32);
                currentRT.antiAliasing = 2;
                currentRT.Create();
            }
            previewCam.targetTexture = target != null ? target : currentRT;
            previewCam.Render();
            previewCam.targetTexture = null;

            // 모델 인스턴스 머티리얼 정리 — InsectEntity가 파트마다 머티리얼을 만드는데 GO 파괴로는
            // 해제되지 않아 렌더(회전/선택/이로치 토글)마다 수십 개씩 누수. 자식 렌더러 인스턴스 해제.
            Renderer[] rends = modelGo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null && rends[i].sharedMaterial != null) Destroy(rends[i].material);

            Destroy(modelGo);
        }

        // 모델 전체 렌더러 바운드를 합쳐 카메라를 곤충 중심·크기에 맞춤(직교 카메라라 거리 무관, orthographicSize로 줌).
        // 카메라는 +Z를 봄 → 화면 가로=월드 X, 세로=월드 Y. 깊이(Z)는 화면 크기에 무관하므로 max(x,y) 기준.
        private void FrameModel(GameObject modelGo)
        {
            Renderer[] rends = modelGo.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            float maxDim = Mathf.Max(b.size.x, b.size.y);
            if (maxDim < 0.05f) maxDim = 0.05f;
            previewCam.orthographicSize = maxDim / (2f * FrameFill);
            // 바운드 중심 정면에 카메라 배치(기존 5° 틸트 유지) — 곤충이 항상 박스 중앙.
            previewCam.transform.position = b.center - previewCam.transform.forward * 4f;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private void OnDestroy()
        {
            if (currentRT != null) currentRT.Release();
            currentRT = null;

            // 썸네일 캐시도 전부 해제한다 — 예전엔 RT 하나만 풀었다.
            foreach (KeyValuePair<string, RenderTexture> pair in thumbs)
                if (pair.Value != null) pair.Value.Release();
            thumbs.Clear();
            thumbOrder.Clear();
            thumbQueue.Clear();
            thumbQueueShiny.Clear();
        }
    }
}
