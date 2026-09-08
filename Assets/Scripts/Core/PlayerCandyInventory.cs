using System;
using System.IO;
using UnityEngine;

namespace InsectGame.Core
{
    [Serializable]
    public class PlayerCandyData
    {
        public int candies = 0;
    }

    public class PlayerCandyInventory : MonoBehaviour
    {
        private PlayerCandyData data;

        public int Candies => data != null ? data.candies : 0;

        public event Action<int> CandyChanged;

        private void Awake()
        {
            data = Load();
        }

        public void AddCandy(int amount)
        {
            // data null 가드 — Awake 실패/순서 race 시 NRE 차단. Candies 프로퍼티(line 17)와 비대칭 회귀.
            if (amount <= 0 || data == null)
            {
                return;
            }

            data.candies += amount;
            Save(data);
            CandyChanged?.Invoke(data.candies);
        }

        // 클라우드 로드 값 적용 — AddCandy는 증분이라 절대값 세팅용 별도 메서드.
        public void SetCandies(int amount)
        {
            if (data == null) return;
            data.candies = Mathf.Max(0, amount);
            Save(data);
            CandyChanged?.Invoke(data.candies);
        }

        // 로그인/계정 전환 후 계정별 파일에서 재로드 — 부트 시 전역(UserId=null) 로드분 교정.
        public void ReloadFromDisk()
        {
            data = Load();
            CandyChanged?.Invoke(Candies);
        }

        public bool SpendCandy(int amount)
        {
            if (amount <= 0 || data == null || data.candies < amount)
            {
                return false;
            }

            data.candies -= amount;
            Save(data);
            CandyChanged?.Invoke(data.candies);
            return true;
        }

        private PlayerCandyData Load()
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return new PlayerCandyData();
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<PlayerCandyData>(json) ?? new PlayerCandyData();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerCandyInventory] 손상된 세이브 — 기본값으로 시작: {e.Message}");
                return new PlayerCandyData();
            }
        }

        private void Save(PlayerCandyData save)
        {
            // PlayerProgressSaveService와 일관성 — null 진입 시 빈 파일 쓰기 차단.
            if (save == null) return;
            string json = JsonUtility.ToJson(save, true);
            AtomicFileWriter.WriteAllText(GetPath(), json);
        }

        private string GetPath()
        {
            return SaveScope.FilePath(GameConstants.SaveFiles.PlayerCandies);
        }
    }
}
