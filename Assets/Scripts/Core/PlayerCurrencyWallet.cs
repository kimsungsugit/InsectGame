using System;
using System.IO;
using UnityEngine;

namespace InsectGame.Core
{
    [Serializable]
    public class PlayerCurrencyData
    {
        public int gems = 0;
        public int coins = 0;
    }

    public class PlayerCurrencyWallet : MonoBehaviour
    {
        private PlayerCurrencyData data;

        public int Gems => data != null ? data.gems : 0;
        public int Coins => data != null ? data.coins : 0;

        public event Action<PlayerCurrencyData> CurrencyChanged;

        private void Awake()
        {
            data = Load();
        }

        public void AddGems(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            data.gems += amount;
            Save();
        }

        public bool SpendGems(int amount)
        {
            if (amount <= 0 || data.gems < amount)
            {
                return false;
            }

            data.gems -= amount;
            Save();
            return true;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            data.coins += amount;
            Save();
        }

        public bool SpendCoins(int amount)
        {
            if (amount <= 0 || data.coins < amount)
            {
                return false;
            }

            data.coins -= amount;
            Save();
            return true;
        }

        private void Save()
        {
            File.WriteAllText(GetPath(), JsonUtility.ToJson(data, true));
            CurrencyChanged?.Invoke(data);
        }

        private PlayerCurrencyData Load()
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return new PlayerCurrencyData();
            }

            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<PlayerCurrencyData>(json) ?? new PlayerCurrencyData();
        }

        private string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.SaveFiles.PlayerCurrency);
        }
    }
}
