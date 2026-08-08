using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 컷신 한 컷. 카메라 좌표는 <b>플레이어 기준 상대</b>다 — 절대 좌표로 두면 유적 지하처럼
    /// 별도 원점을 쓰는 서브에리어에서 카메라가 엉뚱한 곳으로 간다.
    /// </summary>
    public struct CutsceneShot
    {
        public float duration;
        /// <summary>컷 시작 시 카메라 위치(플레이어 기준 오프셋).</summary>
        public Vector3 camFrom;
        /// <summary>컷 끝 카메라 위치. from과 다르면 돌리 인/아웃이 된다.</summary>
        public Vector3 camTo;
        /// <summary>카메라가 바라보는 지점(플레이어 기준 오프셋).</summary>
        public Vector3 lookAt;
        /// <summary>자막. 비면 그 컷은 자막 없이 그림만 보여준다.</summary>
        public string subtitle;
        /// <summary>이 컷에서 터뜨릴 흔들림 세기(0이면 없음). 컷 시작 시 1회.</summary>
        public float shake;
        /// <summary>화면 딤 정도(0~1). 컷 안에서 일정하다.</summary>
        public float dim;

        public CutsceneShot(float duration, Vector3 camFrom, Vector3 camTo, Vector3 lookAt,
            string subtitle = null, float shake = 0f, float dim = 0f)
        {
            this.duration = duration;
            this.camFrom = camFrom;
            this.camTo = camTo;
            this.lookAt = lookAt;
            this.subtitle = subtitle;
            this.shake = shake;
            this.dim = dim;
        }
    }

    /// <summary>
    /// 컷신 타임라인의 <b>순수</b> 계산. MonoBehaviour와 떼어 놓아 테스트로 고정한다
    /// (<see cref="StoryObjectiveResolver"/>와 같은 성격).
    /// </summary>
    public static class CutsceneTimeline
    {
        public static float TotalDuration(CutsceneShot[] shots)
        {
            if (shots == null) return 0f;
            float total = 0f;
            for (int i = 0; i < shots.Length; i++) total += Mathf.Max(0f, shots[i].duration);
            return total;
        }

        /// <summary>
        /// 경과 시각이 몇 번째 컷의 어디인가. 범위를 벗어나면 false(재생 종료).
        /// <paramref name="t"/>는 그 컷 안의 0~1 진행도다.
        ///
        /// 경계에서 컷이 하나 건너뛰지 않게 <b>누적 합으로</b> 찾는다 — 컷마다 시작 시각을
        /// 따로 들고 있으면 duration을 고칠 때 그 값들이 조용히 어긋난다.
        /// </summary>
        public static bool TryGetShot(CutsceneShot[] shots, float elapsed, out int index, out float t)
        {
            index = -1;
            t = 0f;
            if (shots == null || shots.Length == 0) return false;
            if (elapsed < 0f) elapsed = 0f;

            float cursor = 0f;
            for (int i = 0; i < shots.Length; i++)
            {
                float d = Mathf.Max(0.0001f, shots[i].duration);
                if (elapsed < cursor + d)
                {
                    index = i;
                    t = Mathf.Clamp01((elapsed - cursor) / d);
                    return true;
                }
                cursor += d;
            }
            return false;
        }

        /// <summary>컷 안의 카메라 위치. 양 끝에서 부드럽게 붙도록 SmoothStep을 쓴다.</summary>
        public static Vector3 CameraOffsetAt(CutsceneShot shot, float t)
            => Vector3.Lerp(shot.camFrom, shot.camTo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));

        /// <summary>
        /// 자막 알파 — 컷 시작에 페이드 인, 끝에 페이드 아웃. 컷이 아주 짧으면(페이드 2배 미만)
        /// 아예 페이드하지 않는다(깜빡임 방지).
        /// </summary>
        public static float SubtitleAlpha(float duration, float t, float fade = 0.35f)
        {
            if (duration <= fade * 2f) return 1f;
            float elapsed = Mathf.Clamp01(t) * duration;
            float remaining = duration - elapsed;
            return Mathf.Clamp01(Mathf.Min(elapsed, remaining) / fade);
        }
    }
}
