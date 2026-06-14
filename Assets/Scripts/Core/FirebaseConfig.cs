using System;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// Firebase REST API 프로젝트 설정.
    ///
    /// 실제 키는 소스에 커밋하지 말고 <c>Assets/Resources/firebase_config.json</c> 에 넣으세요(.gitignore 권장):
    /// <code>{ "apiKey": "AIza...", "projectId": "my-project-id" }</code>
    /// 파일이 없으면 아래 플레이스홀더가 쓰이고 <see cref="IsConfigured"/> 가 false → 클라우드/소셜 비활성.
    /// </summary>
    public static class FirebaseConfig
    {
        private const string PlaceholderApiKey = "YOUR_FIREBASE_API_KEY";
        private const string PlaceholderProjectId = "YOUR_PROJECT_ID";

        private static string apiKey = PlaceholderApiKey;
        private static string projectId = PlaceholderProjectId;
        private static bool loaded;

        // Firebase Console -> 프로젝트 설정 -> 일반 -> 웹 API 키
        public static string ApiKey { get { EnsureLoaded(); return apiKey; } }

        // Firebase Console -> 프로젝트 설정 -> 일반 -> 프로젝트 ID
        public static string ProjectId { get { EnsureLoaded(); return projectId; } }

        /// <summary>실제 키가 주입돼 Firebase 호출이 가능한 상태인지.</summary>
        public static bool IsConfigured
        {
            get
            {
                EnsureLoaded();
                return !string.IsNullOrEmpty(apiKey) && apiKey != PlaceholderApiKey
                    && !string.IsNullOrEmpty(projectId) && projectId != PlaceholderProjectId;
            }
        }

        [Serializable]
        private class ConfigJson
        {
            public string apiKey;
            public string projectId;
        }

        // Resources/firebase_config.json 에서 키 로드(1회). 없거나 손상돼도 플레이스홀더 유지.
        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            TextAsset ta = Resources.Load<TextAsset>("firebase_config");
            if (ta == null || string.IsNullOrEmpty(ta.text)) return;
            try
            {
                ConfigJson c = JsonUtility.FromJson<ConfigJson>(ta.text);
                if (c != null)
                {
                    if (!string.IsNullOrEmpty(c.apiKey)) apiKey = c.apiKey;
                    if (!string.IsNullOrEmpty(c.projectId)) projectId = c.projectId;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FirebaseConfig] firebase_config.json 파싱 실패 — 플레이스홀더 사용: " + e.Message);
            }
        }

        // Firestore REST API base URL
        public static string FirestoreBaseUrl =>
            $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";

        // Firebase Auth REST API URLs
        public const string SignUpUrl =
            "https://identitytoolkit.googleapis.com/v1/accounts:signUp";
        public const string SignInUrl =
            "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword";
        public const string SignInWithIdpUrl =
            "https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp";
        public const string SignInWithCustomTokenUrl =
            "https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken";
        public const string UpdateAccountUrl =
            "https://identitytoolkit.googleapis.com/v1/accounts:update";
        public const string DeleteAccountUrl =
            "https://identitytoolkit.googleapis.com/v1/accounts:delete";
        public const string RefreshTokenUrl =
            "https://securetoken.googleapis.com/v1/token";
        public const string GetUserUrl =
            "https://identitytoolkit.googleapis.com/v1/accounts:lookup";

        public static string WithKey(string url)
        {
            return $"{url}?key={ApiKey}";
        }
    }
}
