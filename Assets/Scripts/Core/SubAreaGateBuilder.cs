using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 필드 쪽 서브에리어 입구 표식 — "여기로 들어간다"를 테마로 알려 주는 작은 구조물.
    ///
    /// <c>PlaySceneBootstrap.CreateSubAreaEntries</c>는 environmentType 7종(동굴·깊은 숲·수중·갈대·꽃 미로·온실·
    /// 웅덩이)에만 입구를 세우고 나머지는 <c>default: break</c>였다. 배치 캡처로 26곳을 훑어 보니 <b>15곳이
    /// 필드에 아무 표식도 없었다</b>(2막 서브에리어 대부분 + 안개 늪·산 정상·고대 신전·유적 지하) — 근접 프롬프트가
    /// 뜨기 전까지는 거기가 입구인 줄 알 방법이 없었다. 그리고 모래언덕의 개미귀신 구덩이는 environmentType이
    /// cave라 동굴 석문이 섰다.
    ///
    /// 여기 등록한 서브에리어는 부트스트랩이 입구를 짓지 않는다(<see cref="Handles"/>). 이름은
    /// <c>SubArea_{id}_Gate…</c> — 서브에리어 진입 때 <c>HideMainWorld</c>가 이 접두어로 메인 월드를 끈다.
    /// <b>콜라이더는 없다</b>: 입구 한가운데서 [E]를 누르는 자리이고, 밟히는 콜라이더는 플레이어를 띄운다.
    /// 높이는 3m 안팎 — 카메라(플레이어 남쪽 위)가 입구 앞에 선 플레이어를 가리지 않게.
    /// </summary>
    public class SubAreaGateBuilder : MonoBehaviour
    {
        /// <summary>
        /// 서브에리어 id → 입구 설계. <b>이 표가 유일한 목록이다</b> — 부트스트랩이 건너뛸지(<see cref="Handles"/>)도
        /// 여기서 파생한다. 예전엔 id 집합(HashSet)과 짓는 switch가 따로 있었고 switch엔 default가 없었다.
        /// 집합에만 id를 넣고 case를 빠뜨리면 부트스트랩은 기본 입구를 건너뛰고 이 빌더도 아무것도 안 지어
        /// 입구가 <b>조용히</b> 사라진다. 한 표에서 둘 다 읽으면 그 어긋남 자체가 성립하지 않는다.
        /// </summary>
        private static readonly Dictionary<string, System.Action<SubAreaGateBuilder>> Designs =
            new Dictionary<string, System.Action<SubAreaGateBuilder>>
        {
            { "swamp_fog", b => b.SwampFog() },
            { "mountain_peak", b => b.MountainPeak() },
            { "ruins_temple", b => b.RuinsTemple() },
            { "ruins_underground", b => b.RuinsUnderground() },
            { "hollow_silence", b => b.HollowSilence() },
            { "hollow_burrow", b => b.HollowBurrow() },
            { "dunes_vault", b => b.DunesVault() },
            { "dunes_pit", b => b.DunesPit() },
            { "frostline_archive", b => b.FrostlineArchive() },
            { "frostline_ridge", b => b.FrostlineRidge() },
            { "emberfall_kiln", b => b.EmberfallKiln() },
            { "emberfall_vent", b => b.EmberfallVent() },
            { "canopy_crown", b => b.CanopyCrown() },
            { "canopy_bough", b => b.CanopyBough() },
            { "nameless_ledger", b => b.NamelessLedger() },
            { "nameless_core", b => b.NamelessCore() },
        };

        /// <summary>이 빌더가 입구를 짓는 서브에리어인가 — 부트스트랩은 여기 해당하면 건너뛴다.</summary>
        public static bool Handles(string subAreaId) => !string.IsNullOrEmpty(subAreaId) && Designs.ContainsKey(subAreaId);

        /// <summary>설계가 있는 서브에리어 id 전부 — 테스트가 실재 서브에리어인지·실제로 뭔가 짓는지 대조한다.</summary>
        internal static IEnumerable<string> DesignedIds => Designs.Keys;

        private readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
        /// <summary>발광 복제 캐시 — (원본 머티리얼, 발광색). 원본은 <see cref="mats"/>의 한 색이다.</summary>
        private readonly Dictionary<(Material source, Color emission), Material> glowMats =
            new Dictionary<(Material source, Color emission), Material>();
        private readonly List<Material> owned = new List<Material>();

        private Vector3 origin;
        private Quaternion facing;
        private string prefix;

        public void Build(RegionData[] regions)
        {
            if (regions == null) return;
            foreach (RegionData r in regions)
            {
                if (r?.subAreas == null) continue;
                foreach (SubAreaData sub in r.subAreas)
                {
                    if (sub == null || !Handles(sub.subAreaId)) continue;
                    origin = new Vector3(sub.centerPosition.x, 0.1f, sub.centerPosition.z);
                    // 정면(+Z)이 리전 중심을 본다 — 플레이어는 대개 리전 안쪽에서 다가온다
                    Vector3 toCenter = r.centerPosition - sub.centerPosition;
                    toCenter.y = 0f;
                    facing = toCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCenter.normalized) : Quaternion.identity;
                    prefix = $"SubArea_{sub.subAreaId}_Gate";
                    BuildGate(sub.subAreaId);
                }
            }
        }

        /// <summary>
        /// 한 서브에리어의 입구를 짓는다. 설계 표(<see cref="Designs"/>)가 id 목록의 유일한 출처라 여기서 빠질 일은
        /// 구조적으로 없지만, 설계가 비었거나(부품 0개) 표 밖 id가 들어오면 <b>에러로 드러낸다</b> — 이 빌더가 맡았다고
        /// 답한 순간 부트스트랩은 기본 입구를 건너뛰었으므로, 여기서 조용히 넘어가면 필드에 아무 표식도 안 선다.
        /// </summary>
        private void BuildGate(string id)
        {
            int before = transform.childCount;
            if (Designs.TryGetValue(id, out System.Action<SubAreaGateBuilder> design) && design != null) design(this);
            if (transform.childCount == before)
                Debug.LogError($"[SubAreaGateBuilder] '{id}' 입구를 하나도 못 지었다 — Handles가 참이라 부트스트랩도 기본 입구를 건너뛰어 필드에 표식이 없다");
        }

        // ---- 입구 설계 (좌표는 입구 로컬: +Z가 리전 중심 쪽) ----

        private void SwampFog()
        {
            // 안개가 고인 자리 — 낮은 안개 덩이, 마른 갈대, 초록빛 등불
            for (int i = 0; i < 5; i++)
                Smoke(Quaternion.Euler(0f, i * 72f, 0f) * new Vector3(0f, 0.5f, 1.6f), 1.6f, C(0.62f, 0.66f, 0.58f));
            for (int i = 0; i < 9; i++)
                Part(PrimitiveType.Cylinder, "DeadReed", Quaternion.Euler(0f, i * 40f, 0f) * new Vector3(0f, 0.6f, 2.8f), new Vector3((i % 3 - 1) * 8f, 0f, 6f),
                    new Vector3(0.05f, 0.6f, 0.05f), C(0.42f, 0.40f, 0.28f));
            Part(PrimitiveType.Cylinder, "LanternPole", new Vector3(1.2f, 0.9f, 0.6f), Vector3.zero, new Vector3(0.07f, 0.9f, 0.07f), C(0.26f, 0.22f, 0.18f));
            Glow(Part(PrimitiveType.Sphere, "Lantern", new Vector3(1.2f, 1.9f, 0.6f), Vector3.zero, new Vector3(0.28f, 0.34f, 0.28f), C(0.6f, 1f, 0.5f)), new Color(0.5f, 1.1f, 0.35f));
        }

        private void MountainPeak()
        {
            Cairn(C(0.56f, 0.54f, 0.50f), C(0.88f, 0.90f, 0.94f));
            Pennant(C(0.85f, 0.22f, 0.18f));
            Part(PrimitiveType.Cube, "SignPost", new Vector3(1.8f, 0.8f, 0.6f), new Vector3(0f, 0f, 0f), new Vector3(0.12f, 1.6f, 0.12f), C(0.40f, 0.30f, 0.20f));
            Part(PrimitiveType.Cube, "SignArrow", new Vector3(1.8f, 1.45f, 0.7f), new Vector3(0f, 0f, 18f), new Vector3(0.9f, 0.26f, 0.06f), C(0.58f, 0.44f, 0.28f));
        }

        private void RuinsTemple()
        {
            // 신전 문 — 새김 기둥 두 개와 보랏빛 문양
            Color stone = C(0.62f, 0.58f, 0.50f);
            Part(PrimitiveType.Cube, "TemplePillarL", new Vector3(-1.2f, 1.3f, 0f), Vector3.zero, new Vector3(0.55f, 2.6f, 0.55f), stone);
            Part(PrimitiveType.Cube, "TemplePillarR", new Vector3(1.2f, 1.3f, 0f), Vector3.zero, new Vector3(0.55f, 2.6f, 0.55f), stone);
            Part(PrimitiveType.Cube, "TempleLintel", new Vector3(0f, 2.75f, 0f), Vector3.zero, new Vector3(3.2f, 0.4f, 0.7f), C(0.56f, 0.52f, 0.45f));
            Glow(Part(PrimitiveType.Cube, "TempleGlyph", new Vector3(0f, 2.75f, 0.36f), new Vector3(0f, 0f, 45f), new Vector3(0.36f, 0.36f, 0.03f), C(0.72f, 0.52f, 0.96f)),
                new Color(0.5f, 0.3f, 0.9f));
            for (int side = -1; side <= 1; side += 2)
                Glow(Part(PrimitiveType.Cube, "PillarBand", new Vector3(side * 1.2f, 1.6f, 0.28f), Vector3.zero, new Vector3(0.45f, 0.08f, 0.02f), C(0.72f, 0.52f, 0.96f)),
                    new Color(0.4f, 0.25f, 0.7f));
            Part(PrimitiveType.Cube, "TempleStep", new Vector3(0f, 0.08f, 0.6f), Vector3.zero, new Vector3(2.6f, 0.16f, 0.7f), C(0.58f, 0.54f, 0.46f));
        }

        private void RuinsUnderground()
        {
            // 땅 아래로 내려가는 계단 — 어두운 구멍과 돌 틀
            Part(PrimitiveType.Cube, "StairFrameL", new Vector3(-1.1f, 0.25f, 0f), Vector3.zero, new Vector3(0.35f, 0.5f, 2.6f), C(0.56f, 0.52f, 0.44f));
            Part(PrimitiveType.Cube, "StairFrameR", new Vector3(1.1f, 0.25f, 0f), Vector3.zero, new Vector3(0.35f, 0.5f, 2.6f), C(0.56f, 0.52f, 0.44f));
            Part(PrimitiveType.Cube, "StairFrameBack", new Vector3(0f, 0.35f, -1.3f), Vector3.zero, new Vector3(2.55f, 0.7f, 0.35f), C(0.52f, 0.48f, 0.41f));
            Part(PrimitiveType.Cube, "StairHole", new Vector3(0f, 0.012f, -0.2f), Vector3.zero, new Vector3(1.85f, 0.01f, 2.2f), C(0.07f, 0.06f, 0.05f));
            for (int k = 0; k < 3; k++)
                Part(PrimitiveType.Cube, "StairStep", new Vector3(0f, 0.03f, 0.75f - k * 0.45f), Vector3.zero, new Vector3(1.85f, 0.03f, 0.4f),
                    k == 0 ? C(0.50f, 0.46f, 0.38f) : k == 1 ? C(0.34f, 0.31f, 0.26f) : C(0.20f, 0.18f, 0.15f));
        }

        private void HollowSilence()
        {
            // 울리지 않는 풍경 세 대가 빈 판석을 둘러선다
            Part(PrimitiveType.Cylinder, "PaleStone", new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(2f, 0.06f, 1.6f), C(0.74f, 0.73f, 0.68f));
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = Quaternion.Euler(0f, i * 120f + 30f, 0f) * new Vector3(0f, 0f, 2.2f);
                Part(PrimitiveType.Cylinder, "ChimePost", at + Vector3.up * 1.1f, Vector3.zero, new Vector3(0.09f, 1.1f, 0.09f), C(0.62f, 0.58f, 0.50f));
                Part(PrimitiveType.Cube, "ChimeBar", at + Vector3.up * 2.15f, new Vector3(0f, i * 120f, 0f), new Vector3(0.7f, 0.04f, 0.04f), C(0.62f, 0.58f, 0.50f));
                for (int k = 0; k < 3; k++)
                    Part(PrimitiveType.Cylinder, "ChimeTube", at + Quaternion.Euler(0f, i * 120f, 0f) * new Vector3((k - 1) * 0.25f, 1.85f, 0f), Vector3.zero,
                        new Vector3(0.04f, 0.2f, 0.04f), C(0.80f, 0.82f, 0.86f));
            }
        }

        private void HollowBurrow()
        {
            // 마른 굴 입구 — 흙 둔덕 고리와 어두운 구멍, 늘어진 마른 뿌리
            Part(PrimitiveType.Sphere, "BurrowMound", new Vector3(0f, -0.2f, -0.6f), Vector3.zero, new Vector3(3.2f, 1.6f, 2.6f), C(0.54f, 0.50f, 0.40f));
            Part(PrimitiveType.Cube, "BurrowHole", new Vector3(0f, 0.42f, 0.62f), new Vector3(-12f, 0f, 0f), new Vector3(1.2f, 0.75f, 0.05f), C(0.10f, 0.09f, 0.07f));
            for (int k = 0; k < 4; k++)
                Part(PrimitiveType.Cylinder, "DryRoot", new Vector3(-0.45f + k * 0.3f, 0.7f, 0.66f), new Vector3((k % 2) * 10f - 5f, 0f, 0f), new Vector3(0.04f, 0.22f, 0.04f), C(0.46f, 0.40f, 0.30f));
        }

        private void DunesVault()
        {
            // 반쯤 묻힌 창고 뚜껑 + 명부회 화물 + 아이보리 깃발(명부회 제복색)
            Part(PrimitiveType.Cube, "HatchFrame", new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(2.2f, 0.16f, 1.8f), C(0.36f, 0.27f, 0.17f));
            Part(PrimitiveType.Cube, "HatchDoor", new Vector3(0f, 0.17f, 0f), new Vector3(0f, 0f, 0f), new Vector3(1.8f, 0.05f, 1.4f), C(0.46f, 0.35f, 0.22f));
            Part(PrimitiveType.Cube, "HatchBand", new Vector3(0f, 0.2f, 0.35f), Vector3.zero, new Vector3(1.82f, 0.03f, 0.1f), C(0.30f, 0.30f, 0.32f));
            Part(PrimitiveType.Cube, "HatchBand", new Vector3(0f, 0.2f, -0.35f), Vector3.zero, new Vector3(1.82f, 0.03f, 0.1f), C(0.30f, 0.30f, 0.32f));
            Part(PrimitiveType.Cube, "Crate", new Vector3(1.9f, 0.4f, -0.6f), new Vector3(0f, 18f, 0f), new Vector3(0.9f, 0.8f, 0.9f), C(0.52f, 0.40f, 0.25f));
            Part(PrimitiveType.Cube, "Crate", new Vector3(1.8f, 1.1f, -0.5f), new Vector3(0f, -12f, 0f), new Vector3(0.75f, 0.6f, 0.75f), C(0.52f, 0.40f, 0.25f));
            Part(PrimitiveType.Cylinder, "BannerPole", new Vector3(-1.6f, 1.3f, -0.4f), Vector3.zero, new Vector3(0.07f, 1.3f, 0.07f), C(0.30f, 0.24f, 0.18f));
            Part(PrimitiveType.Cube, "Banner", new Vector3(-1.25f, 2.2f, -0.4f), Vector3.zero, new Vector3(0.65f, 0.9f, 0.02f), C(0.90f, 0.88f, 0.82f));
            Part(PrimitiveType.Cube, "BannerMark", new Vector3(-1.25f, 2.25f, -0.38f), Vector3.zero, new Vector3(0.3f, 0.06f, 0.02f), C(0.18f, 0.22f, 0.28f));
        }

        private void DunesPit()
        {
            // 모래 깔때기 — 가운데로 갈수록 짙은 동심원, 둘레 둔덕, 가운데 개미귀신 턱
            Color[] rings = { C(0.66f, 0.55f, 0.38f), C(0.56f, 0.45f, 0.30f), C(0.44f, 0.34f, 0.22f), C(0.28f, 0.21f, 0.13f) };
            float[] radii = { 3.6f, 2.6f, 1.6f, 0.7f };
            for (int i = 0; i < rings.Length; i++)
                Part(PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.01f + i * 0.006f, 0f), Vector3.zero, new Vector3(radii[i] * 2f, 0.004f, radii[i] * 2f), rings[i]);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                Part(PrimitiveType.Sphere, "Rim", Quaternion.Euler(0f, a, 0f) * new Vector3(0f, -0.1f, 3.9f), new Vector3(0f, a, 0f),
                    new Vector3(1.6f, 0.7f, 1.0f), C(0.76f, 0.64f, 0.44f));
            }
            Part(PrimitiveType.Capsule, "JawL", new Vector3(-0.18f, 0.12f, 0f), new Vector3(20f, 0f, 35f), new Vector3(0.06f, 0.18f, 0.06f), C(0.20f, 0.14f, 0.10f));
            Part(PrimitiveType.Capsule, "JawR", new Vector3(0.18f, 0.12f, 0f), new Vector3(20f, 0f, -35f), new Vector3(0.06f, 0.18f, 0.06f), C(0.20f, 0.14f, 0.10f));
        }

        private void FrostlineArchive()
        {
            // 얼음 아치 문 — 안쪽이 푸르게 빛난다
            Color ice = C(0.70f, 0.85f, 0.95f);
            Part(PrimitiveType.Cube, "IcePillarL", new Vector3(-1.1f, 1.2f, 0f), new Vector3(0f, 0f, 4f), new Vector3(0.6f, 2.4f, 0.6f), ice);
            Part(PrimitiveType.Cube, "IcePillarR", new Vector3(1.1f, 1.2f, 0f), new Vector3(0f, 0f, -4f), new Vector3(0.6f, 2.4f, 0.6f), ice);
            Part(PrimitiveType.Cube, "IceLintel", new Vector3(0f, 2.55f, 0f), Vector3.zero, new Vector3(3f, 0.45f, 0.7f), ice);
            Glow(Part(PrimitiveType.Cube, "ArchiveDoor", new Vector3(0f, 1.1f, -0.1f), Vector3.zero, new Vector3(1.6f, 2.2f, 0.05f), C(0.40f, 0.62f, 0.86f)),
                new Color(0.18f, 0.34f, 0.55f));
            Part(PrimitiveType.Sphere, "SnowCap", new Vector3(0f, 2.82f, 0f), Vector3.zero, new Vector3(3.1f, 0.35f, 0.9f), C(0.93f, 0.95f, 0.98f));
            Part(PrimitiveType.Sphere, "Drift", new Vector3(-1.6f, 0.05f, 0.4f), Vector3.zero, new Vector3(1.6f, 0.6f, 1.2f), C(0.90f, 0.93f, 0.97f));
            Part(PrimitiveType.Sphere, "Drift", new Vector3(1.7f, 0.05f, 0.3f), Vector3.zero, new Vector3(1.4f, 0.5f, 1.1f), C(0.90f, 0.93f, 0.97f));
        }

        private void FrostlineRidge()
        {
            Cairn(C(0.56f, 0.62f, 0.70f), C(0.92f, 0.95f, 0.98f));
            Pennant(C(0.40f, 0.62f, 0.90f));
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f + 20f;
                Vector3 at = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 2.6f);
                Glow(Part(PrimitiveType.Cube, "IceSpike", at + Vector3.up * 0.5f, new Vector3(12f, a, 10f), new Vector3(0.28f, 1.1f, 0.28f), C(0.66f, 0.84f, 0.96f)),
                    new Color(0.10f, 0.17f, 0.24f));
            }
        }

        private void EmberfallKiln()
        {
            // 벽돌 가마 — 둥근 몸통, 달아오른 아궁이, 굴뚝
            Part(PrimitiveType.Sphere, "KilnDome", new Vector3(0f, 0.3f, -0.4f), Vector3.zero, new Vector3(2.6f, 2.2f, 2.4f), C(0.55f, 0.30f, 0.20f));
            Glow(Part(PrimitiveType.Cube, "KilnMouth", new Vector3(0f, 0.55f, 0.72f), Vector3.zero, new Vector3(0.8f, 0.6f, 0.1f), C(1f, 0.48f, 0.15f)),
                new Color(1.6f, 0.55f, 0.12f));
            Part(PrimitiveType.Cylinder, "KilnChimney", new Vector3(0.6f, 1.9f, -0.9f), Vector3.zero, new Vector3(0.36f, 0.6f, 0.36f), C(0.44f, 0.24f, 0.16f));
            Part(PrimitiveType.Cube, "BrickStack", new Vector3(-1.7f, 0.25f, 0.3f), new Vector3(0f, 14f, 0f), new Vector3(0.8f, 0.5f, 0.5f), C(0.60f, 0.34f, 0.22f));
        }

        private void EmberfallVent()
        {
            // 김이 새는 분출구 — 검은 둔덕 고리, 빛나는 가운데, 피어오르는 연기
            Part(PrimitiveType.Cylinder, "VentMound", new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(3.2f, 0.2f, 3.2f), C(0.16f, 0.14f, 0.14f));
            Glow(Part(PrimitiveType.Cylinder, "VentGlow", new Vector3(0f, 0.42f, 0f), Vector3.zero, new Vector3(1.4f, 0.02f, 1.4f), C(1f, 0.42f, 0.12f)),
                new Color(1.8f, 0.5f, 0.1f));
            for (int i = 0; i < 4; i++)
                Smoke(new Vector3(0.2f * (i % 2), 1.0f + i * 0.7f, 0.15f * (i - 1)), 0.9f + i * 0.35f, C(0.42f, 0.40f, 0.40f));
        }

        private void CanopyCrown()
        {
            // 거대수 밑동에 걸친 줄사다리 — 위로 올라간다
            Part(PrimitiveType.Cylinder, "CrownTrunk", new Vector3(0f, 1.3f, -0.9f), Vector3.zero, new Vector3(1.7f, 1.3f, 1.7f), C(0.40f, 0.30f, 0.19f));
            Part(PrimitiveType.Sphere, "CrownLeafTop", new Vector3(0f, 3.0f, -0.9f), Vector3.zero, new Vector3(2.8f, 1.2f, 2.6f), C(0.26f, 0.52f, 0.22f));
            for (int side = -1; side <= 1; side += 2)
                Part(PrimitiveType.Cylinder, "LadderRope", new Vector3(side * 0.32f, 1.25f, 0f), Vector3.zero, new Vector3(0.04f, 1.25f, 0.04f), C(0.70f, 0.62f, 0.44f));
            for (int k = 0; k < 6; k++)
                Part(PrimitiveType.Cube, "LadderRung", new Vector3(0f, 0.3f + k * 0.38f, 0f), Vector3.zero, new Vector3(0.7f, 0.05f, 0.08f), C(0.56f, 0.42f, 0.26f));
        }

        private void CanopyBough()
        {
            // 두 가지가 엇갈려 만든 아치 + 잎
            Part(PrimitiveType.Cylinder, "BoughL", new Vector3(-0.7f, 1.2f, 0f), new Vector3(0f, 0f, -22f), new Vector3(0.35f, 1.35f, 0.35f), C(0.40f, 0.30f, 0.19f));
            Part(PrimitiveType.Cylinder, "BoughR", new Vector3(0.7f, 1.2f, 0f), new Vector3(0f, 0f, 22f), new Vector3(0.35f, 1.35f, 0.35f), C(0.40f, 0.30f, 0.19f));
            Part(PrimitiveType.Sphere, "BoughLeaves", new Vector3(0f, 2.55f, 0f), Vector3.zero, new Vector3(2.8f, 1.1f, 1.4f), C(0.28f, 0.54f, 0.24f));
            Glow(Part(PrimitiveType.Sphere, "BoughFlower", new Vector3(0.6f, 2.9f, 0.5f), Vector3.zero, Vector3.one * 0.35f, C(0.98f, 0.70f, 0.40f)), new Color(0.3f, 0.14f, 0.04f));
        }

        private void NamelessLedger()
        {
            // 검은 문 석판 — 희미한 괘선이 빛난다
            Part(PrimitiveType.Cube, "LedgerDoor", new Vector3(0f, 1.4f, -0.2f), Vector3.zero, new Vector3(1.9f, 2.8f, 0.35f), C(0.14f, 0.13f, 0.18f));
            for (int k = 0; k < 5; k++)
                Glow(Part(PrimitiveType.Cube, "LedgerLine", new Vector3(0f, 0.7f + k * 0.42f, -0.01f), Vector3.zero, new Vector3(1.4f, 0.03f, 0.02f), C(0.62f, 0.58f, 0.80f)),
                    new Color(0.35f, 0.32f, 0.55f));
            Part(PrimitiveType.Cube, "LedgerStep", new Vector3(0f, 0.07f, 0.45f), Vector3.zero, new Vector3(2.4f, 0.14f, 0.7f), C(0.22f, 0.21f, 0.26f));
        }

        private void NamelessCore()
        {
            // 창백한 원반을 둘러선 빈 석판 다섯
            Glow(Part(PrimitiveType.Cylinder, "BlankDisc", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(3.4f, 0.02f, 3.4f), C(0.84f, 0.83f, 0.90f)),
                new Color(0.18f, 0.17f, 0.24f));
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f + 36f;
                Vector3 at = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 2.6f);
                Part(PrimitiveType.Cube, "BlankSlab", at + Vector3.up * 0.9f, new Vector3(0f, a, (i % 2) * 4f - 2f), new Vector3(0.8f, 1.8f, 0.2f), C(0.88f, 0.87f, 0.92f));
            }
        }

        // ---- 부품 ----

        private void Cairn(Color stone, Color snow)
        {
            for (int k = 0; k < 5; k++)
            {
                float w = 1f - k * 0.16f;
                Part(PrimitiveType.Sphere, "Cairn", new Vector3(0f, 0.16f + k * 0.27f, 0f), new Vector3(0f, k * 47f, 0f), new Vector3(w, 0.3f, w * 0.85f), stone);
            }
            Part(PrimitiveType.Sphere, "CairnSnow", new Vector3(0.1f, 0.05f, 0.5f), Vector3.zero, new Vector3(1.4f, 0.3f, 0.9f), snow);
        }

        private void Pennant(Color cloth)
        {
            Part(PrimitiveType.Cylinder, "PennantPole", new Vector3(-0.9f, 1.3f, -0.2f), Vector3.zero, new Vector3(0.06f, 1.3f, 0.06f), C(0.40f, 0.30f, 0.20f));
            Part(PrimitiveType.Cube, "PennantCloth", new Vector3(-0.55f, 2.3f, -0.2f), new Vector3(0f, 0f, -5f), new Vector3(0.7f, 0.42f, 0.02f), cloth);
        }

        private void Smoke(Vector3 local, float size, Color color)
        {
            Material m = MatFor(new Color(color.r, color.g, color.b, 0.35f));
            Part(PrimitiveType.Sphere, "Mist", local, Vector3.zero, new Vector3(size, size * 0.7f, size), m);
        }

        private GameObject Part(PrimitiveType type, string name, Vector3 local, Vector3 euler, Vector3 scale, Color color)
        {
            return Part(type, name, local, euler, scale, MatFor(color));
        }

        private GameObject Part(PrimitiveType type, string name, Vector3 local, Vector3 euler, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = $"{prefix}_{name}";
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(origin + facing * local, facing * Quaternion.Euler(euler));
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Collider col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }
            return go;
        }

        /// <summary>
        /// 발광 부품. 발광은 머티리얼 단위라 같은 색 캐시(<see cref="mats"/>)에 걸면 같은 색을 쓰는 다른 부품까지
        /// 빛난다 — 그래서 원본을 복제해 발광을 건다. 복제는 (원본 = 색, 발광색)마다 <b>한 벌</b>이다: 얼음 가시 5개·
        /// 괘선 5개처럼 같은 색·같은 발광이 반복되면 부품마다 복제하던 것을 하나로 모은다.
        /// </summary>
        private void Glow(GameObject go, Color emission)
        {
            if (go == null) return;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            Material source = mr.sharedMaterial;
            var key = (source, emission);
            if (!glowMats.TryGetValue(key, out Material m))
            {
                m = new Material(source);
                SceneryMaterials.SetEmission(m, emission);
                glowMats[key] = m;
                owned.Add(m);
            }
            mr.sharedMaterial = m;
        }

        private Material MatFor(Color color)
        {
            if (mats.TryGetValue(color, out Material m)) return m;
            m = SceneryMaterials.Create(color);
            if (SceneryMaterials.IsTranslucent(color)) SceneryMaterials.MakeFade(m);   // 연기·안개 덩이
            mats[color] = m;
            owned.Add(m);
            return m;
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private void OnDestroy()
        {
            foreach (Material m in owned) if (m != null) Destroy(m);
            owned.Clear();
            mats.Clear();
            glowMats.Clear();
        }
    }
}
