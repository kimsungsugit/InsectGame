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

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<PlayerProgressData>(json) ?? new PlayerProgressData();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerProgressSaveService] 손상된 세이브 — 기본값으로 시작: {e.Message}");
                return new PlayerProgressData();
            }
        }

        public static void Save(PlayerProgressData data)
        {
            // null 거부 — static service는 임의 호출자가 진입 가능.
            // 옛은 JsonUtility.ToJson(null)이 빈 문자열 → WriteAllText로 빈 파일 → 다음 Load에서 데이터 손실.
            if (data == null)
            {
                Debug.LogWarning("[PlayerProgressSaveService] Save(null) 호출 무시 — 빈 파일 쓰기 차단");
                return;
            }

            string json = JsonUtility.ToJson(data, true);
            AtomicFileWriter.WriteAllText(GetPath(), json);
        }

        private static string GetPath()
        {
            return SaveScope.FilePath(GameConstants.SaveFiles.PlayerProgress);
        }
    }
}
