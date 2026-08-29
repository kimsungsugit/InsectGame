using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>표정. 휴지 상태는 캐릭터 생성 때 고른 <c>faceType</c>이 정한다.</summary>
    public enum FaceExpression
    {
        /// <summary>생성 화면에서 고른 얼굴형 그대로. 아무도 표정을 지정하지 않았을 때의 상태.</summary>
        Idle,
        Smile,
        Surprise,
        Sad,
    }

    /// <summary>
    /// 얼굴을 살아 있게 만든다 — 눈 깜빡임과 표정 전환.
    ///
    /// <b>왜 별도 컴포넌트인가</b> (<c>PlayerMovement.AnimateWalk</c>에 넣지 않는 이유):
    /// ① 그 메서드에는 손으로 맞춘 불변식이 촘촘하다(도구 회전과 오른팔 각도의 분리,
    ///    <c>!walking &amp;&amp; catchSwingTimer &lt;= 0</c>일 때만 base 재캐싱, 어깨 Z를 0으로 고정).
    ///    얼굴 코드를 섞으면 다음 사람이 그 주석들을 전부 읽어야 한다.
    /// ② 얼굴은 걷기와 <b>직교</b>한다 — 서 있을 때도 대화 중에도 깜빡여야 한다.
    /// ③ 프리뷰 마네킹에는 <c>PlayerMovement</c>가 아예 없다.
    ///
    /// 얼굴 노드가 없는 캐릭터(NPC는 눈·동공만 있다)에도 붙을 수 있게 전부 null 가드를 둔다.
    /// </summary>
    public class CharacterFaceAnimator : MonoBehaviour
    {
        // ── 깜빡임 파라미터 ──
        private const float BlinkDuration = 0.12f;
        private const float BlinkIntervalMin = 2.5f;
        private const float BlinkIntervalMax = 6.0f;

        // ── 노드 ──
        private Transform eyeL, eyeR, pupilL, pupilR, hlL, hlR;
        private Transform browL, browR, mouth;

        /// <summary>
        /// 눈 관련 노드의 <b>원래</b> 세로 스케일. 깜빡임은 여기에 배율을 <b>대입</b>한다.
        ///
        /// 매 프레임 곱하면 눈이 영영 사라진다 — <c>PlayerMovement</c>의 도구 base 회전이
        /// 같은 종류의 드리프트를 겪고 <c>netHandleBaseRot</c> 캐싱으로 막은 그 문제다.
        /// </summary>
        private float[] baseEyeScaleY;
        private Transform[] eyeNodes;

        private Vector3 browBaseLocalEulerL, browBaseLocalEulerR;
        private Vector3 browBaseLocalPosL, browBaseLocalPosR;
        private Vector3 mouthBaseScale, mouthBaseLocalPos;

        private float blinkAt;
        private float blinkStartedAt = -1f;
        private FaceExpression expression = FaceExpression.Idle;
        private bool nodesResolved;

        private void Start()
        {
            ResolveNodes();
            blinkAt = Time.time + NextBlinkDelay(Random.value);
        }

        private void ResolveNodes()
        {
            if (nodesResolved) return;
            nodesResolved = true;

            Transform head = OutfitShapeLibrary.FindDeep(transform, "HeadPivot");
            if (head == null) head = transform;

            eyeL = OutfitShapeLibrary.FindDeep(head, "EyeL");
            eyeR = OutfitShapeLibrary.FindDeep(head, "EyeR");
            pupilL = OutfitShapeLibrary.FindDeep(head, "PupilL");
            pupilR = OutfitShapeLibrary.FindDeep(head, "PupilR");
            hlL = OutfitShapeLibrary.FindDeep(head, "HighlightL");
            hlR = OutfitShapeLibrary.FindDeep(head, "HighlightR");
            browL = OutfitShapeLibrary.FindDeep(head, "BrowL");
            browR = OutfitShapeLibrary.FindDeep(head, "BrowR");
            mouth = OutfitShapeLibrary.FindDeep(head, "Mouth");

            // 눈꺼풀이 따로 없으므로 눈 관련 노드를 함께 눌러 감는 것처럼 보이게 한다.
            eyeNodes = new[] { eyeL, eyeR, pupilL, pupilR, hlL, hlR };
            baseEyeScaleY = new float[eyeNodes.Length];
            for (int i = 0; i < eyeNodes.Length; i++)
                baseEyeScaleY[i] = eyeNodes[i] != null ? eyeNodes[i].localScale.y : 1f;

            if (browL != null)
            {
                browBaseLocalEulerL = browL.localEulerAngles;
                browBaseLocalPosL = browL.localPosition;
            }
            if (browR != null)
            {
                browBaseLocalEulerR = browR.localEulerAngles;
                browBaseLocalPosR = browR.localPosition;
            }
            if (mouth != null)
            {
                mouthBaseScale = mouth.localScale;
                mouthBaseLocalPos = mouth.localPosition;
            }
        }

        private void Update()
        {
            float now = Time.time;

            if (blinkStartedAt >= 0f)
            {
                float phase = (now - blinkStartedAt) / BlinkDuration;
                if (phase >= 1f)
                {
                    ApplyEyeScale(1f);
                    blinkStartedAt = -1f;
                    blinkAt = now + NextBlinkDelay(Random.value);
                }
                else
                {
                    ApplyEyeScale(BlinkScale(phase));
                }
            }
            else if (now >= blinkAt)
            {
                blinkStartedAt = now;
            }
        }

        /// <summary>
        /// 깜빡임 곡선. 0과 1에서 1(뜬 눈), 중간에서 0(감은 눈)이다.
        /// 순수 함수라 <c>CharacterFaceAnimatorTests</c>가 값으로 고정한다.
        /// </summary>
        internal static float BlinkScale(float phase)
        {
            float p = Mathf.Clamp01(phase);
            // 감았다 뜨는 한 사이클. cos이 0→π→2π를 돌며 1→−1→1이 되므로 절반 진폭으로 0까지 내린다.
            return 0.5f + 0.5f * Mathf.Cos(p * Mathf.PI * 2f);
        }

        /// <summary>다음 깜빡임까지의 간격. <paramref name="random01"/>은 0~1.</summary>
        internal static float NextBlinkDelay(float random01)
        {
            return Mathf.Lerp(BlinkIntervalMin, BlinkIntervalMax, Mathf.Clamp01(random01));
        }

        private void ApplyEyeScale(float factor)
        {
            if (eyeNodes == null) return;
            for (int i = 0; i < eyeNodes.Length; i++)
            {
                if (eyeNodes[i] == null) continue;
                Vector3 sc = eyeNodes[i].localScale;
                // 곱셈 누적이 아니라 base에 대한 대입 — 드리프트 방지(위 주석 참조).
                sc.y = baseEyeScaleY[i] * factor;
                eyeNodes[i].localScale = sc;
            }
        }

        /// <summary>
        /// 깜빡임을 즉시 끝내고 눈을 뜬 상태로 되돌린다.
        ///
        /// 프리뷰 썸네일은 <c>Update</c>에서 한 장씩 구워지므로 하필 눈을 감은 프레임에 찍힐 수
        /// 있다 — 그 카드는 캐시에 남아 계속 감은 눈으로 보인다. 렌더 직전에 부른다.
        /// </summary>
        public void ResetToNeutral()
        {
            ResolveNodes();
            ApplyEyeScale(1f);
            blinkStartedAt = -1f;
            blinkAt = Time.time + NextBlinkDelay(Random.value);
        }

        // ── 표정 ──

        /// <summary>
        /// 표정을 바꾼다. <see cref="FaceExpression.Idle"/>이면 캐릭터 생성 때 고른 얼굴로 돌아간다.
        ///
        /// <b>호출부는 아직 없다.</b> 스토리 연출·포획 성공 등이 쓸 수 있게 열어만 뒀다 —
        /// 아무도 부르지 않으면 관측 가능한 변화가 0이라 지금 상태와 완전히 같다.
        /// </summary>
        public void SetExpression(FaceExpression e)
        {
            ResolveNodes();
            expression = e;
            ApplyExpression();
        }

        public FaceExpression CurrentExpression => expression;

        /// <summary>
        /// 표정별 (눈썹 각도, 눈썹 높이 오프셋, 입 폭 배율, 입 높이 오프셋).
        /// 순수 테이블이라 테스트가 값으로 고정한다.
        /// </summary>
        internal static void ExpressionValues(FaceExpression e,
            out float browTiltDeg, out float browRaise, out float mouthWidthScale, out float mouthRaise)
        {
            switch (e)
            {
                case FaceExpression.Smile:
                    browTiltDeg = -6f; browRaise = 0.012f; mouthWidthScale = 1.45f; mouthRaise = -0.004f; break;
                case FaceExpression.Surprise:
                    browTiltDeg = 0f; browRaise = 0.030f; mouthWidthScale = 0.75f; mouthRaise = -0.016f; break;
                case FaceExpression.Sad:
                    browTiltDeg = 11f; browRaise = -0.010f; mouthWidthScale = 0.85f; mouthRaise = -0.010f; break;
                default:   // Idle — 생성 화면이 고른 얼굴 그대로
                    browTiltDeg = 0f; browRaise = 0f; mouthWidthScale = 1f; mouthRaise = 0f; break;
            }
        }

        private void ApplyExpression()
        {
            ExpressionValues(expression, out float tilt, out float raise, out float widthScale, out float mouthRaise);

            // 눈썹은 좌우가 거울상이라야 한쪽만 치켜뜬 것처럼 보이지 않는다.
            if (browL != null)
            {
                browL.localEulerAngles = browBaseLocalEulerL + new Vector3(0f, 0f, -tilt);
                browL.localPosition = browBaseLocalPosL + new Vector3(0f, raise, 0f);
            }
            if (browR != null)
            {
                browR.localEulerAngles = browBaseLocalEulerR + new Vector3(0f, 0f, tilt);
                browR.localPosition = browBaseLocalPosR + new Vector3(0f, raise, 0f);
            }

            if (mouth != null)
            {
                Vector3 sc = mouthBaseScale;
                sc.x = mouthBaseScale.x * widthScale;
                mouth.localScale = sc;
                mouth.localPosition = mouthBaseLocalPos + new Vector3(0f, mouthRaise, 0f);
            }
        }
    }
}
