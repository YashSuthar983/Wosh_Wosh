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
        [SerializeField] private string firstLevelSceneName = "OutdoorsScene";
        
        /// <summary>
        /// Starts the journey by loading the first gameplay level.
        /// </summary>
        public void Embark()
        {
            // TODO: Add transition/fading logic here
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
