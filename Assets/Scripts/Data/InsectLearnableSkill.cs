using System;

namespace InsectGame.Data
{
    [Serializable]
    public class InsectLearnableSkill
    {
        public string skillId;
        public int learnLevel;
        public InsectSkill skill;
    }
}
