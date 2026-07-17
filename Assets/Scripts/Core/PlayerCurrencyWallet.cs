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
            // data null 가드 — Gems/Coins 프로퍼티(line 18-19)와 비대칭, Awake 실패 시 NRE 차단.
            if (amount <= 0 || data == null)
            {
                return;
            }

            data.gems += amount;
            Save();
        }

        public bool SpendGems(int amount)
        {
            if (amount <= 0 || data == null || data.gems < amount)
            {
                return false;
            }

            data.gems -= amount;
            Save();
            return true;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0 || data == null)
            {
                return;
            }

            data.coins += amount;
            Save();
        }

        // 클라우드 로드 값 적용 — AddCoins/AddGems는 증분이라 절대값 세팅용 별도 메서드.
        public void SetCoins(int amount)
        {
            if (data == null) return;
            data.coins = Mathf.Max(0, amount);
            Save();
        }

        public void SetGems(int amount)
        {
            if (data == null) return;
            data.gems = Mathf.Max(0, amount);
            Save();
        }

        public bool SpendCoins(int amount)
        {
            if (amount <= 0 || data == null || data.coins < amount)
            {
                return false;
            }

            data.coins -= amount;
            Save();
            return true;
        }

        // 로그인/계정 전환 후 계정별 파일에서 재로드 — 부트 시 전역(UserId=null) 로드분 교정.
        public void ReloadFromDisk()
        {
            data = Load();
            CurrencyChanged?.Invoke(data);
        }

        private void Save()
        {
            // data null 가드 — 빈 파일 쓰기 + 구독자에게 null 전파 차단.
            if (data == null) return;
            AtomicFileWriter.WriteAllText(GetPath(), JsonUtility.ToJson(data, true));
            CurrencyChanged?.Invoke(data);
        }

        private PlayerCurrencyData Load()
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return new PlayerCurrencyData();
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<PlayerCurrencyData>(json) ?? new PlayerCurrencyData();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerCurrencyWallet] 손상된 세이브 — 기본값으로 시작: {e.Message}");
                return new PlayerCurrencyData();
            }
        }

        private string GetPath()
        {
            return SaveScope.FilePath(GameConstants.SaveFiles.PlayerCurrency);
        }
    }
}
