using AO.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Audio
{
    [DisallowMultipleComponent]
    public class MenuBgmPlayer : MonoBehaviour
    {
        private static MenuBgmPlayer _instance;
        private static bool _previewDucked;

        [SerializeField] private AudioSource _source;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volumeScale = 0.38f;
        [SerializeField, Range(0f, 1f)] private float _previewDuckScale = 0.35f;
        [SerializeField, Range(0.05f, 2f)] private float _fadeSeconds = 0.65f;

        private bool _fadeOutAndDestroy;

        public static void SetPreviewDucked(bool ducked)
        {
            _previewDucked = ducked;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSource();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            if (IsGameplayScene(SceneManager.GetActiveScene()))
            {
                FadeOutAndDestroy();
            }
            else
            {
                PlayMenuBgm();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                _instance = null;
            }
        }

        private void Update()
        {
            if (_source == null) return;

            float target = _fadeOutAndDestroy ? 0f : TargetVolume();
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, _fadeSeconds);
            _source.volume = Mathf.MoveTowards(_source.volume, target, step);

            if (_fadeOutAndDestroy && _source.volume <= 0.001f)
            {
                Destroy(gameObject);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsGameplayScene(scene))
            {
                FadeOutAndDestroy();
                return;
            }

            _fadeOutAndDestroy = false;
            PlayMenuBgm();
        }

        private void PlayMenuBgm()
        {
            if (_clip == null || !EnsureSource()) return;

            if (_source.clip != _clip)
            {
                _source.clip = _clip;
                _source.time = 0f;
            }

            _source.loop = true;
            _source.pitch = 1f;
            if (!_source.isPlaying) _source.Play();
        }

        private void FadeOutAndDestroy()
        {
            _fadeOutAndDestroy = true;
            _previewDucked = false;
        }

        private bool EnsureSource()
        {
            if (_source == null) _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            if (_source == null) return false;

            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            return true;
        }

        private float TargetVolume()
        {
            float duck = _previewDucked ? _previewDuckScale : 1f;
            return PlayerProgress.GetBgmVolume() * _volumeScale * duck;
        }

        private static bool IsGameplayScene(Scene scene)
        {
            return scene.name.Contains("GamePlay") || scene.name.Contains("Gameplay");
        }
    }
}
