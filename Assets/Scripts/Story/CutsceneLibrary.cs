using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 컷신 저작. 에셋 0개로 만든다 — 이 게임에는 영상도 컷신용 스틸도 없고,
    /// 카메라 워크 + 기존 월드 + 조명 + 자막만으로 장면을 만든다.
    ///
    /// 좌표는 전부 <b>플레이어 기준 상대</b>이므로 어느 리전·서브에리어에서 발화해도 동작한다.
    /// y가 큰 값은 위에서 내려다보는 각, z 음수는 플레이어 뒤쪽이다.
    ///
    /// 내용의 단일 출처는 <c>Docs/StoryBible.md</c>다 — 여기 대사를 고치면 그쪽도 함께 고친다.
    /// </summary>
    public static class CutsceneLibrary
    {
        /// <summary>1막 개막 — 마을 어르신에게 이야기를 듣기 직전, 초원을 둘러본다.</summary>
        public const string StoryPrologue = "cs_story_prologue";
        /// <summary>2막 개막 — 유적 지하에서 봉인이 열리는 장면.</summary>
        public const string SealOpening = "cs_seal_opening";
        /// <summary>최종장 — 무명과의 대치.</summary>
        public const string NamelessConfront = "cs_nameless_confront";
        /// <summary>1막 클라이맥스 — 유적 신전에서 봉인의 정체(기록)를 본 순간.</summary>
        public const string SealDiscovery = "cs_seal_discovery";
        /// <summary>최종장 마무리 — 마지막 빈칸이 메워지고 그것이 설 자리를 잃는다.</summary>
        public const string FinalSeal = "cs_final_seal";
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
                case SealOpening: shots = BuildSealOpening(); return true;
                case NamelessConfront: shots = BuildNamelessConfront(); return true;
                case SealDiscovery: shots = BuildSealDiscovery(); return true;
                case FinalSeal: shots = BuildFinalSeal(); return true;
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
        /// 「봉인이 열린 날」 — 기록을 되살린 그 행위가 안에 갇혀 있던 것을 가둔 순간.
        /// 플레이어의 선의가 사건을 일으켰다는 것이 2막의 골자이므로, 카메라는 제단이 아니라
        /// <b>플레이어를 먼저</b> 비춘다.
        /// </summary>
        private static CutsceneShot[] BuildSealOpening()
        {
            return new[]
            {
                // 1. 플레이어에게서 천천히 물러난다 — 방금 한 일의 크기를 보여주는 각.
                new CutsceneShot(2.6f,
                    camFrom: new Vector3(0f, 2.4f, -3.2f),
                    camTo: new Vector3(0f, 5.2f, -7.4f),
                    lookAt: new Vector3(0f, 1.2f, 0f),
                    subtitle: "기록이 제자리를 찾았다."),

                // 2. 정적. 자막 없이 그림만 — 다음 흔들림을 크게 만드는 건 이 침묵이다.
                new CutsceneShot(1.4f,
                    camFrom: new Vector3(0f, 5.2f, -7.4f),
                    camTo: new Vector3(0.6f, 5.0f, -7.0f),
                    lookAt: new Vector3(0f, 1.2f, 0f)),

                // 3. 균열. 카메라가 아래로 꺾이며 흔들린다.
                new CutsceneShot(2.2f,
                    camFrom: new Vector3(0.6f, 5.0f, -7.0f),
                    camTo: new Vector3(0.2f, 2.8f, -4.6f),
                    lookAt: new Vector3(0f, 0.2f, 2.4f),
                    subtitle: "그리고 바닥이 갈라졌다.",
                    shake: 0.42f),

                // 4. 갈라진 틈으로 시선을 내린다. 딤이 들어오며 빛이 줄어든다.
                new CutsceneShot(3.0f,
                    camFrom: new Vector3(0.2f, 2.8f, -4.6f),
                    camTo: new Vector3(0f, 1.6f, -2.2f),
                    lookAt: new Vector3(0f, -0.6f, 2.8f),
                    subtitle: "울타리는 무언가를 지키려고 세운 것이 아니었다.",
                    dim: 0.30f),

                // 5. 반전. 가장 어둡고 가장 가깝다.
                new CutsceneShot(3.4f,
                    camFrom: new Vector3(0f, 1.6f, -2.2f),
                    camTo: new Vector3(0f, 1.3f, -1.6f),
                    lookAt: new Vector3(0f, -0.8f, 2.6f),
                    subtitle: "가두려고 세운 것이었다 — 그리고 방금, 안에 있던 것이 갇혔다.",
                    shake: 0.22f,
                    dim: 0.52f),

                // 6. 복귀. 딤을 걷으며 원래 시점으로.
                new CutsceneShot(1.8f,
                    camFrom: new Vector3(0f, 1.3f, -1.6f),
                    camTo: new Vector3(0f, 3.0f, -4.0f),
                    lookAt: new Vector3(0f, 1.0f, 0f),
                    dim: 0.16f),
            };
        }

        /// <summary>
        /// 최종장 — 「무명」과의 대치. 이름이 없는 것 앞에서는 카메라도 초점을 잡지 못한다는
        /// 인상을 주려고 시선 지점을 조금씩 어긋나게 둔다.
        /// </summary>
        private static CutsceneShot[] BuildNamelessConfront()
        {
            return new[]
            {
                new CutsceneShot(2.4f,
                    camFrom: new Vector3(0f, 2.2f, -3.0f),
                    camTo: new Vector3(-1.4f, 2.6f, -3.8f),
                    lookAt: new Vector3(0f, 1.4f, 3.0f),
                    subtitle: "이름을 부를 수 없는 것이 거기 서 있었다.",
                    dim: 0.24f),

                // 시선이 미끄러진다 — lookAt을 좌우로 흔들어 초점이 안 잡히는 느낌을 만든다.
                new CutsceneShot(2.0f,
                    camFrom: new Vector3(-1.4f, 2.6f, -3.8f),
                    camTo: new Vector3(1.6f, 2.4f, -3.6f),
                    lookAt: new Vector3(-1.2f, 1.6f, 3.4f),
                    subtitle: "보고 있는데도, 무엇을 보고 있는지 알 수 없었다.",
                    dim: 0.34f),

                new CutsceneShot(2.6f,
                    camFrom: new Vector3(1.6f, 2.4f, -3.6f),
                    camTo: new Vector3(0f, 1.8f, -2.4f),
                    lookAt: new Vector3(0f, 1.5f, 3.2f),
                    subtitle: "빈칸은 사라지지 않는다. 다만 지금은, 이름을 부를 차례다.",
                    shake: 0.3f,
                    dim: 0.46f),

                new CutsceneShot(1.6f,
                    camFrom: new Vector3(0f, 1.8f, -2.4f),
                    camTo: new Vector3(0f, 3.0f, -4.0f),
                    lookAt: new Vector3(0f, 1.0f, 0f),
                    dim: 0.12f),
            };
        }

        /// <summary>
        /// 「벽은 목록이었다」 — 1막의 클라이맥스. 세라가 대사로 이미 설명을 마친 뒤에 재생되므로
        /// (<c>StoryBeatCompleted</c>) 여기서는 <b>설명하지 않고 보여준다</b>.
        ///
        /// 카메라가 플레이어를 떠나 벽으로 갔다가 다시 돌아오는 왕복 구조다. 2막의
        /// <see cref="SealOpening"/>이 같은 자리에서 <b>바닥</b>으로 내려가는 것과 짝을 이룬다 —
        /// 여기서 올려다본 것이 저기서 발밑에서 갈라진다.
        ///
        /// 총 10.8초. 상한은 <c>PlayerMovement.AutoUnfreezeTime</c>(20초)이고
        /// <c>CutsceneTimelineTests</c>가 여유 4초를 강제한다.
        /// </summary>
        private static CutsceneShot[] BuildSealDiscovery()
        {
            return new[]
            {
                // 1. 플레이어 어깨 너머에서 벽 쪽으로 — 무엇을 보고 있는지부터 맞춘다.
                new CutsceneShot(2.6f,
                    camFrom: new Vector3(0f, 2.0f, -3.2f),
                    camTo: new Vector3(-1.2f, 2.4f, -1.8f),
                    lookAt: new Vector3(0f, 2.2f, 3.4f),
                    subtitle: "벽은 장식이 아니었다. 목록이었다."),

                // 2. 벽을 옆으로 훑는다. 자막 없이 그림만 — 수를 세게 하려는 컷이다.
                new CutsceneShot(2.4f,
                    camFrom: new Vector3(-1.2f, 2.4f, -1.8f),
                    camTo: new Vector3(2.4f, 2.6f, -1.4f),
                    lookAt: new Vector3(0.8f, 2.4f, 3.6f)),

                // 3. 올라가 전체를 담는다. 빈칸이 보이기 시작하는 각.
                new CutsceneShot(2.8f,
                    camFrom: new Vector3(2.4f, 2.6f, -1.4f),
                    camTo: new Vector3(0.4f, 5.6f, -4.2f),
                    lookAt: new Vector3(0f, 2.0f, 3.0f),
                    subtitle: "이름 하나가 바랠 때마다, 세계에서 한 종이 지워졌다.",
                    dim: 0.18f),

                // 4. 내려와 플레이어 곁으로. 딤을 걷으며 끝낸다.
                new CutsceneShot(3.0f,
                    camFrom: new Vector3(0.4f, 5.6f, -4.2f),
                    camTo: new Vector3(0f, 2.4f, -3.4f),
                    lookAt: new Vector3(0f, 1.3f, 0.8f),
                    subtitle: "그리고 방금, 한 줄이 다시 채워졌다."),
            };
        }

        /// <summary>
        /// 「들어올 틈이 없어졌다」 — 최종전 직후. <see cref="NamelessConfront"/>이 딤을 쌓으며
        /// 들어갔으므로 여기서는 <b>딤을 걷으며 나온다</b>(0.40 → 0.22 → 0.08 → 0).
        /// 화면이 밝아지는 것 자체가 결말이라, 마지막 컷에는 자막을 두지 않는다.
        ///
        /// 「무명」을 이름으로 부르지 않는다 — 이 이야기의 규칙이자 승리 조건이다(StoryBible 2장).
        /// 총 9.0초.
        /// </summary>
        private static CutsceneShot[] BuildFinalSeal()
        {
            return new[]
            {
                // 1. 가장 어둡고 가장 가깝다 — 대치의 마지막 프레임을 이어받는다.
                new CutsceneShot(2.4f,
                    camFrom: new Vector3(0f, 1.5f, -2.0f),
                    camTo: new Vector3(0f, 1.9f, -2.8f),
                    lookAt: new Vector3(0f, 1.4f, 2.6f),
                    subtitle: "빌려 쓴 모습들이 하나씩 제자리로 돌아갔다.",
                    dim: 0.40f),

                // 2. 물러나며 올라간다. 자막 없이 — 빈 자리를 눈으로 확인하는 사이.
                new CutsceneShot(2.2f,
                    camFrom: new Vector3(0f, 1.9f, -2.8f),
                    camTo: new Vector3(-0.8f, 3.4f, -4.6f),
                    lookAt: new Vector3(0f, 1.2f, 2.0f),
                    dim: 0.22f),

                // 3. 가장 먼 각. 여기서만 결말을 말한다.
                new CutsceneShot(2.6f,
                    camFrom: new Vector3(-0.8f, 3.4f, -4.6f),
                    camTo: new Vector3(0.6f, 4.4f, -6.0f),
                    lookAt: new Vector3(0f, 1.0f, 1.6f),
                    subtitle: "그것은 죽지 않았다. 들어올 틈이 없어졌을 뿐이다.",
                    dim: 0.08f),

                // 4. 플레이어 곁으로 복귀. 딤 0 — 멀리서 끝내면 카메라가 튀어 돌아온다.
                new CutsceneShot(1.8f,
                    camFrom: new Vector3(0.6f, 4.4f, -6.0f),
                    camTo: new Vector3(0f, 2.6f, -3.6f),
                    lookAt: new Vector3(0f, 1.2f, 0.6f)),
            };
        }

        /// <summary>
        /// 거점이 무너진 직후 — 걷어 간 손이 사라진 땅.
        ///
        /// <b>딤을 걷으며 나온다</b>(0.42 → 0). 오염 아크의 다른 장면이 아니라 이 장면 하나가
        /// 아크의 마침표라, 들어갈 때가 아니라 나올 때 밝아지는 것이 맞다(<c>FinalSeal</c>이
        /// 같은 형태다). 흔들림은 첫 컷에만 약하게 — 구조물이 주저앉는 순간을 대신한다.
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
