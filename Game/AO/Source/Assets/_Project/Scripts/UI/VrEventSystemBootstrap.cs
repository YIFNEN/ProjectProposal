using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AO.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EventSystem))]
    public class VrEventSystemBootstrap : MonoBehaviour
    {
        [SerializeField] private bool _disableStandaloneInputModule = true;
        [SerializeField] private bool _allowRuntimeComponentCreation = false;

        private void Awake()
        {
            EnsureSetup();
        }

        public void EnsureSetup()
        {
            EventSystem eventSystem = GetComponent<EventSystem>();
            eventSystem.sendNavigationEvents = true;

            XRUIInputModule xrModule = GetComponent<XRUIInputModule>();
            if (xrModule == null)
            {
                if (!_allowRuntimeComponentCreation)
                {
                    Debug.LogError("[VrEventSystemBootstrap] Required XRUIInputModule is missing. Runtime component creation is disabled.", this);
                    return;
                }

                xrModule = gameObject.AddComponent<XRUIInputModule>();
            }

            xrModule.enableXRInput = true;
            xrModule.enableMouseInput = true;
            xrModule.enableTouchInput = true;
            xrModule.enableGamepadInput = true;
            xrModule.enableJoystickInput = true;
            xrModule.enableBuiltinActionsAsFallback = true;

            if (_disableStandaloneInputModule)
            {
                StandaloneInputModule standalone = GetComponent<StandaloneInputModule>();
                if (standalone != null) standalone.enabled = false;
            }
        }
    }
}
