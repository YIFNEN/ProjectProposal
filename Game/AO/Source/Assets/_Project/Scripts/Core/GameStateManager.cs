using System.Collections;
using AO.Audio;
using AO.Character;
using AO.Rhythm;
using AO.State;
using AO.UI;
using UnityEngine;

namespace AO.Core
{
    public class GameStateManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameSession _session;
        [SerializeField] private SongLibrary _songLibrary;
        [SerializeField] private RhythmEngine _rhythmEngine;
        [SerializeField] private JudgementConfig _judgementConfig;
        [SerializeField] private OxygenSystem _oxygenSystem;
        [SerializeField] private AudioManager _audioManager;
        [SerializeField] private GameplaySettingsOverlay _settingsOverlay;
        [SerializeField] private EternalExitObject _eternalExitObject;
        [SerializeField] private GameplayBodyCalibrationController _bodyCalibrationController;

        [Header("Fallback")]
        [SerializeField] private TextAsset _fallbackBeatmap;
        [SerializeField] private AudioClip _fallbackBgm;
        [SerializeField] private string _fallbackSongName = "Synthion - Twinkle";

        [Header("Options")]
        [SerializeField] private bool _startOnAwake = true;
        [SerializeField, Tooltip("Legacy testing fallback. When enabled, gameplay starts from each song's default timing options instead of saved per-song tweaks.")]
        private bool _forceDefaultTimingOptions;
        [SerializeField, Tooltip("Runs the D-Variant body calibration flow before the song starts when a GameplayBodyCalibrationController exists.")]
        private bool _calibrateBodyBeforeSong = true;

        [Header("Testing")]
        [SerializeField, Tooltip("Play Mode input/haptic testing only. When enabled, oxygen drain/recovery/penalty and oxygen GameOver are disabled in Normal mode.")]
        private bool _disableOxygenRulesForTesting;

        private const float MinPlaybackSpeed = 0.5f;
        private const float MaxPlaybackSpeed = 3f;
        private const float PlaybackSpeedStep = 0.5f;

        private SongDefinition _currentSong;
        private BeatmapData _currentBeatmap;
        private bool _ending;
        private bool _settingsOpen;
        private bool _lastDisableOxygenRulesForTesting;
        private bool _startSequenceRunning;

        public GameSession Session => _session;
        public SongDefinition CurrentSong => _currentSong;
        public float PlaybackSpeed => _session != null ? _session.PlaybackSpeed : 1f;
        public float NoteSpeed => _session != null ? _session.NoteSpeed : 1f;
        public float AudioOffsetSeconds => _session != null ? _session.AudioOffsetSeconds : 0f;
        public bool ForceDefaultTimingOptions => _forceDefaultTimingOptions;

        private void Awake()
        {
            if (_rhythmEngine == null) _rhythmEngine = FindFirstObjectByType<RhythmEngine>();
            if (_oxygenSystem == null) _oxygenSystem = FindFirstObjectByType<OxygenSystem>();
            if (_audioManager == null) _audioManager = FindFirstObjectByType<AudioManager>();
            if (_settingsOverlay == null) _settingsOverlay = FindFirstObjectByType<GameplaySettingsOverlay>(FindObjectsInactive.Include);
            if (_eternalExitObject == null) _eternalExitObject = FindFirstObjectByType<EternalExitObject>(FindObjectsInactive.Include);
            if (_bodyCalibrationController == null) _bodyCalibrationController = FindFirstObjectByType<GameplayBodyCalibrationController>(FindObjectsInactive.Include);
            if (_settingsOverlay != null) _settingsOverlay.Bind(this);
            _lastDisableOxygenRulesForTesting = _disableOxygenRulesForTesting;
        }

        private void OnEnable()
        {
            EventBus.SongEnded += HandleSongEnded;
            EventBus.GameOver += HandleGameOver;
            EventBus.EternalExitRequested += HandleEternalExitRequested;
        }

        private void OnDisable()
        {
            EventBus.SongEnded -= HandleSongEnded;
            EventBus.GameOver -= HandleGameOver;
            EventBus.EternalExitRequested -= HandleEternalExitRequested;
        }

        private void Start()
        {
            if (_startOnAwake) StartSelectedSong();
        }

        private void Update()
        {
            if (_lastDisableOxygenRulesForTesting == _disableOxygenRulesForTesting) return;

            _lastDisableOxygenRulesForTesting = _disableOxygenRulesForTesting;
            ApplyOxygenRules();
            if (_disableOxygenRulesForTesting) _oxygenSystem?.RefillToInitial();
        }

        public void StartSelectedSong()
        {
            if (_startSequenceRunning) return;

            if (_calibrateBodyBeforeSong
                && _bodyCalibrationController != null
                && _bodyCalibrationController.EnabledBeforeSong)
            {
                StartCoroutine(StartSelectedSongAfterCalibration());
                return;
            }

            StartSelectedSongNow();
        }

        private IEnumerator StartSelectedSongAfterCalibration()
        {
            _startSequenceRunning = true;
            yield return _bodyCalibrationController.RunBeforeSong();
            StartSelectedSongNow();
            _startSequenceRunning = false;
        }

        private void StartSelectedSongNow()
        {
            _ending = false;
            GameplayRuntimeState.SetInputBlocked(false);

            _currentSong = ResolveSong();
            if (_currentSong != null && !_currentSong.IsPlayable)
            {
                SongDefinition fallbackSong = _songLibrary != null ? _songLibrary.FirstPlayableSong() : null;
                Debug.LogWarning($"[GameStateManager] Selected song '{_currentSong.DisplayName}' is missing BGM or beatmap. Falling back to '{(fallbackSong != null ? fallbackSong.DisplayName : "none")}'.");
                _currentSong = fallbackSong;
            }

            if (_session != null && _currentSong != null)
            {
                _session.SelectSong(_currentSong.SongId, _currentSong.DisplayName);
                float playbackSpeed = _forceDefaultTimingOptions
                    ? QuantizePlaybackSpeed(_currentSong.DefaultPlaybackSpeed)
                    : PlayerProgress.GetPlaybackSpeed(_currentSong);
                float noteSpeed = _forceDefaultTimingOptions
                    ? Mathf.Clamp(_currentSong.DefaultNoteSpeed, 0.5f, 2f)
                    : PlayerProgress.GetNoteSpeed(_currentSong);
                float audioOffset = _forceDefaultTimingOptions
                    ? Mathf.Clamp(_currentSong.DefaultAudioOffsetSeconds, -0.3f, 0.3f)
                    : PlayerProgress.GetAudioOffset(_currentSong);

                _session.SetSpeed(playbackSpeed, noteSpeed);
                _session.SetAudioOffset(audioOffset);

                if (_forceDefaultTimingOptions)
                {
                    Debug.Log("[GameStateManager] ForceDefaultTimingOptions is enabled for this scene instance. Saved per-song timing prefs were ignored for this run.");
                }

                string timingSource = _forceDefaultTimingOptions ? "forced defaults" : "saved prefs";
                Debug.Log($"[GameStateManager] PlaybackSpeed={playbackSpeed:0.##}x, NoteSpeed={noteSpeed:0.##}x, AudioOffset={_session.AudioOffsetSeconds * 1000f:0}ms ({timingSource}).");
            }

            ApplyRuntimeOptions();

            TextAsset beatmapAsset = _currentSong != null && _currentSong.NormalBeatmap != null ? _currentSong.NormalBeatmap : _fallbackBeatmap;
            AudioClip clip = _currentSong != null && _currentSong.BgmClip != null ? _currentSong.BgmClip : _fallbackBgm;
            if (beatmapAsset == null || clip == null || _rhythmEngine == null)
            {
                Debug.LogError("[GameStateManager] Missing beatmap, BGM, or RhythmEngine.");
                return;
            }

            _currentBeatmap = BeatmapLoader.FromJson(beatmapAsset);
            if (_currentBeatmap == null) return;

            _rhythmEngine.LoopOnEnd = _session != null && _session.Mode == PlayMode.Eternal;
            _rhythmEngine.StartSong(_currentBeatmap, clip);
        }

        public void SetSettingsOpen(bool open)
        {
            if (_settingsOpen == open) return;
            _settingsOpen = open;
            GameplayRuntimeState.SetInputBlocked(open);

            if (open)
            {
                _rhythmEngine?.PauseSong();
                _audioManager?.PauseMusicLayers();
            }
            else
            {
                _rhythmEngine?.ResumeSong();
                _audioManager?.ResumeMusicLayers();
            }
        }

        public void ReturnToLobby()
        {
            _ending = true;
            GameplayRuntimeState.SetInputBlocked(false);
            _rhythmEngine?.StopSong(false);
            SceneTransition.GoToLobby();
        }

        public void SetPlaybackSpeed(float value)
        {
            value = QuantizePlaybackSpeed(value);
            if (_session != null)
            {
                _session.PlaybackSpeed = value;
                PlayerProgress.SetPlaybackSpeed(_session.SelectedSongId, value);
                PlayerPrefs.Save();
            }

            _rhythmEngine?.SetPlaybackSpeed(value);
            _audioManager?.SetPlaybackSpeed(value);
        }

        public void SetNoteSpeed(float value)
        {
            value = Mathf.Clamp(value, 0.5f, 2f);
            if (_session != null)
            {
                _session.NoteSpeed = value;
                PlayerProgress.SetNoteSpeed(_session.SelectedSongId, value);
                PlayerPrefs.Save();
            }

            _rhythmEngine?.SetNoteSpeed(value);
        }

        public void SetAudioOffset(float seconds)
        {
            seconds = Mathf.Clamp(seconds, -0.3f, 0.3f);
            if (_session != null)
            {
                _session.AudioOffsetSeconds = seconds;
                PlayerProgress.SetAudioOffset(_session.SelectedSongId, seconds);
            }

            if (_judgementConfig != null) _judgementConfig.AudioOffsetSeconds = seconds;
            PlayerPrefs.Save();
        }

        public void ResetTimingOptionsToDefaults()
        {
            SongDefinition song = _currentSong != null ? _currentSong : ResolveSong();
            if (song == null) return;

            float playbackSpeed = QuantizePlaybackSpeed(song.DefaultPlaybackSpeed);
            float noteSpeed = Mathf.Clamp(song.DefaultNoteSpeed, 0.5f, 2f);
            float audioOffset = Mathf.Clamp(song.DefaultAudioOffsetSeconds, -0.3f, 0.3f);

            PlayerProgress.ResetTimingOptions(song);
            PlayerPrefs.Save();

            if (_session != null)
            {
                _session.SetSpeed(playbackSpeed, noteSpeed);
                _session.SetAudioOffset(audioOffset);
            }

            if (_judgementConfig != null) _judgementConfig.AudioOffsetSeconds = audioOffset;
            _rhythmEngine?.SetPlaybackSpeed(playbackSpeed);
            _rhythmEngine?.SetNoteSpeed(noteSpeed);
            _audioManager?.SetPlaybackSpeed(playbackSpeed);

            Debug.Log($"[GameStateManager] Timing options reset to defaults for '{song.DisplayName}'.");
        }

        public void SetBgmVolume(float value)
        {
            PlayerProgress.SetBgmVolume(value);
            _audioManager?.SetBgmVolume(value);
            PlayerPrefs.Save();
        }

        public void SetSfxVolume(float value)
        {
            PlayerProgress.SetSfxVolume(value);
            _audioManager?.SetSfxVolume(value);
            PlayerPrefs.Save();
        }

        private SongDefinition ResolveSong()
        {
            if (_songLibrary == null) return null;
            return _songLibrary.FindById(_session != null ? _session.SelectedSongId : null);
        }

        private void ApplyRuntimeOptions()
        {
            float playbackSpeed = _session != null ? QuantizePlaybackSpeed(_session.PlaybackSpeed) : 1f;
            float noteSpeed = _session != null ? _session.NoteSpeed : 1f;
            float audioOffset = _session != null ? _session.AudioOffsetSeconds : 0f;

            if (_judgementConfig != null) _judgementConfig.AudioOffsetSeconds = audioOffset;
            if (_rhythmEngine != null)
            {
                _rhythmEngine.SetPlaybackSpeed(playbackSpeed);
                _rhythmEngine.SetNoteSpeed(noteSpeed);
            }

            if (_audioManager != null)
            {
                _audioManager.SetPlaybackSpeed(playbackSpeed);
                _audioManager.SetBgmVolume(PlayerProgress.GetBgmVolume());
                _audioManager.SetSfxVolume(PlayerProgress.GetSfxVolume());
            }

            bool normalMode = _session == null || _session.Mode == PlayMode.Normal;
            ApplyOxygenRules();
            if (_eternalExitObject != null) _eternalExitObject.SetAvailable(!normalMode);
        }

        private void ApplyOxygenRules()
        {
            bool normalMode = _session == null || _session.Mode == PlayMode.Normal;
            bool oxygenRulesEnabled = normalMode && !_disableOxygenRulesForTesting;
            if (_oxygenSystem != null) _oxygenSystem.SetRulesEnabled(oxygenRulesEnabled);
        }

        private static float QuantizePlaybackSpeed(float value)
        {
            float stepped = Mathf.Round(value / PlaybackSpeedStep) * PlaybackSpeedStep;
            return Mathf.Clamp(stepped, MinPlaybackSpeed, MaxPlaybackSpeed);
        }

        private void HandleSongEnded(bool cleared)
        {
            if (_ending) return;
            if (_session != null && _session.Mode == PlayMode.Eternal) return;
            CompleteRun(cleared ? SessionResultStatus.Cleared : SessionResultStatus.ReturnedToLobby);
        }

        private void HandleGameOver()
        {
            if (_ending) return;
            if (_session != null && _session.Mode == PlayMode.Eternal) return;
            CompleteRun(SessionResultStatus.GameOver);
        }

        private void HandleEternalExitRequested()
        {
            if (_ending) return;
            if (_session == null || _session.Mode != PlayMode.Eternal) return;
            CompleteRun(SessionResultStatus.EternalExited);
        }

        private void CompleteRun(SessionResultStatus status)
        {
            _ending = true;
            GameplayRuntimeState.SetInputBlocked(false);

            string songId = _currentSong != null ? _currentSong.SongId : (_session != null ? _session.SelectedSongId : "unknown");
            string songName = _currentSong != null ? _currentSong.DisplayName : (_session != null ? _session.SongName : _fallbackSongName);
            SessionResult result = RunStatsTracker.Instance != null
                ? RunStatsTracker.Instance.BuildResult(status, _session, songId, songName)
                : new SessionResult { Status = status, SongId = songId, SongName = songName };

            if (_session != null) _session.SetResult(result);
            PlayerProgress.SaveResult(result);
            _rhythmEngine?.StopSong(status == SessionResultStatus.Cleared);
            SceneTransition.GoToResult();
        }
    }
}
