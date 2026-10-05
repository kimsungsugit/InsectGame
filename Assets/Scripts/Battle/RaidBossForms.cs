using System.Collections.Generic;
using InsectGame.Data;

namespace InsectGame.Battle
{
    /// <summary>
    /// 체력에 따라 <b>모습을 바꾸는</b> 레이드 보스 표 — 보스 곤충 ID → 변신 단계 목록(순수 데이터·순수 판정).
    ///
    /// 첫 항목은 이름 없는 사마귀(<c>mantis_unnamed</c>)다. 이름 없는 자리의 수문장이자 최종장 「빈칸」의 상대인
    /// 그림자가, 빼앗은 이름의 모습을 차례로 빌린다 — 사마귀였다가, 나비였다가, 반딧불이였다가.
    /// 표가 종 ID로 걸리므로 같은 종의 레이드는 전부 변신한다(수문장·빈칸·야생 모두).
    ///
    /// <b>바뀌는 것은 속성(주·부)과 기술뿐</b>이다(<see cref="RaidBossStats.ChangeForm"/>) — HP·최대 HP·레벨·격노는
    /// 그대로다. 정체(<see cref="InsectBattleStats.Data"/>)도 그대로라 이기면 잡히는 것은 사마귀다.
    /// 상성이 바뀌므로 플레이어는 대응을 바꿔야 한다.
    ///
    /// 단계는 <b>래치</b>다 — 한 번 넘은 임계는 회복해도 되돌아가지 않는다(<see cref="NextFormIndex"/>).
    /// 판정·실행은 <see cref="RaidBattleController"/>가, 표시는 <c>RaidBattleUI</c>(변신 단계)가 한다.
    /// </summary>
    public static class RaidBossForms
    {
        /// <summary>변신 한 단계 — HP가 <see cref="HpPercent"/>% 이하가 되면 <see cref="FormInsectId"/>의 모습을 빌린다.</summary>
        public readonly struct Stage
        {
            /// <summary>이 값(%) <b>이하</b>로 떨어지면 이 모습. 단계는 내림차순이어야 한다(테스트가 고정한다).</summary>
            public readonly int HpPercent;
            /// <summary>빌리는 모습의 곤충 ID — 곤충 DB에 실재해야 한다(<c>RaidBossFormTests</c>).</summary>
            public readonly string FormInsectId;
            /// <summary>표시용 한 줄의 틀. <c>{0}</c>에 빌린 곤충의 표시명이 들어간다.</summary>
            public readonly string LineFormat;

            public Stage(int hpPercent, string formInsectId, string lineFormat)
            {
                HpPercent = hpPercent;
                FormInsectId = formInsectId;
                LineFormat = lineFormat;
            }
        }

        private const string BorrowLine = "그림자가 {0}의 모습을 빌렸다!";

        private static readonly Dictionary<string, Stage[]> Table = new Dictionary<string, Stage[]>
        {
            // 이름 없는 사마귀 — `gd_nameless`의 "몸이 여러 모양으로 번진다". 66% 나비, 33% 반딧불이.
            ["mantis_unnamed"] = new[]
            {
                new Stage(66, "butterfly_swallowtail", BorrowLine),
                new Stage(33, "firefly_blue", BorrowLine),
            },
        };

        /// <summary>표에 오른 보스 곤충 ID 전부 — 검증용.</summary>
        public static IEnumerable<string> BossIds => Table.Keys;

        /// <summary>이 보스가 모습을 바꾸는가.</summary>
        public static bool HasForms(string bossInsectId) => StageCount(bossInsectId) > 0;

        /// <summary>변신 단계 수(원래 모습 제외). 표에 없으면 0.</summary>
        public static int StageCount(string bossInsectId)
        {
            return !string.IsNullOrEmpty(bossInsectId) && Table.TryGetValue(bossInsectId, out Stage[] stages)
                ? stages.Length
                : 0;
        }

        /// <summary>변신 단계 목록의 사본(원래 모습 제외). 표에 없으면 false·빈 배열.</summary>
        public static bool TryGetStages(string bossInsectId, out Stage[] stages)
        {
            if (string.IsNullOrEmpty(bossInsectId) || !Table.TryGetValue(bossInsectId, out Stage[] found))
            {
                stages = System.Array.Empty<Stage>();
                return false;
            }
            stages = (Stage[])found.Clone();
            return true;
        }

        /// <summary>
        /// 지금 HP로 서야 할 모습 번호 — 0이면 원래 모습, k면 k번째 변신. HP가 낮을수록 번호가 크다(단조).
        /// 비율은 정수로 잰다(<c>currentHp × 100 ≤ maxHp × 임계</c>) — 부동소수점 경계에서 한 끗 차로 갈리지 않게.
        /// 이 함수는 래치를 모른다 — 실제로 쓸 번호는 <see cref="NextFormIndex"/>.
        /// </summary>
        public static int FormIndexFor(string bossInsectId, int currentHp, int maxHp)
        {
            if (maxHp <= 0 || string.IsNullOrEmpty(bossInsectId)
                || !Table.TryGetValue(bossInsectId, out Stage[] stages)) return 0;

            int index = 0;
            long hp100 = (long)System.Math.Max(0, currentHp) * 100L;
            for (int i = 0; i < stages.Length; i++)
            {
                if (hp100 <= (long)maxHp * stages[i].HpPercent) index = i + 1;
                else break;   // 내림차순이라 이 단계를 못 넘었으면 뒤 단계도 못 넘었다
            }
            return index;
        }

        /// <summary>
        /// 래치를 건 다음 모습 번호 — <paramref name="currentIndex"/>보다 작아지지 않는다(회복해도 돌아가지 않는다).
        /// <b>HP가 0 이하면 바꾸지 않는다</b> — 쓰러지는 보스는 변신 없이 쓰러진다.
        /// 한 번에 임계를 둘 넘으면 마지막 모습 번호를 곧바로 돌려준다(중간 모습은 건너뛴다).
        /// </summary>
        public static int NextFormIndex(string bossInsectId, int currentIndex, int currentHp, int maxHp)
        {
            if (currentHp <= 0) return currentIndex;
            int target = FormIndexFor(bossInsectId, currentHp, maxHp);
            return target > currentIndex ? target : currentIndex;
        }

        /// <summary>모습 번호의 곤충 ID — 0이면 보스 자신, 범위 밖이면 null.</summary>
        public static string FormInsectId(string bossInsectId, int formIndex)
        {
            if (formIndex == 0) return bossInsectId;
            if (formIndex < 0 || string.IsNullOrEmpty(bossInsectId)
                || !Table.TryGetValue(bossInsectId, out Stage[] stages) || formIndex > stages.Length) return null;
            return stages[formIndex - 1].FormInsectId;
        }

        /// <summary>
        /// 표시용 한 줄 — "그림자가 호랑나비의 모습을 빌렸다!". 표시명이 비면 곤충 ID를 쓴다(빈칸으로 뜨는 것보다 낫다).
        /// 원래 모습(0)·범위 밖이면 빈 문자열.
        /// </summary>
        public static string BuildLine(string bossInsectId, int formIndex, string formDisplayName)
        {
            if (formIndex <= 0 || string.IsNullOrEmpty(bossInsectId)
                || !Table.TryGetValue(bossInsectId, out Stage[] stages) || formIndex > stages.Length) return string.Empty;
            Stage stage = stages[formIndex - 1];
            string name = string.IsNullOrEmpty(formDisplayName) ? stage.FormInsectId : formDisplayName;
            return string.IsNullOrEmpty(stage.LineFormat) ? string.Empty : string.Format(stage.LineFormat, name);
        }
    }

    /// <summary>
    /// 보스가 모습을 바꿨다 — <see cref="RaidBattleController.BossFormChanged"/>가 싣는다.
    /// <b>한 번의 피해로 임계를 둘 이상 넘으면 이벤트는 한 번</b>이고 <see cref="ToIndex"/>가 마지막 모습이다
    /// (<see cref="SkippedStages"/>가 true). 화면은 변신 단계를 한 번만 돌리면 된다.
    /// </summary>
    public sealed class RaidBossFormChange
    {
        /// <summary>바뀌기 전 모습 번호(0 = 원래 모습).</summary>
        public int FromIndex { get; internal set; }
        /// <summary>바뀐 뒤 모습 번호.</summary>
        public int ToIndex { get; internal set; }
        /// <summary>바뀌기 전 모습의 곤충(상성·기술의 출처였던 것).</summary>
        public InsectData FromData { get; internal set; }
        /// <summary>바뀐 뒤 모습의 곤충 — 아레나가 이 곤충으로 보스 모델을 다시 세운다.</summary>
        public InsectData ToData { get; internal set; }
        /// <summary>표시용 한 줄("그림자가 반딧불이의 모습을 빌렸다!") — 마지막 모습의 것.</summary>
        public string Line { get; internal set; }
        /// <summary>중간 모습을 건너뛰었는가(한 번에 임계 둘 이상).</summary>
        public bool SkippedStages => ToIndex - FromIndex > 1;
    }
}
