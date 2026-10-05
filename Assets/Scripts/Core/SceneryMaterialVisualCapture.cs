#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 월드 머티리얼의 반투명·발광이 <b>플레이어 빌드에서</b> 실제로 그려지는지 재는 촬영 fixture(<c>-battleScenario materials</c>).
    ///
    /// 에디터에선 모든 셰이더 변형이 있어 늘 멀쩡하다 — 결함은 빌드가 Standard의 <c>shader_feature</c> 변형을 걸러낼 때만
    /// 난다(<c>ShaderVariantKeepers</c> 주석). 그래서 눈대중 대신 수치로 판정한다: 빨강·파랑 세로 줄무늬 벽 앞에 구를 세우고
    /// 구 가로줄의 (r−b) 편차를 잰다. 반투명이면 줄무늬가 비쳐 편차가 크고, 불투명 변형으로 떨어지면 거의 0이다.
    /// 발광은 역광 쪽 어두운 구 두 개(발광/무발광)의 밝기 차로 본다. 필드 안개(Exp2)를 켠 판도 함께 찍는다 —
    /// 안개 변형과 곱해진 조합이 따로 빠질 수 있다.
    /// </summary>
    public static class SceneryMaterialVisualCapture
    {
        private const float Radius = 0.6f;

        private struct Probe
        {
            public string name;
            public Vector3 center;
        }

        public static IEnumerator Run(string output, Camera camera)
        {
            camera.transform.position = new Vector3(0f, 1.2f, -6.5f);
            camera.transform.LookAt(new Vector3(0f, 1.2f, 0f));
            camera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

            Light key = new GameObject("MaterialQAKey").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            // 구 앞면에 빛을 준다 — 발광 비교 구는 뒤쪽 면이 아니라 카메라를 향한 면을 재므로 조명 차가 없다.
            key.transform.rotation = Quaternion.Euler(30f, 20f, 0f);

            // 줄무늬 벽: 0.5m 폭 빨강·파랑 번갈아. 불투명 무광(월드와 같은 생성 경로).
            Material red = SceneryMaterials.Create(new Color(0.85f, 0.12f, 0.1f));
            Material blue = SceneryMaterials.Create(new Color(0.1f, 0.2f, 0.9f));
            for (int i = 0; i < 20; i++)
            {
                GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cube);
                col.transform.position = new Vector3(-5f + i * 0.5f + 0.25f, 1.5f, 3f);
                col.transform.localScale = new Vector3(0.5f, 4f, 0.2f);
                col.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? red : blue;
            }

            Color glow = new Color(1f, 0.8f, 0.3f);
            Probe fade = Sphere("fade", -2.4f, M(new Color(0.95f, 0.95f, 0.95f, 0.35f), false, glow));
            Probe fadeGlow = Sphere("fade+emission", -0.8f, M(new Color(0.95f, 0.95f, 0.95f, 0.35f), true, glow));
            Probe glowing = Sphere("emission", 0.8f, M(new Color(0.12f, 0.12f, 0.12f), true, glow));
            Probe plain = Sphere("plain", 2.4f, M(new Color(0.12f, 0.12f, 0.12f), false, glow));

            var report = new StringBuilder();
            report.AppendLine("Actual standalone render. Stripe signal = stddev of (r-b) across the sphere's middle row;");
            report.AppendLine("translucent spheres must show the stripe wall behind them. Emission = luminance(emission) - luminance(plain).");
            bool allPass = true;
            foreach (bool fog in new[] { false, true })
            {
                RenderSettings.fog = fog;
                RenderSettings.fogMode = RegionAtmosphere.FieldFogMode;
                RenderSettings.fogDensity = 0.02f;
                RenderSettings.fogColor = new Color(0.6f, 0.65f, 0.7f);
                yield return null;
                yield return new WaitForEndOfFrame();
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                string tag = fog ? "fog-exp2" : "no-fog";
                File.WriteAllBytes(Path.Combine(output, "materials-" + tag + ".png"), shot.EncodeToPNG());

                float wall = StripeSignal(shot, camera, new Vector3(0f, 2.9f, 3f), 1.2f);
                float sFade = StripeSignal(shot, camera, fade.center, Radius);
                float sFadeGlow = StripeSignal(shot, camera, fadeGlow.center, Radius);
                float emission = Luminance(shot, camera, glowing.center) - Luminance(shot, camera, plain.center);
                // 기준: 벽 자체 편차의 15% 이상이 구를 통해 보이면 비친다. 불투명 변형이면 1~2% 수준이다.
                bool fadeOk = sFade > wall * 0.15f;
                bool fadeGlowOk = sFadeGlow > wall * 0.15f;
                bool emissionOk = emission > 0.12f;
                allPass &= fadeOk && fadeGlowOk && emissionOk;
                report.AppendLine($"[{tag}] wall={wall:0.000} fade={sFade:0.000} ({Verdict(fadeOk)}) " +
                                  $"fade+emission={sFadeGlow:0.000} ({Verdict(fadeGlowOk)}) " +
                                  $"emission-delta={emission:0.000} ({Verdict(emissionOk)})");
                Object.Destroy(shot);
            }
            report.AppendLine("RESULT " + (allPass ? "PASS" : "FAIL"));
            File.WriteAllText(Path.Combine(output, "README.txt"), report.ToString());
            Debug.Log("[MaterialQA]\n" + report);
            Application.Quit(allPass ? 0 : 5);   // 4는 BattleVisualCapture 감시견(시간 초과)이 쓴다
        }

        private static string Verdict(bool ok) => ok ? "PASS" : "FAIL";

        /// <summary>월드 빌더와 같은 경로 — 생성 → 발광 → 반투명 순(SubAreaGateBuilder 연기 덩이가 이 순서다).</summary>
        private static Material M(Color color, bool emission, Color glow)
        {
            Material m = SceneryMaterials.Create(color);
            if (emission) SceneryMaterials.SetEmission(m, glow);
            if (SceneryMaterials.IsTranslucent(color)) SceneryMaterials.MakeFade(m);
            return m;
        }

        private static Probe Sphere(string name, float x, Material mat)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "MaterialQA_" + name;
            s.transform.position = new Vector3(x, 1.2f, 0f);
            s.transform.localScale = Vector3.one * Radius * 2f;
            s.GetComponent<Renderer>().sharedMaterial = mat;
            return new Probe { name = name, center = s.transform.position };
        }

        /// <summary>중심 가로줄의 가운데 60%에서 (r−b) 표준편차.</summary>
        private static float StripeSignal(Texture2D shot, Camera camera, Vector3 center, float worldHalfWidth)
        {
            Vector3 c = camera.WorldToScreenPoint(center);
            Vector3 edge = camera.WorldToScreenPoint(center + camera.transform.right * worldHalfWidth);
            float half = Mathf.Abs(edge.x - c.x) * 0.6f;
            int y = Mathf.Clamp(Mathf.RoundToInt(c.y), 0, shot.height - 1);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(c.x - half), 0, shot.width - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(c.x + half), 0, shot.width - 1);
            int n = 0;
            float sum = 0f, sumSq = 0f;
            for (int x = x0; x <= x1; x++)
            {
                Color p = shot.GetPixel(x, y);
                float v = p.r - p.b;
                sum += v;
                sumSq += v * v;
                n++;
            }
            if (n < 2) return 0f;
            float mean = sum / n;
            return Mathf.Sqrt(Mathf.Max(0f, sumSq / n - mean * mean));
        }

        /// <summary>중심 둘레 5×5 픽셀 평균 휘도.</summary>
        private static float Luminance(Texture2D shot, Camera camera, Vector3 center)
        {
            Vector3 c = camera.WorldToScreenPoint(center);
            int cx = Mathf.RoundToInt(c.x), cy = Mathf.RoundToInt(c.y);
            float sum = 0f;
            int n = 0;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = Mathf.Clamp(cx + dx, 0, shot.width - 1), y = Mathf.Clamp(cy + dy, 0, shot.height - 1);
                Color p = shot.GetPixel(x, y);
                sum += 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b;
                n++;
            }
            return sum / n;
        }
    }
}
#endif
