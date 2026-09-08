using InsectGame.Core;
using InsectGame.Spawning;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InsectGame.Capture
{
    public class CaptureFeedbackController : MonoBehaviour
    {
        [SerializeField] private CaptureController captureController;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip successClip;
        [SerializeField] private AudioClip failClip;
        [SerializeField] private ParticleSystem successEffect;
        [SerializeField] private ParticleSystem failEffect;
        [SerializeField] private bool useVibration = true;
        [Header("Vibration")]
        [SerializeField] private long successVibrationMs = 40;
        [SerializeField] private long failVibrationMs = 120;
        [Header("Popup Text")]
        [SerializeField] private Text popupText;
        [SerializeField] private TMP_Text popupTextTmp;
        [SerializeField] private string successMessage = "포획 성공!";
        [SerializeField] private string failMessage = "포획 실패";
        [SerializeField] private float popupDuration = 1.2f;

        private float popupTimer;

        private void OnEnable()
        {
            if (captureController != null)
            {
                captureController.CaptureResolved += HandleCaptureResolved;
            }
        }

        private void OnDisable()
        {
            if (captureController != null)
            {
                captureController.CaptureResolved -= HandleCaptureResolved;
            }
        }

        private void Update()
        {
            if ((popupText == null && popupTextTmp == null) || popupDuration <= 0f)
            {
                return;
            }

            if (popupTimer > 0f)
            {
                popupTimer -= Time.deltaTime;
                if (popupTimer <= 0f)
                {
                    if (popupText != null)
                    {
                        popupText.enabled = false;
                    }
                    if (popupTextTmp != null)
                    {
                        popupTextTmp.enabled = false;
                    }
                }
            }
        }

        private void HandleCaptureResolved(InsectEntity target, bool success)
        {
            if (success)
            {
                PlayAudio(successClip);
                PlayEffect(successEffect);

                bool isShiny = target != null && target.IsShiny;
                // 지워진 개체를 잡는 것은 이름을 되찾아주는 일이다(2막 서사의 핵심 행위).
                // 이로치보다 우선해 알린다 — 둘이 겹치는 일은 드물고, 겹치면 이쪽이 더 특별하다.
                bool isErased = target != null && target.IsErased;
                ShowPopup(
                    isErased ? $"이름을 되찾아주었다 — {NameOf(target)}"
                    : isShiny ? "★ 색다른 곤충 포획! ★"
                    : successMessage);

                if (isShiny && AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(SfxType.Victory);

                // 퀘스트 통지는 여기 없다 — `CaptureController`가 직접 부른다.
                // 이 컴포넌트는 `CaptureResolved`의 구독자 중 하나일 뿐이라, 앞선 구독자가
                // 던지면 여기까지 오지 못해 진행이 유실됐다. 진행 통지를 연출 경로에 두지 않는다.
            }
            else
            {
                PlayAudio(failClip);
                PlayEffect(failEffect);
                ShowPopup(failMessage);
            }

            if (audioSource == null || (success && successClip == null) || (!success && failClip == null))
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(success ? SfxType.CaptureSuccess : SfxType.CaptureFail);
            }

            if (useVibration)
            {
                long duration = success ? successVibrationMs : failVibrationMs;
                TriggerVibration(duration);
            }
        }

        private void PlayAudio(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void PlayEffect(ParticleSystem effect)
        {
            if (effect != null)
            {
                effect.Play();
            }
        }

        private static string NameOf(InsectEntity target)
            => target != null && target.Data != null && !string.IsNullOrEmpty(target.Data.displayName)
                ? target.Data.displayName : "이름 없는 것";

        private void ShowPopup(string message)
        {
            if (popupText == null && popupTextTmp == null)
            {
                return;
            }

            if (popupText != null)
            {
                popupText.text = message;
                popupText.enabled = true;
            }

            if (popupTextTmp != null)
            {
                popupTextTmp.text = message;
                popupTextTmp.enabled = true;
            }
            popupTimer = popupDuration;
        }

        private void TriggerVibration(long durationMs)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                using (AndroidJavaObject vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null)
                    {
                        return;
                    }

                    AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    if (vibrationEffectClass != null)
                    {
                        AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot",
                            durationMs,
                            vibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE"));
                        vibrator.Call("vibrate", effect);
                    }
                    else
                    {
                        vibrator.Call("vibrate", durationMs);
                    }
                }
            }
            catch
            {
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public void AutoWire(CaptureController controller)
        {
            if (captureController == null || captureController != controller)
            {
                if (captureController != null)
                    captureController.CaptureResolved -= HandleCaptureResolved;
                captureController = controller;
                if (captureController != null)
                    captureController.CaptureResolved += HandleCaptureResolved;
            }
        }
    }
}
