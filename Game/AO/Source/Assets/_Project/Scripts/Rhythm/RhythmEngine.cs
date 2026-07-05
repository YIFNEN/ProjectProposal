using System;
using System.Collections.Generic;
using AO.Core;
using UnityEngine;

namespace AO.Rhythm
{
    public class RhythmEngine : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private JudgementConfig _judgementConfig;

        [Header("Scheduling")]
        [SerializeField, Range(0.5f, 10f)] private float _leadInSeconds = 3f;

        [Header("Runtime Speed")]
        [SerializeField, Range(0.5f, 3f)] private float _playbackSpeed = 1f;
        [SerializeField, Range(0.5f, 2f)] private float _noteSpeedMultiplier = 1f;

        [Header("Song Tail")]
        [SerializeField, Tooltip("마지막 노트의 판정 가능 시간이 지난 뒤 산소 자연 감소를 멈추기까지의 여유 시간(초).")]
        [Range(0f, 5f)] private float _postLastNoteOxygenGraceSeconds = 0.5f;

        private BeatmapData _beatmap;
        private double _dspStartTime;
        private bool _isPlaying;
        private bool _isPaused;
        private bool _hasPostedSongEnd;
        private bool _hasPostedSongInteractionStart;
        private bool _hasPostedSongInteractionEnd;
        private double _lastInteractiveSongTime;
        private double _pauseDspTime;
        private Queue<NoteData> _pendingNotes;

        public bool IsPlaying => _isPlaying;
        public bool IsPaused => _isPaused;
        public bool LoopOnEnd { get; set; }
        public float PlaybackSpeed => _playbackSpeed;
        public float NoteSpeedMultiplier => _noteSpeedMultiplier;
        public float SongLengthSeconds
        {
            get
            {
                if (_beatmap != null && _beatmap.SongLengthSeconds > 0f) return _beatmap.SongLengthSeconds;
                if (_bgmSource != null && _bgmSource.clip != null) return _bgmSource.clip.length;
                return 0f;
            }
        }
        public float RemainingSongTimeSeconds => Mathf.Max(0f, SongLengthSeconds - (float)SongTime);

        public float EffectiveNoteLeadTimeSeconds
        {
            get
            {
                float baseLead = _judgementConfig != null ? _judgementConfig.NoteLeadTimeSeconds : 1.5f;
                return baseLead / Mathf.Max(0.1f, _noteSpeedMultiplier);
            }
        }

        public double SongTime
        {
            get
            {
                if (_beatmap == null) return 0;
                double now = _isPaused ? _pauseDspTime : AudioSettings.dspTime;
                double offset = _judgementConfig != null ? _judgementConfig.AudioOffsetSeconds : 0f;
                return (now - _dspStartTime) * _playbackSpeed - _beatmap.StartOffsetSeconds - offset;
            }
        }

        public double SongTimeWithLead => SongTime + EffectiveNoteLeadTimeSeconds;

        public event Action<NoteData> OnNoteShouldSpawn;

        private void Awake()
        {
            if (_bgmSource == null) _bgmSource = GetComponent<AudioSource>();
        }

        public void StartSong(BeatmapData beatmap, AudioClip bgmClip)
        {
            if (beatmap == null || bgmClip == null)
            {
                Debug.LogError("[RhythmEngine] StartSong: beatmap or clip is null.");
                return;
            }

            if (_bgmSource == null)
            {
                Debug.LogError("[RhythmEngine] AudioSource not assigned.");
                return;
            }

            _beatmap = beatmap;
            _bgmSource.clip = bgmClip;
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = false;
            _bgmSource.pitch = _playbackSpeed;

            if (bgmClip.loadState == AudioDataLoadState.Unloaded)
            {
                bgmClip.LoadAudioData();
            }

            _pendingNotes = new Queue<NoteData>(beatmap.Notes);
            _dspStartTime = AudioSettings.dspTime + _leadInSeconds;
            _bgmSource.PlayScheduled(_dspStartTime);

            _isPlaying = true;
            _isPaused = false;
            _hasPostedSongEnd = false;
            _hasPostedSongInteractionStart = false;
            _hasPostedSongInteractionEnd = false;
            _lastInteractiveSongTime = CalculateLastInteractiveSongTime(beatmap);
            EventBus.RaiseGameplayPauseChanged(false);
            EventBus.RaiseSongStarted(_dspStartTime);
        }

        public void StopSong(bool cleared)
        {
            if (!_isPlaying) return;

            _bgmSource.Stop();
            _isPlaying = false;
            _isPaused = false;
            EventBus.RaiseGameplayPauseChanged(false);

            if (!_hasPostedSongEnd)
            {
                _hasPostedSongEnd = true;
                EventBus.RaiseSongEnded(cleared);
            }
        }

        public void PauseSong()
        {
            if (!_isPlaying || _isPaused) return;
            _pauseDspTime = AudioSettings.dspTime;
            _bgmSource.Pause();
            _isPaused = true;
            EventBus.RaiseGameplayPauseChanged(true);
        }

        public void ResumeSong()
        {
            if (!_isPlaying || !_isPaused) return;
            double pausedSeconds = AudioSettings.dspTime - _pauseDspTime;
            _dspStartTime += pausedSeconds;
            _bgmSource.UnPause();
            _isPaused = false;
            EventBus.RaiseGameplayPauseChanged(false);
        }

        public void SetPlaybackSpeed(float speed)
        {
            speed = Mathf.Clamp(speed, 0.5f, 3f);
            if (Mathf.Approximately(_playbackSpeed, speed))
            {
                if (_bgmSource != null) _bgmSource.pitch = speed;
                return;
            }

            double now = _isPaused ? _pauseDspTime : AudioSettings.dspTime;
            double elapsedSong = (now - _dspStartTime) * _playbackSpeed;
            _playbackSpeed = speed;
            if (_isPlaying)
            {
                _dspStartTime = now - elapsedSong / _playbackSpeed;
            }

            if (_bgmSource != null) _bgmSource.pitch = _playbackSpeed;
        }

        public void SetNoteSpeed(float speed)
        {
            _noteSpeedMultiplier = Mathf.Clamp(speed, 0.5f, 2f);
        }

        private void Update()
        {
            if (!_isPlaying || _isPaused || _beatmap == null) return;

            double sweep = SongTimeWithLead;
            while (_pendingNotes.Count > 0 && _pendingNotes.Peek().HitTime <= sweep)
            {
                NoteData note = _pendingNotes.Dequeue();
                MaybeRaiseSongInteractionStarted();
                OnNoteShouldSpawn?.Invoke(note);
            }

            MaybeRaiseSongInteractionEnded();

            if (_pendingNotes.Count == 0 && SongTime >= _beatmap.SongLengthSeconds)
            {
                if (LoopOnEnd)
                {
                    RestartLoopNow();
                    return;
                }

                if (!_hasPostedSongEnd)
                {
                    _hasPostedSongEnd = true;
                    _isPlaying = false;
                    EventBus.RaiseSongEnded(true);
                }
            }
        }

        private void RestartLoopNow()
        {
            if (_beatmap == null || _bgmSource == null || _bgmSource.clip == null) return;

            _pendingNotes = new Queue<NoteData>(_beatmap.Notes);
            _hasPostedSongInteractionStart = false;
            _hasPostedSongInteractionEnd = false;
            _lastInteractiveSongTime = CalculateLastInteractiveSongTime(_beatmap);
            _dspStartTime = AudioSettings.dspTime;
            _bgmSource.Stop();
            _bgmSource.time = 0f;
            _bgmSource.pitch = _playbackSpeed;
            _bgmSource.Play();
        }

        private void MaybeRaiseSongInteractionEnded()
        {
            if (_hasPostedSongInteractionEnd || _beatmap == null) return;
            if (SongTime < _lastInteractiveSongTime + _postLastNoteOxygenGraceSeconds) return;

            _hasPostedSongInteractionEnd = true;
            EventBus.RaiseSongInteractionEnded();
        }

        private void MaybeRaiseSongInteractionStarted()
        {
            if (_hasPostedSongInteractionStart) return;

            _hasPostedSongInteractionStart = true;
            EventBus.RaiseSongInteractionStarted();
        }

        private double CalculateLastInteractiveSongTime(BeatmapData beatmap)
        {
            if (beatmap == null || beatmap.Notes == null || beatmap.Notes.Count == 0)
            {
                return 0d;
            }

            double lastTime = 0d;
            float bubbleWindow = _judgementConfig != null ? _judgementConfig.GoodWindow : 0.1f;
            float fishMinDuration = _judgementConfig != null ? _judgementConfig.FishStrokeMinDuration : 0.8f;

            for (int i = 0; i < beatmap.Notes.Count; i++)
            {
                NoteData note = beatmap.Notes[i];
                double interactionEnd = note.HitTime;
                if (note.Type == NoteType.Fish)
                {
                    interactionEnd += System.Math.Max(note.Duration, fishMinDuration);
                }
                else
                {
                    interactionEnd += bubbleWindow;
                }

                if (interactionEnd > lastTime) lastTime = interactionEnd;
            }

            return lastTime;
        }

        private void OnDisable()
        {
            if (_isPlaying)
            {
                _bgmSource?.Stop();
                _isPlaying = false;
                _isPaused = false;
                EventBus.RaiseGameplayPauseChanged(false);

                if (!_hasPostedSongEnd)
                {
                    _hasPostedSongEnd = true;
                    EventBus.RaiseSongEnded(false);
                }
            }
        }
    }
}
