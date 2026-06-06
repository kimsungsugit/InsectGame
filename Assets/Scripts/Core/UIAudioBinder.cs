using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InsectGame.Core
{
    /// <summary>
    /// 씬 내 모든 UnityEngine.UI.Button에 자동으로 hover/click 사운드를 부착합니다.
    /// PlaySceneBootstrap에서 1회 등록하면 이후 동적 생성된 버튼은 다음 BindAll() 호출 시 처리됩니다.
    /// </summary>
    public class UIAudioBinder : MonoBehaviour
    {
        private float rebindTimer;
        private const float RebindIntervalSec = 5f;

        public void BindAll()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Button btn in buttons)
            {
                if (btn == null) continue;
                if (btn.GetComponent<UIAudioListener>() != null) continue;
                btn.gameObject.AddComponent<UIAudioListener>();
            }
        }

        private void Start()
        {
            BindAll();
        }

        private void Update()
        {
            rebindTimer += Time.deltaTime;
            if (rebindTimer >= RebindIntervalSec)
            {
                rebindTimer = 0f;
                BindAll();
            }
        }
    }

    /// <summary>버튼에 부착되어 hover(EventSystem 기반)와 click 사운드를 재생합니다.</summary>
    public class UIAudioListener : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SfxType.MenuHover);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SfxType.ButtonClick);
        }
    }
}
