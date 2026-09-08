using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 개발/테스트용 마스터(관리자) 계정.
    ///
    /// 보안 설계:
    ///  1) 자격 증명(아이디/비번)을 소스에 하드코딩하지 않고 <c>Resources/master_config.json</c> 에서
    ///     로드한다(.gitignore 권장). 파일 형식: <code>{ "id": "...", "password": "..." }</code>
    ///  2) 마스터 비교 로직 자체를 <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c> 로 감싸 프로덕션(릴리스)
    ///     빌드에는 컴파일되지 않는다 → 릴리스 바이너리에 백도어/자격 증명 코드가 존재하지 않음.
    ///
    /// 주의: 프로덕션 빌드 시 <c>Resources/master_config.json</c> 도 함께 제외하라(Resources는 빌드에 번들됨).
    /// 코드가 빠져 기능은 죽지만, 파일이 남으면 자격 증명 평문이 추출 가능하다.
    /// </summary>
    public static class MasterAccount
    {
        // 식별자/토큰은 비밀이 아닌 sentinel(세션 표시용) — 하드코딩 유지.
        public const string Uid = "master_admin_001";
        public const string Token = "master_token";
        public const string RefreshToken = "master_refresh";

        /// <summary>마스터 로그인 코드가 이 빌드에 포함되는지(에디터/개발 빌드만 true, 프로덕션 false).</summary>
        public static bool IsEnabled
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            get { return true; }
#else
            get { return false; }
#endif
        }

        /// <summary>입력 자격 증명이 설정된 마스터 계정과 일치하는지.
        /// 프로덕션 빌드에서는 코드가 컴파일되지 않아 항상 false.</summary>
        public static bool TryMatch(string id, string password)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureLoaded();
            return !string.IsNullOrEmpty(masterId)
                && id == masterId && password == masterPw;
#else
            return false;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string masterId;
        private static string masterPw;
        private static bool loaded;

        [System.Serializable]
        private class Cfg { public string id; public string password; }

        // Resources/master_config.json 에서 1회 로드. 파일이 없거나 손상되면 마스터 로그인 비활성.
        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            TextAsset ta = Resources.Load<TextAsset>("master_config");
            if (ta == null || string.IsNullOrEmpty(ta.text)) return;
            try
            {
                Cfg c = JsonUtility.FromJson<Cfg>(ta.text);
                if (c != null)
                {
                    masterId = c.id;
                    masterPw = c.password;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MasterAccount] master_config.json 파싱 실패 — 마스터 로그인 비활성: " + e.Message);
            }
        }
#endif
    }
}
