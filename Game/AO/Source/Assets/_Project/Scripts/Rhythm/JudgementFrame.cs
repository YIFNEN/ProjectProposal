using UnityEngine;
using UnityEngine.Rendering;

namespace AO.Rhythm
{
    /// <summary>
    /// HitAnchor 주변에 판정 기준면을 희미한 사각 테두리로 표시한다.
    /// 실제 판정 로직에는 관여하지 않는 시각 가이드 전용 컴포넌트다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class JudgementFrame : MonoBehaviour
    {
        private const string FrameLineName = "FrameLine";

        [Header("Frame")]
        [SerializeField, Min(0.1f)] private float _width = 1.05f;
        [SerializeField, Min(0.1f)] private float _height = 1.05f;
        [SerializeField] private Vector3 _localOffset = Vector3.zero;
        [SerializeField, Min(0.001f)] private float _lineWidth = 0.012f;
        [SerializeField] private Color _lineColor = new Color(0.25f, 0.88f, 0.88f, 0.22f);
        [SerializeField] private bool _visible = true;

        [Header("Guides")]
        [SerializeField] private bool _showCenterCross = true;
        [SerializeField] private bool _showLaneMarkers = true;
        [SerializeField, Min(0.001f)] private float _guideLineWidth = 0.006f;
        [SerializeField, Min(0.005f)] private float _laneMarkerRadius = 0.035f;
        [SerializeField] private Color _guideColor = new Color(0.25f, 0.88f, 0.88f, 0.16f);
        [SerializeField] private Color _laneMarkerColor = new Color(1f, 0.92f, 0.38f, 0.45f);
        [SerializeField] private Vector3 _laneUpOffset = LaneLayout.UpperLeftOffset;
        [SerializeField] private Vector3 _laneDownOffset = LaneLayout.LowerRightOffset;
        [SerializeField] private Vector3 _laneLeftOffset = LaneLayout.LowerLeftOffset;
        [SerializeField] private Vector3 _laneRightOffset = LaneLayout.UpperRightOffset;
        [SerializeField] private Vector3 _laneCenterOffset = LaneLayout.CenterOffset;
        [SerializeField] private bool _allowRuntimeLineCreation = false;

        private LineRenderer _line;
        private Material _runtimeMaterial;
#if UNITY_EDITOR
        private bool _editorRebuildQueued;
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                QueueEditorRebuild();
                return;
            }
#endif
            Rebuild();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            QueueEditorRebuild();
#else
            Rebuild();
#endif
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= DelayedEditorRebuild;
#endif
            if (_runtimeMaterial == null) return;

            if (Application.isPlaying) Destroy(_runtimeMaterial);
            else DestroyImmediate(_runtimeMaterial);
        }

        public void Rebuild()
        {
            Rebuild(allowCreate: !Application.isPlaying || _allowRuntimeLineCreation);
        }

        public Vector3 GetConfiguredLaneOffset(Lane lane)
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

        public void ConfigureLaneOffsets(Vector3 up, Vector3 down, Vector3 left, Vector3 right, Vector3 center)
        {
            _laneUpOffset = up;
            _laneDownOffset = down;
            _laneLeftOffset = left;
            _laneRightOffset = right;
            _laneCenterOffset = center;
            Rebuild();
        }

        private void Rebuild(bool allowCreate)
        {
            _line = GetOrCreateLine(allowCreate);
            if (_line == null) return;

            float halfWidth = _width * 0.5f;
            float halfHeight = _height * 0.5f;

            ConfigureLine(_line, _visible, true, _lineWidth, _lineColor, 4);
            _line.SetPosition(0, _localOffset + new Vector3(-halfWidth, -halfHeight, 0f));
            _line.SetPosition(1, _localOffset + new Vector3(-halfWidth, halfHeight, 0f));
            _line.SetPosition(2, _localOffset + new Vector3(halfWidth, halfHeight, 0f));
            _line.SetPosition(3, _localOffset + new Vector3(halfWidth, -halfHeight, 0f));

            RebuildCenterCross(allowCreate, halfWidth, halfHeight);
            RebuildLaneMarkers(allowCreate);
        }

        private void RebuildCenterCross(bool allowCreate, float halfWidth, float halfHeight)
        {
            LineRenderer horizontal = GetOrCreateLine("CenterHorizontal", allowCreate);
            ConfigureLine(horizontal, _visible && _showCenterCross, false, _guideLineWidth, _guideColor, 2);
            if (horizontal != null)
            {
                horizontal.SetPosition(0, _localOffset + new Vector3(-halfWidth, 0f, 0f));
                horizontal.SetPosition(1, _localOffset + new Vector3(halfWidth, 0f, 0f));
            }

            LineRenderer vertical = GetOrCreateLine("CenterVertical", allowCreate);
            ConfigureLine(vertical, _visible && _showCenterCross, false, _guideLineWidth, _guideColor, 2);
            if (vertical != null)
            {
                vertical.SetPosition(0, _localOffset + new Vector3(0f, -halfHeight, 0f));
                vertical.SetPosition(1, _localOffset + new Vector3(0f, halfHeight, 0f));
            }
        }

        private void RebuildLaneMarkers(bool allowCreate)
        {
            RebuildLaneMarker("LaneMarker_Up", _laneUpOffset, allowCreate);
            RebuildLaneMarker("LaneMarker_Down", _laneDownOffset, allowCreate);
            RebuildLaneMarker("LaneMarker_Left", _laneLeftOffset, allowCreate);
            RebuildLaneMarker("LaneMarker_Right", _laneRightOffset, allowCreate);
            RebuildLaneMarker("LaneMarker_Center", _laneCenterOffset, allowCreate);
        }

        private void RebuildLaneMarker(string lineName, Vector3 laneOffset, bool allowCreate)
        {
            const int Segments = 16;

            LineRenderer marker = GetOrCreateLine(lineName, allowCreate);
            ConfigureLine(marker, _visible && _showLaneMarkers, true, _guideLineWidth, _laneMarkerColor, Segments);
            if (marker == null) return;

            Vector3 center = _localOffset + laneOffset;
            for (int i = 0; i < Segments; i++)
            {
                float angle = Mathf.PI * 2f * i / Segments;
                marker.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * _laneMarkerRadius);
            }
        }

        private void ConfigureLine(LineRenderer line, bool enabled, bool loop, float lineWidth, Color color, int positionCount)
        {
            if (line == null) return;

            line.enabled = enabled;
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = positionCount;
            line.widthMultiplier = lineWidth;
            line.startColor = color;
            line.endColor = color;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            Material material = GetOrCreateMaterial();
            if (material != null) line.material = material;
        }

        private LineRenderer GetOrCreateLine(bool allowCreate)
        {
            return GetOrCreateLine(FrameLineName, allowCreate);
        }

        private LineRenderer GetOrCreateLine(string lineName, bool allowCreate)
        {
            Transform existing = transform.Find(lineName);
            if (existing == null && !allowCreate)
            {
                Debug.LogError($"[JudgementFrame] Required line child '{lineName}' is missing. Runtime line creation is disabled.", this);
                return null;
            }

            GameObject lineObject = existing != null ? existing.gameObject : new GameObject(lineName);

            if (existing == null)
            {
                lineObject.transform.SetParent(transform, false);
            }

            lineObject.transform.localPosition = Vector3.zero;
            lineObject.transform.localRotation = Quaternion.identity;
            lineObject.transform.localScale = Vector3.one;

            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            if (line == null)
            {
                if (!allowCreate)
                {
                    Debug.LogError($"[JudgementFrame] Required LineRenderer on '{lineName}' is missing. Runtime component creation is disabled.", this);
                    return null;
                }

                line = lineObject.AddComponent<LineRenderer>();
            }

            return line;
        }

        private Material GetOrCreateMaterial()
        {
            if (_runtimeMaterial != null) return _runtimeMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning("[JudgementFrame] No supported line shader found. The frame may not render.");
                return null;
            }

            _runtimeMaterial = new Material(shader)
            {
                name = "M_JudgementFrame_Runtime",
                hideFlags = HideFlags.HideAndDontSave,
            };

            SetMaterialColor(_runtimeMaterial, Color.white);
            return _runtimeMaterial;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;

            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
        }

#if UNITY_EDITOR
        private void QueueEditorRebuild()
        {
            if (_editorRebuildQueued) return;

            _editorRebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += DelayedEditorRebuild;
        }

        private void DelayedEditorRebuild()
        {
            UnityEditor.EditorApplication.delayCall -= DelayedEditorRebuild;
            _editorRebuildQueued = false;

            if (this == null) return;
            Rebuild();
        }
#endif
    }
}
