using System.Collections.Generic;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Dex
{
    /// <summary>
    /// 곤충 3D 모델(InsectEntity)을 RenderTexture로 렌더해 도감 OnGUI에 진짜 곤충 그림으로 표시.
    /// 옛 도감은 단색 박스/약식 2D만 그렸음 — 기존 34종 3D 빌더를 재사용해 품질 향상.
    ///
    /// 구조: 플레이 영역 밖(RigOrigin)에 전용 레이어 직교 카메라 + 전용 광원 리그를 두고,
    /// insectId별로 1회 렌더해 RenderTexture를 캐싱. 렌더는 OnGUI가 아니라 Update에서 처리
    /// (GUI 도중 Camera.Render 회피). 도감은 한 번에 한 곤충만 보므로 프레임당 1개만 lazy 렌더.
    /// </summary>
    public class InsectModelPreviewRenderer : MonoBehaviour
    {
        private const int PreviewLayer = 30;   // 전용 레이어 (SubAreaWorldBuilder는 31 사용)
        private const int TexSize = 256;
        private static readonly Vector3 RigOrigin = new Vector3(0f, -5000f, 0f); // 메인 카메라 시야 밖

        private Camera previewCam;
        private bool rigBuilt;
        private readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();
        private readonly Queue<InsectData> pending = new Queue<InsectData>();
        private readonly HashSet<string> queued = new HashSet<string>();

        /// <summary>도감에서 호출. 캐시되어 있으면 즉시 반환, 아니면 렌더 예약 후 null 반환(다음 프레임에 준비됨).</summary>
        public Texture GetPreview(InsectData data)
        {
            if (data == null || string.IsNullOrEmpty(data.insectId)) return null;
            if (cache.TryGetValue(data.insectId, out RenderTexture rt) && rt != null) return rt;
            if (queued.Add(data.insectId)) pending.Enqueue(data);
            return null;
        }

        private void Update()
        {
            if (pending.Count == 0) return;
            EnsureRig();
            // 프레임당 1개만 렌더(스파이크 회피). 도감은 선택된 곤충 1개만 표시하므로 충분.
            InsectData data = pending.Dequeue();
            queued.Remove(data.insectId);
            if (!cache.ContainsKey(data.insectId))
                cache[data.insectId] = RenderInsect(data);
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
            previewCam.orthographicSize = 1.1f; // 곤충이 도감 박스를 적절히 채우도록(큰 날개종은 약간 여유)
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

        private RenderTexture RenderInsect(InsectData data)
        {
            GameObject modelGo = new GameObject("InsectPreviewModel");
            modelGo.transform.position = RigOrigin;
            modelGo.transform.rotation = Quaternion.Euler(0f, 150f, 0f); // 3/4 시점(머리가 카메라쪽으로 살짝 틀어짐)
            InsectEntity ent = modelGo.AddComponent<InsectEntity>();
            ent.BuildForBattle(data, Mathf.Max(1, data.minLevel), false);
            ent.enabled = false; // Update(bob/wing/회전) 정지 — 정적 프레임 렌더
            SetLayerRecursive(modelGo, PreviewLayer);

            RenderTexture rt = new RenderTexture(TexSize, TexSize, 16, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 2;
            rt.Create();
            previewCam.targetTexture = rt;
            previewCam.Render();
            previewCam.targetTexture = null;

            Destroy(modelGo);
            return rt;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private void OnDestroy()
        {
            foreach (RenderTexture rt in cache.Values)
                if (rt != null) rt.Release();
            cache.Clear();
        }
    }
}
