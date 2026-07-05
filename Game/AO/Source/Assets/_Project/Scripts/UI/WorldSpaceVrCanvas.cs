using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AO.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class WorldSpaceVrCanvas : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _minimumDynamicPixelsPerUnit = 80f;
        [SerializeField, Min(1f)] private float _minimumReferencePixelsPerUnit = 100f;
        [SerializeField] private bool _allowRuntimeComponentCreation = false;

        private void Awake() => EnsureSetup();

        private void OnEnable() => EnsureSetup();

        private void OnValidate() => EnsureSetup();

        public void EnsureSetup()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;

            bool allowCreate = !Application.isPlaying || _allowRuntimeComponentCreation;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                if (!allowCreate)
                {
                    Debug.LogError("[WorldSpaceVrCanvas] Required CanvasScaler is missing. Runtime component creation is disabled.", this);
                }
                else
                {
                    scaler = gameObject.AddComponent<CanvasScaler>();
                }
            }

            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = Mathf.Max(scaler.dynamicPixelsPerUnit, _minimumDynamicPixelsPerUnit);
                scaler.referencePixelsPerUnit = Mathf.Max(scaler.referencePixelsPerUnit, _minimumReferencePixelsPerUnit);
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                if (!allowCreate) Debug.LogError("[WorldSpaceVrCanvas] Required GraphicRaycaster is missing. Runtime component creation is disabled.", this);
                else gameObject.AddComponent<GraphicRaycaster>();
            }

            if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                if (!allowCreate) Debug.LogError("[WorldSpaceVrCanvas] Required TrackedDeviceGraphicRaycaster is missing. Runtime component creation is disabled.", this);
                else gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
        }
    }
}
