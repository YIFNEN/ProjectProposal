using AO.Core;
using TMPro;
using UnityEngine;

namespace AO.UI
{
    /// <summary>
    /// 콤보 표시 + 배율 표시. 콤보 변화 시 펀치 스케일 애니메이션.
    /// 0 콤보일 땐 텍스트 숨김.
    /// </summary>
    public class ComboCounter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _comboText;
        [SerializeField] private TMP_Text _multiplierText;

        [Header("Font")]
        [SerializeField] private bool _applyPreferredFont;

        [Header("Punch Animation")]
        [SerializeField, Range(0f, 0.5f)] private float _punchScale = 0.2f;
        [SerializeField, Range(0.05f, 1f)] private float _punchDuration = 0.15f;

        private Vector3 _originalScale;
        private float _punchTimer;

        private void Awake()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            if (_comboText == null && texts.Length > 0) _comboText = texts[0];
            if (_multiplierText == null && texts.Length > 1) _multiplierText = texts[1];
            if (_applyPreferredFont)
            {
                RuntimeUiFactory.ApplyPreferredFont(_comboText);
                RuntimeUiFactory.ApplyPreferredFont(_multiplierText);
            }
            _originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            EventBus.ComboChanged += HandleComboChanged;
            EventBus.MultiplierChanged += HandleMultiplierChanged;
            EventBus.SongStarted += HandleSongStarted;
            UpdateCombo(0);
            UpdateMultiplier(1f);
        }

        private void OnDisable()
        {
            EventBus.ComboChanged -= HandleComboChanged;
            EventBus.MultiplierChanged -= HandleMultiplierChanged;
            EventBus.SongStarted -= HandleSongStarted;
        }

        private void Update()
        {
            if (_punchTimer > 0f)
            {
                _punchTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_punchTimer / _punchDuration);
                transform.localScale = _originalScale * (1f + _punchScale * t);
                if (_punchTimer <= 0f) transform.localScale = _originalScale;
            }
        }

        private void HandleComboChanged(int combo)
        {
            UpdateCombo(combo);
            if (combo > 0) _punchTimer = _punchDuration;
        }

        private void HandleMultiplierChanged(float mult) => UpdateMultiplier(mult);

        private void HandleSongStarted(double _)
        {
            UpdateCombo(0);
            UpdateMultiplier(1f);
        }

        private void UpdateCombo(int combo)
        {
            if (_comboText != null) _comboText.text = combo > 0 ? $"{combo} COMBO" : "";
        }

        private void UpdateMultiplier(float mult)
        {
            if (_multiplierText != null) _multiplierText.text = mult > 1f ? $"x{mult:F1}" : "";
        }
    }
}
