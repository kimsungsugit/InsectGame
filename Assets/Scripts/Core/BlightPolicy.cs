using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 명부회 오염 거점이 살아 있는 동안 그 리전이 어떻게 달라지는가 — <b>순수 계산부</b>.
    ///
    /// <c>UISafeLayout</c>·<c>CutsceneTimeline</c>·<c>StoryStageTimeline</c>과 같은 계열이다.
    /// MonoBehaviour 없이 도는 함수만 두어 EditMode 테스트가 씬 없이 값을 고정한다.
    ///
    /// <b>리전 ID를 하나도 적지 않는다.</b> "어느 리전이 오염 대상인가"는
    /// <see cref="Data.RegionData.blightBossNpcId"/>가 답하고 여기는 "오염이면 얼마나"만 답한다.
    /// 하드코딩 리전 목록은 이 저장소에서 세 번 조용히 어긋났다(RegionDefinitions 주석 참조).
    /// </summary>
    public static class BlightPolicy
    {
        /// <summary>
        /// 오염 리전에서도 유지하는 동시 출현 하한.
        ///
        /// <b>0으로 내리면 캠페인이 영구 정지한다.</b> 오염 아크의 비트 둘이 그 리전에서의
        /// 포획(<c>bl_*_sign</c>)과 전투 승리(<c>bl_*_clash</c>)를 조건으로 걸고, 같은 리전의
        /// 1막 비트도 특정 종 포획을 요구한다(산의 아폴로나비, 유적의 유물풍뎅이).
        /// 곤충이 하나도 안 뜨면 그 전부가 발화 지점에 영영 도달하지 못한다.
        ///
        /// "황폐함"은 <b>수를 줄여서</b> 만들고 0으로 만들지 않는다.
        /// </summary>
        public const int MinActive = 2;

        /// <summary>오염 시 동시 출현 상한을 몇 분의 1로 줄이는가.</summary>
        public const int ScarcityDivisor = 3;

        /// <summary>지면 탈색 강도(0=원색, 1=완전 탈색). 완전히 회색으로 만들지는 않는다.</summary>
        public const float GroundDrainAmount = 0.72f;

        /// <summary>
        /// 탈색과 함께 낮추는 밝기 배수.
        ///
        /// <b>채도만 빼면 안 되는 이유를 실제 화면에서 배웠다.</b> 파일럿 리전인 산은
        /// 지면색이 이미 (0.35, 0.325, 0.24)인 무채색 갈색이라 뺄 채도가 없다 — 옛 공식은
        /// 그 색을 (0.340, 0.319, 0.267)로 만들었다. 사실상 그대로이고 파랑은 오히려 올라간다.
        /// 배치모드 캡처로 오염/정화를 나란히 찍어 보니 두 장이 구분되지 않았다.
        ///
        /// 밝기를 함께 내리면 어떤 원색에서도 변화가 보인다(산 기준 약 30% 어두워진다).
        /// </summary>
        public const float DrainBrightness = 0.62f;

        /// <summary>
        /// 이 리전의 동시 출현 상한. 오염이면 <see cref="ScarcityDivisor"/>로 나누되
        /// <see cref="MinActive"/> 아래로는 내려가지 않는다.
        ///
        /// <paramref name="baseMax"/>를 상수로 가정하지 않는다 — 스포너의 기본값은 10이고
        /// <c>ApplyTuning</c>이 프로파일 값으로 덮는다.
        /// </summary>
        public static int MaxActiveFor(bool blighted, int baseMax)
        {
            if (baseMax <= 0) return baseMax;   // 스포너가 "리전 상한 없음"으로 쓰는 값 — 건드리지 않는다
            if (!blighted) return baseMax;
            return Mathf.Max(MinActive, baseMax / ScarcityDivisor);
        }

        /// <summary>
        /// 오염된 땅의 색 — 휘도는 남기고 색조만 뺀다.
        ///
        /// <c>InsectEntity.Erase</c>와 같은 휘도 보존식이다. 명암이 남아야 지형의 굴곡이
        /// 읽히고, 그래야 "죽은 땅"이지 "안 그려진 땅"이 아니게 된다.
        /// 다만 저쪽은 검은 실루엣까지 밀어붙이고 여기는 <paramref name="amount"/>만큼만 간다 —
        /// 지면이 새까매지면 플레이어와 곤충이 배경에 묻힌다.
        /// </summary>
        public static Color TintOf(Color source, float amount)
        {
            float t = Mathf.Clamp01(amount);
            float lum = source.r * 0.299f + source.g * 0.587f + source.b * 0.114f;
            // 완전 무채색이 아니라 누렇게 죽은 회색으로 간다 — 잿빛보다 "말라 죽은" 인상이 난다.
            // 밝기도 함께 내린다: 채도만 빼면 원래 무채색인 리전(산)에서 아무 변화가 안 보인다.
            Color drained = new Color(
                lum * 1.04f * DrainBrightness,
                lum * 0.98f * DrainBrightness,
                lum * 0.86f * DrainBrightness,
                source.a);
            return new Color(
                Mathf.Lerp(source.r, drained.r, t),
                Mathf.Lerp(source.g, drained.g, t),
                Mathf.Lerp(source.b, drained.b, t),
                source.a);
        }

        /// <summary>기본 강도의 지면 탈색.</summary>
        public static Color TintOf(Color source) => TintOf(source, GroundDrainAmount);
    }
}
