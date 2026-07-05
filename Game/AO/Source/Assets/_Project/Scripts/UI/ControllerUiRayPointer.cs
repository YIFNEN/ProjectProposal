using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

namespace AO.UI
{
    /// <summary>
    /// Lightweight controller ray fallback for World Space Unity UI.
    /// It complements XRUIInputModule and keeps Title/Lobby/Result usable before the final XR rig is authored.
    /// </summary>
    public class ControllerUiRayPointer : MonoBehaviour
    {
        [SerializeField] private Transform _leftRayOrigin;
        [SerializeField] private Transform _rightRayOrigin;
        [SerializeField] private UnityEngine.Camera _eventCamera;
        [SerializeField] private float _maxDistance = 5f;
        [SerializeField] private bool _showRayLines = true;
        [SerializeField] private bool _hideRayWhenNoHitInGameplay = true;
        [SerializeField] private bool _allowRuntimeLineCreation = false;
        [SerializeField] private float _lineWidth = 0.02f;
        [SerializeField] private Color _lineColor = new Color(0.25f, 0.94f, 1f, 0.68f);
        [SerializeField] private Color _lineHitColor = new Color(0.72f, 1f, 0.88f, 0.95f);
        [SerializeField, Range(0f, 1f)] private float _lineEndAlphaMultiplier = 0.12f;
        [SerializeField, Range(1f, 3f)] private float _hitLineWidthMultiplier = 1.45f;
        [SerializeField, Range(0f, 6f)] private float _linePulseHz = 1.6f;
        [SerializeField, Range(0f, 0.5f)] private float _linePulseAmount = 0.16f;
        [SerializeField, Range(0, 16)] private int _lineCapVertices = 8;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private bool _showRaySparkles = true;
        [SerializeField, Tooltip("When enabled, the script rewrites the Sparkles ParticleSystem on Play. Keep this off when editing ray sparkle prefab visuals directly.")]
        private bool _configureRaySparklesAtRuntime;
        [SerializeField] private Material _sparkleMaterial;
        [SerializeField, Range(0f, 40f)] private float _sparkleRate = 11f;
        [SerializeField, Range(1f, 4f)] private float _sparkleHitRateMultiplier = 1.7f;
        [SerializeField, Range(0.005f, 0.08f)] private float _sparkleBaseSize = 0.026f;
        [SerializeField, Range(0.5f, 3f)] private float _sparkleHitSizeMultiplier = 1.25f;
        [SerializeField, Range(0.02f, 0.35f)] private float _sparkleCrossSection = 0.055f;

        private readonly PointerState _left = new PointerState(-20, XRNode.LeftHand);
        private readonly PointerState _right = new PointerState(-21, XRNode.RightHand);
        private Material _runtimeLineMaterial;
        private Material _runtimeSparkleMaterial;

        private void Awake()
        {
            if (_eventCamera == null) _eventCamera = Camera.main;
            EnsureLine(_left, "LeftUiRayLine");
            EnsureLine(_right, "RightUiRayLine");
        }

        private void Update()
        {
            if (_eventCamera == null) _eventCamera = Camera.main;
            ProcessPointer(_left, _leftRayOrigin);
            ProcessPointer(_right, _rightRayOrigin);
        }

        private void ProcessPointer(PointerState state, Transform origin)
        {
            if (origin == null || EventSystem.current == null)
            {
                SetLineVisible(state, false);
                return;
            }

            Ray ray = new Ray(origin.position, origin.forward);
            bool hasHit = TryGetUiHit(ray, out UiHit hit);
            if (!hasHit) hasHit = TryGetPhysicsClickHit(ray, out hit);

            Vector3 lineEnd = hasHit ? hit.WorldPosition : origin.position + origin.forward * _maxDistance;
            UpdateLine(state, origin.position, lineEnd, hasHit);

            GameObject target = hasHit ? hit.Target : null;
            PointerEventData eventData = BuildEventData(state, hit, target);

            if (state.Hovered != target)
            {
                if (state.Hovered != null) ExecuteEvents.Execute(state.Hovered, eventData, ExecuteEvents.pointerExitHandler);
                state.Hovered = target;
                if (state.Hovered != null)
                {
                    ExecuteEvents.Execute(state.Hovered, eventData, ExecuteEvents.pointerEnterHandler);
                    EventSystem.current.SetSelectedGameObject(state.Hovered);
                }
            }

            bool pressed = IsPressed(state);
            bool pressedThisFrame = pressed && !state.WasPressed;
            bool releasedThisFrame = !pressed && state.WasPressed;

            if (pressedThisFrame && target != null)
            {
                eventData.pressPosition = eventData.position;
                eventData.pointerPressRaycast = eventData.pointerCurrentRaycast;
                state.PressPosition = eventData.position;
                state.PressRaycast = eventData.pointerCurrentRaycast;
                state.Pressed = ExecuteEvents.ExecuteHierarchy(target, eventData, ExecuteEvents.pointerDownHandler);
                if (state.Pressed == null) state.Pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                eventData.pointerPress = state.Pressed;

                state.DragHandler = ExecuteEvents.GetEventHandler<IDragHandler>(target);
                eventData.pointerDrag = state.DragHandler;
                if (state.DragHandler != null)
                {
                    ExecuteEvents.Execute(state.DragHandler, eventData, ExecuteEvents.initializePotentialDrag);
                    ExecuteEvents.Execute(state.DragHandler, eventData, ExecuteEvents.beginDragHandler);
                }
            }

            if (pressed && state.DragHandler != null)
            {
                ExecuteEvents.Execute(state.DragHandler, eventData, ExecuteEvents.dragHandler);
            }

            if (releasedThisFrame)
            {
                if (state.Pressed != null) ExecuteEvents.Execute(state.Pressed, eventData, ExecuteEvents.pointerUpHandler);

                GameObject clickTarget = target != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) : null;
                if (state.Pressed != null && state.Pressed == clickTarget)
                {
                    ExecuteEvents.Execute(state.Pressed, eventData, ExecuteEvents.pointerClickHandler);
                }

                if (state.DragHandler != null)
                {
                    ExecuteEvents.Execute(state.DragHandler, eventData, ExecuteEvents.endDragHandler);
                }

                state.Pressed = null;
                state.DragHandler = null;
                state.PressPosition = Vector2.zero;
                state.PressRaycast = default;
            }

            state.WasPressed = pressed;
            state.LastScreenPosition = eventData.position;
        }

        private PointerEventData BuildEventData(PointerState state, UiHit hit, GameObject target)
        {
            Vector2 screenPosition = hit.EventCamera != null
                ? (Vector2)hit.EventCamera.WorldToScreenPoint(hit.WorldPosition)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                pointerId = state.PointerId,
                position = screenPosition,
                delta = screenPosition - state.LastScreenPosition,
                pressPosition = state.PressPosition,
                pointerPress = state.Pressed,
                pointerDrag = state.DragHandler,
                button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = new RaycastResult
                {
                    gameObject = target,
                    module = hit.RaycastModule,
                    distance = hit.Distance,
                    worldPosition = hit.WorldPosition,
                    worldNormal = hit.WorldNormal,
                    screenPosition = screenPosition,
                },
                pointerPressRaycast = state.PressRaycast,
            };

            return eventData;
        }

        private bool TryGetUiHit(Ray ray, out UiHit best)
        {
            best = default;
            best.Distance = float.MaxValue;

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace || !canvas.gameObject.activeInHierarchy) continue;
                RectTransform canvasRect = canvas.transform as RectTransform;
                if (canvasRect == null) continue;

                Plane plane = new Plane(canvas.transform.forward, canvas.transform.position);
                if (!plane.Raycast(ray, out float distance) || distance < 0f || distance > _maxDistance || distance >= best.Distance) continue;

                Vector3 worldPoint = ray.GetPoint(distance);
                Vector3 canvasLocal = canvasRect.InverseTransformPoint(worldPoint);
                if (!canvasRect.rect.Contains(new Vector2(canvasLocal.x, canvasLocal.y))) continue;

                GameObject target = FindTopGraphicTarget(canvas, worldPoint);
                if (target == null) continue;

                best = new UiHit
                {
                    Target = target,
                    WorldPosition = worldPoint,
                    WorldNormal = canvas.transform.forward,
                    Distance = distance,
                    EventCamera = canvas.worldCamera != null ? canvas.worldCamera : _eventCamera,
                    RaycastModule = canvas.GetComponent<GraphicRaycaster>(),
                };
            }

            return best.Target != null;
        }

        private bool TryGetPhysicsClickHit(Ray ray, out UiHit best)
        {
            best = default;
            RaycastHit[] hits = Physics.RaycastAll(ray, _maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null) continue;

                GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.collider.gameObject);
                if (handler == null) continue;

                best = new UiHit
                {
                    Target = handler,
                    WorldPosition = hit.point,
                    WorldNormal = hit.normal,
                    Distance = hit.distance,
                    EventCamera = _eventCamera,
                    RaycastModule = null,
                };
                return true;
            }

            return false;
        }

        private static GameObject FindTopGraphicTarget(Canvas canvas, Vector3 worldPoint)
        {
            IList<Graphic> graphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
            GameObject bestTarget = null;
            int bestDepth = int.MinValue;

            for (int i = 0; i < graphics.Count; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null || !graphic.raycastTarget || !graphic.isActiveAndEnabled || graphic.canvasRenderer.cull) continue;

                RectTransform rect = graphic.rectTransform;
                Vector3 local = rect.InverseTransformPoint(worldPoint);
                if (!rect.rect.Contains(new Vector2(local.x, local.y))) continue;

                GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(graphic.gameObject);
                if (handler == null) handler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(graphic.gameObject);
                if (handler == null) handler = ExecuteEvents.GetEventHandler<ISelectHandler>(graphic.gameObject);
                if (handler == null) continue;

                if (graphic.depth >= bestDepth)
                {
                    bestDepth = graphic.depth;
                    bestTarget = handler;
                }
            }

            return bestTarget;
        }

        private bool IsPressed(PointerState state)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(state.Node);
            return device.isValid
                && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed)
                && triggerPressed;
        }

        private void EnsureLine(PointerState state, string lineName)
        {
            if (state.Line != null) return;

            Transform existing = transform.Find(lineName);
            if (existing == null)
            {
                if (!_allowRuntimeLineCreation)
                {
                    Debug.LogError($"[ControllerUiRayPointer] Required UI ray line '{lineName}' is missing under '{name}'. Runtime line creation is disabled.", this);
                    return;
                }

                GameObject newLineObject = new GameObject(lineName);
                newLineObject.transform.SetParent(transform, false);
                existing = newLineObject.transform;
            }

            GameObject lineObject = existing.gameObject;
            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            if (line == null)
            {
                if (!_allowRuntimeLineCreation)
                {
                    Debug.LogError($"[ControllerUiRayPointer] Required LineRenderer on '{lineName}' is missing. Runtime component creation is disabled.", this);
                    return;
                }

                line = lineObject.AddComponent<LineRenderer>();
            }

            line.positionCount = 2;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = _lineCapVertices;
            line.numCornerVertices = 2;
            line.startWidth = _lineWidth;
            line.endWidth = _lineWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GetLineMaterial();
            line.startColor = _lineColor;
            line.endColor = _lineColor;
            state.Line = line;

            EnsureSparkles(state, lineName.Replace("Line", "Sparkles"));
        }

        private Material GetLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            if (_runtimeLineMaterial != null) return _runtimeLineMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _runtimeLineMaterial = new Material(shader)
            {
                name = "RuntimeUiRayLineMaterial"
            };
            return _runtimeLineMaterial;
        }

        private void EnsureSparkles(PointerState state, string sparklesName)
        {
            if (state.Sparkles != null) return;

            bool created = false;
            Transform existing = transform.Find(sparklesName);
            if (existing == null)
            {
                if (!_allowRuntimeLineCreation) return;

                GameObject newSparklesObject = new GameObject(sparklesName);
                newSparklesObject.transform.SetParent(transform, false);
                existing = newSparklesObject.transform;
                created = true;
            }

            ParticleSystem sparkles = existing.GetComponent<ParticleSystem>();
            if (sparkles == null)
            {
                if (!_allowRuntimeLineCreation) return;
                sparkles = existing.gameObject.AddComponent<ParticleSystem>();
                created = true;
            }

            if (created || _configureRaySparklesAtRuntime)
            {
                ConfigureSparkles(sparkles);
            }

            state.Sparkles = sparkles;
        }

        private void ConfigureSparkles(ParticleSystem sparkles)
        {
            ParticleSystem.MainModule main = sparkles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.015f);
            main.startSize = new ParticleSystem.MinMaxCurve(_sparkleBaseSize * 0.65f, _sparkleBaseSize * 1.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 1f, 0.92f, 0.65f), Color.white);

            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.one;
            shape.randomDirectionAmount = 0.16f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparkles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.74f, 1f, 0.94f, 1f), 0f),
                    new GradientColorKey(Color.white, 0.4f),
                    new GradientColorKey(new Color(0.58f, 1f, 0.92f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.16f),
                    new GradientAlphaKey(0.78f, 0.52f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = sparkles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.32f, 1f),
                new Keyframe(1f, 0.12f)));

            ParticleSystemRenderer renderer = sparkles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = GetSparkleMaterial();
        }

        private Material GetSparkleMaterial()
        {
            if (_sparkleMaterial != null) return _sparkleMaterial;
            if (_runtimeSparkleMaterial != null) return _runtimeSparkleMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _runtimeSparkleMaterial = new Material(shader)
            {
                name = "RuntimeUiRaySparkleMaterial"
            };
            return _runtimeSparkleMaterial;
        }

        private void UpdateLine(PointerState state, Vector3 start, Vector3 end, bool hit)
        {
            if (state.Line == null) return;
            if (!_showRayLines)
            {
                SetLineVisible(state, false);
                return;
            }

            if (!hit && ShouldHideRayWhenNoHit())
            {
                SetLineVisible(state, false);
                return;
            }

            state.Line.enabled = true;
            state.Line.SetPosition(0, start);
            state.Line.SetPosition(1, end);
            Color startColor = hit ? _lineHitColor : _lineColor;
            Color endColor = startColor;
            endColor.a *= _lineEndAlphaMultiplier;

            float pulse = _linePulseAmount > 0f && _linePulseHz > 0f
                ? 1f + (Mathf.Sin(Time.unscaledTime * _linePulseHz * Mathf.PI * 2f) + 1f) * 0.5f * _linePulseAmount
                : 1f;
            float width = _lineWidth * (hit ? _hitLineWidthMultiplier : 1f) * pulse;

            state.Line.numCapVertices = _lineCapVertices;
            state.Line.startColor = startColor;
            state.Line.endColor = endColor;
            state.Line.startWidth = width;
            state.Line.endWidth = width * 0.35f;
            UpdateSparkles(state, start, end, hit, width);
        }

        private bool ShouldHideRayWhenNoHit()
        {
            if (!_hideRayWhenNoHitInGameplay || SceneManager.GetActiveScene().name != "GamePlayScene")
            {
                return false;
            }

            return !IsGameplaySettingsOpen();
        }

        private static bool IsGameplaySettingsOpen()
        {
            GameplaySettingsOverlay[] overlays = FindObjectsByType<GameplaySettingsOverlay>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < overlays.Length; i++)
            {
                GameplaySettingsOverlay overlay = overlays[i];
                if (overlay != null && overlay.IsOpen)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateSparkles(PointerState state, Vector3 start, Vector3 end, bool hit, float lineWidth)
        {
            if (state.Sparkles == null) return;

            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (!_showRaySparkles || length <= 0.01f)
            {
                SetSparklesVisible(state, false);
                return;
            }

            Transform sparklesTransform = state.Sparkles.transform;
            sparklesTransform.position = Vector3.Lerp(start, end, 0.5f);
            sparklesTransform.rotation = Quaternion.FromToRotation(Vector3.forward, direction.normalized);

            float hitMultiplier = hit ? _sparkleHitSizeMultiplier : 1f;
            ParticleSystem.MainModule main = state.Sparkles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(_sparkleBaseSize * 0.65f * hitMultiplier, _sparkleBaseSize * 1.35f * hitMultiplier);

            ParticleSystem.ShapeModule shape = state.Sparkles.shape;
            shape.scale = new Vector3(_sparkleCrossSection, _sparkleCrossSection, length);

            ParticleSystem.EmissionModule emission = state.Sparkles.emission;
            emission.rateOverTime = _sparkleRate * (hit ? _sparkleHitRateMultiplier : 1f);

            SetSparklesVisible(state, true);
        }

        private void SetLineVisible(PointerState state, bool visible)
        {
            if (state.Line != null) state.Line.enabled = visible;
            SetSparklesVisible(state, visible && _showRaySparkles);
        }

        private static void SetSparklesVisible(PointerState state, bool visible)
        {
            if (state.Sparkles == null) return;

            if (visible)
            {
                if (!state.Sparkles.isPlaying) state.Sparkles.Play();
            }
            else if (state.Sparkles.isPlaying || state.Sparkles.isEmitting)
            {
                state.Sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private sealed class PointerState
        {
            public readonly int PointerId;
            public readonly XRNode Node;
            public bool WasPressed;
            public Vector2 LastScreenPosition;
            public Vector2 PressPosition;
            public RaycastResult PressRaycast;
            public GameObject Hovered;
            public GameObject Pressed;
            public GameObject DragHandler;
            public LineRenderer Line;
            public ParticleSystem Sparkles;

            public PointerState(int pointerId, XRNode node)
            {
                PointerId = pointerId;
                Node = node;
                LastScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
        }

        private struct UiHit
        {
            public GameObject Target;
            public Vector3 WorldPosition;
            public Vector3 WorldNormal;
            public float Distance;
            public UnityEngine.Camera EventCamera;
            public BaseRaycaster RaycastModule;
        }
    }
}
