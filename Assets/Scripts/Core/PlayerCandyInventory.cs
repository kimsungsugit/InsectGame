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
            if (amount <= 0)
            {
                return;
            }

            data.candies += amount;
            Save(data);
            CandyChanged?.Invoke(data.candies);
        }

        public bool SpendCandy(int amount)
        {
            if (amount <= 0 || data.candies < amount)
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

            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<PlayerCandyData>(json) ?? new PlayerCandyData();
        }

        private void Save(PlayerCandyData save)
        {
            string json = JsonUtility.ToJson(save, true);
            File.WriteAllText(GetPath(), json);
        }

        private string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.SaveFiles.PlayerCandies);
        }
    }
}
