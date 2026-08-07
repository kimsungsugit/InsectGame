using System;
using UnityEngine;

namespace InsectGame.Core
{
    public class PlayerProgressController : MonoBehaviour
    {
        // 2막(ver2) 6지역이 Lv.42~70 구간을 쓴다. 1막만 있던 시절의 50은 mountain 수문장(45)에
        // 이미 붙어 있어 신규 지역을 얹을 여유가 없었다. XP 곡선은 선형(50 + 15×(L-1))이라
        // 상한을 올려도 후반이 지수로 폭발하지 않는다.
        [SerializeField] private int maxLevel = 80;
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

        // 로그인/계정 전환 후 계정별 파일에서 재로드 — 부트 시 전역(UserId=null) 로드분 교정.
        public void ReloadFromDisk()
        {
            data = PlayerProgressSaveService.Load();
            if (data.level < 1) data.level = 1;
            if (data.level > maxLevel) { data.level = maxLevel; data.currentXp = 0; }
            if (data.currentXp < 0) data.currentXp = 0;
            ProgressChanged?.Invoke(data);
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

        // 클라우드 로드 값 적용 — GainXp는 증분 누적이라 절대값 세팅이 불가능.
        // level/xp를 직접 설정하되 손상값 방어(클램프) 후 저장·이벤트 발화.
        public void ApplyCloudProgress(int level, int xp)
        {
            if (data == null) return;
            data.level = Mathf.Clamp(level, 1, maxLevel);
            data.currentXp = Mathf.Max(0, xp);
            if (data.level >= maxLevel) data.currentXp = 0;
            PlayerProgressSaveService.Save(data);
            ProgressChanged?.Invoke(data);
        }

        private int GetXpToNextLevel(int level)
        {
            return Mathf.Max(10, baseXpToLevel + (level - 1) * xpGrowthPerLevel);
        }
    }
}
