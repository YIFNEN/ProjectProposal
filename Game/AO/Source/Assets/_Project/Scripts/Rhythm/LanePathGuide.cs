using UnityEngine;

namespace AO.Rhythm
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LanePathGuide : MonoBehaviour
    {
        private static readonly Lane[] VisibleLanes =
        {
            Lane.Up,
            Lane.Left,
            Lane.Center,
            Lane.Right,
            Lane.Down
        };

        [Header("Anchors")]
        [SerializeField] private Transform _spawnAnchor;
        [SerializeField] private Transform _hitAnchor;

        [Header("Lane Offsets")]
        [SerializeField] private Vector3 _laneUpOffset = LaneLayout.UpperLeftOffset;
        [SerializeField] private Vector3 _laneDownOffset = LaneLayout.LowerRightOffset;
        [SerializeField] private Vector3 _laneLeftOffset = LaneLayout.LowerLeftOffset;
        [SerializeField] private Vector3 _laneRightOffset = LaneLayout.UpperRightOffset;
        [SerializeField] private Vector3 _laneCenterOffset = LaneLayout.CenterOffset;
        [SerializeField] private Vector3 _sharedSpawnOffset = LaneLayout.TopMidSpawnOffset;

        [Header("Visual")]
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Color _lineColor = new Color(0.16f, 0.78f, 1f, 0.2f);
        [SerializeField] private Color _hitColor = new Color(0.66f, 1f, 0.92f, 0.74f);
        [SerializeField, Range(0.002f, 0.08f)] private float _lineWidth = 0.022f;
        [SerializeField, Range(0.05f, 1f)] private float _spawnWidthMultiplier = 0.28f;
        [SerializeField, Range(0.1f, 2f)] private float _hitWidthMultiplier = 1f;
        [SerializeField, Range(0, 16)] private int _capVertices = 8;
        [SerializeField] private bool _animatePulse = true;
        [SerializeField, Range(0f, 4f)] private float _pulseHz = 0.85f;
        [SerializeField, Range(0f, 0.5f)] private float _pulseAmount = 0.12f;
        [SerializeField] private bool _showGuide = true;
        [SerializeField] private bool _allowRuntimeLineCreation = false;

        private LineRenderer[] _lines;

        private void OnEnable()
        {
            AutoFindAnchors();
            Rebuild();
        }

        private void LateUpdate()
        {
            UpdateLines();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            Rebuild();
        }

        public void Rebuild()
        {
            EnsureLines();
            UpdateLines();
        }

        public Vector3 GetConfiguredLaneOffset(Lane lane)
        {
            return GetLaneOffset(lane);
        }

        public Vector3 SharedSpawnOffset => _sharedSpawnOffset;

        public void ConfigureLaneOffsets(Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center)
        {
            _laneUpOffset = up;
            _laneDownOffset = down;
            _laneLeftOffset = left;
            _laneRightOffset = right;
            _laneCenterOffset = center;
            Rebuild();
        }

        public void ConfigureSharedSpawnOffset(Vector3 sharedSpawnOffset)
        {
            _sharedSpawnOffset = sharedSpawnOffset;
            Rebuild();
        }

        private void AutoFindAnchors()
        {
            if (_spawnAnchor == null)
            {
                GameObject spawn = GameObject.Find("SpawnAnchor");
                if (spawn != null) _spawnAnchor = spawn.transform;
            }

            if (_hitAnchor == null)
            {
                GameObject hit = GameObject.Find("HitAnchor");
                if (hit != null) _hitAnchor = hit.transform;
            }
        }

        private void EnsureLines()
        {
            bool allowCreate = !Application.isPlaying || _allowRuntimeLineCreation;
            if (_lines == null || _lines.Length != VisibleLanes.Length)
            {
                _lines = new LineRenderer[VisibleLanes.Length];
            }

            Transform root = transform.Find("LanePathLines");
            if (root == null)
            {
                if (!allowCreate)
                {
                    Debug.LogError("[LanePathGuide] Required child 'LanePathLines' is missing. Runtime lane guide creation is disabled.", this);
                    return;
                }

                GameObject rootGo = new GameObject("LanePathLines");
                root = rootGo.transform;
                root.SetParent(transform, false);
            }

            for (int i = 0; i < VisibleLanes.Length; i++)
            {
                string childName = $"LanePath_{VisibleLanes[i]}";
                Transform child = root.Find(childName);
                if (child == null)
                {
                    if (!allowCreate)
                    {
                        Debug.LogError($"[LanePathGuide] Required lane guide child '{childName}' is missing. Runtime lane guide creation is disabled.", this);
                        continue;
                    }

                    GameObject go = new GameObject(childName);
                    child = go.transform;
                    child.SetParent(root, false);
                }

                LineRenderer line = child.GetComponent<LineRenderer>();
                if (line == null)
                {
                    if (!allowCreate)
                    {
                        Debug.LogError($"[LanePathGuide] Required LineRenderer on '{childName}' is missing. Runtime component creation is disabled.", this);
                        continue;
                    }

                    line = child.gameObject.AddComponent<LineRenderer>();
                }

                ConfigureLine(line);
                _lines[i] = line;
            }
        }

        private void ConfigureLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = _lineWidth * _spawnWidthMultiplier;
            line.endWidth = _lineWidth * _hitWidthMultiplier;
            line.numCapVertices = _capVertices;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = _lineColor;
            line.endColor = _hitColor;

            Material material = _lineMaterial != null ? _lineMaterial : RuntimeLineMaterial();
            if (material != null) line.sharedMaterial = material;
        }

        private void UpdateLines()
        {
            if (_lines == null) return;

            Vector3 spawn = (_spawnAnchor != null ? _spawnAnchor.position : transform.position) + _sharedSpawnOffset;
            Vector3 hitBase = _hitAnchor != null ? _hitAnchor.position : transform.position;

            for (int i = 0; i < _lines.Length; i++)
            {
                LineRenderer line = _lines[i];
                if (line == null) continue;

                float pulse = _animatePulse && _pulseHz > 0f
                    ? 1f + (Mathf.Sin(Time.unscaledTime * _pulseHz * Mathf.PI * 2f + i * 0.55f) + 1f) * 0.5f * _pulseAmount
                    : 1f;

                line.enabled = _showGuide;
                line.numCapVertices = _capVertices;
                line.startWidth = _lineWidth * _spawnWidthMultiplier * pulse;
                line.endWidth = _lineWidth * _hitWidthMultiplier * pulse;
                line.startColor = _lineColor;
                line.endColor = _hitColor;
                line.SetPosition(0, spawn);
                line.SetPosition(1, hitBase + GetLaneOffset(VisibleLanes[i]));
            }
        }

        private Vector3 GetLaneOffset(Lane lane)
        {
            return lane switch
            {
                Lane.Up => _laneUpOffset,
                Lane.Down => _laneDownOffset,
                Lane.Left => _laneLeftOffset,
                Lane.Right => _laneRightOffset,
                Lane.Center => _laneCenterOffset,
                _ => Vector3.zero,
            };
        }

        private static Material RuntimeLineMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            Material material = new Material(shader)
            {
                name = "LanePathGuide_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
            return material;
        }
    }
}
