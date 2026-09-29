#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 런타임에 키워드를 켜는 Standard 머티리얼의 <b>셰이더 변형을 빌드에 남긴다</b>.
    ///
    /// 월드 머티리얼은 전부 코드에서 <c>new Material(Standard)</c>로 만들고 반투명(<c>SceneryMaterials.MakeFade</c> —
    /// <c>_ALPHABLEND_ON</c>)·발광(<c>SceneryMaterials.SetEmission</c> — <c>_EMISSION</c>)을 런타임에 켠다. 그런데 둘 다
    /// Standard의 <c>shader_feature</c>라 <b>빌드에 든 머티리얼이 쓰는 조합만</b> 컴파일된다. 이 저장소엔 .mat이 하나도
    /// 없어서 Standard가 Always Included인데도 빌드 로그가 "FORWARD 664 → built-in stripping 뒤 8"이었다 — 반투명·발광
    /// 변형이 통째로 빠졌고, 없는 변형을 요청하면 불투명·무발광 변형으로 그려진다(전투 이펙트가 QA 빌드에서 불투명 공으로
    /// 찍힌 원인). 물·얼음·유리·안개·발광 소품이 같은 경로라 기기에서 같은 결함을 안고 있었다.
    ///
    /// 고치는 방법은 그 조합을 쓰는 머티리얼을 <c>Resources</c>에 두는 것이다 — Resources 에셋은 항상 빌드에 들어가고,
    /// 빌드는 그 키워드 조합의 변형을 multi_compile(조명·그림자·안개) 전부와 곱해 남긴다. 머티리얼은 로드하지 않는다.
    /// 조합 목록의 단일 출처는 런타임 쪽 <c>SceneryMaterials.BuildKeepers</c>다(테스트가 런타임 어셈블리라 에디터 코드를 못 본다).
    ///
    /// 단 <b>Standard가 Always Included Shaders에 있으면 이 방법이 안 먹는다</b> — 그 목록의 셰이더는 머티리얼 키워드를 안 보고
    /// shader_feature를 끈 변형만 싣는다. 머티리얼을 두고 빌드해도 로그가 그대로여서(FORWARD/Fragment 16) 목록에서 뺐고,
    /// 그러자 64(반투명 32·발광 32·둘 다 16)가 됐다. 그래서 키워드 없는 <c>StandardOpaque</c>도 둔다 — 셰이더 자체를 빌드에
    /// 싣는 게 그 몫이다(없으면 <c>Shader.Find("Standard")</c>가 빌드에서 null이 되어 Unlit 폴백으로 떨어진다).
    /// 판정은 QA 빌드의 <c>-battleScenario materials</c>(<c>SceneryMaterialVisualCapture</c>)가 수치로 한다.
    /// </summary>
    public static class ShaderVariantKeepers
    {
        public const string Folder = "Assets/Resources/" + InsectGame.Core.SceneryMaterials.BuildKeeperFolder;

        /// <summary>배치: <c>-executeMethod InsectGame.EditorTools.ShaderVariantKeepers.Ensure</c>. 있으면 키워드만 맞춘다.</summary>
        [MenuItem("InsectGame/Shader Variant Keepers 갱신")]
        public static void Ensure()
        {
            Shader standard = Shader.Find("Standard");
            if (standard == null) throw new BuildFailedException("Standard 셰이더를 찾지 못했다");
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }
            foreach ((string name, string[] keywords) in InsectGame.Core.SceneryMaterials.BuildKeepers)
            {
                string path = Folder + "/" + name + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool created = mat == null;
                if (created) mat = new Material(standard);
                mat.shader = standard;
                mat.shaderKeywords = keywords;
                bool fade = System.Array.IndexOf(keywords, "_ALPHABLEND_ON") >= 0;
                // 변형 선택에는 키워드만 쓰인다. 블렌드·큐는 인스펙터에서 열었을 때 같은 모습이 되게만 맞춘다(MakeFade와 같은 값).
                mat.SetFloat("_Mode", fade ? 2f : 0f);
                mat.SetInt("_SrcBlend", (int)(fade ? BlendMode.SrcAlpha : BlendMode.One));
                mat.SetInt("_DstBlend", (int)(fade ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                mat.SetInt("_ZWrite", fade ? 0 : 1);
                mat.renderQueue = fade ? (int)RenderQueue.Transparent : -1;
                if (System.Array.IndexOf(keywords, "_EMISSION") >= 0)
                {
                    mat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f));
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                if (created) AssetDatabase.CreateAsset(mat, path);
                else EditorUtility.SetDirty(mat);
                Debug.Log("[ShaderVariantKeepers] " + path + " — " + string.Join(" ", keywords));
            }
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// 빌드 로그에 Standard 변형이 실제로 몇 개 남았는지 키워드별로 적는다 — 아무것도 걸러내지 않는다.
    /// 반투명·발광 변형이 0이면 기기에서 물이 불투명해지는데 빌드는 성공하므로, 로그에서 "[ShaderVariants]"를 찾아 확인한다.
    /// 마지막 순서라 다른 스트리퍼가 걸러낸 뒤의 수다.
    /// </summary>
    internal sealed class StandardVariantReport : IPreprocessShaders
    {
        public int callbackOrder => int.MaxValue;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || shader.name != "Standard") return;
            var alpha = new ShaderKeyword(shader, "_ALPHABLEND_ON");
            var emission = new ShaderKeyword(shader, "_EMISSION");
            int a = 0, e = 0, both = 0;
            for (int i = 0; i < data.Count; i++)
            {
                bool ha = data[i].shaderKeywordSet.IsEnabled(alpha);
                bool he = data[i].shaderKeywordSet.IsEnabled(emission);
                if (ha) a++;
                if (he) e++;
                if (ha && he) both++;
            }
            Debug.Log($"[ShaderVariants] Standard {snippet.passName}/{snippet.shaderType}: {data.Count}개 — " +
                      $"_ALPHABLEND_ON {a} · _EMISSION {e} · 둘 다 {both}");
        }
    }
}
#endif
