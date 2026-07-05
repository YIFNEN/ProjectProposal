using System.Collections.Generic;
using AO.Notes;
using AO.Rhythm;
using UnityEngine;

namespace AO.Debugging
{
    [DisallowMultipleComponent]
    public sealed class GameplayHitDebugVisualizer : MonoBehaviour
    {
        private const int MaxBubbleMarkers = 24;

        [Header("References")]
        [SerializeField] private Transform _hitAnchor;
        [SerializeField] private NoteSpawner _noteSpawner;
        [SerializeField] private Transform _leftJudgementHand;
        [SerializeField] private Transform _rightJudgementHand;
        [SerializeField] private Transform _leftVisualHand;
        [SerializeField] private Transform _rightVisualHand;

        [Header("Visibility")]
        [SerializeField] private bool _showJudgementHands = true;
        [SerializeField] private bool _showVisualHands = true;
        [SerializeField] private bool _showLaneHitPoints = true;
        [SerializeField] private bool _showActiveBubbleOverlap = true;

        [Header("Sizes")]
        [SerializeField, Min(0.01f)] private float _defaultHandRadius = 0.12f;
        [SerializeField, Min(0.005f)] private float _handCenterRadius = 0.035f;
        [SerializeField, Min(0.005f)] private float _visualHandRadius = 0.025f;
        [SerializeField, Min(0.005f)] private float _lanePointRadius = 0.045f;
        [SerializeField, Min(0.005f)] private float _bubbleCenterRadius = 0.025f;

        private Transform _visualRoot;
        private Marker _leftHandVolume;
        private Marker _rightHandVolume;
        private Marker _leftHandCenter;
        private Marker _rightHandCenter;
        private Marker _leftVisualHandMarker;
        private Marker _rightVisualHandMarker;
        private readonly Dictionary<Lane, Marker> _laneMarkers = new();
        private readonly Dictionary<Lane, LineRenderer> _laneLines = new();
        private readonly List<Marker> _bubbleVolumes = new();
        private readonly List<Marker> _bubbleCenters = new();
        private readonly BubbleNote[] _bubbleBuffer = new BubbleNote[MaxBubbleMarkers];

        private Material _leftHandMaterial;
        private Material _rightHandMaterial;
        private Material _visualHandMaterial;
        private Material _laneMaterial;
        private Material _bubbleNearMaterial;
        private Material _bubbleFarMaterial;
        private Material _lineMaterial;

        private static readonly Lane[] Lanes =
        {
            Lane.Up,
            Lane.Right,
            Lane.Left,
            Lane.Down,
            Lane.Center,
        };

        private void Awake()
        {
            ResolveReferences();
            EnsureVisuals();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureVisuals();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureVisuals();

            UpdateHandMarkers();
            UpdateLaneMarkers();
            UpdateBubbleMarkers();
        }

        [ContextMenu("Resolve References")]
        public void ResolveReferences()
        {
            if (_hitAnchor == null)
            {
                GameObject hitAnchorObject = GameObject.Find("HitAnchor");
                if (hitAnchorObject != null) _hitAnchor = hitAnchorObject.transform;
            }

            if (_noteSpawner == null)
            {
                _noteSpawner = FindFirstObjectByType<NoteSpawner>(FindObjectsInactive.Include);
            }

            if (_leftJudgementHand == null || _rightJudgementHand == null)
            {
                GameObject judgementRig = GameObject.Find("JudgementRig");
                if (judgementRig != null)
                {
                    Transform rigTransform = judgementRig.transform;
                    if (_leftJudgementHand == null) _leftJudgementHand = rigTransform.Find("LeftHandTarget");
                    if (_rightJudgementHand == null) _rightJudgementHand = rigTransform.Find("RightHandTarget");
                }
            }

            if (_leftVisualHand == null || _rightVisualHand == null)
            {
                GameObject visualTargets = GameObject.Find("VisualHandTargets");
                if (visualTargets != null)
                {
                    Transform visualTransform = visualTargets.transform;
                    if (_leftVisualHand == null) _leftVisualHand = visualTransform.Find("LeftHandTarget");
                    if (_rightVisualHand == null) _rightVisualHand = visualTransform.Find("RightHandTarget");
                }
            }
        }

        private void EnsureVisuals()
        {
            EnsureMaterials();

            if (_visualRoot == null)
            {
                GameObject root = new GameObject("HitDebug_RuntimeVisuals");
                root.transform.SetParent(transform, false);
                _visualRoot = root.transform;
            }

            _leftHandVolume ??= CreateMarker("LeftJudgementHand_Radius", _leftHandMaterial);
            _rightHandVolume ??= CreateMarker("RightJudgementHand_Radius", _rightHandMaterial);
            _leftHandCenter ??= CreateMarker("LeftJudgementHand_Center", _leftHandMaterial);
            _rightHandCenter ??= CreateMarker("RightJudgementHand_Center", _rightHandMaterial);
            _leftVisualHandMarker ??= CreateMarker("LeftVisualHand_Center", _visualHandMaterial);
            _rightVisualHandMarker ??= CreateMarker("RightVisualHand_Center", _visualHandMaterial);

            foreach (Lane lane in Lanes)
            {
                if (!_laneMarkers.ContainsKey(lane))
                {
                    _laneMarkers.Add(lane, CreateMarker($"LaneHitPoint_{lane}", _laneMaterial));
                }

                if (!_laneLines.ContainsKey(lane))
                {
                    _laneLines.Add(lane, CreateLine($"LaneHitLine_{lane}"));
                }
            }

            while (_bubbleVolumes.Count < MaxBubbleMarkers)
            {
                _bubbleVolumes.Add(CreateMarker($"BubbleOverlapRadius_{_bubbleVolumes.Count:00}", _bubbleFarMaterial));
            }

            while (_bubbleCenters.Count < MaxBubbleMarkers)
            {
                _bubbleCenters.Add(CreateMarker($"BubbleCenter_{_bubbleCenters.Count:00}", _bubbleNearMaterial));
            }
        }

        private void EnsureMaterials()
        {
            _leftHandMaterial ??= CreateMaterial("HitDebug_LeftHand", new Color(0.1f, 0.9f, 1f, 0.28f));
            _rightHandMaterial ??= CreateMaterial("HitDebug_RightHand", new Color(1f, 0.35f, 0.9f, 0.28f));
            _visualHandMaterial ??= CreateMaterial("HitDebug_VisualHand", new Color(0.25f, 1f, 0.35f, 0.85f));
            _laneMaterial ??= CreateMaterial("HitDebug_LanePoint", new Color(1f, 0.95f, 0.15f, 0.8f));
            _bubbleNearMaterial ??= CreateMaterial("HitDebug_BubbleSpatialHit", new Color(0.2f, 1f, 0.35f, 0.32f));
            _bubbleFarMaterial ??= CreateMaterial("HitDebug_BubbleSpatialMiss", new Color(1f, 0.45f, 0.05f, 0.2f));
            _lineMaterial ??= CreateMaterial("HitDebug_Line", new Color(1f, 0.95f, 0.15f, 0.7f));
        }

        private void UpdateHandMarkers()
        {
            UpdateSphere(_leftHandVolume, _leftJudgementHand, GetColliderRadius(_leftJudgementHand), _showJudgementHands);
            UpdateSphere(_rightHandVolume, _rightJudgementHand, GetColliderRadius(_rightJudgementHand), _showJudgementHands);
            UpdateSphere(_leftHandCenter, _leftJudgementHand, _handCenterRadius, _showJudgementHands);
            UpdateSphere(_rightHandCenter, _rightJudgementHand, _handCenterRadius, _showJudgementHands);
            UpdateSphere(_leftVisualHandMarker, _leftVisualHand, _visualHandRadius, _showVisualHands);
            UpdateSphere(_rightVisualHandMarker, _rightVisualHand, _visualHandRadius, _showVisualHands);
        }

        private void UpdateLaneMarkers()
        {
            Vector3 center = _hitAnchor != null ? _hitAnchor.position : transform.position;
            foreach (Lane lane in Lanes)
            {
                Vector3 position = LaneHitPosition(lane);
                bool visible = _showLaneHitPoints && _hitAnchor != null;
                UpdateSphere(_laneMarkers[lane], position, _lanePointRadius, visible);

                LineRenderer line = _laneLines[lane];
                line.gameObject.SetActive(visible && lane != Lane.Center);
                if (line.gameObject.activeSelf)
                {
                    line.SetPosition(0, center);
                    line.SetPosition(1, position);
                }
            }
        }

        private void UpdateBubbleMarkers()
        {
            int activeCount = 0;
            if (_showActiveBubbleOverlap)
            {
                BubbleNote[] notes = FindObjectsByType<BubbleNote>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                int count = Mathf.Min(notes.Length, MaxBubbleMarkers);
                for (int i = 0; i < count; i++)
                {
                    _bubbleBuffer[i] = notes[i];
                }

                activeCount = count;
            }

            for (int i = 0; i < MaxBubbleMarkers; i++)
            {
                bool visible = i < activeCount && _bubbleBuffer[i] != null;
                if (!visible)
                {
                    _bubbleVolumes[i].SetActive(false);
                    _bubbleCenters[i].SetActive(false);
                    _bubbleBuffer[i] = null;
                    continue;
                }

                BubbleNote bubble = _bubbleBuffer[i];
                float radius = BubbleOverlapRadius(bubble);
                bool spatiallyReachable = IsAnyHandInside(bubble.transform.position, radius);
                _bubbleVolumes[i].SetMaterial(spatiallyReachable ? _bubbleNearMaterial : _bubbleFarMaterial);
                UpdateSphere(_bubbleVolumes[i], bubble.transform.position, radius, true);
                UpdateSphere(_bubbleCenters[i], bubble.transform.position, _bubbleCenterRadius, true);
                _bubbleBuffer[i] = null;
            }
        }

        private Vector3 LaneHitPosition(Lane lane)
        {
            Vector3 baseHit = _hitAnchor != null ? _hitAnchor.position : transform.position;
            Vector3 offset = _noteSpawner != null ? _noteSpawner.GetConfiguredLaneOffset(lane) : LaneLayout.GetDefaultOffset(lane);
            return baseHit + offset;
        }

        private float GetColliderRadius(Transform target)
        {
            if (target == null) return _defaultHandRadius;
            SphereCollider sphere = target.GetComponent<SphereCollider>();
            if (sphere == null) return _defaultHandRadius;
            Vector3 scale = target.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return sphere.radius * maxScale;
        }

        private static float BubbleOverlapRadius(BubbleNote bubble)
        {
            Collider noteCollider = bubble.GetComponent<Collider>();
            if (noteCollider == null) return 0.25f;
            Vector3 extents = noteCollider.bounds.extents;
            float noteRadius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            return Mathf.Max(0.05f, noteRadius + 0.14f);
        }

        private bool IsAnyHandInside(Vector3 position, float radius)
        {
            return IsTargetInside(_leftJudgementHand, position, radius)
                || IsTargetInside(_rightJudgementHand, position, radius);
        }

        private static bool IsTargetInside(Transform target, Vector3 position, float radius)
        {
            if (target == null) return false;
            SphereCollider sphere = target.GetComponent<SphereCollider>();
            float targetRadius = sphere != null ? sphere.radius * MaxAbs(target.lossyScale) : 0f;
            return Vector3.Distance(target.position, position) <= radius + targetRadius;
        }

        private static float MaxAbs(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Max(Mathf.Abs(value.y), Mathf.Abs(value.z)));
        }

        private void UpdateSphere(Marker marker, Transform target, float radius, bool visible)
        {
            if (target == null)
            {
                marker.SetActive(false);
                return;
            }

            UpdateSphere(marker, target.position, radius, visible);
        }

        private static void UpdateSphere(Marker marker, Vector3 position, float radius, bool visible)
        {
            marker.SetActive(visible);
            if (!visible) return;

            marker.Transform.position = position;
            marker.Transform.rotation = Quaternion.identity;
            marker.Transform.localScale = Vector3.one * Mathf.Max(0.001f, radius * 2f);
        }

        private Marker CreateMarker(string markerName, Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = markerName;
            marker.transform.SetParent(_visualRoot, false);

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;

            marker.SetActive(false);
            return new Marker(marker.transform, renderer);
        }

        private LineRenderer CreateLine(string lineName)
        {
            GameObject lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(_visualRoot, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.01f;
            line.endWidth = 0.01f;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            lineObject.SetActive(false);
            return line;
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                renderQueue = 3000,
            };

            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_ALPHABLEND_ON");

            return material;
        }

        private sealed class Marker
        {
            public Marker(Transform transform, Renderer renderer)
            {
                Transform = transform;
                Renderer = renderer;
            }

            public Transform Transform { get; }
            private Renderer Renderer { get; }

            public void SetActive(bool active)
            {
                if (Transform != null && Transform.gameObject.activeSelf != active)
                {
                    Transform.gameObject.SetActive(active);
                }
            }

            public void SetMaterial(Material material)
            {
                if (Renderer != null && Renderer.sharedMaterial != material)
                {
                    Renderer.sharedMaterial = material;
                }
            }
        }
    }
}
