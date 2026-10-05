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
            // 치명타는 리졸버가 굴린 진짜 판정이다(RaidActionResult.Critical) — 세기 구간과 치명타 연출이 함께 따른다.
            bool critical = action.Critical && action.Damage > 0;
            // 전용기는 리졸버가 1대1과 같은 기준(SignatureSkills)으로 적어 둔 표지다 — 빗나가도 꺼낸 것 자체가 연출거리다.
            return new BattleArenaController.HitCue(action.DisplayName,
                BattleArenaController.HitCue.WeightFor(action.Damage, Mathf.Max(1, bossMax / 4), critical),
                action.KnockedOut, action.Missed, critical, action.IsSignature);
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
            // 보스의 한 행동에 한 번 굴린 판정(전체 공격이면 맞은 전원이 같다) — 붉은 별 빛살·CriticalHit 소리가 따른다.
            bool critical = round.BossAction != null && round.BossAction.Critical && damage > 0;
            bool signature = round.BossAction != null && round.BossAction.IsSignature;   // 보스의 지금 모습 기준(리졸버)
            return new BattleArenaController.HitCue(null, BattleArenaController.HitCue.WeightFor(damage, maxHp, critical),
                fainted, false, critical, signature);
        }

        /// <summary>
        /// 레이드를 시작할 때 이미 기절해 있던 슬롯(HP 0). 아레나가 그 모델을 세우지 않도록
        /// <c>BattleArenaController.SetupRaidBattle(…, teamDown)</c>에 넘긴다 — 컨트롤러는 그 슬롯을 처음부터
        /// "쓰러짐을 보여 줬다"로 쳐서(<c>teamFaintPresented</c>) 레이드 내내 아무도 눕히지 않는다.
        /// </summary>
        private bool[] TeamDownAtStart()
        {
            InsectBattleStats[] team = raidController != null ? raidController.TeamStats : null;
            if (team == null) return null;
            var down = new bool[team.Length];
            for (int i = 0; i < team.Length; i++)
                down[i] = team[i] == null || team[i].CurrentHp <= 0;
            return down;
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
        private float teamStripHide;

        /// <summary>
        /// 팀 HP 패널 줄의 y. 스킬을 고를 때는 화면 53%(스킬 패널 바로 위), 3D 연출이 도는 동안
        /// (팀원 공격·보스 예고·보스 공격·합체공격)에는 화면 아래로 내려 비켜선다 — 레이드 카메라 구도에서
        /// 팀원 모델이 정확히 그 줄 뒤에 서서, 돌진도 피격도 패널에 가려 보이지 않았다(QA 캡처, 개편 전부터).
        /// <b>내려갈 때는 곧바로, 올라올 때만 0.25초에 걸쳐 미끄러진다</b>(실제 시간 — 히트스톱에 멈추지 않게, <see cref="RaidTeamStrip.NextDrop"/>).
        /// 예전엔 내려갈 때도 미끄러져서, 기술을 고른 순간 스킬 패널이 사라진 자리에 줄과 행동 문구 띠가 화면 가운데에 떴다가 내려갔다.
        /// </summary>
        private float TeamStripY()
        {
            float rest = Mathf.Clamp(UIScale.VirtualScreenHeight * 0.53f, UISafeLayout.ContentTop,
                UISafeLayout.ContentBottom - TeamStripHeight);
            if (!Arena3D) return rest;
            bool acting = phase == Phase.PlayerAttack || phase == Phase.BossTelegraph
                || phase == Phase.BossAttack || phase == Phase.UniteAttack;
            if (acting) teamStripDrop = 1f;   // 이벤트 종류와 무관하게 곧바로 — 레이아웃 패스와 그리기 패스가 같은 자리를 본다
            else if (Event.current == null || Event.current.type == EventType.Repaint)
                teamStripDrop = RaidTeamStrip.NextDrop(teamStripDrop, false, Time.unscaledDeltaTime);
            float eased = Mathf.SmoothStep(0f, 1f, teamStripDrop);
            return Mathf.Lerp(rest, UISafeLayout.BottomY(TeamStripHeight), eased);
        }

        /// <summary>
        /// 팀 줄의 불투명도(1 = 보임). 수문장 등장·그림자 변신·결과 동안은 숨는다 — 그때 줄이 화면 가운데(53%)에 떠서 보스 아랫부분과
        /// 변신 연기를 가렸고(3단계 QA), 아래로 내리면 바닥에 서는 수문장 배너·변신 문구와 겹친다. 수문장 등장은 전투 첫 장면이라 곧바로 숨고,
        /// 나머지는 0.2초에 옅어진다(<see cref="RaidTeamStrip.NextHide"/>). 합체 게이지도 같이 숨는다.
        /// </summary>
        private float TeamStripVisibility()
        {
            bool guardianIntro = phase == Phase.Intro && IsGuardianRaid;
            bool hidden = RaidTeamStrip.Hidden(guardianIntro, phase == Phase.BossTransform, phase == Phase.Result);
            if (hidden && guardianIntro) teamStripHide = 1f;
            else if (Event.current == null || Event.current.type == EventType.Repaint)
                teamStripHide = RaidTeamStrip.NextHide(teamStripHide, hidden, false, Time.unscaledDeltaTime);
            return 1f - teamStripHide;
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
