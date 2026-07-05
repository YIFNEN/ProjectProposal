using UnityEngine;
using UnityEngine.EventSystems;

namespace AO.Core
{
    [RequireComponent(typeof(Collider))]
    public class EternalExitObject : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private bool _available;
        [SerializeField, Tooltip("Editor/desktop fallback. Quest controller ray clicks use OnPointerClick.")]
        private bool _allowMouseClick = true;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
            gameObject.SetActive(_available);
        }

        public void SetAvailable(bool available)
        {
            _available = available;
            gameObject.SetActive(available);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            RequestExit();
        }

        private void OnMouseDown()
        {
            if (!_allowMouseClick) return;
            RequestExit();
        }

        private void RequestExit()
        {
            if (!_available || GameplayRuntimeState.IsInputBlocked) return;
            EventBus.RaiseEternalExitRequested();
        }
    }
}
