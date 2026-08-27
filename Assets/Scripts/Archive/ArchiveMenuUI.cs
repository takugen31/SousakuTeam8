using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ArchiveMenuUI : MonoBehaviour
{
    private static readonly Color BackdropColor = new Color(0.015f, 0.025f, 0.045f, 0.94f);
    private static readonly Color PanelColor = new Color(0.035f, 0.065f, 0.095f, 0.98f);
    private static readonly Color PanelLightColor = new Color(0.055f, 0.105f, 0.14f, 0.98f);
    private static readonly Color AccentColor = new Color(0.18f, 0.82f, 0.9f, 1f);
    private static readonly Color AccentSoftColor = new Color(0.11f, 0.34f, 0.39f, 1f);
    private static readonly Color PrimaryTextColor = new Color(0.9f, 0.96f, 0.98f, 1f);
    private static readonly Color MutedTextColor = new Color(0.54f, 0.65f, 0.7f, 1f);
    private static readonly Color LockedTextColor = new Color(0.38f, 0.46f, 0.5f, 1f);

    private readonly List<ArchiveEntry> filteredEntries = new List<ArchiveEntry>();
    private readonly List<Button> entryButtons = new List<Button>();

    private ArchiveManager manager;
    private TMP_FontAsset font;
    private Canvas canvas;
    private GameObject windowRoot;
    private RectTransform listContent;
    private TMP_InputField searchInput;
    private TMP_Text countText;
    private TMP_Text detailCategory;
    private TMP_Text detailTitle;
    private TMP_Text detailSubtitle;
    private TMP_Text detailBody;
    private TMP_Text detailStatus;
    private Image detailIcon;
    private GameObject detailEmpty;
    private GameObject detailContent;
    private GameObject notificationRoot;
    private TMP_Text notificationText;
    private Coroutine notificationCoroutine;

    private ArchiveCategory? selectedCategory;
    private ArchiveEntry selectedEntry;
    private bool isRefreshing;
    private bool hasCapturedGameState;
    private float timeScaleBeforeOpen = 1f;
    private CursorLockMode cursorLockBeforeOpen;
    private bool cursorVisibleBeforeOpen;

    public bool IsOpen => windowRoot != null && windowRoot.activeSelf;
    public bool IsEditingSearch => searchInput != null && searchInput.isFocused;

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
        canvas.transform.SetAsLastSibling();
        RefreshAll();
    }

    public void Close()
    {
        if (windowRoot == null || !IsOpen)
        {
            return;
        }

        if (searchInput != null)
        {
            searchInput.DeactivateInputField();
        }

        windowRoot.SetActive(false);
        RestoreGameState();
    }

    public void HandleKeyboard(Keyboard keyboard)
    {
        if (keyboard == null || IsEditingSearch || filteredEntries.Count == 0)
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
        notificationText.text = $"NEW INFORMATION  /  {entryTitle}";
        notificationRoot.SetActive(true);

        yield return new WaitForSecondsRealtime(3.2f);

        notificationRoot.SetActive(false);
        notificationCoroutine = null;
    }

    private void RestoreGameState()
    {
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
        SetAnchors(frame, new Vector2(0.045f, 0.055f), new Vector2(0.955f, 0.945f), Vector2.zero, Vector2.zero);

        BuildHeader(frame.transform);
        BuildSidebar(frame.transform);
        BuildMainContent(frame.transform);
        BuildFooter(frame.transform);
        BuildNotification(canvasObject.transform);

        windowRoot.SetActive(false);
        notificationRoot.SetActive(false);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = CreatePanel("Header", parent, new Color(0.025f, 0.052f, 0.075f, 1f));
        SetAnchors(header, new Vector2(0f, 0.875f), Vector2.one, Vector2.zero, Vector2.zero);

        TMP_Text eyebrow = CreateText("Eyebrow", header.transform, "INFORMATION DATABASE", 17f, AccentColor, FontStyles.Bold);
        SetAnchors(eyebrow.gameObject, new Vector2(0.028f, 0.57f), new Vector2(0.5f, 0.9f), Vector2.zero, Vector2.zero);
        eyebrow.alignment = TextAlignmentOptions.BottomLeft;
        eyebrow.characterSpacing = 4f;

        TMP_Text title = CreateText("Title", header.transform, "ARCHIVE", 42f, PrimaryTextColor, FontStyles.Bold);
        SetAnchors(title.gameObject, new Vector2(0.026f, 0.08f), new Vector2(0.5f, 0.62f), Vector2.zero, Vector2.zero);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.characterSpacing = 5f;

        TMP_Text shortcut = CreateText("Shortcut", header.transform, "B  CLOSE", 17f, MutedTextColor, FontStyles.Bold);
        SetAnchors(shortcut.gameObject, new Vector2(0.82f, 0.25f), new Vector2(0.965f, 0.75f), Vector2.zero, Vector2.zero);
        shortcut.alignment = TextAlignmentOptions.Center;
    }

    private void BuildSidebar(Transform parent)
    {
        GameObject sidebar = CreatePanel("Sidebar", parent, new Color(0.025f, 0.05f, 0.07f, 1f));
        SetAnchors(sidebar, new Vector2(0f, 0.07f), new Vector2(0.205f, 0.875f), Vector2.zero, Vector2.zero);

        TMP_Text menuLabel = CreateText("MenuLabel", sidebar.transform, "MENU", 15f, MutedTextColor, FontStyles.Bold);
        SetAnchors(menuLabel.gameObject, new Vector2(0.1f, 0.91f), new Vector2(0.9f, 0.97f), Vector2.zero, Vector2.zero);
        menuLabel.characterSpacing = 3f;

        Button infoTab = CreateButton("InformationTab", sidebar.transform, "◆  情報", 23f, AccentSoftColor);
        SetAnchors(infoTab.gameObject, new Vector2(0.07f, 0.81f), new Vector2(0.93f, 0.9f), Vector2.zero, Vector2.zero);
        infoTab.interactable = false;

        TMP_Text categoryLabel = CreateText("CategoryLabel", sidebar.transform, "CATEGORY", 15f, MutedTextColor, FontStyles.Bold);
        SetAnchors(categoryLabel.gameObject, new Vector2(0.1f, 0.70f), new Vector2(0.9f, 0.76f), Vector2.zero, Vector2.zero);
        categoryLabel.characterSpacing = 3f;

        AddCategoryButton(sidebar.transform, "すべて", null, 0.61f);
        AddCategoryButton(sidebar.transform, "人物", ArchiveCategory.Person, 0.52f);
        AddCategoryButton(sidebar.transform, "場所", ArchiveCategory.Place, 0.43f);
        AddCategoryButton(sidebar.transform, "手がかり", ArchiveCategory.Clue, 0.34f);
        AddCategoryButton(sidebar.transform, "記録", ArchiveCategory.Record, 0.25f);
        AddCategoryButton(sidebar.transform, "ガイド", ArchiveCategory.Tips, 0.16f);

        countText = CreateText("Count", sidebar.transform, string.Empty, 15f, MutedTextColor);
        SetAnchors(countText.gameObject, new Vector2(0.1f, 0.035f), new Vector2(0.9f, 0.11f), Vector2.zero, Vector2.zero);
        countText.alignment = TextAlignmentOptions.BottomLeft;
    }

    private void AddCategoryButton(
        Transform parent,
        string label,
        ArchiveCategory? category,
        float yMin)
    {
        Button button = CreateButton($"Category_{label}", parent, $"  {label}", 18f, Color.clear);
        SetAnchors(button.gameObject, new Vector2(0.08f, yMin), new Vector2(0.92f, yMin + 0.075f), Vector2.zero, Vector2.zero);
        button.onClick.AddListener(() =>
        {
            selectedCategory = category;
            RefreshAll();
        });
    }

    private void BuildMainContent(Transform parent)
    {
        GameObject main = CreatePanel("InformationContent", parent, PanelColor);
        SetAnchors(main, new Vector2(0.205f, 0.07f), new Vector2(1f, 0.875f), Vector2.zero, Vector2.zero);

        BuildToolbar(main.transform);
        BuildEntryList(main.transform);
        BuildDetail(main.transform);
    }

    private void BuildToolbar(Transform parent)
    {
        TMP_Text heading = CreateText("InformationHeading", parent, "情報アーカイブ", 28f, PrimaryTextColor, FontStyles.Bold);
        SetAnchors(heading.gameObject, new Vector2(0.035f, 0.875f), new Vector2(0.45f, 0.97f), Vector2.zero, Vector2.zero);
        heading.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject searchBackground = CreatePanel("Search", parent, PanelLightColor);
        SetAnchors(searchBackground, new Vector2(0.62f, 0.89f), new Vector2(0.965f, 0.955f), Vector2.zero, Vector2.zero);

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(searchBackground.transform, false);
        SetAnchors(textArea, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), Vector2.zero, Vector2.zero);

        TMP_Text placeholder = CreateText("Placeholder", textArea.transform, "タイトル・本文を検索", 16f, MutedTextColor);
        Stretch(placeholder.rectTransform);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.fontStyle = FontStyles.Italic;

        TMP_Text inputText = CreateText("Text", textArea.transform, string.Empty, 16f, PrimaryTextColor);
        Stretch(inputText.rectTransform);
        inputText.alignment = TextAlignmentOptions.MidlineLeft;

        searchInput = searchBackground.AddComponent<TMP_InputField>();
        searchInput.textViewport = textArea.GetComponent<RectTransform>();
        searchInput.textComponent = inputText;
        searchInput.placeholder = placeholder;
        searchInput.lineType = TMP_InputField.LineType.SingleLine;
        searchInput.onValueChanged.AddListener(_ => RefreshAll());
    }

    private void BuildEntryList(Transform parent)
    {
        GameObject listPanel = CreatePanel("EntryListPanel", parent, new Color(0.025f, 0.052f, 0.073f, 1f));
        SetAnchors(listPanel, new Vector2(0.03f, 0.04f), new Vector2(0.405f, 0.85f), Vector2.zero, Vector2.zero);

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
        SetAnchors(panel, new Vector2(0.425f, 0.04f), new Vector2(0.97f, 0.85f), Vector2.zero, Vector2.zero);

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

        detailStatus = CreateText("Status", detailContent.transform, string.Empty, 14f, MutedTextColor, FontStyles.Bold);
        SetAnchors(detailStatus.gameObject, new Vector2(0.72f, 0.91f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);
        detailStatus.alignment = TextAlignmentOptions.TopRight;

        detailTitle = CreateText("Title", detailContent.transform, string.Empty, 34f, PrimaryTextColor, FontStyles.Bold);
        SetAnchors(detailTitle.gameObject, new Vector2(0f, 0.72f), new Vector2(0.82f, 0.91f), Vector2.zero, Vector2.zero);
        detailTitle.alignment = TextAlignmentOptions.BottomLeft;
        detailTitle.textWrappingMode = TextWrappingModes.Normal;

        detailIcon = CreatePanel("Icon", detailContent.transform, AccentSoftColor).GetComponent<Image>();
        SetAnchors(detailIcon.gameObject, new Vector2(0.84f, 0.75f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);
        detailIcon.preserveAspect = true;

        detailSubtitle = CreateText("Subtitle", detailContent.transform, string.Empty, 17f, MutedTextColor);
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

        detailBody = CreateText("Body", bodyViewport.transform, string.Empty, 19f, PrimaryTextColor);
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

        TMP_Text acquired = CreateText("Acquired", detailContent.transform, "● ACQUIRED INFORMATION", 13f, AccentColor, FontStyles.Bold);
        SetAnchors(acquired.gameObject, new Vector2(0f, 0f), new Vector2(0.55f, 0.045f), Vector2.zero, Vector2.zero);
        acquired.characterSpacing = 1.5f;

        detailContent.SetActive(false);
    }

    private void BuildFooter(Transform parent)
    {
        GameObject footer = CreatePanel("Footer", parent, new Color(0.02f, 0.043f, 0.06f, 1f));
        SetAnchors(footer, Vector2.zero, new Vector2(1f, 0.07f), Vector2.zero, Vector2.zero);

        TMP_Text help = CreateText("Help", footer.transform, "↑ ↓  項目選択     マウスホイール  スクロール     ESC / B  閉じる", 15f, MutedTextColor);
        SetAnchors(help.gameObject, new Vector2(0.025f, 0f), new Vector2(0.75f, 1f), Vector2.zero, Vector2.zero);
        help.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text tab = CreateText("Tab", footer.transform, "INFORMATION TAB", 14f, AccentColor, FontStyles.Bold);
        SetAnchors(tab.gameObject, new Vector2(0.76f, 0f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero);
        tab.alignment = TextAlignmentOptions.MidlineRight;
        tab.characterSpacing = 2f;
    }

    private void BuildNotification(Transform parent)
    {
        notificationRoot = CreatePanel("ArchiveNotification", parent, new Color(0.025f, 0.12f, 0.15f, 0.97f));
        SetAnchors(notificationRoot, new Vector2(0.65f, 0.87f), new Vector2(0.97f, 0.95f), Vector2.zero, Vector2.zero);
        notificationRoot.GetComponent<Image>().raycastTarget = false;

        GameObject accent = CreatePanel("Accent", notificationRoot.transform, AccentColor);
        SetAnchors(accent, Vector2.zero, new Vector2(0.018f, 1f), Vector2.zero, Vector2.zero);
        accent.GetComponent<Image>().raycastTarget = false;

        notificationText = CreateText("Text", notificationRoot.transform, string.Empty, 16f, PrimaryTextColor, FontStyles.Bold);
        SetAnchors(notificationText.gameObject, new Vector2(0.06f, 0f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        notificationText.alignment = TextAlignmentOptions.MidlineLeft;
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
        string query = searchInput == null ? string.Empty : searchInput.text.Trim();

        foreach (ArchiveEntry entry in manager.Entries)
        {
            if (entry == null ||
                (!manager.IsEntryUnlocked(entry) && !entry.ShowBeforeUnlock) ||
                (selectedCategory.HasValue && entry.Category != selectedCategory.Value))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(query) && !MatchesSearch(entry, query))
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

    private static bool MatchesSearch(ArchiveEntry entry, string query)
    {
        return Contains(entry.Title, query) ||
            Contains(entry.Subtitle, query) ||
            Contains(entry.Body, query);
    }

    private static bool Contains(string source, string query)
    {
        return !string.IsNullOrEmpty(source) &&
            source.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private void RebuildEntryButtons()
    {
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
        detailStatus.text = $"ID  {selectedEntry.Id}";
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

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.72f, 0.9f, 0.92f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.72f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text text = CreateText("Label", buttonObject.transform, label, size, PrimaryTextColor, FontStyles.Bold);
        Stretch(text.rectTransform, 18f, 18f, 6f, 6f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
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
