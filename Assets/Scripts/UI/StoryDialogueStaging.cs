using System;
using System.Collections.Generic;

namespace InsectGame.UI
{
    /// <summary>
    /// 스토리 대사 한 줄의 연출 규칙 — 줄별 fx 태그 해석, 타자 공개 글자 수, 인물의 좌우 배치.
    ///
    /// 대사창(<see cref="NpcDialogueUI"/>)이 그리는 것과 **무엇을 언제 보여주는가**를 나눈다.
    /// 이쪽은 Unity API를 하나도 쓰지 않는 순수 계산이라 <c>StoryDialogueStagingTests</c>가 씬 없이 고정한다.
    ///
    /// 옛 대사창은 한 줄을 통째로 띄우고 초상 하나를 왼쪽에 붙였다. 144비트 중 두 사람 이상이 말하는
    /// 장면이 23개뿐이었던 것도 있지만, <b>주고받아도 주고받는 것처럼 보이지 않았다</b> — 화자가 바뀌어도
    /// 같은 자리의 초상만 바뀌었다. 지금은 먼저 말한 사람이 왼쪽, 다음 사람이 오른쪽에 서서 마주 본다.
    /// </summary>
    public static class StoryDialogueStaging
    {
        /// <summary>해설 줄의 화자 — 초상·이름표 없이 가운데 기울임으로 그린다.</summary>
        public const string NarrationSpeaker = "지문";

        /// <summary>기본 타자 속도(글자/초). 한국어 한 줄(30~50자)이 1~1.3초에 다 나온다.</summary>
        public const float CharsPerSecond = 38f;
        /// <summary><c>pause</c> — 글자가 나오기 전의 사이(間).</summary>
        public const float PauseSeconds = 0.7f;
        /// <summary>흔들림 길이·세기(가상 픽셀).</summary>
        public const float ShakeSeconds = 0.45f;
        public const float ShakeAmplitude = 12f;
        /// <summary>번쩍임이 사라지는 시간과 시작 알파.</summary>
        public const float FlashSeconds = 0.4f;
        public const float FlashAlpha = 0.75f;
        /// <summary>화자가 바뀔 때 새 화자가 뛰어오르는 시간·높이.</summary>
        public const float HopSeconds = 0.22f;
        public const float HopHeight = 14f;

        [Flags]
        public enum LineFx
        {
            None = 0,
            /// <summary>화면이 흔들린다 — 충격·붕괴·분노.</summary>
            Shake = 1 << 0,
            /// <summary>흰 번쩍임 — 깨달음·섬광.</summary>
            Flash = 1 << 1,
            /// <summary>말하기 전 사이 — 망설임·무게.</summary>
            Pause = 1 << 2,
            /// <summary>외침 — 크게, 빠르게, 짧게 흔들린다.</summary>
            Shout = 1 << 3,
            /// <summary>속삭임 — 작게, 흐리게, 느리게.</summary>
            Whisper = 1 << 4,
            /// <summary>느리게 — 한 글자씩 곱씹는 줄.</summary>
            Slow = 1 << 5,
            /// <summary>배경이 더 어두워진다 — 불길함.</summary>
            Dark = 1 << 6,
        }

        /// <summary>저작에 쓸 수 있는 토큰 — story_lint 검사 28이 이 목록과 대조한다(여기가 단일 출처).</summary>
        public static readonly string[] KnownFxTokens =
        {
            "shake", "flash", "pause", "shout", "whisper", "slow", "dark",
        };

        public static bool IsNarration(string speaker) => speaker == NarrationSpeaker;

        /// <summary>쉼표로 이어진 토큰을 해석한다. 대소문자·공백은 무시하고, 모르는 토큰은 버린다.</summary>
        public static LineFx ParseFx(string fx)
        {
            if (string.IsNullOrEmpty(fx)) return LineFx.None;
            LineFx result = LineFx.None;
            string[] tokens = fx.Split(',');
            for (int i = 0; i < tokens.Length; i++)
            {
                switch (tokens[i].Trim().ToLowerInvariant())
                {
                    case "shake": result |= LineFx.Shake; break;
                    case "flash": result |= LineFx.Flash; break;
                    case "pause": result |= LineFx.Pause; break;
                    case "shout": result |= LineFx.Shout; break;
                    case "whisper": result |= LineFx.Whisper; break;
                    case "slow": result |= LineFx.Slow; break;
                    case "dark": result |= LineFx.Dark; break;
                }
            }
            return result;
        }

        public static float TypingSpeed(LineFx fx)
        {
            float speed = CharsPerSecond;
            if ((fx & LineFx.Shout) != 0) speed *= 1.5f;
            if ((fx & LineFx.Whisper) != 0) speed *= 0.7f;
            if ((fx & LineFx.Slow) != 0) speed *= 0.5f;
            return speed;
        }

        public static float StartDelay(LineFx fx) => (fx & LineFx.Pause) != 0 ? PauseSeconds : 0f;

        /// <summary>줄이 뜬 지 <paramref name="elapsed"/>초 뒤에 보이는 글자 수.</summary>
        public static int VisibleChars(int length, float elapsed, LineFx fx)
        {
            if (length <= 0) return 0;
            float typing = elapsed - StartDelay(fx);
            if (typing <= 0f) return 0;
            double count = Math.Floor(typing * TypingSpeed(fx));
            return count >= length ? length : (int)count;
        }

        /// <summary>줄이 다 나오는 데 걸리는 시간(초).</summary>
        public static float RevealDuration(int length, LineFx fx)
        {
            return length <= 0 ? 0f : StartDelay(fx) + length / TypingSpeed(fx);
        }

        /// <summary>흔들림 가로 오프셋 — 감쇠하는 떨림. 연출이 없거나 끝났으면 0.</summary>
        public static float ShakeOffset(LineFx fx, float elapsed)
        {
            float duration = (fx & LineFx.Shake) != 0 ? ShakeSeconds : (fx & LineFx.Shout) != 0 ? ShakeSeconds * 0.5f : 0f;
            if (duration <= 0f || elapsed < 0f || elapsed >= duration) return 0f;
            float amplitude = (fx & LineFx.Shake) != 0 ? ShakeAmplitude : ShakeAmplitude * 0.5f;
            float decay = 1f - elapsed / duration;
            return (float)Math.Sin(elapsed * 70.0) * amplitude * decay * decay;
        }

        /// <summary>번쩍임 오버레이 알파 — 줄이 뜨는 순간 가장 밝고 빠르게 걷힌다.</summary>
        public static float FlashOverlay(LineFx fx, float elapsed)
        {
            if ((fx & LineFx.Flash) == 0 || elapsed < 0f || elapsed >= FlashSeconds) return 0f;
            float t = 1f - elapsed / FlashSeconds;
            return FlashAlpha * t * t;
        }

        /// <summary>새 화자가 뛰어오르는 높이(위로 +). 화자가 그대로면 0.</summary>
        public static float HopOffset(float sinceSpeakerChange)
        {
            if (sinceSpeakerChange < 0f || sinceSpeakerChange >= HopSeconds) return 0f;
            float t = sinceSpeakerChange / HopSeconds;
            return HopHeight * 4f * t * (1f - t);
        }

        public enum Side
        {
            None,
            Left,
            Right,
        }

        /// <summary>한 줄에서 무대에 선 두 사람과 지금 말하는 쪽.</summary>
        public struct Stage
        {
            public string LeftId;
            public string RightId;
            public Side Active;
        }

        /// <summary>
        /// 줄마다 무대 배치를 정한다. <paramref name="portraitIds"/>는 줄별 초상 ID이고 null이면
        /// 초상이 없는 줄(지문·미등록 화자)이다 — 그 줄엔 아무도 말하지 않고 서 있던 사람은 그대로 선다.
        ///
        /// 먼저 말한 사람이 왼쪽, 다음 사람이 오른쪽. 세 번째 사람이 끼면 <b>더 오래 말하지 않은 쪽</b>과
        /// 자리를 바꾼다 — 방금 말한 사람을 치우면 대꾸하는 그림이 끊긴다.
        /// </summary>
        public static Stage[] AssignSides(IList<string> portraitIds)
        {
            int n = portraitIds != null ? portraitIds.Count : 0;
            Stage[] stages = new Stage[n];
            string left = null, right = null;
            int leftLast = -1, rightLast = -1;
            for (int i = 0; i < n; i++)
            {
                string id = portraitIds[i];
                Side active = Side.None;
                if (!string.IsNullOrEmpty(id))
                {
                    if (id == left) { active = Side.Left; leftLast = i; }
                    else if (id == right) { active = Side.Right; rightLast = i; }
                    else if (left == null) { left = id; active = Side.Left; leftLast = i; }
                    else if (right == null) { right = id; active = Side.Right; rightLast = i; }
                    else if (leftLast < rightLast) { left = id; active = Side.Left; leftLast = i; }
                    else { right = id; active = Side.Right; rightLast = i; }
                }
                stages[i] = new Stage { LeftId = left, RightId = right, Active = active };
            }
            return stages;
        }
    }
}
