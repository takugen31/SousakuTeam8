using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 既存のImage背景だけをメッシュでフェードさせる。文字や子オブジェクトは変更しない。
/// ラスター画像を作らず、Canvasの解像度に合わせて描画する。
/// </summary>
[AddComponentMenu("UI/Effects/Dialogue Window Feather")]
public sealed class DialogueWindowFeather : BaseMeshEffect
{
    [SerializeField, Tooltip("左右と上下の縁を透明にする幅（Canvasの基準ピクセル）。")]
    private Vector2 featherWidth = new Vector2(64f, 24f);

    [SerializeField, Range(2, 24), Tooltip("フェードの分割数。")]
    private int featherSteps = 8;

    [SerializeField, Tooltip("上下のフェード幅を別々に指定します。")]
    private bool separateVerticalEdges;
    [SerializeField, Min(0f)] private float topFeatherWidth = 8f;
    [SerializeField, Range(0f, 1f)] private float topEdgeOpacity = 0.8f;
    [SerializeField, Tooltip("重なる本文のImage。重なり部分が二重に暗くなるのを防ぎます。")]
    private Image blendIntoImage;

    public void ConfigureNamePlate(Image messageWindow)
    {
        featherWidth = new Vector2(48f, 0f);
        featherSteps = 8;
        separateVerticalEdges = true;
        topFeatherWidth = 8f;
        topEdgeOpacity = 0.8f;
        blendIntoImage = messageWindow;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount == 0) return;

        Rect rect = graphic.GetPixelAdjustedRect();
        UIVertex seed = UIVertex.simpleVert;
        vertices.PopulateUIVertex(ref seed, 0);
        vertices.Clear();
        if (rect.width <= 0f || rect.height <= 0f) return;

        float horizontal = Mathf.Clamp(featherWidth.x, 0f, rect.width * 0.5f);
        float vertical = Mathf.Clamp(featherWidth.y, 0f, rect.height * 0.5f);
        int steps = Mathf.Clamp(featherSteps, 2, 24);
        List<float> xs = BuildAxis(rect.width, horizontal, steps);
        List<float> ys = separateVerticalEdges
            ? BuildVerticalAxis(rect.height, steps)
            : BuildAxis(rect.height, vertical, steps);
        AddBlendSamples(ys, rect, steps);
        Color baseColor = graphic.color;

        foreach (float y in ys)
        {
            foreach (float x in xs)
            {
                UIVertex vertex = seed;
                vertex.position = new Vector3(rect.xMin + x, rect.yMin + y, 0f);
                Color vertexColor = baseColor;
                vertexColor.a *= OpacityAt(x, y, rect);
                vertexColor.a = BlendAlpha(vertexColor.a, vertex.position);
                vertex.color = vertexColor;
                vertices.AddVert(vertex);
            }
        }

        for (int y = 0; y < ys.Count - 1; y++)
        {
            for (int x = 0; x < xs.Count - 1; x++)
            {
                int bottomLeft = y * xs.Count + x;
                vertices.AddTriangle(bottomLeft, bottomLeft + xs.Count, bottomLeft + xs.Count + 1);
                vertices.AddTriangle(bottomLeft, bottomLeft + xs.Count + 1, bottomLeft + 1);
            }
        }
    }

    private float OpacityAt(float x, float y, Rect rect)
    {
        if (x < 0f || y < 0f || x > rect.width || y > rect.height) return 0f;
        float horizontal = Mathf.Clamp(featherWidth.x, 0f, rect.width * 0.5f);
        float vertical = Mathf.Clamp(featherWidth.y, 0f, rect.height * 0.5f);
        float verticalOpacity = EdgeOpacity(y, rect.height, vertical);
        if (separateVerticalEdges)
        {
            float width = Mathf.Clamp(topFeatherWidth, 0f, rect.height);
            float fade = width > 0f ? Mathf.Clamp01((rect.height - y) / width) : 1f;
            fade = fade * fade * (3f - 2f * fade);
            verticalOpacity = Mathf.Lerp(Mathf.Clamp01(topEdgeOpacity), 1f, fade);
        }
        return EdgeOpacity(x, rect.width, horizontal) * verticalOpacity;
    }

    private List<float> BuildVerticalAxis(float height, int steps)
    {
        float width = Mathf.Clamp(topFeatherWidth, 0f, height);
        var positions = new List<float> { 0f };
        for (int i = steps; i >= 0; i--)
        {
            float y = height - width * i / steps;
            if (y > positions[positions.Count - 1]) positions.Add(y);
        }
        return positions;
    }

    private void AddBlendSamples(List<float> ys, Rect rect, int steps)
    {
        if (blendIntoImage == null || blendIntoImage == graphic) return;
        var targetEffect = blendIntoImage.GetComponent<DialogueWindowFeather>();
        if (targetEffect == null || !targetEffect.IsActive()) return;
        Rect targetRect = blendIntoImage.GetPixelAdjustedRect();
        float width = Mathf.Clamp(targetEffect.featherWidth.y, 0f, targetRect.height * 0.5f);
        for (int i = 0; i <= steps; i++)
        {
            Vector3 world = blendIntoImage.rectTransform.TransformPoint(
                new Vector3(targetRect.xMin, targetRect.yMax - width * i / steps, 0f));
            float y = graphic.rectTransform.InverseTransformPoint(world).y - rect.yMin;
            if (y > 0f && y < rect.height && !ys.Contains(y)) ys.Add(y);
        }
        ys.Sort();
    }

    private float BlendAlpha(float alpha, Vector3 localPosition)
    {
        if (blendIntoImage == null || blendIntoImage == graphic || !blendIntoImage.isActiveAndEnabled)
            return alpha;
        Vector3 world = graphic.rectTransform.TransformPoint(localPosition);
        Vector3 targetLocal = blendIntoImage.rectTransform.InverseTransformPoint(world);
        Rect targetRect = blendIntoImage.GetPixelAdjustedRect();
        if (!targetRect.Contains(new Vector2(targetLocal.x, targetLocal.y))) return alpha;
        var targetEffect = blendIntoImage.GetComponent<DialogueWindowFeather>();
        float underneath = blendIntoImage.color.a;
        if (targetEffect != null && targetEffect.IsActive())
            underneath *= targetEffect.OpacityAt(targetLocal.x - targetRect.xMin,
                targetLocal.y - targetRect.yMin, targetRect);
        return CompositeOverlayAlpha(alpha, graphic.color.a, underneath);
    }

    public static float CompositeOverlayAlpha(float alpha, float centerAlpha, float underneath)
    {
        if (centerAlpha <= 0f || underneath >= 1f) return 0f;
        return alpha * Mathf.Clamp01((centerAlpha - underneath) / (centerAlpha * (1f - underneath)));
    }

    private static List<float> BuildAxis(float length, float fadeWidth, int steps)
    {
        if (fadeWidth <= 0f) return new List<float> { 0f, length };

        var positions = new List<float>(2 * (steps + 1));
        for (int i = 0; i <= steps; i++) positions.Add(fadeWidth * i / steps);
        for (int i = steps; i >= 0; i--)
        {
            float position = length - fadeWidth * i / steps;
            if (position > positions[positions.Count - 1]) positions.Add(position);
        }
        return positions;
    }

    public static float EdgeOpacity(float position, float length, float fadeWidth)
    {
        if (fadeWidth <= 0f) return 1f;
        float distance = Mathf.Min(position, length - position);
        float t = Mathf.Clamp01(distance / fadeWidth);
        return t * t * (3f - 2f * t);
    }
}
