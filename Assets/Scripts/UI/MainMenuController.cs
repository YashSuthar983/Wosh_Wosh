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
            Debug.Log("Embarking on the journey...");
            // TODO: Add transition/fading logic here
            SceneManager.LoadScene(firstLevelSceneName);
        }

        /// <summary>
        /// Placeholder for opening the co-op join menu.
        /// </summary>
        public void JoinJourney()
        {
            Debug.Log("Opening Co-op Menu...");
            // TODO: Implement Multiplayer UI
        }

        /// <summary>
        /// Opens the options/settings menu.
        /// </summary>
        public void OpenOptions()
        {
            Debug.Log("Opening Options...");
            // TODO: Implement Settings Menu
        }

        /// <summary>
        /// Quits the application.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("Quitting HEARTWELL. Come back soon!");
            Application.Quit();
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    }
}
