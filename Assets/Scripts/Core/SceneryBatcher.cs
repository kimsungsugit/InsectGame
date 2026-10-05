using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 장식 소품의 색표 — 256×1 텍스처 한 장. 소품마다 UV로 제 색 칸을 가리키므로 <b>색이 몇 개든 머티리얼이 하나</b>다.
    ///
    /// 처음엔 색마다 머티리얼을 나눴더니(리전 장식 전체에서) 머티리얼 201개·렌더러 3,407개가 나왔다 — 칸마다
    /// 색 수만큼 드로우콜이 늘었다. Standard 셰이더는 정점 색을 안 읽으므로 색을 텍스처로 옮긴다.
    /// 감마 색공간 프로젝트라 텍스처 값이 그대로 알베도다. 점 필터 + 칸 중앙 UV라 이웃 색이 번지지 않는다.
    /// </summary>
    public sealed class SceneryPalette
    {
        public const int Size = 256;

        private readonly Dictionary<Color, int> index = new Dictionary<Color, int>();
        private readonly Color32[] pixels = new Color32[Size];
        private Texture2D texture;
        private Material material;
        private bool overflowWarned;

        /// <summary>
        /// 색 칸을 얻는다. 256칸이 차면 false — 호출부는 색별 머티리얼로 떨어진다.
        ///
        /// 넘치면 <b>경고를 한 번</b> 남긴다. 폴백 자체는 멀쩡히 그려지므로 아무도 눈치채지 못한 채 칸마다
        /// 드로우콜이 색 수만큼 다시 늘어난다(팔레트를 도입한 이유가 통째로 사라진다). 리전 장식은 2026-09-28에
        /// 약 205색이라 여유가 50칸뿐이다 — 리전·색을 늘리다 넘기면 이 경고가 먼저 알려 준다.
        /// </summary>
        public bool TryIndex(Color c, out int i)
        {
            if (index.TryGetValue(c, out i)) return true;
            if (index.Count >= Size)
            {
                if (!overflowWarned)
                {
                    overflowWarned = true;
                    Debug.LogWarning($"[SceneryPalette] 색 {Size}칸이 찼다 — 넘친 색은 색별 머티리얼로 떨어져 칸마다 드로우콜이 는다. " +
                        "장식 색 수를 줄이거나(가까운 색 합치기) 팔레트를 늘릴 것.");
                }
                i = -1;
                return false;
            }
            i = index.Count;
            index[c] = i;
            pixels[i] = c;
            return true;
        }

        public static Vector2 UV(int i) => new Vector2((i + 0.5f) / Size, 0.5f);

        /// <summary>팔레트 머티리얼(무광). 처음 부를 때 만들고, <see cref="Apply"/>가 색을 올린다.</summary>
        public Material GetMaterial(List<Material> ownedMaterials, List<Texture2D> ownedTextures)
        {
            if (material != null) return material;
            texture = new Texture2D(Size, 1, TextureFormat.RGBA32, false)
            {
                name = "SceneryPalette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            ownedTextures.Add(texture);
            material = SceneryMaterials.Create(Color.white);
            material.mainTexture = texture;
            ownedMaterials.Add(material);
            Apply();
            return material;
        }

        /// <summary>지금까지 모인 색을 텍스처에 올린다. 빌드가 끝난 뒤 한 번 더 부르면 된다.</summary>
        public void Apply()
        {
            if (texture == null) return;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }
    }

    /// <summary>
    /// 콜라이더 없는 장식 소품 수백~수천 개를 <b>칸(cell)</b> 단위 메시로 합친다.
    ///
    /// 소품 하나를 GameObject 하나로 두면(이 저장소의 옛 방식) 풀포기 천 개가 드로우콜 천 번이다.
    /// 전부 한 메시로 합치면 리전 하나가 늘 통째로 그려져 프러스텀 컬링이 안 먹는다. 그래서 한 변
    /// <see cref="cellSize"/>m 칸으로 나눈다 — 카메라가 보는 칸만 그려진다.
    ///
    /// 색은 <see cref="SceneryPalette"/>의 UV로 준다(칸당 메시·머티리얼 1개). 발광·광택이 필요한 색만
    /// 색별 머티리얼로 따로 합친다(<see cref="MarkGlow"/>/<see cref="MarkWet"/>). 합치기는 직접 한다 —
    /// <c>Mesh.CombineMeshes</c>는 인스턴스마다 UV를 바꿀 수 없다. 원본 메시는 읽기만 한다.
    /// </summary>
    public sealed class SceneryBatcher
    {
        private struct Item
        {
            public Mesh mesh;
            public Matrix4x4 matrix;
            public Color color;
        }

        private struct Source
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public int[] triangles;
        }

        private readonly float cellSize;
        private readonly SceneryPalette palette;
        private readonly Dictionary<long, List<Item>> cells = new Dictionary<long, List<Item>>();
        private readonly Dictionary<Color, Color> glow = new Dictionary<Color, Color>();
        private readonly HashSet<Color> wet = new HashSet<Color>();

        /// <summary>원본 메시 배열 캐시 — <c>mesh.vertices</c>는 부를 때마다 새 배열을 만든다(소품 수천 번).</summary>
        private static readonly Dictionary<Mesh, Source> sources = new Dictionary<Mesh, Source>();

        public SceneryBatcher(float cellSize, SceneryPalette palette)
        {
            this.cellSize = Mathf.Max(4f, cellSize);
            this.palette = palette;
        }

        public void MarkGlow(Color color, Color emission) => glow[color] = emission;
        public void MarkWet(Color color) => wet.Add(color);

        public void Add(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            if (mesh == null) return;
            long key = CellKey(position);
            if (!cells.TryGetValue(key, out List<Item> list))
            {
                list = new List<Item>();
                cells[key] = list;
            }
            list.Add(new Item { mesh = mesh, matrix = Matrix4x4.TRS(position, rotation, scale), color = color });
        }

        /// <summary>
        /// 합친 메시를 parent 아래에 만든다. 머티리얼·메시·텍스처는 넘긴 목록에 넣으므로 <b>소유자(빌더)의
        /// OnDestroy가 회수</b>한다. surfaceMaterials는 빌더 전체가 공유해 같은 색·같은 표면(발광·광택)이
        /// 머티리얼 하나로 모인다 — 키가 표면까지 보므로 같은 색이라도 표면이 다르면 따로 만든다(<see cref="SceneryMaterialKey"/>).
        /// 공유 범위는 <b>같은 사전 인스턴스를 넘긴 배처끼리</b>다(리전 장식 전체가 하나, 울타리 장식이 하나).
        /// 옛날엔 색만 키로 쓰는 <c>Dictionary&lt;Color, Material&gt;</c>를 받아 같은 색의 발광·광택이 먼저 만든 쪽으로 합쳐졌다.
        ///
        /// 칸 오브젝트 이름은 <c>{namePrefix}_{n}</c>이다. <paramref name="namePrefix"/>에 <c>Scenery_</c>를
        /// 달지 말 것 — 서브에리어 진입 때 <c>SubAreaWorldBuilder.HideMainWorld</c>가 씬의 오브젝트를 <b>전부</b>
        /// 훑어 그 접두어를 가진 것을 하나하나 끄고 목록에 담는다. 접두어는 호출부가 만드는 <b>parent</b>에만 달면
        /// 루트 하나가 꺼지고 칸들은 부모를 따라 안 보인다(옛날엔 칸 수백 개가 따로 걸려 진입마다 SetActive 수백 번).
        /// </summary>
        public void Build(Transform parent, string namePrefix, bool castShadows,
            List<Mesh> ownedMeshes, List<Material> ownedMaterials, List<Texture2D> ownedTextures,
            Dictionary<SceneryMaterialKey, Material> surfaceMaterials)
        {
            int n = 0;
            var paletteItems = new List<Item>();
            var byColor = new Dictionary<Color, List<Item>>();
            foreach (var cell in cells)
            {
                paletteItems.Clear();
                byColor.Clear();
                foreach (Item item in cell.Value)
                {
                    bool special = glow.ContainsKey(item.color) || wet.Contains(item.color);
                    if (!special && palette != null && palette.TryIndex(item.color, out _))
                    {
                        paletteItems.Add(item);
                        continue;
                    }
                    if (!byColor.TryGetValue(item.color, out List<Item> list))
                    {
                        list = new List<Item>();
                        byColor[item.color] = list;
                    }
                    list.Add(item);
                }

                if (paletteItems.Count > 0)
                {
                    Mesh mesh = Combine(paletteItems, namePrefix, true);
                    ownedMeshes.Add(mesh);
                    Spawn(parent, $"{namePrefix}_{n++}", mesh, palette.GetMaterial(ownedMaterials, ownedTextures), castShadows);
                }
                foreach (var kv in byColor)
                {
                    Mesh mesh = Combine(kv.Value, namePrefix, false);
                    ownedMeshes.Add(mesh);
                    Spawn(parent, $"{namePrefix}_{n++}", mesh, ColorMaterial(kv.Key, ownedMaterials, surfaceMaterials), castShadows);
                }
            }
            cells.Clear();
        }

        /// <summary>이 배처에서 <paramref name="color"/>가 받을 표면 — <see cref="MarkGlow"/>/<see cref="MarkWet"/>가 정한다.</summary>
        internal SceneryMaterialKey KeyFor(Color color)
        {
            bool isGlow = glow.TryGetValue(color, out Color emission);
            return new SceneryMaterialKey(color, isGlow, emission, wet.Contains(color));
        }

        /// <summary>
        /// 합친 메시의 인덱스 폭. 16비트 인덱스는 정점 65,535개까지라 넘으면 삼각형이 엉뚱한 정점을 가리켜
        /// 조각이 사방으로 튄다. 경계에 여유를 두고 65,000에서 32비트로 넘긴다(기본값이 16비트라 넘기지 않으면 그대로다).
        /// </summary>
        internal static UnityEngine.Rendering.IndexFormat IndexFormatFor(int vertexCount) =>
            vertexCount > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

        private Mesh Combine(List<Item> items, string name, bool usePalette)
        {
            int vCount = 0, tCount = 0;
            foreach (Item it in items)
            {
                Source s = SourceOf(it.mesh);
                vCount += s.vertices.Length;
                tCount += s.triangles.Length;
            }
            var v = new Vector3[vCount];
            var nrm = new Vector3[vCount];
            var uv = new Vector2[vCount];
            var tri = new int[tCount];
            int vi = 0, ti = 0;
            foreach (Item it in items)
            {
                Source s = SourceOf(it.mesh);
                Matrix4x4 m = it.matrix;
                Matrix4x4 nm = m.inverse.transpose;
                Vector2 u = Vector2.zero;
                if (usePalette && palette.TryIndex(it.color, out int idx)) u = SceneryPalette.UV(idx);
                int baseIndex = vi;
                for (int k = 0; k < s.vertices.Length; k++)
                {
                    v[vi] = m.MultiplyPoint3x4(s.vertices[k]);
                    Vector3 sn = k < s.normals.Length ? s.normals[k] : Vector3.up;
                    nrm[vi] = nm.MultiplyVector(sn).normalized;
                    uv[vi] = u;
                    vi++;
                }
                for (int k = 0; k < s.triangles.Length; k++) tri[ti++] = baseIndex + s.triangles[k];
            }
            var mesh = new Mesh { name = name };
            mesh.indexFormat = IndexFormatFor(vCount);   // 삼각형을 넣기 전에 정해야 한다
            mesh.vertices = v;
            mesh.normals = nrm;
            mesh.uv = uv;
            mesh.triangles = tri;
            mesh.RecalculateBounds();
            // CPU 사본을 버린다 — 다시 읽을 일이 없는 정적 장식이라 GPU 쪽만 있으면 된다(메모리 절반)
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static Source SourceOf(Mesh mesh)
        {
            if (sources.TryGetValue(mesh, out Source s) && s.vertices != null) return s;
            s = new Source { vertices = mesh.vertices, normals = mesh.normals, triangles = mesh.triangles };
            sources[mesh] = s;
            return s;
        }

        /// <summary>
        /// 원본 배열 캐시를 비운다. 빌더가 자기 메시를 파괴하기 전에 불러야 한다 — 파괴된 메시를 키로 쥔 채
        /// 남으면 다음 씬에서 같은 인스턴스 ID가 재사용될 때 엉뚱한 배열이 나올 수 있다.
        /// </summary>
        public static void ClearSourceCache() => sources.Clear();

        private static void Spawn(Transform parent, string name, Mesh mesh, Material mat, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// 색별 머티리얼(발광·광택·팔레트 넘침). 캐시 키는 색 + 이 배처가 정한 표면이다 — 색만 키로 쓰면 같은 색이
        /// 다른 배처에서 다른 표면으로 지정될 때 먼저 만든 쪽으로 합쳐진다. 파괴된 항목(<c>== null</c>)은 새로 만든다.
        /// </summary>
        internal Material ColorMaterial(Color color, List<Material> owned, Dictionary<SceneryMaterialKey, Material> cache)
        {
            SceneryMaterialKey key = KeyFor(color);
            if (cache.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material mat = SceneryMaterials.Create(color);
            if (key.Glow) SceneryMaterials.SetEmission(mat, key.Emission);
            if (key.Wet)
            {
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.82f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.82f);
            }
            owned.Add(mat);
            cache[key] = mat;
            return mat;
        }

        private long CellKey(Vector3 p)
        {
            long cx = Mathf.FloorToInt(p.x / cellSize);
            long cz = Mathf.FloorToInt(p.z / cellSize);
            return (cx << 32) ^ (cz & 0xffffffffL);
        }
    }
}
