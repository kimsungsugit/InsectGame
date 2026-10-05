using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 「챔피언의 꿈」의 <b>저작 데이터</b> — 챔피언 팀, 적, 꾸며진 섬. 순수 데이터와 순수 조립만 둔다.
    ///
    /// 전부 코드 상수다(Story.json이 아니라). 꿈은 이야기 비트가 아니라 한 번의 연출이고, 종·물건 ID가
    /// 바뀌면 <c>DreamPrologueDataTests</c>가 바로 잡는다 — ID 오타는 "그 곤충만 조용히 안 나오는" 결함이라
    /// 눈으로는 안 보인다.
    /// </summary>
    public static class DreamPrologueData
    {
        // ── 챔피언 ──
        /// <summary>에이스 — 챔피언전에서 직접 싸우는 곤충.</summary>
        public const string AceInsectId = "beetle_hercules";
        public const int AceLevel = 80;
        public const int AceIv = 15;

        // ── 상대 ──
        /// <summary>도전자. 이름 있는 전설종이라 "엄청 강한 상대를 이긴다"는 느낌이 선다.</summary>
        public const string ChallengerInsectId = "dragonfly_ancient";
        public const int ChallengerLevel = 72;

        // ── 도입 영상 ──
        /// <summary>
        /// 경기장 입장 영상의 길이(초). <c>Tools/Video/dream_arena.py</c>의 <c>T_TOTAL</c>과 같아야 한다 —
        /// 테스트가 mp4·wav 헤더로 맞춰 본다(디코더가 필요 없다).
        /// </summary>
        public const float IntroSeconds = 8f;

        /// <summary>내 곤충이 쓸 기술 수 — 전투 화면의 스킬 카드 수와 같다.</summary>
        public const int SkillCount = 4;

        /// <summary>섬을 돌아다니는 곤충들. 전설 위주에 영웅 둘, 색다른 개체 둘. 방목 상한(10)을 넘지 않는다.</summary>
        public static readonly (string insectId, int level, bool shiny)[] IslandInsects =
        {
            ("beetle_hercules", 80, false),
            ("butterfly_alexandras", 76, true),
            ("beetle_golden_stag", 78, false),
            ("dragonfly_ancient", 74, false),
            ("atlas_moth_giant", 72, false),
            ("gacha_celestial_beetle", 80, false),
            ("gacha_rainbow_butterfly", 70, true),
            ("gacha_diamond_beetle", 75, false),
            ("mantis_ghost", 64, false),
            ("firefly_blue", 60, false),
        };

        /// <summary>
        /// 섬 단계. 가장 큰 섬(22×22)이다 — 처음 보여 주는 섬이 "나중에 이만큼 커진다"의 맛보기다.
        /// </summary>
        public static readonly int IslandSizeLevel = GameConstants.Island.MaxSizeLevel;

        /// <summary>
        /// 꾸며진 섬 배치(id, x, z, 회전). 입구(남쪽 가운데)에서 분수 광장까지 가운데 두 칸이 열려 있고
        /// 서쪽은 마을(오두막·온실·모닥불), 동쪽은 연못·풍차·등대다. 겹침·경계는 테스트가 고정한다.
        /// </summary>
        public static readonly (string id, int x, int z, int rot)[] IslandObjects =
        {
            // 남쪽 입구
            ("f_sign", 2, -10, 0), ("f_mailbox", -4, -10, 0), ("f_lantern", -3, -8, 0), ("f_lantern", 2, -8, 0),
            ("t_flowerbed", -7, -9, 0), ("t_flowerbed", 4, -10, 0), ("t_bush", -6, -11, 0), ("t_bush", 7, -11, 0),
            // 서쪽 마을
            ("b_cabin", -10, -6, 0), ("f_campfire", -7, -4, 0), ("f_bench", -9, -2, 0), ("o_feeder", -4, -2, 0),
            ("o_basket", -5, -6, 0), ("f_hammock", -10, 1, 0), ("b_greenhouse", -10, 4, 0), ("t_blossom", -6, 5, 0),
            ("t_sapling", -8, 8, 0), ("t_oak", -10, 8, 0),
            // 중앙 분수 광장
            ("f_fountain", FountainX, FountainZ, 0), ("f_lantern", -3, 2, 0), ("f_lantern", 3, 2, 0),
            ("f_lantern", -3, 5, 0), ("f_lantern", 3, 5, 0), ("f_bench", -4, 0, 0), ("f_bench", 2, 0, 0),
            ("t_flowerbed", -5, 2, 0), ("t_flowerbed", 4, 3, 0), ("t_blossom", -3, 7, 0), ("t_blossom", 1, 7, 0),
            ("t_bush", -4, 9, 0), ("t_bush", 3, 9, 0),
            // 동쪽
            ("f_table", 3, -4, 0), ("t_pond", 6, -6, 0), ("t_oak", 8, -2, 0), ("o_honeypot", 4, -1, 0),
            ("o_water", 6, 0, 0), ("b_windmill", 6, 3, 0), ("b_lighthouse", 8, 8, 0), ("t_boulder", 9, 5, 0),
            ("t_flowerbed", 5, 8, 0), ("f_fence", -1, 9, 0), ("f_fence", 1, 9, 0),
        };

        /// <summary>분수 — 프롤로그의 마지막 목표 지점. 3×3 차지 칸의 왼쪽 아래 모서리.</summary>
        public const int FountainX = -1;
        public const int FountainZ = 2;
        public const int FountainSize = 3;

        /// <summary>쾌적도 표기용 값 — 방문 화면이 아니라 데이터 일관성을 위해 채운다.</summary>
        private const int ShowcaseComfort = 100;

        /// <summary>꾸며진 섬의 공개용 스냅샷. <paramref name="ownerName"/>은 챔피언(플레이어)의 이름이다.</summary>
        public static IslandSnapshot BuildIslandSnapshot(string ownerName)
        {
            var snapshot = new IslandSnapshot
            {
                ownerName = string.IsNullOrEmpty(ownerName) ? "챔피언" : ownerName,
                sizeLevel = IslandSizeLevel,
                comfort = ShowcaseComfort,
            };
            for (int i = 0; i < IslandObjects.Length; i++)
            {
                var o = IslandObjects[i];
                snapshot.placed.Add(new IslandPlacedRecord { id = o.id, x = o.x, z = o.z, rot = o.rot });
            }
            for (int i = 0; i < IslandInsects.Length; i++)
            {
                var s = IslandInsects[i];
                snapshot.insects.Add(new IslandSnapshotInsect { insectId = s.insectId, level = s.level, shiny = s.shiny });
            }
            return snapshot;
        }

        /// <summary>분수 광장 중심의 월드 좌표 — 마지막 목표 마커가 선다.</summary>
        public static Vector3 FountainWorldPosition()
        {
            return IslandWorldBuilder.CellWorldCenter(FountainX, FountainZ, FountainSize, FountainSize);
        }

        /// <summary>
        /// 챔피언의 에이스 — 컬렉션에 들어가지 않는 <b>메모리 개체</b>. 개체값이 전부 최대라 같은 종의 누구보다 세다.
        /// instanceId는 컬렉션과 겹치지 않는 접두를 쓴다(전투 UI가 이 값으로 개체를 조회한다).
        /// </summary>
        public static PlayerInsectData BuildChampionInsect(InsectData data)
        {
            var pid = new PlayerInsectData
            {
                instanceId = "dream_champion_ace",
                insectId = data != null ? data.insectId : AceInsectId,
                level = AceLevel,
                ivHp = AceIv,
                ivAtk = AceIv,
                ivDef = AceIv,
                sizeRoll = 90,
            };
            pid.currentHp = pid.GetTotalHp(data != null ? data.baseHp : 100);   // 풀 HP로 시작 — -1(미초기화) 센티넬로 두지 않는다
            return pid;
        }

        /// <summary>
        /// 챔피언이 쓸 기술 넷. 위력이 높은 피해기를 앞에서 셋(필살기가 맨 앞) 고르고 마지막 칸에는 능력 상승기를 둔다 —
        /// "때리고, 올리고, 필살기"를 한 번씩 눌러 보게 한다. 상승기가 없으면 피해기로 채운다.
        /// </summary>
        public static InsectSkill[] PickChampionSkills(InsectData data, int level)
        {
            var damage = new List<InsectSkill>();
            InsectSkill support = null;
            if (data != null && data.learnset != null)
            {
                foreach (InsectLearnableSkill learnable in data.learnset)
                {
                    InsectSkill skill = learnable != null ? learnable.skill : null;
                    if (skill == null || learnable.learnLevel > level) continue;
                    if (skill.effectType == SkillEffectType.Damage) damage.Add(skill);
                    else if (support == null && skill.effectType == SkillEffectType.BuffAttack) support = skill;
                }
            }
            damage.Sort((a, b) => b.power.CompareTo(a.power));

            var picked = new List<InsectSkill>();
            int damageSlots = support != null ? SkillCount - 1 : SkillCount;
            for (int i = 0; i < damage.Count && picked.Count < damageSlots; i++) picked.Add(damage[i]);
            if (support != null) picked.Add(support);
            return picked.ToArray();
        }
    }
}
