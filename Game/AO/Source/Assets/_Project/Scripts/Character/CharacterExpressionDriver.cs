using System.Collections.Generic;
using AO.Core;
using UniVRM10;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Character
{
    [DisallowMultipleComponent]
    public sealed class CharacterExpressionDriver : MonoBehaviour
    {
        private enum SceneExpressionMode
        {
            Auto,
            Title,
            Lobby,
            Gameplay,
            Result
        }

        private static readonly ExpressionKey[] DrivenKeys =
        {
            ExpressionKey.Happy,
            ExpressionKey.Relaxed,
            ExpressionKey.Surprised,
            ExpressionKey.Sad,
            ExpressionKey.Angry,
            ExpressionKey.Aa,
            ExpressionKey.Ih,
            ExpressionKey.Ou,
            ExpressionKey.Ee,
            ExpressionKey.Oh,
            ExpressionKey.Blink,
            ExpressionKey.BlinkLeft,
            ExpressionKey.BlinkRight
        };

        private static readonly ExpressionKey[] LobbyCycle =
        {
            ExpressionKey.Happy,
            ExpressionKey.Relaxed,
            ExpressionKey.Surprised,
            ExpressionKey.Sad,
            ExpressionKey.Angry,
            ExpressionKey.Aa,
            ExpressionKey.Ee,
            ExpressionKey.Oh,
            ExpressionKey.Ih,
            ExpressionKey.Ou,
            ExpressionKey.BlinkLeft,
            ExpressionKey.BlinkRight
        };

        private static readonly ExpressionKey[] ResultCycle =
        {
            ExpressionKey.Happy,
            ExpressionKey.Relaxed,
            ExpressionKey.Surprised,
            ExpressionKey.Sad
        };

        [Header("Mode")]
        [SerializeField] private SceneExpressionMode _mode = SceneExpressionMode.Auto;
        [SerializeField] private bool _listenToGameplayEvents = true;

        [Header("Blink")]
        [SerializeField] private bool _enableBlink = true;
        [SerializeField, Range(0.05f, 0.5f)] private float _blinkDuration = 0.12f;
        [SerializeField, Range(1f, 8f)] private float _blinkIntervalMin = 2.2f;
        [SerializeField, Range(1f, 10f)] private float _blinkIntervalMax = 4.6f;

        [Header("Blend")]
        [SerializeField, Range(1f, 20f)] private float _blendSpeed = 7f;
        [SerializeField, Range(0f, 1f)] private float _mouthCycleStrength = 0.48f;
        [SerializeField, Range(0f, 1f)] private float _emoteCycleStrength = 0.72f;
        [SerializeField, Range(0f, 1f)] private float _winkCycleStrength = 0.82f;

        [Header("Title")]
        [SerializeField, Range(0f, 1f)] private float _titleHappyWeight = 0.28f;
        [SerializeField, Range(0f, 1f)] private float _titleRelaxedWeight = 0.18f;

        [Header("Lobby")]
        [SerializeField, Range(0.4f, 6f)] private float _lobbyExpressionSeconds = 1.55f;
        [SerializeField, Range(0f, 2f)] private float _lobbyNeutralSeconds = 0.22f;
        [SerializeField, Range(0f, 1f)] private float _lobbyBaseRelaxedWeight = 0.1f;

        [Header("Gameplay")]
        [SerializeField, Range(0f, 1f)] private float _feverHappyWeight = 0.32f;
        [SerializeField, Range(0f, 1f)] private float _criticalSadWeight = 0.35f;

        [Header("Result")]
        [SerializeField, Range(0.5f, 8f)] private float _resultExpressionSeconds = 2.35f;
        [SerializeField, Range(0f, 1f)] private float _resultBaseRelaxedWeight = 0.16f;

        private readonly Dictionary<ExpressionKey, float> _currentWeights =
            new Dictionary<ExpressionKey, float>(ExpressionKey.Comparer);
        private readonly Dictionary<ExpressionKey, float> _targetWeights =
            new Dictionary<ExpressionKey, float>(ExpressionKey.Comparer);
        private readonly HashSet<ExpressionKey> _availableKeys =
            new HashSet<ExpressionKey>(ExpressionKey.Comparer);

        private Vrm10Instance _vrm;
        private bool _hasCachedKeys;
        private bool _oxygenCritical;
        private bool _feverActive;
        private bool _hasReaction;
        private ExpressionKey _reactionKey;
        private float _reactionWeight;
        private float _reactionUntil;
        private float _nextBlinkAt;
        private float _blinkStartedAt = -100f;

        private void Awake()
        {
            ResolveVrm();
            ScheduleNextBlink();
        }

        private void OnEnable()
        {
            if (!_listenToGameplayEvents) return;

            EventBus.NoteJudged += HandleNoteJudged;
            EventBus.FishStrokeSucceeded += HandleFishStrokeSucceeded;
            EventBus.FishStrokeFailed += HandleFishStrokeFailed;
            EventBus.OxygenCritical += HandleOxygenCritical;
            EventBus.OxygenRecovered += HandleOxygenRecovered;
            EventBus.GameOver += HandleGameOver;
            EventBus.FeverActivated += HandleFeverActivated;
            EventBus.FeverEnded += HandleFeverEnded;
            EventBus.SongStarted += HandleSongStarted;
        }

        private void OnDisable()
        {
            if (_listenToGameplayEvents)
            {
                EventBus.NoteJudged -= HandleNoteJudged;
                EventBus.FishStrokeSucceeded -= HandleFishStrokeSucceeded;
                EventBus.FishStrokeFailed -= HandleFishStrokeFailed;
                EventBus.OxygenCritical -= HandleOxygenCritical;
                EventBus.OxygenRecovered -= HandleOxygenRecovered;
                EventBus.GameOver -= HandleGameOver;
                EventBus.FeverActivated -= HandleFeverActivated;
                EventBus.FeverEnded -= HandleFeverEnded;
                EventBus.SongStarted -= HandleSongStarted;
            }

            ResetDrivenWeights();
        }

        private void Update()
        {
            if (!TryGetExpression(out Vrm10RuntimeExpression expression)) return;

            BuildTargets(ResolveMode());
            ApplyBlinkTarget();
            ApplyReactionTarget();
            BlendAndApply(expression);
        }

        public void ConfigureForSceneLabel(string sceneLabel)
        {
            _mode = SceneExpressionMode.Auto;
            if (string.IsNullOrEmpty(sceneLabel)) return;

            if (sceneLabel.Contains("Title"))
            {
                _mode = SceneExpressionMode.Title;
            }
            else if (sceneLabel.Contains("Lobby"))
            {
                _mode = SceneExpressionMode.Lobby;
            }
            else if (sceneLabel.Contains("Result"))
            {
                _mode = SceneExpressionMode.Result;
            }
            else if (sceneLabel.Contains("Game"))
            {
                _mode = SceneExpressionMode.Gameplay;
            }
        }

        private void ResolveVrm()
        {
            if (_vrm != null) return;
            _vrm = GetComponent<Vrm10Instance>();
            if (_vrm == null) _vrm = GetComponentInChildren<Vrm10Instance>(true);
            if (_vrm == null) _vrm = GetComponentInParent<Vrm10Instance>();
        }

        private bool TryGetExpression(out Vrm10RuntimeExpression expression)
        {
            expression = null;
            ResolveVrm();
            if (_vrm == null || _vrm.Vrm == null) return false;

            expression = _vrm.Runtime != null ? _vrm.Runtime.Expression : null;
            if (expression == null) return false;

            if (!_hasCachedKeys)
            {
                CacheAvailableKeys(expression);
            }

            return _availableKeys.Count > 0;
        }

        private void CacheAvailableKeys(Vrm10RuntimeExpression expression)
        {
            _availableKeys.Clear();
            IReadOnlyList<ExpressionKey> keys = expression.ExpressionKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                _availableKeys.Add(keys[i]);
            }

            for (int i = 0; i < DrivenKeys.Length; i++)
            {
                ExpressionKey key = DrivenKeys[i];
                if (!_availableKeys.Contains(key)) continue;
                _currentWeights[key] = expression.GetWeight(key);
                _targetWeights[key] = 0f;
            }

            _hasCachedKeys = true;
        }

        private SceneExpressionMode ResolveMode()
        {
            if (_mode != SceneExpressionMode.Auto) return _mode;

            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Title")) return SceneExpressionMode.Title;
            if (sceneName.Contains("Lobby")) return SceneExpressionMode.Lobby;
            if (sceneName.Contains("Result")) return SceneExpressionMode.Result;
            if (sceneName.Contains("GamePlay") || sceneName.Contains("Gameplay")) return SceneExpressionMode.Gameplay;
            return SceneExpressionMode.Gameplay;
        }

        private void BuildTargets(SceneExpressionMode mode)
        {
            ClearTargets();

            switch (mode)
            {
                case SceneExpressionMode.Title:
                    SetTarget(ExpressionKey.Happy, _titleHappyWeight);
                    SetTarget(ExpressionKey.Relaxed, _titleRelaxedWeight);
                    break;
                case SceneExpressionMode.Lobby:
                    SetTarget(ExpressionKey.Relaxed, _lobbyBaseRelaxedWeight);
                    ApplyCycle(LobbyCycle, _lobbyExpressionSeconds, _lobbyNeutralSeconds);
                    break;
                case SceneExpressionMode.Result:
                    SetTarget(ExpressionKey.Relaxed, _resultBaseRelaxedWeight);
                    ApplyCycle(ResultCycle, _resultExpressionSeconds, 0.35f);
                    break;
                case SceneExpressionMode.Gameplay:
                    if (_feverActive) SetTarget(ExpressionKey.Happy, _feverHappyWeight);
                    if (_oxygenCritical) SetTarget(ExpressionKey.Sad, _criticalSadWeight);
                    break;
            }
        }

        private void ApplyCycle(IReadOnlyList<ExpressionKey> cycle, float expressionSeconds, float neutralSeconds)
        {
            if (cycle == null || cycle.Count == 0) return;

            float activeSeconds = Mathf.Max(0.05f, expressionSeconds);
            float totalSeconds = Mathf.Max(0.05f, activeSeconds + Mathf.Max(0f, neutralSeconds));
            float time = Time.time;
            int index = Mathf.FloorToInt(time / totalSeconds) % cycle.Count;
            float phase = Mathf.Repeat(time, totalSeconds);
            if (phase > activeSeconds) return;

            float normalized = Mathf.Clamp01(phase / activeSeconds);
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            ExpressionKey key = cycle[index];
            SetTarget(key, GetCycleStrength(key) * envelope);
        }

        private float GetCycleStrength(ExpressionKey key)
        {
            if (key.IsMouth) return _mouthCycleStrength;
            if (key.IsBlink) return _winkCycleStrength;
            return _emoteCycleStrength;
        }

        private void ApplyBlinkTarget()
        {
            if (!_enableBlink) return;

            float now = Time.time;
            if (now >= _nextBlinkAt)
            {
                _blinkStartedAt = now;
                ScheduleNextBlink();
            }

            float t = (now - _blinkStartedAt) / Mathf.Max(0.01f, _blinkDuration);
            if (t < 0f || t > 1f) return;

            SetTarget(ExpressionKey.Blink, Mathf.Sin(t * Mathf.PI));
        }

        private void ApplyReactionTarget()
        {
            if (!_hasReaction) return;

            float remaining = _reactionUntil - Time.time;
            if (remaining <= 0f)
            {
                _hasReaction = false;
                return;
            }

            float fade = Mathf.Clamp01(remaining / 0.2f);
            SetTarget(_reactionKey, _reactionWeight * fade);
        }

        private void BlendAndApply(Vrm10RuntimeExpression expression)
        {
            float blend = 1f - Mathf.Exp(-_blendSpeed * Time.deltaTime);
            for (int i = 0; i < DrivenKeys.Length; i++)
            {
                ExpressionKey key = DrivenKeys[i];
                if (!_availableKeys.Contains(key)) continue;

                float current = _currentWeights.ContainsKey(key) ? _currentWeights[key] : 0f;
                float target = _targetWeights.ContainsKey(key) ? _targetWeights[key] : 0f;
                float next = Mathf.Lerp(current, target, blend);
                _currentWeights[key] = next;
                expression.SetWeight(key, next);
            }
        }

        private void ClearTargets()
        {
            for (int i = 0; i < DrivenKeys.Length; i++)
            {
                ExpressionKey key = DrivenKeys[i];
                if (_availableKeys.Contains(key)) _targetWeights[key] = 0f;
            }
        }

        private void SetTarget(ExpressionKey key, float weight)
        {
            if (!_availableKeys.Contains(key)) return;

            float clamped = Mathf.Clamp01(weight);
            if (_targetWeights.TryGetValue(key, out float existing))
            {
                _targetWeights[key] = Mathf.Max(existing, clamped);
            }
            else
            {
                _targetWeights.Add(key, clamped);
            }
        }

        private void ScheduleNextBlink()
        {
            float min = Mathf.Min(_blinkIntervalMin, _blinkIntervalMax);
            float max = Mathf.Max(_blinkIntervalMin, _blinkIntervalMax);
            _nextBlinkAt = Time.time + Random.Range(min, max);
        }

        private void ResetDrivenWeights()
        {
            if (!TryGetExpression(out Vrm10RuntimeExpression expression)) return;

            for (int i = 0; i < DrivenKeys.Length; i++)
            {
                ExpressionKey key = DrivenKeys[i];
                if (!_availableKeys.Contains(key)) continue;
                _currentWeights[key] = 0f;
                _targetWeights[key] = 0f;
                expression.SetWeight(key, 0f);
            }
        }

        private void HandleNoteJudged(NoteJudgedEvent e)
        {
            switch (e.Result)
            {
                case JudgementResult.Perfect:
                    TriggerReaction(ExpressionKey.Happy, 0.78f, 0.45f);
                    break;
                case JudgementResult.Good:
                    TriggerReaction(ExpressionKey.Relaxed, 0.5f, 0.35f);
                    break;
                case JudgementResult.Miss:
                    TriggerReaction(ExpressionKey.Sad, 0.72f, 0.7f);
                    break;
            }
        }

        private void HandleFishStrokeSucceeded()
        {
            TriggerReaction(ExpressionKey.Happy, 0.95f, 1f);
        }

        private void HandleFishStrokeFailed()
        {
            TriggerReaction(ExpressionKey.Angry, 0.68f, 0.65f);
        }

        private void HandleOxygenCritical()
        {
            _oxygenCritical = true;
            TriggerReaction(ExpressionKey.Sad, 0.82f, 1f);
        }

        private void HandleOxygenRecovered()
        {
            _oxygenCritical = false;
        }

        private void HandleGameOver()
        {
            _oxygenCritical = true;
            TriggerReaction(ExpressionKey.Sad, 1f, 2f);
        }

        private void HandleFeverActivated()
        {
            _feverActive = true;
            TriggerReaction(ExpressionKey.Surprised, 0.82f, 0.7f);
        }

        private void HandleFeverEnded()
        {
            _feverActive = false;
            TriggerReaction(ExpressionKey.Relaxed, 0.45f, 0.45f);
        }

        private void HandleSongStarted(double dspStart)
        {
            _oxygenCritical = false;
            _feverActive = false;
            _hasReaction = false;
        }

        private void TriggerReaction(ExpressionKey key, float weight, float duration)
        {
            _reactionKey = key;
            _reactionWeight = Mathf.Clamp01(weight);
            _reactionUntil = Time.time + Mathf.Max(0.05f, duration);
            _hasReaction = true;
        }
    }
}
