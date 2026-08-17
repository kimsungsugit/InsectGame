using InsectGame.NPC;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>연출 한 스텝이 배우에게 시키는 일.</summary>
    public enum StageAction
    {
        /// <summary>즉시 배치 — 등장 전에 배우를 무대 밖에 세운다(걷기 없음).</summary>
        WarpToOffset,
        /// <summary>플레이어 기준 상대 좌표까지 걸어간다.</summary>
        MoveToOffset,
        /// <summary>스폰 앵커까지 걸어 돌아간다.</summary>
        ReturnToAnchor,
        /// <summary>즉시 플레이어를 바라본다.</summary>
        FacePlayer,
        /// <summary>몸짓 1회.</summary>
        Gesture,
        /// <summary>그냥 기다린다 — 사이(間)를 만든다.</summary>
        Wait,
    }

    /// <summary>
    /// 연출 한 스텝. 좌표는 <b>플레이어 기준 상대</b>이고 회전은 적용하지 않는다 —
    /// <see cref="CutsceneShot"/>의 카메라 오프셋과 같은 규약이라, 유적 지하처럼 원점이 다른
    /// 서브에리어에서도 그대로 맞는다.
    /// </summary>
    public struct StoryStageStep
    {
        /// <summary>배우 — <c>village_elder</c> 등. 월드에 없으면 그 스텝은 조용히 건너뛴다.</summary>
        public string storyNpcId;
        public StageAction action;
        /// <summary>Warp/Move — 플레이어 기준 상대 좌표.</summary>
        public Vector3 offset;
        /// <summary>Gesture — 재생할 몸짓.</summary>
        public NpcGesture gesture;
        /// <summary>Wait/FacePlayer/Gesture — 이 스텝에 머무는 시간(초).</summary>
        public float duration;
        /// <summary>Move/Return — 도착 판정 반경.</summary>
        public float arriveRadius;

        // ── 저작 편의 팩토리 (StoryStageLibrary가 읽기 좋게) ──

        public static StoryStageStep Warp(string npcId, Vector3 offset)
            => new StoryStageStep { storyNpcId = npcId, action = StageAction.WarpToOffset, offset = offset };

        public static StoryStageStep MoveTo(string npcId, Vector3 offset, float arriveRadius = 1.2f)
            => new StoryStageStep
            {
                storyNpcId = npcId, action = StageAction.MoveToOffset,
                offset = offset, arriveRadius = arriveRadius
            };

        public static StoryStageStep GoHome(string npcId)
            => new StoryStageStep { storyNpcId = npcId, action = StageAction.ReturnToAnchor, arriveRadius = 0.6f };

        public static StoryStageStep Face(string npcId, float duration = 0.25f)
            => new StoryStageStep { storyNpcId = npcId, action = StageAction.FacePlayer, duration = duration };

        public static StoryStageStep Play(string npcId, NpcGesture gesture)
            => new StoryStageStep { storyNpcId = npcId, action = StageAction.Gesture, gesture = gesture };

        public static StoryStageStep Pause(float seconds)
            => new StoryStageStep { action = StageAction.Wait, duration = seconds };
    }

    /// <summary>
    /// 연출 시간 계산의 <b>순수</b> 부분. MonoBehaviour와 떼어 놓아 테스트로 고정한다
    /// (<see cref="CutsceneTimeline"/>과 같은 성격).
    ///
    /// 여기서 나오는 값의 쓸모는 하나다 — <b>재생기의 하드 타임아웃</b>. 연출이 끝나지 않으면
    /// 대사 모달이 영영 안 뜨고 그 비트는 <c>pendingBeatId</c>에 갇혀 캠페인이 멈춘다.
    /// 그래서 "이 정도면 무슨 일이 있어도 끝났어야 한다"를 스텝에서 계산해 둔다.
    /// </summary>
    public static class StoryStageTimeline
    {
        /// <summary>이동 스텝 1개의 최악 시간 — <c>VillagerNpc.ScriptedTimeoutSeconds</c>와 같은 값.</summary>
        public const float MoveTimeoutSeconds = 8f;
        /// <summary>시퀀스 전체 상한. 아무리 길게 저작해도 이보다 오래 조작을 뺏지 않는다.</summary>
        public const float MaxSequenceSeconds = 15f;
        /// <summary>시퀀스 하한 — 스텝이 전부 즉시라도 프레임 몇 개는 돌 여유를 준다.</summary>
        public const float MinSequenceSeconds = 2f;

        /// <summary>스텝 하나가 최악의 경우 걸리는 시간(초).</summary>
        public static float WorstCaseSeconds(StoryStageStep step)
        {
            switch (step.action)
            {
                case StageAction.MoveToOffset:
                case StageAction.ReturnToAnchor:
                    return MoveTimeoutSeconds;
                case StageAction.Gesture:
                    return Mathf.Max(NpcGesturePose.DurationOf(step.gesture), Mathf.Max(0f, step.duration));
                case StageAction.FacePlayer:
                case StageAction.Wait:
                    return Mathf.Max(0f, step.duration);
                default:
                    // WarpToOffset 등 즉시 스텝 — 다음 Update에서 넘어가므로 시간을 세지 않는다.
                    return 0f;
            }
        }

        /// <summary>
        /// 시퀀스 전체의 하드 타임아웃(초). 스텝 합에 여유 1초를 더하고
        /// <see cref="MinSequenceSeconds"/>~<see cref="MaxSequenceSeconds"/>로 가둔다.
        /// </summary>
        public static float SequenceTimeoutSeconds(StoryStageStep[] steps)
        {
            if (steps == null || steps.Length == 0) return MinSequenceSeconds;
            float total = 0f;
            for (int i = 0; i < steps.Length; i++) total += WorstCaseSeconds(steps[i]);
            return Mathf.Clamp(total + 1f, MinSequenceSeconds, MaxSequenceSeconds);
        }
    }

    /// <summary>
    /// 대사 <b>앞</b>에 끼어드는 연출의 게이트. <see cref="InsectGame.UI.NpcDialogueUI"/>가
    /// 비트를 렌더하기 전에 물어본다.
    ///
    /// <b>구현체는 어떤 경우에도 <c>onDone</c>을 반드시 부른다.</b> true를 돌려놓고 안 부르면
    /// 그 비트의 대사가 영영 안 뜨고 캠페인이 그 자리에서 멈춘다.
    /// </summary>
    public interface IStoryStagePrelude
    {
        /// <summary>
        /// 이 비트에 입장 연출이 있으면 재생을 시작하고 true. 그때 대사는 <paramref name="onDone"/>이
        /// 불릴 때까지 미뤄진다. 연출이 없으면 false — 호출부가 곧바로 대사를 띄운다.
        /// </summary>
        bool TryPlayPrelude(StoryBeat beat, System.Action onDone);
    }
}
