using System.IO;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 원자적 파일 쓰기 헬퍼. 게임 종료 직전 크래시 시 부분 쓰기로 인한 세이브 파일 손상을 차단합니다.
    /// 패턴: .tmp 파일에 쓰고 atomic rename으로 원본 교체. File.Replace는 OS 레벨 atomic 보장.
    /// SaveService 7개(Progress/Candy/Currency/Item/InsectCollection/BattleTeam/Dex) + CloudSaveManager 캐시 공용.
    /// </summary>
    public static class AtomicFileWriter
    {
        public static void WriteAllText(string path, string content)
        {
            if (string.IsNullOrEmpty(path) || content == null) return;

            string tempPath = path + ".tmp";
            try
            {
                File.WriteAllText(tempPath, content);
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AtomicFileWriter] {path} 쓰기 실패: {e.Message}");
                // tmp 파일 잔존 시 정리
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }
    }
}
