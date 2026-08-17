using InsectGame.NPC;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 연출 저작. <see cref="CutsceneLibrary"/>와 <b>의도적으로 같은 모양</b>이다 —
    /// 저쪽은 카메라를, 여기는 배우를 움직인다. 에셋은 쓰지 않는다(이 게임엔 애니메이션 클립이 없다).
    ///
    /// 좌표는 전부 <b>플레이어 기준 상대</b>다. z 양수가 플레이어 앞, x 양수가 오른쪽.
    /// 카메라가 고정 yaw라 월드축 오프셋이 곧 화면 방향이다(컷신 오프셋 규약과 같음).
    ///
    /// <b>상수 선언과 <c>case</c>를 반드시 함께 넣는다.</b> 하나만 있으면 런타임에 LogWarning만 찍고
    /// 연출이 조용히 안 나온다 — 화면상 티가 안 나서 배포까지 살아남는다.
    /// <c>story_lint.py</c>가 둘을 대조한다.
    /// </summary>
    public static class StoryStageLibrary
    {
        /// <summary>1막 개막 — 마을 어르신이 플레이어를 알아보고 부른다.</summary>
        public const string Ch1ElderGreet = "st_ch1_elder_greet";
        /// <summary>1막 — 라온이 뛰어 들어온다(대사 앞).</summary>
        public const string Ch1RivalEnter = "st_ch1_rival_enter";
        /// <summary>1막 — 라온이 손을 흔들고 달려 나간다(대사 뒤).</summary>
        public const string Ch1RivalExit = "st_ch1_rival_exit";

        public static bool TryGet(string stageId, out StoryStageStep[] steps)
        {
            switch (stageId)
            {
                case Ch1ElderGreet: steps = BuildCh1ElderGreet(); return true;
                case Ch1RivalEnter: steps = BuildCh1RivalEnter(); return true;
                case Ch1RivalExit: steps = BuildCh1RivalExit(); return true;
                default: steps = null; return false;
            }
        }

        /// <summary>
        /// 어르신은 <b>이미 옆에 있다</b> — 플레이어가 찾아왔거나 조우 접근이 데려왔다.
        /// 그래서 걷게 하지 않는다. 알아보고, 부르고, 한 박자 쉰다. 그게 전부다.
        /// 여기서 어르신을 또 걸어오게 하면 방금 다가온 것을 두 번 하는 꼴이 된다.
        /// </summary>
        private static StoryStageStep[] BuildCh1ElderGreet()
        {
            return new[]
            {
                StoryStageStep.Face("village_elder", 0.3f),
                StoryStageStep.Play("village_elder", NpcGesture.Wave),
                StoryStageStep.Pause(0.35f),   // 손짓이 끝나고 말이 시작되기까지의 사이
            };
        }

        /// <summary>
        /// 라온의 등장. 이 비트의 트리거는 <c>CaptureInsect</c>라 <b>위치와 무관하게</b> 터진다 —
        /// 그냥 두면 라온이 지도 반대편에 있는 채로 대사만 뜬다. 그래서 먼저 무대 밖(플레이어
        /// 뒤쪽 9m)으로 옮겨 세우고, 거기서 달려 들어온다.
        ///
        /// 뒤에서 오는 이유는 "언제 왔지" 하는 인상을 주기 위해서다. 앞에서 오면 카메라에
        /// 처음부터 잡혀 등장이 아니라 이동이 된다.
        /// </summary>
        private static StoryStageStep[] BuildCh1RivalEnter()
        {
            return new[]
            {
                StoryStageStep.Warp("catcher_rival", new Vector3(-2.2f, 0f, -9f)),
                StoryStageStep.MoveTo("catcher_rival", new Vector3(-1.6f, 0f, 1.4f), 1.1f),
                StoryStageStep.Face("catcher_rival", 0.2f),
                StoryStageStep.Play("catcher_rival", NpcGesture.NetSwing),   // 뜰채를 한 번 휘두르며 인사
            };
        }

        /// <summary>라온의 퇴장 — 손을 흔들고 제 자리로 달려 돌아간다.</summary>
        private static StoryStageStep[] BuildCh1RivalExit()
        {
            return new[]
            {
                StoryStageStep.Play("catcher_rival", NpcGesture.Wave),
                StoryStageStep.GoHome("catcher_rival"),
            };
        }
    }
}
