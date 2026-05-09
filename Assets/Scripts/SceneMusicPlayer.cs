using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Heartwell.Audio
{
    public class SceneMusicPlayer : MonoBehaviour
    {
        private static readonly List<AudioSource> PersistentMusicBlockers = new List<AudioSource>();

        [SerializeField] private AudioClip musicClip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool persistAcrossScenes = false;
        [SerializeField] private bool destroyWhenFinished = false;
        [SerializeField] private bool blockSceneMusicUntilFinished = false;
        [SerializeField] private bool waitForPersistentMusicToFinish = false;

        private AudioSource source;
        private bool musicStarted;
        private bool playbackCompleted;

        private void Awake()
        {
            AudioOutputGuard.EnsureOutputReady($"music source '{name}' awake");

            source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            source.clip = musicClip;
            source.volume = volume;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.mute = false;
            source.ignoreListenerPause = true;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (playOnStart && musicClip != null)
            {
                if (waitForPersistentMusicToFinish)
                {
                    StartCoroutine(PlayWhenPersistentMusicFinishes());
                }
                else
                {
                    StartCoroutine(PlayMusicWhenReady());
                }
            }
        }

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
        }

        private IEnumerator PlayWhenPersistentMusicFinishes()
        {
            while (HasActivePersistentMusicBlockers())
            {
                yield return null;
            }

            yield return PlayMusicWhenReady();
        }

        private IEnumerator PlayMusicWhenReady()
        {
            if (musicClip == null)
            {
                yield break;
            }

            if (musicClip.loadState == AudioDataLoadState.Unloaded)
            {
                musicClip.LoadAudioData();
            }

            while (musicClip.loadState == AudioDataLoadState.Loading)
            {
                yield return null;
            }

            if (musicClip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogWarning($"SceneMusicPlayer could not load audio clip '{musicClip.name}'.", this);
                yield break;
            }

            PlayMusic();
        }

        private void PlayMusic()
        {
            AudioOutputGuard.EnsureOutputReady($"playing '{musicClip.name}'");

            source.clip = musicClip;
            source.volume = volume;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.mute = false;
            source.ignoreListenerPause = true;

            source.Play();
            musicStarted = true;
            playbackCompleted = false;

            Debug.Log(
                $"HEARTWELL: Playing music '{musicClip.name}' on Unity default audio output. " +
                $"isPlaying={source.isPlaying}, volume={source.volume}, listenerVolume={AudioListener.volume}",
                this);

            if (blockSceneMusicUntilFinished && !loop)
            {
                AddPersistentMusicBlocker(source);
            }

            if (destroyWhenFinished && !loop)
            {
                StartCoroutine(DestroyAfterPlayback());
            }
        }

        private IEnumerator DestroyAfterPlayback()
        {
            while (source != null && source.isPlaying)
            {
                yield return null;
            }

            playbackCompleted = true;
            RemovePersistentMusicBlocker(source);
            Destroy(gameObject);
        }

        private void HandleAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!deviceWasChanged || !musicStarted || playbackCompleted || source == null || musicClip == null)
            {
                return;
            }

            StartCoroutine(ResumeAfterAudioDeviceReset());
        }

        private IEnumerator ResumeAfterAudioDeviceReset()
        {
            float resumeTime = GetSafeResumeTime();
            yield return null;
            yield return null;

            if (source == null || musicClip == null || playbackCompleted)
            {
                yield break;
            }

            source.clip = musicClip;
            source.volume = volume;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.mute = false;
            source.ignoreListenerPause = true;
            source.time = resumeTime;
            source.Play();

            Debug.Log($"HEARTWELL: Resumed music '{musicClip.name}' after audio output changed.", this);
        }

        private float GetSafeResumeTime()
        {
            if (source == null || musicClip == null)
            {
                return 0f;
            }

            if (loop && musicClip.length > 0f)
            {
                return Mathf.Repeat(source.time, musicClip.length);
            }

            return Mathf.Clamp(source.time, 0f, Mathf.Max(0f, musicClip.length - 0.05f));
        }

        private void OnDestroy()
        {
            RemovePersistentMusicBlocker(source);
        }

        private static void AddPersistentMusicBlocker(AudioSource audioSource)
        {
            PrunePersistentMusicBlockers();

            if (audioSource != null && !PersistentMusicBlockers.Contains(audioSource))
            {
                PersistentMusicBlockers.Add(audioSource);
            }
        }

        private static void RemovePersistentMusicBlocker(AudioSource audioSource)
        {
            if (audioSource != null)
            {
                PersistentMusicBlockers.Remove(audioSource);
            }

            PrunePersistentMusicBlockers();
        }

        private static bool HasActivePersistentMusicBlockers()
        {
            PrunePersistentMusicBlockers();
            return PersistentMusicBlockers.Count > 0;
        }

        private static void PrunePersistentMusicBlockers()
        {
            for (int i = PersistentMusicBlockers.Count - 1; i >= 0; i--)
            {
                AudioSource audioSource = PersistentMusicBlockers[i];
                if (audioSource == null || !audioSource.isPlaying)
                {
                    PersistentMusicBlockers.RemoveAt(i);
                }
            }
        }
    }
}
