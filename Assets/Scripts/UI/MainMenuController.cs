using UnityEngine;
using UnityEngine.SceneManagement;

namespace Heartwell.UI
{
    /// <summary>
    /// Manages the high-level logic for the Main Menu, including scene transitions
    /// and application lifecycle events.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Settings")]
        [SerializeField] private string firstLevelSceneName = "OpeningCutscene";

        private void Start()
        {
            // Auto-wire buttons at runtime to avoid Unity Editor persistent listener serialization issues
            var playBtnObj = GameObject.Find("Btn_Embark");
            if (playBtnObj != null)
            {
                var btn = playBtnObj.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.onClick.AddListener(Embark);
            }

            var optBtnObj = GameObject.Find("Btn_Options");
            if (optBtnObj != null)
            {
                var btn = optBtnObj.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.onClick.AddListener(OpenOptions);
            }

            var exitBtnObj = GameObject.Find("Btn_Exit");
            if (exitBtnObj != null)
            {
                var btn = exitBtnObj.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.onClick.AddListener(QuitGame);
            }
        }
        
        /// <summary>
        /// Starts the journey by loading the first gameplay level.
        /// </summary>
        public void Embark()
        {
            Debug.Log($"Embark button clicked! Attempting to load scene: {firstLevelSceneName}");
            Time.timeScale = 1f; // Ensure time isn't frozen, which would break the cutscene coroutines
            SceneManager.LoadScene(firstLevelSceneName);
        }

        /// <summary>
        /// Placeholder for opening the co-op join menu.
        /// </summary>
        public void JoinJourney()
        {
            // TODO: Implement Multiplayer UI
        }

        /// <summary>
        /// Opens the options/settings menu.
        /// </summary>
        public void OpenOptions()
        {
            // TODO: Implement Settings Menu
        }

        /// <summary>
        /// Quits the application.
        /// </summary>
        public void QuitGame()
        {
            Application.Quit();
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    }
}
