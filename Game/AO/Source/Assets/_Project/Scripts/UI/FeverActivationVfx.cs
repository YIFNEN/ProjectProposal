using AO.Core;
using UnityEngine;

namespace AO.UI
{
    [DisallowMultipleComponent]
    public class FeverActivationVfx : MonoBehaviour
    {
        [SerializeField] private GameObject _activationPrefab;
        [SerializeField] private Transform _spawnAnchor;
        [SerializeField] private Vector3 _worldOffset = Vector3.zero;
        [SerializeField, Min(0f)] private float _lifetime = 1.2f;
        [SerializeField] private bool _matchAnchorRotation = true;
        [SerializeField] private bool _spawnInFrontOfMainCamera = true;
        [SerializeField, Range(0.25f, 4f)] private float _cameraDistance = 1.15f;
        [SerializeField] private Vector2 _cameraViewOffset = new Vector2(0f, -0.05f);
        [SerializeField, Range(0.1f, 8f)] private float _scaleMultiplier = 2.4f;
        [SerializeField] private bool _forcePlay = true;

        private void OnEnable()
        {
            EventBus.FeverActivated += HandleFeverActivated;
        }

        private void OnDisable()
        {
            EventBus.FeverActivated -= HandleFeverActivated;
        }

        private void HandleFeverActivated()
        {
            if (_activationPrefab == null) return;

            Transform anchor = _spawnAnchor != null ? _spawnAnchor : transform;
            Vector3 position;
            Quaternion rotation;
            if (_spawnInFrontOfMainCamera && Camera.main != null)
            {
                Transform cameraTransform = Camera.main.transform;
                position = cameraTransform.position
                    + cameraTransform.forward * _cameraDistance
                    + cameraTransform.right * _cameraViewOffset.x
                    + cameraTransform.up * _cameraViewOffset.y
                    + _worldOffset;
                rotation = cameraTransform.rotation;
            }
            else
            {
                position = anchor.position + _worldOffset;
                rotation = _matchAnchorRotation ? anchor.rotation : Quaternion.identity;
            }

            GameObject instance = Instantiate(_activationPrefab, position, rotation);
            instance.transform.localScale *= _scaleMultiplier;

            if (_forcePlay)
            {
                ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particles.Length; i++)
                {
                    particles[i].Clear(true);
                    particles[i].Play(true);
                }
            }

            if (_lifetime > 0f)
            {
                Destroy(instance, _lifetime);
            }
        }
    }
}
