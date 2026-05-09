using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
#endif

namespace Heartwell.Audio
{
    [DefaultExecutionOrder(-10000)]
    public sealed class AudioOutputGuard : MonoBehaviour
    {
        private const string GuardObjectName = "Heartwell_AudioOutputGuard";

        private static AudioOutputGuard instance;

        private Coroutine pendingReset;
        private bool resetInProgress;
        private bool outputOpened;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBeforeFirstScene()
        {
            EnsureExists();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void EnableEditorGameViewAudioOnLoad()
        {
            EditorApplication.delayCall += EnsureEditorGameViewAudioEnabled;
        }
#endif

        public static void EnsureOutputReady(string reason)
        {
            EnsureExists();

            if (instance != null)
            {
                instance.EnsureDefaultOutput(reason);
            }
        }

        private static void EnsureExists()
        {
            if (instance != null)
            {
                return;
            }

            GameObject guardObject = GameObject.Find(GuardObjectName);
            if (guardObject == null)
            {
                guardObject = new GameObject(GuardObjectName);
            }

            instance = guardObject.GetComponent<AudioOutputGuard>();
            if (instance == null)
            {
                instance = guardObject.AddComponent<AudioOutputGuard>();
            }

            DontDestroyOnLoad(guardObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
            ResetAudioOutput("startup");
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
            instance = null;
        }

        private void HandleAudioConfigurationChanged(bool deviceWasChanged)
        {
            AudioListener.pause = false;
            AudioListener.volume = 1f;

            if (!deviceWasChanged || resetInProgress)
            {
                return;
            }

            outputOpened = false;

            if (pendingReset != null)
            {
                StopCoroutine(pendingReset);
            }

            pendingReset = StartCoroutine(ResetAfterDeviceChange());
        }

        private IEnumerator ResetAfterDeviceChange()
        {
            yield return null;
            pendingReset = null;
            ResetAudioOutput("audio device changed");
        }

        private void ResetAudioOutput(string reason)
        {
            if (resetInProgress)
            {
                return;
            }

            resetInProgress = true;
            EnsureEditorGameViewAudioEnabled();
            AudioListener.pause = false;
            AudioListener.volume = 1f;

            AudioConfiguration configuration = AudioSettings.GetConfiguration();
            bool resetSucceeded = AudioSettings.Reset(configuration);
            outputOpened = true;

            Debug.Log(
                $"HEARTWELL: Audio output reset to the system default device ({reason}). " +
                $"sampleRate={configuration.sampleRate}, speakerMode={configuration.speakerMode}, success={resetSucceeded}",
                this);

            resetInProgress = false;
        }

        private void EnsureDefaultOutput(string reason)
        {
            EnsureEditorGameViewAudioEnabled();
            AudioListener.pause = false;
            AudioListener.volume = 1f;

            if (!outputOpened)
            {
                ResetAudioOutput(reason);
            }
        }

        private static void EnsureEditorGameViewAudioEnabled()
        {
#if UNITY_EDITOR
            try
            {
                Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null)
                {
                    return;
                }

                UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
                if (gameViews == null || gameViews.Length == 0)
                {
                    return;
                }

                FieldInfo playAudioField = gameViewType.GetField("m_PlayAudio", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo audioPlayField = gameViewType.GetField("m_AudioPlay", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo repaintMethod = gameViewType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (UnityEngine.Object gameView in gameViews)
                {
                    bool changed = SetBooleanField(gameView, playAudioField, true);
                    changed |= SetBooleanField(gameView, audioPlayField, true);

                    if (changed)
                    {
                        repaintMethod?.Invoke(gameView, null);
                        Debug.Log("HEARTWELL: Unity Editor Game view audio was muted and has been re-enabled.");
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"HEARTWELL: Could not verify Unity Editor Game view audio state: {exception.Message}");
            }
#endif
        }

#if UNITY_EDITOR
        private static bool SetBooleanField(object target, FieldInfo fieldInfo, bool value)
        {
            if (target == null || fieldInfo == null || fieldInfo.FieldType != typeof(bool))
            {
                return false;
            }

            bool currentValue = (bool)fieldInfo.GetValue(target);
            if (currentValue == value)
            {
                return false;
            }

            fieldInfo.SetValue(target, value);
            return true;
        }
#endif
    }
}
