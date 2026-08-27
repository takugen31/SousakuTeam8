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

public sealed class KayoSearchController : MonoBehaviour
{
    private const string SaveKey = "KayoSearch.Acquired.v1";
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
    private string completionResumeLineId = "chapter_kayo_3_001";

    private readonly HashSet<string> acquiredItemIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ItemDefinition> items = new List<ItemDefinition>();
    private readonly List<KayoSearchHotspot> hotspots =
        new List<KayoSearchHotspot>();

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
    private NovelDialogueController itemDialogueController;
    private GameObject itemDialogueRoot;
    private ItemDefinition inspectedItem;
    private ItemDefinition currentDialogueItem;
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
                "KayoSearchControllerにroom1Background / room2Backgroundが設定されていません。",
                this);
            enabled = false;
            return;
        }

        currentRoom1Sprite = room1Background;

        CaptureAndShowCursor();
        LoadProgress();

        if (acquiredItemIds.Contains("bookshelf") &&
            room1PaperBackground != null)
        {
            currentRoom1Sprite = room1PaperBackground;
        }

        BuildItemDefinitions();
        EnsureEventSystem();
        BuildUI();
        bool hasCompletedSearch = HasAcquiredAllItems();
        isFadingIn = !hasCompletedSearch;
        ShowRoom(RoomView.Room1);

        if (hasCompletedSearch)
        {
            BeginCompletionTransition();
        }
        else
        {
            StartCoroutine(FadeInSearchScene());
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
                true));

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
            "KayoSearchCanvas",
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
            new Vector2(0.015f, 0.12f),
            new Vector2(0.30f, 0.89f));
        leftPortrait.preserveAspect = true;
        leftPortrait.enabled = false;

        Image rightPortrait = CreateImage(
            "RightPortrait",
            itemDialogueRoot.transform,
            Color.white,
            false).GetComponent<Image>();
        SetAnchors(
            rightPortrait.gameObject,
            new Vector2(0.70f, 0.12f),
            new Vector2(0.985f, 0.89f));
        rightPortrait.preserveAspect = true;
        rightPortrait.enabled = false;

        Color dialoguePlateColor =
            new Color(0.012f, 0.02f, 0.032f, 0.94f);

        GameObject dialoguePanel = CreateImage(
            "DialoguePanel",
            itemDialogueRoot.transform,
            dialoguePlateColor,
            false);
        SetAnchors(
            dialoguePanel,
            new Vector2(0.075f, 0.045f),
            new Vector2(0.925f, 0.32f));
        AddBorder(dialoguePanel.transform, 3f, Gold);

        GameObject speakerPlate = CreateImage(
            "SpeakerPlate",
            dialoguePanel.transform,
            dialoguePlateColor,
            false);
        SetAnchors(
            speakerPlate,
            new Vector2(0.035f, 0.73f),
            new Vector2(0.25f, 0.98f));
        AddBorder(speakerPlate.transform, 3f, Gold);

        TMP_Text speakerText = CreateText(
            "SpeakerName",
            speakerPlate.transform,
            string.Empty,
            22f,
            MainText,
            FontStyles.Bold);
        Stretch(speakerText.rectTransform, 18f, 18f, 0f, 0f);
        speakerText.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text dialogueText = CreateText(
            "DialogueText",
            dialoguePanel.transform,
            string.Empty,
            29f,
            MainText);
        SetAnchors(
            dialogueText.gameObject,
            new Vector2(0.05f, 0.16f),
            new Vector2(0.95f, 0.72f));
        dialogueText.alignment = TextAlignmentOptions.TopLeft;

        TMP_Text advanceHint = CreateText(
            "AdvanceHint",
            dialoguePanel.transform,
            "左クリックで進む",
            15f,
            new Color(0.67f, 0.71f, 0.72f, 1f));
        SetAnchors(
            advanceHint.gameObject,
            new Vector2(0.72f, 0.02f),
            new Vector2(0.95f, 0.16f));
        advanceHint.alignment = TextAlignmentOptions.MidlineRight;

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

        itemDialogueRoot.SetActive(false);
    }

    private void CreateHotspot(ItemDefinition item)
    {
        GameObject hotspotObject = CreateImage(
            $"Hotspot_{item.Id}",
            backgroundFrame,
            new Color(1f, 1f, 1f, 0.002f),
            true);
        SetAnchors(hotspotObject, item.HotspotMin, item.HotspotMax);

        KayoSearchHotspot hotspot =
            hotspotObject.AddComponent<KayoSearchHotspot>();

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
        backgroundImage.sprite =
            room == RoomView.Room1 ? currentRoom1Sprite : room2Background;
        leftArrow.gameObject.SetActive(room == RoomView.Room2);
        rightArrow.gameObject.SetActive(room == RoomView.Room1);
        roomIndicator.text = room == RoomView.Room1 ? "●  ○" : "○  ●";
        RefreshHotspots();
    }

    private void RefreshHotspots()
    {
        foreach (KayoSearchHotspot hotspot in hotspots)
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
        NovelDialogueController.QueueResumeLine(completionResumeLineId);
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
            bool swapRoom1BackgroundAfterDialogue = false)
        {
            Id = id;
            ArchiveId = archiveId;
            Title = title;
            Room = room;
            HotspotMin = hotspotMin;
            HotspotMax = hotspotMax;
            StillUv = stillUv;
            SourceSprite = sourceSprite;
            DialogueStartLineId = dialogueStartLineId;
            DialogueEndLineId = dialogueEndLineId;
            RequiresItemId = requiresItemId;
            SwapRoom1BackgroundAfterDialogue = swapRoom1BackgroundAfterDialogue;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Kayo Search/Clear Saved Data")]
    private static void ClearSavedData()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        Debug.Log("カヨ探索の取得済みデータを削除しました。");
    }

    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ClearSearchProgressOnEnterPlayMode()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }
#endif
}

internal sealed class KayoSearchHotspot :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    private KayoSearchController owner;
    private GameObject glow;
    private CanvasGroup glowCanvasGroup;
    private bool available;
    private bool isGlowing;

    public string ItemId { get; private set; }

    public void Initialize(
        KayoSearchController searchController,
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

    private void Update()
    {
        if (!isGlowing)
        {
            return;
        }

        float intensity =
            0.72f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.22f;

        if (glowCanvasGroup != null)
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

        if (glow != null)
        {
            glow.SetActive(value);
        }
    }
}
