using System;
using AO.Core;
using AO.State;
using UnityEngine;

namespace AO.Audio
{
    public enum SfxId
    {
        Perfect,
        Good,
        Miss,
        FishStroke,
        OxygenCritical,
        FeverStart,
        FeverEnd,
        GameOver,
        UiClick,
        UiConfirm,
        FeverHit
    }

    public class AudioManager : MonoBehaviour
    {
        [Serializable]
        public struct SfxEntry
        {
            public SfxId Id;
            public AudioClip Clip;
            [Range(0f, 1f)] public float Volume;
            [Min(0f)] public float StartTime;
            [Min(0f)] public float MaxDuration;
        }

        [Header("BGM")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _feverSource;
        [SerializeField] private AudioSource _feverExitSource;
        [SerializeField] private AudioSource _criticalSource;
        [Tooltip("Short fever entry layer/stinger. Currently used for whale.mp3.")]
        [SerializeField] private AudioClip _feverClip;
        [SerializeField] private AudioClip _feverExitClip;
        [SerializeField] private AudioClip _criticalClip;
        [SerializeField, Range(0.05f, 2f)] private float _crossfadeSeconds = 0.65f;
        [Tooltip("Volume of the fever entry stinger/layer. Multiplied by BGM volume.")]
        [InspectorName("Fever Entry Volume")]
        [SerializeField, Range(0f, 1f)] private float _feverLayerVolume = 0.68f;
        [SerializeField, Tooltip("0 uses Crossfade Seconds. Raise this to hear more of the whale/stinger clip.")]
        [InspectorName("Fever Entry Seconds")]
        [Range(0f, 3f)] private float _feverEntryStingerSeconds = 1.4f;
        [InspectorName("Fever Exit Volume")]
        [SerializeField, Range(0f, 1f)] private float _feverExitVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _criticalVolume = 0.22f;
        [SerializeField, Range(0.05f, 2f)] private float _criticalFadeSeconds = 0.45f;
        [SerializeField, Tooltip("BGM volume multiplier reached during fever. 1 keeps the original song volume.")]
        [Range(0.25f, 2f)] private float _bgmFeverVolumeMultiplier = 1.15f;
        [SerializeField, Tooltip("When enabled, applies a low-pass filter to the current BGM during fever.")]
        private bool _useLowPassFallback = true;
        [SerializeField, Range(300f, 22000f)] private float _feverLowPassCutoff = 900f;

        [Header("SFX")]
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private SfxEntry[] _sfxEntries = Array.Empty<SfxEntry>();
        [SerializeField, Range(0f, 0.25f)] private float _sfxMinInterval = 0.04f;
        [SerializeField, Range(0f, 0.25f)] private float _hitSfxMinInterval = 0.065f;

        [Header("Ambient")]
        [SerializeField] private AudioSource _ambientSource;
        [SerializeField] private AudioClip _ambientClip;
        [SerializeField, Range(0f, 1f)] private float _ambientVolume = 0.35f;

        private AudioLowPassFilter _lowPass;
        private float _baseBgmVolume = 1f;
        private float _userBgmVolume = 1f;
        private float _userSfxVolume = 1f;
        private float _playbackSpeed = 1f;
        private float _feverMix;
        private float _feverTarget;
        private float _criticalMix;
        private float _criticalTarget;
        private bool _isFeverActive;
        private readonly float[] _lastSfxTimes = new float[Enum.GetValues(typeof(SfxId)).Length];
        private float _lastHitSfxTime = -999f;
        private float _trimmedSfxStopTime = -1f;
        private float _feverEntryStingerStopTime = -1f;

        private void Awake()
        {
            _userBgmVolume = PlayerProgress.GetBgmVolume();
            _userSfxVolume = PlayerProgress.GetSfxVolume();

            if (_bgmSource != null) _baseBgmVolume = Mathf.Max(0f, _bgmSource.volume);

            PrepareSource(_bgmSource, loop: false);
            PrepareSource(_feverSource, loop: false);
            PrepareSource(_feverExitSource, loop: false);
            PrepareSource(_criticalSource, loop: true);
            PrepareSource(_sfxSource, loop: false);
            PrepareSource(_ambientSource, loop: true);

            if (_bgmSource != null && _useLowPassFallback)
            {
                _lowPass = _bgmSource.GetComponent<AudioLowPassFilter>();
                if (_lowPass == null) _lowPass = _bgmSource.gameObject.AddComponent<AudioLowPassFilter>();
                _lowPass.cutoffFrequency = 22000f;
                _lowPass.enabled = false;
            }

            if (_ambientSource != null && _ambientClip != null)
            {
                _ambientSource.clip = _ambientClip;
                _ambientSource.volume = _ambientVolume * _userBgmVolume;
                if (!_ambientSource.isPlaying) _ambientSource.Play();
            }

            ApplyMusicVolumes();
        }

        private void OnEnable()
        {
            EventBus.SongStarted += HandleSongStarted;
            EventBus.NoteJudged += HandleNoteJudged;
            EventBus.FishStrokeSucceeded += HandleFishSucceeded;
            EventBus.FishStrokeFailed += HandleFishFailed;
            EventBus.OxygenCritical += HandleOxygenCritical;
            EventBus.OxygenRecovered += HandleOxygenRecovered;
            EventBus.FeverActivated += HandleFeverActivated;
            EventBus.FeverEnded += HandleFeverEnded;
            EventBus.GameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            EventBus.SongStarted -= HandleSongStarted;
            EventBus.NoteJudged -= HandleNoteJudged;
            EventBus.FishStrokeSucceeded -= HandleFishSucceeded;
            EventBus.FishStrokeFailed -= HandleFishFailed;
            EventBus.OxygenCritical -= HandleOxygenCritical;
            EventBus.OxygenRecovered -= HandleOxygenRecovered;
            EventBus.FeverActivated -= HandleFeverActivated;
            EventBus.FeverEnded -= HandleFeverEnded;
            EventBus.GameOver -= HandleGameOver;
        }

        private void Update()
        {
            bool changed = false;

            float feverStep = Time.deltaTime / Mathf.Max(0.01f, _crossfadeSeconds);
            float nextFever = Mathf.MoveTowards(_feverMix, _feverTarget, feverStep);
            if (!Mathf.Approximately(nextFever, _feverMix))
            {
                _feverMix = nextFever;
                changed = true;
            }

            float criticalStep = Time.deltaTime / Mathf.Max(0.01f, _criticalFadeSeconds);
            float nextCritical = Mathf.MoveTowards(_criticalMix, _criticalTarget, criticalStep);
            if (!Mathf.Approximately(nextCritical, _criticalMix))
            {
                _criticalMix = nextCritical;
                changed = true;
            }

            if (changed) ApplyMusicVolumes();

            if (_criticalSource != null && _criticalTarget <= 0f && _criticalMix <= 0f && _criticalSource.isPlaying)
            {
                _criticalSource.Stop();
            }

            if (_sfxSource != null && _trimmedSfxStopTime > 0f && Time.unscaledTime >= _trimmedSfxStopTime)
            {
                _sfxSource.Stop();
                _trimmedSfxStopTime = -1f;
            }

            if (_feverSource != null && _feverEntryStingerStopTime > 0f && Time.unscaledTime >= _feverEntryStingerStopTime)
            {
                _feverSource.Stop();
                _feverEntryStingerStopTime = -1f;
            }
        }

        public void PlaySfx(SfxId id)
        {
            if (_sfxSource == null || ShouldThrottle(id)) return;

            for (int i = 0; i < _sfxEntries.Length; i++)
            {
                if (_sfxEntries[i].Id != id || _sfxEntries[i].Clip == null) continue;
                float volume = (_sfxEntries[i].Volume <= 0f ? 1f : _sfxEntries[i].Volume) * _userSfxVolume;
                if (IsHitSfx(id) && _sfxEntries[i].MaxDuration > 0f)
                {
                    PlayTrimmedSfx(_sfxEntries[i], volume);
                }
                else
                {
                    _sfxSource.PlayOneShot(_sfxEntries[i].Clip, volume);
                }
                return;
            }
        }

        public void SetPlaybackSpeed(float speed)
        {
            _playbackSpeed = Mathf.Clamp(speed, 0.5f, 3f);
            SetPitch(_bgmSource);
            SetPitch(_feverSource);
            SetPitch(_feverExitSource);
            SetPitch(_criticalSource);
        }

        public void SetBgmVolume(float volume)
        {
            _userBgmVolume = Mathf.Clamp01(volume);
            ApplyMusicVolumes();
        }

        public void SetSfxVolume(float volume)
        {
            _userSfxVolume = Mathf.Clamp01(volume);
        }

        public void PauseMusicLayers()
        {
            PauseIfPlaying(_feverSource);
            PauseIfPlaying(_feverExitSource);
            PauseIfPlaying(_criticalSource);
            PauseIfPlaying(_ambientSource);
        }

        public void ResumeMusicLayers()
        {
            _feverSource?.UnPause();
            _feverExitSource?.UnPause();
            _criticalSource?.UnPause();
            _ambientSource?.UnPause();
        }

        private void HandleSongStarted(double dspStart)
        {
            _isFeverActive = false;
            _feverMix = 0f;
            _feverTarget = 0f;
            _criticalMix = 0f;
            _criticalTarget = 0f;
            _feverEntryStingerStopTime = -1f;

            if (_bgmSource != null)
            {
                _bgmSource.pitch = _playbackSpeed;
                _bgmSource.volume = EffectiveBgmVolume;
            }

            StopAndAssign(_feverSource, _feverClip, loop: false);
            StopAndAssign(_feverExitSource, _feverExitClip, loop: false);
            StopAndAssign(_criticalSource, _criticalClip, loop: true);

            if (_lowPass != null)
            {
                _lowPass.cutoffFrequency = 22000f;
                _lowPass.enabled = false;
            }

            ApplyMusicVolumes();
        }

        private void HandleNoteJudged(NoteJudgedEvent e)
        {
            if (ShouldPlayFeverHit(e))
            {
                PlaySfx(SfxId.FeverHit);
                return;
            }

            switch (e.Result)
            {
                case JudgementResult.Perfect:
                    PlaySfx(SfxId.Perfect);
                    break;
                case JudgementResult.Good:
                    PlaySfx(SfxId.Good);
                    break;
                case JudgementResult.Miss:
                    PlaySfx(SfxId.Miss);
                    break;
            }
        }

        private bool ShouldPlayFeverHit(NoteJudgedEvent e)
        {
            if (e.Result == JudgementResult.Miss) return false;
            if (e.IsFeverHit) return true;
            return _isFeverActive || (FeverSystem.Instance != null && FeverSystem.Instance.IsActive);
        }

        private void HandleFishSucceeded()
        {
            PlaySfx(SfxId.FishStroke);
        }

        private void HandleFishFailed()
        {
        }

        private void HandleOxygenCritical()
        {
            if (_criticalSource == null || _criticalClip == null) return;
            if (_criticalSource.clip != _criticalClip) _criticalSource.clip = _criticalClip;
            _criticalSource.loop = true;
            _criticalSource.pitch = _playbackSpeed;
            if (!_criticalSource.isPlaying) _criticalSource.Play();
            _criticalTarget = 1f;
        }

        private void HandleOxygenRecovered()
        {
            _criticalTarget = 0f;
        }

        private void HandleGameOver()
        {
            _criticalTarget = 0f;
            PlaySfx(SfxId.GameOver);
        }

        private void HandleFeverActivated()
        {
            _isFeverActive = true;
            StartFeverEntryStinger();
            EnsureLowPassFilter();
            _feverTarget = 1f;
        }

        private void HandleFeverEnded()
        {
            _isFeverActive = false;
            _feverTarget = 0f;
            if (_feverSource != null)
            {
                _feverSource.Stop();
                _feverEntryStingerStopTime = -1f;
            }
            StartLayerFromBeginning(_feverExitSource, _feverExitClip, loop: false);
        }

        private bool ShouldThrottle(SfxId id)
        {
            float now = Time.unscaledTime;
            int index = (int)id;
            float perIdInterval = IsHitSfx(id) ? _hitSfxMinInterval : _sfxMinInterval;
            if (index >= 0 && index < _lastSfxTimes.Length && now - _lastSfxTimes[index] < perIdInterval) return true;

            if (IsHitSfx(id) && now - _lastHitSfxTime < _hitSfxMinInterval) return true;

            if (index >= 0 && index < _lastSfxTimes.Length) _lastSfxTimes[index] = now;
            if (IsHitSfx(id)) _lastHitSfxTime = now;
            return false;
        }

        private static bool IsHitSfx(SfxId id)
        {
            return id == SfxId.Perfect || id == SfxId.Good || id == SfxId.Miss || id == SfxId.FeverHit;
        }

        private void PlayTrimmedSfx(SfxEntry entry, float volume)
        {
            if (_sfxSource == null || entry.Clip == null) return;

            float start = Mathf.Clamp(entry.StartTime, 0f, Mathf.Max(0f, entry.Clip.length - 0.01f));
            float duration = Mathf.Min(entry.MaxDuration, Mathf.Max(0.01f, entry.Clip.length - start));

            _sfxSource.Stop();
            _sfxSource.clip = entry.Clip;
            _sfxSource.time = start;
            _sfxSource.volume = volume;
            _sfxSource.pitch = 1f;
            _sfxSource.loop = false;
            _sfxSource.Play();
            _trimmedSfxStopTime = Time.unscaledTime + duration;
        }

        private void ApplyMusicVolumes()
        {
            float feverBgmMultiplier = Mathf.Lerp(1f, Mathf.Max(0f, _bgmFeverVolumeMultiplier), _feverMix);
            if (_bgmSource != null) _bgmSource.volume = EffectiveBgmVolume * feverBgmMultiplier;
            if (_feverSource != null && _feverSource.isPlaying) _feverSource.volume = EffectiveFeverEntryVolume;
            if (_feverExitSource != null && _feverExitSource.isPlaying) _feverExitSource.volume = EffectiveFeverExitVolume;
            if (_criticalSource != null) _criticalSource.volume = EffectiveCriticalVolume * _criticalMix;
            if (_ambientSource != null) _ambientSource.volume = _ambientVolume * _userBgmVolume;

            if (_lowPass != null && _useLowPassFallback)
            {
                _lowPass.enabled = _feverMix > 0.001f;
                _lowPass.cutoffFrequency = Mathf.Lerp(22000f, _feverLowPassCutoff, _feverMix);
            }
        }

        private void StartFeverEntryStinger()
        {
            if (_feverSource == null || _feverClip == null)
            {
                _feverEntryStingerStopTime = -1f;
                return;
            }

            StartLayerFromBeginning(_feverSource, _feverClip, loop: false);
            _feverSource.pitch = _playbackSpeed;
            _feverSource.volume = EffectiveFeverEntryVolume;

            float stingerSeconds = _feverEntryStingerSeconds > 0f
                ? _feverEntryStingerSeconds
                : _crossfadeSeconds;
            _feverEntryStingerStopTime = Time.unscaledTime + Mathf.Min(stingerSeconds, _feverClip.length);
        }

        private void EnsureLowPassFilter()
        {
            if (!_useLowPassFallback || _bgmSource == null) return;

            _lowPass = _bgmSource.GetComponent<AudioLowPassFilter>();
            if (_lowPass == null) _lowPass = _bgmSource.gameObject.AddComponent<AudioLowPassFilter>();
        }

        private void StartLayerFromBeginning(AudioSource source, AudioClip clip, bool loop)
        {
            if (source == null || clip == null) return;
            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.pitch = _playbackSpeed;
            source.time = 0f;
            source.Play();
        }

        private void StopAndAssign(AudioSource source, AudioClip clip, bool loop)
        {
            if (source == null) return;
            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.volume = 0f;
            source.pitch = _playbackSpeed;
        }

        private static void PrepareSource(AudioSource source, bool loop)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
        }

        private void SetPitch(AudioSource source)
        {
            if (source != null) source.pitch = _playbackSpeed;
        }

        private static void PauseIfPlaying(AudioSource source)
        {
            if (source != null && source.isPlaying) source.Pause();
        }

        private float EffectiveBgmVolume => _baseBgmVolume * _userBgmVolume;
        private float EffectiveFeverEntryVolume => _feverLayerVolume * _userBgmVolume;
        private float EffectiveFeverExitVolume => _feverExitVolume * _userBgmVolume;
        private float EffectiveCriticalVolume => _criticalVolume * _userBgmVolume;
    }
}
