using System;
using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 주간 크기 대결 진행자. 매주 저레어 종 하나를 지정하고, 그 종을 포획할 때마다
    /// 이번 주 최대 개체 기록이 자동으로 갱신된다(별도 제출 UI 없음).
    ///
    /// <b>기록을 저장하지 않는다.</b> 보유 곤충 중 대상 종이면서 <c>capturedUnix</c>가
    /// 이번 주에 든 개체의 최대 크기를 매번 훑어 구한다 — <c>player_insects.json</c>이
    /// 이미 클라우드로 통째 올라가므로 기록도 기기 간에 저절로 따라온다.
    /// 저장하는 건 보상 수령 여부 하나뿐이다.
    /// </summary>
    public class WeeklyContestManager : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private InsectDatabase database;

        private readonly List<InsectData> pool = new List<InsectData>();
        private int cachedWeek = -1;
        private InsectData cachedTarget;

        /// <summary>기록이 갱신되어 등급이 올라갔을 때만 발화(같은 등급 유지면 조용하다).</summary>
        public event Action<ContestTier> TierReached;

        public InsectData TargetInsect => ResolveTarget();
        public int CurrentWeek => WeeklyContestSchedule.WeekIndex(NowUnix());

        public void AutoWire(PlayerInsectCollection col, InsectDatabase db)
        {
            if (collection != col)
            {
                if (collection != null) collection.InsectCaptured -= OnInsectCaptured;
                collection = col;
                // 포획 전용 이벤트를 쓴다 — InsectUpdated는 치료·레벨업·진화에도 울려서
                // 기록 갱신이 오발화한다(StoryDirector가 같은 이유로 이 이벤트를 쓴다).
                if (collection != null) collection.InsectCaptured += OnInsectCaptured;
            }
            if (database == null) database = db;
            cachedWeek = -1;   // 풀 재구성 유도
        }

        private void OnDestroy()
        {
            if (collection != null) collection.InsectCaptured -= OnInsectCaptured;
        }

        private static long NowUnix()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // 주차가 바뀌었을 때만 풀을 다시 만든다 — 128종 정렬을 매 조회마다 돌리지 않는다.
        private InsectData ResolveTarget()
        {
            int week = CurrentWeek;
            if (week != cachedWeek || cachedTarget == null)
            {
                pool.Clear();
                if (database != null && database.insects != null)
                    pool.AddRange(WeeklyContestSchedule.BuildPool(database.insects));
                cachedTarget = WeeklyContestSchedule.TargetFor(week, pool);
                cachedWeek = week;
            }
            return cachedTarget;
        }

        /// <summary>이번 주 대상 종으로 잡은 개체 중 가장 큰 몸길이(mm). 없으면 0.</summary>
        public float BestSizeMm()
        {
            InsectData target = ResolveTarget();
            if (target == null || collection == null) return 0f;

            int week = CurrentWeek;
            float best = 0f;
            List<PlayerInsectData> owned = collection.GetAllOwned();
            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData pid = owned[i];
                if (pid == null || pid.insectId != target.insectId) continue;
                if (!WeeklyContestSchedule.IsWithinWeek(pid.capturedUnix, week)) continue;

                float mm = InsectSizeCalculator.SizeMm(target, pid);
                if (mm > best) best = mm;
            }
            return best;
        }

        public ContestTier CurrentTier()
        {
            InsectData target = ResolveTarget();
            if (target == null || target.baseSizeMm <= 0f) return ContestTier.None;
            return WeeklyContestSchedule.TierForRatio(BestSizeMm() / target.baseSizeMm);
        }

        /// <summary>다음 등급까지 남은 몸길이(mm). 이미 금이면 0.</summary>
        public float RemainingMmToNextTier()
        {
            InsectData target = ResolveTarget();
            if (target == null) return 0f;

            ContestTier current = CurrentTier();
            if (current == ContestTier.Gold) return 0f;

            ContestTier next = current == ContestTier.None ? ContestTier.Bronze
                : current == ContestTier.Bronze ? ContestTier.Silver
                : ContestTier.Gold;
            return Mathf.Max(0f, WeeklyContestSchedule.RequiredMm(target, next) - BestSizeMm());
        }

        // ── 보상 수령 상태 (PlayerPrefs — 퀘스트 세이브와 같은 방식) ──
        // "주차:등급" 형태. 주차가 바뀌면 문자열이 안 맞아 자동으로 미수령이 된다.

        private static string ClaimKey =>
            AuthManager.ScopedKey(GameConstants.PrefsKeys.WeeklyContestClaimed);

        public ContestTier ClaimedTier()
        {
            string raw = PlayerPrefs.GetString(ClaimKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return ContestTier.None;

            int sep = raw.IndexOf(':');
            if (sep <= 0) return ContestTier.None;
            if (!int.TryParse(raw.Substring(0, sep), out int week) || week != CurrentWeek)
                return ContestTier.None;
            if (!int.TryParse(raw.Substring(sep + 1), out int tier)) return ContestTier.None;

            return (ContestTier)Mathf.Clamp(tier, 0, (int)ContestTier.Gold);
        }

        /// <summary>
        /// 아직 받지 않은 등급이 있으면 그 등급을 돌려주고 수령 처리한다. 없으면 false.
        /// 등급이 올라갈 때마다 차액을 받는 구조라 동→은→금을 순서대로 받아도 손해가 없다.
        /// </summary>
        public bool TryClaim(out ContestTier claimed)
        {
            claimed = CurrentTier();
            if (claimed == ContestTier.None || claimed <= ClaimedTier()) return false;

            PlayerPrefs.SetString(ClaimKey, CurrentWeek + ":" + (int)claimed);
            PlayerPrefs.Save();
            return true;
        }

        private void OnInsectCaptured(PlayerInsectData captured)
        {
            InsectData target = ResolveTarget();
            if (target == null || captured == null || captured.insectId != target.insectId) return;

            ContestTier tier = CurrentTier();
            if (tier == ContestTier.None) return;

            // 이미 도달했던 등급이면 다시 알리지 않는다 — 같은 등급 개체를 여러 마리 잡을 때
            // 매번 토스트가 뜨는 걸 막는다.
            if (tier <= ClaimedTier()) return;
            TierReached?.Invoke(tier);
        }
    }
}
