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
        /// <summary>2막 개막 — 유적 지하에서 봉인이 열리는 장면.</summary>
        public const string SealOpening = "cs_seal_opening";
        /// <summary>최종장 — 무명과의 대치.</summary>
        public const string NamelessConfront = "cs_nameless_confront";

        public static bool TryGet(string cutsceneId, out CutsceneShot[] shots)
        {
            switch (cutsceneId)
            {
                case SealOpening: shots = BuildSealOpening(); return true;
                case NamelessConfront: shots = BuildNamelessConfront(); return true;
                default: shots = null; return false;
            }
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
    }
}
