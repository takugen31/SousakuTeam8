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
    [Header("Idle Motion")]
    [SerializeField, Min(0f)] private float bobAmplitude = 2f;
    [SerializeField, Min(0.1f)] private float bobPeriod = 1.8f;
    private Sprite currentSprite;
    private bool pageReady;
    private float idleTime;
    private float verticalOffset;
    private static DialogueAdvanceMarkSet defaultMarks;

    public override Texture mainTexture => currentSprite != null
        ? currentSprite.texture : Texture2D.whiteTexture;

    public static void Create(Transform parent, NovelDialogueController controller,
        Sprite kayo, Sprite moteru, Sprite yowashi)
    {
        var markerObject = new GameObject("DialogueAdvanceIndicator",
            typeof(RectTransform), typeof(CanvasRenderer));
        markerObject.transform.SetParent(parent, false);
        var rect = (RectTransform)markerObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.88f, 0.38f);
        rect.sizeDelta = new Vector2(56.16f, 56.16f);
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
        // A scene already loaded during editing can still have empty new fields.
        // Resolve original art from a build-included asset, independent of visible names (???).
        if ((speaker == "kayo" && kayoMark == null) ||
            (speaker == "moteru" && moteruMark == null) ||
            (speaker == "yowashi" && yowashiMark == null))
        {
            if (defaultMarks == null)
                defaultMarks = Resources.Load<DialogueAdvanceMarkSet>("DialogueAdvanceMarks");
            if (defaultMarks != null)
            {
                if (kayoMark == null) kayoMark = defaultMarks.kayo;
                if (moteruMark == null) moteruMark = defaultMarks.moteru;
                if (yowashiMark == null) yowashiMark = defaultMarks.yowashi;
            }
        }
        Sprite sprite = speaker == "kayo" ? kayoMark
            : speaker == "moteru" ? moteruMark
            : speaker == "yowashi" ? yowashiMark : null;
        bool stateChanged = ready != pageReady || sprite != currentSprite;
        if (stateChanged)
        {
            pageReady = ready;
            currentSprite = sprite;
            idleTime = 0f;
            verticalOffset = 0f;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        if (!pageReady) return;
        // Animate the drawn mark, leaving the editable anchor and size untouched.
        // Unscaled time keeps the page-ready cue moving independently of game time.
        idleTime = Mathf.Repeat(idleTime + Time.unscaledDeltaTime, Mathf.Max(0.1f, bobPeriod));
        float offset = Mathf.Sin(idleTime * (2f * Mathf.PI) / Mathf.Max(0.1f, bobPeriod))
            * Mathf.Max(0f, bobAmplitude);
        if (Mathf.Approximately(offset, verticalOffset)) return;
        verticalOffset = offset;
        SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        idleTime = 0f;
        verticalOffset = 0f;
        pageReady = false;
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!pageReady) return;
        Rect rect = GetPixelAdjustedRect();
        rect.y += verticalOffset;
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
