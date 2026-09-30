using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>무늬를 입힐 표면. 메시마다 UV 배치가 달라 같은 무늬도 표면별로 따로 굽는다.</summary>
    public enum PatternSurface
    {
        /// <summary>몸통(Body) — 둥근 상자 아틀라스: 왼쪽 절반 = 앞면, 오른쪽 절반 = 뒷면·옆면.</summary>
        Torso,
        /// <summary>자켓 사이로 보이는 셔츠 판(Shirt) — 앞면 절반만 보인다. 몸통보다 좁아 무늬 배치가 다르다.</summary>
        Panel,
        /// <summary>팔(ArmL/R) — 캡슐 원통 투영: u 둘레(0.5 = 앞, 이음매는 뒤), v 위(1)→손목(0).</summary>
        Sleeve,
        /// <summary>다리(LegL/R) — 팔과 같은 원통 투영.</summary>
        Leg,
        /// <summary>신발(BootL/R) — 몸통과 같은 아틀라스(앞 = 발끝, 뒤 = 뒤꿈치·옆). v 0 = 밑창.</summary>
        Boot,
    }

    public enum PatternKind
    {
        None,
        Placket, Polo, Vest, Stripes, LabCoat, Galaxy, Leaf, Flame, WesternVest, Web, Ninja, PirateCoat, Circuit, Camo,
        Cargo, Denim, Sheen, SideStripe, AnkleWrap,
        BootSole, Sneaker, RocketBoot, Crystal, Western,
        JacketTrim, RainCoat, Windbreaker, ShadowCoat, WizardRobe, LabOuter,
    }

    /// <summary>
    /// 의상 **표면 무늬**의 단일 출처 — 절차 생성 텍스처. 형태(실루엣)는 <see cref="OutfitShapeLibrary"/>가 맡고,
    /// 여기는 줄무늬·별·거미줄·위장처럼 "천에 인쇄된 것"만 맡는다.
    ///
    /// 왜 텍스처인가(2026-09-30 샘플 비교): 같은 무늬를 입체 파츠로 만들면 줄무늬는 몸에 두른 벨트, 거미줄은 몸 앞의
    /// 철사 우리, 갤럭시는 흩어진 점 몇 개가 됐다. 텍스처는 정점 비용이 0이고, 같은 무늬를 "겉옷 벗었을 때의 몸통"과
    /// "자켓 사이 셔츠 판"에 그대로 쓸 수 있다.
    ///
    /// 색은 텍스처에 **구워 넣는다**(아이템의 primary·secondary). 그래서 입힐 때 머티리얼 색을 흰색으로 둔다
    /// (<c>CharacterOutfitManager.ApplyPattern</c>). 결과는 (무늬, 표면, 두 색)으로 프로세스 수명 캐시한다 —
    /// 굽는 데 수 ms가 들고 마네킹 썸네일은 같은 옷을 여러 번 입힌다.
    /// </summary>
    public static class OutfitPatternLibrary
    {
        private static readonly Dictionary<string, PatternKind> Kinds = new Dictionary<string, PatternKind>
        {
            // ── 상의 ──
            ["top_shirt"] = PatternKind.Placket,
            ["top_polo"] = PatternKind.Polo,
            ["top_vest"] = PatternKind.Vest,
            ["top_stripe"] = PatternKind.Stripes,
            ["top_lab"] = PatternKind.LabCoat,
            ["top_galaxy"] = PatternKind.Galaxy,
            ["top_nature"] = PatternKind.Leaf,
            ["top_flame"] = PatternKind.Flame,
            ["top_cowboy"] = PatternKind.WesternVest,
            ["top_hero_suit"] = PatternKind.Web,
            ["top_ninja"] = PatternKind.Ninja,
            ["top_pirate"] = PatternKind.PirateCoat,
            ["top_cyber"] = PatternKind.Circuit,
            ["top_military"] = PatternKind.Camo,

            // ── 하의 ──
            ["bot_cargo"] = PatternKind.Cargo,
            ["bot_jeans"] = PatternKind.Denim,
            ["bot_galaxy"] = PatternKind.Galaxy,
            ["bot_golden"] = PatternKind.Sheen,
            ["bot_cowboy"] = PatternKind.Denim,
            ["bot_hero_suit"] = PatternKind.SideStripe,
            ["bot_ninja"] = PatternKind.AnkleWrap,
            ["bot_pirate"] = PatternKind.Stripes,
            ["bot_cyber"] = PatternKind.Circuit,
            ["bot_military"] = PatternKind.Camo,

            // ── 신발 ──
            ["shoe_boots"] = PatternKind.BootSole,
            ["shoe_sneakers"] = PatternKind.Sneaker,
            ["shoe_waders"] = PatternKind.BootSole,
            ["shoe_rocket"] = PatternKind.RocketBoot,
            ["shoe_crystal"] = PatternKind.Crystal,
            ["shoe_cowboy"] = PatternKind.Western,

            // ── 겉옷 ──
            ["outer_jacket"] = PatternKind.JacketTrim,
            ["outer_raincoat"] = PatternKind.RainCoat,
            ["outer_windbreaker"] = PatternKind.Windbreaker,
            ["outer_labcoat"] = PatternKind.LabOuter,
            ["outer_crystal"] = PatternKind.Crystal,
            ["outer_shadow"] = PatternKind.ShadowCoat,
            ["outer_wizard"] = PatternKind.WizardRobe,
        };

        /// <summary>아이템의 무늬 종류. 없으면 None(색만).</summary>
        public static PatternKind KindOf(string itemId)
        {
            return itemId != null && Kinds.TryGetValue(itemId, out PatternKind k) ? k : PatternKind.None;
        }

        internal static IEnumerable<KeyValuePair<string, PatternKind>> Entries() => Kinds;

        // ── 캐시 ──

        private readonly struct Key : System.IEquatable<Key>
        {
            private readonly PatternKind kind;
            private readonly PatternSurface surface;
            private readonly int primary;
            private readonly int secondary;

            public Key(PatternKind kind, PatternSurface surface, Color p, Color s)
            {
                this.kind = kind;
                this.surface = surface;
                primary = Pack(p);
                secondary = Pack(s);
            }

            private static int Pack(Color c)
            {
                Color32 q = c;
                return (q.r << 16) | (q.g << 8) | q.b;
            }

            public bool Equals(Key o) => kind == o.kind && surface == o.surface && primary == o.primary && secondary == o.secondary;
            public override bool Equals(object obj) => obj is Key o && Equals(o);
            public override int GetHashCode() => (((int)kind * 31 + (int)surface) * 397 ^ primary) * 397 ^ secondary;
        }

        private static readonly Dictionary<Key, Texture2D> cache = new Dictionary<Key, Texture2D>();

        internal static int CachedCount => cache.Count;

        /// <summary>
        /// 아이템 무늬 텍스처. 무늬가 없으면 null — 호출부는 텍스처를 비우고 색만 칠한다.
        /// </summary>
        public static Texture2D Get(string itemId, PatternSurface surface, Color primary, Color secondary)
        {
            PatternKind kind = KindOf(itemId);
            if (kind == PatternKind.None || !HasSurface(kind, surface)) return null;

            Key key = new Key(kind, surface, primary, secondary);
            if (cache.TryGetValue(key, out Texture2D tex) && tex != null) return tex;

            SizeOf(surface, out int w, out int h);
            tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "OutfitPattern_" + kind + "_" + surface,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
                hideFlags = HideFlags.HideAndDontSave,   // 프로세스 수명 캐시 — 씬 언로드 대상에서 뺀다(ProcMeshLibrary와 같은 규약)
            };
            tex.SetPixels(Render(kind, surface, primary, secondary, w, h));
            tex.Apply(true);
            cache[key] = tex;
            return tex;
        }

        internal static void SizeOf(PatternSurface surface, out int w, out int h)
        {
            switch (surface)
            {
                case PatternSurface.Sleeve:
                case PatternSurface.Leg:
                    w = 64; h = 64; break;
                case PatternSurface.Boot:
                    w = 128; h = 64; break;
                default:
                    w = 256; h = 128; break;   // 아틀라스: 앞 128 + 뒤 128
            }
        }

        /// <summary>
        /// 이 무늬가 그 표면에 그릴 게 있는가. 없으면 호출부는 색만 칠한다 — 예를 들어 조끼는 소매가 없고
        /// (소매는 속셔츠 색이다), 대부분의 하의 무늬는 신발에 안 쓴다.
        /// </summary>
        internal static bool HasSurface(PatternKind kind, PatternSurface surface)
        {
            switch (kind)
            {
                case PatternKind.None: return false;
                case PatternKind.Vest:
                case PatternKind.Polo:
                case PatternKind.Placket:
                    return surface == PatternSurface.Torso || surface == PatternSurface.Panel;
                case PatternKind.Cargo:
                case PatternKind.Denim:
                case PatternKind.Sheen:
                case PatternKind.SideStripe:
                case PatternKind.AnkleWrap:
                    return surface == PatternSurface.Leg;
                case PatternKind.BootSole:
                case PatternKind.Sneaker:
                case PatternKind.RocketBoot:
                case PatternKind.Western:
                    return surface == PatternSurface.Boot;
                case PatternKind.Crystal:
                    return true;
                default:
                    return surface != PatternSurface.Boot;
            }
        }

        // ── 굽기 ──────────────────────────────────────────

        /// <summary>
        /// 픽셀 배열을 굽는다(순수 — 테스트가 결정성과 표면별 차이를 본다). 아틀라스 표면은 u &lt; 0.5가 앞면이다.
        /// </summary>
        internal static Color[] Render(PatternKind kind, PatternSurface surface, Color p, Color s, int w, int h)
        {
            Color[] px = new Color[w * h];
            Ctx ctx = new Ctx(kind, surface, p, s, w, h);
            bool atlas = surface == PatternSurface.Torso || surface == PatternSurface.Panel || surface == PatternSurface.Boot;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h;
                    bool front = true;
                    if (atlas)
                    {
                        front = u < 0.5f;
                        u = front ? u * 2f : (u - 0.5f) * 2f;
                    }
                    Color c = Shade(ctx, u, v, front);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            return px;
        }

        /// <summary>무늬마다 미리 뽑아 두는 것(별·잎·회로 선분). 픽셀마다 난수를 돌리지 않는다.</summary>
        private sealed class Ctx
        {
            public readonly PatternKind kind;
            public readonly PatternSurface surface;
            public readonly Color p, s;
            public readonly float texelU, texelV;   // 한 텍셀의 크기(0..1 좌표) — 선 굵기를 텍셀로 준다
            public readonly List<Vector4> segs = new List<Vector4>();
            public readonly List<Vector3> dots = new List<Vector3>();     // (u, v, 크기/종류)

            public Ctx(PatternKind kind, PatternSurface surface, Color p, Color s, int w, int h)
            {
                this.kind = kind;
                this.surface = surface;
                this.p = p;
                this.s = s;
                bool atlas = surface == PatternSurface.Torso || surface == PatternSurface.Panel || surface == PatternSurface.Boot;
                texelU = (atlas ? 2f : 1f) / w;
                texelV = 1f / h;

                System.Random rng = new System.Random(((int)kind + 1) * 7919 + (int)surface * 131);
                switch (kind)
                {
                    case PatternKind.Galaxy:
                    case PatternKind.WizardRobe:
                    {
                        int n = surface == PatternSurface.Sleeve || surface == PatternSurface.Leg ? 26 : 70;
                        for (int i = 0; i < n; i++)
                            dots.Add(new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), i < n / 10 ? 1f : 0f));
                        break;
                    }
                    case PatternKind.Leaf:
                    {
                        int n = surface == PatternSurface.Sleeve || surface == PatternSurface.Leg ? 8 : 18;
                        for (int i = 0; i < n; i++)
                            dots.Add(new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble() * Mathf.PI));
                        break;
                    }
                    case PatternKind.Crystal:
                    {
                        for (int i = 0; i < 7; i++)
                            dots.Add(new Vector3((float)rng.NextDouble(), 0.25f + 0.7f * (float)rng.NextDouble(), 1f));
                        break;
                    }
                    case PatternKind.Web:
                    {
                        Vector2 hub = surface == PatternSurface.Sleeve ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0.62f);
                        int spokes = surface == PatternSurface.Sleeve ? 6 : 10;
                        for (int k = 0; k < spokes; k++)
                        {
                            float a = k * Mathf.PI * 2f / spokes;
                            segs.Add(new Vector4(hub.x, hub.y, hub.x + Mathf.Cos(a) * 1.2f, hub.y + Mathf.Sin(a) * 1.2f));
                        }
                        for (int ring = 1; ring <= 6; ring++)
                        {
                            float r = ring * 0.13f;
                            for (int k = 0; k < spokes; k++)
                            {
                                float a0 = k * Mathf.PI * 2f / spokes, a1 = (k + 1) * Mathf.PI * 2f / spokes;
                                // 거미줄은 살 사이에서 **안쪽으로 처진다** — 가운데를 조금 당겨 곡선처럼 보이게 두 토막으로 긋는다.
                                Vector2 p0 = hub + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                                Vector2 p1 = hub + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                                float am = (a0 + a1) * 0.5f;
                                Vector2 pm = hub + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * r * 0.9f;
                                segs.Add(new Vector4(p0.x, p0.y, pm.x, pm.y));
                                segs.Add(new Vector4(pm.x, pm.y, p1.x, p1.y));
                            }
                        }
                        break;
                    }
                    case PatternKind.Circuit:
                    {
                        // 격자를 따라 꺾이는 회로 선 — 가로·세로 토막을 이어 붙이고 끝에 단자(점)를 둔다.
                        int lines = surface == PatternSurface.Sleeve || surface == PatternSurface.Leg ? 3 : 7;
                        for (int i = 0; i < lines; i++)
                        {
                            Vector2 cur = new Vector2(0.1f + 0.8f * (float)rng.NextDouble(), 0.05f + 0.9f * (float)rng.NextDouble());
                            for (int step = 0; step < 4; step++)
                            {
                                bool horiz = (step + i) % 2 == 0;
                                float len = 0.1f + 0.25f * (float)rng.NextDouble();
                                float dir = rng.NextDouble() < 0.5 ? -1f : 1f;
                                Vector2 next = horiz ? new Vector2(Mathf.Clamp01(cur.x + dir * len), cur.y)
                                                     : new Vector2(cur.x, Mathf.Clamp01(cur.y + dir * len));
                                segs.Add(new Vector4(cur.x, cur.y, next.x, next.y));
                                cur = next;
                            }
                            dots.Add(new Vector3(cur.x, cur.y, 1f));
                        }
                        break;
                    }
                }
            }
        }

        // ── 무늬별 음영 ──

        private static Color Shade(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            switch (c.kind)
            {
                case PatternKind.Placket: return Placket(c, u, v, front);
                case PatternKind.Polo: return Polo(c, u, v, front);
                case PatternKind.Vest: return Vest(c, u, v, front, s, false);
                case PatternKind.WesternVest:
                    // 소매는 조끼 속 체크 셔츠다(조끼에는 소매가 없다).
                    return c.surface == PatternSurface.Sleeve ? Plaid(u * 2f, v * 2f) : Vest(c, u, v, front, Plaid(u, v), true);
                case PatternKind.Stripes:
                {
                    int bands = c.surface == PatternSurface.Leg ? 10 : c.surface == PatternSurface.Sleeve ? 7 : 9;
                    return (Mathf.FloorToInt(v * bands) % 2 == 1) ? s : p;
                }
                case PatternKind.LabCoat: return LabCoat(c, u, v, front);
                case PatternKind.Galaxy: return Galaxy(c, u, v, front);
                case PatternKind.Leaf: return Leaf(c, u, v);
                case PatternKind.Flame: return Flame(c, u, v, front);
                case PatternKind.Web: return Web(c, u, v);
                case PatternKind.Ninja: return Ninja(c, u, v, front);
                case PatternKind.PirateCoat: return Pirate(c, u, v, front);
                case PatternKind.Circuit: return Circuit(c, u, v);
                case PatternKind.Camo: return Camo(p, s, u, v, front ? 0 : 17);
                case PatternKind.Cargo: return Cargo(c, u, v);
                case PatternKind.Denim: return Denim(c, u, v);
                case PatternKind.Sheen:
                {
                    float d = Mathf.Abs(u - 0.5f);
                    float k = 0.82f + 0.45f * Mathf.Exp(-(u - 0.43f) * (u - 0.43f) / 0.004f) - 0.25f * d;
                    return Scale(p, k);
                }
                case PatternKind.SideStripe:
                {
                    float d = Mathf.Min(Mathf.Abs(u - 0.25f), Mathf.Abs(u - 0.75f));
                    if (d < 0.045f) return s;
                    if (d < 0.058f) return Scale(p, 0.6f);
                    return p;
                }
                case PatternKind.AnkleWrap:
                {
                    if (v > 0.32f) return p;
                    float band = Frac(u * 3f + v * 7f);
                    return band < 0.5f ? Color.Lerp(p, s, 0.55f) : Scale(p, 0.8f);
                }
                case PatternKind.BootSole: return BootSole(c, u, v, front);
                case PatternKind.Sneaker: return Sneaker(c, u, v, front);
                case PatternKind.RocketBoot: return RocketBoot(c, u, v, front);
                case PatternKind.Crystal: return Crystal(c, u, v);
                case PatternKind.Western: return WesternBoot(c, u, v, front);
                case PatternKind.JacketTrim: return JacketTrim(c, u, v, front);
                case PatternKind.RainCoat: return RainCoat(c, u, v, front);
                case PatternKind.Windbreaker: return Windbreaker(c, u, v, front);
                case PatternKind.ShadowCoat: return ShadowCoat(c, u, v, front);
                case PatternKind.WizardRobe: return WizardRobe(c, u, v, front);
                case PatternKind.LabOuter: return LabOuter(c, u, v, front);
                default: return p;
            }
        }

        // ── 상의 ──

        private static Color Placket(Ctx c, float u, float v, bool front)
        {
            Color p = c.p;
            if (v > 0.93f) return Scale(p, 0.9f);                       // 목둘레
            if (!front) return p;
            if (Mathf.Abs(u - 0.5f) < 0.012f) return Scale(p, 0.85f);   // 단추 여밈선
            for (int i = 0; i < 4; i++)
            {
                if (Circle(u, v, 0.5f, 0.2f + i * 0.19f, 0.022f, c)) return Scale(p, 0.62f);
            }
            // 옷깃 — 목둘레에서 양옆으로 내려오는 삼각형
            float cu = Mathf.Abs(u - 0.5f);
            if (v > 0.8f && cu < 0.2f && (v - 0.8f) > cu * 0.55f) return Scale(p, 0.92f);
            return p;
        }

        private static Color Polo(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            if (v > 0.9f) return s;                                      // 흰 깃
            if (!front) return p;
            float cu = Mathf.Abs(u - 0.5f);
            if (v > 0.62f && cu < 0.032f)
            {
                if (Circle(u, v, 0.5f, 0.7f, 0.016f, c) || Circle(u, v, 0.5f, 0.8f, 0.016f, c)) return s;
                if (cu > 0.024f || v < 0.63f) return Scale(p, 0.8f);    // 여밈판 테두리
            }
            return p;
        }

        /// <summary>조끼 — 가운데 속셔츠(흰 셔츠·체크 셔츠) + 양옆 조끼 + 주머니. 셔츠 판에서는 속셔츠 폭을 넓힌다.</summary>
        private static Color Vest(Ctx c, float u, float v, bool front, Color shirt, bool leather)
        {
            Color p = c.p;
            if (!front)
            {
                if (leather && Mathf.Abs(v - 0.8f) < 0.01f && Dashed(u, 0.04f)) return new Color(0.85f, 0.72f, 0.45f);
                return p;
            }
            float open = c.surface == PatternSurface.Panel ? 0.3f : 0.13f;
            float cu = Mathf.Abs(u - 0.5f);
            // 속셔츠는 아래로 갈수록 조끼 앞섶이 열려 조금 더 넓게 보인다
            float edge = open + (1f - v) * 0.04f;
            if (cu < edge) return shirt;
            if (cu < edge + 0.02f) return Scale(p, 0.72f);               // 앞섶 테두리
            if (leather && cu < edge + 0.04f && Dashed(v, 0.05f)) return new Color(0.85f, 0.72f, 0.45f);   // 스티치
            if (c.surface == PatternSurface.Torso && v > 0.18f && v < 0.36f)
            {
                float pu = cu - 0.28f;                                   // 주머니 중심 ±0.28
                if (Mathf.Abs(pu) < 0.08f)
                {
                    if (Mathf.Abs(pu) > 0.068f || v < 0.195f || Mathf.Abs(v - 0.31f) < 0.012f) return Scale(p, 0.75f);
                }
            }
            return p;
        }

        private static Color Plaid(float u, float v)
        {
            Color baseC = new Color(0.94f, 0.9f, 0.82f);
            Color red = new Color(0.72f, 0.2f, 0.18f);
            bool bu = Frac(u * 8f) < 0.28f;
            bool bv = Frac(v * 8f) < 0.28f;
            if (bu && bv) return Color.Lerp(red, Color.black, 0.25f);
            if (bu || bv) return Color.Lerp(baseC, red, 0.6f);
            return baseC;
        }

        private static Color LabCoat(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            if (c.surface == PatternSurface.Sleeve) return v < 0.12f ? Scale(p, 0.9f) : p;
            if (!front) return (v < 0.3f && Mathf.Abs(u - 0.5f) < 0.006f) ? Scale(p, 0.8f) : p;
            if (Mathf.Abs(u - 0.5f) < 0.01f) return Scale(p, 0.84f);
            for (int i = 0; i < 4; i++)
                if (Circle(u, v, 0.5f, 0.18f + i * 0.2f, 0.018f, c)) return Scale(s, 0.85f);
            // 가슴 주머니 + 펜
            if (u > 0.64f && u < 0.66f && v > 0.66f && v < 0.8f) return new Color(0.2f, 0.35f, 0.8f);
            if (u > 0.6f && u < 0.8f && v > 0.58f && v < 0.72f && (u < 0.608f || u > 0.792f || v < 0.588f || v > 0.71f))
                return Scale(s, 0.9f);
            return p;
        }

        private static Color Galaxy(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            Color col = Color.Lerp(p, Color.Lerp(p, s, 0.7f), v);
            float cx = front ? 0.62f : 0.35f, cy = front ? 0.58f : 0.45f;
            float neb = Mathf.Exp(-((u - cx) * (u - cx) + (v - cy) * (v - cy)) / 0.02f);
            col = Color.Lerp(col, Scale(s, 1.3f), neb * 0.6f);
            float su = front ? 0f : 0.37f;
            for (int i = 0; i < c.dots.Count; i++)
            {
                Vector3 d = c.dots[i];
                float dx = (u - Frac(d.x + su)) / c.texelU, dy = (v - d.y) / c.texelV;
                float d2 = dx * dx + dy * dy;
                if (d.z > 0.5f)
                {
                    bool cross = (Mathf.Abs(dx) < 0.8f && Mathf.Abs(dy) < 4.5f) || (Mathf.Abs(dy) < 0.8f && Mathf.Abs(dx) < 4.5f);
                    if (cross || d2 < 2.2f) return new Color(1f, 0.96f, 0.7f);
                }
                else if (d2 < 1.1f) col = Color.Lerp(col, Color.white, 0.9f);
            }
            return col;
        }

        private static Color Leaf(Ctx c, float u, float v)
        {
            Color p = c.p, s = c.s;
            for (int i = 0; i < c.dots.Count; i++)
            {
                Vector3 d = c.dots[i];
                float ca = Mathf.Cos(d.z), sa = Mathf.Sin(d.z);
                float du = u - d.x, dv = v - d.y;
                float lx = du * ca + dv * sa, ly = -du * sa + dv * ca;
                float len = 0.075f, wid = 0.03f;
                float e = (lx * lx) / (len * len) + (ly * ly) / (wid * wid);
                if (e < 1f)
                {
                    if (Mathf.Abs(ly) < 0.004f) return Scale(s, 0.72f);    // 잎맥
                    return Color.Lerp(s, p, 0.15f + 0.2f * e);
                }
            }
            return p;
        }

        private static Color Flame(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            Color dark = Scale(p, 0.5f);
            float phase = front ? 0f : 1.7f;
            bool sleeve = c.surface == PatternSurface.Sleeve;
            float baseH = sleeve ? 0.28f : 0.38f;
            float f = baseH + 0.14f * Mathf.Sin(u * Mathf.PI * 2f * 3f + phase) + 0.07f * Mathf.Sin(u * Mathf.PI * 2f * 7f + phase * 2f);
            if (v > f) return dark;
            float t = v / Mathf.Max(0.01f, f);                             // 0 밑단 → 1 불꽃 끝
            if (f - v < 0.025f) return Color.Lerp(p, dark, 0.3f);
            return Color.Lerp(Scale(s, 1.05f), p, t * t);
        }

        private static Color Web(Ctx c, float u, float v)
        {
            Color p = c.p, s = c.s;
            if (c.surface != PatternSurface.Sleeve && (u < 0.13f || u > 0.87f)) p = s;   // 옆구리 패널
            float best = SegDistTexels(c, u, v);
            return best < 1.1f ? new Color(0.08f, 0.06f, 0.1f) : p;
        }

        private static Color Ninja(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            if (c.surface == PatternSurface.Sleeve) return v < 0.12f ? Color.Lerp(p, s, 0.6f) : p;
            if (v > 0.08f && v < 0.2f) return (front && Mathf.Abs(u - 0.5f) < 0.05f) ? Scale(s, 0.7f) : s;   // 띠 + 매듭
            if (front)
            {
                // 겹쳐 여민 앞섶(왼쪽 위 → 오른쪽 아래)
                float line = Mathf.Lerp(0.28f, 0.64f, (1f - v) / 0.8f);
                float d = Mathf.Abs(u - line);
                if (v > 0.2f && d < 0.022f) return Color.Lerp(p, s, 0.55f);
            }
            return p;
        }

        private static Color Pirate(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            if (c.surface == PatternSurface.Sleeve) return (v > 0.06f && v < 0.16f) ? s : p;
            if (v < 0.05f) return s;                                          // 금테 밑단
            if (!front) return (Circle(u, v, 0.4f, 0.42f, 0.03f, c) || Circle(u, v, 0.6f, 0.42f, 0.03f, c)) ? s : p;
            float cu = Mathf.Abs(u - 0.5f);
            if (cu < 0.015f) return Scale(p, 0.55f);
            if (cu < 0.035f) return s;                                        // 앞섶 금테
            for (int i = 0; i < 4; i++)
            {
                float vy = 0.3f + i * 0.15f;
                if (Circle(u, v, 0.36f, vy, 0.028f, c) || Circle(u, v, 0.64f, vy, 0.028f, c))
                    return Circle(u, v, 0.36f, vy, 0.018f, c) || Circle(u, v, 0.64f, vy, 0.018f, c) ? s : Scale(s, 0.6f);
            }
            if (v > 0.8f && cu < 0.2f && (v - 0.8f) > (cu - 0.035f) * 0.9f) return Scale(p, 0.75f);   // 라펠
            return p;
        }

        private static Color Circuit(Ctx c, float u, float v)
        {
            Color p = c.p, s = c.s;
            float best = SegDistTexels(c, u, v);
            for (int i = 0; i < c.dots.Count; i++)
            {
                float dx = (u - c.dots[i].x) / c.texelU, dy = (v - c.dots[i].y) / c.texelV;
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy) - 2.2f);
            }
            if (best < 0.9f) return Scale(s, 1.1f);
            if (best < 2.6f) return Color.Lerp(p, s, 0.3f);                  // 번짐(발광처럼 보이게)
            return p;
        }

        private static Color Camo(Color p, Color s, float u, float v, int seed)
        {
            float n = Fbm(u * 5f, v * 5f, seed);
            if (n < 0.42f) return p;
            if (n < 0.6f) return s;
            if (n < 0.72f) return new Color(0.55f, 0.5f, 0.36f);
            return Scale(s, 0.7f);
        }

        // ── 하의 ──

        private static Color Cargo(Ctx c, float u, float v)
        {
            Color p = c.p;
            float d = Mathf.Min(Mathf.Abs(u - 0.25f), Mathf.Abs(u - 0.75f));
            if (d < 0.004f) return Scale(p, 0.78f);                          // 옆솔기
            if (v > 0.4f && v < 0.64f && d < 0.1f)
            {
                if (d > 0.088f || v < 0.412f || Mathf.Abs(v - 0.57f) < 0.012f) return Scale(p, 0.72f);   // 주머니 + 덮개
                return Scale(p, 0.95f);
            }
            return p;
        }

        private static Color Denim(Ctx c, float u, float v)
        {
            Color p = c.p;
            float twill = Hash(Mathf.FloorToInt((u + v) * 90f), Mathf.FloorToInt((u - v) * 30f), 3) * 0.1f - 0.05f;
            Color col = Scale(p, 1f + twill);
            float knee = Mathf.Exp(-((u - 0.5f) * (u - 0.5f) / 0.01f + (v - 0.5f) * (v - 0.5f) / 0.02f));
            col = Color.Lerp(col, Color.Lerp(p, Color.white, 0.35f), knee * 0.5f);
            float d = Mathf.Min(Mathf.Abs(u - 0.25f), Mathf.Abs(u - 0.75f));
            if (d < 0.01f && Dashed(v, 0.06f)) return new Color(0.86f, 0.68f, 0.32f);   // 주황 스티치
            return col;
        }

        // ── 신발 ──

        private static Color Sole(Ctx c, float u, float v, Color sole, float top)
        {
            if (v < top) return Mathf.Abs(v - top) < 0.035f ? Scale(sole, 1.25f) : sole;
            return c.p;
        }

        private static Color BootSole(Ctx c, float u, float v, bool front)
        {
            Color p = c.p;
            if (v < 0.24f) return Sole(c, u, v, new Color(0.17f, 0.12f, 0.08f), 0.24f);
            if (front && v > 0.5f && Mathf.Abs(u - 0.5f) < 0.2f)
            {
                // 끈 — X자로 엇갈린 두 선
                float t = (v - 0.5f) * 5f;
                float a = Mathf.Abs(Frac(t) - 0.5f) * 0.4f;
                if (Mathf.Abs(Mathf.Abs(u - 0.5f) - a) < 0.018f) return new Color(0.88f, 0.82f, 0.66f);
            }
            if (!front && v < 0.45f) return Scale(p, 0.85f);                  // 뒤꿈치 보강
            return p;
        }

        private static Color Sneaker(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            if (v < 0.26f) return Mathf.Abs(v - 0.16f) < 0.025f ? s : new Color(0.93f, 0.93f, 0.92f);
            if (front)
            {
                if (v < 0.5f) return Scale(p, 0.94f);                         // 앞코
                if (Mathf.Abs(u - 0.5f) < 0.18f && Frac((v - 0.5f) * 8f) < 0.3f) return Scale(p, 0.8f);   // 끈
                return p;
            }
            float curve = 0.45f + 0.18f * Mathf.Sin(u * Mathf.PI);
            if (Mathf.Abs(v - curve) < 0.055f) return s;                       // 옆 스우시
            return p;
        }

        private static Color RocketBoot(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            if (v < 0.2f) return new Color(0.18f, 0.18f, 0.2f);
            if (front)
                return (v < 0.58f && Mathf.Abs(u - 0.5f) < 0.3f) ? new Color(0.72f, 0.74f, 0.78f) : p;   // 금속 앞코
            float f = 0.3f + 0.16f * Mathf.Sin(u * Mathf.PI * 6f) + 0.06f * Mathf.Sin(u * Mathf.PI * 14f);
            if (v < f) return Color.Lerp(new Color(1f, 0.92f, 0.3f), s, (v - 0.2f) / Mathf.Max(0.01f, f - 0.2f));
            return p;
        }

        private static Color Crystal(Ctx c, float u, float v)
        {
            Color p = c.p, s = c.s;
            if (c.surface == PatternSurface.Boot && v < 0.18f) return s;
            float a = Frac((u + v) * 4f), b = Frac((u - v) * 4f + 10f);
            Color col = p;
            if (a < 0.5f) col = Scale(p, 1.06f);
            if (a < 0.035f || b < 0.035f) col = Color.Lerp(p, Color.white, 0.7f);
            for (int i = 0; i < c.dots.Count; i++)
            {
                float dx = (u - c.dots[i].x) / c.texelU, dy = (v - c.dots[i].y) / c.texelV;
                if ((Mathf.Abs(dx) < 0.7f && Mathf.Abs(dy) < 4f) || (Mathf.Abs(dy) < 0.7f && Mathf.Abs(dx) < 4f)) return Color.white;
            }
            return col;
        }

        private static Color WesternBoot(Ctx c, float u, float v, bool front)
        {
            Color p = c.p;
            if (v < 0.2f) return Sole(c, u, v, new Color(0.22f, 0.14f, 0.08f), 0.2f);
            if (front) return v < 0.55f ? Scale(p, 0.88f) : p;
            float curve = 0.74f + 0.1f * Mathf.Sin(u * Mathf.PI * 4f);
            if (Mathf.Abs(v - curve) < 0.016f || Mathf.Abs(v - curve + 0.1f) < 0.012f) return new Color(0.9f, 0.78f, 0.5f);
            return p;
        }

        // ── 겉옷 ──

        private static Color Cuff(Ctx c, float v, float k)
        {
            return v < 0.1f ? Scale(c.p, k) : c.p;
        }

        private static Color JacketTrim(Ctx c, float u, float v, bool front)
        {
            Color p = c.p;
            if (c.surface == PatternSurface.Sleeve) return Cuff(c, v, 0.78f);
            if (v < 0.06f) return Scale(p, 0.82f);                             // 밑단 시보리
            if (!front) return Mathf.Abs(v - 0.8f) < 0.008f ? Scale(p, 0.8f) : p;
            float cu = Mathf.Abs(u - 0.5f);
            if (cu > 0.2f && cu < 0.4f && Mathf.Abs(v - 0.3f) < 0.02f) return Scale(p, 0.7f);   // 주머니 덮개
            return p;
        }

        private static Color RainCoat(Ctx c, float u, float v, bool front)
        {
            Color p = c.p;
            if (c.surface == PatternSurface.Sleeve) return Cuff(c, v, 0.85f);
            if (Mathf.Abs(v - 0.2f) < 0.022f) return new Color(0.82f, 0.84f, 0.86f);   // 반사띠
            if (!front) return p;
            float cu = Mathf.Abs(u - 0.5f);
            float slant = 0.36f + (cu - 0.3f) * 0.6f;
            if (cu > 0.24f && cu < 0.4f && Mathf.Abs(v - slant) < 0.012f) return Scale(p, 0.7f);   // 사선 주머니
            return p;
        }

        private static Color Windbreaker(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            if (c.surface == PatternSurface.Sleeve)
            {
                float d = Mathf.Min(Mathf.Abs(u - 0.25f), Mathf.Abs(u - 0.75f));   // 양팔 바깥쪽
                if (d < 0.03f || (d > 0.05f && d < 0.07f)) return s;
                return Cuff(c, v, 0.8f);
            }
            if (Mathf.Abs(v - 0.62f) < 0.035f) return s;                        // 가슴 띠
            if (v < 0.05f) return Scale(p, 0.8f);
            return p;
        }

        private static Color ShadowCoat(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            if (c.surface == PatternSurface.Sleeve) return v < 0.1f ? Color.Lerp(p, s, 0.7f) : p;
            if (v < 0.04f) return Color.Lerp(p, s, 0.7f);
            float d = Mathf.Min(Mathf.Abs(u - 0.3f), Mathf.Abs(u - 0.7f));
            return d < 0.006f ? Color.Lerp(p, s, 0.35f) : p;                    // 솔기
        }

        private static Color WizardRobe(Ctx c, float u, float v, bool front)
        {
            Color p = c.p, s = c.s;
            if (c.surface == PatternSurface.Sleeve && v < 0.16f) return s;
            if (v < 0.07f) return s;                                            // 금테
            for (int i = 0; i < c.dots.Count; i++)
            {
                Vector3 d = c.dots[i];
                if (d.z < 0.5f && i % 4 != 0) continue;                        // 드문드문
                float dx = (u - d.x) / c.texelU, dy = (v - d.y) / c.texelV;
                float r2 = dx * dx + dy * dy;
                if (i % 8 == 0)
                {
                    // 초승달: 큰 원에서 어긋난 원을 뺀다
                    float ox = dx - 2.2f, r2b = ox * ox + dy * dy;
                    if (r2 < 30f && r2b > 22f) return Scale(s, 1.15f);
                }
                else if ((Mathf.Abs(dx) < 0.8f && Mathf.Abs(dy) < 3.5f) || (Mathf.Abs(dy) < 0.8f && Mathf.Abs(dx) < 3.5f))
                    return Scale(s, 1.2f);
            }
            return p;
        }

        private static Color LabOuter(Ctx c, float u, float v, bool front)
        {
            Color p = c.p;
            if (c.surface == PatternSurface.Sleeve) return Cuff(c, v, 0.9f);
            if (!front) return p;
            float cu = Mathf.Abs(u - 0.5f);
            if (cu > 0.2f && cu < 0.4f && v > 0.14f && v < 0.3f && (cu < 0.21f || cu > 0.39f || v < 0.15f || v > 0.29f))
                return Scale(p, 0.82f);                                         // 아래 주머니
            if (u > 0.22f && u < 0.24f && v > 0.62f && v < 0.76f) return new Color(0.2f, 0.35f, 0.8f);   // 펜
            return p;
        }

        // ── 도구 ──

        private static float SegDistTexels(Ctx c, float u, float v)
        {
            float best = 1e9f;
            for (int i = 0; i < c.segs.Count; i++)
            {
                Vector4 sg = c.segs[i];
                float ax = sg.x, ay = sg.y, bx = sg.z - sg.x, by = sg.w - sg.y;
                float px = u - ax, py = v - ay;
                float t = Mathf.Clamp01((px * bx + py * by) / Mathf.Max(1e-8f, bx * bx + by * by));
                float dx = (px - bx * t) / c.texelU, dy = (py - by * t) / c.texelV;
                float d = dx * dx + dy * dy;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        private static bool Circle(float u, float v, float cu, float cv, float r, Ctx c)
        {
            // 아틀라스 앞면은 가로 텍셀이 세로의 2배 밀도(256×128의 절반 = 128×128)라 u·v 스케일이 같다.
            float du = u - cu, dv = v - cv;
            return du * du + dv * dv < r * r;
        }

        private static bool Dashed(float t, float period) => Frac(t / period) < 0.55f;

        private static Color Scale(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        private static float Frac(float x) => x - Mathf.Floor(x);

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed);
            float c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float x, float y, int seed)
        {
            return ValueNoise(x, y, seed) * 0.65f + ValueNoise(x * 2.3f, y * 2.3f, seed + 7) * 0.35f;
        }
    }
}
