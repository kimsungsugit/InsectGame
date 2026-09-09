using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 영상 위에 얹는 자막 한 줄. <b>자막은 영상에 굽지 않는다</b> — AI 생성 영상의 한글 렌더링이
    /// 불안정하고, 문구 수정·「무명」 금칙(StoryBible 2장)·현지화를 코드 한 곳에서 하기 위해서다.
    /// </summary>
    public struct StoryVideoCue
    {
        /// <summary>영상 시작 기준 자막이 뜨는 시각(초).</summary>
        public float at;
        /// <summary>자막이 떠 있는 길이(초). 앞뒤 페이드는 <see cref="CutsceneTimeline.SubtitleAlpha"/>가 준다.</summary>
        public float duration;
        public string text;

        public StoryVideoCue(float at, float duration, string text)
        {
            this.at = at;
            this.duration = duration;
            this.text = text;
        }

        public float End => at + duration;
    }

    /// <summary>
    /// 영상 한 편의 정의. 파일은 <c>Assets/StreamingAssets/Video/</c>에 두고 <c>VideoPlayer.url</c>로
    /// 스트리밍한다(임포터를 타지 않아 Unity 트랜스코딩·메모리 상주가 없다).
    /// </summary>
    public sealed class StoryVideoDefinition
    {
        public string videoId;
        /// <summary>파일명(확장자 포함). 경로는 <see cref="StoryVideoDirector"/>가 붙인다.</summary>
        public string fileName;
        /// <summary>
        /// 저작 기준 길이(초). 실제 파일과 어긋나도 재생은 파일이 끝날 때 끝나지만, 프리즈 상한
        /// 판정(테스트)과 디코더가 종료 이벤트를 안 주는 기기의 안전망(<c>+3s</c>)이 이 값을 쓴다.
        /// </summary>
        public float expectedDuration;
        public StoryVideoCue[] cues;

        public StoryVideoDefinition(string videoId, string fileName, float expectedDuration, StoryVideoCue[] cues)
        {
            this.videoId = videoId;
            this.fileName = fileName;
            this.expectedDuration = expectedDuration;
            this.cues = cues ?? new StoryVideoCue[0];
        }
    }

    /// <summary>
    /// 영상 자막 타임라인의 <b>순수</b> 계산 — MonoBehaviour와 떼어 테스트로 고정한다
    /// (<see cref="CutsceneTimeline"/>과 같은 성격).
    /// </summary>
    public static class StoryVideoTimeline
    {
        /// <summary>
        /// 경과 시각에 떠 있어야 할 자막 인덱스. 없으면 false. 큐가 겹치면 앞의 것이 이긴다
        /// (겹침 자체는 테스트가 금지한다).
        /// </summary>
        public static bool TryGetCue(StoryVideoCue[] cues, float elapsed, out int index)
        {
            index = -1;
            if (cues == null || cues.Length == 0) return false;
            for (int i = 0; i < cues.Length; i++)
            {
                if (elapsed >= cues[i].at && elapsed < cues[i].End)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>그 큐 안의 0~1 진행도 — 페이드 계산용.</summary>
        public static float CueProgress(StoryVideoCue cue, float elapsed)
        {
            if (cue.duration <= 0f) return 1f;
            return Mathf.Clamp01((elapsed - cue.at) / cue.duration);
        }
    }
}
