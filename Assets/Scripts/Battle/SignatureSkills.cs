using InsectGame.Data;

namespace InsectGame.Battle
{
    /// <summary>
    /// 전용기(그 종만 쓰는 필살기) 판별 — 순수. 전용기 연출(visual-dev)·「전용기!」 문구(ui-dev)·레이드 보스의 기술 고르기가
    /// <b>같은 기준</b>을 쓴다.
    ///
    /// 기준은 learnset의 표시다: 기술의 <c>isSignatureSkill</c>이 켜져 있고 <b>그 곤충의 learnset에</b> 그 기술이 있어야 한다.
    /// 부트스트랩이 영웅 Lv15·전설 Lv20 칸에 <c>{insectId}_signature</c>를 넣는다(<c>PlaySceneBootstrap.BuildLevelLearnset</c>).
    /// 훈련·디스크로는 전용기를 못 배운다(<c>TrainingManager</c>) — 그래서 "그 곤충의 learnset에 있다"가 곧 "그 곤충의 것"이다.
    /// 기술은 ID로도 맞춘다 — 장착 기술(<c>GetEquippedSkills</c>)이 learnset과 다른 인스턴스로 올 수 있다.
    /// </summary>
    public static class SignatureSkills
    {
        /// <summary>
        /// <paramref name="skill"/>이 <paramref name="owner"/>의 전용기인가. 둘 중 하나라도 없으면 false.
        /// 레이드 보스는 <b>지금 모습</b>(<c>BossStats.CombatData</c>)을 넘긴다 — 빌린 모습의 전용기는 그 모습의 것이다.
        /// </summary>
        public static bool IsSignature(InsectData owner, InsectSkill skill)
        {
            if (owner == null || skill == null || !skill.isSignatureSkill || owner.learnset == null) return false;
            foreach (InsectLearnableSkill learnable in owner.learnset)
                if (IsSignatureEntry(learnable) && SameSkill(learnable.skill, skill)) return true;
            return false;
        }

        /// <summary>learnset 한 칸이 전용기 칸인가(레벨 무관).</summary>
        public static bool IsSignatureEntry(InsectLearnableSkill learnable)
        {
            return learnable != null && learnable.skill != null && learnable.skill.isSignatureSkill;
        }

        /// <summary>learnset 한 칸이 <paramref name="level"/>에 이미 열린 전용기인가 — 레이드 보스의 기술 고르기가 쓴다.</summary>
        public static bool IsUnlocked(InsectLearnableSkill learnable, int level)
        {
            return IsSignatureEntry(learnable) && learnable.learnLevel <= level;
        }

        /// <summary><paramref name="owner"/>의 첫 전용기(레벨 무관). 없으면 null(일반~희귀는 전용기가 없다).</summary>
        public static InsectSkill FindSignature(InsectData owner)
        {
            if (owner == null || owner.learnset == null) return null;
            foreach (InsectLearnableSkill learnable in owner.learnset)
                if (IsSignatureEntry(learnable)) return learnable.skill;
            return null;
        }

        private static bool SameSkill(InsectSkill a, InsectSkill b)
        {
            if (ReferenceEquals(a, b)) return true;
            return a != null && b != null && !string.IsNullOrEmpty(a.skillId) && a.skillId == b.skillId;
        }
    }
}
