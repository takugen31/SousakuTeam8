using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleMenu : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private CanvasGroup fadePanel;
    [SerializeField] private float fadeDuration = 1.0f;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;

    private bool isTransitioning = false;
    private TMP_Text bgmLabel;

    private void Awake()
    {
        // Match the existing title buttons without changing either title scene asset.
        Button start = null;
        Button quit = null;
        foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (button.gameObject.scene != gameObject.scene) continue;
            if (button.name == "LoadButton") return;
            if (button.name == "StartButton") start = button;
            if (button.name == "QuitButton") quit = button;
        }
        if (start == null) return;
        Button load = Instantiate(start, start.transform.parent);
        load.name = "LoadButton";
        // Replace the event itself: RemoveAllListeners would retain StartGame's serialized callback.
        load.onClick = new Button.ButtonClickedEvent();
        load.onClick.AddListener(OpenLoad);
        RectTransform rect = load.GetComponent<RectTransform>();
        Vector2 startPosition = start.GetComponent<RectTransform>().anchoredPosition;
        rect.anchoredPosition = quit != null
            ? (startPosition + quit.GetComponent<RectTransform>().anchoredPosition) * 0.5f
            : startPosition + Vector2.down * (rect.rect.height + 20f);
        if (quit != null)
        {
            // Three equally sized buttons with breathing room between their existing centers.
            float height = Mathf.Min(rect.rect.height, Mathf.Abs(startPosition.y - rect.anchoredPosition.y) * 0.8f);
            foreach (Button button in new[] { start, load, quit })
                button.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
        TMP_Text label = load.GetComponentInChildren<TMP_Text>();
        if (label != null) { label.text = "ロード"; label.alignment = TextAlignmentOptions.Center; }
        load.transform.SetSiblingIndex(start.transform.GetSiblingIndex() + 1);
        ConfigureTitleLayout(start, load, quit);
    }

    private void ConfigureTitleLayout(Button start, Button load, Button quit)
    {
        Canvas canvas = start.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        TMP_FontAsset font = start.GetComponentInChildren<TMP_Text>()?.font ?? TMP_Settings.defaultFontAsset;
        Image panel = CreateTitlePanel("TitleMenuPanel", canvas.transform, new Color(0f, 0f, 0f, 0.86f));
        panel.rectTransform.anchorMin = new Vector2(0.64f, 0f);
        panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<DialogueWindowFeather>().ConfigureRightMenu();
        ConfigureTitleButton(start, panel.transform, 0.62f, 36f);
        ConfigureTitleButton(load, panel.transform, 0.46f, 36f);
        if (quit != null) ConfigureTitleButton(quit, panel.transform, 0.30f, 36f);

        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "TitleText")
            {
                Image titlePlate = CreateTitlePanel("TitlePlate", canvas.transform, new Color(0f, 0f, 0f, 0.64f));
                PlaceTitleRect(titlePlate.rectTransform, new Vector2(0.315f, 0.73f), new Vector2(1030f, 220f));
                titlePlate.gameObject.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(64f, 28f));
                text.transform.SetParent(titlePlate.transform, false);
                StretchTitleLabel(text.rectTransform, 36f, 12f);
                text.color = Color.white;
                text.font = font;
                text.fontSize = 120f;
                text.enableAutoSizing = true;
                text.fontSizeMin = 72f;
                text.fontSizeMax = 120f;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
            }
            else if (text.name == "TeamText")
            {
                Image teamPlate = CreateTitlePanel("TeamPlate", canvas.transform, new Color(0f, 0f, 0f, 0.60f));
                PlaceTitleRect(teamPlate.rectTransform, new Vector2(0.20f, 0.075f), new Vector2(530f, 64f));
                teamPlate.gameObject.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(24f, 10f));
                text.transform.SetParent(teamPlate.transform, false);
                StretchTitleLabel(text.rectTransform, 24f, 8f);
                text.color = new Color(0.86f, 0.86f, 0.86f, 1f);
                text.font = font;
                text.fontSize = 24f;
                text.enableAutoSizing = false;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
            }
        }

        foreach (Toggle toggle in canvas.GetComponentsInChildren<Toggle>(true))
            if (toggle.name == "BGMToggle") toggle.gameObject.SetActive(false);
        Image audioControl = CreateTitlePanel("BGMButton", panel.transform, Color.white);
        audioControl.raycastTarget = true;
        Button audioButton = audioControl.gameObject.AddComponent<Button>();
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(audioControl.transform, false);
        bgmLabel = labelObject.GetComponent<TextMeshProUGUI>();
        bgmLabel.font = font;
        bgmLabel.text = "BGM：" + (bgmSource != null && bgmSource.mute ? "OFF" : "ON");
        ConfigureTitleButton(audioButton, panel.transform, 0.14f, 24f, new Vector2(320f, 64f));
        audioButton.onClick.AddListener(() =>
        {
            if (isTransitioning || ArchiveManager.IsOpen || bgmSource == null) return;
            SetBGM(bgmSource.mute);
        });
        if (fadePanel != null) fadePanel.transform.SetAsLastSibling();
    }

    private static Image CreateTitlePanel(string name, Transform parent, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void PlaceTitleRect(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void StretchTitleLabel(RectTransform rect, float horizontal, float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
        rect.localScale = Vector3.one;
    }

    private static void ConfigureTitleButton(Button button, Transform parent, float y, float fontSize, Vector2? size = null)
    {
        button.transform.SetParent(parent, false);
        PlaceTitleRect(button.GetComponent<RectTransform>(), new Vector2(0.55f, y), size ?? new Vector2(460f, 96f));
        Image image = button.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0.62f);
        colors.highlightedColor = new Color(0.28f, 0.28f, 0.28f, 0.90f);
        colors.pressedColor = new Color(0.06f, 0.06f, 0.06f, 0.95f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        button.targetGraphic.CrossFadeColor(colors.normalColor, 0f, true, true);
        button.transition = Selectable.Transition.ColorTint;
        DialogueWindowFeather feather = button.GetComponent<DialogueWindowFeather>();
        if (feather == null) feather = button.gameObject.AddComponent<DialogueWindowFeather>();
        feather.ConfigureFeather(new Vector2(30f, 12f));
        if (button.GetComponent<ChoiceButtonHoverScale>() == null) button.gameObject.AddComponent<ChoiceButtonHoverScale>();
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        StretchTitleLabel(label.rectTransform, 22f, 10f);
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
    }

    public void OpenLoad()
    {
        if (isTransitioning || ArchiveManager.IsOpen) return;
        ArchiveManager.Instance.OpenLoadFromTitle();
    }

    private void Start()
    {
        bgmSource.volume = 0f;
        bgmSource.loop = true;
        bgmSource.Play();

        StartCoroutine(FadeIn());
    }

    public void StartGame()
    {
        if (isTransitioning || ArchiveManager.IsOpen)
            return;

        GameProgress.ResetAll();
        StartCoroutine(FadeOutAndLoadScene());
    }

    public void QuitGame()
    {
        if (isTransitioning || ArchiveManager.IsOpen) return;
        Application.Quit();
    }

    public void SetBGM(bool enabled)
    {
        bgmSource.mute = !enabled;
        StoryMusicController.SetEnabled(enabled);
        if (bgmLabel != null) bgmLabel.text = "BGM：" + (enabled ? "ON" : "OFF");
    }

    private IEnumerator FadeIn()
    {
        isTransitioning = true;

        fadePanel.alpha = 1f;
        fadePanel.blocksRaycasts = true;

        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;

            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            fadePanel.alpha = 1f - smoothT;

            bgmSource.volume = bgmVolume * smoothT;

            yield return null;
        }

        fadePanel.alpha = 0f;
        fadePanel.blocksRaycasts = false;

        bgmSource.volume = bgmVolume;

        isTransitioning = false;
    }

    private IEnumerator FadeOutAndLoadScene()
    {
        isTransitioning = true;

        fadePanel.blocksRaycasts = true;

        float startVolume = bgmSource.volume;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;

            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            fadePanel.alpha = smoothT;

            bgmSource.volume = startVolume * (1f - smoothT);

            yield return null;
        }

        fadePanel.alpha = 1f;

        bgmSource.volume = 0f;
        bgmSource.Stop();

        SceneManager.LoadSceneAsync("NovelScene");
    }
}
