using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 섬 한 채가 쓰는 머티리얼 모음 — <b>같은 색이면 같은 머티리얼</b>을 돌려준다.
    ///
    /// 섬에는 물건이 수백 개까지 놓이고 물건 하나가 프리미티브 여러 개다. 조각마다 <c>new Material</c>을 하면
    /// 머티리얼이 천 개 단위가 되어 배칭이 전혀 안 걸린다. 색으로 묶으면 섬 전체가 수십 개로 끝난다.
    ///
    /// 머티리얼은 GameObject를 파괴해도 남는다 — 섬을 떠날 때 <see cref="DestroyAll"/>을 부를 것
    /// (<c>SubAreaWorldBuilder.runtimeMaterials</c>와 같은 이유).
    /// 만드는 길은 <see cref="SceneryMaterials"/> 하나다: 그쪽 키워드 조합만 플레이어 빌드에 변형이 남는다.
    /// </summary>
    public sealed class IslandMaterialCache
    {
        // 키는 정수로 접는다 — Color32를 그대로 키로 쓰면 조회마다 박싱이 생긴다.
        private readonly Dictionary<uint, Material> opaque = new Dictionary<uint, Material>();
        private readonly Dictionary<uint, Material> fade = new Dictionary<uint, Material>();
        private readonly Dictionary<long, Material> glow = new Dictionary<long, Material>();

        /// <summary>불투명 무광.</summary>
        public Material Get(Color color)
        {
            uint key = Pack(color);
            if (opaque.TryGetValue(key, out Material mat) && mat != null) return mat;
            mat = SceneryMaterials.Create(new Color(color.r, color.g, color.b, 1f));
            opaque[key] = mat;
            return mat;
        }

        /// <summary>반투명(물·유리). 알파는 색의 a를 쓴다.</summary>
        public Material GetFade(Color color)
        {
            Color32 c32 = color;
            uint key = ((uint)c32.a << 24) | Pack(color);
            if (fade.TryGetValue(key, out Material mat) && mat != null) return mat;
            mat = SceneryMaterials.Create(color);
            SceneryMaterials.MakeFade(mat);
            fade[key] = mat;
            return mat;
        }

        /// <summary>스스로 빛나는 것(등불·모닥불).</summary>
        public Material GetGlow(Color color, Color emission)
        {
            long key = ((long)Pack(color) << 32) | Pack(emission);
            if (glow.TryGetValue(key, out Material mat) && mat != null) return mat;
            mat = SceneryMaterials.Create(new Color(color.r, color.g, color.b, 1f));
            SceneryMaterials.SetEmission(mat, emission);
            glow[key] = mat;
            return mat;
        }

        public void DestroyAll()
        {
            DestroyIn(opaque.Values);
            DestroyIn(fade.Values);
            DestroyIn(glow.Values);
            opaque.Clear();
            fade.Clear();
            glow.Clear();
        }

        private static void DestroyIn(IEnumerable<Material> materials)
        {
            foreach (Material mat in materials)
                if (mat != null) Object.Destroy(mat);
        }

        private static uint Pack(Color color)
        {
            Color32 c = color;
            return ((uint)c.r << 16) | ((uint)c.g << 8) | c.b;
        }
    }
}
