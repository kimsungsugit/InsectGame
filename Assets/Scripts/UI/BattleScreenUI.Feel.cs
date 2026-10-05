using System.Collections.Generic;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 1대1 전투의 <b>체감 화면</b>(4단계) — 진입(화면 쓸기 + 「야생 ○○이(가) 나타났다!」), 타격 순간의 상성 표시, 전용기 컷인 띠, 승리 화면.
    /// 자리·문구·시각은 순수 계산(<see cref="BattleEntryLayout"/>·<see cref="MatchupHud"/>·<see cref="SignatureCutInLayout"/>·
    /// <see cref="BattleVictoryLayout"/>)이, 칠하기는 <see cref="BattleFeelDraw"/>가 한다. 여기는 전투 상태를 거기에 잇기만 한다.
    ///
    /// 모놀리스 본체를 키우지 않으려고 partial로 뗐다. 본체는 OnGUI(진입·전용기 띠·아레나 머리 위 이름 끄기), <c>DrawAttackAnimation</c>(상성 표시),
    /// <c>DrawSkillPanel</c>(상성 칩), <c>DrawResult</c>(승리 화면·「눌러서 계속」)에서 부른다.
    /// </summary>
    public partial class BattleScreenUI
    {
        // ── 진입 ──

        private bool entryReady;
        private BattleKind entryKind;
        private string entryInsect;
        private string entryPerson;
        private string entryRegion;
        private string entryTitle;

        /// <summary>
        /// 진입 문구 — 종류·상대 곤충·사람 이름·리전이 바뀔 때만 다시 짓는다. 간부·라온은 상대 표지가 시작 함수 <b>뒤에</b> 서므로
        /// (<see cref="BattleKinds"/> 주석) 첫 프레임에 아이 문구였다가 곧바로 바뀔 수 있다 — 그래서 매 프레임 비교한다(할당은 바뀔 때만).
        /// </summary>
        private string EntryTitle()
        {
            if (enemyStats == null || enemyStats.Data == null) return null;
            SyncDuelContext();
            BattleKind kind = CurrentBattleKind;
            string insect = enemyStats.Data.displayName;
            string person = hasDuelLines ? duelLines.name : null;
            string region = guardianRegion != null ? guardianRegion.displayName : null;
            if (!entryReady || kind != entryKind || !string.Equals(insect, entryInsect)
                || !string.Equals(person, entryPerson) || !string.Equals(region, entryRegion))
            {
                entryReady = true;
                entryKind = kind;
                entryInsect = insect;
                entryPerson = person;
                entryRegion = region;
                entryTitle = BattleEntryText.For(kind, insect, person, region);
            }
            return entryTitle;
        }

        /// <summary>진입 카드의 강조색 — 야생 민트, 수문장 호박, 사람과의 대결 코랄.</summary>
        private static Color EntryAccent(BattleKind kind)
        {
            UITheme theme = UITheme.Instance;
            switch (kind)
            {
                case BattleKind.Wild: return theme.accentMint;
                case BattleKind.Guardian:
                case BattleKind.Sandbox: return theme.accentAmber;
                default: return theme.accentCoral;
            }
        }

        /// <summary>
        /// 진입 구간(<see cref="EntryIntroProgress"/>, 실제 1.4초) — 화면 쓸기가 HP 카드까지 덮고 열리며, 큰 문구가 위쪽 가운데에 튀어나온다.
        /// 대결·수문장 컷인이 이어지면 문구는 진입 구간 끝에 사라지고(컷인은 <c>DrawDuelCutIn</c>이 그 뒤에 그린다), 아니면 인트로 끝까지 남는다.
        /// 꿈 챔피언전은 문구가 없다(꿈이 「챔피언 결정전」 카드를 먼저 띄운다) — 화면 쓸기만.
        /// </summary>
        private void DrawEntry()
        {
            if (playerStats == null || enemyStats == null) return;
            HudFrame f = HudFrame.Current;
            bool reduced = BattlePresentation.ReducedMotion;
            float p = EntryIntroProgress;
            BattleFeelDraw.EntryWipe(f, p, reduced);
            string title = EntryTitle();
            if (title == null) return;
            float alpha = BattleEntryStaging.TitleAlpha(p, introTimer, IntroSeconds, HasCutIn);
            if (alpha <= 0.001f) return;
            float since = (p - BattleEntryStaging.TitleIn) * BattleReadPacing.EntryIntroSeconds;
            BattleFeelDraw.EntryTitle(BattleEntryLayout.DuelTitle(f), title, null, BattleEntryLayout.TitleFont,
                alpha, BattleEntryStaging.Pop(since, reduced), EntryAccent(entryKind));
        }

        // ── 상성 ──

        /// <summary>방금 보인 타격의 상성 등급 — 컨트롤러가 그 라운드에 남긴 값(빗나감·기본 공격·보통은 Neutral).</summary>
        private Matchup ImpactMatchup(bool playerAction)
        {
            if (battleController == null) return Matchup.Neutral;
            return playerAction ? battleController.LastPlayerHitMatchup : battleController.LastEnemyHitMatchup;
        }

        /// <summary>피해 숫자(치명타면 「치명타!」) 바로 위에 화살표 + 「아주 잘 통했다!」. 보통이면 아무것도 없다.</summary>
        private void DrawImpactMatchup(bool playerAction, float cx, float numberCenterY, int numberFontSize, bool crit, float alpha)
        {
            Matchup m = ImpactMatchup(playerAction);
            if (m == Matchup.Neutral) return;
            BattleFeelDraw.MatchupCaption(HudFrame.Current, cx, MatchupHud.CaptionCenterY(numberCenterY, numberFontSize, crit), m,
                alpha, MatchupHud.CaptionScale(impactTimer, BattlePresentation.ReducedMotion));
        }

        // ── 전용기 ──

        /// <summary>
        /// 1대1 화면이 떠 있는 동안 아레나의 전용기 머리 위 이름(말풍선)을 끈다 — 아래 띠가 같은 이름을 크게 띄운다. 레이드 화면은 다시 켠다
        /// (레이드는 사전 컷인이 없어 말풍선이 이름을 알린다). 정적 표지라 화면이 닫힐 때(<c>EndBattle</c>) 되돌린다.
        /// </summary>
        private static void SuppressArenaSignatureCallout() => BattleArenaController.ShowSignatureCallout = false;

        /// <summary>
        /// 전용기 컷인 띠 — 아레나가 시전자를 클로즈업하는 동안(<see cref="BattleArenaController.IsSignatureCutIn"/>) 화면을 가로지른다.
        /// 내 곤충이면 왼쪽에서, 상대면 오른쪽에서 밀려 들어오고, 컷인이 끝나면 잠깐 뒤 사라진다.
        /// </summary>
        private void DrawSignatureCutIn()
        {
            if (arena == null || !arena.IsActive || !arena.IsSignaturePlaying) return;
            if (phase != Phase.PlayerAttack && phase != Phase.EnemyAttack) return;
            float alpha = SignatureCutInLayout.Alpha(arena.SignatureElapsed - arena.SignatureCutInEndSeconds);
            if (alpha <= 0.001f) return;
            bool mine = arena.SignatureFromPlayer;
            InsectBattleStats caster = mine ? playerStats : enemyStats;
            string casterName = caster != null && caster.Data != null ? caster.Data.displayName : null;
            BattleFeelDraw.SignatureBand(HudFrame.Current, mine, casterName, arena.SignatureSkillName,
                BattleArenaController.GetUIElementColor(arena.SignatureElement), arena.SignatureCutInProgress, alpha,
                BattlePresentation.ReducedMotion);
        }

        // ── 승리 ──

        private List<RewardLine> victoryLines;
        private InsectBattleStats victoryLinesFor;
        private ItemDatabase resultItemDb;

        /// <summary>
        /// 승리 화면 — 위쪽 큰 「승리!」, 그 밑 상대의 한마디(대결), 가운데는 아레나 승리 연출, 아래 보상 판(0.8초부터 한 줄씩), 맨 아래 「눌러서 계속」.
        /// 시계는 결과 화면의 실제 초(<see cref="ResultShownSeconds"/>) — 아레나 승리 연출(<see cref="BattleFlourish.VictorySeconds"/>)과 같은 시계다.
        /// </summary>
        private void DrawVictory(bool sandbox)
        {
            HudFrame f = HudFrame.Current;
            bool reduced = BattlePresentation.ReducedMotion;
            float shown = ResultShownSeconds;
            // 승리 포즈가 보이게 아주 옅게만 — 예전 결과 창은 0.3 딤 위에 가운데 판을 세워 포즈를 덮었다.
            UISurface.Dim(0.12f);
            BattleFeelDraw.VictoryTitle(f, sandbox ? BattleVictoryLayout.ChampionTitle : BattleVictoryLayout.Title, shown, reduced);
            if (!sandbox) DrawDuelResultQuoteAt(BattleVictoryLayout.QuoteRect(f));
            BattleFeelDraw.RewardPanel(f, VictoryLines(sandbox), shown);
            BattleFeelDraw.ContinueHint(f, ResultCanClose, shown - BattleResultRules.InputLockSeconds, reduced);
        }

        /// <summary>보상 줄 — 결과마다 한 번만 짓는다(마지막 상대 기준).</summary>
        private List<RewardLine> VictoryLines(bool sandbox)
        {
            if (victoryLines != null && ReferenceEquals(victoryLinesFor, enemyStats)) return victoryLines;
            victoryLinesFor = enemyStats;
            if (sandbox || battleController == null)
            {
                victoryLines = BattleRewardLines.Cheer(BattleRewardLines.DreamCheer);
                return victoryLines;
            }
            int itemCount = battleController.GetLastItemCount();
            string itemName = itemCount > 0 ? ResultItemName(battleController.GetLastItemId()) : null;
            string insect = enemyStats != null && enemyStats.Data != null ? enemyStats.Data.displayName : null;
            // 코인 0이면 줄을 만들지 않는다(샌드박스·패배).
            victoryLines = BattleRewardLines.Duel(battleController.GetLastCandyReward(), battleController.GetLastExpReward(),
                battleController.GetLastCoinReward(),
                itemName, itemCount, battleController.GetLastCaptureAttempted(), battleController.GetLastCaptureSucceeded(), insect);
            return victoryLines;
        }

        /// <summary>
        /// 아이템 ID → 표시명. 야생 승리 드랍은 재료(<c>InsectData.itemRewardId</c> — 기본 <c>mat_leaf</c>)인데 재료는 아이템 데이터베이스에 없다 —
        /// 그 이름표는 도감(<c>DexScreenUI.GetItemDisplayName</c>, data-architect)에만 있어 여기서는 「재료」로 적는다(아이에게 ID를 보이지 않는다).
        /// </summary>
        private string ResultItemName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (resultItemDb == null) resultItemDb = Resources.Load<ItemDatabase>("ItemDatabase");
            if (resultItemDb == null) resultItemDb = ItemDatabase.CreateRuntimeDefault();
            ItemData data = resultItemDb != null ? resultItemDb.FindById(itemId) : null;
            if (data != null && !string.IsNullOrEmpty(data.displayName)) return data.displayName;
            return BattleRewardLines.FallbackItemName(itemId);
        }
    }
}
