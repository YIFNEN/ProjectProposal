using System.Collections.Generic;
using AO.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AO.Audio
{
    [DisallowMultipleComponent]
    public class UiClickSfxPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource _source;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.16f;
        [SerializeField, Range(0f, 0.25f)] private float _minInterval = 0.045f;
        [SerializeField, Range(0.1f, 5f)] private float _rebindingInterval = 1f;

        private readonly HashSet<int> _boundButtons = new HashSet<int>();
        private float _lastPlayed = -999f;
        private float _nextBindTime;

        private void Awake()
        {
            EnsureSource();
        }

        private void Start()
        {
            BindSceneButtons();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextBindTime) return;
            _nextBindTime = Time.unscaledTime + _rebindingInterval;
            BindSceneButtons();
        }

        public void Play()
        {
            if (_clip == null || !EnsureSource()) return;

            float now = Time.unscaledTime;
            if (now - _lastPlayed < _minInterval) return;

            _lastPlayed = now;
            _source.PlayOneShot(_clip, _volume * PlayerProgress.GetSfxVolume());
        }

        private void BindSceneButtons()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.gameObject.scene != gameObject.scene) continue;

                int id = button.GetInstanceID();
                if (_boundButtons.Contains(id)) continue;

                button.onClick.AddListener(Play);
                _boundButtons.Add(id);
            }
        }

        private bool EnsureSource()
        {
            if (_source == null) _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            if (_source == null) return false;

            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            return true;
        }
    }
}
