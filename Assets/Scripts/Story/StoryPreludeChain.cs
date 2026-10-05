using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 대사 앞 연출 여럿을 <b>차례로</b> 잇는 고리 — 대화창(<see cref="InsectGame.UI.NpcDialogueUI"/>)의 연출 게이트가
    /// 하나(<c>AutoWire(IStoryStagePrelude)</c>)라서 둔다. 부트스트랩이 <b>영상(<see cref="StoryVideoDirector"/>) →
    /// NPC 등장 연출(<see cref="StoryStageDirector"/>)</b> 순서로 넘긴다: 영상이 무슨 일인지 설명하고, 그다음 인물이 걸어 들어와 말한다.
    /// 장을 여는 비트면 그 뒤에 「지난 이야기」 카드, 그리고 대사다(카드는 대화창이 연다 — 드라마의 콜드 오픈과 같은 순서).
    ///
    /// <b>계약(<see cref="IStoryStagePrelude"/>)을 그대로 지킨다</b> — true면 <c>onDone</c>을 나중에(또는 이미) 정확히 한 번,
    /// false면 절대 안 부른다. 못 지키면 그 비트가 <c>pendingBeatId</c>에 갇혀 캠페인이 멈추거나 대사가 두 번 열린다.
    /// <list type="bullet">
    /// <item>고리가 false면 다음 고리로. 전부 false면 false — 대화창이 곧바로 대사를 띄운다.</item>
    /// <item>고리가 true면 그 고리의 콜백에서 <b>다음 고리부터</b> 이어 간다. 남은 고리가 다 false면 그때 <c>onDone</c>.</item>
    /// <item><b>고리가 던지면 연출만 버리고 진행은 살린다</b> — 대화창의 try/catch는 첫 고리(동기 호출)만 감싼다. 영상이 끝난 뒤
    ///   콜백 안에서 등장 연출이 던지면 그 예외는 여기서만 받을 수 있다(안 받으면 대사가 영영 안 열린다).</item>
    /// <item>던진 고리가 콜백을 나중에 부르면 <b>무시한다</b>(이미 다음으로 넘어갔다 — 두 번 열지 않는다). 같은 콜백이 두 번 와도 한 번만 잇는다.</item>
    /// </list>
    /// 순수 C# 클래스다(MonoBehaviour 아님) — 고리 자체의 수명 검사(꺼짐·파괴)는 각 고리가 한다.
    /// 파괴된 Unity 객체 고리는 여기서도 건너뛴다(인터페이스 참조의 null 비교는 파괴 검사를 못 한다).
    /// </summary>
    public sealed class StoryPreludeChain : IStoryStagePrelude
    {
        private readonly IStoryStagePrelude[] links;

        public StoryPreludeChain(params IStoryStagePrelude[] links)
        {
            this.links = links ?? new IStoryStagePrelude[0];
        }

        /// <summary>고리 수(테스트·진단용).</summary>
        public int Count => links.Length;

        public bool TryPlayPrelude(StoryBeat beat, System.Action onDone)
        {
            if (beat == null || onDone == null) return false;
            return RunFrom(0, beat, onDone);
        }

        /// <summary>
        /// <paramref name="start"/>번째 고리부터 연다. 하나라도 시작했으면 true(그 뒤는 콜백이 잇는다), 아무것도 안 열렸으면 false.
        /// </summary>
        private bool RunFrom(int start, StoryBeat beat, System.Action onDone)
        {
            for (int i = start; i < links.Length; i++)
            {
                IStoryStagePrelude link = links[i];
                if (IsGone(link)) continue;

                var hop = new Hop(this, i + 1, beat, onDone);
                bool started;
                try
                {
                    started = link.TryPlayPrelude(beat, hop.Fire);
                }
                catch (System.Exception e)
                {
                    Debug.LogError(hop.Fired
                        ? $"[StoryPrelude] {link.GetType().Name} 뒤의 연출·대사 열기에서 예외: {e}"
                        : $"[StoryPrelude] {link.GetType().Name} 연출 실패 — 다음으로 넘어간다: {e}");
                    started = false;
                }

                // 콜백이 이미 왔다(동기로 끝났거나, false를 돌려주면서 계약을 어기고 불렀다) — 그 콜백이 뒤를 이었다.
                if (hop.Fired) return true;
                if (started) return true;

                // 시작하지 않았다(또는 던졌다) — 이 고리의 늦은 콜백은 버린다. 이미 다음 고리로 넘어가므로.
                hop.Disarm();
            }
            return false;
        }

        /// <summary>고리 하나가 끝났다 — 다음 고리부터 이어 열고, 남은 게 없으면 대사를 연다.</summary>
        private void Continue(int next, StoryBeat beat, System.Action onDone)
        {
            if (RunFrom(next, beat, onDone)) return;
            onDone();
        }

        private static bool IsGone(IStoryStagePrelude link)
        {
            if (link == null) return true;
            // 인터페이스 참조의 `== null`은 C# 참조 비교라 파괴된 MonoBehaviour를 못 거른다 — Unity의 `==`로 다시 본다.
            return link is UnityEngine.Object unityObject && unityObject == null;
        }

        /// <summary>고리 하나에 건 콜백 — 한 번만 잇고, 버린 고리의 늦은 콜백은 무시한다.</summary>
        private sealed class Hop
        {
            private readonly StoryPreludeChain chain;
            private readonly int next;
            private readonly StoryBeat beat;
            private readonly System.Action onDone;
            private bool disarmed;

            public bool Fired { get; private set; }

            public Hop(StoryPreludeChain chain, int next, StoryBeat beat, System.Action onDone)
            {
                this.chain = chain;
                this.next = next;
                this.beat = beat;
                this.onDone = onDone;
            }

            public void Disarm() => disarmed = true;

            public void Fire()
            {
                if (Fired || disarmed) return;
                Fired = true;
                chain.Continue(next, beat, onDone);
            }
        }
    }
}
