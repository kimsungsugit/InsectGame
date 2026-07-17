using System.IO;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 계정별(계정 uid) 로컬 저장 스코핑 단일 출처. 같은 기기에서 여러 계정을 써도 곤충/아이템/도감/팀/재화/진행
    /// 등 로컬 데이터가 섞이지 않도록, 저장 파일과 PlayerPrefs 키를 현재 로그인 uid로 분리한다.
    ///
    /// - 파일: persistentDataPath/users/&lt;uid&gt;/&lt;file&gt;  (비로그인 시 전역 persistentDataPath 폴백)
    /// - PlayerPrefs: baseKey + "." + uid  (AuthManager.ScopedKey 위임)
    ///
    /// 모든 *SaveService/*Inventory.GetPath()와 CloudSaveManager의 LoadLocalFile/SaveLocalFile이 이 헬퍼를 경유한다.
    /// </summary>
    public static class SaveScope
    {
        // 마이그레이션/정리 대상 PlayerPrefs 베이스 키 — 실제 계정별로 스코핑하는 키.
        // 퀘스트 3종 + 지역/가디언 + 외형/캐릭터. (player_level/xp/candies/coins 미러와 InsectGame.Gems는
        // 파일이 단일 출처라 전역 유지.)
        // String 타입 PlayerPrefs 베이스 키 (SetString/GetString으로 저장됨).
        private static readonly string[] ScopedStringPrefsKeys =
        {
            GameConstants.PrefsKeys.QuestProgress,
            GameConstants.PrefsKeys.QuestCompleted,
            GameConstants.PrefsKeys.ActiveQuest,
            "InsectGame.UnlockedRegions",
            "InsectGame.DefeatedGuardians",
            "InsectGame.Equipped",
            "InsectGame.OwnedOutfits",
            "InsectGame.Character.Name",
        };

        // Int 타입 PlayerPrefs 베이스 키 (SetInt/GetInt로 저장됨). 마이그레이션 시 반드시 GetInt/SetInt로
        // 복사해야 함 — GetString으로 복사하면 ""가 되어 GetInt 판독 사이트(PlayerVisualBuilder 등)가
        // 0으로 읽어 캐릭터 외형이 영구 초기화됨.
        private static readonly string[] ScopedIntPrefsKeys =
        {
            "InsectGame.Character.Created",
            "InsectGame.Character.SkinColor",
            "InsectGame.Character.HairStyle",
            "InsectGame.Character.Gender",
            "InsectGame.Character.HairColor",
            "InsectGame.Character.FaceType",
            "InsectGame.Character.OutfitPreset",
        };

        // 마이그레이션 버전 — 스코핑 대상 키를 늘리거나 복사 로직을 고칠 때마다 +1. 기존 소유자도 1회 재이전.
        // v3: int형 캐릭터 외형 키를 GetString→GetInt 복사로 수정 (외형 초기화 버그).
        private const int MigrationVersion = 3;

        private static readonly string[] ScopedFiles =
        {
            GameConstants.SaveFiles.PlayerProgress,
            GameConstants.SaveFiles.PlayerInsects,
            GameConstants.SaveFiles.PlayerCandies,
            GameConstants.SaveFiles.PlayerCurrency,
            GameConstants.SaveFiles.PlayerItems,
            GameConstants.SaveFiles.BattleTeam,
            GameConstants.SaveFiles.DexSave,
        };

        private const string LocalOwnerKey = "InsectGame.LocalOwnerUid";

        private static string CurrentUid =>
            AuthManager.Instance != null ? AuthManager.Instance.UserId : null;

        /// <summary>계정별 저장 파일 경로. 비로그인 시 전역 경로로 폴백.</summary>
        public static string FilePath(string fileName)
        {
            string uid = CurrentUid;
            if (string.IsNullOrEmpty(uid))
                return Path.Combine(Application.persistentDataPath, fileName);

            string dir = Path.Combine(Application.persistentDataPath, "users", Sanitize(uid));
            Directory.CreateDirectory(dir); // 멱등 — 없으면 생성
            return Path.Combine(dir, fileName);
        }

        /// <summary>계정별 PlayerPrefs 키.</summary>
        public static string PrefsKey(string baseKey) => AuthManager.ScopedKey(baseKey);

        /// <summary>
        /// 전역(레거시) 로컬 데이터를 현재 계정의 계정별 위치로 1회 이전. 데이터 손실/오염 방지를 위해
        /// "이 계정이 전역 로컬 데이터의 소유자(LocalOwnerKey==uid)"일 때만 수행한다. 비소유 계정은
        /// 전역 데이터를 건드리지 않고 빈 상태로 시작(클라우드 복원 또는 신규).
        /// </summary>
        public static void MigrateLegacyIfOwned()
        {
            string uid = CurrentUid;
            if (string.IsNullOrEmpty(uid)) return;

            string flagKey = "InsectGame.MigratedVer." + uid;
            if (PlayerPrefs.GetInt(flagKey, 0) >= MigrationVersion) return; // 이미 최신 버전까지 이전됨

            string owner = PlayerPrefs.GetString(LocalOwnerKey, "");
            bool owned = owner == uid;

            if (owned)
            {
                // 파일 이전(이동) — 계정별 경로에 이미 있으면 건너뜀.
                foreach (string file in ScopedFiles)
                {
                    string global = Path.Combine(Application.persistentDataPath, file);
                    string scoped = FilePath(file);
                    if (global == scoped) continue;
                    try
                    {
                        if (File.Exists(global) && !File.Exists(scoped))
                            File.Move(global, scoped);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[SaveScope] 파일 이전 실패(" + file + "): " + e.Message);
                    }
                }

                // PlayerPrefs 이전(복사) — 계정별 키가 없을 때만. String/Int 타입을 구분해 복사한다.
                foreach (string baseKey in ScopedStringPrefsKeys)
                {
                    string scopedKey = PrefsKey(baseKey);
                    if (scopedKey == baseKey) continue;
                    if (PlayerPrefs.HasKey(scopedKey)) continue;
                    if (PlayerPrefs.HasKey(baseKey))
                        PlayerPrefs.SetString(scopedKey, PlayerPrefs.GetString(baseKey, ""));
                }
                foreach (string baseKey in ScopedIntPrefsKeys)
                {
                    string scopedKey = PrefsKey(baseKey);
                    if (scopedKey == baseKey) continue;
                    if (PlayerPrefs.HasKey(scopedKey)) continue;
                    if (PlayerPrefs.HasKey(baseKey))
                        PlayerPrefs.SetInt(scopedKey, PlayerPrefs.GetInt(baseKey, 0));
                }

                // 이전 완료한 소유자만 현재 버전으로 플래그를 찍는다. 비소유 계정은 플래그를 안 찍어,
                // 이후(클라우드 저장으로 LocalOwner가 이 계정이 되면) 재평가될 수 있게 한다.
                PlayerPrefs.SetInt(flagKey, MigrationVersion);
                PlayerPrefs.Save();
            }
        }

        /// <summary>현재 계정의 모든 계정별 로컬 데이터(파일+PlayerPrefs) 삭제. 계정 삭제/마스터 정리용.</summary>
        public static void ClearCurrentAccountLocal()
        {
            foreach (string file in ScopedFiles)
            {
                try { if (File.Exists(FilePath(file))) File.Delete(FilePath(file)); }
                catch (System.Exception e) { Debug.LogWarning("[SaveScope] 파일 삭제 실패(" + file + "): " + e.Message); }
            }
            foreach (string baseKey in ScopedStringPrefsKeys)
                PlayerPrefs.DeleteKey(PrefsKey(baseKey));
            foreach (string baseKey in ScopedIntPrefsKeys)
                PlayerPrefs.DeleteKey(PrefsKey(baseKey));
            PlayerPrefs.Save();
        }

        // 파일 경로 안전화 — uid에 경로 구분자/제어문자가 들어가도 디렉터리명으로 안전하게.
        private static string Sanitize(string uid)
        {
            var sb = new System.Text.StringBuilder(uid.Length);
            foreach (char c in uid)
                sb.Append((char.IsLetterOrDigit(c) || c == '_' || c == '-') ? c : '_');
            return sb.ToString();
        }
    }
}
