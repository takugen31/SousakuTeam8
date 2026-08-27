using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    private Canvas canvas;
    private GameObject confirmationRoot;
    private TMP_Text confirmationText;
    private Button confirmButton;
    private Button cancelButton;
    private Image fadeOverlay;
    private string pendingSceneName;
    private bool isTransitioning;

    private void Awake()
    {
        EnsureEventSystem();
        BuildInterface();
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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
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
            new Vector2(310f, 310f),
            0f,
            kayoSceneName);

        CreateSelectionItem(
            "MoteruItem",
            canvasObject.transform,
            moteruItem,
            "モテル",
            new Vector2(0.17f, 0.51f),
            new Vector2(310f, 310f),
            2.1f,
            moteruSceneName);

        CreateSelectionItem(
            "YowashiItem",
            canvasObject.transform,
            yowashiItem,
            "ヨワシ",
            new Vector2(0.83f, 0.51f),
            new Vector2(360f, 270f),
            4.2f,
            yowashiSceneName);

        BuildDialoguePanel(canvasObject.transform);
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
            34f,
            Gold,
            FontStyles.Bold);
        SetAnchors(title.gameObject, new Vector2(0.32f, 0.93f), new Vector2(0.68f, 0.995f));
        title.alignment = TextAlignmentOptions.Center;
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
        rect.sizeDelta = size;
        itemImage.sprite = sprite;
        itemImage.preserveAspect = true;

        Button button = itemImage.gameObject.AddComponent<Button>();
        button.targetGraphic = itemImage;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.04f, 0.88f, 1f);
        colors.pressedColor = new Color(0.88f, 0.76f, 0.5f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        button.onClick.AddListener(() => ShowConfirmation(displayName, destinationScene));

        Outline hoverFrame = itemImage.gameObject.AddComponent<Outline>();
        hoverFrame.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.72f);
        hoverFrame.effectDistance = new Vector2(3f, -3f);
        hoverFrame.useGraphicAlpha = false;

        SelectionItemMotion motion = itemImage.gameObject.AddComponent<SelectionItemMotion>();
        motion.Initialize(phase);

        Image namePlate = CreateImage(
            "NamePlate",
            itemImage.transform,
            DarkPanel,
            false);
        RectTransform nameRect = namePlate.rectTransform;
        nameRect.anchorMin = new Vector2(0.5f, 0f);
        nameRect.anchorMax = new Vector2(0.5f, 0f);
        nameRect.anchoredPosition = new Vector2(0f, -24f);
        nameRect.sizeDelta = new Vector2(170f, 42f);
        AddBorder(namePlate.transform, 2f, Gold);

        TMP_Text label = CreateText(
            "Name",
            namePlate.transform,
            displayName,
            21f,
            MainText,
            FontStyles.Bold);
        Stretch(label.rectTransform, 12f, 12f, 2f, 2f);
        label.alignment = TextAlignmentOptions.Center;
    }

    private void BuildDialoguePanel(Transform parent)
    {
        Image dialoguePanel = CreateImage("DialoguePanel", parent, DarkPanel, false);
        SetAnchors(
            dialoguePanel.gameObject,
            new Vector2(0.075f, 0.045f),
            new Vector2(0.925f, 0.32f));
        AddBorder(dialoguePanel.transform, 3f, Gold);

        Image speakerPlate = CreateImage("SpeakerPlate", dialoguePanel.transform, DarkPanel, false);
        SetAnchors(
            speakerPlate.gameObject,
            new Vector2(0.035f, 0.73f),
            new Vector2(0.25f, 0.98f));
        AddBorder(speakerPlate.transform, 3f, Gold);

        TMP_Text speaker = CreateText(
            "SpeakerName",
            speakerPlate.transform,
            "ドウテ",
            22f,
            MainText,
            FontStyles.Bold);
        Stretch(speaker.rectTransform, 18f, 18f, 0f, 0f);
        speaker.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text dialogue = CreateText(
            "DialogueText",
            dialoguePanel.transform,
            "ひとまず誰から話しかけようかな……？",
            29f,
            MainText);
        SetAnchors(
            dialogue.gameObject,
            new Vector2(0.05f, 0.16f),
            new Vector2(0.95f, 0.72f));
        dialogue.alignment = TextAlignmentOptions.TopLeft;
    }

    private void BuildConfirmation(Transform parent)
    {
        confirmationRoot = CreateImage(
            "SelectionConfirmation",
            parent,
            new Color(0f, 0f, 0f, 0.62f),
            true).gameObject;
        Stretch(confirmationRoot.GetComponent<RectTransform>());

        Image panel = CreateImage("ConfirmationPanel", confirmationRoot.transform, DarkPanel, false);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 30f);
        panelRect.sizeDelta = new Vector2(760f, 270f);
        AddBorder(panel.transform, 3f, Gold);

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
        if (isTransitioning)
        {
            return;
        }

        pendingSceneName = destinationScene;
        confirmationText.text = $"{displayName}と話してみますか？";
        confirmationRoot.SetActive(true);
        confirmationRoot.transform.SetAsLastSibling();
        fadeOverlay.transform.SetAsLastSibling();
    }

    private void HideConfirmation()
    {
        if (isTransitioning)
        {
            return;
        }

        pendingSceneName = null;
        confirmationRoot.SetActive(false);
    }

    private void ConfirmSelection()
    {
        if (isTransitioning || string.IsNullOrWhiteSpace(pendingSceneName))
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
        Image image = CreateImage(objectName, parent, new Color(0.055f, 0.065f, 0.08f, 0.98f), true);
        AddBorder(image.transform, 2f, Gold);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.14f, 1.04f, 0.75f, 1f);
        colors.pressedColor = new Color(0.86f, 0.68f, 0.3f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        TMP_Text label = CreateText("Label", image.transform, labelText, 22f, MainText, FontStyles.Bold);
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

    public void Initialize(float animationPhase)
    {
        rectTransform = (RectTransform)transform;
        basePosition = rectTransform.anchoredPosition;
        phase = animationPhase;
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            return;
        }

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
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }
}
