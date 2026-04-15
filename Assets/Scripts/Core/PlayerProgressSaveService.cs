using System.IO;
using UnityEngine;

namespace InsectGame.Core
{
    public static class PlayerProgressSaveService
    {


        public static PlayerProgressData Load()
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return new PlayerProgressData();
            }

            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<PlayerProgressData>(json) ?? new PlayerProgressData();
        }

        public static void Save(PlayerProgressData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetPath(), json);
        }

        private static string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.SaveFiles.PlayerProgress);
        }
    }
}
