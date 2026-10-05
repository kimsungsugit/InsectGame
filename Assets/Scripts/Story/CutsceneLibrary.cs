using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 프로시저럴 컷신 저작. 에셋 0개로 만든다 — 카메라 워크 + 기존 월드 + 조명 + 자막만으로
    /// 장면을 만든다. 실제 영상 파일은 <see cref="StoryVideoLibrary"/>가 따로 맡는다.
    ///
    /// 이야기의 큰 장면(이름 벽·울타리·그림자·결말)은 2026-10-05에 그림책 영상(대사 앞 <c>introVideoId</c>)으로 옮겼고
    /// 그 네 컷신(<c>cs_seal_discovery</c>·<c>cs_seal_opening</c>·<c>cs_nameless_confront</c>·<c>cs_final_seal</c>)은 지웠다.
    /// 남은 것은 오염 거점 정화(<see cref="BlightCleanse"/>)와 쓰지 않는 폴백(<see cref="StoryPrologue"/>)이다.
    ///
    /// 좌표는 전부 <b>플레이어 기준 상대</b>이므로 어느 리전·서브에리어에서 발화해도 동작한다.
    /// y가 큰 값은 위에서 내려다보는 각, z 음수는 플레이어 뒤쪽이다.
    ///
    /// 내용의 단일 출처는 <c>Docs/StoryBible.md</c>다 — 여기 대사를 고치면 그쪽도 함께 고친다.
    /// </summary>
    public static class CutsceneLibrary
    {
        /// <summary>
        /// 1막 개막 — 초원을 둘러본다. <b>지금은 쓰이지 않는다</b>: <c>ch1_intro</c>는 영상
        /// <c>vid_ch1_prologue</c>(<see cref="StoryVideoLibrary"/>)로 대체됐다. 상수·case·테스트는
        /// 영상 파일이 없는 환경의 폴백 후보로 남긴다.
        /// </summary>
        public const string StoryPrologue = "cs_story_prologue";
        /// <summary>
        /// 오염 거점이 무너지고 그 리전에 곤충이 돌아온다. <b>거점 전부가 같은 컷신을 쓴다</b> —
        /// 좌표가 전부 플레이어 기준 상대라 산에서도 유적에서도 그대로 맞고, 자막도 장소를
        /// 지목하지 않는다. 장소별 감상은 비트의 <c>lines[]</c>가 이미 말한다.
        /// </summary>
        public const string BlightCleanse = "cs_bl_cleanse";

        public static bool TryGet(string cutsceneId, out CutsceneShot[] shots)
        {
            switch (cutsceneId)
            {
                case StoryPrologue: shots = BuildStoryPrologue(); return true;
                case BlightCleanse: shots = BuildBlightCleanse(); return true;
                default: shots = null; return false;
            }
        }

        /// <summary>
        /// 1막 개막 — 어르신에게 이야기를 <b>듣고 첫 파트너를 받은 직후</b>.
        ///
        /// <b>대사는 앞에 온다.</b> 컷신은 <c>StoryBeatCompleted</c>로 재생되므로 대화 모달이
        /// 닫힌 뒤다(<c>CutsceneDirector</c> 클래스 주석이 근거). 그러니 여기서 하는 일은
        /// 이야기를 <b>여는</b> 것이 아니라 들은 이야기를 들고 필드로 <b>내보내는</b> 것이다.
        ///
        /// 예전 자막은 이 순서를 거꾸로 알고 쓰여 있었다 — 세 번째 컷이 어르신 대사 2번째 줄
        /// ("어제 있던 아이가 오늘은 보이질 않는단다")을 <b>거의 그대로 되풀이</b>했고, 마지막
        /// 컷은 "마을 어르신이 그 이야기를 알고 있다"로 <b>방금 만나고 나온 사람을 찾아가라</b>고
        /// 안내했다. 재생 시점을 확인하지 않고 쓰면 이렇게 어긋난다.
        ///
        /// 카메라 워크(초원 훑기 → 부감 → 복귀)는 그대로 둔다. 어긋난 것은 자막뿐이고,
        /// 그림은 "지금부터 나갈 곳"을 보여주는 데 그대로 맞는다.
        /// </summary>
        private static CutsceneShot[] BuildStoryPrologue()
        {
            return new[]
            {
                // 1. 플레이어 어깨 너머에서 시작 — 지금까지 서 있던 자리를 확인시킨다.
                new CutsceneShot(2.4f,
                    camFrom: new Vector3(0f, 2.2f, -3.4f),
                    camTo: new Vector3(-2.6f, 3.4f, -4.2f),
                    lookAt: new Vector3(0f, 1.2f, 1.2f),
                    subtitle: "잡는 법은 몸이 먼저 익혔다. 그리고 이제 혼자가 아니다."),

                // 2. 초원을 옆으로 훑는다. 자막 없이 그림만 — 넓이를 느끼게 한다.
                new CutsceneShot(2.2f,
                    camFrom: new Vector3(-2.6f, 3.4f, -4.2f),
                    camTo: new Vector3(3.2f, 4.0f, -3.0f),
                    lookAt: new Vector3(0f, 1.0f, 3.0f)),

                // 3. 높이 올라가 초원 전체를 담는다 — 사라지고 있는 것의 규모.
                new CutsceneShot(2.8f,
                    camFrom: new Vector3(3.2f, 4.0f, -3.0f),
                    camTo: new Vector3(0.5f, 8.5f, -6.5f),
                    lookAt: new Vector3(0f, 0.5f, 4.0f),
                    subtitle: "풀밭은 이렇게 넓은데, 움직이는 것이 눈에 잘 띄지 않는다."),

                // 4. 다시 내려와 플레이어 곁으로 — 다음 목표(필드로 나가기)로 넘어가는 다리.
                new CutsceneShot(2.6f,
                    camFrom: new Vector3(0.5f, 8.5f, -6.5f),
                    camTo: new Vector3(0f, 2.6f, -3.6f),
                    lookAt: new Vector3(0f, 1.4f, 1.0f),
                    subtitle: "이 아이와 함께라면, 그 이유를 찾을 수 있을지도 모른다."),
            };
        }

        /// <summary>
        /// 거점이 무너진 직후 — 걷어 간 손이 사라진 땅.
        ///
        /// <b>딤을 걷으며 나온다</b>(0.42 → 0). 오염 아크의 다른 장면이 아니라 이 장면 하나가
        /// 아크의 마침표라, 들어갈 때가 아니라 나올 때 밝아지는 것이 맞다(옛 결말 컷신 <c>cs_final_seal</c>도
        /// 같은 형태였다 — 지금 결말은 영상 <c>vid_fin_return</c>이 맡는다). 흔들림은 첫 컷에만 약하게 — 구조물이 주저앉는 순간을 대신한다.
        ///
        /// <b>마지막 컷은 플레이어 가까이서 끝낸다.</b> 멀리서 끝나면 컷신이 카메라를 놓는
        /// 순간 추적 카메라가 튀어 돌아온다.
        ///
        /// 총 9.4초 — <c>PlayerMovement.AutoUnfreezeTime</c>(20s)보다 충분히 짧다.
        /// <c>CutsceneTimelineTests</c>가 여유 4초를 강제한다.
        /// </summary>
        private static CutsceneShot[] BuildBlightCleanse()
        {
            return new[]
            {
                // 1. 낮은 자리에서 거점이 있던 쪽을 올려다본다. 흔들림 + 가장 짙은 딤.
                new CutsceneShot(2.3f,
                    camFrom: new Vector3(1.6f, 1.1f, -3.2f),
                    camTo: new Vector3(0.6f, 1.6f, -3.8f),
                    lookAt: new Vector3(0f, 2.4f, 2.6f),
                    subtitle: "그물이 무너졌다. 상자를 세던 손도 없다.",
                    shake: 0.35f,
                    dim: 0.42f),

                // 2. 옆으로 돌며 땅을 훑는다 — 자막 없이 그림만. 딤이 절반으로 걷힌다.
                new CutsceneShot(2.4f,
                    camFrom: new Vector3(0.6f, 1.6f, -3.8f),
                    camTo: new Vector3(-3.4f, 2.4f, -2.2f),
                    lookAt: new Vector3(0f, 0.8f, 2.0f),
                    dim: 0.22f),

                // 3. 올라가 리전 전체를 담는다. 돌아온 것이 눈에 들어오는 자리.
                new CutsceneShot(2.7f,
                    camFrom: new Vector3(-3.4f, 2.4f, -2.2f),
                    camTo: new Vector3(-0.8f, 7.6f, -6.0f),
                    lookAt: new Vector3(0f, 0.6f, 3.4f),
                    subtitle: "걷어 가는 손만 없으면, 땅은 스스로 채운다.",
                    dim: 0.08f),

                // 4. 플레이어 곁으로 내려온다. 딤 0 — 밝은 채로 조작이 돌아간다.
                new CutsceneShot(2.0f,
                    camFrom: new Vector3(-0.8f, 7.6f, -6.0f),
                    camTo: new Vector3(0f, 2.5f, -3.5f),
                    lookAt: new Vector3(0f, 1.3f, 1.0f),
                    subtitle: "장부에 없는 줄이 하나 늘었다 — 돌려보냈다는 줄이."),
            };
        }
    }
}
