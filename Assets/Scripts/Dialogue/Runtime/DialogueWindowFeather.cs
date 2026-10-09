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
        List<float> ys = BuildAxis(rect.height, vertical, steps);
        Color baseColor = graphic.color;

        foreach (float y in ys)
        {
            foreach (float x in xs)
            {
                UIVertex vertex = seed;
                vertex.position = new Vector3(rect.xMin + x, rect.yMin + y, 0f);
                Color vertexColor = baseColor;
                vertexColor.a *= EdgeOpacity(x, rect.width, horizontal) *
                                 EdgeOpacity(y, rect.height, vertical);
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
