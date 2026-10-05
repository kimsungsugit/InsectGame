using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.NPC
{
    /// <summary>모자 모양. 기본값(0)이 옛 야구모자라 기존 외형 정의는 그대로 읽힌다.</summary>
    public enum NpcHatStyle { Cap, Straw, Wide, Beanie, Helmet, Headwrap, Bandana, Hood }

    /// <summary>겉옷. 색은 <see cref="NpcAppearance.wearColor"/> — 비어 있으면(알파 0) 상의색에서 파생.</summary>
    public enum NpcWear { None, Vest, Apron, Coat, Robe }

    public enum NpcBag { None, Satchel, Backpack }

    /// <summary>오른손 도구. 아이는 도구가 없을 때만 뜰채를 든다.</summary>
    public enum NpcTool { None, Axe, Pickaxe, Staff, Lantern, Book, Scroll, Brush, Shears, Basket, Ledger }

    /// <summary>NPC 외형 파라미터 — System.Random(seed) 기반 결정적 생성.</summary>
    public struct NpcAppearance
    {
        public bool isChild;
        public int hairStyle;   // 0 짧은머리 / 1 중간머리 / 2 올림머리
        public bool hasHat;
        public Color hair;
        public Color top;
        public Color bottom;
        public Color skin;
        public Color hat;

        // ── 옷차림(테마) ── 전부 기본값이 "없음"이라 옛 정의는 옛 모습 그대로다.
        public NpcHatStyle hatStyle;
        public NpcWear wear;
        public Color wearColor;
        public bool scarf;
        public Color scarfColor;
        public NpcBag bag;
        public NpcTool tool;
        public bool glasses;
        /// <summary>명부회 표식 — 왼팔 완장 + 가슴 배지. 제복이 색만 같던 걸 표식으로 묶는다.</summary>
        public bool ledgerMark;
    }

    /// <summary>
    /// NPC 프로시저럴 모델 빌더 — PlayerVisualBuilder.BuildAll 지오메트리의 단순화 이식.
    /// PlayerPrefs/CharacterOutfitManager 의존 없음. 노드명(Body/Shirt/HeadPivot/ArmL/ArmR/
    /// LegLPivot/LegRPivot/NetHandle/NetRing)은 NpcWalkAnimator가 transform.Find로 캐시하므로 변경 금지.
    /// 콜라이더는 몸통 캡슐(트리거) 하나만 루트에 부착 — 파츠 콜라이더는 전부 Destroy.
    /// (트리거로 두어 PlayerMovement의 이동 차단/끼임 감지에 걸리지 않게 함.)
    /// 생성된 머티리얼 정리는 NPC 컴포넌트 OnDestroy에서 CleanupMaterials 호출.
    /// </summary>
    public static class NpcVisualBuilder
    {
        // ── 팔레트 (결정적 랜덤 변주용) ──
        private static readonly Color[] HairPalette =
        {
            new Color(0.12f, 0.08f, 0.05f),
            new Color(0.35f, 0.2f, 0.1f),
            new Color(0.55f, 0.42f, 0.28f),
            new Color(0.45f, 0.45f, 0.5f),
            new Color(0.2f, 0.15f, 0.35f),
        };

        private static readonly Color[] TopPalette =
        {
            new Color(0.75f, 0.3f, 0.25f),
            new Color(0.25f, 0.5f, 0.75f),
            new Color(0.35f, 0.6f, 0.35f),
            new Color(0.85f, 0.7f, 0.4f),
            new Color(0.6f, 0.45f, 0.7f),
            new Color(0.9f, 0.88f, 0.82f),
        };

        private static readonly Color[] KidTopPalette =
        {
            new Color(1.0f, 0.55f, 0.3f),
            new Color(0.4f, 0.75f, 0.95f),
            new Color(0.55f, 0.85f, 0.4f),
            new Color(0.95f, 0.8f, 0.3f),
            new Color(0.9f, 0.5f, 0.7f),
        };

        private static readonly Color[] BottomPalette =
        {
            new Color(0.18f, 0.22f, 0.28f),
            new Color(0.35f, 0.28f, 0.2f),
            new Color(0.25f, 0.35f, 0.3f),
            new Color(0.4f, 0.4f, 0.45f),
        };

        /// <summary>
        /// NPC 피부 <b>다양성</b>용 4색. 플레이어의 <c>CharacterPalette.Skin</c>(생성 화면의
        /// "밝은/보통/어두운/진한" 선택지)과는 성격이 다르므로 통합하지 않는다 — 인덱스를 그대로
        /// 옮기면 스토리 NPC 9종의 고정 외형이 통째로 바뀐다(그건 이 작업의 목표가 아니다).
        /// 첫 항이 플레이어의 옛 하드코딩 피부색과 같은 값인 건 우연이 아니라 복제의 흔적이다.
        /// </summary>
        private static readonly Color[] SkinPalette =
        {
            new Color(0.92f, 0.78f, 0.62f),
            new Color(0.85f, 0.68f, 0.5f),
            new Color(0.95f, 0.83f, 0.7f),
            new Color(0.72f, 0.55f, 0.4f),
        };

        private static readonly Color[] HatPalette =
        {
            new Color(1.0f, 0.65f, 0.2f),
            new Color(0.3f, 0.45f, 0.65f),
            new Color(0.55f, 0.35f, 0.25f),
            new Color(0.85f, 0.3f, 0.3f),
        };

        /// <summary>seed 기반 결정적 성인 주민 외형(리전 복장 없음 — 초원 기준).</summary>
        public static NpcAppearance RandomVillager(int seed) => RandomVillager(seed, null);

        /// <summary>seed 기반 결정적 아이 외형(리전 복장 없음 — 초원 기준).</summary>
        public static NpcAppearance RandomKid(int seed) => RandomKid(seed, null);

        /// <summary>
        /// 리전 복장을 입힌 주민. 옛날엔 리전과 무관해서 서릿길 주민도 모래언덕 주민도 같은 반팔이었다.
        /// 1막은 한국 시골 마을풍(밀짚모자·조끼·앞치마·수건), 2막은 기후 실용복(털모자·두건·안전모·코트)이다.
        /// 옷차림은 <b>별도 난수</b>(seed 파생)로 뽑는다 — 기존 색·머리 추첨 순서를 건드리면 주민 얼굴이 통째로 바뀐다.
        /// </summary>
        public static NpcAppearance RandomVillager(int seed, string regionId)
        {
            NpcAppearance a = RandomVillagerBase(seed);
            DressForRegion(ref a, regionId, new System.Random(seed ^ 0x5eed));
            CoverTop(ref a);
            return a;
        }

        /// <summary>리전 복장을 입힌 곤충잡이 아이 — 뜰채는 그대로 들고, 모자·목도리만 기후를 따른다.</summary>
        public static NpcAppearance RandomKid(int seed, string regionId)
        {
            NpcAppearance a = RandomKidBase(seed);
            DressKidForRegion(ref a, regionId, new System.Random(seed ^ 0x5eed));
            return a;
        }

        private static Color Pick(System.Random rng, params Color[] options) => options[rng.Next(options.Length)];

        private static void DressForRegion(ref NpcAppearance a, string regionId, System.Random rng)
        {
            Color straw = new Color(0.86f, 0.74f, 0.46f);
            switch (regionId)
            {
                case "forest":   // 나무꾼·숲지기
                    a.hasHat = rng.NextDouble() < 0.6; a.hatStyle = NpcHatStyle.Beanie;
                    a.hat = Pick(rng, new Color(0.45f, 0.30f, 0.18f), new Color(0.30f, 0.42f, 0.26f));
                    a.wear = NpcWear.Vest; a.wearColor = Pick(rng, new Color(0.40f, 0.30f, 0.18f), new Color(0.32f, 0.40f, 0.24f));
                    a.tool = rng.NextDouble() < 0.4 ? NpcTool.Axe : NpcTool.None;
                    a.bag = rng.NextDouble() < 0.3 ? NpcBag.Backpack : NpcBag.None;
                    break;
                case "swamp":    // 우비와 챙 넓은 비모자
                    a.hasHat = true; a.hatStyle = NpcHatStyle.Wide; a.hat = new Color(0.36f, 0.40f, 0.26f);
                    a.wear = rng.NextDouble() < 0.6 ? NpcWear.Coat : NpcWear.Vest;
                    a.wearColor = Pick(rng, new Color(0.40f, 0.44f, 0.26f), new Color(0.46f, 0.40f, 0.24f));
                    a.tool = rng.NextDouble() < 0.4 ? NpcTool.Basket : NpcTool.Staff;
                    break;
                case "mountain": // 등산객·봉우리 사람들
                    a.hasHat = rng.NextDouble() < 0.7; a.hatStyle = NpcHatStyle.Beanie;
                    a.hat = Pick(rng, new Color(0.78f, 0.28f, 0.22f), new Color(0.26f, 0.40f, 0.62f), new Color(0.90f, 0.72f, 0.26f));
                    a.wear = rng.NextDouble() < 0.5 ? NpcWear.Coat : NpcWear.Vest;
                    a.wearColor = Pick(rng, new Color(0.30f, 0.36f, 0.46f), new Color(0.46f, 0.34f, 0.24f));
                    a.bag = NpcBag.Backpack;
                    a.tool = rng.NextDouble() < 0.5 ? NpcTool.Staff : NpcTool.None;
                    break;
                case "ruins":    // 탐사대
                    a.hasHat = true; a.hatStyle = NpcHatStyle.Wide; a.hat = new Color(0.66f, 0.58f, 0.42f);
                    a.wear = NpcWear.Vest; a.wearColor = new Color(0.58f, 0.50f, 0.36f);
                    a.bag = NpcBag.Satchel;
                    a.tool = rng.NextDouble() < 0.5 ? NpcTool.Book : NpcTool.Brush;
                    a.glasses = rng.NextDouble() < 0.3;
                    break;
                case "hollow":   // 떠나지 못한 사람들 — 빛바랜 두건과 목도리
                    a.top = Color.Lerp(a.top, new Color(0.6f, 0.58f, 0.52f), 0.55f);
                    a.hasHat = rng.NextDouble() < 0.6; a.hatStyle = NpcHatStyle.Bandana; a.hat = new Color(0.62f, 0.58f, 0.50f);
                    a.scarf = true; a.scarfColor = new Color(0.70f, 0.66f, 0.58f);
                    a.wear = rng.NextDouble() < 0.5 ? NpcWear.Vest : NpcWear.None; a.wearColor = new Color(0.50f, 0.47f, 0.40f);
                    break;
                case "dunes":    // 사막 행상 — 두건·로브·얼굴 가리개
                    a.hasHat = true; a.hatStyle = NpcHatStyle.Headwrap;
                    a.hat = Pick(rng, new Color(0.92f, 0.86f, 0.70f), new Color(0.76f, 0.40f, 0.26f), new Color(0.30f, 0.46f, 0.62f));
                    a.wear = NpcWear.Robe; a.wearColor = Pick(rng, new Color(0.86f, 0.76f, 0.56f), new Color(0.72f, 0.58f, 0.40f));
                    a.scarf = rng.NextDouble() < 0.6; a.scarfColor = a.hat;
                    a.bag = rng.NextDouble() < 0.5 ? NpcBag.Satchel : NpcBag.None;
                    break;
                case "frostline": // 털모자·솜 코트·목도리
                    a.hasHat = true; a.hatStyle = NpcHatStyle.Beanie;
                    a.hat = Pick(rng, new Color(0.80f, 0.26f, 0.24f), new Color(0.26f, 0.38f, 0.62f), new Color(0.92f, 0.92f, 0.90f));
                    a.wear = NpcWear.Coat; a.wearColor = Pick(rng, new Color(0.30f, 0.40f, 0.56f), new Color(0.56f, 0.30f, 0.26f), new Color(0.40f, 0.44f, 0.36f));
                    a.scarf = true; a.scarfColor = Pick(rng, new Color(0.86f, 0.30f, 0.26f), new Color(0.94f, 0.80f, 0.36f), new Color(0.92f, 0.92f, 0.90f));
                    a.bag = rng.NextDouble() < 0.4 ? NpcBag.Backpack : NpcBag.None;
                    break;
                case "emberfall": // 광부 — 안전모·가죽 앞치마·곡괭이
                    a.hasHat = true; a.hatStyle = NpcHatStyle.Helmet; a.hat = Pick(rng, new Color(0.95f, 0.62f, 0.18f), new Color(0.92f, 0.80f, 0.24f));
                    a.wear = NpcWear.Apron; a.wearColor = new Color(0.40f, 0.28f, 0.18f);
                    a.tool = rng.NextDouble() < 0.5 ? NpcTool.Pickaxe : NpcTool.Lantern;
                    break;
                case "canopy":   // 나무 위 사람들 — 잎빛 조끼, 열매 바구니
                    a.hasHat = rng.NextDouble() < 0.5; a.hatStyle = NpcHatStyle.Bandana; a.hat = new Color(0.32f, 0.54f, 0.26f);
                    a.wear = NpcWear.Vest; a.wearColor = new Color(0.30f, 0.50f, 0.26f);
                    a.bag = NpcBag.Satchel;
                    a.tool = rng.NextDouble() < 0.5 ? NpcTool.Basket : NpcTool.None;
                    break;
                case "nameless":
                    a.hasHat = true; a.hatStyle = NpcHatStyle.Hood; a.hat = new Color(0.44f, 0.43f, 0.48f);
                    a.wear = NpcWear.Robe; a.wearColor = new Color(0.42f, 0.41f, 0.46f);
                    break;
                case "garden":   // 정원사 — 챙 넓은 모자·앞치마·전지가위
                    a.hasHat = rng.NextDouble() < 0.7; a.hatStyle = NpcHatStyle.Wide; a.hat = Pick(rng, straw, new Color(0.96f, 0.66f, 0.30f));
                    a.wear = NpcWear.Apron; a.wearColor = Pick(rng, new Color(0.42f, 0.58f, 0.34f), new Color(0.86f, 0.80f, 0.66f));
                    a.tool = rng.NextDouble() < 0.5 ? NpcTool.Shears : NpcTool.Basket;
                    break;
                case "pond":     // 나루터 — 밀짚모자·목수건
                    a.hasHat = rng.NextDouble() < 0.7; a.hatStyle = NpcHatStyle.Straw; a.hat = straw;
                    a.scarf = rng.NextDouble() < 0.5; a.scarfColor = new Color(0.94f, 0.94f, 0.90f);
                    a.wear = rng.NextDouble() < 0.4 ? NpcWear.Vest : NpcWear.None; a.wearColor = new Color(0.30f, 0.42f, 0.56f);
                    break;
                default:         // 초원(본 마을) — 한국 시골 마을풍
                    a.hasHat = rng.NextDouble() < 0.5; a.hatStyle = NpcHatStyle.Straw; a.hat = straw;
                    a.scarf = rng.NextDouble() < 0.3; a.scarfColor = new Color(0.94f, 0.94f, 0.90f);   // 목에 두른 수건
                    double w = rng.NextDouble();
                    a.wear = w < 0.3 ? NpcWear.Vest : w < 0.55 ? NpcWear.Apron : NpcWear.None;
                    a.wearColor = Pick(rng, new Color(0.42f, 0.32f, 0.22f), new Color(0.30f, 0.40f, 0.52f), new Color(0.86f, 0.80f, 0.66f));
                    a.tool = rng.NextDouble() < 0.2 ? NpcTool.Basket : NpcTool.None;
                    break;
            }
        }

        /// <summary>
        /// 코트·로브는 상체도 덮는다 — 몸통·팔(상의색)을 겉옷색으로 바꾼다. 스토리 NPC는 여기를 거치지 않는다
        /// (상의색이 대사창 초상과 짝이라 바꾸면 두 화면의 인상이 갈린다).
        /// </summary>
        private static void CoverTop(ref NpcAppearance a)
        {
            if (a.wear == NpcWear.Coat || a.wear == NpcWear.Robe) a.top = a.wearColor;
        }

        private static void DressKidForRegion(ref NpcAppearance a, string regionId, System.Random rng)
        {
            switch (regionId)
            {
                case "frostline":
                    a.hasHat = true; a.hatStyle = NpcHatStyle.Beanie;
                    a.scarf = true; a.scarfColor = Pick(rng, new Color(0.94f, 0.80f, 0.36f), new Color(0.86f, 0.30f, 0.26f));
                    a.wear = NpcWear.Coat; a.wearColor = a.top;
                    break;
                case "dunes":
                    a.hasHat = true; a.hatStyle = NpcHatStyle.Headwrap; a.hat = new Color(0.94f, 0.88f, 0.72f);
                    break;
                case "mountain":
                    a.bag = NpcBag.Backpack;
                    break;
                case "canopy":
                case "forest":
                    a.hatStyle = NpcHatStyle.Bandana;
                    break;
                default:
                    if (rng.NextDouble() < 0.4)
                    {
                        a.hatStyle = NpcHatStyle.Straw;
                        a.hat = new Color(0.86f, 0.74f, 0.46f);   // 밀짚은 밀짚색 — 모자 팔레트(파랑·빨강)를 그대로 쓰면 색칠한 판자 같다
                    }
                    break;
            }
        }

        private static NpcAppearance RandomVillagerBase(int seed)
        {
            System.Random rng = new System.Random(seed);
            return new NpcAppearance
            {
                isChild = false,
                hairStyle = rng.Next(0, 3),
                hasHat = rng.NextDouble() < 0.4,
                hair = HairPalette[rng.Next(HairPalette.Length)],
                top = TopPalette[rng.Next(TopPalette.Length)],
                bottom = BottomPalette[rng.Next(BottomPalette.Length)],
                skin = SkinPalette[rng.Next(SkinPalette.Length)],
                hat = HatPalette[rng.Next(HatPalette.Length)],
            };
        }

        /// <summary>seed 기반 결정적 아이 외형 — 밝은 상의 팔레트 + 모자 확률 높음.</summary>
        private static NpcAppearance RandomKidBase(int seed)
        {
            System.Random rng = new System.Random(seed);
            return new NpcAppearance
            {
                isChild = true,
                hairStyle = rng.Next(0, 3),
                hasHat = rng.NextDouble() < 0.55,
                hair = HairPalette[rng.Next(HairPalette.Length)],
                top = KidTopPalette[rng.Next(KidTopPalette.Length)],
                bottom = BottomPalette[rng.Next(BottomPalette.Length)],
                skin = SkinPalette[rng.Next(SkinPalette.Length)],
                hat = HatPalette[rng.Next(HatPalette.Length)],
            };
        }

        /// <summary>
        /// 스토리 NPC 고정 외형 — 구분되는 실루엣. seed 대신 storyNpcId로 결정.
        /// 동행자(어르신/라온/세라)와 명부회 4인(관장·집게·저울·먹).
        /// <b>얼굴·색은 <see cref="StoryNpcFace"/>, 옷차림·소품은 <see cref="StorySignature"/></b>에 등록한다.
        /// **StoryNpcFace에 case를 빠뜨리면 그 NPC가 default(마을 어르신) 외형으로 뜬다** —
        /// <see cref="NpcManager.StoryNpcDisplayName"/>의 이름 switch와 짝이다(둘 다 등록할 것, story_lint 22가 읽는다).
        /// 명부회는 아이보리 상의 + 남색 하의로 제복처럼 통일하고 머리·모자로 개체를 가른다
        /// (세라의 보라 상의와 겹치지 않게 함).
        /// </summary>
        public static NpcAppearance StoryNpcAppearance(string storyNpcId)
        {
            NpcAppearance a = StoryNpcFace(storyNpcId);
            StorySignature(ref a, storyNpcId);
            return a;
        }

        /// <summary>
        /// 스토리 NPC의 옷차림·소품 — 주석으로만 약속하던 것(밀짚모자·털모자·안전모·챙 넓은 모자·도끼·책)을
        /// 실제 모양으로 만든다. 색·머리(얼굴)와 모자를 쓰는지(hasHat)·모자색(hat)은 <see cref="StoryNpcFace"/>가 정하고,
        /// 여기엔 모양(hatStyle)과 겉옷·목도리·가방·도구만 둔다. 대사창 초상은 둘을 합친 <see cref="StoryNpcAppearance"/>를
        /// 받으므로(몸통은 겉옷 색) 어느 쪽을 고쳐도 대사창이 따라간다 — 나눈 건 역할 구분이지 초상 때문이 아니다.
        /// </summary>
        private static void StorySignature(ref NpcAppearance a, string storyNpcId)
        {
            Color ivory = TopPalette[5];
            Color navy = new Color(0.18f, 0.22f, 0.30f);
            switch (storyNpcId)
            {
                // 명부회 — 간부는 아이보리 긴 코트, 하수는 아이보리 상의 위 남색 조끼. 넷 다 완장·배지.
                // 1막 하수가 간부와 같은 아이보리를 입는 게 단서라(StoryNpcFace 주석) 상의는 건드리지 않는다.
                case "ledger_chief": a.wear = NpcWear.Coat; a.wearColor = ivory; a.ledgerMark = true; a.tool = NpcTool.Ledger; a.glasses = true; break;
                case "ledger_grip": a.wear = NpcWear.Coat; a.wearColor = ivory; a.ledgerMark = true; a.bag = NpcBag.Satchel; break;
                case "ledger_scale": a.wear = NpcWear.Coat; a.wearColor = ivory; a.ledgerMark = true; a.glasses = true; a.tool = NpcTool.Book; break;
                case "ledger_ink": a.wear = NpcWear.Coat; a.wearColor = ivory; a.ledgerMark = true; a.hatStyle = NpcHatStyle.Beanie; a.tool = NpcTool.Brush; a.bag = NpcBag.Satchel; break;
                case "ledger_thug_cord": a.wear = NpcWear.Vest; a.wearColor = navy; a.ledgerMark = true; a.hatStyle = NpcHatStyle.Wide; break;
                case "ledger_thug_rule": a.wear = NpcWear.Vest; a.wearColor = navy; a.ledgerMark = true; a.tool = NpcTool.Staff; break;
                case "ledger_thug_pin": a.wear = NpcWear.Vest; a.wearColor = navy; a.ledgerMark = true; break;
                case "catcher_rival": a.bag = NpcBag.Backpack; break;                                   // 라온 — 뜰채 + 채집 배낭
                case "ruins_scholar": a.glasses = true; a.bag = NpcBag.Satchel; a.tool = NpcTool.Book;   // 세라 — 안경·책·가방
                    a.wear = NpcWear.Coat; a.wearColor = new Color(0.36f, 0.28f, 0.46f); break;
                case "town_meadow": a.tool = NpcTool.Book; break;                                      // 달래 — 그림책을 든 아이
                case "town_pond": a.hatStyle = NpcHatStyle.Straw;   // 물결 할머니 — 밀짚모자(밀짚색은 StoryNpcFace)
                    a.scarf = true; a.scarfColor = new Color(0.94f, 0.94f, 0.90f); a.tool = NpcTool.Staff; break;
                case "town_forest": a.hatStyle = NpcHatStyle.Beanie; a.tool = NpcTool.Axe;              // 솔 — 나무꾼
                    a.wear = NpcWear.Vest; a.wearColor = new Color(0.40f, 0.30f, 0.18f); break;
                case "town_swamp": a.tool = NpcTool.Basket; a.wear = NpcWear.Apron; a.wearColor = new Color(0.44f, 0.46f, 0.30f); break;   // 이끼 — 약초 바구니
                case "town_mountain": a.hatStyle = NpcHatStyle.Beanie; a.tool = NpcTool.Lantern;        // 너울 — 봉수지기
                    a.wear = NpcWear.Coat; a.wearColor = Color.Lerp(a.top, Color.black, 0.2f); break;
                case "town_garden": a.hatStyle = NpcHatStyle.Wide; a.tool = NpcTool.Shears;             // 누리 — 챙 넓은 모자·전지가위
                    a.wear = NpcWear.Apron; a.wearColor = new Color(0.86f, 0.80f, 0.66f); break;
                case "town_ruins": a.bag = NpcBag.Satchel; a.tool = NpcTool.Scroll; break;             // 결 — 탁본 두루마리
                case "town_hollow": a.scarf = true; a.scarfColor = new Color(0.72f, 0.70f, 0.62f); a.tool = NpcTool.Lantern; break;   // 메아리 — 풍경지기
                case "town_dunes": a.hatStyle = NpcHatStyle.Wide; a.wear = NpcWear.Robe; a.wearColor = new Color(0.84f, 0.72f, 0.50f);   // 모래 — 떠돌이 상인
                    a.bag = NpcBag.Backpack; break;
                case "town_frostline": a.hatStyle = NpcHatStyle.Beanie; a.wear = NpcWear.Coat; a.wearColor = new Color(0.34f, 0.44f, 0.58f);   // 서리 — 필사생
                    a.scarf = true; a.scarfColor = new Color(0.92f, 0.92f, 0.90f); a.tool = NpcTool.Scroll; break;
                case "town_emberfall": a.hatStyle = NpcHatStyle.Helmet; a.wear = NpcWear.Apron; a.wearColor = new Color(0.40f, 0.28f, 0.18f);   // 숯 — 광부
                    a.tool = NpcTool.Pickaxe; break;
                case "town_canopy": a.hatStyle = NpcHatStyle.Bandana; a.tool = NpcTool.Basket; break;   // 잎새 — 잎 두건(쓰는지·잎색은 StoryNpcFace)
                default: // village_elder — 챙 넓은 모자·조끼·지팡이
                    a.hatStyle = NpcHatStyle.Wide; a.wear = NpcWear.Vest; a.wearColor = new Color(0.44f, 0.34f, 0.24f); a.tool = NpcTool.Staff; break;
            }
        }

        /// <summary>
        /// 스토리 NPC 얼굴·색(피부·머리·상의·모자색). 대사창 초상(<c>NpcDialogueUI.GetStoryPortrait</c>)이 <b>이 값을 그대로 받는다</b> —
        /// 초상 쪽에 색 사본이 없으므로 여기를 고치면 필드와 대사창이 함께 바뀐다.
        /// </summary>
        private static NpcAppearance StoryNpcFace(string storyNpcId)
        {
            switch (storyNpcId)
            {
                case "ledger_chief": // 하월(관장) — 백발·모자 없음. 명부회 수장
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 1, hasHat = false,
                        hair = HairPalette[3], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[1], hat = HatPalette[1],
                    };
                case "ledger_grip": // 집게 — 포획반장. 짧은머리 + 붉은 모자
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = true,
                        hair = HairPalette[0], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[3], hat = HatPalette[3],
                    };
                case "ledger_scale": // 저울 — 분류관. 올림머리 + 모자 없음
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = false,
                        hair = HairPalette[4], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[2], hat = HatPalette[1],
                    };
                case "ledger_ink": // 먹 — 필경사. 올림머리 + 청회 모자
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = true,
                        hair = HairPalette[0], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[0], hat = HatPalette[1],
                    };
                // 1막 하수 2인 — **간부와 같은 상의(TopPalette[5])를 입는다.** 그게 유일한 단서다.
                // 2막에서 집게·저울을 만나면 "저 옷을 어디서 봤더라"가 되게 하려는 것이라
                // 색을 다르게 하면 안 된다. 대신 모자로 둘을 구분한다.
                case "ledger_thug_cord": // 끈 — 그물 담당. 챙 깊은 모자로 얼굴을 가린다
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = true,
                        hair = HairPalette[0], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[2], hat = HatPalette[1],
                    };
                case "ledger_thug_rule": // 자 — 측량 담당. 모자 없이 묶은 머리
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = false,
                        hair = HairPalette[3], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[1], hat = HatPalette[1],
                    };
                case "ledger_thug_pin": // 핀 — 그물터 말단. 모자 있고 앞머리를 덮었다(가장 어리다)
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 1, hasHat = true,
                        hair = HairPalette[1], top = TopPalette[5], bottom = BottomPalette[0],
                        skin = SkinPalette[0], hat = HatPalette[1],
                    };
                case "catcher_rival": // 라온 — 곤충잡이 아이(뜰채·모자·밝은 상의)
                    return new NpcAppearance
                    {
                        isChild = true, hairStyle = 0, hasHat = true,
                        hair = HairPalette[1], top = KidTopPalette[0], bottom = BottomPalette[1],
                        skin = SkinPalette[2], hat = HatPalette[0],
                    };
                case "ruins_scholar": // 세라 — 학자(올림머리·모자 없음·보라 상의)
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = false,
                        hair = HairPalette[0], top = TopPalette[4], bottom = BottomPalette[0],
                        skin = SkinPalette[0], hat = HatPalette[1],
                    };
                // ── 마을 이야기 주민 12인(1막 일곱 + 2막 다섯) ── 대사창 초상(NpcDialogueUI.GetStoryPortrait)도 이 색을 받는다.
                // 리전이 서로 떨어져 있어 한 화면에 둘이 서지 않으므로 색이 겹쳐도 되지만,
                // **같은 리전의 동행자·검은 옷(TopPalette[5])과는 겹치지 않게** 골랐다.
                case "town_meadow": // 달래 — 그림책을 든 아이. 분홍 상의, 묶은 머리
                    return new NpcAppearance
                    {
                        isChild = true, hairStyle = 2, hasHat = false,
                        hair = HairPalette[1], top = KidTopPalette[4], bottom = BottomPalette[2],
                        skin = SkinPalette[2], hat = HatPalette[0],
                    };
                case "town_pond": // 물결 할머니 — 나루터. 물빛 상의, 백발 쪽머리, 밀짚모자
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = true,
                        hair = HairPalette[3], top = TopPalette[1], bottom = BottomPalette[1],
                        skin = SkinPalette[1], hat = new Color(0.86f, 0.74f, 0.46f),   // 밀짚색 — 모자 팔레트 주황이면 밀짚모자가 색칠한 판자 같다
                    };
                case "town_forest": // 솔 — 나무꾼. 붉은 상의, 갈색 모자
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = true,
                        hair = HairPalette[0], top = TopPalette[0], bottom = BottomPalette[1],
                        skin = SkinPalette[3], hat = HatPalette[2],
                    };
                case "town_swamp": // 이끼 — 약초꾼. 풀색 상의, 묶은 머리, 모자 없음
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = false,
                        hair = HairPalette[1], top = TopPalette[2], bottom = BottomPalette[2],
                        skin = SkinPalette[0], hat = HatPalette[2],
                    };
                case "town_mountain": // 너울 — 봉수지기 노인. 어르신과 같은 백발이라 모자색(청회)으로 가른다
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 1, hasHat = true,
                        hair = HairPalette[3], top = TopPalette[3], bottom = BottomPalette[3],
                        skin = SkinPalette[3], hat = HatPalette[1],
                    };
                case "town_garden": // 누리 — 정원사. 연두 상의, 챙 넓은 주황 모자
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 1, hasHat = true,
                        hair = HairPalette[2], top = KidTopPalette[2], bottom = BottomPalette[2],
                        skin = SkinPalette[2], hat = HatPalette[0],
                    };
                case "town_ruins": // 결 — 탁본꾼(세라의 옛 조수). 푸른 상의, 짙은 보라 머리
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = false,
                        hair = HairPalette[4], top = TopPalette[1], bottom = BottomPalette[0],
                        skin = SkinPalette[0], hat = HatPalette[1],
                    };
                // ── 2막 다섯 ── 같은 리전의 세라(TopPalette[4])·라온(KidTopPalette[0])·명부회(TopPalette[5])와 겹치지 않게.
                case "town_hollow": // 메아리 — 풍경지기. 하늘색 상의, 모자 없음
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 1, hasHat = false,
                        hair = HairPalette[2], top = TopPalette[1], bottom = BottomPalette[3],
                        skin = SkinPalette[1], hat = HatPalette[1],
                    };
                case "town_dunes": // 모래 — 떠돌이 상인. 모래빛 상의, 주황 햇볕 모자
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = true,
                        hair = HairPalette[0], top = TopPalette[3], bottom = BottomPalette[1],
                        skin = SkinPalette[3], hat = HatPalette[0],
                    };
                case "town_frostline": // 서리 — 필사생. 얼음빛 상의, 청회 털모자
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 2, hasHat = true,
                        hair = HairPalette[4], top = KidTopPalette[1], bottom = BottomPalette[0],
                        skin = SkinPalette[2], hat = HatPalette[1],
                    };
                case "town_emberfall": // 숯 — 광부. 잿빛 상의, 주황 안전모
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = true,
                        hair = HairPalette[0], top = BottomPalette[3], bottom = BottomPalette[0],
                        skin = SkinPalette[3], hat = HatPalette[0],
                    };
                case "town_canopy": // 잎새 — 나무 위에서 자란 아이. 노란 상의, 잎빛 두건
                    return new NpcAppearance
                    {
                        isChild = true, hairStyle = 1, hasHat = true,
                        hair = HairPalette[1], top = KidTopPalette[3], bottom = BottomPalette[2],
                        skin = SkinPalette[0], hat = new Color(0.34f, 0.58f, 0.28f),
                    };
                default: // village_elder — 마을 어르신(백발·모자·따뜻한 상의)
                    return new NpcAppearance
                    {
                        isChild = false, hairStyle = 0, hasHat = true,
                        hair = HairPalette[3], top = TopPalette[3], bottom = BottomPalette[1],
                        skin = SkinPalette[1], hat = HatPalette[2],
                    };
            }
        }

        /// <summary>root 아래에 NPC 모델 생성. 아이는 루트 스케일 0.75 + 뜰채(NetHandle/NetRing) 부착.</summary>
        public static void Build(Transform root, NpcAppearance a)
        {
            if (root == null) return;
            partMats.Clear();

            Material topMat = MakeMaterial(a.top, SurfaceKind.Cloth);
            Material bottomMat = MakeMaterial(a.bottom, SurfaceKind.Cloth);
            Material skinMat = MakeMaterial(a.skin, SurfaceKind.Skin);
            Material hairMat = MakeMaterial(a.hair, SurfaceKind.Hair);
            Material shirtMat = MakeMaterial(Color.Lerp(a.top, Color.white, 0.45f), SurfaceKind.Cloth);
            Material shoesMat = MakeMaterial(new Color(0.2f, 0.12f, 0.06f), SurfaceKind.Leather);

            // ── 몸통 (둥근 상자 — 플레이어 치비 비례 이식, NpcWalkAnimator가 "Body"명으로 캐시) ──
            MakeBoxPart("Body", root, new Vector3(0f, 0.77f, 0f),
                new Vector3(0.46f, 0.46f, 0.36f), 0.085f, 3, topMat);

            // ── 셔츠 (앞면 패널) ──
            // 플레이어와 같은 이유로 좁혔다 — 넓으면 흰 판이 앞을 덮어 상의 색이 안 보인다.
            MakeBoxPart("Shirt", root, new Vector3(0f, 0.83f, 0.10f),
                new Vector3(0.24f, 0.36f, 0.20f), 0.05f, 2, shirtMat);

            MakePart(PrimitiveType.Cylinder, "Neck", root,
                new Vector3(0f, 1f, 0.02f), new Vector3(0.14f, 0.05f, 0.12f), skinMat);

            // ── 머리 (HeadPivot 컨테이너 + Head 구) ──
            GameObject headPivot = new GameObject("HeadPivot");
            headPivot.transform.SetParent(root, false);
            headPivot.transform.localPosition = PlayerVisualBuilder.HeadAnchor;
            headPivot.transform.localScale = Vector3.one * 0.60f;

            MakeMeshPart("Head", headPivot.transform, UnitSphere(10, 14),
                Vector3.zero, new Vector3(0.70f, 0.68f, 0.68f), skinMat);

            // ── 눈 (흰자 + 동공) ──
            Material eyeMat = MakeMaterial(Color.white, SurfaceKind.Wet);
            Material pupilMat = MakeMaterial(new Color(0.12f, 0.08f, 0.05f), SurfaceKind.Wet);
            Mesh eyeMesh = UnitDisc(16);
            Mesh pupilMesh = UnitDisc(12);
            MakeMeshPart("EyeL", headPivot.transform, eyeMesh,
                new Vector3(-0.12f, -0.03f, 0.32f), new Vector3(0.15f, 0.17f, 0.06f), eyeMat);
            MakeMeshPart("EyeR", headPivot.transform, eyeMesh,
                new Vector3(0.12f, -0.03f, 0.32f), new Vector3(0.15f, 0.17f, 0.06f), eyeMat);
            MakeMeshPart("PupilL", headPivot.transform, pupilMesh,
                new Vector3(-0.12f, -0.04f, 0.35f), new Vector3(0.09f, 0.11f, 0.02f), pupilMat);
            MakeMeshPart("PupilR", headPivot.transform, pupilMesh,
                new Vector3(0.12f, -0.04f, 0.35f), new Vector3(0.09f, 0.11f, 0.02f), pupilMat);

            // ── 머리카락 (스타일 3종 단순 변주) ──
            // 머리를 감싸는 모자(털모자·안전모·두건·후드) 밑으로 올림머리 번이 뚫고 나오지 않게 뺀다
            bool covered = a.hasHat && (a.hatStyle == NpcHatStyle.Beanie || a.hatStyle == NpcHatStyle.Helmet
                || a.hatStyle == NpcHatStyle.Headwrap || a.hatStyle == NpcHatStyle.Hood);
            BuildHair(headPivot.transform, covered && a.hairStyle == 2 ? 0 : a.hairStyle, hairMat);

            // ── 모자 ──
            if (a.hasHat) BuildHat(headPivot.transform, a.hatStyle, a.hat);
            if (a.glasses) BuildGlasses(headPivot.transform);

            // ── 팔 ──
            Mesh armMesh = UnitCapsule(0.72f);
            MakeMeshPart("ArmL", root, armMesh,
                new Vector3(-0.29f, 0.78f, 0f), new Vector3(0.135f, 0.23f, 0.135f), topMat);
            MakeMeshPart("ArmR", root, armMesh,
                new Vector3(0.29f, 0.78f, 0f), new Vector3(0.135f, 0.23f, 0.135f), topMat);

            // ── 손 (미튼) ──
            Vector3 handSize = new Vector3(0.105f, 0.135f, 0.095f);
            MakeBoxPart("HandL", root, new Vector3(-0.29f, 0.52f, 0f), handSize, 0.042f, 2, skinMat);
            MakeBoxPart("HandR", root, new Vector3(0.29f, 0.52f, 0f), handSize, 0.042f, 2, skinMat);

            // ── 다리 + 부츠 (LegPivot로 묶어 회전 시 발도 함께 — 플레이어와 동일 구조) ──
            BuildLeg(root, "L", -0.13f, bottomMat, shoesMat);
            BuildLeg(root, "R", 0.13f, bottomMat, shoesMat);

            // ── 겉옷·목도리·가방·도구·명부회 표식 ──
            Transform body = root.Find("Body");
            if (body != null)
            {
                if (a.wear != NpcWear.None) BuildWear(body, a.wear, a.wearColor.a > 0.01f ? a.wearColor : Color.Lerp(a.top, Color.black, 0.25f));
                if (a.scarf) BuildScarf(body, a.scarfColor.a > 0.01f ? a.scarfColor : Color.white);
                if (a.bag != NpcBag.None) BuildBag(body, a.bag);
                if (a.ledgerMark) BuildLedgerMark(root, body);
            }
            Transform handR = root.Find("HandR");
            if (handR != null && a.tool != NpcTool.None) BuildTool(handR, a.tool);

            // ── 아이 전용: 루트 스케일 축소 + 뜰채(다른 도구를 들었으면 뜰채는 없다) ──
            if (a.isChild)
            {
                root.localScale = Vector3.one * 0.75f;
            }
            if (a.isChild && a.tool == NpcTool.None)
            {
                Material netHandleMat = MakeMaterial(new Color(0.6f, 0.4f, 0.2f), SurfaceKind.Leather);
                Material netRingMat = MakeMaterial(new Color(0.95f, 0.92f, 0.88f), SurfaceKind.Metal);
                GameObject handle = MakePart(PrimitiveType.Cylinder, "NetHandle", root,
                    new Vector3(0.29f, 0.74f, 0f), new Vector3(0.04f, 0.40f, 0.04f), netHandleMat);
                handle.transform.localRotation = Quaternion.identity;
                GameObject ring = MakePart(PrimitiveType.Cylinder, "NetRing", root,
                    new Vector3(0.29f, 1.14f, 0f), new Vector3(0.20f, 0.02f, 0.20f), netRingMat);
                ring.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            }

            // ── 몸통 캡슐 콜라이더 (루트, 트리거) — 파츠 콜라이더는 전부 제거 완료 상태 ──
            CapsuleCollider capsule = root.gameObject.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = root.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.75f, 0f);
            capsule.radius = 0.28f;
            capsule.height = 1.5f;
            capsule.isTrigger = true;
            partMats.Clear();   // 다음 NPC가 이 NPC의 머티리얼을 물려받지 않게(파괴는 CleanupMaterials가 한다)

        }

        /// <summary>
        /// Build가 생성한 인스턴스 머티리얼 정리 — NPC 컴포넌트 OnDestroy에서 호출.
        /// (PlayerVisualBuilder.OnDestroy 패턴의 NPC판: NPC 파괴 시점엔 sharedMaterial
        /// 사용자가 함께 사라지므로 고유 sharedMaterial을 전부 Destroy해도 안전.)
        /// </summary>
        public static void CleanupMaterials(Transform root)
        {
            if (root == null) return;
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            var seen = new HashSet<Material>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                // **글자(TextMesh)의 머티리얼은 건너뛴다.** 그건 Build가 만든 인스턴스가 아니라 폰트 에셋의
                // 머티리얼이다 — 지우면 "Destroying assets is not permitted" 에러가 나고(씬 재로드마다),
                // 에디터에선 폰트 자체를 망가뜨린다. 머리 위 의뢰 표식(VillagerNpc.QuestMark)이 여기 걸렸다.
                if (renderers[i].TryGetComponent(out TextMesh _)) continue;
                Material m = renderers[i].sharedMaterial;
                if (m != null && seen.Add(m)) Object.Destroy(m);
            }
        }

        // ── 옷차림 부품 ── (콜라이더 없음. 머티리얼은 한 NPC 안에서 색·재질 단위로 공유 — CleanupMaterials가 회수)

        /// <summary>
        /// 키는 (색, 재질)이다. 색만 키로 쓰면 먼저 캐시된 재질이 이긴다 — 안전모는 모자 천(<c>BuildHat</c> 첫 줄의
        /// Cloth)이 같은 색으로 먼저 들어가 있어 Leather를 요청해도 천 인스턴스를 돌려받았고, 광부 안전모가 무광 천으로 그려졌다.
        /// </summary>
        private static readonly Dictionary<(Color, SurfaceKind), Material> partMats = new Dictionary<(Color, SurfaceKind), Material>();

        private static Material PartMat(Color color, SurfaceKind kind)
        {
            if (partMats.TryGetValue((color, kind), out Material m) && m != null) return m;
            m = MakeMaterial(color, kind);
            partMats[(color, kind)] = m;
            return m;
        }

        private static Mesh Ball => ProcMeshLibrary.LowSphere(0.5f, 0.5f, 0.5f, 6, 10);

        private static GameObject Ball3(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            return MakeMeshPart(name, parent, Ball, pos, scale, mat);
        }

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float round = 0.02f)
        {
            return MakeBoxPart(name, parent, pos, size, Mathf.Min(round, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f), 1, mat);
        }

        /// <summary>
        /// 모자 7종. 좌표는 HeadPivot 공간(머리 구 반지름 ≈0.35, 정수리 ≈0.39, 얼굴 +Z). 옛 모자는 전부
        /// 같은 야구모자 모양이어서 밀짚모자·털모자·안전모라는 주석이 색 말고는 구분되지 않았다.
        /// </summary>
        private static void BuildHat(Transform head, NpcHatStyle style, Color color)
        {
            // 안전모만 단단한 껍데기(Leather)고 나머지는 천이다. 천을 먼저 만들어 두면 안전모 분기에선 렌더러에
            // 안 붙은 채 남는다 — CleanupMaterials는 렌더러를 훑어 회수하므로 그건 씬이 끝날 때까지 샌다.
            Material hat = PartMat(color, style == NpcHatStyle.Helmet ? SurfaceKind.Leather : SurfaceKind.Cloth);
            switch (style)
            {
                case NpcHatStyle.Straw:
                case NpcHatStyle.Wide:
                {
                    float brim = style == NpcHatStyle.Wide ? 1.22f : 1.08f;
                    MakePart(PrimitiveType.Cylinder, "HatBrim", head, new Vector3(0f, 0.25f, 0f), new Vector3(brim, 0.018f, brim), hat);
                    MakePart(PrimitiveType.Cylinder, "HatCrown", head, new Vector3(0f, 0.36f, -0.01f), new Vector3(0.68f, 0.11f, 0.68f), hat);
                    Color band = style == NpcHatStyle.Straw ? new Color(0.62f, 0.22f, 0.18f) : Color.Lerp(color, Color.black, 0.45f);
                    MakePart(PrimitiveType.Cylinder, "HatBand", head, new Vector3(0f, 0.285f, -0.01f), new Vector3(0.7f, 0.035f, 0.7f), PartMat(band, SurfaceKind.Cloth));
                    break;
                }
                case NpcHatStyle.Beanie:
                    Ball3("Beanie", head, new Vector3(0f, 0.2f, -0.03f), new Vector3(0.76f, 0.5f, 0.74f), hat);
                    MakePart(PrimitiveType.Cylinder, "BeanieFold", head, new Vector3(0f, 0.1f, -0.03f), new Vector3(0.76f, 0.05f, 0.74f),
                        PartMat(Color.Lerp(color, Color.white, 0.2f), SurfaceKind.Cloth));
                    Ball3("BeaniePom", head, new Vector3(0f, 0.47f, -0.05f), Vector3.one * 0.18f, PartMat(new Color(0.95f, 0.95f, 0.92f), SurfaceKind.Cloth));
                    break;
                case NpcHatStyle.Helmet:
                    Ball3("Helmet", head, new Vector3(0f, 0.19f, -0.02f), new Vector3(0.8f, 0.54f, 0.8f), hat);
                    MakePart(PrimitiveType.Cylinder, "HelmetBrim", head, new Vector3(0f, 0.12f, 0.02f), new Vector3(0.9f, 0.018f, 0.92f), hat);
                    Material lamp = MakeMaterial(new Color(1f, 0.92f, 0.6f), SurfaceKind.Wet);
                    SceneryMaterials.SetEmission(lamp, new Color(1.2f, 1.0f, 0.55f));
                    Ball3("HelmetLamp", head, new Vector3(0f, 0.25f, 0.37f), new Vector3(0.14f, 0.12f, 0.08f), lamp);
                    break;
                case NpcHatStyle.Headwrap:
                    Ball3("Headwrap", head, new Vector3(0f, 0.19f, -0.04f), new Vector3(0.8f, 0.56f, 0.78f), hat);
                    Box("HeadwrapTail", head, new Vector3(0.1f, -0.12f, -0.34f), new Vector3(0.16f, 0.36f, 0.05f), hat);
                    break;
                case NpcHatStyle.Bandana:
                    MakePart(PrimitiveType.Cylinder, "Bandana", head, new Vector3(0f, 0.14f, -0.01f), new Vector3(0.74f, 0.06f, 0.73f), hat);
                    Ball3("BandanaKnot", head, new Vector3(0f, 0.13f, -0.37f), new Vector3(0.12f, 0.1f, 0.08f), hat);
                    break;
                case NpcHatStyle.Hood:
                    Ball3("Hood", head, new Vector3(0f, 0.07f, -0.1f), new Vector3(0.84f, 0.84f, 0.8f), hat);
                    break;
                default: // Cap — 옛 모양 그대로(노드명 Cap/CapBrim 유지)
                    MakePart(PrimitiveType.Sphere, "Cap", head, new Vector3(0f, 0.24f, -0.02f), PlayerVisualBuilder.CapSize, hat);
                    MakePart(PrimitiveType.Sphere, "CapBrim", head, new Vector3(0f, 0.16f, 0.29f), PlayerVisualBuilder.CapBrimSize, hat);
                    break;
            }
        }

        private static void BuildGlasses(Transform head)
        {
            Material frame = PartMat(new Color(0.16f, 0.13f, 0.12f), SurfaceKind.Leather);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("GlassTop", head, new Vector3(side * 0.12f, 0.07f, 0.365f), new Vector3(0.2f, 0.025f, 0.02f), frame, 0.005f);
                Box("GlassBottom", head, new Vector3(side * 0.12f, -0.125f, 0.365f), new Vector3(0.2f, 0.02f, 0.02f), frame, 0.005f);
                Box("GlassSide", head, new Vector3(side * 0.22f, -0.027f, 0.35f), new Vector3(0.02f, 0.2f, 0.02f), frame, 0.005f);
            }
            Box("GlassBridge", head, new Vector3(0f, 0.0f, 0.37f), new Vector3(0.06f, 0.02f, 0.02f), frame, 0.005f);
        }

        /// <summary>겉옷 — 좌표는 Body 공간(몸통 0.46×0.46×0.36, 중심 = 루트 y 0.77). 셔츠 앞판(z≈0.2)은 가리지 않는다.</summary>
        private static void BuildWear(Transform body, NpcWear wear, Color color)
        {
            Material m = PartMat(color, SurfaceKind.Cloth);
            // 짙은 덧감(주머니·띠·단추선)은 앞치마·코트만 쓴다 — 조끼에서 미리 만들면 렌더러 없이 남아 샌다(BuildHat 주석)
            Material Dark() => PartMat(Color.Lerp(color, Color.black, 0.35f), SurfaceKind.Cloth);
            switch (wear)
            {
                case NpcWear.Vest:
                    Box("VestL", body, new Vector3(-0.155f, 0.01f, 0.185f), new Vector3(0.15f, 0.42f, 0.035f), m);
                    Box("VestR", body, new Vector3(0.155f, 0.01f, 0.185f), new Vector3(0.15f, 0.42f, 0.035f), m);
                    Box("VestBack", body, new Vector3(0f, 0.01f, -0.19f), new Vector3(0.46f, 0.44f, 0.035f), m);
                    Box("VestSideL", body, new Vector3(-0.24f, 0.0f, 0f), new Vector3(0.03f, 0.4f, 0.34f), m);
                    Box("VestSideR", body, new Vector3(0.24f, 0.0f, 0f), new Vector3(0.03f, 0.4f, 0.34f), m);
                    break;
                case NpcWear.Apron:
                    Box("Apron", body, new Vector3(0f, -0.14f, 0.205f), new Vector3(0.36f, 0.56f, 0.025f), m);
                    Box("ApronPocket", body, new Vector3(0f, -0.24f, 0.222f), new Vector3(0.2f, 0.1f, 0.012f), Dark());
                    Box("ApronTie", body, new Vector3(0f, -0.06f, 0f), new Vector3(0.48f, 0.04f, 0.38f), Dark());
                    break;
                case NpcWear.Coat:
                case NpcWear.Robe:
                {
                    // 허리 아래 자락 — 코트는 무릎, 로브는 발목까지. 다리가 그 안에서 걷는다
                    float len = wear == NpcWear.Robe ? 0.5f : 0.32f;
                    Box("CoatSkirt", body, new Vector3(0f, -0.23f - len * 0.5f, 0f), new Vector3(0.5f, len, 0.4f), m, 0.05f);
                    Box("CoatCollar", body, new Vector3(0f, 0.22f, 0f), new Vector3(0.36f, 0.06f, 0.3f), m);
                    Box("CoatPlacket", body, new Vector3(0f, -0.2f, 0.2f), new Vector3(0.04f, len + 0.1f, 0.012f), Dark(), 0.005f);
                    Box("CoatBelt", body, new Vector3(0f, -0.21f, 0f), new Vector3(0.49f, 0.05f, 0.39f), Dark());
                    break;
                }
            }
        }

        private static void BuildScarf(Transform body, Color color)
        {
            Material m = PartMat(color, SurfaceKind.Cloth);
            MakePart(PrimitiveType.Cylinder, "Scarf", body, new Vector3(0f, 0.22f, 0.01f), new Vector3(0.36f, 0.05f, 0.32f), m);
            Box("ScarfTail", body, new Vector3(-0.09f, 0.08f, 0.2f), new Vector3(0.08f, 0.24f, 0.025f), m);
        }

        private static void BuildBag(Transform body, NpcBag bag)
        {
            Material leather = PartMat(new Color(0.46f, 0.32f, 0.20f), SurfaceKind.Leather);
            Material strap = PartMat(new Color(0.30f, 0.22f, 0.15f), SurfaceKind.Leather);
            if (bag == NpcBag.Satchel)
            {
                Box("Satchel", body, new Vector3(0.27f, -0.2f, 0.04f), new Vector3(0.09f, 0.18f, 0.22f), leather);
                GameObject s = Box("SatchelStrap", body, new Vector3(0.02f, 0.02f, 0.195f), new Vector3(0.05f, 0.56f, 0.015f), strap, 0.005f);
                s.transform.localRotation = Quaternion.Euler(0f, 0f, 38f);
            }
            else
            {
                Box("Backpack", body, new Vector3(0f, 0.02f, -0.28f), new Vector3(0.36f, 0.4f, 0.18f), leather, 0.05f);
                Box("BackpackFlap", body, new Vector3(0f, 0.17f, -0.36f), new Vector3(0.34f, 0.12f, 0.04f), strap);
                Box("BackpackRoll", body, new Vector3(0f, 0.25f, -0.28f), new Vector3(0.38f, 0.08f, 0.1f), PartMat(new Color(0.40f, 0.46f, 0.30f), SurfaceKind.Cloth));
                for (int side = -1; side <= 1; side += 2)
                    Box("BackpackStrap", body, new Vector3(side * 0.12f, 0.03f, 0.195f), new Vector3(0.045f, 0.4f, 0.015f), strap, 0.005f);
            }
        }

        /// <summary>명부회 표식 — 왼팔 남색 완장(팔과 함께 흔들린다) + 가슴의 금 배지.</summary>
        private static void BuildLedgerMark(Transform root, Transform body)
        {
            Material navy = PartMat(new Color(0.18f, 0.22f, 0.30f), SurfaceKind.Cloth);
            Transform armL = root.Find("ArmL");
            if (armL != null)
            {
                // ArmL은 (0.135, 0.23, 0.135)로 눌린 캡슐이라 자식 좌표도 그 공간이다 — 팔 둘레보다 15% 굵게
                MakePart(PrimitiveType.Cylinder, "Armband", armL, new Vector3(0f, 0.3f, 0f), new Vector3(1.15f, 0.12f, 1.15f), navy);
            }
            Material gold = MakeMaterial(new Color(0.95f, 0.78f, 0.30f), SurfaceKind.Metal);
            Ball3("LedgerBadge", body, new Vector3(-0.13f, 0.1f, 0.21f), new Vector3(0.06f, 0.06f, 0.02f), gold);
        }

        /// <summary>오른손 도구 — HandR의 자식이라 팔 스윙을 그대로 따른다(좌표는 손 중심 기준).</summary>
        private static void BuildTool(Transform hand, NpcTool tool)
        {
            // 나무·쇠는 쓰는 도구에서만 만든다 — 책·두루마리·등불·바구니에서 미리 만들면 렌더러 없이 남아 샌다(BuildHat 주석).
            // PartMat이 캐시하므로 한 도구 안에서 여러 번 불러도 인스턴스는 하나다.
            Material Wood() => PartMat(new Color(0.46f, 0.32f, 0.18f), SurfaceKind.Leather);
            Material Iron() => PartMat(new Color(0.52f, 0.54f, 0.58f), SurfaceKind.Metal);
            switch (tool)
            {
                case NpcTool.Axe:
                    MakePart(PrimitiveType.Cylinder, "ToolHandle", hand, new Vector3(0f, 0.08f, 0.05f), new Vector3(0.035f, 0.24f, 0.035f), Wood());
                    Box("AxeHead", hand, new Vector3(0.06f, 0.3f, 0.05f), new Vector3(0.14f, 0.1f, 0.025f), Iron());
                    break;
                case NpcTool.Pickaxe:
                    MakePart(PrimitiveType.Cylinder, "ToolHandle", hand, new Vector3(0f, 0.08f, 0.05f), new Vector3(0.035f, 0.26f, 0.035f), Wood());
                    Box("PickHead", hand, new Vector3(0f, 0.33f, 0.05f), new Vector3(0.36f, 0.045f, 0.045f), Iron());
                    break;
                case NpcTool.Staff:
                    MakePart(PrimitiveType.Cylinder, "Staff", hand, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.035f, 0.52f, 0.035f), Wood());
                    break;
                case NpcTool.Lantern:
                {
                    Material dark = PartMat(new Color(0.18f, 0.16f, 0.14f), SurfaceKind.Metal);
                    Box("LanternCage", hand, new Vector3(0f, -0.16f, 0.05f), new Vector3(0.12f, 0.15f, 0.12f), dark, 0.01f);
                    Material glow = MakeMaterial(new Color(1f, 0.85f, 0.5f), SurfaceKind.Wet);
                    SceneryMaterials.SetEmission(glow, new Color(1.3f, 0.95f, 0.45f));
                    Ball3("LanternGlow", hand, new Vector3(0f, -0.16f, 0.05f), new Vector3(0.13f, 0.13f, 0.13f), glow);
                    break;
                }
                case NpcTool.Book:
                case NpcTool.Ledger:
                {
                    Color cover = tool == NpcTool.Ledger ? new Color(0.16f, 0.18f, 0.24f) : new Color(0.62f, 0.24f, 0.20f);
                    Box("BookCover", hand, new Vector3(0f, -0.02f, 0.08f), new Vector3(0.17f, 0.22f, tool == NpcTool.Ledger ? 0.07f : 0.045f), PartMat(cover, SurfaceKind.Leather), 0.01f);
                    Box("BookPages", hand, new Vector3(0.012f, -0.02f, 0.08f), new Vector3(0.155f, 0.2f, tool == NpcTool.Ledger ? 0.058f : 0.034f), PartMat(new Color(0.94f, 0.91f, 0.82f), SurfaceKind.Cloth), 0.005f);
                    break;
                }
                case NpcTool.Scroll:
                {
                    GameObject roll = MakePart(PrimitiveType.Cylinder, "Scroll", hand, new Vector3(0f, -0.04f, 0.07f), new Vector3(0.06f, 0.13f, 0.06f), PartMat(new Color(0.92f, 0.88f, 0.76f), SurfaceKind.Cloth));
                    roll.transform.localRotation = Quaternion.Euler(0f, 0f, 80f);
                    break;
                }
                case NpcTool.Brush:
                    MakePart(PrimitiveType.Cylinder, "BrushStem", hand, new Vector3(0f, 0.08f, 0.05f), new Vector3(0.022f, 0.12f, 0.022f), Wood());
                    Ball3("BrushTip", hand, new Vector3(0f, 0.22f, 0.05f), new Vector3(0.04f, 0.07f, 0.04f), PartMat(new Color(0.12f, 0.10f, 0.10f), SurfaceKind.Hair));
                    break;
                case NpcTool.Shears:
                {
                    Material iron = Iron();
                    GameObject a1 = Box("ShearA", hand, new Vector3(0f, 0.1f, 0.06f), new Vector3(0.025f, 0.24f, 0.012f), iron, 0.004f);
                    a1.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                    GameObject a2 = Box("ShearB", hand, new Vector3(0f, 0.1f, 0.07f), new Vector3(0.025f, 0.24f, 0.012f), iron, 0.004f);
                    a2.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                    break;
                }
                case NpcTool.Basket:
                {
                    Material wicker = PartMat(new Color(0.72f, 0.56f, 0.34f), SurfaceKind.Leather);
                    MakePart(PrimitiveType.Cylinder, "Basket", hand, new Vector3(0f, -0.2f, 0.06f), new Vector3(0.24f, 0.07f, 0.2f), wicker);
                    GameObject h = Box("BasketHandle", hand, new Vector3(0f, -0.07f, 0.06f), new Vector3(0.2f, 0.02f, 0.02f), wicker, 0.005f);
                    h.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Ball3("BasketFill", hand, new Vector3(0f, -0.13f, 0.06f), new Vector3(0.2f, 0.06f, 0.16f), PartMat(new Color(0.46f, 0.62f, 0.28f), SurfaceKind.Cloth));
                    break;
                }
            }
        }

        private static void BuildLeg(Transform root, string side, float x, Material bottomMat, Material shoesMat)
        {
            GameObject pivot = new GameObject($"Leg{side}Pivot");
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = new Vector3(x, 0.48f, 0f);

            MakeMeshPart($"Leg{side}", pivot.transform, UnitCapsule(0.78f),
                new Vector3(0f, -0.14f, 0f), new Vector3(0.20f, 0.20f, 0.20f), bottomMat);
            MakeBoxPart($"Boot{side}", pivot.transform, new Vector3(0f, -0.36f, 0.07f),
                new Vector3(0.21f, 0.15f, 0.30f), 0.052f, 2, shoesMat);
        }

        private static void BuildHair(Transform headPivot, int style, Material hairMat)
        {
            // 공통: 정수리 덮개
            MakeMeshPart("HairTop", headPivot, UnitSphere(8, 12),
                new Vector3(0f, 0.22f, -0.02f), new Vector3(0.62f, 0.34f, 0.60f), hairMat);

            switch (style)
            {
                case 1: // 중간머리 — 옆/뒤 볼륨 추가
                    Mesh tuft = UnitSphere(6, 8);
                    MakeMeshPart("HairSideL", headPivot, tuft,
                        new Vector3(-0.2f, 0.05f, -0.02f), new Vector3(0.12f, 0.2f, 0.35f), hairMat);
                    MakeMeshPart("HairSideR", headPivot, tuft,
                        new Vector3(0.2f, 0.05f, -0.02f), new Vector3(0.12f, 0.2f, 0.35f), hairMat);
                    MakeMeshPart("HairBack", headPivot, tuft,
                        new Vector3(0f, 0.08f, -0.15f), new Vector3(0.45f, 0.28f, 0.2f), hairMat);
                    break;
                case 2: // 올림머리 — 뒤통수 번(bun)
                    MakeMeshPart("HairBun", headPivot, UnitSphere(7, 10),
                        new Vector3(0f, 0.15f, -0.22f), new Vector3(0.22f, 0.22f, 0.22f), hairMat);
                    break;
                    // case 0: 짧은머리 — HairTop만
            }
        }

        // ── 프로시저럴 메시 헬퍼 (PlayerVisualBuilder와 같은 규약) ──
        //
        // 내장 프리미티브와 같은 단위 크기 메시를 쓰므로 기존 localPosition/localScale을
        // 그대로 둔 채 메시만 갈아끼운다. 노드 이름은 NpcWalkAnimator가 문자열로 찾으므로 불변.

        private static GameObject MakeMeshPart(string name, Transform parent, Mesh mesh,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = ProcMeshLibrary.CreateNode(name, parent, mesh, mat, localPos);
            go.transform.localScale = localScale;
            return go;
        }

        /// <summary>크기를 메시에 구운 둥근 상자 — 스케일을 걸지 않는다(모서리 반경 왜곡 방지).</summary>
        private static GameObject MakeBoxPart(string name, Transform parent, Vector3 localPos,
            Vector3 size, float radius, int subdiv, Material mat)
        {
            Mesh mesh = ProcMeshLibrary.RoundedBox(size, radius, subdiv);
            return ProcMeshLibrary.CreateNode(name, parent, mesh, mat, localPos);
        }

        private static Mesh UnitSphere(int rings, int segments)
        {
            return ProcMeshLibrary.LowSphere(0.5f, 0.5f, 0.5f, rings, segments);
        }

        private static Mesh UnitCapsule(float taper)
        {
            float rTop = 0.5f;
            float rBottom = 0.5f * taper;
            return ProcMeshLibrary.TaperedCapsule(rTop, rBottom, 2f - rTop - rBottom, 8, 10);
        }

        private static Mesh UnitDisc(int segments)
        {
            return ProcMeshLibrary.Disc(0.5f, 0.5f, 0.4f, segments);
        }

        private static GameObject MakePart(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().material = mat;
            Collider col = go.GetComponent<Collider>();
            // Destroy는 프레임 끝까지 미뤄진다 — 그동안 non-trigger 콜라이더가 살아 있으면 런타임 스폰 프레임의
            // 물리 질의(스폰 안전 좌표·이동 차단)에 걸린다. 먼저 끈다(SubAreaWorldBuilder.NoCollider·SubAreaGateBuilder.Part와 같게).
            if (col != null) { col.enabled = false; Object.Destroy(col); }
            return go;
        }

        private static bool shaderDiagLogged;

        /// <summary>
        /// PlayerVisualBuilder.MakeMaterial 방식 복제 — Standard→URP→Unlit 폴백 + 부위별 PBR 재질.
        /// 재질을 안 나누면 NPC가 플레이어와 나란히 섰을 때 혼자 무광 점토로 보인다.
        /// </summary>
        private static Material MakeMaterial(Color color, SurfaceKind kind)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                if (!shaderDiagLogged)
                {
                    Debug.LogError("[NpcVisualBuilder] fallback shader를 찾을 수 없습니다 — NPC가 검은색/마젠타로 렌더링됩니다.");
                    shaderDiagLogged = true;
                }
                shader = Shader.Find("Hidden/InternalErrorShader");
            }
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            CharacterPalette.ApplySurface(mat, kind);
            return mat;
        }
    }
}
