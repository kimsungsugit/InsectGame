namespace InsectGame.Core
{
    /// <summary>
    /// Firebase REST API 프로젝트 설정.
    /// Firebase Console에서 발급받은 값으로 교체하세요.
    /// </summary>
    public static class FirebaseConfig
    {
        // Firebase Console -> 프로젝트 설정 -> 일반 -> 웹 API 키
        public const string ApiKey = "YOUR_FIREBASE_API_KEY";

        // Firebase Console -> 프로젝트 설정 -> 일반 -> 프로젝트 ID
        public const string ProjectId = "YOUR_PROJECT_ID";

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
