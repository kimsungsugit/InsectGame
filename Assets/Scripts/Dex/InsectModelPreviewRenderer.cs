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

        private void Update()
        {
            if (requestData == null) return;
            // (id, angle, shiny) 변화 없으면 재렌더 안 함 — 회전/선택/이로치 토글 시에만 1회 렌더
            if (currentId == requestData.insectId && currentShiny == requestShiny
                && Mathf.Approximately(currentAngle, requestAngle)) return;
            EnsureRig();
            RenderInsect(requestData, requestAngle, requestShiny);
            currentId = requestData.insectId;
            currentAngle = requestAngle;
            currentShiny = requestShiny;
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
            previewCam.targetTexture = currentRT;
            previewCam.Render();
            previewCam.targetTexture = null;

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
        }
    }
}
