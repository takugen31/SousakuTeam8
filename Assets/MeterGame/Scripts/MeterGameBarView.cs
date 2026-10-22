using UnityEngine;
using UnityEngine.UI;

namespace SousakuTeam8.MeterGame
{
    /// <summary>Resolution-independent bar inspired by Art/meter_bar.png. No text or additional HUD.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MeterGameBarView : MaskableGraphic
    {
        [SerializeField, HideInInspector] private float gauge = 0.8f;
        [SerializeField, HideInInspector] private float heart;
        [SerializeField, HideInInspector] private float minimum = 0.65f;
        [SerializeField, HideInInspector] private float maximum = 0.95f;
        [SerializeField, HideInInspector] private bool inTarget = true;
        private MeterGameResult result;
        private static readonly Color Gold = new Color32(255, 201, 99, 255);
        private static readonly Color Pink = new Color32(255, 91, 162, 255);
        private static readonly Color Ink = new Color32(48, 20, 43, 255);
        private VertexHelper mesh;
        private bool drawGaugeGradient;

        public void Show(MeterGameState state)
        {
            gauge = state.Gauge;
            heart = state.Heart;
            minimum = state.TargetMinimum;
            maximum = state.TargetMaximum;
            inTarget = state.IsInTarget;
            result = state.Result;
            SetVerticesDirty();
        }

        public void Show(float gaugeValue, float heartValue, float targetMinimum, float targetMaximum, MeterGameResult gameResult)
        {
            gauge = Mathf.Clamp01(gaugeValue);
            heart = Mathf.Clamp01(heartValue);
            minimum = Mathf.Clamp01(targetMinimum);
            maximum = Mathf.Clamp(targetMaximum, minimum, 1f);
            inTarget = gauge >= minimum && gauge <= maximum;
            result = gameResult;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            mesh = vh;
            // Coordinates in a 1120 x 160 design canvas; the enclosing RectTransform
            // and CanvasScaler handle actual screen sizes.
            Capsule(new Rect(20, 27, 1078, 100), new Color(0f, 0f, 0f, 0.24f));
            Capsule(new Rect(24, 37, 1018, 96), Ink);
            Capsule(new Rect(27, 40, 1012, 90), Gold);
            Capsule(new Rect(31, 44, 1004, 82), Pink);
            Capsule(new Rect(36, 49, 994, 72), new Color32(255, 203, 219, 255));
            Capsule(new Rect(41, 54, 984, 62), Ink);
            var track = new Rect(56, 61, 928, 48);
            Capsule(track, new Color32(28, 28, 37, 255));

            if (gauge > 0f)
            {
                Color fill = result == MeterGameResult.Failure ? new Color32(139, 90, 122, 255) : Color.white;
                drawGaugeGradient = result != MeterGameResult.Failure;
                Capsule(new Rect(track.x, track.y, track.width * gauge, track.height), fill);
                drawGaugeGradient = false;
                Capsule(new Rect(track.x + 5, track.y + 29, Mathf.Max(0f, track.width * gauge - 10), 12), new Color(1f, 1f, 1f, 0.3f));
            }

            for (int i = 1; i < 10; i++)
                Quad(new Rect(track.x + track.width * i / 10f, 66, 2, 38), new Color(1f, 1f, 1f, 0.2f));

            float left = track.x + minimum * track.width;
            float right = track.x + maximum * track.width;
            Color target = inTarget ? new Color32(171, 255, 222, 255) : new Color32(182, 203, 255, 255);
            Quad(new Rect(left, 54, right - left, 62), new Color(target.r, target.g, target.b, 0.22f));
            Quad(new Rect(left, 52, right - left, 3), target);
            Quad(new Rect(left, 115, right - left, 3), target);
            Quad(new Rect(left, 52, 3, 66), target);
            Quad(new Rect(right - 3, 52, 3, 66), target);
            Triangle(new Vector2(left - 6, 136), new Vector2(left + 9, 136), new Vector2(left + 1.5f, 124), target);
            Triangle(new Vector2(right - 9, 136), new Vector2(right + 6, 136), new Vector2(right - 1.5f, 124), target);

            float cursor = track.x + gauge * track.width;
            Quad(new Rect(cursor - 5, 50, 10, 70), new Color(0.2f, 0.7f, 1f, 0.35f));
            Quad(new Rect(cursor - 1.5f, 51, 3, 68), Color.white);
            Triangle(new Vector2(cursor - 8, 36), new Vector2(cursor, 47), new Vector2(cursor + 8, 36), Color.white);

            Heart(new Vector2(44, 86), 40, Ink);
            Heart(new Vector2(44, 88), 36, Gold);
            Heart(new Vector2(44, 89), 30, Pink);
            Heart(new Vector2(1045, 84), 66, Ink);
            Heart(new Vector2(1045, 86), 61, Gold);
            Heart(new Vector2(1045, 87), 54, new Color32(84, 49, 71, 255));
            Color heartColor = result == MeterGameResult.Success ? new Color32(255, 174, 216, 255) : Pink;
            Heart(new Vector2(1045, 87), 54, heartColor, heart);
            // A small highlight makes the progress heart read as a glossy ornament.
            Capsule(new Rect(1021, 105, 14, 7), new Color(1f, 1f, 1f, 0.6f));
            mesh = null;
        }

        private Vector3 Position(Vector2 point)
        {
            Rect rect = rectTransform.rect;
            return new Vector3(rect.xMin + point.x / 1120f * rect.width,
                rect.yMin + point.y / 160f * rect.height, 0f);
        }

        private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(Position(a), VertexColor(a, tint), Vector2.zero);
            mesh.AddVert(Position(b), VertexColor(b, tint), Vector2.zero);
            mesh.AddVert(Position(c), VertexColor(c, tint), Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private Color VertexColor(Vector2 point, Color tint)
        {
            if (!drawGaugeGradient) return tint * color;
            float position = Mathf.Clamp01((point.x - 56f) / 928f);
            Color red = new Color32(255, 57, 86, 255);
            Color yellow = new Color32(255, 219, 51, 255);
            Color green = new Color32(108, 246, 128, 255);
            return (position < 0.5f ? Color.Lerp(red, yellow, position * 2f) :
                Color.Lerp(yellow, green, (position - 0.5f) * 2f)) * color;
        }

        private void Quad(Rect rect, Color tint)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            Vector2 a = new Vector2(rect.xMin, rect.yMin);
            Vector2 b = new Vector2(rect.xMin, rect.yMax);
            Vector2 c = new Vector2(rect.xMax, rect.yMax);
            Vector2 d = new Vector2(rect.xMax, rect.yMin);
            Triangle(a, b, c, tint);
            Triangle(a, c, d, tint);
        }

        private void Capsule(Rect rect, Color tint)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 previous = Vector2.zero;
            for (int i = 0; i <= 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48f;
                Vector2 corner = new Vector2(Mathf.Cos(angle) >= 0 ? rect.xMax - radius : rect.xMin + radius,
                    Mathf.Sin(angle) >= 0 ? rect.yMax - radius : rect.yMin + radius);
                Vector2 point = corner + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i > 0) Triangle(rect.center, point, previous, tint);
                previous = point;
            }
        }

        private static Vector2 HeartPoint(float angle, Vector2 center, float size)
        {
            float s = Mathf.Sin(angle);
            return center + new Vector2(s * s * s,
                (13f * Mathf.Cos(angle) - 5f * Mathf.Cos(2f * angle) - 2f * Mathf.Cos(3f * angle) - Mathf.Cos(4f * angle)) / 16f) * size;
        }

        private void Heart(Vector2 center, float size, Color tint, float fill = 1f)
        {
            if (fill <= 0f) return;
            float ceiling = center.y + Mathf.Lerp(-17f / 16f, 12f / 16f, fill) * size;
            Vector2 previous = HeartPoint(0f, center, size);
            for (int i = 1; i <= 64; i++)
            {
                Vector2 point = HeartPoint(i * Mathf.PI * 2f / 64f, center, size);
                ClippedTriangle(center, previous, point, ceiling, tint);
                previous = point;
            }
        }

        // Clip each triangle to the rising horizontal liquid surface.
        private void ClippedTriangle(Vector2 a, Vector2 b, Vector2 c, float ceiling, Color tint)
        {
            Vector2 first = Vector2.zero;
            Vector2 last = Vector2.zero;
            int count = 0;
            Vector2 previous = c;
            for (int i = 0; i < 3; i++)
            {
                Vector2 current = i == 0 ? a : i == 1 ? b : c;
                bool inside = current.y <= ceiling;
                if (inside != (previous.y <= ceiling))
                    AddClippedPoint(Vector2.Lerp(previous, current, (ceiling - previous.y) / (current.y - previous.y)),
                        ref first, ref last, ref count, tint);
                if (inside) AddClippedPoint(current, ref first, ref last, ref count, tint);
                previous = current;
            }
        }

        private void AddClippedPoint(Vector2 point, ref Vector2 first, ref Vector2 last, ref int count, Color tint)
        {
            if (count == 0) first = point;
            else if (count >= 2) Triangle(first, last, point, tint);
            last = point;
            count++;
        }
    }
}
