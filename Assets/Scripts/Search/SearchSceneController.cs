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

public sealed class SearchSceneController : MonoBehaviour
{
    private const string KayoSaveKey = "KayoSearch.Acquired.v1";
    private const string CompletionDialogueId = "search_completion";
    private string SaveKey => searchProfile != null ? searchProfile.saveKey : KayoSaveKey;
    private const float ReferenceAspect = 1672f / 941f;
    private const int SearchCanvasSortingOrder = 100;

    private static readonly Color Gold = new Color(1f, 0.72f, 0.16f, 1f);
    private static readonly Color DarkPanel = new Color(0.018f, 0.027f, 0.04f, 0.84f);
    private static readonly Color MainText = new Color(0.96f, 0.97f, 0.94f, 1f);

    [Header("Backgrounds")]
    [SerializeField]
    private Sprite room1Background;

    [SerializeField]
    private Sprite room2Background;

    [SerializeField]
    [Tooltip("本棚の会話終了後に部屋1へ表示する背景です。")]
    private Sprite room1PaperBackground;

    [Header("UI")]
    [SerializeField]
    private TMP_FontAsset uiFont;

    [Header("Item Dialogue")]
    [SerializeField]
    private DialogueScenarioSO itemDialogueScenario;

    [SerializeField]
    private CharacterDatabaseSO dialogueCharacterDatabase;

    [Header("Page Advance Marks")]
    [SerializeField] private Sprite kayoAdvanceMark;
    [SerializeField] private Sprite moteruAdvanceMark;
    [SerializeField] private Sprite yowashiAdvanceMark;

    [Header("Completion Transition")]
    [SerializeField, Min(0f)]
    private float sceneFadeInDuration = 1f;

    [SerializeField, Min(0f)]
    private float fadeOutDuration = 2.5f;

    [SerializeField, Min(0f)]
    private float postFadeDelay = 2f;

    [SerializeField]
    private string completionSceneName = "NovelScene_Kayo";

    [SerializeField]
    private string completionResumeLineId = "chapter_kayo_diary_001";

    [Header("Diary Puzzle")]
    [SerializeField]
    private string puzzleSceneName = "PuzzleGame";

    [SerializeField]
    [Tooltip("各ルートの日記用パズル画像。未設定の場合は既存パズルの仮画像を使用します。")]
    private Texture2D diaryPuzzleImage;

    [Header("Optional Chapter Profile")]
    [SerializeField] private SearchSceneProfileSO searchProfile;
    private readonly Dictionary<string, GameObject> placedImages = new Dictionary<string, GameObject>();
    private RawImage modalSecondaryStill;
    private TMP_Text modalPlaceholderText;

    private readonly HashSet<string> acquiredItemIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ItemDefinition> items = new List<ItemDefinition>();
    private readonly List<SearchSceneHotspot> hotspots =
        new List<SearchSceneHotspot>();

    private Canvas canvas;
    private RectTransform backgroundFrame;
    private Image backgroundImage;
    private GameObject searchHeader;
    private Button leftArrow;
    private Button rightArrow;
    private TMP_Text roomIndicator;
    private GameObject modalRoot;
    private RawImage modalStill;
    private AspectRatioFitter modalStillAspect;
    private TMP_Text modalTitle;
    private TMP_Text savedMessage;
    private NovelDialogueController itemDialogueController;
    private GameObject itemDialogueRoot;
    private ItemDefinition inspectedItem;
    private ItemDefinition currentDialogueItem;
    private ItemDefinition completionDialogueItem;
    private Image completionFadeOverlay;
    private int modalOpenedFrame = -1;
    private RoomView currentRoom = RoomView.Room1;
    private Sprite currentRoom1Sprite;
    private bool isModalOpen;
    private bool isDialogueOpen;
    private bool isFadingIn;
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
        if (room1Background == null || room2Background == null)
        {
            Debug.LogError(
                "SearchSceneControllerにroom1Background / room2Backgroundが設定されていません。",
                this);
            enabled = false;
            return;
        }

        currentRoom1Sprite = room1Background;

        CaptureAndShowCursor();
        LoadProgress();

        if (searchProfile == null && acquiredItemIds.Contains("bookshelf") &&
            room1PaperBackground != null)
        {
            currentRoom1Sprite = room1PaperBackground;
        }

        BuildItemDefinitions();
        EnsureEventSystem();
        BuildUI();
        GameSaveData checkpoint = GameSaveService.Consume(gameObject.scene.name, "search");
        if (checkpoint != null)
        {
            RestoreCheckpoint(checkpoint);
            return;
        }
        bool hasCompletedSearch = HasAcquiredAllItems();
        isFadingIn = !hasCompletedSearch;
        ShowRoom(RoomView.Room1);

        if (hasCompletedSearch)
        {
            FinishAcquisitionSequence();
        }
        else
        {
            StartCoroutine(FadeInSearchScene());
        }
    }

    public bool CaptureCheckpoint(GameSaveData data, bool sceneDeparture = false)
    {
        if (sceneDeparture && isCompleting)
        {
            data.kind = "search";
            data.label = searchProfile != null ? searchProfile.saveLabel : "カヨ編の探索";
            data.text = "探索完了後の場面へ進む";
            data.room = (int)currentRoom;
            data.searchMode = "room";
            data.restartScene = true;
            return true;
        }
        if (isFadingIn || isCompleting) return false;
        if (isDialogueOpen)
        {
            if (currentDialogueItem == null || !itemDialogueController.CaptureCheckpoint(data)) return false;
            data.itemId = currentDialogueItem.Id;
            data.searchMode = "dialogue";
        }
        else
        {
            data.searchMode = isModalOpen ? "modal" : "room";
            data.itemId = inspectedItem?.Id;
            data.text = isModalOpen ? inspectedItem?.Title : "気になる場所を調べる";
        }
        data.kind = "search";
        data.room = (int)currentRoom;
        data.label = searchProfile != null ? searchProfile.saveLabel : "カヨ編の探索";
        data.paperVisible = currentRoom1Sprite == room1PaperBackground;
        return true;
    }

    private void RestoreCheckpoint(GameSaveData data)
    {
        isFadingIn = false;
        completionFadeOverlay.color = Color.clear;
        completionFadeOverlay.raycastTarget = false;
        currentRoom1Sprite = data.paperVisible && room1PaperBackground != null ? room1PaperBackground : room1Background;
        ShowRoom((RoomView)data.room);
        if (data.searchMode == "room") return;
        ItemDefinition item = data.itemId == CompletionDialogueId
            ? completionDialogueItem : items.Find(candidate => candidate.Id == data.itemId);
        if (item == null) { Debug.LogError("セーブした調査対象が見つかりません。", this); return; }
        if (data.searchMode == "modal") ShowInspection(item);
        else
        {
            currentDialogueItem = item;
            isDialogueOpen = true;
            leftArrow.gameObject.SetActive(false);
            rightArrow.gameObject.SetActive(false);
            RefreshHotspots();
            itemDialogueController.RestoreCheckpoint(data);
        }
    }

    private void Update()
    {
        if (isFadingIn ||
            isCompleting ||
            !isModalOpen ||
            ArchiveManager.IsOpen)
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

    public string CurrentOperationHelp => isModalOpen
        ? "調査画面を閉じる：左クリック"
        : "気になる場所を調べる：左クリック\n部屋を切り替える：矢印を左クリック";

    private void OnDestroy()
    {
        if (itemDialogueController != null)
        {
            itemDialogueController.DialogueCompleted -= OnItemDialogueCompleted;
        }

        if (!cursorStateCaptured)
        {
            return;
        }

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    internal bool CanInspect(string itemId)
    {
        if (isModalOpen ||
            isDialogueOpen ||
            isFadingIn ||
            isCompleting ||
            ArchiveManager.IsOpen ||
            acquiredItemIds.Contains(itemId))
        {
            return false;
        }

        ItemDefinition item = items.Find(candidate => candidate.Id == itemId);
        return item != null && IsPrerequisiteMet(item);
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

    private bool IsPrerequisiteMet(ItemDefinition item)
    {
        return item == null ||
            string.IsNullOrWhiteSpace(item.RequiresItemId) ||
            acquiredItemIds.Contains(item.RequiresItemId);
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
        completionDialogueItem = null;

        if (searchProfile != null)
        {
            foreach (SearchItemSettings settings in searchProfile.items)
            {
                items.Add(new ItemDefinition(settings.id, settings.archiveId, settings.title,
                    (RoomView)settings.room, settings.anchorMin, settings.anchorMax,
                    settings.uvRect, settings.sprite, settings.firstLineId, settings.lastLineId,
                    settings.requiresItemId, hotspotPolygon: settings.hotspotPolygon));
            }
            if (!string.IsNullOrWhiteSpace(searchProfile.completionFirstLineId) ||
                !string.IsNullOrWhiteSpace(searchProfile.completionLastLineId))
            {
                // Reuse range playback and checkpoint handling without counting this as a fourth collectible.
                completionDialogueItem = new ItemDefinition(CompletionDialogueId, null, "探索後の会話",
                    RoomView.Room1, Vector2.zero, Vector2.one, new Rect(0, 0, 1, 1), null,
                    searchProfile.completionFirstLineId, searchProfile.completionLastLineId);
            }
            return;
        }

        items.Add(
            new ItemDefinition(
                "bookshelf",
                "chapter_kayo.bookshelf",
                "本棚",
                RoomView.Room1,
                new Vector2(0.12f, 0.11f),
                new Vector2(0.34f, 0.66f),
                new Rect(0.095f, 0.08f, 0.27f, 0.64f),
                room1Background,
                "search_kayo_bookshelf_001",
                "search_kayo_bookshelf_004",
                null,
                true,
                new[]
                {
                    new Vector2(0.1330f, 0.6440f),
                    new Vector2(0.3164f, 0.6440f),
                    new Vector2(0.3272f, 0.6323f),
                    new Vector2(0.3272f, 0.1637f),
                    new Vector2(0.3164f, 0.1456f),
                    new Vector2(0.1330f, 0.1456f)
                }));

        items.Add(
            new ItemDefinition(
                "floor",
                "chapter_kayo.floor_dent",
                "床のくぼみ",
                RoomView.Room2,
                new Vector2(0.30f, 0.05f),
                new Vector2(0.72f, 0.34f),
                new Rect(0.28f, 0.04f, 0.46f, 0.31f),
                room2Background,
                "search_kayo_floor_001",
                "search_kayo_floor_001"));

        items.Add(
            new ItemDefinition(
                "paper_scrap",
                "chapter_kayo.paper_scrap",
                "紙片",
                RoomView.Room1,
                new Vector2(0.50f, 0.04f),
                new Vector2(0.76f, 0.27f),
                new Rect(0.48f, 0.03f, 0.30f, 0.26f),
                room1PaperBackground,
                "search_kayo_paper_001",
                "search_kayo_paper_003",
                "bookshelf"));
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject(
            searchProfile != null ? "MoteruSearchCanvas" : "KayoSearchCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SearchCanvasSortingOrder;

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
        // Fill the screen without stretching the art; hotspots share this frame.
        frameAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        frameAspect.aspectRatio = ReferenceAspect;

        backgroundImage = CreateImage(
            "Background",
            frameObject.transform,
            Color.white,
            false).GetComponent<Image>();
        Stretch(backgroundImage.rectTransform);
        backgroundImage.preserveAspect = false;

        if (searchProfile != null)
        {
            foreach (SearchItemSettings settings in searchProfile.items)
            {
                GameObject group = new GameObject($"PlacedImage_{settings.id}", typeof(RectTransform));
                group.transform.SetParent(backgroundFrame, false);
                SetAnchors(group, settings.anchorMin, settings.anchorMax);
                placedImages.Add(settings.id, group);
                // The room already contains this art (e.g. the bookshelf). Drawing
                // a cropped copy here would duplicate it and change its scale/margins.
                if (settings.useBackgroundArt) continue;
                CreatePlacedImage(group.transform, settings.sprite, settings.uvRect,
                    settings.secondarySprite == null ? Vector2.one : new Vector2(0.5f, 1f), Vector2.zero);
                if (!string.IsNullOrEmpty(settings.placeholderText))
                {
                    TMP_Text caption = CreateText("PlaceholderText", group.transform, settings.placeholderText,
                        20f, Color.black);
                    Stretch(caption.rectTransform, 8f, 8f, 4f, 4f);
                    caption.alignment = TextAlignmentOptions.Center;
                }
                if (settings.secondarySprite != null)
                    CreatePlacedImage(group.transform, settings.secondarySprite, new Rect(0, 0, 1, 1),
                        Vector2.one, new Vector2(0.5f, 0f));
            }
        }

        foreach (ItemDefinition item in items)
        {
            CreateHotspot(item);
        }

        BuildNavigation(canvasObject.transform);
        BuildHeader(canvasObject.transform);
        BuildModal(canvasObject.transform);
        BuildItemDialogue(canvasObject.transform);

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

    private void CreatePlacedImage(Transform parent, Sprite sprite, Rect uv, Vector2 max, Vector2 min)
    {
        uv = ValidatedUvRect(uv);
        // Dedicated art needs no UV crop. Let Image preserve the sprite aspect inside
        // the already sized item bounds, so enabling a hidden item cannot collapse
        // its rect through AspectRatioFitter's deferred layout update.
        if (sprite != null && uv == new Rect(0f, 0f, 1f, 1f))
        {
            GameObject spriteObject = CreateImage("Art", parent, Color.white, false);
            SetAnchors(spriteObject, min, max);
            Image spriteImage = spriteObject.GetComponent<Image>();
            spriteImage.sprite = sprite;
            spriteImage.preserveAspect = true;
            return;
        }

        GameObject imageObject = new GameObject("Art", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(parent, false);
        SetAnchors(imageObject, min, max);
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = sprite != null ? sprite.texture : null;
        if (sprite == null) image.color = new Color(0.94f, 0.9f, 0.77f, 1f);
        image.uvRect = uv;
        image.raycastTarget = false;
        if (sprite != null)
        {
            AspectRatioFitter fit = imageObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = sprite.texture.width * uv.width / (sprite.texture.height * uv.height);
        }
    }

    private static Rect ValidatedUvRect(Rect uv)
    {
        // Older hand-authored Rect data can deserialize as an all-zero rectangle.
        // Reject non-finite/out-of-range dimensions before aspect or UV calculations.
        return uv.width > 0f && uv.height > 0f && uv.x >= 0f && uv.y >= 0f &&
            uv.xMax <= 1.0001f && uv.yMax <= 1.0001f
            ? uv : new Rect(0f, 0f, 1f, 1f);
    }

    private static Rect InspectionUvRect(Sprite sprite, Rect configuredUv)
    {
        Rect uv = ValidatedUvRect(configuredUv);
        if (sprite == null || uv != new Rect(0f, 0f, 1f, 1f)) return uv;

        // Dedicated art follows the current Sprite Editor slice, just like the
        // exploration Image. Preserve explicit background crops used by Kayo.
        Rect slice = sprite.rect;
        Texture2D texture = sprite.texture;
        return ValidatedUvRect(new Rect(slice.x / texture.width, slice.y / texture.height,
            slice.width / texture.width, slice.height / texture.height));
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = CreateImage(
            "SearchHeader",
            parent,
            new Color(0f, 0f, 0f, 0.82f),
            false);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        searchHeader = header;
        headerRect.anchorMin = headerRect.anchorMax = new Vector2(0.035f, 1f);
        headerRect.pivot = new Vector2(0f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -16f);
        headerRect.sizeDelta = new Vector2(430f, 72f);
        header.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(32f, 14f));

        TMP_Text title = CreateText(
            "Title",
            header.transform,
            "気になる場所を調べる",
            30f,
            MainText,
            FontStyles.Bold);
        Stretch(title.rectTransform);
        title.rectTransform.offsetMin = new Vector2(26f, 8f);
        title.rectTransform.offsetMax = new Vector2(-26f, -8f);
        title.alignment = TextAlignmentOptions.Center;

    }

    private void BuildModal(Transform parent)
    {
        modalRoot = CreateImage(
            "AcquiredInformationModal",
            parent,
            new Color(0.008f, 0.012f, 0.02f, 0.77f),
            true);
        Stretch(modalRoot.GetComponent<RectTransform>());

        GameObject stillBounds = new GameObject("StillBounds", typeof(RectTransform));
        stillBounds.transform.SetParent(modalRoot.transform, false);
        SetAnchors(
            stillBounds,
            new Vector2(0.21f, 0.28f),
            new Vector2(0.79f, 0.79f));

        // 背景と枠も画像と同じ縦横比で収め、左右に黒い余白を残さない。
        GameObject stillFrame = CreateImage(
            "StillFrame",
            stillBounds.transform,
            new Color(0.035f, 0.04f, 0.04f, 0.98f),
            false);
        Stretch(stillFrame.GetComponent<RectTransform>());
        modalStillAspect = stillFrame.AddComponent<AspectRatioFitter>();
        modalStillAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        GameObject stillObject = new GameObject(
            "ItemStill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        stillObject.transform.SetParent(stillFrame.transform, false);
        modalStill = stillObject.GetComponent<RawImage>();
        modalStill.color = Color.white;
        modalStill.raycastTarget = false;
        Stretch(modalStill.rectTransform);
        GameObject secondary = new GameObject("SecondaryItemStill", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        secondary.transform.SetParent(stillFrame.transform, false);
        modalSecondaryStill = secondary.GetComponent<RawImage>();
        modalSecondaryStill.raycastTarget = false;
        secondary.SetActive(false);
        modalPlaceholderText = CreateText("PlaceholderText", stillFrame.transform, "", 32f, Color.black);
        Stretch(modalPlaceholderText.rectTransform, 12f, 12f, 12f, 12f);
        modalPlaceholderText.alignment = TextAlignmentOptions.Center;
        AddBorder(stillFrame.transform, 4f, Gold);

        modalTitle = CreateText(
            "ItemTitle",
            modalRoot.transform,
            string.Empty,
            36f,
            Gold,
            FontStyles.Bold);
        SetAnchors(
            modalTitle.gameObject,
            new Vector2(0.20f, 0.20f),
            new Vector2(0.80f, 0.28f));
        modalTitle.alignment = TextAlignmentOptions.Center;

        savedMessage = CreateText(
            "SavedMessage",
            modalRoot.transform,
            "情報を保存しました",
            23f,
            MainText,
            FontStyles.Bold);
        SetAnchors(
            savedMessage.gameObject,
            new Vector2(0.2f, 0.13f),
            new Vector2(0.8f, 0.20f));
        savedMessage.alignment = TextAlignmentOptions.Center;
        savedMessage.characterSpacing = 2f;


        modalRoot.SetActive(false);
    }

    private void BuildItemDialogue(Transform parent)
    {
        itemDialogueRoot = CreateImage(
            "ItemDialogueRoot",
            parent,
            new Color(0f, 0f, 0f, 0.28f),
            true);
        Stretch(itemDialogueRoot.GetComponent<RectTransform>());

        Image leftPortrait = CreateImage(
            "LeftPortrait",
            itemDialogueRoot.transform,
            Color.white,
            false).GetComponent<Image>();
        SetAnchors(
            leftPortrait.gameObject,
            new Vector2(-0.018f, -0.792f),
            new Vector2(0.478f, 0.92f));
        leftPortrait.preserveAspect = true;
        leftPortrait.enabled = false;

        Image rightPortrait = CreateImage(
            "RightPortrait",
            itemDialogueRoot.transform,
            Color.white,
            false).GetComponent<Image>();
        SetAnchors(
            rightPortrait.gameObject,
            new Vector2(0.522f, -0.792f),
            new Vector2(1.018f, 0.92f));
        rightPortrait.preserveAspect = true;
        rightPortrait.enabled = false;

        Color dialoguePlateColor =
            new Color(0f, 0f, 0f, 0.72f);

        GameObject dialoguePanel = CreateImage(
            "DialoguePanel",
            itemDialogueRoot.transform,
            dialoguePlateColor,
            false);
        SetAnchors(
            dialoguePanel,
            new Vector2(0.075f, 0.0148875f),
            new Vector2(0.925f, 0.2758625f));
        dialoguePanel.AddComponent<DialogueWindowFeather>();

        GameObject speakerPlate = CreateImage(
            "SpeakerPlate",
            itemDialogueRoot.transform,
            dialoguePlateColor,
            false);
        SetAnchors(
            speakerPlate,
            new Vector2(0.10475f, 0.24575f),
            new Vector2(0.2875f, 0.3145f));
        speakerPlate.AddComponent<DialogueWindowFeather>()
            .ConfigureNamePlate(dialoguePanel.GetComponent<Image>());

        TMP_Text speakerText = CreateText(
            "SpeakerName",
            speakerPlate.transform,
            string.Empty,
            40f,
            MainText,
            FontStyles.Bold);
        Stretch(speakerText.rectTransform, 18f, 18f, 0f, 0f);
        speakerText.alignment = TextAlignmentOptions.Center;

        TMP_Text dialogueText = CreateText(
            "DialogueText",
            dialoguePanel.transform,
            string.Empty,
            36.685f,
            MainText);
        SetAnchors(
            dialogueText.gameObject,
            new Vector2(0.05f, 0.1f),
            new Vector2(0.95f, 0.86f));
        dialogueText.alignment = TextAlignmentOptions.TopLeft;


        GameObject controllerObject = new GameObject(
            "EmbeddedItemDialogueController",
            typeof(RectTransform));
        controllerObject.transform.SetParent(parent, false);
        itemDialogueController =
            controllerObject.AddComponent<NovelDialogueController>();
        itemDialogueController.ConfigureEmbeddedDialogue(
            itemDialogueScenario,
            dialogueCharacterDatabase,
            itemDialogueRoot,
            speakerPlate,
            speakerText,
            dialogueText,
            leftPortrait,
            rightPortrait);
        itemDialogueController.DialogueCompleted += OnItemDialogueCompleted;
        DialogueAdvanceIndicator.Create(dialoguePanel.transform, itemDialogueController,
            kayoAdvanceMark, moteruAdvanceMark, yowashiAdvanceMark);

        itemDialogueRoot.SetActive(false);
    }

    private void CreateHotspot(ItemDefinition item)
    {
        if (item.HotspotPolygon != null && item.HotspotPolygon.Length >= 3)
        {
            // Use the same outline for the glow and pointer hit testing.
            GameObject polygonObject = new GameObject(
                $"Hotspot_{item.Id}_Polygon",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Chapter1SearchPolygonGraphic));
            polygonObject.transform.SetParent(backgroundFrame, false);
            Stretch(polygonObject.GetComponent<RectTransform>());
            Chapter1SearchPolygonGraphic polygon = polygonObject.GetComponent<Chapter1SearchPolygonGraphic>();
            polygon.SetPoints(item.HotspotPolygon);
            polygon.raycastTarget = true;
            polygon.SetGlowIntensity(0f);
            SearchSceneHotspot polygonHotspot = polygonObject.AddComponent<SearchSceneHotspot>();
            polygonHotspot.Initialize(this, item.Id, polygon);
            hotspots.Add(polygonHotspot);
            return;
        }

        GameObject hotspotObject = CreateImage(
            $"Hotspot_{item.Id}",
            backgroundFrame,
            new Color(1f, 1f, 1f, 0.002f),
            true);
        SetAnchors(hotspotObject, item.HotspotMin, item.HotspotMax);

        SearchSceneHotspot hotspot =
            hotspotObject.AddComponent<SearchSceneHotspot>();

        GameObject glow = CreateImage(
            "GoldenGlow",
            hotspotObject.transform,
            new Color(Gold.r, Gold.g, Gold.b, 0.13f),
            false);
        Stretch(glow.GetComponent<RectTransform>());
        AddBorder(glow.transform, 5f, Gold);
        glow.SetActive(false);
        hotspot.Initialize(this, item.Id, glow);

        hotspots.Add(hotspot);
    }

    private void ShowRoom(RoomView room)
    {
        if (isModalOpen || isDialogueOpen || isCompleting)
        {
            return;
        }

        currentRoom = room;
        if (searchHeader != null) searchHeader.SetActive(true);
        backgroundImage.sprite =
            room == RoomView.Room1 ? currentRoom1Sprite : room2Background;
        leftArrow.gameObject.SetActive(room == RoomView.Room2);
        rightArrow.gameObject.SetActive(room == RoomView.Room1);
        roomIndicator.text = room == RoomView.Room1 ? "●  ○" : "○  ●";
        RefreshHotspots();
    }

    private void RefreshHotspots()
    {
        if (searchProfile != null)
        {
            foreach (SearchItemSettings settings in searchProfile.items)
            {
                ItemDefinition item = items.Find(candidate => candidate.Id == settings.id);
                string busyItemId = isModalOpen ? inspectedItem?.Id : isDialogueOpen ? currentDialogueItem?.Id : null;
                bool prerequisiteConversationFinished = string.IsNullOrEmpty(settings.requiresItemId) ||
                    busyItemId != settings.requiresItemId;
                // Remove collected paper/people only after their acquisition conversation.
                placedImages[settings.id].SetActive(settings.room == (int)currentRoom &&
                    IsPrerequisiteMet(item) && prerequisiteConversationFinished &&
                    (!settings.hideImageWhenAcquired || !acquiredItemIds.Contains(settings.id) || busyItemId == settings.id));
            }
        }
        foreach (SearchSceneHotspot hotspot in hotspots)
        {
            ItemDefinition item = items.Find(candidate => candidate.Id == hotspot.ItemId);
            bool visible = item != null &&
                item.Room == currentRoom &&
                !acquiredItemIds.Contains(item.Id) &&
                IsPrerequisiteMet(item) &&
                !isModalOpen &&
                !isDialogueOpen &&
                !isFadingIn &&
                !isCompleting;
            hotspot.SetAvailable(visible);
        }
    }

    private void ShowInspection(ItemDefinition item)
    {
        inspectedItem = item;
        isModalOpen = true;
        modalOpenedFrame = Time.frameCount;
        modalTitle.text = item.Title;
        modalStill.texture = item.SourceSprite != null ? item.SourceSprite.texture : null;
        modalStill.color = item.SourceSprite != null ? Color.white : new Color(0.94f, 0.9f, 0.77f, 1f);
        Rect displayUv = InspectionUvRect(item.SourceSprite, item.StillUv);
        modalStill.uvRect = displayUv;
        modalStillAspect.aspectRatio = item.SourceSprite != null ?
            (displayUv.width * item.SourceSprite.texture.width) /
            (displayUv.height * item.SourceSprite.texture.height) : 1.4f;
        SearchItemSettings settings = searchProfile != null
            ? searchProfile.items.Find(candidate => candidate.id == item.Id) : null;
        bool paired = settings != null && settings.secondarySprite != null;
        modalPlaceholderText.text = settings != null ? settings.placeholderText : "";
        modalSecondaryStill.gameObject.SetActive(paired);
        SetAnchors(modalStill.gameObject, Vector2.zero, paired ? new Vector2(0.5f, 1f) : Vector2.one);
        if (paired)
        {
            modalStillAspect.aspectRatio *= 2f;
            modalSecondaryStill.texture = settings.secondarySprite.texture;
            modalSecondaryStill.uvRect = InspectionUvRect(settings.secondarySprite, new Rect(0f, 0f, 1f, 1f));
            SetAnchors(modalSecondaryStill.gameObject, new Vector2(0.5f, 0f), Vector2.one);
        }
        modalRoot.SetActive(true);
        leftArrow.gameObject.SetActive(false);
        rightArrow.gameObject.SetActive(false);
        RefreshHotspots();
    }

    private void CloseInspection()
    {
        ItemDefinition item = inspectedItem;
        inspectedItem = null;
        isModalOpen = false;
        modalRoot.SetActive(false);

        if (TryStartItemDialogue(item))
        {
            return;
        }

        FinishAcquisitionSequence();
    }

    private bool TryStartItemDialogue(ItemDefinition item)
    {
        if (item == null ||
            itemDialogueController == null ||
            itemDialogueScenario == null ||
            dialogueCharacterDatabase == null ||
            string.IsNullOrWhiteSpace(item.DialogueStartLineId) ||
            string.IsNullOrWhiteSpace(item.DialogueEndLineId) ||
            !itemDialogueScenario.TryGetLine(
                item.DialogueStartLineId,
                out _) ||
            !itemDialogueScenario.TryGetLine(
                item.DialogueEndLineId,
                out _))
        {
            Debug.LogWarning(
                "探索会話のデータまたはDialogueシステムが設定されていません。",
                this);
            return false;
        }

        currentDialogueItem = item;
        isDialogueOpen = true;
        if (searchHeader != null) searchHeader.SetActive(false);
        leftArrow.gameObject.SetActive(false);
        rightArrow.gameObject.SetActive(false);
        RefreshHotspots();
        StartCoroutine(StartItemDialogueNextFrame(item));
        return true;
    }

    private IEnumerator StartItemDialogueNextFrame(ItemDefinition item)
    {
        yield return null;

        if (!isDialogueOpen || isCompleting)
        {
            yield break;
        }

        itemDialogueController.PlayDialogueRange(
            itemDialogueScenario,
            item.DialogueStartLineId,
            item.DialogueEndLineId);
    }

    private void OnItemDialogueCompleted()
    {
        ItemDefinition completedItem = currentDialogueItem;
        currentDialogueItem = null;
        isDialogueOpen = false;

        if (completedItem != null && completedItem == completionDialogueItem)
        {
            BeginCompletionTransition();
            return;
        }

        if (completedItem != null &&
            completedItem.SwapRoom1BackgroundAfterDialogue &&
            room1PaperBackground != null)
        {
            currentRoom1Sprite = room1PaperBackground;
        }

        FinishAcquisitionSequence();
    }

    private void FinishAcquisitionSequence()
    {
        if (isCompleting || isDialogueOpen) return;
        if (HasAcquiredAllItems())
        {
            if (completionDialogueItem != null)
            {
                if (!TryStartItemDialogue(completionDialogueItem))
                {
                    // Never silently bypass a configured ending conversation if its CSV is not synced.
                    Debug.LogError("探索後の会話を開始できません。終了会話のIDとCSV同期を確認してください。", this);
                    ShowRoom(currentRoom);
                }
                return;
            }
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

    private IEnumerator FadeInSearchScene()
    {
        ArchiveManager.Close();
        canvas.sortingOrder = short.MaxValue;
        completionFadeOverlay.raycastTarget = true;
        completionFadeOverlay.transform.SetAsLastSibling();
        completionFadeOverlay.color = Color.black;

        float duration = Mathf.Max(0f, sceneFadeInDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha =
                duration <= 0f
                    ? 0f
                    : 1f - Mathf.Clamp01(elapsed / duration);
            completionFadeOverlay.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        completionFadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        completionFadeOverlay.raycastTarget = false;
        canvas.sortingOrder = SearchCanvasSortingOrder;
        ArchiveManager.Close();
        isFadingIn = false;
        ShowRoom(currentRoom);
    }

    private void BeginCompletionTransition()
    {
        if (isCompleting)
        {
            return;
        }

        isCompleting = true;
        if (searchHeader != null) searchHeader.SetActive(false);
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

        bool hasPuzzleReturn = !string.IsNullOrWhiteSpace(completionResumeLineId);
        if (!Application.CanStreamedLevelBeLoaded(puzzleSceneName) ||
            (hasPuzzleReturn && !Application.CanStreamedLevelBeLoaded(completionSceneName)))
        {
            Debug.LogError(
                $"探索の遷移先 '{puzzleSceneName}' が有効なScene Listにありません。",
                this);
            yield break;
        }

        ArchiveManager.Close();
        // Any chapter can opt into a diary return; an empty resume ID keeps standalone behaviour.
        if (hasPuzzleReturn) PuzzleGameFlowController.Queue(
            puzzleSceneName,
            completionSceneName,
            completionResumeLineId,
            diaryPuzzleImage);
        GameAutoSaveController.SaveBeforeSceneChange();
        SceneManager.LoadScene(puzzleSceneName, LoadSceneMode.Single);
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
        GameObject buttonObject = CreateImage(name, parent, new Color(0f, 0f, 0f, 0.72f), true);
        buttonObject.AddComponent<RoomNavigationCircleMesh>();
        AspectRatioFitter circleAspect = buttonObject.AddComponent<AspectRatioFitter>();
        circleAspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        circleAspect.aspectRatio = 1f;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(1f, 1f, 1f, 0.8f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text buttonText = CreateText(
            "Label",
            buttonObject.transform,
            label,
            labelSize,
            Color.white,
            FontStyles.Bold);
        Stretch(buttonText.rectTransform);
        buttonText.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void AddBorder(Transform parent, float thickness, Color color)
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
        public string DialogueStartLineId { get; }
        public string DialogueEndLineId { get; }
        public string RequiresItemId { get; }
        public bool SwapRoom1BackgroundAfterDialogue { get; }
        public Vector2[] HotspotPolygon { get; }

        public ItemDefinition(
            string id,
            string archiveId,
            string title,
            RoomView room,
            Vector2 hotspotMin,
            Vector2 hotspotMax,
            Rect stillUv,
            Sprite sourceSprite,
            string dialogueStartLineId,
            string dialogueEndLineId,
            string requiresItemId = null,
            bool swapRoom1BackgroundAfterDialogue = false,
            Vector2[] hotspotPolygon = null)
        {
            Id = id;
            ArchiveId = archiveId;
            Title = title;
            Room = room;
            HotspotMin = hotspotMin;
            HotspotMax = hotspotMax;
            StillUv = ValidatedUvRect(stillUv);
            SourceSprite = sourceSprite;
            DialogueStartLineId = dialogueStartLineId;
            DialogueEndLineId = dialogueEndLineId;
            RequiresItemId = requiresItemId;
            SwapRoom1BackgroundAfterDialogue = swapRoom1BackgroundAfterDialogue;
            HotspotPolygon = hotspotPolygon;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Moteru Search/Clear Saved Data")]
    private static void ClearMoteruSavedData()
    {
        PlayerPrefs.DeleteKey("MoteruSearch.Acquired.v1");
        PlayerPrefs.Save();
        Debug.Log("モテル探索の取得済みデータを削除しました。");
    }

    [UnityEditor.MenuItem("Tools/Kayo Search/Clear Saved Data")]
    private static void ClearSavedData()
    {
        PlayerPrefs.DeleteKey(KayoSaveKey);
        PlayerPrefs.Save();
        Debug.Log("カヨ探索の取得済みデータを削除しました。");
    }

    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ClearSearchProgressOnEnterPlayMode()
    {
        PlayerPrefs.DeleteKey(KayoSaveKey);
        PlayerPrefs.DeleteKey("MoteruSearch.Acquired.v1");
        PlayerPrefs.Save();
    }
#endif
}

internal sealed class SearchSceneHotspot :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    private SearchSceneController owner;
    private GameObject glow;
    private CanvasGroup glowCanvasGroup;
    private Chapter1SearchPolygonGraphic polygonGlow;
    private bool available;
    private bool isGlowing;

    public string ItemId { get; private set; }

    public void Initialize(
        SearchSceneController searchController,
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
        SearchSceneController searchController,
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
