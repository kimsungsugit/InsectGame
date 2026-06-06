using System;
using UnityEngine;

namespace InsectGame.Core
{
    public class PlayerProgressController : MonoBehaviour
    {
        [SerializeField] private int maxLevel = 50;
        [SerializeField] private int baseXpToLevel = 50;
        [SerializeField] private int xpGrowthPerLevel = 15;

        private PlayerProgressData data;

        public int Level => data != null ? data.level : 1;
        public int CurrentXp => data != null ? data.currentXp : 0;
        public int XpToNextLevel => GetXpToNextLevel(Level);

        public event Action<PlayerProgressData> ProgressChanged;

        private void Awake()
        {
            data = PlayerProgressSaveService.Load();

            // 손상 세이브 sanitize — UI/배틀 시스템에 음수/0 level이나 음수 XP 진입 차단.
            // 옛은 외부 JSON 직접 편집 또는 손상 파일 → GetXpToNextLevel 음수, UI Lv 0 표시 등 회귀.
            bool dirty = false;
            if (data.level < 1) { data.level = 1; dirty = true; }
            if (data.level > maxLevel) { data.level = maxLevel; data.currentXp = 0; dirty = true; }
            if (data.currentXp < 0) { data.currentXp = 0; dirty = true; }
            if (dirty) PlayerProgressSaveService.Save(data);
        }

        public void GainXp(int amount)
        {
            if (data == null || amount <= 0)
            {
                return;
            }

            // 만렙 도달 후 GainXp 호출은 잉여 XP 누적 차단 — UI 표시 일관성.
            // 옛은 currentXp 무한 누적, 디스크 쓰기/SaveCloud 무의미하게 발생.
            if (data.level >= maxLevel)
            {
                if (data.currentXp != 0)
                {
                    data.currentXp = 0;
                    PlayerProgressSaveService.Save(data);
                    ProgressChanged?.Invoke(data);
                }
                return;
            }

            int levelBefore = data.level;
            data.currentXp += amount;
            while (data.level < maxLevel && data.currentXp >= GetXpToNextLevel(data.level))
            {
                data.currentXp -= GetXpToNextLevel(data.level);
                data.level++;
            }

            // 만렙 도달 시 잉여 XP 클램프 — Lv max / XP 0 일관성.
            if (data.level >= maxLevel)
            {
                data.currentXp = 0;
            }

            PlayerProgressSaveService.Save(data);
            ProgressChanged?.Invoke(data);

            // 레벨업은 중요한 진행이라 자동저장 120초 대기 안 하고 즉시 클라우드 동기화
            // (앱 강제 종료 시 레벨업 손실 방지)
            if (data.level > levelBefore && CloudSaveManager.Instance != null)
                CloudSaveManager.Instance.SaveToCloud();
        }

        private int GetXpToNextLevel(int level)
        {
            return Mathf.Max(10, baseXpToLevel + (level - 1) * xpGrowthPerLevel);
        }
    }
}
