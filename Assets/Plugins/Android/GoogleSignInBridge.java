package com.insectexploration.auth;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Android Credential Manager와 Unity AuthManager 사이의 최소 브리지.
 * Firebase 로그인은 C# REST 경로가 담당하고 이 클래스는 Google ID Token만 반환한다.
 */
public final class GoogleSignInBridge {
    private static final ExecutorService EXECUTOR = Executors.newSingleThreadExecutor();

    public interface Callback {
        void onSuccess(String idToken);
        void onError(String error);
    }

    private GoogleSignInBridge() {
    }

    public static void signIn(
            final Activity activity,
            final String serverClientId,
            final Callback callback) {
        if (activity == null) {
            callback.onError("Google 로그인 화면을 열 수 없습니다.");
            return;
        }
        if (serverClientId == null || serverClientId.trim().isEmpty()) {
            callback.onError("Google 웹 클라이언트 ID가 없습니다.");
            return;
        }

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                launchCredentialManager(activity, serverClientId, callback);
            }
        });
    }

    private static void launchCredentialManager(
            final Activity activity,
            String serverClientId,
            final Callback callback) {
        CredentialManager credentialManager = CredentialManager.create(activity);
        GetGoogleIdOption googleIdOption = new GetGoogleIdOption.Builder()
                .setFilterByAuthorizedAccounts(false)
                .setServerClientId(serverClientId)
                .build();
        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(googleIdOption)
                .build();

        credentialManager.getCredentialAsync(
                activity,
                request,
                new CancellationSignal(),
                EXECUTOR,
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse response) {
                        handleCredential(response.getCredential(), callback);
                    }

                    @Override
                    public void onError(GetCredentialException error) {
                        String message = error.getLocalizedMessage();
                        callback.onError(message == null || message.trim().isEmpty()
                                ? "Google 로그인이 취소되었습니다."
                                : "Google 로그인 실패: " + message);
                    }
                });
    }

    private static void handleCredential(Credential credential, Callback callback) {
        if (!(credential instanceof CustomCredential)
                || !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL
                .equals(credential.getType())) {
            callback.onError("Google 계정 인증 정보를 받지 못했습니다.");
            return;
        }

        try {
            CustomCredential customCredential = (CustomCredential) credential;
            GoogleIdTokenCredential tokenCredential =
                    GoogleIdTokenCredential.createFrom(customCredential.getData());
            callback.onSuccess(tokenCredential.getIdToken());
        } catch (Exception error) {
            callback.onError("Google ID 토큰 처리 실패: " + error.getMessage());
        }
    }
}
