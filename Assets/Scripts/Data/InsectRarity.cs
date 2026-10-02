namespace InsectGame.Data
{
    public enum InsectRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// 등급의 <b>화면 표기</b> 단일 출처. 도감이 자기 파일에 한글 표를 두고, 배틀팀·보유 곤충·포획 창은
    /// enum 이름을 그대로 찍어 한 게임 안에서 "희귀"와 "Rare"가 섞여 나왔다.
    /// </summary>
    public static class InsectRarityText
    {
        public static string Korean(this InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Uncommon: return "고급";
                case InsectRarity.Rare: return "희귀";
                case InsectRarity.Epic: return "영웅";
                case InsectRarity.Legendary: return "전설";
                default: return "일반";
            }
        }
    }
}
