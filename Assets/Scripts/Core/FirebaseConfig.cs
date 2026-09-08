using System;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// Firebase REST API 프로젝트 설정.
    ///
    /// 실제 키는 소스에 커밋하지 말고 <c>Assets/Resources/firebase_config.json</c> 에 넣으세요(.gitignore 권장):
    /// <code>{ "apiKey": "AIza...", "projectId": "my-project-id",
    /// "googleWebClientId": "...apps.googleusercontent.com",
    /// "purchaseVerificationUrl": "https://...cloudfunctions.net/verifyGooglePlayPurchase",
    /// "socialPvpApiUrl": "https://...cloudfunctions.net/socialPvpApi",
    /// "privacyPolicyUrl": "https://example.com/privacy" }</code>
    /// 파일이 없으면 아래 플레이스홀더가 쓰이고 <see cref="IsConfigured"/> 가 false → 클라우드/소셜 비활성.
    /// </summary>
    public static class FirebaseConfig
    {
        private const string PlaceholderApiKey = "YOUR_FIREBASE_API_KEY";
        private const string PlaceholderProjectId = "YOUR_PROJECT_ID";
        private const string PlaceholderGoogleWebClientId = "YOUR_GOOGLE_WEB_CLIENT_ID";
        private const string PlaceholderPurchaseVerificationUrl = "YOUR_PURCHASE_VERIFICATION_URL";
        private const string PlaceholderSocialPvpApiUrl = "YOUR_SOCIAL_PVP_API_URL";
        private const string PlaceholderPrivacyPolicyUrl = "YOUR_PRIVACY_POLICY_URL";

        private static string apiKey = PlaceholderApiKey;
        private static string projectId = PlaceholderProjectId;
        private static string googleWebClientId = PlaceholderGoogleWebClientId;
        private static string purchaseVerificationUrl = PlaceholderPurchaseVerificationUrl;
        private static string socialPvpApiUrl = PlaceholderSocialPvpApiUrl;
        private static string privacyPolicyUrl = PlaceholderPrivacyPolicyUrl;
        private static bool loaded;

        // Firebase Console -> 프로젝트 설정 -> 일반 -> 웹 API 키
        public static string ApiKey { get { EnsureLoaded(); return apiKey; } }

        // Firebase Console -> 프로젝트 설정 -> 일반 -> 프로젝트 ID
        public static string ProjectId { get { EnsureLoaded(); return projectId; } }

        // Credential Manager가 Google ID Token을 발급할 때 사용하는 웹 OAuth 클라이언트 ID.
        // Android 클라이언트 ID가 아니라 client_type=3 값을 사용해야 합니다.
        public static string GoogleWebClientId
        {
            get { EnsureLoaded(); return googleWebClientId; }
        }

        // Firebase Functions verifyGooglePlayPurchase HTTPS 엔드포인트.
        public static string PurchaseVerificationUrl
        {
            get
            {
                EnsureLoaded();
                if (!string.IsNullOrEmpty(purchaseVerificationUrl)
                    && purchaseVerificationUrl != PlaceholderPurchaseVerificationUrl)
                {
                    return purchaseVerificationUrl;
                }

                if (!string.IsNullOrEmpty(projectId) && projectId != PlaceholderProjectId)
                {
                    return $"https://asia-northeast3-{projectId}.cloudfunctions.net/verifyGooglePlayPurchase";
                }

                return purchaseVerificationUrl;
            }
        }

        public static string SocialPvpApiUrl
        {
            get
            {
                EnsureLoaded();
                if (!string.IsNullOrEmpty(socialPvpApiUrl)
                    && socialPvpApiUrl != PlaceholderSocialPvpApiUrl)
                    return socialPvpApiUrl;

                if (!string.IsNullOrEmpty(projectId) && projectId != PlaceholderProjectId)
                    return $"https://asia-northeast3-{projectId}.cloudfunctions.net/socialPvpApi";

                return socialPvpApiUrl;
            }
        }

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

        public static bool IsGoogleConfigured
        {
            get
            {
                EnsureLoaded();
                return IsConfigured
                    && !string.IsNullOrEmpty(googleWebClientId)
                    && googleWebClientId != PlaceholderGoogleWebClientId;
            }
        }

        public static bool IsPurchaseVerificationConfigured
        {
            get
            {
                string url = PurchaseVerificationUrl;
                return IsConfigured
                    && !string.IsNullOrEmpty(url)
                    && url != PlaceholderPurchaseVerificationUrl
                    && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsSocialPvpConfigured
        {
            get
            {
                string url = SocialPvpApiUrl;
                return IsConfigured && !string.IsNullOrEmpty(url)
                    && url != PlaceholderSocialPvpApiUrl
                    && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string PrivacyPolicyUrl
        {
            get { EnsureLoaded(); return privacyPolicyUrl; }
        }

        public static bool IsPrivacyPolicyConfigured
        {
            get
            {
                string url = PrivacyPolicyUrl;
                return !string.IsNullOrEmpty(url)
                    && url != PlaceholderPrivacyPolicyUrl
                    && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            }
        }

        [Serializable]
        private class ConfigJson
        {
            public string apiKey;
            public string projectId;
            public string googleWebClientId;
            public string purchaseVerificationUrl;
            public string socialPvpApiUrl;
            public string privacyPolicyUrl;
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
                    if (!string.IsNullOrEmpty(c.googleWebClientId))
                        googleWebClientId = c.googleWebClientId;
                    if (!string.IsNullOrEmpty(c.purchaseVerificationUrl))
                        purchaseVerificationUrl = c.purchaseVerificationUrl;
                    if (!string.IsNullOrEmpty(c.socialPvpApiUrl))
                        socialPvpApiUrl = c.socialPvpApiUrl;
                    if (!string.IsNullOrEmpty(c.privacyPolicyUrl))
                        privacyPolicyUrl = c.privacyPolicyUrl;
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
