using UnityEngine;

namespace InsectGame.NPC
{
    /// <summary>
    /// NPC가 1회 재생하는 몸짓. 걷기 스윙·idle 호흡 <b>위에 덮어쓴다</b>.
    /// 새 값을 넣으면 <see cref="NpcGesturePose.Evaluate"/>의 switch와
    /// <see cref="NpcGesturePose.DurationOf"/>에 둘 다 등록해야 한다 — 하나만 넣으면
    /// 길이 0이거나 포즈 0이라 <b>조용히 아무 일도 일어나지 않는다</b>.
    /// </summary>
    public enum NpcGesture
    {
        None,
        /// <summary>뜰채 휘두르기 — 곤충잡이 아이의 포획 액션(기존 PlaySwing).</summary>
        NetSwing,
        /// <summary>손 흔들기 — "이보게!" 부르는 몸짓. 조우 접근 도착 시.</summary>
        Wave,
        /// <summary>가리키기 — 방향 지시. 들었다가 유지하고 내린다.</summary>
        Point,
        /// <summary>끄덕임 — 수긍. 팔은 건드리지 않는다.</summary>
        Nod,
        /// <summary>움찔 — 놀람/경계. 팔을 뒤로 젖히고 몸을 살짝 낮춘다.</summary>
        Recoil,
        /// <summary>건네기 — 보상·물건을 내민다. 양팔을 앞으로.</summary>
        Offer,
    }

    /// <summary>
    /// 제스처 한 순간의 관절 변위. 각도는 <b>도</b>이고 축 규약은
    /// <see cref="NpcWalkAnimator"/>·<c>PlayerMovement.AnimateWalk</c>와 같다 —
    /// 팔은 X축(앞뒤 틸트), 머리는 X(끄덕임)/Y(도리질).
    ///
    /// <b>owns* 플래그가 핵심이다.</b> 제스처가 지배하지 않는 관절은 걷기/idle 파형을
    /// 그대로 쓴다. 뜰채 스윙이 오른팔만 뺏고 왼팔은 평소대로 흔들리는 기존 동작이
    /// 이 규약으로 표현된다.
    /// </summary>
    public struct NpcPoseDelta
    {
        public float rightArmDeg;
        public float leftArmDeg;
        public float headPitchDeg;
        public float headYawDeg;
        /// <summary>몸통 상하 변위(m). 걷기 밥과 <b>더해지지 않고</b> 대체된다.</summary>
        public float bodyOffsetY;

        public bool ownsRightArm;
        public bool ownsLeftArm;
        public bool ownsHead;
        /// <summary>몸통 높이를 제스처가 지배하는가.</summary>
        public bool ownsBody;
    }

    /// <summary>
    /// 제스처의 <b>순수</b> 계산부. MonoBehaviour와 씬에서 떼어 놓아 PlayMode 테스트로 고정한다
    /// (<see cref="InsectGame.Story.CutsceneTimeline"/>·<c>StoryObjectiveResolver</c>와 같은 성격).
    ///
    /// <b>리그 제약이 진폭을 정한다.</b> ArmL/ArmR은 어깨 피벗 없이 캡슐 <i>중심</i>에서 회전하고
    /// HandL/HandR은 팔의 자식이 아니라 루트 직속이라 <b>팔을 돌려도 손은 안 따라온다</b>
    /// (<c>NpcVisualBuilder</c>의 노드 구성). 그래서 기존 뜰채 스윙의 72°를 상한 삼아
    /// 그 안에서만 움직인다 — 더 크게 돌리면 손이 팔에서 떨어져 나온 게 눈에 띈다.
    ///
    /// 모든 곡선은 <b>t=0과 t=1에서 정확히 0으로 돌아온다</b>. 안 그러면 제스처가 끝난 뒤
    /// 관절이 틀어진 채 남아 그 NPC가 영영 어색한 자세로 서 있게 된다.
    /// </summary>
    public static class NpcGesturePose
    {
        /// <summary>뜰채 스윙 최대각 — 다른 제스처의 진폭 상한이기도 하다.</summary>
        public const float SwingMaxDeg = 72f;

        /// <summary>제스처 1회 재생 시간(초). 등록되지 않은 값은 0 — 즉시 끝난다.</summary>
        public static float DurationOf(NpcGesture gesture)
        {
            switch (gesture)
            {
                case NpcGesture.NetSwing: return 0.60f;   // CatcherKidNpc CatchSwing과 동기
                case NpcGesture.Wave: return 1.30f;
                case NpcGesture.Point: return 1.50f;
                case NpcGesture.Nod: return 0.90f;
                case NpcGesture.Recoil: return 0.80f;
                case NpcGesture.Offer: return 1.40f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 제스처 진행도 <paramref name="t"/>(0~1)에서의 관절 변위.
        /// 알 수 없는 값은 전부 0인 포즈(= 아무것도 지배하지 않음)로 떨어진다 — 안전한 쪽이다.
        /// </summary>
        public static NpcPoseDelta Evaluate(NpcGesture gesture, float t)
        {
            NpcPoseDelta pose = default;
            if (gesture == NpcGesture.None) return pose;
            t = Mathf.Clamp01(t);

            switch (gesture)
            {
                case NpcGesture.NetSwing:
                    // 0 → peak → 0. 기존 NpcWalkAnimator/PlayerMovement의 잡기 아크 그대로.
                    pose.rightArmDeg = Mathf.Sin(t * Mathf.PI) * SwingMaxDeg;
                    pose.ownsRightArm = true;
                    break;

                case NpcGesture.Wave:
                {
                    // 팔을 뒤로 들어(음의 X) 3회 떤다. 봉투가 sin(πt)라 양 끝이 0이다.
                    float env = Mathf.Sin(t * Mathf.PI);
                    pose.rightArmDeg = -(34f + 16f * Mathf.Sin(t * Mathf.PI * 6f)) * env;
                    pose.headYawDeg = 6f * env;
                    pose.ownsRightArm = true;
                    pose.ownsHead = true;
                    break;
                }

                case NpcGesture.Point:
                {
                    // 사다리꼴: 0.25 동안 들고, 유지하고, 0.25 동안 내린다.
                    float hold = Trapezoid(t, 0.25f);
                    pose.rightArmDeg = -58f * hold;
                    pose.headYawDeg = -8f * hold;     // 가리키는 쪽을 함께 본다
                    pose.ownsRightArm = true;
                    pose.ownsHead = true;
                    break;
                }

                case NpcGesture.Nod:
                    // 고개만 두 번. 팔은 걷기/idle 파형을 그대로 둔다.
                    pose.headPitchDeg = 12f * Mathf.Sin(t * Mathf.PI * 4f) * Mathf.Sin(t * Mathf.PI);
                    pose.ownsHead = true;
                    break;

                case NpcGesture.Recoil:
                {
                    // 빠르게 젖혔다가 천천히 돌아온다 — 지수 0.6이 앞을 가파르게 만든다.
                    // **Max(0, ...)가 없으면 안 된다**: sin(π)는 부동소수점에서 -8.7e-8이라
                    // 분수 지수 Pow가 NaN을 뱉고, 그 NaN이 Quaternion.Euler로 흘러가면
                    // 그 NPC의 트랜스폼이 영구히 망가진다(제스처가 끝나도 복구 불가).
                    float env = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.6f);
                    pose.rightArmDeg = -30f * env;
                    pose.leftArmDeg = -30f * env;
                    pose.headPitchDeg = -10f * env;   // 턱을 든다
                    pose.bodyOffsetY = -0.03f * env;  // 살짝 움츠린다
                    pose.ownsRightArm = true;
                    pose.ownsLeftArm = true;
                    pose.ownsHead = true;
                    pose.ownsBody = true;
                    break;
                }

                case NpcGesture.Offer:
                {
                    float hold = Trapezoid(t, 0.30f);
                    pose.rightArmDeg = 44f * hold;
                    pose.leftArmDeg = 44f * hold;
                    pose.headPitchDeg = 5f * hold;
                    pose.ownsRightArm = true;
                    pose.ownsLeftArm = true;
                    pose.ownsHead = true;
                    break;
                }
            }

            return pose;
        }

        /// <summary>
        /// 0에서 올라가 1로 유지하다 0으로 내려오는 사다리꼴(양 끝은 정확히 0).
        /// <paramref name="ramp"/>가 0.5 이상이면 삼각형이 된다.
        /// </summary>
        private static float Trapezoid(float t, float ramp)
        {
            ramp = Mathf.Clamp(ramp, 0.01f, 0.5f);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / ramp));
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / ramp));
            return Mathf.Min(rise, fall);
        }
    }
}
