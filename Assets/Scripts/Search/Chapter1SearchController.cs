using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class Chapter1SearchController : MonoBehaviour
{
    private const string SaveKey = "Chapter1Search.Acquired.v1";
    private const float ReferenceAspect = 1672f / 941f;

    private static readonly Color Gold = new Color(1f, 0.72f, 0.16f, 1f);
    private static readonly Color DarkPanel = new Color(0.018f, 0.027f, 0.04f, 0.84f);
    private static readonly Color MainText = new Color(0.96f, 0.97f, 0.94f, 1f);

    [Header("Backgrounds")]
    [SerializeField]
    private Sprite whiteRoom1;

    [SerializeField]
    private Sprite whiteRoom2;

    [Header("UI")]
    [SerializeField]
    private TMP_FontAsset uiFont;

    [Header("Completion Transition")]
    [SerializeField, Min(0f)]
    private float fadeOutDuration = 2.5f;

    [SerializeField, Min(0f)]
    private float postFadeDelay = 2f;

    [SerializeField]
    private string completionSceneName = "NovelScene";

    private readonly HashSet<string> acquiredItemIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ItemDefinition> items = new List<ItemDefinition>();
    private readonly List<Chapter1SearchHotspot> hotspots =
        new List<Chapter1SearchHotspot>();

    private Canvas canvas;
    private RectTransform backgroundFrame;
    private Image backgroundImage;
    private Button leftArrow;
    private Button rightArrow;
    private TMP_Text roomIndicator;
    private GameObject modalRoot;
    private RawImage modalStill;
    private AspectRatioFitter modalStillAspect;
    private TMP_Text modalTitle;
    private TMP_Text savedMessage;
    private Image completionFadeOverlay;
    private int modalOpenedFrame = -1;
    private RoomView currentRoom = RoomView.Room1;
    private bool isModalOpen;
    private bool isCompleting;
    private bool cursorStateCaptured;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private enum RoomView
    {
        Room1,
        Room2
    }

    private void Awake()
    {
        if (whiteRoom1 == null || whiteRoom2 == null)
        {
            Debug.LogError(
                "Chapter1SearchControllerにwhite_room_1 / white_room_2が設定されていません。",
                this);
            enabled = false;
            return;
        }

        CaptureAndShowCursor();
        LoadProgress();
        BuildItemDefinitions();
        EnsureEventSystem();
        BuildUI();
        ShowRoom(RoomView.Room1);

        if (HasAcquiredAllItems())
        {
            BeginCompletionTransition();
        }
    }

    private void Update()
    {
        if (isCompleting || !isModalOpen || ArchiveManager.IsOpen)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse != null &&
            mouse.leftButton.wasPressedThisFrame &&
            Time.frameCount > modalOpenedFrame)
        {
            CloseInspection();
        }
    }

    private void OnDestroy()
    {
        if (!cursorStateCaptured)
        {
            return;
        }

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    internal bool CanInspect(string itemId)
    {
        return !isModalOpen &&
            !isCompleting &&
            !ArchiveManager.IsOpen &&
            !acquiredItemIds.Contains(itemId);
    }

    internal void Inspect(string itemId)
    {
        if (!CanInspect(itemId))
        {
            return;
        }

        ItemDefinition item = items.Find(candidate => candidate.Id == itemId);

        if (item == null || item.Room != currentRoom)
        {
            return;
        }

        acquiredItemIds.Add(item.Id);
        SaveProgress();
        ArchiveManager.Unlock(item.ArchiveId);
        RefreshHotspots();
        ShowInspection(item);
    }

    private void CaptureAndShowCursor()
    {
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorStateCaptured = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void BuildItemDefinitions()
    {
        items.Clear();

        items.Add(
            new ItemDefinition(
                "bookshelf",
                "chapter1.bookshelf",
                "本棚",
                RoomView.Room1,
                new Vector2(0.12f, 0.11f),
                new Vector2(0.34f, 0.66f),
                new Rect(0.095f, 0.08f, 0.27f, 0.64f),
                whiteRoom1,
                new[]
                {
                    new Vector2(0.1310f, 0.6461f),
                    new Vector2(0.3176f, 0.6461f),
                    new Vector2(0.3295f, 0.6302f),
                    new Vector2(0.3290f, 0.1435f),
                    new Vector2(0.1316f, 0.1435f),
                    new Vector2(0.1316f, 0.6312f)
                },
                false));

        items.Add(
            new ItemDefinition(
                "nameplate",
                "chapter1.nameplate",
                "ドアの表札",
                RoomView.Room1,
                new Vector2(0.43f, 0.79f),
                new Vector2(0.57f, 0.88f),
                new Rect(0.35f, 0.68f, 0.30f, 0.26f),
                whiteRoom1,
                new[]
                {
                    new Vector2(0.4426f, 0.8470f),
                    new Vector2(0.5562f, 0.8470f),
                    new Vector2(0.5562f, 0.8077f),
                    new Vector2(0.4426f, 0.8077f)
                }));

        items.Add(
            new ItemDefinition(
                "manga",
                "chapter1.manga",
                "ベッドの上の漫画",
                RoomView.Room2,
                new Vector2(0.635f, 0.275f),
                new Vector2(0.78f, 0.43f),
                new Rect(0.55f, 0.17f, 0.33f, 0.39f),
                whiteRoom2));

        items.Add(
            new ItemDefinition(
                "facing_wall",
                "chapter1.facing_wall",
                "ベッドが面している壁",
                RoomView.Room2,
                new Vector2(0.045f, 0.30f),
                new Vector2(0.385f, 0.88f),
                new Rect(0.015f, 0.25f, 0.42f, 0.66f),
                whiteRoom2,
                new[]
                {
                    new Vector2(0.004f, 0.995f),
                    new Vector2(0.378f, 0.885f),
                    new Vector2(0.378f, 0.390f),
                    new Vector2(0.004f, 0.290f)
                }));
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject(
            "Chapter1SearchCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1672f, 941f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject blackBackdrop = CreateImage(
            "BlackBackdrop",
            canvasObject.transform,
            Color.black,
            false);
        Stretch(blackBackdrop.GetComponent<RectTransform>());

        GameObject frameObject = new GameObject(
            "BackgroundFrame",
            typeof(RectTransform),
            typeof(AspectRatioFitter));
        frameObject.transform.SetParent(canvasObject.transform, false);
        backgroundFrame = frameObject.GetComponent<RectTransform>();
        Stretch(backgroundFrame);

        AspectRatioFitter frameAspect = frameObject.GetComponent<AspectRatioFitter>();
        frameAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        frameAspect.aspectRatio = ReferenceAspect;

        backgroundImage = CreateImage(
            "Background",
            frameObject.transform,
            Color.white,
            false).GetComponent<Image>();
        Stretch(backgroundImage.rectTransform);
        backgroundImage.preserveAspect = false;

        foreach (ItemDefinition item in items)
        {
            CreateHotspot(item);
        }

        BuildNavigation(canvasObject.transform);
        BuildHeader(canvasObject.transform);
        BuildModal(canvasObject.transform);

        completionFadeOverlay = CreateImage(
            "CompletionFadeOverlay",
            canvasObject.transform,
            new Color(0f, 0f, 0f, 0f),
            false).GetComponent<Image>();
        Stretch(completionFadeOverlay.rectTransform);
    }

    private void BuildNavigation(Transform parent)
    {
        leftArrow = CreateButton("LeftArrow", parent, "＜", 48f);
        SetAnchors(
            leftArrow.gameObject,
            new Vector2(0.018f, 0.42f),
            new Vector2(0.082f, 0.58f));
        leftArrow.onClick.AddListener(() => ShowRoom(RoomView.Room1));

        rightArrow = CreateButton("RightArrow", parent, "＞", 48f);
        SetAnchors(
            rightArrow.gameObject,
            new Vector2(0.918f, 0.42f),
            new Vector2(0.982f, 0.58f));
        rightArrow.onClick.AddListener(() => ShowRoom(RoomView.Room2));

        roomIndicator = CreateText(
            "RoomIndicator",
            parent,
            string.Empty,
            17f,
            MainText,
            FontStyles.Bold);
        SetAnchors(
            roomIndicator.gameObject,
            new Vector2(0.43f, 0.035f),
            new Vector2(0.57f, 0.09f));
        roomIndicator.alignment = TextAlignmentOptions.Center;
        roomIndicator.characterSpacing = 4f;
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = CreateImage(
            "SearchHeader",
            parent,
            new Color(0.015f, 0.023f, 0.035f, 0.76f),
            false);
        SetAnchors(header, new Vector2(0f, 0.92f), Vector2.one);

        TMP_Text title = CreateText(
            "Title",
            header.transform,
            "気になる場所を調べる",
            22f,
            MainText,
            FontStyles.Bold);
        SetAnchors(title.gameObject, new Vector2(0.035f, 0f), new Vector2(0.5f, 1f));
        title.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text help = CreateText(
            "Help",
            header.transform,
            "マウスで選択   /   B 情報",
            16f,
            new Color(0.72f, 0.76f, 0.76f, 1f));
        SetAnchors(help.gameObject, new Vector2(0.62f, 0f), new Vector2(0.965f, 1f));
        help.alignment = TextAlignmentOptions.MidlineRight;
    }

    private void BuildModal(Transform parent)
    {
        modalRoot = CreateImage(
            "AcquiredInformationModal",
            parent,
            new Color(0.008f, 0.012f, 0.02f, 0.77f),
            true);
        Stretch(modalRoot.GetComponent<RectTransform>());

        GameObject stillFrame = CreateImage(
            "StillFrame",
            modalRoot.transform,
            new Color(0.035f, 0.04f, 0.04f, 0.98f),
            false);
        SetAnchors(
            stillFrame,
            new Vector2(0.21f, 0.28f),
            new Vector2(0.79f, 0.79f));

        GameObject stillObject = new GameObject(
            "ItemStill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter));
        stillObject.transform.SetParent(stillFrame.transform, false);
        modalStill = stillObject.GetComponent<RawImage>();
        modalStill.color = Color.white;
        modalStill.raycastTarget = false;
        modalStillAspect = stillObject.GetComponent<AspectRatioFitter>();
        modalStillAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        Stretch(modalStill.rectTransform, 10f, 10f, 10f, 10f);
        AddBorder(stillFrame.transform, 4f, Gold);

        modalTitle = CreateText(
            "ItemTitle",
            modalRoot.transform,
            string.Empty,
            25f,
            Gold,
            FontStyles.Bold);
        SetAnchors(
            modalTitle.gameObject,
            new Vector2(0.22f, 0.80f),
            new Vector2(0.78f, 0.87f));
        modalTitle.alignment = TextAlignmentOptions.Center;

        savedMessage = CreateText(
            "SavedMessage",
            modalRoot.transform,
            "情報を保存しました",
            31f,
            MainText,
            FontStyles.Bold);
        SetAnchors(
            savedMessage.gameObject,
            new Vector2(0.2f, 0.16f),
            new Vector2(0.8f, 0.25f));
        savedMessage.alignment = TextAlignmentOptions.Center;
        savedMessage.characterSpacing = 2f;

        TMP_Text closeHint = CreateText(
            "CloseHint",
            modalRoot.transform,
            "左クリックで戻る",
            16f,
            new Color(0.67f, 0.71f, 0.72f, 1f));
        SetAnchors(
            closeHint.gameObject,
            new Vector2(0.35f, 0.09f),
            new Vector2(0.65f, 0.145f));
        closeHint.alignment = TextAlignmentOptions.Center;

        modalRoot.SetActive(false);
    }

    private void CreateHotspot(ItemDefinition item)
    {
        if (item.HotspotPolygon != null &&
            item.HotspotPolygon.Length >= 3 &&
            item.UsePolygonRaycast)
        {
            CreatePolygonHotspot(item);
            return;
        }

        GameObject hotspotObject = CreateImage(
            $"Hotspot_{item.Id}",
            backgroundFrame,
            new Color(1f, 1f, 1f, 0.002f),
            true);
        SetAnchors(hotspotObject, item.HotspotMin, item.HotspotMax);

        Chapter1SearchHotspot hotspot =
            hotspotObject.AddComponent<Chapter1SearchHotspot>();

        if (item.HotspotPolygon != null && item.HotspotPolygon.Length >= 3)
        {
            GameObject polygonObject = new GameObject(
                $"GoldenGlow_{item.Id}_Polygon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Chapter1SearchPolygonGraphic));
            polygonObject.transform.SetParent(backgroundFrame, false);
            Stretch(polygonObject.GetComponent<RectTransform>());

            Chapter1SearchPolygonGraphic polygon =
                polygonObject.GetComponent<Chapter1SearchPolygonGraphic>();
            polygon.SetPoints(item.HotspotPolygon);
            polygon.raycastTarget = false;
            polygon.SetGlowIntensity(0f);
            hotspot.Initialize(this, item.Id, polygon);
        }
        else
        {
            GameObject glow = CreateImage(
                "GoldenGlow",
                hotspotObject.transform,
                new Color(Gold.r, Gold.g, Gold.b, 0.13f),
                false);
            Stretch(glow.GetComponent<RectTransform>());
            AddBorder(glow.transform, 5f, Gold);
            glow.SetActive(false);
            hotspot.Initialize(this, item.Id, glow);
        }

        hotspots.Add(hotspot);
    }

    private void CreatePolygonHotspot(ItemDefinition item)
    {
        GameObject hotspotObject = new GameObject(
            $"Hotspot_{item.Id}_Polygon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Chapter1SearchPolygonGraphic));
        hotspotObject.transform.SetParent(backgroundFrame, false);
        Stretch(hotspotObject.GetComponent<RectTransform>());

        Chapter1SearchPolygonGraphic polygon =
            hotspotObject.GetComponent<Chapter1SearchPolygonGraphic>();
        polygon.SetPoints(item.HotspotPolygon);
        polygon.raycastTarget = true;
        polygon.SetGlowIntensity(0f);

        Chapter1SearchHotspot hotspot =
            hotspotObject.AddComponent<Chapter1SearchHotspot>();
        hotspot.Initialize(this, item.Id, polygon);
        hotspots.Add(hotspot);
    }

    private void ShowRoom(RoomView room)
    {
        if (isModalOpen || isCompleting)
        {
            return;
        }

        currentRoom = room;
        backgroundImage.sprite = room == RoomView.Room1 ? whiteRoom1 : whiteRoom2;
        leftArrow.gameObject.SetActive(room == RoomView.Room2);
        rightArrow.gameObject.SetActive(room == RoomView.Room1);
        roomIndicator.text = room == RoomView.Room1 ? "●  ○" : "○  ●";
        RefreshHotspots();
    }

    private void RefreshHotspots()
    {
        foreach (Chapter1SearchHotspot hotspot in hotspots)
        {
            ItemDefinition item = items.Find(candidate => candidate.Id == hotspot.ItemId);
            bool visible = item != null &&
                item.Room == currentRoom &&
                !acquiredItemIds.Contains(item.Id) &&
                !isModalOpen &&
                !isCompleting;
            hotspot.SetAvailable(visible);
        }
    }

    private void ShowInspection(ItemDefinition item)
    {
        isModalOpen = true;
        modalOpenedFrame = Time.frameCount;
        modalTitle.text = item.Title;
        modalStill.texture = item.SourceSprite.texture;
        modalStill.uvRect = item.StillUv;
        modalStillAspect.aspectRatio =
            (item.StillUv.width * item.SourceSprite.texture.width) /
            (item.StillUv.height * item.SourceSprite.texture.height);
        modalRoot.SetActive(true);
        leftArrow.gameObject.SetActive(false);
        rightArrow.gameObject.SetActive(false);
        RefreshHotspots();
    }

    private void CloseInspection()
    {
        isModalOpen = false;
        modalRoot.SetActive(false);

        if (HasAcquiredAllItems())
        {
            BeginCompletionTransition();
            return;
        }

        ShowRoom(currentRoom);
    }

    private bool HasAcquiredAllItems()
    {
        return items.Count > 0 &&
            items.TrueForAll(item => acquiredItemIds.Contains(item.Id));
    }

    private void BeginCompletionTransition()
    {
        if (isCompleting)
        {
            return;
        }

        isCompleting = true;
        ArchiveManager.Close();
        canvas.sortingOrder = short.MaxValue;
        leftArrow.gameObject.SetActive(false);
        rightArrow.gameObject.SetActive(false);
        roomIndicator.gameObject.SetActive(false);
        RefreshHotspots();

        completionFadeOverlay.raycastTarget = true;
        completionFadeOverlay.transform.SetAsLastSibling();
        StartCoroutine(CompleteSearchSequence());
    }

    private IEnumerator CompleteSearchSequence()
    {
        float duration = Mathf.Max(0f, fadeOutDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            completionFadeOverlay.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        completionFadeOverlay.color = Color.black;

        if (postFadeDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(postFadeDelay);
        }

        if (!Application.CanStreamedLevelBeLoaded(completionSceneName))
        {
            Debug.LogError(
                $"遷移先シーン '{completionSceneName}' がBuild Settingsに登録されていません。",
                this);
            yield break;
        }

        ArchiveManager.Close();
        SceneManager.LoadScene(completionSceneName, LoadSceneMode.Single);
    }

    private void LoadProgress()
    {
        acquiredItemIds.Clear();

        if (!PlayerPrefs.HasKey(SaveKey))
        {
            return;
        }

        SearchSaveData data = JsonUtility.FromJson<SearchSaveData>(
            PlayerPrefs.GetString(SaveKey));

        if (data != null && data.acquiredItemIds != null)
        {
            acquiredItemIds.UnionWith(data.acquiredItemIds);
        }
    }

    private void SaveProgress()
    {
        SearchSaveData data = new SearchSaveData
        {
            acquiredItemIds = new List<string>(acquiredItemIds)
        };

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Exclude) != null)
        {
            return;
        }

        new GameObject(
            "SearchEventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
    }

    private GameObject CreateImage(
        string name,
        Transform parent,
        Color color,
        bool raycastTarget)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return imageObject;
    }

    private TMP_Text CreateText(
        string name,
        Transform parent,
        string textValue,
        float fontSize,
        Color color,
        FontStyles fontStyle = FontStyles.Normal)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = textValue;
        text.font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = fontStyle;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private Button CreateButton(
        string name,
        Transform parent,
        string label,
        float labelSize)
    {
        GameObject buttonObject = CreateImage(name, parent, DarkPanel, true);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.1f, 0.75f, 1f);
        colors.pressedColor = new Color(0.9f, 0.68f, 0.24f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text buttonText = CreateText(
            "Label",
            buttonObject.transform,
            label,
            labelSize,
            MainText,
            FontStyles.Bold);
        Stretch(buttonText.rectTransform);
        buttonText.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private GameObject AddBorder(Transform parent, float thickness, Color color)
    {
        GameObject borderRoot = new GameObject("Border", typeof(RectTransform));
        borderRoot.transform.SetParent(parent, false);
        Stretch(borderRoot.GetComponent<RectTransform>());

        CreateEdge("Top", borderRoot.transform, color, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, -thickness), Vector2.zero);
        CreateEdge("Bottom", borderRoot.transform, color, Vector2.zero, new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, thickness));
        CreateEdge("Left", borderRoot.transform, color, Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, new Vector2(thickness, 0f));
        CreateEdge("Right", borderRoot.transform, color, new Vector2(1f, 0f), Vector2.one,
            new Vector2(-thickness, 0f), Vector2.zero);
        return borderRoot;
    }

    private void CreateEdge(
        string name,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject edge = CreateImage(name, parent, color, false);
        RectTransform rect = edge.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(
        RectTransform rect,
        float left = 0f,
        float right = 0f,
        float bottom = 0f,
        float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetAnchors(
        GameObject target,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    [Serializable]
    private sealed class SearchSaveData
    {
        public List<string> acquiredItemIds = new List<string>();
    }

    private sealed class ItemDefinition
    {
        public string Id { get; }
        public string ArchiveId { get; }
        public string Title { get; }
        public RoomView Room { get; }
        public Vector2 HotspotMin { get; }
        public Vector2 HotspotMax { get; }
        public Rect StillUv { get; }
        public Sprite SourceSprite { get; }
        public Vector2[] HotspotPolygon { get; }
        public bool UsePolygonRaycast { get; }

        public ItemDefinition(
            string id,
            string archiveId,
            string title,
            RoomView room,
            Vector2 hotspotMin,
            Vector2 hotspotMax,
            Rect stillUv,
            Sprite sourceSprite,
            Vector2[] hotspotPolygon = null,
            bool usePolygonRaycast = true)
        {
            Id = id;
            ArchiveId = archiveId;
            Title = title;
            Room = room;
            HotspotMin = hotspotMin;
            HotspotMax = hotspotMax;
            StillUv = stillUv;
            SourceSprite = sourceSprite;
            HotspotPolygon = hotspotPolygon;
            UsePolygonRaycast = usePolygonRaycast;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Chapter 1 Search/Clear Saved Data")]
    private static void ClearSavedData()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        Debug.Log("Chapter 1探索の取得済みデータを削除しました。");
    }
#endif
}

internal sealed class Chapter1SearchHotspot :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    private Chapter1SearchController owner;
    private GameObject glow;
    private CanvasGroup glowCanvasGroup;
    private Chapter1SearchPolygonGraphic polygonGlow;
    private bool available;
    private bool isGlowing;

    public string ItemId { get; private set; }

    public void Initialize(
        Chapter1SearchController searchController,
        string itemId,
        GameObject goldenGlow)
    {
        owner = searchController;
        ItemId = itemId;
        glow = goldenGlow;
        glowCanvasGroup = glow.AddComponent<CanvasGroup>();
        glowCanvasGroup.interactable = false;
        glowCanvasGroup.blocksRaycasts = false;
    }

    public void Initialize(
        Chapter1SearchController searchController,
        string itemId,
        Chapter1SearchPolygonGraphic goldenGlow)
    {
        owner = searchController;
        ItemId = itemId;
        polygonGlow = goldenGlow;
    }

    private void Update()
    {
        if (!isGlowing)
        {
            return;
        }

        float intensity =
            0.72f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.22f;

        if (polygonGlow != null)
        {
            polygonGlow.SetGlowIntensity(intensity);
        }
        else if (glowCanvasGroup != null)
        {
            glowCanvasGroup.alpha = intensity;
        }
    }

    public void SetAvailable(bool value)
    {
        available = value;
        SetGlowVisible(false);
        gameObject.SetActive(value);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (available && owner.CanInspect(ItemId))
        {
            SetGlowVisible(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetGlowVisible(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left &&
            available)
        {
            owner.Inspect(ItemId);
        }
    }

    private void SetGlowVisible(bool value)
    {
        isGlowing = value;

        if (polygonGlow != null)
        {
            polygonGlow.SetGlowIntensity(value ? 0.72f : 0f);
        }
        else if (glow != null)
        {
            glow.SetActive(value);
        }
    }
}

internal sealed class Chapter1SearchPolygonGraphic :
    MaskableGraphic,
    ICanvasRaycastFilter
{
    private readonly List<Vector2> points = new List<Vector2>();
    private float glowIntensity;

    public void SetPoints(IReadOnlyList<Vector2> normalizedPoints)
    {
        points.Clear();

        if (normalizedPoints != null)
        {
            for (int index = 0; index < normalizedPoints.Count; index++)
            {
                points.Add(normalizedPoints[index]);
            }
        }

        SetVerticesDirty();
    }

    public void SetGlowIntensity(float value)
    {
        float clamped = Mathf.Clamp01(value);

        if (Mathf.Approximately(glowIntensity, clamped))
        {
            return;
        }

        glowIntensity = clamped;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        if (points.Count < 3)
        {
            return;
        }

        Rect rect = rectTransform.rect;
        Color32 fillColor = new Color(
            1f,
            0.72f,
            0.16f,
            Mathf.Max(0.002f, 0.15f * glowIntensity));
        Color32 outlineColor = new Color(
            1f,
            0.74f,
            0.18f,
            0.92f * glowIntensity);

        int fillStart = vertexHelper.currentVertCount;

        foreach (Vector2 point in points)
        {
            vertexHelper.AddVert(ToLocalPoint(rect, point), fillColor, Vector2.zero);
        }

        bool clockwise = CalculateSignedArea() < 0f;

        for (int index = 1; index < points.Count - 1; index++)
        {
            if (clockwise)
            {
                vertexHelper.AddTriangle(fillStart, fillStart + index + 1, fillStart + index);
            }
            else
            {
                vertexHelper.AddTriangle(fillStart, fillStart + index, fillStart + index + 1);
            }
        }

        const float outlineThickness = 5f;

        for (int index = 0; index < points.Count; index++)
        {
            Vector2 start = ToLocalPoint(rect, points[index]);
            Vector2 end = ToLocalPoint(rect, points[(index + 1) % points.Count]);
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) *
                (outlineThickness * 0.5f);
            int edgeStart = vertexHelper.currentVertCount;

            vertexHelper.AddVert(start - normal, outlineColor, Vector2.zero);
            vertexHelper.AddVert(start + normal, outlineColor, Vector2.zero);
            vertexHelper.AddVert(end + normal, outlineColor, Vector2.zero);
            vertexHelper.AddVert(end - normal, outlineColor, Vector2.zero);
            vertexHelper.AddTriangle(edgeStart, edgeStart + 1, edgeStart + 2);
            vertexHelper.AddTriangle(edgeStart, edgeStart + 2, edgeStart + 3);
        }
    }

    public bool IsRaycastLocationValid(
        Vector2 screenPoint,
        Camera eventCamera)
    {
        if (points.Count < 3 ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPoint,
                eventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;

        if (rect.width <= 0f || rect.height <= 0f)
        {
            return false;
        }

        Vector2 normalizedPoint = new Vector2(
            (localPoint.x - rect.xMin) / rect.width,
            (localPoint.y - rect.yMin) / rect.height);

        bool inside = false;

        for (int current = 0, previous = points.Count - 1;
             current < points.Count;
             previous = current++)
        {
            Vector2 a = points[current];
            Vector2 b = points[previous];
            bool crosses = (a.y > normalizedPoint.y) !=
                (b.y > normalizedPoint.y);

            if (!crosses)
            {
                continue;
            }

            float intersectionX =
                (b.x - a.x) *
                (normalizedPoint.y - a.y) /
                (b.y - a.y) +
                a.x;

            if (normalizedPoint.x < intersectionX)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private float CalculateSignedArea()
    {
        float area = 0f;

        for (int index = 0; index < points.Count; index++)
        {
            Vector2 current = points[index];
            Vector2 next = points[(index + 1) % points.Count];
            area += current.x * next.y - next.x * current.y;
        }

        return area * 0.5f;
    }

    private static Vector2 ToLocalPoint(Rect rect, Vector2 normalizedPoint)
    {
        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedPoint.x),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedPoint.y));
    }
}
