namespace InsectGame.Core
{
    /// <summary>섬 첫 방문 안내의 단계. 저장은 정수(<see cref="IslandSave.guideStep"/>)라 순서를 바꾸지 말 것 — 뒤에만 붙인다.</summary>
    public enum IslandGuideStep
    {
        OpenEdit = 0,
        PlaceFirst = 1,
        ReleaseInsect = 2,
        Harvest = 3,
        OpenShop = 4,
        Finish = 5,
        Done = 6,
    }

    /// <summary>안내가 다음 단계로 넘어갈지 정하는 데 보는 사실들. 전부 실제 행동의 결과다.</summary>
    public struct IslandGuideFacts
    {
        public bool editOpened;
        public bool shopOpened;
        public bool finishAcknowledged;
        public int placedCount;
        public int releasedCount;
        public int harvestCount;
        public int ownedInsectCount;
    }

    /// <summary>
    /// 섬 안내의 <b>순수</b> 단계 판정. "다음" 버튼이 아니라 <b>행동</b>으로 넘어간다 —
    /// 벤치를 놓으면 다음 단계, 곤충을 풀면 그다음. 이미 한 행동의 단계는 건너뛴다
    /// (다른 기기에서 섬을 꾸민 뒤 클라우드로 받아 온 세이브가 "벤치를 놓아 보세요"에 멈추지 않게).
    ///
    /// 기존 <c>GuidedTutorialController</c>를 안 쓰는 이유: 그쪽은 Story 퀘스트의 <c>QuestActivated</c>에
    /// 묶여 있는데 섬 퀘스트는 Side라 그 이벤트가 울리지 않는다.
    /// </summary>
    public static class IslandGuideSteps
    {
        /// <summary>지금 사실들로 갈 수 있는 데까지 나아간 단계.</summary>
        public static IslandGuideStep Advance(IslandGuideStep step, IslandGuideFacts facts)
        {
            // 한 번에 여러 단계를 넘을 수 있다(이미 다 해 둔 세이브). 단계 수가 상한이라 무한 루프가 없다.
            for (int guard = 0; guard <= (int)IslandGuideStep.Done; guard++)
            {
                if (step == IslandGuideStep.Done || !IsSatisfied(step, facts)) return step;
                step = (IslandGuideStep)((int)step + 1);
            }
            return step;
        }

        public static bool IsSatisfied(IslandGuideStep step, IslandGuideFacts facts)
        {
            switch (step)
            {
                // 이미 무언가 놓았다면 꾸미기 화면은 써 본 것이다.
                case IslandGuideStep.OpenEdit: return facts.editOpened || facts.placedCount > 0;
                case IslandGuideStep.PlaceFirst: return facts.placedCount > 0;
                // 풀어놓을 곤충이 한 마리도 없으면 막지 않고 넘긴다(마스터 계정·빈 세이브).
                case IslandGuideStep.ReleaseInsect: return facts.releasedCount > 0 || facts.ownedInsectCount <= 0;
                case IslandGuideStep.Harvest: return facts.harvestCount > 0;
                case IslandGuideStep.OpenShop: return facts.shopOpened;
                case IslandGuideStep.Finish: return facts.finishAcknowledged;
                default: return true;
            }
        }

        public static string Title(IslandGuideStep step)
        {
            switch (step)
            {
                case IslandGuideStep.OpenEdit: return "나의 섬에 오신 걸 환영합니다";
                case IslandGuideStep.PlaceFirst: return "물건 놓기";
                case IslandGuideStep.ReleaseInsect: return "곤충 풀어놓기";
                case IslandGuideStep.Harvest: return "수확하기";
                case IslandGuideStep.OpenShop: return "섬 상점";
                case IslandGuideStep.Finish: return "이제 마음대로 꾸며 보세요";
                default: return string.Empty;
            }
        }

        public static string Body(IslandGuideStep step)
        {
            switch (step)
            {
                case IslandGuideStep.OpenEdit:
                    return "보관함에 벤치와 화분을 넣어 두었어요. 아래 [꾸미기]를 눌러 보세요.";
                case IslandGuideStep.PlaceFirst:
                    return "아래 보관함에서 물건을 고르고, 섬 위의 칸을 누른 뒤 [놓기]를 누르세요.";
                case IslandGuideStep.ReleaseInsect:
                    return "[곤충]을 눌러 보유 곤충을 섬에 풀어놓으세요. 풀어도 전투에는 그대로 쓸 수 있어요.";
                case IslandGuideStep.Harvest:
                    return "곤충이 머무는 동안 캔디와 코인이 쌓여요. 첫 선물을 넣어 두었으니 [수확]을 눌러 받으세요.";
                case IslandGuideStep.OpenShop:
                    return "[상점]에서 건물·가구·지형지물·도구를 사고 섬과 곤충 자리를 넓힐 수 있어요.";
                case IslandGuideStep.Finish:
                    return "[방문]으로 다른 사람의 섬을 구경하고, 궁금할 땐 [도움말]을 다시 열어 보세요.";
                default: return string.Empty;
            }
        }

        /// <summary>도움말 창에 한꺼번에 보여 주는 요약 — 단계 문구와 같은 내용을 다시 쓴 것이 아니라 "무엇을 어디서"만 적는다.</summary>
        public static readonly (string title, string body)[] HelpTopics =
        {
            ("꾸미기", "보관함의 물건을 섬 칸에 놓습니다. 놓인 물건을 누르면 옮기기·돌리기·넣기를 할 수 있어요. 화면을 끌면 섬을 둘러봅니다."),
            ("곤충", "보유 곤충을 섬에 풀어놓습니다. 풀어놓은 곤충은 전투·훈련에 그대로 쓸 수 있고, 오래 머물수록 친밀도(하트)가 올라 생산이 늘어요."),
            ("수확", "쌓인 캔디와 코인을 받습니다. 가득 차면 더 쌓이지 않으니 가끔 들러 주세요. 창고와 수확 바구니가 쌓이는 시간을 늘립니다."),
            ("쾌적도", "놓인 물건마다 쾌적도가 있고, 쾌적도가 높을수록 생산이 늘어요. 같은 물건을 여러 개 놓는 것보다 종류를 늘리는 쪽이 잘 오릅니다."),
            ("상점", "건물·가구·지형지물·도구를 삽니다. [확장] 탭에서 섬 크기와 곤충 자리를 넓혀요. 넣어 둔 물건은 팔 수 없고 보관함에 남습니다."),
            ("방문", "친구 목록이나 섬 코드로 다른 사람의 섬을 구경합니다. 내 섬 코드는 [방문] 창에 있고, 공개를 끄면 아무도 들어올 수 없어요."),
        };
    }
}
