using InsectGame.Battle;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 레이드 연출에 넘길 타격 정보와 3D 위치 — 모놀리스 본체(<c>RaidBattleUI.cs</c>·<c>.Draw.cs</c>)를
    /// 키우지 않으려고 떼었다. 본체는 팀원 행동·보스 예고·보스 공격 콜백에서 여기를 부르고,
    /// 3D 아레나가 켜져 있으면 2D용 사각형 투사체·섬광 대신 <see cref="ArenaPoint"/>로 숫자만 모델 위에 붙인다.
    /// </summary>
    public partial class RaidBattleUI
    {
        /// <summary>3D 아레나가 연출을 맡고 있다 — 2D 폴백 그림(사각형 투사체·화면 섬광·폭발)은 그리지 않는다.</summary>
        private bool Arena3D => arena != null && arena.IsActive;

        /// <summary>
        /// 팀원 한 마리의 공격 세기. 보스 HP는 팀원 다섯 마리가 여러 라운드에 나눠 깎는 크기라
        /// 1v1처럼 "최대 HP의 40%"를 만점으로 두면 모든 타격이 솜방망이가 된다 — 보스 최대 HP의 10%를 만점으로 본다.
        /// </summary>
        private BattleArenaController.HitCue BuildMemberCue(RaidActionResult action)
        {
            int bossMax = raidController != null && raidController.BossStats != null ? raidController.BossStats.MaxHp : 0;
            return new BattleArenaController.HitCue(action.DisplayName,
                BattleArenaController.HitCue.WeightFor(action.Damage, Mathf.Max(1, bossMax / 4), false),
                action.KnockedOut, action.Missed);
        }

        /// <summary>보스 공격을 맞는 쪽의 세기 — 피해 ÷ 그 팀원의 최대 HP, 쓰러졌으면 마무리.</summary>
        private BattleArenaController.HitCue BuildBossCue(RaidRoundResult round, int slot)
        {
            if (round == null || raidController == null || raidController.TeamStats == null) return BattleArenaController.HitCue.None;
            int index = slot;
            if (index < 0)
            {
                // 전체 공격 — 가장 크게 맞은 팀원 기준으로 세기를 잡는다.
                int best = 0;
                for (int i = 0; i < round.BossDamageBySlot.Length; i++)
                    if (round.BossDamageBySlot[i] > best) { best = round.BossDamageBySlot[i]; index = i; }
            }
            if (index < 0 || index >= round.BossDamageBySlot.Length || index >= raidController.TeamStats.Length)
                return BattleArenaController.HitCue.None;
            InsectBattleStats victim = raidController.TeamStats[index];
            int damage = round.BossDamageBySlot[index];
            int maxHp = victim != null ? victim.MaxHp : 0;
            bool fainted = victim != null && damage > 0 && victim.CurrentHp <= 0;
            return new BattleArenaController.HitCue(null, BattleArenaController.HitCue.WeightFor(damage, maxHp, false), fainted, false);
        }

        /// <summary>
        /// 보스 공격 예고를 3D로도 건다 — 보스가 젖히며 기를 모으고, 노리는 팀원 발밑에 경고 고리.
        /// 길이는 UI 예고 배너와 같다(<c>BossTelegraphDuration</c>). 이름 있는 기술(시그니처)만 외친다 —
        /// 일반 공격의 이름은 "공격"이라 외치면 어색하다.
        /// </summary>
        private void BeginBossTelegraphPresentation()
        {
            if (!Arena3D || raidController == null) return;
            RaidBossIntent intent = activeRound != null ? activeRound.BossIntent : raidController.NextBossIntent;
            if (intent == null) return;
            arena.PlayRaidBossTelegraph(intent.TargetSlot, intent.IsArea, intent.Element,
                intent.Skill != null ? intent.DisplayName : null, BossTelegraphDuration);
        }

        private const float TeamStripHeight = 104f;
        private float teamStripDrop;

        /// <summary>
        /// 팀 HP 패널 줄의 y. 스킬을 고를 때는 화면 53%(스킬 패널 바로 위), 3D 연출이 도는 동안
        /// (팀원 공격·보스 예고·보스 공격·합체공격)에는 화면 아래로 내려 비켜선다 — 레이드 카메라 구도에서
        /// 팀원 모델이 정확히 그 줄 뒤에 서서, 돌진도 피격도 패널에 가려 보이지 않았다(QA 캡처, 개편 전부터).
        /// 0.25초에 걸쳐 미끄러진다(실제 시간 — 히트스톱에 멈추지 않게).
        /// </summary>
        private float TeamStripY()
        {
            float rest = Mathf.Clamp(UIScale.VirtualScreenHeight * 0.53f, UISafeLayout.ContentTop,
                UISafeLayout.ContentBottom - TeamStripHeight);
            if (!Arena3D) return rest;
            bool acting = phase == Phase.PlayerAttack || phase == Phase.BossTelegraph
                || phase == Phase.BossAttack || phase == Phase.UniteAttack;
            if (Event.current == null || Event.current.type == EventType.Repaint)
                teamStripDrop = Mathf.MoveTowards(teamStripDrop, acting ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
            float eased = Mathf.SmoothStep(0f, 1f, teamStripDrop);
            return Mathf.Lerp(rest, UISafeLayout.BottomY(TeamStripHeight), eased);
        }

        /// <summary>
        /// 아레나 화면 좌표(픽셀, y 위쪽)를 가상 캔버스 좌표로. 보스면 <paramref name="slot"/>은 -1.
        /// 3D가 아니거나 카메라 뒤면 false — 호출부는 2D 고정 좌표로 폴백한다.
        /// </summary>
        private bool ArenaPoint(int slot, out Vector2 point)
        {
            point = Vector2.zero;
            if (!Arena3D) return false;
            Vector3 screen = slot < 0 ? arena.GetCombatantScreenPosition(false) : arena.GetTeamScreenPosition(slot);
            if (screen.z <= 0f) return false;
            point = new Vector2(
                screen.x / Mathf.Max(1, Screen.width) * UIScale.VirtualScreenWidth,
                (1f - screen.y / Mathf.Max(1, Screen.height)) * UIScale.VirtualScreenHeight);
            return true;
        }
    }
}
