using UnityEngine;
using UnityEngine.UI;

namespace AO.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HitTargetCanvasAlwaysOnTop : MonoBehaviour
    {
        [SerializeField] private Material _alwaysOnTopMaterial;
        [SerializeField] private int _sortingOrder = 120;
        [SerializeField] private bool _overrideCanvasSorting = true;
        [SerializeField] private bool _applyToInactiveChildren = true;
        [SerializeField] private bool _disableRaycastTargets = true;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        [ContextMenu("Apply Always-On-Top Material")]
        public void Apply()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null && _overrideCanvasSorting)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = _sortingOrder;
            }

            if (_alwaysOnTopMaterial == null) return;

            Graphic[] graphics = GetComponentsInChildren<Graphic>(_applyToInactiveChildren);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null) continue;

                graphic.material = _alwaysOnTopMaterial;
                if (_disableRaycastTargets) graphic.raycastTarget = false;
            }
        }
    }
}
