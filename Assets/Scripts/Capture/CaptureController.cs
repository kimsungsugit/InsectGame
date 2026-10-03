using System;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;
using InsectGame.Core;

namespace InsectGame.Capture
{
    public class CaptureController : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField]
        private float baseSuccessChance = CaptureChanceTuning.DefaultBaseSuccessChance;
        [Range(0f, 0.5f)] [SerializeField]
        private float rarityPenaltyStep = CaptureChanceTuning.DefaultRarityPenaltyStep;
        [Range(0f, 1f)] [SerializeField]
        private float difficultyPenaltyScale = CaptureChanceTuning.DefaultDifficultyPenaltyScale;
        [Range(0f, 0.3f)] [SerializeField]
        private float perfectTimingBonus = CaptureChanceTuning.DefaultPerfectTimingBonus;
        [Range(0f, 0.5f)] [SerializeField]
        private float timingWindow = CaptureChanceTuning.DefaultTimingWindow;
        [Header("Level Modifier")]
        [SerializeField]
        private float playerLevelBonusStep = CaptureChanceTuning.DefaultPlayerLevelBonusStep;
        [SerializeField]
        private float enemyLevelPenaltyStep = CaptureChanceTuning.DefaultEnemyLevelPenaltyStep;

        [SerializeField] private Dex.DexController dexController;
        [SerializeField] private PlayerProgressController playerProgress;
        [SerializeField] private PlayerInsectCollection insectCollection;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private ItemEffectManager itemEffects;
        [SerializeField] private OutfitBonusProvider outfitBonus;

        public event Action<InsectEntity, bool> CaptureResolved;

        /// <summary>
        /// 직전 포획에서 실제로 지급한 EXP·캔디(부스터 포함, 실패면 0). <see cref="CaptureResolved"/> 전에 채운다 —
        /// 팝업이 공식을 다시 돌리면 지급 뒤 캐릭터 레벨로 레벨 차를 재서 지급값과 갈린다.
        /// </summary>
        public int LastExpReward { get; private set; }
        public int LastCandyReward { get; private set; }

        /// <summary>
        /// 직전 포획이 <b>그 종의 첫 포획</b>이었는가 — 팝업이 「NEW」를 붙인다. 도감 등록 <b>전에</b> 재야 한다:
        /// 등록 뒤에 물으면 방금 올린 기록 때문에 늘 "이미 잡은 종"이다.
        /// </summary>
        public bool LastCaptureWasNewSpecies { get; private set; }

        /// <summary>포획 공식이 쓰는 캐릭터 레벨 — 포획 선택 화면이 레벨 차 경고를 같은 값으로 판단한다.</summary>
        public int TrainerLevel => playerProgress != null ? playerProgress.Level : 1;

        public void AttemptCapture(InsectEntity target, float timing01, float extraBonus = 0f)
        {
            if (target == null || target.Data == null)
            {
                return;
            }

            LastExpReward = 0;
            LastCandyReward = 0;
            LastCaptureWasNewSpecies = false;
            // 확률과 EXP가 같은 레벨 차를 보도록 지급 전에 고정한다(GainXp가 레벨을 올린다).
            int trainerLevel = TrainerLevel;
            float chance = CalculateSuccessChance(target.Data, target.Level, timing01, extraBonus);
            bool success = UnityEngine.Random.value <= chance;

            if (dexController != null)
            {
                dexController.RegisterEncounter(target.Data.insectId);
                if (success)
                {
                    LastCaptureWasNewSpecies = !dexController.TryGetRecord(target.Data.insectId, out Dex.DexRecord record)
                        || record == null || record.capturedCount <= 0;
                    dexController.RegisterCapture(target.Data.insectId);
                }
            }

            if (success)
            {
                if (playerProgress != null)
                {
                    int exp = InsectRewardCalculator.GetExpReward(target.Data, target.Level, trainerLevel);
                    float expMultiplier = (itemEffects != null ? itemEffects.GetExpMultiplier() : 1f)
                                        * (outfitBonus != null ? outfitBonus.GetExpMultiplier() : 1f);
                    LastExpReward = Mathf.RoundToInt(exp * expMultiplier);
                    playerProgress.GainXp(LastExpReward);
                }

                if (candyInventory != null)
                {
                    int candy = InsectRewardCalculator.GetCandyReward(target.Data);
                    float candyMultiplier = (itemEffects != null ? itemEffects.GetCandyMultiplier() : 1f)
                                           * (outfitBonus != null ? outfitBonus.GetCandyMultiplier() : 1f);
                    LastCandyReward = Mathf.RoundToInt(candy * candyMultiplier);
                    candyInventory.AddCandy(LastCandyReward);
                }
                // 필드에서 본 이로치(색다른 곤충)를 그대로 저장 — 옛 2-인자 호출은 isShiny=false라
                // 미니게임 포획 시 색다른 개체가 일반 개체로 유실됐음(배틀/레이드 경로는 정상 전달).
                // 반환된 개체는 조건부 퀘스트(몸길이·이로치)가 실제 저장된 값을 보도록 통지에 넘긴다.
                // 컬렉션이 없으면 null — 그래도 통지는 간다(CaptureFacts.From이 크기를 중간값으로 둔다).
                PlayerInsectData captured = insectCollection?.AddCapturedInsect(
                    target.Data.insectId, target.Level, target.IsShiny);

                // **퀘스트 통지는 이벤트가 아니라 여기서 한다.** 예전엔 `CaptureFeedbackController`
                // (효과음·팝업을 담당하는 연출 컴포넌트) 안에 있었는데, 그건 `CaptureResolved`의
                // 구독자 중 하나일 뿐이다. 멀티캐스트 델리게이트는 앞선 구독자가 던지면 **뒤를
                // 호출하지 않으므로**, `CapturePopupUI`가 먼저 등록된 상태에서 예외가 나면
                // 포획 퀘스트 진행이 경고 한 줄만 남기고 영구 유실된다.
                // 진행에 필수인 통지는 연출과 같은 배를 타면 안 된다(`InsectBattleController`가
                // 전투 경로에서 이미 같은 이유로 직접 부른다).
                TutorialQuestManager.Instance?.NotifyCapture(CaptureFacts.From(target.Data, captured, target.IsShiny));
            }

            // **지급이 끝난 뒤에 알린다.** 예전엔 이 호출이 맨 앞이라, 팝업이
            // `GetLatestOwnedBySpecies`로 방금 잡은 개체를 찾을 때 **아직 추가되기 전**이었다 —
            // 같은 종을 이미 갖고 있었다면 **직전 개체의 등급·개체값**이 뜨고, 그 종의 첫 포획이면
            // 조회가 비어 **이전 팝업의 값이 그대로 남았다**.
            // 핸들러 예외가 여기 아래의 Despawn을 막지 않도록 격리는 유지한다.
            try { CaptureResolved?.Invoke(target, success); }
            catch (System.Exception e) { Debug.LogWarning($"[CaptureController] CaptureResolved 핸들러 예외: {e.Message}"); }

            // 성공·실패 모두 Despawn — 사용자 의도("미니게임 끝나면 사라져야").
            // 옛은 실패 시 50% 확률 잔존이라 같은 곤충에 중첩 미니게임 발동 + 필드 중복 인스턴스 가능.
            target.Despawn();
        }

        private float CalculateSuccessChance(
            InsectData data,
            int insectLevel,
            float timing01,
            float minigameBonus)
        {
            int playerLevel = TrainerLevel;
            float activeItemBonus = itemEffects != null ? itemEffects.GetCaptureChanceBonus() : 0f;
            float equippedOutfitBonus = outfitBonus != null ? outfitBonus.GetCaptureChanceBonus() : 0f;
            CaptureChanceTuning tuning = new CaptureChanceTuning(
                baseSuccessChance,
                rarityPenaltyStep,
                difficultyPenaltyScale,
                perfectTimingBonus,
                timingWindow,
                playerLevelBonusStep,
                enemyLevelPenaltyStep);

            return CaptureChanceCalculator.Calculate(
                data.rarity,
                data.captureDifficulty,
                playerLevel,
                insectLevel,
                timing01,
                activeItemBonus,
                equippedOutfitBonus,
                minigameBonus,
                tuning);
        }

        public void AutoWire(Dex.DexController dex)
        {
            if (dexController == null)
            {
                dexController = dex;
            }
        }

        public void AutoWire(PlayerProgressController progress)
        {
            if (playerProgress == null)
            {
                playerProgress = progress;
            }
        }

        public void AutoWire(PlayerInsectCollection collection)
        {
            if (insectCollection == null)
            {
                insectCollection = collection;
            }
        }

        public void AutoWire(PlayerCandyInventory candy)
        {
            if (candyInventory == null)
            {
                candyInventory = candy;
            }
        }

        public void AutoWire(ItemEffectManager effects)
        {
            if (itemEffects == null)
            {
                itemEffects = effects;
            }
        }

        public void AutoWire(OutfitBonusProvider bonus)
        {
            if (outfitBonus == null)
            {
                outfitBonus = bonus;
            }
        }

        public void ApplyTuning(GameplayTuningProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            baseSuccessChance = Mathf.Clamp01(profile.baseSuccessChance);
            rarityPenaltyStep = Mathf.Clamp(profile.rarityPenaltyStep, 0f, 0.5f);
            difficultyPenaltyScale = Mathf.Clamp01(profile.difficultyPenaltyScale);
            perfectTimingBonus = Mathf.Clamp(profile.perfectTimingBonus, 0f, 0.5f);
            timingWindow = Mathf.Clamp(profile.timingWindow, 0f, 0.5f);
        }
    }
}
