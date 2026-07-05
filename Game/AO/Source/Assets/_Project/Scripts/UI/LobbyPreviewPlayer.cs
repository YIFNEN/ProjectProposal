using System;
using System.Reflection;
using AO.Core;
using UnityEngine;

namespace AO.UI
{
    [DisallowMultipleComponent]
    public class LobbyPreviewPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField, Range(0f, 1f)] private float _volumeScale = 0.65f;
        [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.25f;
        [SerializeField] private bool _loopPreview = true;
        [SerializeField] private bool _allowRuntimeAudioSourceCreation = false;

        private AudioClip _currentClip;
        private Coroutine _fadeRoutine;
        private bool _playbackRequested;
        private bool _audioSourceErrorLogged;

        public bool IsPlaybackRequested => _playbackRequested;
        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource != null) ConfigureAudioSource();
            else if (_allowRuntimeAudioSourceCreation) EnsureAudioSource(logMissing: false);
        }

        private void OnDisable()
        {
            SetMenuBgmPreviewDucked(false);
            ResetPreviewState();
        }

        public void PlayPreview(SongDefinition song)
        {
            AudioClip preview = song != null ? song.PreviewClip : null;
            if (preview == null)
            {
                _playbackRequested = false;
                StopPreview();
                return;
            }

            if (!EnsureAudioSource())
            {
                _playbackRequested = false;
                return;
            }

            _playbackRequested = true;
            SetMenuBgmPreviewDucked(true);

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (_currentClip == preview)
            {
                _audioSource.volume = TargetVolume();
                if (!_audioSource.isPlaying)
                {
                    _audioSource.clip = preview;
                    _audioSource.loop = _loopPreview;
                    _audioSource.UnPause();
                    if (!_audioSource.isPlaying) _audioSource.Play();
                }

                return;
            }

            _fadeRoutine = StartCoroutine(SwitchPreview(preview));
        }

        public void PausePreview()
        {
            _playbackRequested = false;
            SetMenuBgmPreviewDucked(false);
            if (!EnsureAudioSource(logMissing: false)) return;

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (_audioSource.isPlaying) _audioSource.Pause();
        }

        public void TogglePreview(SongDefinition song)
        {
            if (_playbackRequested) PausePreview();
            else PlayPreview(song);
        }

        public void ResetPreviewState()
        {
            _playbackRequested = false;
            SetMenuBgmPreviewDucked(false);
            StopPreview(immediate: true);
        }

        public void StopPreview(bool immediate = false)
        {
            _playbackRequested = false;
            SetMenuBgmPreviewDucked(false);
            if (!EnsureAudioSource(logMissing: false)) return;

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (immediate)
            {
                _audioSource.Stop();
                _audioSource.clip = null;
                _audioSource.volume = 0f;
                _currentClip = null;
                return;
            }

            if (isActiveAndEnabled && _audioSource.isPlaying)
            {
                _fadeRoutine = StartCoroutine(FadeOutAndStop());
            }
            else
            {
                _audioSource.Stop();
                _audioSource.clip = null;
                _audioSource.volume = 0f;
                _currentClip = null;
            }
        }

        private System.Collections.IEnumerator SwitchPreview(AudioClip nextClip)
        {
            yield return FadeVolume(0f);

            _currentClip = nextClip;
            _audioSource.clip = nextClip;
            _audioSource.loop = _loopPreview;
            _audioSource.pitch = 1f;
            _audioSource.time = 0f;
            _audioSource.Play();

            yield return FadeVolume(TargetVolume());
            _fadeRoutine = null;
        }

        private System.Collections.IEnumerator FadeOutAndStop()
        {
            yield return FadeVolume(0f);
            _audioSource.Stop();
            _audioSource.clip = null;
            _currentClip = null;
            _fadeRoutine = null;
        }

        private System.Collections.IEnumerator FadeVolume(float target)
        {
            if (!EnsureAudioSource(logMissing: false)) yield break;

            float start = _audioSource.volume;
            if (Mathf.Approximately(_fadeSeconds, 0f))
            {
                _audioSource.volume = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeSeconds);
                _audioSource.volume = Mathf.Lerp(start, target, t);
                yield return null;
            }

            _audioSource.volume = target;
        }

        private float TargetVolume()
        {
            return Mathf.Clamp01(PlayerProgress.GetBgmVolume() * _volumeScale);
        }

        private bool EnsureAudioSource(bool logMissing = true)
        {
            if (_audioSource != null) return true;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                if (_allowRuntimeAudioSourceCreation)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                }
                else
                {
                    if (logMissing && !_audioSourceErrorLogged)
                    {
                        Debug.LogError("[LobbyPreviewPlayer] Required AudioSource is missing. Runtime AudioSource creation is disabled, so preview playback was skipped.", this);
                        _audioSourceErrorLogged = true;
                    }

                    return false;
                }
            }

            ConfigureAudioSource();
            return true;
        }

        private void ConfigureAudioSource()
        {
            if (_audioSource == null) return;

            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.loop = _loopPreview;
            _audioSource.volume = 0f;
        }

        private static void SetMenuBgmPreviewDucked(bool ducked)
        {
            Type type = Type.GetType("AO.Audio.MenuBgmPlayer, Assembly-CSharp");
            MethodInfo method = type?.GetMethod("SetPreviewDucked", BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, new object[] { ducked });
        }
    }
}
