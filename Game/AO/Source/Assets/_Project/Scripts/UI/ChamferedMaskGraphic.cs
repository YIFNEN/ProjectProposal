using UnityEngine;
using UnityEngine.UI;

namespace AO.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChamferedMaskGraphic : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float _cornerCut = 9f;

        public float CornerCut
        {
            get => _cornerCut;
            set
            {
                float next = Mathf.Max(0f, value);
                if (Mathf.Approximately(_cornerCut, next)) return;
                _cornerCut = next;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;
            float cut = Mathf.Min(_cornerCut, Mathf.Min(rect.width, rect.height) * 0.5f);
            Vector2 center = rect.center;
            Vector2[] corners =
            {
                new Vector2(rect.xMin + cut, rect.yMax),
                new Vector2(rect.xMax - cut, rect.yMax),
                new Vector2(rect.xMax, rect.yMax - cut),
                new Vector2(rect.xMax, rect.yMin + cut),
                new Vector2(rect.xMax - cut, rect.yMin),
                new Vector2(rect.xMin + cut, rect.yMin),
                new Vector2(rect.xMin, rect.yMin + cut),
                new Vector2(rect.xMin, rect.yMax - cut)
            };

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vertexHelper.AddVert(vertex);

            for (int i = 0; i < corners.Length; i++)
            {
                vertex.position = corners[i];
                vertexHelper.AddVert(vertex);
            }

            for (int i = 0; i < corners.Length; i++)
            {
                vertexHelper.AddTriangle(0, i + 1, ((i + 1) % corners.Length) + 1);
            }
        }
    }
}
