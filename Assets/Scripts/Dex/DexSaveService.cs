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

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<DexSaveData>(json) ?? new DexSaveData();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DexSaveService] 손상된 세이브 — 기본값으로 시작: {e.Message}");
                return new DexSaveData();
            }
        }

        public static void Save(DexSaveData data)
        {
            if (data == null) return; // 다른 SaveService와 일관성
            string json = JsonUtility.ToJson(data, true);
            InsectGame.Core.AtomicFileWriter.WriteAllText(GetPath(), json);
        }

        private static string GetPath()
        {
            return InsectGame.Core.SaveScope.FilePath(GameConstants.SaveFiles.DexSave);
        }
    }
}
