using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SelectionPartController : MonoBehaviour
{
    private static readonly Color Gold = new Color(1f, 0.72f, 0.16f, 1f);
    private static readonly Color DarkPanel = new Color(0.012f, 0.02f, 0.032f, 0.94f);
    private static readonly Color MainText = new Color(0.96f, 0.97f, 0.94f, 1f);

    [Header("Visuals")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite kayoItem;
    [SerializeField] private Sprite moteruItem;
    [SerializeField] private Sprite yowashiItem;
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("Destination Scenes")]
    [SerializeField] private string kayoSceneName = "NovelScene_Kayo";
    [SerializeField] private string moteruSceneName = "NovelScene_Moteru";
    [SerializeField] private string yowashiSceneName = "NovelScene_Yowashi";
    [SerializeField, Min(0f)] private float fadeOutDuration = 1f;
    [Header("Confirmation UI")]
    [SerializeField, Min(0f)] private float confirmationFadeDuration = 0.25f;

    private Canvas canvas;
    private GameObject confirmationRoot;
    private CanvasGroup confirmationGroup;
    private bool isConfirmationFading;
    private TMP_Text confirmationText;
    private Button confirmButton;
    private Button cancelButton;
    private Image fadeOverlay;
    private string pendingSceneName;
    private bool isTransitioning;
    private bool introDismissed;
    public string CurrentOperationHelp => !introDismissed
        ? "会話を送る：左／右クリック\n（キャラのアイテム以外）"
        : confirmationRoot != null && confirmationRoot.activeSelf
            ? "話す・戻るを選ぶ：左クリック"
            : "キャラ名を見る：アイテムにカーソル\n話す相手を選ぶ：左クリック";
    private GameObject introDialoguePanel;
    private GameObject introSpeakerPlate;
    private readonly List<Button> selectionButtons = new List<Button>();
    private InputAction introAdvanceAction;
    private int introDismissedFrame = -1;
    private GameObject hoverNameRoot;
    private TMP_Text hoverNameText;
    private GameObject selectionTitleRoot;
    private string hoveredCharacterName;

    private void OnEnable()
    {
        introAdvanceAction = new InputAction("SelectionIntroAdvance", InputActionType.Button,
            "<Mouse>/rightButton");
        introAdvanceAction.AddBinding("<Mouse>/leftButton");
        introAdvanceAction.AddBinding("<Touchscreen>/primaryTouch/press");
        introAdvanceAction.performed += OnIntroAdvancePerformed;
        introAdvanceAction.Enable();
    }

    private void OnDisable()
    {
        if (introAdvanceAction == null) return;
        introAdvanceAction.performed -= OnIntroAdvancePerformed;
        introAdvanceAction.Dispose();
        introAdvanceAction = null;
    }

    private void OnIntroAdvancePerformed(InputAction.CallbackContext context)
    {
        TryDismissIntroAtPointer();
    }

    private void Awake()
    {
        EnsureEventSystem();
        BuildInterface();
    }

    private void Update()
    {
        // Accept clicks anywhere except the three character item areas.
        if (!introDismissed && !isTransitioning &&
            (BrowserGameControls.AdvancePressed || Mouse.current?.rightButton.wasPressedThisFrame == true))
        {
            TryDismissIntroAtPointer();
        }
    }

    private void TryDismissIntroAtPointer()
    {
        if (introDismissed || isTransitioning || ArchiveManager.IsOpen ||
            introDialoguePanel == null || introSpeakerPlate == null) return;
        Vector2 pointer = BrowserGameControls.PointerPosition;
        if (pointer.x < 0f || pointer.y < 0f ||
            pointer.x >= Screen.width || pointer.y >= Screen.height) return;
        foreach (Button button in selectionButtons)
        {
            if (button != null && RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)button.transform, pointer, null)) return;
        }
        DismissIntro();
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject(
            "SelectionCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1672f, 941f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Image background = CreateImage(
            "WhiteRoomBackground",
            canvasObject.transform,
            Color.white,
            false);
        Stretch(background.rectTransform);
        background.sprite = backgroundSprite;
        background.preserveAspect = false;

        BuildHeader(canvasObject.transform);

        CreateSelectionItem(
            "KayoItem",
            canvasObject.transform,
            kayoItem,
            "カヨ",
            new Vector2(0.5f, 0.76f),
            new Vector2(356.5f, 356.5f),
            0f,
            kayoSceneName);

        CreateSelectionItem(
            "MoteruItem",
            canvasObject.transform,
            moteruItem,
            "モテル",
            new Vector2(0.27f, 0.43f),
            new Vector2(356.5f, 356.5f),
            2.1f,
            moteruSceneName);

        CreateSelectionItem(
            "YowashiItem",
            canvasObject.transform,
            yowashiItem,
            "ヨワシ",
            new Vector2(0.73f, 0.43f),
            new Vector2(414f, 310.5f),
            4.2f,
            yowashiSceneName);

        BuildDialoguePanel(canvasObject.transform);
        BuildHoverName(canvasObject.transform);
        BuildConfirmation(canvasObject.transform);

        fadeOverlay = CreateImage(
            "SelectionFadeOverlay",
            canvasObject.transform,
            new Color(0f, 0f, 0f, 0f),
            false);
        Stretch(fadeOverlay.rectTransform);
    }

    private void BuildHeader(Transform parent)
    {
        TMP_Text title = CreateText(
            "SelectionTitle",
            parent,
            "誰と話してみる？",
            46f,
            new Color(0.08f, 0.08f, 0.08f, 1f),
            FontStyles.Bold);
        SetAnchors(title.gameObject, new Vector2(0.32f, 0.44f), new Vector2(0.68f, 0.51f));
        title.alignment = TextAlignmentOptions.Center;
        selectionTitleRoot = title.gameObject;
    }

    private void CreateSelectionItem(
        string objectName,
        Transform parent,
        Sprite sprite,
        string displayName,
        Vector2 anchor,
        Vector2 size,
        float phase,
        string destinationScene)
    {
        Image itemImage = CreateImage(objectName, parent, Color.white, true);
        RectTransform rect = itemImage.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        // Preserve the existing artwork footprint after unifying canvas resolution.
        rect.sizeDelta = size * (1672f / 1920f);
        itemImage.sprite = sprite;
        itemImage.preserveAspect = true;

        Button button = itemImage.gameObject.AddComponent<Button>();
        button.targetGraphic = itemImage;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        // Waiting for the intro must not tint/fade the original item artwork.
        colors.disabledColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.04f, 0.88f, 1f);
        colors.pressedColor = new Color(0.88f, 0.76f, 0.5f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        button.onClick.AddListener(() => ShowConfirmation(displayName, destinationScene));
        button.interactable = false;
        selectionButtons.Add(button);

        Outline hoverFrame = itemImage.gameObject.AddComponent<Outline>();
        hoverFrame.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.72f);
        hoverFrame.effectDistance = new Vector2(3f, -3f);
        hoverFrame.useGraphicAlpha = false;

        SelectionItemMotion motion = itemImage.gameObject.AddComponent<SelectionItemMotion>();
        motion.Initialize(phase, hovered => SetHoveredCharacter(displayName, hovered));

    }

    private void BuildHoverName(Transform parent)
    {
        Image plate = CreateImage("HoveredCharacterName", parent, new Color(0f, 0f, 0f, 0.72f), false);
        SetAnchors(plate.gameObject, new Vector2(0.5f, 0.31f), new Vector2(0.5f, 0.43f));
        plate.rectTransform.sizeDelta = new Vector2(250f, 0f);
        plate.gameObject.AddComponent<DialogueWindowFeather>();
        hoverNameRoot = plate.gameObject;
        hoverNameText = CreateText("Name", plate.transform, string.Empty, 60f, MainText, FontStyles.Bold);
        Stretch(hoverNameText.rectTransform, 18f, 18f, 8f, 8f);
        hoverNameText.alignment = TextAlignmentOptions.Center;
        hoverNameText.enableAutoSizing = false;
        hoverNameRoot.SetActive(false);
    }

    private void SetHoveredCharacter(string displayName, bool hovered)
    {
        if (hoverNameRoot == null) return;
        if (!introDismissed)
        {
            hoveredCharacterName = null;
            hoverNameRoot.SetActive(false);
            return;
        }
        if (hovered) hoveredCharacterName = displayName;
        else if (hoveredCharacterName == displayName) hoveredCharacterName = null;
        hoverNameText.text = hoveredCharacterName ?? string.Empty;
        // Fit the original name plus comfortable padding, not a wide screen fraction.
        ((RectTransform)hoverNameRoot.transform).sizeDelta = new Vector2(
            Mathf.Max(160f, hoverNameText.GetPreferredValues(hoverNameText.text).x + 112f), 0f);
        hoverNameRoot.SetActive(!string.IsNullOrEmpty(hoveredCharacterName) && !isTransitioning &&
            (confirmationRoot == null || !confirmationRoot.activeSelf));
    }

    private void BuildDialoguePanel(Transform parent)
    {
        Image dialoguePanel = CreateImage("DialoguePanel", parent, new Color(0f, 0f, 0f, 0.72f), false);
        SetAnchors(
            dialoguePanel.gameObject,
            new Vector2(0.075f, 0.0148875f),
            new Vector2(0.925f, 0.2758625f));
        dialoguePanel.gameObject.AddComponent<DialogueWindowFeather>();

        Image speakerPlate = CreateImage("SpeakerPlate", parent, new Color(0f, 0f, 0f, 0.72f), false);
        SetAnchors(
            speakerPlate.gameObject,
            new Vector2(0.10475f, 0.24575f),
            new Vector2(0.2875f, 0.3145f));
        speakerPlate.gameObject.AddComponent<DialogueWindowFeather>().ConfigureNamePlate(dialoguePanel);

        TMP_Text speaker = CreateText(
            "SpeakerName",
            speakerPlate.transform,
            "ドウテ",
            40f,
            MainText,
            FontStyles.Bold);
        Stretch(speaker.rectTransform, 18f, 18f, 0f, 0f);
        speaker.alignment = TextAlignmentOptions.Center;

        TMP_Text dialogue = CreateText(
            "DialogueText",
            dialoguePanel.transform,
            "ひとまず誰から話しかけようかな……？",
            36.685f,
            MainText);
        SetAnchors(
            dialogue.gameObject,
            new Vector2(0.05f, 0.1f),
            new Vector2(0.95f, 0.86f));
        dialogue.alignment = TextAlignmentOptions.TopLeft;

        introDialoguePanel = dialoguePanel.gameObject;
        introSpeakerPlate = speakerPlate.gameObject;
        Image advanceHitArea = CreateImage("AdvanceButton", dialoguePanel.transform, Color.clear, true);
        RectTransform advanceRect = advanceHitArea.rectTransform;
        advanceRect.anchorMin = advanceRect.anchorMax = new Vector2(0.88f, 0.38f);
        advanceRect.anchoredPosition = Vector2.zero;
        advanceRect.sizeDelta = new Vector2(56.16f, 56.16f);
        Button advanceButton = advanceHitArea.gameObject.AddComponent<Button>();
        advanceButton.targetGraphic = advanceHitArea;
        advanceButton.navigation = new Navigation { mode = Navigation.Mode.None };
        advanceButton.onClick.AddListener(DismissIntro);

        TMP_Text mark = CreateText("AdvanceMark", advanceHitArea.transform, "▼", 27f, Color.white);
        Stretch(mark.rectTransform);
        mark.alignment = TextAlignmentOptions.Center;
        mark.enableAutoSizing = false;
        StartCoroutine(AnimateIntroMark(mark.rectTransform));
    }

    private IEnumerator AnimateIntroMark(RectTransform mark)
    {
        float elapsed = 0f;
        while (!introDismissed && mark != null)
        {
            elapsed = Mathf.Repeat(elapsed + Time.unscaledDeltaTime, 1.8f);
            mark.anchoredPosition = new Vector2(0f, Mathf.Sin(elapsed * 2f * Mathf.PI / 1.8f) * 2f);
            yield return null;
        }
    }

    private void DismissIntro()
    {
        if (introDismissed || isTransitioning) return;
        introDismissed = true;
        introDismissedFrame = Time.frameCount;
        introDialoguePanel.SetActive(false);
        introSpeakerPlate.SetActive(false);
        foreach (Button button in selectionButtons) button.interactable = true;
    }

    private void BuildConfirmation(Transform parent)
    {
        confirmationRoot = CreateImage(
            "SelectionConfirmation",
            parent,
            new Color(0f, 0f, 0f, 0.62f),
            true).gameObject;
        Stretch(confirmationRoot.GetComponent<RectTransform>());
        confirmationGroup = confirmationRoot.AddComponent<CanvasGroup>();
        confirmationGroup.alpha = 0f;
        confirmationGroup.interactable = false;

        Image panel = CreateImage("ConfirmationPanel", confirmationRoot.transform, new Color(0f, 0f, 0f, 0.72f), false);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 30f);
        panelRect.sizeDelta = new Vector2(760f, 270f);
        panel.gameObject.AddComponent<DialogueWindowFeather>();

        confirmationText = CreateText(
            "ConfirmationText",
            panel.transform,
            string.Empty,
            30f,
            MainText,
            FontStyles.Bold);
        SetAnchors(
            confirmationText.gameObject,
            new Vector2(0.08f, 0.48f),
            new Vector2(0.92f, 0.86f));
        confirmationText.alignment = TextAlignmentOptions.Center;

        confirmButton = CreateTextButton(panel.transform, "ConfirmButton", "話してみる");
        RectTransform confirmRect = confirmButton.GetComponent<RectTransform>();
        confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(0.5f, 0f);
        confirmRect.anchoredPosition = new Vector2(-145f, 44f);
        confirmRect.sizeDelta = new Vector2(240f, 64f);
        confirmButton.onClick.AddListener(ConfirmSelection);

        cancelButton = CreateTextButton(panel.transform, "CancelButton", "戻る");
        RectTransform cancelRect = cancelButton.GetComponent<RectTransform>();
        cancelRect.anchorMin = cancelRect.anchorMax = new Vector2(0.5f, 0f);
        cancelRect.anchoredPosition = new Vector2(145f, 44f);
        cancelRect.sizeDelta = new Vector2(240f, 64f);
        cancelButton.onClick.AddListener(HideConfirmation);

        confirmationRoot.SetActive(false);
    }

    private void ShowConfirmation(string displayName, string destinationScene)
    {
        if (isTransitioning || isConfirmationFading || confirmationRoot.activeSelf ||
            !introDismissed || Time.frameCount == introDismissedFrame)
        {
            return;
        }

        pendingSceneName = destinationScene;
        hoverNameRoot.SetActive(false);
        selectionTitleRoot.SetActive(false);
        confirmationText.text = $"{displayName}と話してみますか？";
        confirmationRoot.SetActive(true);
        confirmationRoot.transform.SetAsLastSibling();
        fadeOverlay.transform.SetAsLastSibling();
        StartCoroutine(FadeConfirmation(true));
    }

    private void HideConfirmation()
    {
        if (isTransitioning || isConfirmationFading)
        {
            return;
        }

        pendingSceneName = null;
        StartCoroutine(FadeConfirmation(false));
    }

    private IEnumerator FadeConfirmation(bool show)
    {
        isConfirmationFading = true;
        confirmationGroup.interactable = false;
        // Keep the full-screen blocker during both fades to prevent click-through.
        confirmationGroup.blocksRaycasts = true;
        float startAlpha = confirmationGroup.alpha;
        float targetAlpha = show ? 1f : 0f;
        float elapsed = 0f;
        while (elapsed < confirmationFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / confirmationFadeDuration);
            confirmationGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        confirmationGroup.alpha = targetAlpha;
        confirmationGroup.interactable = show;
        isConfirmationFading = false;
        if (!show)
        {
            confirmationRoot.SetActive(false);
            selectionTitleRoot.SetActive(true);
        }
    }

    private void ConfirmSelection()
    {
        if (isTransitioning || isConfirmationFading || string.IsNullOrWhiteSpace(pendingSceneName))
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(pendingSceneName))
        {
            confirmationText.text = $"シーン「{pendingSceneName}」を読み込めません。";
            return;
        }

        StartCoroutine(FadeAndLoad(pendingSceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        isTransitioning = true;
        confirmButton.interactable = false;
        cancelButton.interactable = false;
        fadeOverlay.raycastTarget = true;
        fadeOverlay.transform.SetAsLastSibling();

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeOutDuration);
            fadeOverlay.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        fadeOverlay.color = Color.black;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private Button CreateTextButton(Transform parent, string objectName, string labelText)
    {
        // A white base lets Button colors control both brightness and transparency.
        Image image = CreateImage(objectName, parent, Color.white, true);
        image.gameObject.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(14f, 8f));

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        bool primary = objectName == "ConfirmButton";
        colors.normalColor = primary
            ? new Color(0.14f, 0.14f, 0.14f, 0.55f)
            : new Color(0.06f, 0.06f, 0.06f, 0.4f);
        colors.highlightedColor = new Color(0.25f, 0.25f, 0.25f, 0.68f);
        colors.pressedColor = new Color(0.03f, 0.03f, 0.03f, 0.72f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.06f, 0.06f, 0.06f, 0.25f);
        colors.fadeDuration = 0.15f;
        button.colors = colors;

        image.gameObject.AddComponent<ChoiceButtonHoverScale>();

        TMP_Text label = CreateText("Label", image.transform, labelText, 26f, Color.white, FontStyles.Bold);
        Stretch(label.rectTransform, 8f, 8f, 4f, 4f);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private Image CreateImage(string objectName, Transform parent, Color color, bool raycastTarget)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        Color color,
        FontStyles style = FontStyles.Normal)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private void AddBorder(Transform parent, float thickness, Color color)
    {
        GameObject border = new GameObject("Border", typeof(RectTransform));
        border.transform.SetParent(parent, false);
        Stretch(border.GetComponent<RectTransform>());

        CreateEdge("Top", border.transform, color, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, -thickness), Vector2.zero);
        CreateEdge("Bottom", border.transform, color, Vector2.zero, new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, thickness));
        CreateEdge("Left", border.transform, color, Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, new Vector2(thickness, 0f));
        CreateEdge("Right", border.transform, color, new Vector2(1f, 0f), Vector2.one,
            new Vector2(-thickness, 0f), Vector2.zero);
    }

    private void CreateEdge(
        string objectName,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        Image edge = CreateImage(objectName, parent, color, false);
        RectTransform rect = edge.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetAnchors(GameObject target, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Exclude) != null)
        {
            return;
        }

        new GameObject(
            "SelectionEventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
    }
}

internal sealed class SelectionItemMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float FloatAmplitude = 11f;
    private const float FloatSpeed = 0.9f;
    private const float ScaleSpeed = 7f;

    private RectTransform rectTransform;
    private Vector2 basePosition;
    private float phase;
    private bool isHovered;
    private bool pointerInside;
    private Button selectionButton;
    private System.Action<bool> hoverChanged;

    public void Initialize(float animationPhase, System.Action<bool> onHoverChanged)
    {
        rectTransform = (RectTransform)transform;
        basePosition = rectTransform.anchoredPosition;
        phase = animationPhase;
        hoverChanged = onHoverChanged;
        selectionButton = GetComponent<Button>();
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            return;
        }

        RefreshHoverState();

        float offset = Mathf.Sin(Time.unscaledTime * FloatSpeed + phase) * FloatAmplitude;
        rectTransform.anchoredPosition = basePosition + Vector2.up * offset;

        Vector3 targetScale = isHovered ? Vector3.one * 1.08f : Vector3.one;
        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScale,
            1f - Mathf.Exp(-ScaleSpeed * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        RefreshHoverState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        RefreshHoverState();
    }

    private void RefreshHoverState()
    {
        bool hovered = pointerInside && selectionButton != null && selectionButton.IsInteractable();
        if (hovered == isHovered) return;
        isHovered = hovered;
        hoverChanged?.Invoke(hovered);
    }

    private void OnDisable()
    {
        pointerInside = false;
        isHovered = false;
        hoverChanged?.Invoke(false);
    }
}
