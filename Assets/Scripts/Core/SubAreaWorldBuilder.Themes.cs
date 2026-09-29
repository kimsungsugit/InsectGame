using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 서브에리어 테마 변주 — 같은 environmentType을 공유하는 방들이 <b>같은 방으로 보이지 않게</b> 한다.
    ///
    /// 배치 캡처로 확인한 것: 동굴형 9곳(초원·숲·습지·산 동굴, 유적 지하, 마른 굴, 개미귀신 구덩이, 잿불 굴뚝,
    /// 빈칸)이 한 개의 갈색 상자 미로였고, 산 정상형 3곳(산 정상·서리 능선·우듬지 "가장 높은 가지")이 같은
    /// 회색 상자였다. 나무 꼭대기가 바위산으로, 개미귀신 구덩이가 동굴로 나왔다.
    ///
    /// <b>environmentType 데이터는 바꾸지 않는다.</b> 그 값은 조명 프로필(<see cref="SubAreaEnvironment"/>),
    /// 환경음(<c>AudioManager.PlayAmbient</c>), 지도 색(<c>RegionMapUI</c>), 스토리 연출 좌표 검사
    /// (story_lint 21 — 빌더별 방 크기를 이 파일이 아니라 본 파일의 switch로 읽는다)에 묶여 있다.
    /// 그래서 빌더 <b>안에서</b> subAreaId로 갈라지고, 방 크기(<c>CreateBoundaryWalls</c>의 halfSize)는
    /// 원래 빌더와 같게 둔다 — 그래야 story_lint가 읽는 방 크기가 거짓이 되지 않는다.
    ///
    /// 장식은 전부 콜라이더를 뺀다. 연출 배우(Scripted 이동)는 콜라이더에 막히면 화면에 안 나오고,
    /// 입구(0,0.5,-8) 주변은 <c>FindSafeSpawnPosition</c>이 비어 있어야 한다.
    /// </summary>
    public partial class SubAreaWorldBuilder
    {
        // ======================= 동굴 =======================

        private enum CaveStyle { Earth, Moss, Wet, Crystal, Ancient, Dry, Ember }

        private struct CaveTheme
        {
            public CaveStyle style;
            public Color wall, floor, flame, light;
            public bool torches;
        }

        private static CaveTheme CaveThemeFor(string subAreaId)
        {
            switch (subAreaId)
            {
                case "forest_cave":
                    return new CaveTheme { style = CaveStyle.Moss, wall = new Color(0.30f, 0.33f, 0.24f), floor = new Color(0.24f, 0.28f, 0.18f),
                        flame = new Color(1f, 0.72f, 0.3f), light = new Color(0.85f, 0.9f, 0.6f), torches = true };
                case "swamp_cave":
                    return new CaveTheme { style = CaveStyle.Wet, wall = new Color(0.26f, 0.30f, 0.27f), floor = new Color(0.20f, 0.24f, 0.20f),
                        flame = new Color(0.7f, 1f, 0.5f), light = new Color(0.6f, 0.9f, 0.55f), torches = false };
                case "mountain_cave":
                    return new CaveTheme { style = CaveStyle.Crystal, wall = new Color(0.36f, 0.37f, 0.42f), floor = new Color(0.28f, 0.29f, 0.33f),
                        flame = new Color(0.6f, 0.85f, 1f), light = new Color(0.62f, 0.72f, 1f), torches = false };
                case "ruins_underground":
                    return new CaveTheme { style = CaveStyle.Ancient, wall = new Color(0.56f, 0.50f, 0.40f), floor = new Color(0.42f, 0.38f, 0.30f),
                        flame = new Color(1f, 0.78f, 0.35f), light = new Color(1f, 0.8f, 0.45f), torches = true };
                case "hollow_burrow":
                    return new CaveTheme { style = CaveStyle.Dry, wall = new Color(0.56f, 0.52f, 0.44f), floor = new Color(0.46f, 0.42f, 0.35f),
                        flame = new Color(0.95f, 0.85f, 0.6f), light = new Color(0.9f, 0.85f, 0.72f), torches = true };
                case "emberfall_vent":
                    return new CaveTheme { style = CaveStyle.Ember, wall = new Color(0.18f, 0.16f, 0.16f), floor = new Color(0.13f, 0.11f, 0.11f),
                        flame = new Color(1f, 0.5f, 0.15f), light = new Color(1f, 0.5f, 0.2f), torches = false };
                default: // meadow_cave 등 — 흙 동굴(옛 색 그대로)
                    return new CaveTheme { style = CaveStyle.Earth, wall = new Color(0.4f, 0.35f, 0.29f), floor = new Color(0.32f, 0.28f, 0.22f),
                        flame = new Color(1f, 0.7f, 0.2f), light = new Color(1f, 0.7f, 0.3f), torches = true };
            }
        }

        /// <summary>
        /// 미로 동굴의 테마 소품. 벽 칸(maze=1) 위·옆과 빈 칸(maze=0) 바닥에 스타일별 소품을 놓는다.
        /// 입구 칸(3~4, 1~2)은 비워 둔다.
        /// </summary>
        private void DecorateCave(CaveTheme t, int[,] maze, float cell, float offX, float offZ)
        {
            int nx = maze.GetLength(0), nz = maze.GetLength(1);
            // 머티리얼은 여기서 스타일당 한 번만 만든다. 칸·소품마다 Mat()을 부르면 한 번 들어갈 때마다 수십 개가 새로
            // 생기고 같은 색 소품끼리도 배칭이 깨진다. extra는 스타일마다 뜻이 다르다(오른쪽 주석).
            // **여기 넣는 건 개체마다 바꾸지 않는 것만이다** — 공유하므로 루프 안에서 발광·광택을 고치면 전부 같이 바뀐다.
            Material accent = null, accent2 = null, extra = null, stem = null;
            switch (t.style)
            {
                case CaveStyle.Earth:
                    accent = Mat(new Color(0.36f, 0.26f, 0.16f));                 // 뿌리
                    accent2 = GlowMat(new Color(0.75f, 1f, 0.45f), new Color(0.5f, 0.9f, 0.3f));   // 반딧불이
                    extra = Mat(new Color(0.72f, 0.36f, 0.24f));                  // 바닥 버섯 갓
                    stem = Mat(new Color(0.88f, 0.84f, 0.74f));                   // 버섯 줄기
                    break;
                case CaveStyle.Moss:
                    accent = Mat(new Color(0.26f, 0.46f, 0.20f));                 // 이끼
                    accent2 = GlowMat(new Color(0.45f, 0.85f, 1f), new Color(0.25f, 0.65f, 0.9f));  // 푸른 버섯
                    stem = Mat(new Color(0.88f, 0.84f, 0.74f));                   // 버섯 줄기
                    break;
                case CaveStyle.Wet:
                    accent = Mat(new Color(0.16f, 0.20f, 0.18f));                 // 물웅덩이
                    Wet(accent);
                    accent2 = GlowMat(new Color(0.7f, 1f, 0.55f), new Color(0.4f, 0.8f, 0.3f));     // 포자
                    extra = Mat(new Color(0.32f, 0.36f, 0.33f));                  // 석순
                    break;
                case CaveStyle.Crystal:
                    accent = GlowMat(new Color(0.72f, 0.55f, 1f), new Color(0.55f, 0.32f, 0.95f)); // 자수정
                    accent2 = GlowMat(new Color(0.55f, 0.95f, 1f), new Color(0.25f, 0.7f, 0.9f));  // 청수정
                    break;
                case CaveStyle.Ancient:
                    accent = GlowMat(new Color(1f, 0.82f, 0.38f), new Color(0.75f, 0.5f, 0.12f));  // 새김 띠
                    accent2 = Mat(new Color(0.62f, 0.56f, 0.45f));                // 기둥·돌 토막
                    extra = Mat(new Color(0.48f, 0.44f, 0.35f));                  // 바닥 타일
                    break;
                case CaveStyle.Dry:
                    accent = Mat(new Color(0.50f, 0.44f, 0.34f));                 // 마른 뿌리
                    accent2 = Mat(new Color(0.78f, 0.76f, 0.70f));                // 흰 돌·거미줄
                    extra = Mat(new Color(0.52f, 0.48f, 0.40f));                  // 흙더미
                    break;
                case CaveStyle.Ember:
                    accent = GlowMat(new Color(1f, 0.45f, 0.12f), new Color(1.5f, 0.45f, 0.08f));  // 용암 틈
                    accent2 = Mat(new Color(0.24f, 0.21f, 0.21f));                // 현무암 돌기
                    break;
            }

            for (int x = 0; x < nx; x++)
            {
                for (int z = 0; z < nz; z++)
                {
                    Vector3 c = new Vector3(offX + x * cell, 0f, offZ + z * cell);
                    bool entry = (x == 3 || x == 4) && (z == 1 || z == 2);
                    if (maze[x, z] == 1)
                        DecorateCaveWall(t.style, c, cell, x * 31 + z * 17, accent, accent2, extra, stem);
                    else if (!entry)
                        DecorateCaveFloor(t.style, c, cell, x * 13 + z * 7, accent, accent2, extra, stem);
                }
            }

            if (t.style == CaveStyle.Crystal)
            {
                CreatePointLight(new Vector3(-6f, 1.5f, 6f), new Color(0.7f, 0.5f, 1f), 10f, 1.4f);
                CreatePointLight(new Vector3(7f, 1.5f, -2f), new Color(0.4f, 0.85f, 1f), 10f, 1.4f);
            }
            else if (t.style == CaveStyle.Ember)
            {
                CreatePointLight(new Vector3(-5f, 0.6f, 4f), new Color(1f, 0.45f, 0.15f), 9f, 1.8f);
                CreatePointLight(new Vector3(6f, 0.6f, 7f), new Color(1f, 0.45f, 0.15f), 9f, 1.8f);
            }
        }

        private void DecorateCaveWall(CaveStyle style, Vector3 c, float cell, int seed, Material a, Material b, Material extra, Material stem)
        {
            float h = 5f, half = (cell - 0.2f) * 0.5f;
            // 벽의 네 면 중 하나 — 결정적으로 고른다(미로가 매번 달라도 모양 규칙은 같게)
            int face = seed % 4;
            Vector3 n = face == 0 ? Vector3.forward : face == 1 ? Vector3.back : face == 2 ? Vector3.right : Vector3.left;
            Vector3 onFace = c + n * (half + 0.05f);
            switch (style)
            {
                case CaveStyle.Earth:
                case CaveStyle.Dry:
                    for (int k = 0; k < 3; k++)   // 늘어진 뿌리
                    {
                        Vector3 side = Vector3.Cross(Vector3.up, n) * ((k - 1) * 0.9f);
                        float len = 0.8f + ((seed + k) % 3) * 0.45f;
                        Deco(PrimitiveType.Cylinder, "CaveRoot", onFace + side + Vector3.up * (h - len * 0.5f),
                            Quaternion.Euler((k - 1) * 8f, 0f, (k % 2) * 10f - 5f), new Vector3(0.07f, len * 0.5f, 0.07f), a);
                    }
                    if (style == CaveStyle.Earth && seed % 2 == 0)
                        for (int k = 0; k < 4; k++)
                            Deco(PrimitiveType.Sphere, "Glowworm", onFace + Vector3.up * (2.6f + (k % 2) * 1.3f)
                                + Vector3.Cross(Vector3.up, n) * ((k - 1.5f) * 0.7f), Quaternion.identity, Vector3.one * 0.08f, b);
                    if (style == CaveStyle.Dry && seed % 3 == 0)   // 거미줄 — 흰 얇은 판
                        Deco(PrimitiveType.Cube, "Cobweb", onFace + Vector3.up * 3.6f, Quaternion.LookRotation(n) * Quaternion.Euler(0f, 0f, 30f),
                            new Vector3(1.1f, 0.9f, 0.01f), b);
                    break;
                case CaveStyle.Moss:
                    Deco(PrimitiveType.Cube, "MossCap", c + Vector3.up * (h + 0.08f), Quaternion.identity,
                        new Vector3(cell - 0.1f, 0.22f, cell - 0.1f), a);
                    for (int k = 0; k < 3; k++)   // 벽을 타고 내려오는 이끼
                        Deco(PrimitiveType.Cube, "MossDrape", onFace + Vector3.up * (h - 0.9f) + Vector3.Cross(Vector3.up, n) * ((k - 1) * 1.1f),
                            Quaternion.LookRotation(n), new Vector3(0.7f, 1.6f + (k % 2) * 0.6f, 0.04f), a);
                    if (seed % 2 == 0)
                        for (int k = 0; k < 3; k++)
                            Mushroom(onFace + n * 0.35f + Vector3.Cross(Vector3.up, n) * ((k - 1) * 0.5f), 0.22f + k * 0.06f, stem, b);
                    break;
                case CaveStyle.Wet:
                    for (int k = 0; k < 2; k++)   // 석순
                    {
                        float sh = 0.8f + ((seed + k) % 3) * 0.4f;
                        Deco(PrimitiveType.Capsule, "Stalagmite", onFace + n * 0.4f + Vector3.Cross(Vector3.up, n) * ((k - 0.5f) * 1.4f) + Vector3.up * sh * 0.5f,
                            Quaternion.identity, new Vector3(0.35f, sh * 0.5f, 0.35f), extra);
                    }
                    Deco(PrimitiveType.Cube, "WetSheen", onFace + Vector3.up * 2.5f, Quaternion.LookRotation(n),
                        new Vector3(cell - 0.6f, 4.4f, 0.02f), a);
                    if (seed % 3 == 0)
                        for (int k = 0; k < 5; k++)
                            Deco(PrimitiveType.Sphere, "Spore", onFace + n * 0.6f + Vector3.up * (1.2f + k * 0.55f)
                                + Vector3.Cross(Vector3.up, n) * (((k * 7) % 5 - 2) * 0.4f), Quaternion.identity, Vector3.one * 0.07f, b);
                    break;
                case CaveStyle.Crystal:
                    for (int k = 0; k < 3; k++)
                    {
                        float ch = 0.7f + ((seed + k) % 3) * 0.45f;
                        Deco(PrimitiveType.Cube, "Crystal", onFace + n * 0.3f + Vector3.Cross(Vector3.up, n) * ((k - 1) * 0.6f) + Vector3.up * ch * 0.45f,
                            Quaternion.Euler((k - 1) * 18f, seed * 13f + k * 40f, (k % 2) * 20f - 10f), new Vector3(0.26f, ch, 0.26f), (seed + k) % 2 == 0 ? a : b);
                    }
                    break;
                case CaveStyle.Ancient:
                    Deco(PrimitiveType.Cube, "CarvedBand", onFace + Vector3.up * 2.3f, Quaternion.LookRotation(n),
                        new Vector3(cell - 0.4f, 0.14f, 0.03f), a);
                    Deco(PrimitiveType.Cube, "CarvedBandLow", onFace + Vector3.up * 1.2f, Quaternion.LookRotation(n),
                        new Vector3(cell - 0.4f, 0.06f, 0.03f), a);
                    if (seed % 3 == 0)
                        Deco(PrimitiveType.Cube, "Glyph", onFace + Vector3.up * 3.3f, Quaternion.LookRotation(n) * Quaternion.Euler(0f, 0f, 45f),
                            new Vector3(0.5f, 0.5f, 0.03f), a);
                    break;
                case CaveStyle.Ember:
                    Deco(PrimitiveType.Cube, "EmberVein", onFace + Vector3.up * (1.2f + (seed % 3) * 0.9f), Quaternion.LookRotation(n) * Quaternion.Euler(0f, 0f, (seed % 5) * 14f - 28f),
                        new Vector3(cell * 0.7f, 0.07f, 0.03f), a);
                    for (int k = 0; k < 2; k++)   // 현무암 돌기
                        Deco(PrimitiveType.Cylinder, "BasaltSpur", onFace + n * 0.35f + Vector3.Cross(Vector3.up, n) * ((k - 0.5f) * 1.5f) + Vector3.up * 0.5f,
                            Quaternion.identity, new Vector3(0.45f, 0.5f + k * 0.25f, 0.45f), b);
                    break;
            }
        }

        private void DecorateCaveFloor(CaveStyle style, Vector3 c, float cell, int seed, Material a, Material b, Material extra, Material stem)
        {
            Vector3 jitter = new Vector3(((seed * 7) % 5 - 2) * 0.35f, 0f, ((seed * 3) % 5 - 2) * 0.35f);
            Vector3 p = c + jitter;
            switch (style)
            {
                case CaveStyle.Earth:
                    if (seed % 3 == 0)
                        for (int k = 0; k < 3; k++)
                            Mushroom(p + new Vector3(k * 0.3f, 0f, (k % 2) * 0.25f), 0.2f + k * 0.07f, stem, extra);
                    break;
                case CaveStyle.Moss:
                    Deco(PrimitiveType.Cylinder, "MossCarpet", p + Vector3.up * 0.02f, Quaternion.identity, new Vector3(1.8f, 0.01f, 1.5f), a);
                    break;
                case CaveStyle.Wet:
                    if (seed % 2 == 0)
                        Deco(PrimitiveType.Cylinder, "Puddle", p + Vector3.up * 0.015f, Quaternion.identity, new Vector3(2.2f, 0.01f, 1.7f), a);
                    break;
                case CaveStyle.Crystal:
                    if (seed % 4 == 0)
                        Deco(PrimitiveType.Cube, "CrystalShard", p + Vector3.up * 0.3f, Quaternion.Euler(20f, seed * 23f, 15f), new Vector3(0.2f, 0.6f, 0.2f), b);
                    break;
                case CaveStyle.Ancient:
                    if (seed % 3 == 0)
                        Deco(PrimitiveType.Cube, "FallenBlock", p + Vector3.up * 0.25f, Quaternion.Euler(0f, seed * 29f, 6f), new Vector3(0.9f, 0.5f, 0.6f), b);
                    else
                        Deco(PrimitiveType.Cube, "FloorTile", p + Vector3.up * 0.015f, Quaternion.Euler(0f, 45f, 0f), new Vector3(1.6f, 0.02f, 1.6f), extra);
                    break;
                case CaveStyle.Dry:
                    if (seed % 2 == 0)
                        Deco(PrimitiveType.Sphere, "DustMound", p + Vector3.up * 0.04f, Quaternion.identity, new Vector3(1.4f, 0.16f, 1.1f), extra);
                    else
                        Deco(PrimitiveType.Sphere, "PaleStone", p + Vector3.up * 0.1f, Quaternion.Euler(0f, seed * 17f, 0f), new Vector3(0.45f, 0.22f, 0.35f), b);
                    break;
                case CaveStyle.Ember:
                    Deco(PrimitiveType.Cube, "Fissure", p + Vector3.up * 0.02f, Quaternion.Euler(0f, seed * 37f, 0f), new Vector3(0.14f, 0.02f, cell * 0.8f), a);
                    if (seed % 3 == 0)
                        Deco(PrimitiveType.Cylinder, "LavaPool", p + new Vector3(0.6f, 0.02f, 0.4f), Quaternion.identity, new Vector3(1.2f, 0.01f, 1f), a);
                    break;
            }
        }

        /// <summary>버섯 한 송이. 줄기·갓 머티리얼은 호출부가 한 번 만들어 넘긴다(<see cref="DecorateCave"/> 주석).</summary>
        private void Mushroom(Vector3 at, float h, Material stem, Material cap)
        {
            Deco(PrimitiveType.Cylinder, "MushStem", at + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(0.06f, h * 0.5f, 0.06f), stem);
            Deco(PrimitiveType.Sphere, "MushCap", at + Vector3.up * h, Quaternion.identity, new Vector3(h * 0.9f, h * 0.4f, h * 0.9f), cap);
        }

        // ======================= 개미귀신 구덩이 =======================

        /// <summary>
        /// 모래언덕의 개미귀신 구덩이 — 동굴이 아니라 <b>하늘이 열린 모래 깔때기</b>다. 가운데로 갈수록 짙어지는
        /// 동심원이 파인 경사를, 둘레의 모래 둔덕이 가장자리를 만든다. 방 크기는 동굴과 같은 14(story_lint 21).
        /// </summary>
        private void BuildAntlionPit(SubAreaData sub)
        {
            Material sand = Mat(new Color(0.76f, 0.64f, 0.44f));
            Material wallMat = Mat(new Color(0.70f, 0.58f, 0.40f));
            CreateFloor(sand, 30f);

            Vector3 pit = new Vector3(0f, 0f, 3f);   // 입구(0,-8)에서 11m 앞
            Color[] rings = { new Color(0.71f, 0.59f, 0.40f), new Color(0.64f, 0.52f, 0.35f), new Color(0.56f, 0.45f, 0.30f),
                new Color(0.46f, 0.36f, 0.23f), new Color(0.32f, 0.24f, 0.15f) };
            float[] radii = { 9.5f, 7.2f, 5f, 3f, 1.3f };
            for (int i = 0; i < rings.Length; i++)
                Deco(PrimitiveType.Cylinder, $"PitRing_{i}", pit + Vector3.up * (0.012f + i * 0.006f), Quaternion.identity,
                    new Vector3(radii[i] * 2f, 0.005f, radii[i] * 2f), Mat(rings[i]));
            // 흘러내린 모래 자국 — 가장자리에서 중심으로 향하는 줄
            Material streak = Mat(new Color(0.80f, 0.69f, 0.49f));
            for (int i = 0; i < 14; i++)
            {
                float a = i * 360f / 14f;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Deco(PrimitiveType.Cube, "SandStreak", pit + dir * 6f + Vector3.up * 0.05f, Quaternion.LookRotation(dir),
                    new Vector3(0.18f, 0.02f, 4.2f), streak);
            }
            // 구덩이 둘레의 모래 둔덕 + 벽 밑을 덮는 사구(상자 모서리를 숨긴다)
            Material dune = Mat(new Color(0.80f, 0.68f, 0.47f));
            for (int i = 0; i < 12; i++)
            {
                float a = (i + 0.5f) * 30f * Mathf.Deg2Rad;
                Vector3 at = pit + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 10.6f;
                if (at.z < -6f && Mathf.Abs(at.x) < 4f) continue;   // 입구 쪽은 트인다
                Deco(PrimitiveType.Sphere, "PitRim", at + Vector3.down * 0.35f, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f),
                    new Vector3(2.4f, 1.3f, 4.2f), dune);
            }
            for (int i = 0; i < 16; i++)
            {
                float t = i / 16f * 4f;
                int side = Mathf.FloorToInt(t);
                float u = (t - side) * 26f - 13f;
                Vector3 at = side == 0 ? new Vector3(u, 0f, 13f) : side == 1 ? new Vector3(13f, 0f, u)
                    : side == 2 ? new Vector3(-u, 0f, -13f) : new Vector3(-13f, 0f, -u);
                if (at.z < -12f && Mathf.Abs(at.x) < 3f) continue;
                Deco(PrimitiveType.Sphere, "WallDune", at + Vector3.down * 0.4f, Quaternion.Euler(0f, side * 90f, 0f),
                    new Vector3(5.5f, 3.2f, 3.2f), i % 3 == 0 ? wallMat : dune);
            }
            // 반쯤 묻힌 명부회 화물 상자와 마른 덤불
            Material crate = Mat(new Color(0.50f, 0.38f, 0.24f));
            Deco(PrimitiveType.Cube, "BuriedCrate", new Vector3(8.5f, 0.2f, 9f), Quaternion.Euler(8f, 25f, 12f), new Vector3(1.2f, 0.9f, 1.1f), crate);
            Deco(PrimitiveType.Cube, "BuriedCrate", new Vector3(-9f, 0.1f, 7.5f), Quaternion.Euler(-10f, -30f, 6f), new Vector3(1.1f, 0.8f, 1.1f), crate);
            Material twig = Mat(new Color(0.46f, 0.38f, 0.25f));
            for (int i = 0; i < 18; i++)
            {
                Vector3 at = new Vector3(Mathf.Sin(i * 2.4f) * 11f, 0.3f, Mathf.Cos(i * 1.7f) * 11f);
                if (at.z < -5f && Mathf.Abs(at.x) < 4f) continue;
                Deco(PrimitiveType.Cylinder, "DryTwig", at, Quaternion.Euler(i * 23f % 50f - 25f, i * 40f, i * 17f % 40f - 20f), new Vector3(0.05f, 0.35f, 0.05f), twig);
            }

            CreatePointLight(new Vector3(0f, 8f, 0f), new Color(1f, 0.92f, 0.75f), 26f, 0.9f);
            CreateBoundaryWalls(wallMat, 14f, 3.5f);
        }

        // ======================= 빈칸 =======================

        /// <summary>
        /// 이름 없는 자리의 끝(nameless_core) — 아무것도 적히지 않은 <b>장부의 한 페이지</b>. 창백한 바닥에 줄이 그어져
        /// 있고, 빈 석판이 둘러서 있고, 가운데엔 틀만 남은 문이 있다. 최종 대면 연출(fin_unnamed)의 무대라 입구에서
        /// 가운데까지는 아무것도 막지 않는다(전부 콜라이더 없음). 방 크기는 동굴과 같은 14.
        /// </summary>
        private void BuildBlankCore(SubAreaData sub)
        {
            Material paper = Mat(new Color(0.80f, 0.79f, 0.84f));
            Material wallMat = Mat(new Color(0.70f, 0.69f, 0.76f));
            Material rule = Mat(new Color(0.62f, 0.60f, 0.74f));
            Material slab = Mat(new Color(0.88f, 0.87f, 0.92f));
            Material frame = Mat(new Color(0.46f, 0.44f, 0.56f));
            Material mote = GlowMat(new Color(0.92f, 0.90f, 1f), new Color(0.6f, 0.58f, 0.8f));
            CreateFloor(paper, 30f);

            // 괘선 — 장부의 줄. 가로줄만 긋고 왼쪽에 세로 여백선 하나
            for (int i = -4; i <= 4; i++)
                Deco(PrimitiveType.Cube, "RuledLine", new Vector3(0f, 0.012f, i * 3f), Quaternion.identity, new Vector3(26f, 0.01f, 0.06f), rule);
            Deco(PrimitiveType.Cube, "MarginLine", new Vector3(-9.5f, 0.013f, 0f), Quaternion.identity, new Vector3(0.08f, 0.01f, 26f), GlowMat(new Color(0.85f, 0.55f, 0.6f), new Color(0.35f, 0.12f, 0.15f)));

            // 빈 석판 원 — 입구 쪽(남쪽 90°)은 비운다
            for (int i = 0; i < 12; i++)
            {
                float deg = i * 30f + 15f;
                if (deg > 225f && deg < 315f) continue;
                float a = deg * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Cos(a) * 9.5f, 0f, Mathf.Sin(a) * 9.5f + 1f);
                float h = 2.2f + (i % 3) * 0.5f;
                Quaternion face = Quaternion.Euler(0f, -(deg + 90f), 0f);
                Deco(PrimitiveType.Cube, "BlankSlab", at + Vector3.up * h * 0.5f, face, new Vector3(1.3f, h, 0.24f), slab);
                Deco(PrimitiveType.Cube, "BlankSlabFrame", at + Vector3.up * h * 0.55f, face,
                    new Vector3(0.95f, h * 0.62f, 0.26f), frame);
                Deco(PrimitiveType.Cube, "BlankSlabFace", at + Vector3.up * h * 0.55f, face, new Vector3(0.82f, h * 0.54f, 0.28f), slab);
            }

            // 틀만 남은 문 — 이름을 적을 자리
            Vector3 door = new Vector3(0f, 0f, 5f);
            Deco(PrimitiveType.Cube, "EmptyDoorL", door + new Vector3(-1.1f, 1.5f, 0f), Quaternion.identity, new Vector3(0.22f, 3f, 0.22f), frame);
            Deco(PrimitiveType.Cube, "EmptyDoorR", door + new Vector3(1.1f, 1.5f, 0f), Quaternion.identity, new Vector3(0.22f, 3f, 0.22f), frame);
            Deco(PrimitiveType.Cube, "EmptyDoorTop", door + new Vector3(0f, 3.05f, 0f), Quaternion.identity, new Vector3(2.44f, 0.22f, 0.22f), frame);
            Deco(PrimitiveType.Cube, "EmptyDoorGlow", door + new Vector3(0f, 1.5f, 0f), Quaternion.identity, new Vector3(1.9f, 2.9f, 0.02f), mote);

            // 떠 있는 빈 종이
            for (int i = 0; i < 22; i++)
            {
                Vector3 at = new Vector3(Mathf.Sin(i * 1.9f) * 10f, 1.4f + (i % 5) * 0.6f, Mathf.Cos(i * 2.7f) * 10f);
                if (at.z < -5f && Mathf.Abs(at.x) < 4f) continue;
                Deco(PrimitiveType.Cube, "BlankPage", at, Quaternion.Euler(i * 37f % 60f - 30f, i * 53f, i * 29f % 40f - 20f), new Vector3(0.36f, 0.5f, 0.01f),
                    i % 3 == 0 ? mote : slab);
            }

            CreatePointLight(door + Vector3.up * 1.5f, new Color(0.85f, 0.82f, 1f), 12f, 1.2f);
            CreatePointLight(new Vector3(0f, 7f, -2f), new Color(0.92f, 0.9f, 1f), 24f, 0.9f);
            CreateBoundaryWalls(wallMat, 14f, 4f);
        }

        // ======================= 가장 높은 가지 =======================

        /// <summary>
        /// 우듬지의 가장 높은 가지(canopy_crown) — 산 정상이 아니라 <b>나무 꼭대기</b>다. 바닥은 잎이 깔린 넓은 가지,
        /// 굵은 가지가 가로지르고, 가장자리는 잎 덩이가 둘러싼다. 뒤로 하늘이 보인다. 방 크기는 산 정상과 같은 11.
        /// </summary>
        private void BuildCanopyCrown(SubAreaData sub)
        {
            Material leafFloor = Mat(new Color(0.30f, 0.46f, 0.22f));
            Material bark = Mat(new Color(0.42f, 0.31f, 0.19f));
            Material barkDark = Mat(new Color(0.33f, 0.24f, 0.15f));
            Material leaf = Mat(new Color(0.26f, 0.52f, 0.22f));
            Material leafLight = Mat(new Color(0.40f, 0.64f, 0.28f));
            Material blossom = Mat(new Color(0.98f, 0.72f, 0.36f));
            Material wallMat = Mat(new Color(0.24f, 0.44f, 0.20f));
            CreateFloor(leafFloor, 25f);

            // 굵은 가지 — 바닥을 가로지르는 나무껍질 길. 입구(0,-8)에서 북쪽으로 이어진다.
            // 콜라이더가 없어 플레이어가 그 위를 "걷는" 게 아니라 통과하므로 윗면을 발목 아래(≈0.15m)로 둔다
            Deco(PrimitiveType.Capsule, "MainBranch", new Vector3(0f, -0.1f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(3.2f, 11f, 0.5f), bark);
            Deco(PrimitiveType.Capsule, "SideBranchE", new Vector3(5f, -0.08f, 3f), Quaternion.Euler(90f, 55f, 0f), new Vector3(1.8f, 5f, 0.4f), barkDark);
            Deco(PrimitiveType.Capsule, "SideBranchW", new Vector3(-5f, -0.08f, -1f), Quaternion.Euler(90f, -60f, 0f), new Vector3(1.8f, 5f, 0.4f), barkDark);
            // 잎 조각 — 바닥 결
            for (int i = 0; i < 40; i++)
            {
                Vector3 at = new Vector3(Mathf.Sin(i * 2.1f) * 9.5f, 0.03f, Mathf.Cos(i * 1.3f) * 9.5f);
                Deco(PrimitiveType.Cylinder, "LeafMat", at, Quaternion.Euler(0f, i * 31f, 0f), new Vector3(1.1f, 0.01f, 0.6f), i % 2 == 0 ? leaf : leafLight);
            }
            // 가장자리 잎 덩이 — 벽을 덮는다(남쪽 가운데는 낮게)
            for (int i = 0; i < 24; i++)
            {
                float t = i / 24f * 4f;
                int side = Mathf.FloorToInt(t);
                float u = (t - side) * 22f - 11f;
                Vector3 at = side == 0 ? new Vector3(u, 0f, 10.3f) : side == 1 ? new Vector3(10.3f, 0f, u)
                    : side == 2 ? new Vector3(-u, 0f, -10.3f) : new Vector3(-10.3f, 0f, -u);
                bool south = side == 2;
                float s = south ? 1.8f : 3.2f + (i % 3) * 0.6f;
                Deco(PrimitiveType.Sphere, "CrownLeaves", at + Vector3.up * s * 0.35f, Quaternion.Euler(0f, i * 40f, 0f),
                    new Vector3(s * 1.3f, s, s * 1.1f), i % 2 == 0 ? leaf : leafLight);
                if (i % 4 == 1 && !south)
                    Deco(PrimitiveType.Sphere, "Blossom", at + Vector3.up * (s * 0.8f), Quaternion.identity, Vector3.one * 0.45f, blossom);
            }
            // 가지 끝의 새순과 열매
            Material fruit = GlowMat(new Color(1f, 0.55f, 0.28f), new Color(0.35f, 0.12f, 0.04f));
            for (int i = 0; i < 8; i++)
                Deco(PrimitiveType.Sphere, "CrownFruit", new Vector3(Mathf.Sin(i * 1.4f) * 6f, 0.25f, Mathf.Cos(i * 1.4f) * 6f + 2f), Quaternion.identity, Vector3.one * 0.3f, fruit);

            CreatePointLight(Vector3.up * 10f, new Color(1f, 0.98f, 0.85f), 25f, 1.3f);
            CreateBoundaryWalls(wallMat, 11f, 3f);
        }

        // ======================= 산 정상 · 서리 능선 =======================

        /// <summary>산 정상의 돌탑·깃발, 서리 능선의 얼음 가시·눈 덮인 바위. 눈 패치는 둔덕(각진 사각형 금지).</summary>
        private void DecoratePeak(SubAreaData sub)
        {
            bool frost = sub.subAreaId == "frostline_ridge";
            Material snow = Mat(frost ? new Color(0.90f, 0.93f, 0.97f) : new Color(0.88f, 0.90f, 0.94f));
            for (int i = 0; i < (frost ? 14 : 7); i++)
            {
                Vector3 at = new Vector3(Mathf.Sin(i * 2.3f) * 8f, 0.02f, Mathf.Cos(i * 1.9f) * 8f);
                Deco(PrimitiveType.Sphere, "SnowMound", at, Quaternion.Euler(0f, i * 47f, 0f), new Vector3(2.8f, 0.4f, 2.1f), snow);
            }
            if (frost)
            {
                Material ice = GlowMat(new Color(0.66f, 0.84f, 0.96f), new Color(0.12f, 0.2f, 0.28f));
                for (int i = 0; i < 12; i++)
                {
                    Vector3 at = new Vector3(Mathf.Sin(i * 1.7f + 0.4f) * 8.5f, 0f, Mathf.Cos(i * 2.9f) * 8.5f);
                    if (at.z < -5f && Mathf.Abs(at.x) < 3.5f) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        float h = 0.8f + ((i + k) % 3) * 0.6f;
                        Deco(PrimitiveType.Cube, "IceSpike", at + new Vector3(k * 0.35f, h * 0.45f, (k % 2) * 0.3f),
                            Quaternion.Euler((k - 1) * 16f, i * 29f + k * 50f, (k % 2) * 14f - 7f), new Vector3(0.28f, h, 0.28f), ice);
                    }
                }
                // 서릿발 바람에 깎인 능선 바위
                Material rock = Mat(new Color(0.52f, 0.58f, 0.66f));
                for (int i = 0; i < 5; i++)
                {
                    Vector3 at = new Vector3(-7f + i * 3.5f, 0.5f, 7.5f + (i % 2) * 1.2f);
                    Deco(PrimitiveType.Cube, "RidgeRock", at, Quaternion.Euler(0f, i * 33f, 8f), new Vector3(2f, 1.3f, 1.4f), rock);
                    Deco(PrimitiveType.Sphere, "RidgeSnowCap", at + Vector3.up * 0.72f, Quaternion.Euler(0f, i * 33f, 0f), new Vector3(2.1f, 0.35f, 1.5f), snow);
                }
                return;
            }
            // 산 정상 — 돌탑(소원 돌)과 깃발
            Material stone = Mat(new Color(0.56f, 0.54f, 0.50f));
            Vector3 cairn = new Vector3(0f, 0f, 4.5f);
            for (int k = 0; k < 6; k++)
            {
                float w = 1.2f - k * 0.16f;
                Deco(PrimitiveType.Sphere, "CairnStone", cairn + Vector3.up * (0.18f + k * 0.3f), Quaternion.Euler(0f, k * 51f, 0f),
                    new Vector3(w, 0.32f, w * 0.85f), stone);
            }
            Deco(PrimitiveType.Cylinder, "SummitPole", new Vector3(2.2f, 1.4f, 5f), Quaternion.identity, new Vector3(0.08f, 1.4f, 0.08f), Mat(new Color(0.40f, 0.30f, 0.20f)));
            Deco(PrimitiveType.Cube, "SummitFlag", new Vector3(2.75f, 2.45f, 5f), Quaternion.Euler(0f, 0f, -4f), new Vector3(1f, 0.6f, 0.02f), Mat(new Color(0.85f, 0.22f, 0.18f)));
            Material alpine = Mat(new Color(0.46f, 0.52f, 0.28f));
            for (int i = 0; i < 16; i++)
                Deco(PrimitiveType.Sphere, "AlpineTuft", new Vector3(Mathf.Sin(i * 2.7f) * 9f, 0.08f, Mathf.Cos(i * 1.1f) * 9f), Quaternion.identity,
                    new Vector3(0.6f, 0.22f, 0.5f), alpine);
        }

        // ======================= 침묵의 자리 =======================

        /// <summary>
        /// 텅 빈 들의 침묵의 자리(hollow_silence) — 늪이 아니다. 빛바랜 풀밭에 풍경(風磬)을 단 장대가 서 있는데
        /// 아무것도 울리지 않는다. 늪 안개 빌더를 쓰되 물웅덩이·고목 대신 이걸 둔다.
        /// </summary>
        private void DecorateSilence()
        {
            Material post = Mat(new Color(0.62f, 0.58f, 0.50f));
            Material chime = Mat(new Color(0.78f, 0.80f, 0.84f));
            Material grass = Mat(new Color(0.66f, 0.63f, 0.50f));
            for (int i = 0; i < 7; i++)
            {
                float a = (i * 51f + 20f) * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Cos(a) * 7.5f, 0f, Mathf.Sin(a) * 7.5f + 1f);
                if (at.z < -5f && Mathf.Abs(at.x) < 3.5f) continue;
                Deco(PrimitiveType.Cylinder, "ChimePost", at + Vector3.up * 1.2f, Quaternion.Euler(0f, 0f, (i % 3 - 1) * 3f), new Vector3(0.1f, 1.2f, 0.1f), post);
                Deco(PrimitiveType.Cube, "ChimeBar", at + Vector3.up * 2.35f, Quaternion.Euler(0f, i * 40f, 0f), new Vector3(0.9f, 0.05f, 0.05f), post);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 off = Quaternion.Euler(0f, i * 40f, 0f) * new Vector3((k - 1) * 0.35f, 0f, 0f);
                    Deco(PrimitiveType.Cylinder, "ChimeTube", at + off + Vector3.up * (2.0f - k * 0.08f), Quaternion.identity, new Vector3(0.05f, 0.22f + k * 0.05f, 0.05f), chime);
                }
            }
            for (int i = 0; i < 40; i++)
                Deco(PrimitiveType.Sphere, "PaleGrass", new Vector3(Mathf.Sin(i * 2.2f) * 11f, 0.12f, Mathf.Cos(i * 1.6f) * 11f), Quaternion.Euler(0f, i * 17f, 0f),
                    new Vector3(0.7f, 0.35f, 0.5f), grass);
        }

        // ======================= 겹친 가지 속 =======================

        /// <summary>우듬지의 겹친 가지 속(canopy_bough) — 깊은 숲 빌더 위에 밝은 잎·꽃·이끼 낀 가지를 얹는다.</summary>
        private void DecorateBough()
        {
            Material moss = Mat(new Color(0.34f, 0.56f, 0.26f));
            Material flower = GlowMat(new Color(0.98f, 0.70f, 0.40f), new Color(0.25f, 0.12f, 0.04f));
            Material vine = Mat(new Color(0.24f, 0.46f, 0.20f));
            for (int i = 0; i < 30; i++)
            {
                Vector3 at = new Vector3(Mathf.Sin(i * 2.9f) * 13f, 0.04f, Mathf.Cos(i * 1.3f) * 13f);
                Deco(PrimitiveType.Cylinder, "MossPatch", at, Quaternion.identity, new Vector3(1.6f, 0.01f, 1.2f), moss);
                if (i % 2 == 0)
                    Deco(PrimitiveType.Sphere, "BoughFlower", at + new Vector3(0.3f, 0.2f, 0f), Quaternion.identity, Vector3.one * 0.28f, flower);
            }
            for (int i = 0; i < 16; i++)   // 늘어진 덩굴(잎 덩이 아래)
            {
                Vector3 at = new Vector3(Mathf.Sin(i * 1.9f) * 12f, 3.2f, Mathf.Cos(i * 2.3f) * 12f);
                Deco(PrimitiveType.Cylinder, "Vine", at, Quaternion.Euler((i % 3 - 1) * 6f, 0f, 0f), new Vector3(0.05f, 0.9f, 0.05f), vine);
            }
        }

        // ======================= 숨겨진 웅덩이 =======================

        /// <summary>
        /// 초원의 숨겨진 웅덩이(meadow_pond) — 수중이 아니다. 풀밭 한가운데 맑은 웅덩이와 수련, 갈대.
        /// 옛날엔 연못 깊은 곳과 같은 수중 빌더라 분홍 산호 기둥이 초원 물웅덩이에 서 있었다. 방 크기는 14.
        /// </summary>
        private void BuildHiddenPuddle(SubAreaData sub)
        {
            Material grass = Mat(new Color(0.30f, 0.46f, 0.23f));
            Material grassLight = Mat(new Color(0.38f, 0.54f, 0.27f));
            Material mud = Mat(new Color(0.36f, 0.32f, 0.22f));
            Material water = Mat(new Color(0.30f, 0.52f, 0.62f));
            Wet(water);
            Material shallow = Mat(new Color(0.42f, 0.62f, 0.60f));
            Wet(shallow);
            Material pad = Mat(new Color(0.28f, 0.52f, 0.22f));
            Material reed = Mat(new Color(0.40f, 0.52f, 0.24f));
            Material hedge = Mat(new Color(0.24f, 0.40f, 0.20f));
            Material lily = Mat(new Color(0.98f, 0.80f, 0.88f));   // 수련 꽃 — 루프 안에서 만들면 송이마다 인스턴스가 는다
            CreateFloor(grass, 30f);

            Vector3 pool = new Vector3(0f, 0f, 3f);
            Deco(PrimitiveType.Cylinder, "PuddleMud", pool + Vector3.up * 0.012f, Quaternion.identity, new Vector3(15f, 0.005f, 13f), mud);
            Deco(PrimitiveType.Cylinder, "PuddleShallow", pool + Vector3.up * 0.02f, Quaternion.identity, new Vector3(13.4f, 0.005f, 11.4f), shallow);
            Deco(PrimitiveType.Cylinder, "PuddleWater", pool + Vector3.up * 0.028f, Quaternion.identity, new Vector3(10.5f, 0.005f, 8.6f), water);
            for (int i = 0; i < 14; i++)
            {
                Vector3 at = pool + new Vector3(Mathf.Sin(i * 2.4f) * 4f, 0.04f, Mathf.Cos(i * 1.7f) * 3.2f);
                Deco(PrimitiveType.Cylinder, "LilyPad", at, Quaternion.Euler(0f, i * 40f, 0f), new Vector3(0.8f, 0.01f, 0.8f), pad);
                if (i % 4 == 0)
                    Deco(PrimitiveType.Sphere, "LilyFlower", at + Vector3.up * 0.08f, Quaternion.identity, new Vector3(0.25f, 0.14f, 0.25f), lily);
            }
            for (int i = 0; i < 26; i++)
            {
                float a = i * 360f / 26f * Mathf.Deg2Rad;
                Vector3 at = pool + new Vector3(Mathf.Cos(a) * 7.2f, 0f, Mathf.Sin(a) * 6.2f);
                if (at.z < -3f && Mathf.Abs(at.x) < 2.5f) continue;   // 입구에서 물가로 들어오는 틈
                float h = 1.0f + (i % 3) * 0.35f;
                Deco(PrimitiveType.Cylinder, "Reed", at + Vector3.up * h * 0.5f, Quaternion.Euler((i % 3 - 1) * 5f, 0f, (i % 2) * 6f - 3f), new Vector3(0.07f, h * 0.5f, 0.07f), reed);
            }
            for (int i = 0; i < 40; i++)
                Deco(PrimitiveType.Sphere, "GrassClump", new Vector3(Mathf.Sin(i * 1.9f) * 12f, 0.1f, Mathf.Cos(i * 2.6f) * 12f), Quaternion.identity,
                    new Vector3(0.9f, 0.35f, 0.7f), i % 2 == 0 ? grass : grassLight);
            // 둘레 수풀 — 상자 벽을 가린다
            for (int i = 0; i < 20; i++)
            {
                float t = i / 20f * 4f;
                int side = Mathf.FloorToInt(t);
                float u = (t - side) * 28f - 14f;
                Vector3 at = side == 0 ? new Vector3(u, 0f, 13.2f) : side == 1 ? new Vector3(13.2f, 0f, u)
                    : side == 2 ? new Vector3(-u, 0f, -13.2f) : new Vector3(-13.2f, 0f, -u);
                Deco(PrimitiveType.Sphere, "HedgeBush", at + Vector3.up * 0.8f, Quaternion.identity, new Vector3(3.4f, 2.2f, 2.4f), hedge);
            }

            CreatePointLight(new Vector3(0f, 6f, 0f), new Color(1f, 0.97f, 0.88f), 24f, 1.1f);
            CreateBoundaryWalls(hedge, 14f, 3f);
        }

        // ======================= 공용 =======================

        /// <summary>장식용 프리미티브 — 콜라이더 없음(연출 배우·입구 안전 좌표를 막지 않는다).</summary>
        private GameObject Deco(PrimitiveType type, string name, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat)
        {
            GameObject go = Prim(type, name);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            Apply(go, mat);
            NoCollider(go);
            return go;
        }

        private Material GlowMat(Color color, Color emission)
        {
            Material m = Mat(color);
            SceneryMaterials.SetEmission(m, emission);
            return m;
        }

        private static void Wet(Material m)
        {
            if (m == null) return;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.85f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
        }
    }
}
