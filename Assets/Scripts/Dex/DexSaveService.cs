using System.IO;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Dex
{
    public static class DexSaveService
    {
        public static DexSaveData Load()
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return new DexSaveData();
            }

            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<DexSaveData>(json) ?? new DexSaveData();
        }

        public static void Save(DexSaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetPath(), json);
        }

        private static string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.SaveFiles.DexSave);
        }
    }
}
