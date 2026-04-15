using InsectGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.UI
{
    /// <summary>
    /// 메인 메뉴 씬 컨트롤러.
    /// Unity UI Button의 OnClick에 연결하여 사용합니다.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }

        /// <summary>게임 시작 버튼.</summary>
        public void OnStartGame()
        {
            SceneManager.LoadScene(GameConstants.Scenes.Play);
        }

        /// <summary>설정 패널 토글.</summary>
        public void OnToggleSettings()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        /// <summary>게임 종료 버튼.</summary>
        public void OnExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
