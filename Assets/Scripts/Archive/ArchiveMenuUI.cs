using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ArchiveMenuUI : MonoBehaviour
{
    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.88f);
    private static readonly Color PanelLightColor = new Color(0f, 0f, 0f, 0.42f);
    private static readonly Color AccentColor = Color.white;
    private static readonly Color AccentSoftColor = new Color(0f, 0f, 0f, 0.5f);
    private static readonly Color PrimaryTextColor = Color.white;
    private static readonly Color MutedTextColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    private static readonly Color LockedTextColor = new Color(0.58f, 0.58f, 0.58f, 1f);

    private readonly List<ArchiveEntry> filteredEntries = new List<ArchiveEntry>();
    private readonly List<Button> entryButtons = new List<Button>();

    private ArchiveManager manager;
    private TMP_FontAsset font;
    private Canvas canvas;
    private GameObject windowRoot;
    private RectTransform listContent;
    private TMP_Text countText;
    private TMP_Text detailCategory;
    private TMP_Text detailTitle;
    private TMP_Text detailSubtitle;
    private TMP_Text detailBody;
    private Image detailIcon;
    private GameObject detailEmpty;
    private GameObject detailContent;
    private GameObject notificationRoot;
    private TMP_Text notificationText;
    private Coroutine notificationCoroutine;
    private GameObject informationRoot;
    private Button autoButton;
    private Button skipButton;
    private TMP_Text autoLabel;
    private GameObject menuRoot;
    private GameObject archiveFrame;
    private GameObject restartConfirmation;
    private TMP_Text contextHelp;

    private void UpdateContextHelp()
    {
        string help = GetCurrentOperationHelp();
        if (Application.isMobilePlatform)
        {
            help = help.Replace("左／右クリック", "タップ").Replace("左クリック", "タップ")
                .Replace("右クリック", "タップ").Replace("左ドラッグ＆ドロップ", "指でドラッグ＆ドロップ")
                .Replace("アイテムにカーソル", "アイテムをタップ")
                .Replace("キー／マウスクリック", "画面をタップ")
                .Replace("A／D・マウス移動", "画面を左右にドラッグ")
                .Replace("Space／W", "ジャンプボタン").Replace("Shift／タップ", "一掃ボタン");
        }
        string[] rows = help.Split('\n');
        float labelWidth = 0f;
        float keyWidth = 0f;
        float plainWidth = 0f;
        foreach (string row in rows)
        {
            int separator = row.IndexOf('：');
            if (separator < 0)
            {
                plainWidth = Mathf.Max(plainWidth, contextHelp.GetPreferredValues(row).x);
                continue;
            }
            labelWidth = Mathf.Max(labelWidth, contextHelp.GetPreferredValues(row.Substring(0, separator)).x);
            keyWidth = Mathf.Max(keyWidth, contextHelp.GetPreferredValues(row.Substring(separator + 1)).x);
        }
        float colonPosition = labelWidth + 8f;
        float keyPosition = colonPosition + 25f;
        var formatted = new List<string>();
        foreach (string row in rows)
        {
            int separator = row.IndexOf('：');
            formatted.Add(separator < 0 ? row : row.Substring(0, separator) +
                "<pos=" + colonPosition.ToString(System.Globalization.CultureInfo.InvariantCulture) + ">：<pos=" +
                keyPosition.ToString(System.Globalization.CultureInfo.InvariantCulture) + ">" + row.Substring(separator + 1));
        }
        contextHelp.text = string.Join("\n", formatted);
        contextHelp.rectTransform.sizeDelta = new Vector2(Mathf.Max(plainWidth, keyPosition + keyWidth) + 4f, 0f);
    }

    private string GetCurrentOperationHelp()
    {
        NovelDialogueController dialogue = FindCurrentDialogue();
        if (dialogue != null) return dialogue.CurrentOperationHelp;
        SelectionPartController selection = FindFirstObjectByType<SelectionPartController>();
        if (selection != null) return selection.CurrentOperationHelp;
        Chapter1SearchController commonSearch = FindFirstObjectByType<Chapter1SearchController>();
        if (commonSearch != null) return commonSearch.CurrentOperationHelp;
        KayoSearchController kayoSearch = FindFirstObjectByType<KayoSearchController>();
        if (kayoSearch != null) return kayoSearch.CurrentOperationHelp;
        SousakuTeam8.PuzzleGame.PuzzleGameController puzzle = FindFirstObjectByType<SousakuTeam8.PuzzleGame.PuzzleGameController>();
        if (puzzle != null) return "ピースを移動：左ドラッグ＆ドロップ";
        Sousakusai8.MiniGame.CatchMiniGameController catcher = FindFirstObjectByType<Sousakusai8.MiniGame.CatchMiniGameController>();
        if (catcher != null) return catcher.CurrentOperationHelp;
        return "ボタンを選ぶ：左クリック";
    }
    private readonly List<(CanvasGroup group, float alpha, bool interactable, bool blocksRaycasts)> hiddenDialogueWindows = new();

    private void HideDialogueWindows()
    {
        foreach (DialogueWindowFeather window in FindObjectsByType<DialogueWindowFeather>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Hide only the dialogue surfaces, not portraits or the menu itself.
            if (window.gameObject.name != "DialoguePanel" && window.gameObject.name != "SpeakerPlate") continue;
            CanvasGroup group = window.GetComponent<CanvasGroup>();
            if (group == null) group = window.gameObject.AddComponent<CanvasGroup>();
            hiddenDialogueWindows.Add((group, group.alpha, group.interactable, group.blocksRaycasts));
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        foreach (NovelDialogueController dialogue in FindObjectsByType<NovelDialogueController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            RectTransform choices = dialogue.ChoiceOptionsRoot;
            if (choices == null) continue;
            CanvasGroup group = choices.GetComponent<CanvasGroup>();
            if (group == null) group = choices.gameObject.AddComponent<CanvasGroup>();
            hiddenDialogueWindows.Add((group, group.alpha, group.interactable, group.blocksRaycasts));
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    private void RestoreDialogueWindows()
    {
        foreach (var state in hiddenDialogueWindows)
        {
            if (state.group == null) continue;
            state.group.alpha = state.alpha;
            state.group.interactable = state.interactable;
            state.group.blocksRaycasts = state.blocksRaycasts;
        }
        hiddenDialogueWindows.Clear();
    }

    private void ShowMenu()
    {
        archiveFrame.SetActive(false);
        menuRoot.SetActive(true);
        restartConfirmation.SetActive(false);
        windowRoot.GetComponent<Image>().color = Color.clear;
    }

    private void ShowArchive()
    {
        menuRoot.SetActive(false);
        archiveFrame.SetActive(true);
        windowRoot.GetComponent<Image>().color = BackdropColor;
        informationRoot.SetActive(true);
        RefreshAll();
    }

    private Button CreateMenuItem(string name, string label, float bottom)
    {
        Button button = CreateButton(name, menuRoot.transform, label, 38f, new Color(0f, 0f, 0f, 0.22f));
        SetAnchors(button.gameObject, new Vector2(0.20f, bottom), new Vector2(0.91f, bottom + 0.09f), Vector2.zero, Vector2.zero);
        button.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
        button.GetComponentInChildren<TMP_Text>().color = Color.white;
        button.targetGraphic.color = Color.white;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0.5f);
        colors.highlightedColor = new Color(0.28f, 0.28f, 0.28f, 0.85f);
        colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0f, 0f, 0f, 0.25f);
        button.colors = colors;
        button.GetComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(30f, 12f));
        return button;
    }

    private void BuildMenu()
    {
        menuRoot = CreatePanel("SystemMenu", windowRoot.transform, new Color(0f, 0f, 0f, 0.90f));
        SetAnchors(menuRoot, new Vector2(0.66f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        menuRoot.AddComponent<DialogueWindowFeather>().ConfigureRightMenu();
        TMP_Text title = CreateText("MenuTitle", menuRoot.transform, "メニュー", 46f, Color.white, FontStyles.Bold);
        SetAnchors(title.gameObject, new Vector2(0.20f, 0.79f), new Vector2(0.91f, 0.89f), Vector2.zero, Vector2.zero);
        title.alignment = TextAlignmentOptions.Center;
        CreateMenuItem("Archive", "アーカイブ", 0.64f).onClick.AddListener(ShowArchive);
        skipButton = CreateMenuItem("Skip", "スキップ", 0.51f);
        skipButton.onClick.AddListener(() =>
        {
            NovelDialogueController dialogue = FindCurrentDialogue();
            if (dialogue == null || !dialogue.CanSkipFromMenu) return;
            manager.CloseArchive();
            dialogue.ShowSkipConfirmation();
        });
        autoButton = CreateMenuItem("AutoPlay", "オート：OFF", 0.38f);
        autoLabel = autoButton.GetComponentInChildren<TMP_Text>();
        autoButton.onClick.AddListener(() => FindCurrentDialogue()?.ToggleAutoPlay());
        CreateMenuItem("Restart", "最初から", 0.25f).onClick.AddListener(() => restartConfirmation.SetActive(true));
        contextHelp = CreateText("ContextOperationHelp", menuRoot.transform, string.Empty, 21f, new Color(0.95f, 0.95f, 0.95f, 1f));
        SetAnchors(contextHelp.gameObject, new Vector2(0.555f, 0.02f), new Vector2(0.555f, 0.22f), Vector2.zero, Vector2.zero);
        contextHelp.alignment = TextAlignmentOptions.MidlineLeft;
        contextHelp.textWrappingMode = TextWrappingModes.NoWrap;
        contextHelp.richText = true;

        restartConfirmation = CreatePanel("RestartConfirmation", windowRoot.transform, new Color(0f, 0f, 0f, 0.72f));
        Stretch(restartConfirmation.GetComponent<RectTransform>());
        GameObject box = CreatePanel("Confirmation", restartConfirmation.transform, new Color(0f, 0f, 0f, 0.8f));
        SetAnchors(box, new Vector2(0.27f, 0.33f), new Vector2(0.73f, 0.67f), Vector2.zero, Vector2.zero);
        box.AddComponent<DialogueWindowFeather>();
        TMP_Text warning = CreateText("Warning", box.transform, "進行状況を初期化して、最初から始めますか？", 30f, Color.white, FontStyles.Bold);
        SetAnchors(warning.gameObject, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.86f), Vector2.zero, Vector2.zero);
        warning.alignment = TextAlignmentOptions.Center;
        Button restart = CreateButton("Confirm", box.transform, "最初から", 28f, new Color(0.15f, 0.15f, 0.15f, 0.8f));
        SetAnchors(restart.gameObject, new Vector2(0.10f, 0.16f), new Vector2(0.46f, 0.38f), Vector2.zero, Vector2.zero);
        restart.onClick.AddListener(() =>
        {
            manager.CloseArchive();
            GameProgress.ResetAll();
            Time.timeScale = 1f;
            SceneManager.LoadScene("NovelScene", LoadSceneMode.Single);
        });
        Button cancel = CreateButton("Cancel", box.transform, "戻る", 28f, new Color(0.15f, 0.15f, 0.15f, 0.8f));
        SetAnchors(cancel.gameObject, new Vector2(0.54f, 0.16f), new Vector2(0.90f, 0.38f), Vector2.zero, Vector2.zero);
        cancel.onClick.AddListener(() => restartConfirmation.SetActive(false));
        restartConfirmation.SetActive(false);
    }

    private NovelDialogueController FindCurrentDialogue()
    {
        foreach (NovelDialogueController controller in FindObjectsByType<NovelDialogueController>(FindObjectsSortMode.None))
            if (controller.IsDialoguePlaying) return controller;
        return null;
    }

    private void Update()
    {
        if (!IsOpen) return;
        NovelDialogueController dialogue = FindCurrentDialogue();
        autoButton.interactable = dialogue != null;
        skipButton.interactable = dialogue != null && dialogue.CanSkipFromMenu;
        autoLabel.color = autoButton.interactable ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f);
        skipButton.GetComponentInChildren<TMP_Text>().color = skipButton.interactable
            ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f);
        autoLabel.text = "オート：" + (dialogue != null && dialogue.IsAutoPlayEnabled ? "ON" : "OFF");
    }

    private ArchiveCategory? selectedCategory;
    private ArchiveEntry selectedEntry;
    private bool isRefreshing;
    private bool hasCapturedGameState;
    private float timeScaleBeforeOpen = 1f;
    private CursorLockMode cursorLockBeforeOpen;
    private bool cursorVisibleBeforeOpen;

    public bool IsOpen => windowRoot != null && windowRoot.activeSelf;

    public void Initialize(ArchiveManager archiveManager, TMP_FontAsset uiFont)
    {
        manager = archiveManager;
        font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        BuildUI();
        manager.ArchiveChanged += RefreshAll;
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.ArchiveChanged -= RefreshAll;
        }

        RestoreGameState();
    }

    public void Open()
    {
        if (windowRoot == null || IsOpen)
        {
            return;
        }

        timeScaleBeforeOpen = Time.timeScale;
        cursorLockBeforeOpen = Cursor.lockState;
        cursorVisibleBeforeOpen = Cursor.visible;
        hasCapturedGameState = true;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        windowRoot.SetActive(true);
        UpdateContextHelp();
        HideDialogueWindows();
        ShowMenu();
        canvas.transform.SetAsLastSibling();
        RefreshAll();
    }

    public void Close()
    {
        if (windowRoot == null || !IsOpen)
        {
            return;
        }

        windowRoot.SetActive(false);
        RestoreGameState();
    }

    public void HandleKeyboard(Keyboard keyboard)
    {
        if (keyboard == null || !archiveFrame.activeSelf || filteredEntries.Count == 0)
        {
            return;
        }

        int currentIndex = selectedEntry == null
            ? -1
            : filteredEntries.IndexOf(selectedEntry);

        if (keyboard.downArrowKey.wasPressedThisFrame)
        {
            SelectByIndex(Mathf.Min(currentIndex + 1, filteredEntries.Count - 1));
        }
        else if (keyboard.upArrowKey.wasPressedThisFrame)
        {
            SelectByIndex(Mathf.Max(currentIndex - 1, 0));
        }
    }

    public void ShowUnlockNotification(string entryTitle)
    {
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }

        notificationCoroutine = StartCoroutine(
            ShowNotificationRoutine(entryTitle));
    }

    private IEnumerator ShowNotificationRoutine(string entryTitle)
    {
        notificationText.text = $"<color=#FFD34D>New!</color>  /  {entryTitle}";
        notificationRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        RectTransform rect = notificationRoot.GetComponent<RectTransform>();
        float hiddenX = rect.rect.width + 20f;
        yield return SlideNotification(rect, hiddenX, 0f);
        yield return new WaitForSecondsRealtime(3.2f);
        yield return SlideNotification(rect, 0f, hiddenX);
        notificationRoot.SetActive(false);
        notificationCoroutine = null;
    }

    private IEnumerator SlideNotification(RectTransform rect, float startX, float endX)
    {
        const float duration = 0.3f;
        float elapsed = 0f;
        rect.anchoredPosition = new Vector2(startX, 0f);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            rect.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, progress), 0f);
            yield return null;
        }
        rect.anchoredPosition = new Vector2(endX, 0f);
    }

    private void RestoreGameState()
    {
        RestoreDialogueWindows();
        if (!hasCapturedGameState)
        {
            return;
        }

        Time.timeScale = timeScaleBeforeOpen;
        Cursor.lockState = cursorLockBeforeOpen;
        Cursor.visible = cursorVisibleBeforeOpen;
        hasCapturedGameState = false;
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject(
            "ArchiveCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        windowRoot = CreatePanel("ArchiveWindow", canvasObject.transform, BackdropColor);
        Stretch(windowRoot.GetComponent<RectTransform>());

        GameObject frame = CreatePanel("Frame", windowRoot.transform, PanelColor);
        frame.AddComponent<DialogueWindowFeather>();
        archiveFrame = frame;
        SetAnchors(frame, new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.93f), Vector2.zero, Vector2.zero);

        BuildHeader(frame.transform);
        informationRoot = new GameObject("Information", typeof(RectTransform));
        informationRoot.transform.SetParent(frame.transform, false);
        Stretch(informationRoot.GetComponent<RectTransform>());
        BuildSidebar(informationRoot.transform);
        BuildMainContent(informationRoot.transform);
        BuildMenu();
        BuildNotification(canvasObject.transform);

        windowRoot.SetActive(false);
        notificationRoot.SetActive(false);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = CreatePanel("Header", parent, Color.clear);
        SetAnchors(header, new Vector2(0f, 0.875f), Vector2.one, Vector2.zero, Vector2.zero);

        TMP_Text title = CreateText("Title", header.transform, "アーカイブ", 50f, PrimaryTextColor, FontStyles.Bold);
        SetAnchors(title.gameObject, new Vector2(0.10f, 0f), new Vector2(0.65f, 0.72f), Vector2.zero, Vector2.zero);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.characterSpacing = 5f;

        Button back = CreateButton("BackToMenu", parent, "← メニュー", 24f, AccentSoftColor);
        SetAnchors(back.gameObject, new Vector2(0.78f, 0.03f), new Vector2(0.94f, 0.11f), Vector2.zero, Vector2.zero);
        back.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
        back.onClick.AddListener(ShowMenu);
    }

    private void BuildSidebar(Transform parent)
    {
        GameObject sidebar = CreatePanel("Sidebar", parent, Color.clear);
        SetAnchors(sidebar, new Vector2(0.04f, 0.14f), new Vector2(0.23f, 0.84f), Vector2.zero, Vector2.zero);

        TMP_Text categoryLabel = CreateText("CategoryLabel", sidebar.transform, "分類", 28f, PrimaryTextColor, FontStyles.Bold);
        SetAnchors(categoryLabel.gameObject, new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.96f), Vector2.zero, Vector2.zero);
        categoryLabel.characterSpacing = 3f;
        categoryLabel.alignment = TextAlignmentOptions.Center;

        AddCategoryButton(sidebar.transform, "すべて", null, 0.72f);
        AddCategoryButton(sidebar.transform, "人物", ArchiveCategory.Person, 0.60f);
        AddCategoryButton(sidebar.transform, "場所", ArchiveCategory.Place, 0.48f);
        AddCategoryButton(sidebar.transform, "手がかり", ArchiveCategory.Clue, 0.36f);
        AddCategoryButton(sidebar.transform, "記録", ArchiveCategory.Record, 0.24f);
        AddCategoryButton(sidebar.transform, "ガイド", ArchiveCategory.Tips, 0.12f);

        countText = CreateText("Count", sidebar.transform, string.Empty, 15f, MutedTextColor);
        SetAnchors(countText.gameObject, new Vector2(0.1f, 0.035f), new Vector2(0.9f, 0.11f), Vector2.zero, Vector2.zero);
        countText.alignment = TextAlignmentOptions.BottomLeft;
        countText.gameObject.SetActive(false);
    }

    private void AddCategoryButton(
        Transform parent,
        string label,
        ArchiveCategory? category,
        float yMin)
    {
        Button button = CreateButton($"Category_{label}", parent, label, 24f, new Color(0f, 0f, 0f, 0.72f));
        SetAnchors(button.gameObject, new Vector2(0.08f, yMin), new Vector2(0.92f, yMin + 0.10f), Vector2.zero, Vector2.zero);
        TMP_Text categoryText = button.GetComponentInChildren<TMP_Text>();
        Stretch(categoryText.rectTransform, 10f, 10f, 2f, 2f);
        categoryText.alignment = TextAlignmentOptions.Center;
        categoryText.textWrappingMode = TextWrappingModes.NoWrap;
        categoryText.overflowMode = TextOverflowModes.Overflow;
        categoryText.color = Color.white;
        button.onClick.AddListener(() =>
        {
            selectedCategory = category;
            RefreshAll();
        });
    }

    private void BuildMainContent(Transform parent)
    {
        GameObject main = CreatePanel("InformationContent", parent, Color.clear);
        SetAnchors(main, new Vector2(0.25f, 0.14f), new Vector2(0.96f, 0.84f), Vector2.zero, Vector2.zero);

        BuildEntryList(main.transform);
        BuildDetail(main.transform);
    }

    private void BuildEntryList(Transform parent)
    {
        GameObject listPanel = CreatePanel("EntryListPanel", parent, PanelLightColor);
        listPanel.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(24f, 20f));
        SetAnchors(listPanel, new Vector2(0.02f, 0.02f), new Vector2(0.36f, 0.98f), Vector2.zero, Vector2.zero);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(listPanel.transform, false);
        Stretch(viewport.GetComponent<RectTransform>(), 8f, 8f, 8f, 8f);
        viewport.GetComponent<Image>().color = Color.clear;

        GameObject content = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        listContent = content.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 7f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = listPanel.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 28f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    private void BuildDetail(Transform parent)
    {
        GameObject panel = CreatePanel("DetailPanel", parent, PanelLightColor);
        panel.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(32f, 24f));
        SetAnchors(panel, new Vector2(0.39f, 0.02f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);

        detailEmpty = new GameObject("Empty", typeof(RectTransform));
        detailEmpty.transform.SetParent(panel.transform, false);
        Stretch(detailEmpty.GetComponent<RectTransform>());

        TMP_Text emptyMark = CreateText("Mark", detailEmpty.transform, "◇", 54f, AccentColor);
        SetAnchors(emptyMark.gameObject, new Vector2(0.35f, 0.52f), new Vector2(0.65f, 0.68f), Vector2.zero, Vector2.zero);
        emptyMark.alignment = TextAlignmentOptions.Center;

        TMP_Text emptyText = CreateText("Text", detailEmpty.transform, "項目を選択してください", 18f, MutedTextColor);
        SetAnchors(emptyText.gameObject, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.54f), Vector2.zero, Vector2.zero);
        emptyText.alignment = TextAlignmentOptions.Top;

        detailContent = new GameObject("Content", typeof(RectTransform));
        detailContent.transform.SetParent(panel.transform, false);
        Stretch(detailContent.GetComponent<RectTransform>(), 36f, 36f, 30f, 30f);

        detailCategory = CreateText("Category", detailContent.transform, string.Empty, 15f, AccentColor, FontStyles.Bold);
        SetAnchors(detailCategory.gameObject, new Vector2(0f, 0.9f), new Vector2(0.7f, 0.98f), Vector2.zero, Vector2.zero);
        detailCategory.characterSpacing = 2f;

        detailTitle = CreateText("Title", detailContent.transform, string.Empty, 34f, PrimaryTextColor, FontStyles.Bold);
        SetAnchors(detailTitle.gameObject, new Vector2(0f, 0.72f), new Vector2(0.82f, 0.91f), Vector2.zero, Vector2.zero);
        detailTitle.alignment = TextAlignmentOptions.BottomLeft;
        detailTitle.textWrappingMode = TextWrappingModes.Normal;

        detailIcon = CreatePanel("Icon", detailContent.transform, AccentSoftColor).GetComponent<Image>();
        SetAnchors(detailIcon.gameObject, new Vector2(0.84f, 0.75f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);
        detailIcon.preserveAspect = true;

        detailSubtitle = CreateText("Subtitle", detailContent.transform, string.Empty, 22f, MutedTextColor);
        SetAnchors(detailSubtitle.gameObject, new Vector2(0f, 0.62f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero);
        detailSubtitle.alignment = TextAlignmentOptions.TopLeft;
        detailSubtitle.textWrappingMode = TextWrappingModes.Normal;

        GameObject rule = CreatePanel("Rule", detailContent.transform, AccentSoftColor);
        SetAnchors(rule, new Vector2(0f, 0.595f), new Vector2(1f, 0.6f), Vector2.zero, Vector2.zero);

        GameObject bodyViewport = new GameObject(
            "BodyViewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D));
        bodyViewport.transform.SetParent(detailContent.transform, false);
        SetAnchors(bodyViewport, new Vector2(0f, 0.06f), new Vector2(1f, 0.56f), Vector2.zero, Vector2.zero);
        bodyViewport.GetComponent<Image>().color = Color.clear;

        detailBody = CreateText("Body", bodyViewport.transform, string.Empty, 26f, PrimaryTextColor);
        RectTransform bodyRect = detailBody.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.sizeDelta = Vector2.zero;
        detailBody.textWrappingMode = TextWrappingModes.Normal;
        detailBody.overflowMode = TextOverflowModes.Overflow;
        detailBody.lineSpacing = 18f;
        detailBody.alignment = TextAlignmentOptions.TopLeft;
        ContentSizeFitter bodyFitter = detailBody.gameObject.AddComponent<ContentSizeFitter>();
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect bodyScroll = bodyViewport.AddComponent<ScrollRect>();
        bodyScroll.viewport = bodyViewport.GetComponent<RectTransform>();
        bodyScroll.content = bodyRect;
        bodyScroll.horizontal = false;
        bodyScroll.vertical = true;
        bodyScroll.scrollSensitivity = 24f;
        bodyScroll.movementType = ScrollRect.MovementType.Clamped;

        TMP_Text acquired = CreateText("Acquired", detailContent.transform, "取得した情報", 16f, MutedTextColor, FontStyles.Bold);
        SetAnchors(acquired.gameObject, new Vector2(0f, 0f), new Vector2(0.55f, 0.045f), Vector2.zero, Vector2.zero);
        acquired.characterSpacing = 1.5f;
        acquired.gameObject.SetActive(false);

        detailContent.SetActive(false);
    }

    private void BuildFooter(Transform parent)
    {
        GameObject footer = CreatePanel("Footer", parent, Color.clear);
        SetAnchors(footer, Vector2.zero, new Vector2(1f, 0.07f), Vector2.zero, Vector2.zero);

        TMP_Text help = CreateText("Help", footer.transform, "↑ ↓  項目選択     マウスホイール  スクロール     ESC  閉じる", 15f, MutedTextColor);
        SetAnchors(help.gameObject, new Vector2(0.025f, 0f), new Vector2(0.75f, 1f), Vector2.zero, Vector2.zero);
        help.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text tab = CreateText("Tab", footer.transform, "アーカイブ", 16f, MutedTextColor, FontStyles.Bold);
        SetAnchors(tab.gameObject, new Vector2(0.76f, 0f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero);
        tab.alignment = TextAlignmentOptions.MidlineRight;
        tab.characterSpacing = 2f;
    }

    private void BuildNotification(Transform parent)
    {
        notificationRoot = CreatePanel("ArchiveNotification", parent, new Color(0f, 0f, 0f, 0.82f));
        SetAnchors(notificationRoot, new Vector2(0.65f, 0.87f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero);
        notificationRoot.GetComponent<Image>().raycastTarget = false;
        notificationRoot.AddComponent<DialogueWindowFeather>().ConfigureLeftEdgeOnly(64f);

        notificationText = CreateText("Text", notificationRoot.transform, string.Empty, 16f, PrimaryTextColor, FontStyles.Bold);
        SetAnchors(notificationText.gameObject, new Vector2(0.06f, 0f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        notificationText.alignment = TextAlignmentOptions.MidlineLeft;
        notificationText.richText = true;
    }

    private void RefreshAll()
    {
        if (!IsOpen || isRefreshing)
        {
            return;
        }

        isRefreshing = true;

        try
        {
            BuildFilteredEntries();

            if (selectedEntry == null || !filteredEntries.Contains(selectedEntry))
            {
                selectedEntry = filteredEntries.FirstOrDefault();
            }

            if (selectedEntry != null)
            {
                manager.MarkRead(selectedEntry);
            }

            RebuildEntryButtons();

            int unlockedCount = manager.Entries.Count(manager.IsEntryUnlocked);
            int unreadCount = manager.Entries.Count(
                entry => manager.IsEntryUnlocked(entry) && !manager.IsEntryRead(entry));
            countText.text = $"ACQUIRED  {unlockedCount:00}\nUNREAD       {unreadCount:00}";

            ShowSelectedEntry();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void BuildFilteredEntries()
    {
        filteredEntries.Clear();

        foreach (ArchiveEntry entry in manager.Entries)
        {
            if (entry == null ||
                (!manager.IsEntryUnlocked(entry) && !entry.ShowBeforeUnlock) ||
                (selectedCategory.HasValue && entry.Category != selectedCategory.Value))
            {
                continue;
            }

            filteredEntries.Add(entry);
        }

        filteredEntries.Sort((left, right) =>
        {
            int order = left.SortOrder.CompareTo(right.SortOrder);
            return order != 0
                ? order
                : string.Compare(left.Title, right.Title, StringComparison.CurrentCulture);
        });
    }

    private void RebuildEntryButtons()
    {
        bool sameEntries = entryButtons.Count > 0 && entryButtons.Count == filteredEntries.Count;
        for (int i = 0; sameEntries && i < entryButtons.Count; i++)
            sameEntries = entryButtons[i] != null && entryButtons[i].name == $"Entry_{filteredEntries[i].Id}";
        if (sameEntries)
        {
            for (int i = 0; i < entryButtons.Count; i++)
            {
                ArchiveEntry entry = filteredEntries[i];
                Button button = entryButtons[i];
                bool unlocked = manager.IsEntryUnlocked(entry);
                bool unread = unlocked && !manager.IsEntryRead(entry);
                string prefix = unread ? "NEW   " : string.Empty;
                string title = unlocked ? entry.Title : "？？？？？？";
                button.GetComponentInChildren<TMP_Text>().text =
                    $"<color=#{ColorUtility.ToHtmlStringRGB(unread ? AccentColor : MutedTextColor)}>{prefix}{GetCategoryLabel(entry.Category)}</color>\n{title}";
                ColorBlock colors = button.colors;
                colors.normalColor = entry == selectedEntry ? AccentSoftColor : PanelLightColor;
                button.colors = colors;
                button.interactable = unlocked;
            }
            return;
        }
        foreach (Transform child in listContent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        entryButtons.Clear();

        foreach (ArchiveEntry entry in filteredEntries)
        {
            bool unlocked = manager.IsEntryUnlocked(entry);
            bool unread = unlocked && !manager.IsEntryRead(entry);
            string category = GetCategoryLabel(entry.Category);
            string title = unlocked ? entry.Title : "？？？？？？";
            string prefix = unread ? "NEW   " : string.Empty;
            string label = $"<color=#{ColorUtility.ToHtmlStringRGB(unread ? AccentColor : MutedTextColor)}>{prefix}{category}</color>\n{title}";

            Button button = CreateButton(
                $"Entry_{entry.Id}",
                listContent,
                label,
                17f,
                entry == selectedEntry ? AccentSoftColor : PanelLightColor);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 78f;
            button.onClick.AddListener(() => SelectEntry(entry));
            button.interactable = unlocked;
            entryButtons.Add(button);
        }

        if (filteredEntries.Count == 0)
        {
            TMP_Text noResults = CreateText("NoResults", listContent, "該当する情報はありません", 17f, MutedTextColor);
            LayoutElement layout = noResults.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 90f;
            noResults.alignment = TextAlignmentOptions.Center;
        }
    }

    private void SelectByIndex(int index)
    {
        if (index < 0 || index >= filteredEntries.Count)
        {
            return;
        }

        ArchiveEntry entry = filteredEntries[index];

        if (manager.IsEntryUnlocked(entry))
        {
            SelectEntry(entry);
        }
    }

    private void SelectEntry(ArchiveEntry entry)
    {
        selectedEntry = entry;
        manager.MarkRead(entry);
        RefreshAll();
    }

    private void ShowSelectedEntry()
    {
        bool canShow = selectedEntry != null && manager.IsEntryUnlocked(selectedEntry);
        detailEmpty.SetActive(!canShow);
        detailContent.SetActive(canShow);

        if (!canShow)
        {
            return;
        }

        detailCategory.text = GetCategoryLabel(selectedEntry.Category).ToUpperInvariant();
        detailTitle.text = selectedEntry.Title;
        detailSubtitle.text = selectedEntry.Subtitle;
        detailBody.text = selectedEntry.Body;
        detailIcon.sprite = selectedEntry.Icon;
        detailIcon.color = selectedEntry.Icon != null ? Color.white : AccentSoftColor;
        detailIcon.enabled = selectedEntry.Icon != null;
    }

    private static string GetCategoryLabel(ArchiveCategory category)
    {
        switch (category)
        {
            case ArchiveCategory.Person:
                return "人物";
            case ArchiveCategory.Place:
                return "場所";
            case ArchiveCategory.Clue:
                return "手がかり";
            case ArchiveCategory.Record:
                return "記録";
            case ArchiveCategory.Tips:
                return "ガイド";
            default:
                return "情報";
        }
    }

    private GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float size,
        Color color,
        FontStyles style = FontStyles.Normal)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private Button CreateButton(
        string name,
        Transform parent,
        string label,
        float size,
        Color normalColor)
    {
        GameObject buttonObject = CreatePanel(name, parent, normalColor);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.targetGraphic.color = Color.white;
        buttonObject.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(24f, 10f));
        buttonObject.AddComponent<ChoiceButtonHoverScale>();

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = new Color(0.28f, 0.28f, 0.28f, 0.85f);
        colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0f, 0f, 0f, 0.3f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text text = CreateText("Label", buttonObject.transform, label, size, PrimaryTextColor, FontStyles.Bold);
        Stretch(text.rectTransform, 18f, 18f, 6f, 6f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        // The new Image starts white; apply the initial tint without a transition.
        button.targetGraphic.CrossFadeColor(colors.normalColor, 0f, true, true);
        return button;
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
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
