using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// Page-ready marker. Reuses the original character item art; never blocks input.
public sealed class DialogueAdvanceIndicator : MaskableGraphic
{
    [SerializeField] private NovelDialogueController dialogueController;
    [SerializeField] private Sprite kayoMark;
    [SerializeField] private Sprite moteruMark;
    [SerializeField] private Sprite yowashiMark;
    private Sprite currentSprite;
    private bool pageReady;

    public override Texture mainTexture => currentSprite != null
        ? currentSprite.texture : Texture2D.whiteTexture;

    public static void Create(Transform parent, NovelDialogueController controller,
        Sprite kayo, Sprite moteru, Sprite yowashi)
    {
        var markerObject = new GameObject("DialogueAdvanceIndicator",
            typeof(RectTransform), typeof(CanvasRenderer));
        markerObject.transform.SetParent(parent, false);
        var rect = (RectTransform)markerObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.95f, 0.14f);
        rect.sizeDelta = new Vector2(36f, 36f);
        var marker = markerObject.AddComponent<DialogueAdvanceIndicator>();
        marker.dialogueController = controller;
        marker.kayoMark = kayo;
        marker.moteruMark = moteru;
        marker.yowashiMark = yowashi;
        marker.raycastTarget = false;
    }

    private void LateUpdate()
    {
        bool ready = dialogueController != null && dialogueController.CanAdvanceCurrentPage;
        string speaker = dialogueController != null ? dialogueController.CurrentSpeakerId : null;
        Sprite sprite = speaker == "kayo" ? kayoMark
            : speaker == "moteru" ? moteruMark
            : speaker == "yowashi" ? yowashiMark : null;
        if (ready == pageReady && sprite == currentSprite) return;
        pageReady = ready;
        currentSprite = sprite;
        SetVerticesDirty();
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!pageReady) return;
        Rect rect = GetPixelAdjustedRect();
        if (currentSprite == null)
        {
            float radius = Mathf.Min(rect.width, rect.height) * 0.24f;
            Vector2 center = rect.center;
            mesh.AddVert(new Vector3(center.x - radius, center.y + radius * 0.6f), color, Vector2.zero);
            mesh.AddVert(new Vector3(center.x + radius, center.y + radius * 0.6f), color, Vector2.zero);
            mesh.AddVert(new Vector3(center.x, center.y - radius), color, Vector2.zero);
            mesh.AddTriangle(0, 1, 2);
            return;
        }

        float aspect = currentSprite.rect.width / currentSprite.rect.height;
        float width = Mathf.Min(rect.width, rect.height * aspect);
        float height = width / aspect;
        Vector2 c = rect.center;
        Vector4 uv = DataUtility.GetOuterUV(currentSprite);
        mesh.AddVert(new Vector3(c.x - width / 2, c.y - height / 2), color, new Vector2(uv.x, uv.y));
        mesh.AddVert(new Vector3(c.x - width / 2, c.y + height / 2), color, new Vector2(uv.x, uv.w));
        mesh.AddVert(new Vector3(c.x + width / 2, c.y + height / 2), color, new Vector2(uv.z, uv.w));
        mesh.AddVert(new Vector3(c.x + width / 2, c.y - height / 2), color, new Vector2(uv.z, uv.y));
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(2, 3, 0);
    }
}
