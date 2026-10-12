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
    private Material titleLetterMaterial;

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
        // Keep the sky composition visible. A subtle bottom shade replaces the opaque menu panel.
        Image shade = CreateTitlePanel("TitleBottomShade", canvas.transform, new Color(0.015f, 0.025f, 0.045f, 1f));
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = Vector2.one;
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
        shade.gameObject.AddComponent<TitleBottomShade>();
        ConfigureTitleButton(start, canvas.transform, 0.405f, 32f, new Vector2(390f, 72f));
        ConfigureTitleButton(load, canvas.transform, 0.265f, 30f, new Vector2(350f, 66f));
        if (quit != null) ConfigureTitleButton(quit, canvas.transform, 0.17f, 30f, new Vector2(350f, 66f));

        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "TitleText")
            {
                text.transform.SetParent(canvas.transform, false);
                PlaceTitleRect(text.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(1000f, 190f));
                text.color = Color.white;
                text.font = font;
                text.text = text.text.Replace("♡", "<color=#F2BACB>♡</color>");
                text.richText = true;
                text.characterSpacing = 4f;
                text.fontSize = 126f;
                text.enableAutoSizing = false;
                text.fontStyle = FontStyles.Normal;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
                titleLetterMaterial = new Material(text.fontSharedMaterial);
                titleLetterMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.16f, 0.30f, 0.7f));
                titleLetterMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.09f);
                titleLetterMaterial.EnableKeyword("UNDERLAY_ON");
                titleLetterMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.02f, 0.08f, 0.18f, 0.45f));
                titleLetterMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
                titleLetterMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                titleLetterMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
                text.fontSharedMaterial = titleLetterMaterial;
            }
            else if (text.name == "TeamText")
            {
                text.transform.SetParent(canvas.transform, false);
                PlaceTitleRect(text.rectTransform, new Vector2(0.5f, 0.085f), new Vector2(470f, 64f));
                text.color = new Color(0.035f, 0.09f, 0.14f, 0.85f);
                text.font = font;
                text.text = "<size=15>Created by</size>\n<size=21>新入生創作祭8班</size>";
                text.richText = true;
                text.fontStyle = FontStyles.Normal;
                text.fontSize = 21f;
                text.lineSpacing = 4f;
                text.enableAutoSizing = false;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
            }
        }

        Image rule = CreateTitlePanel("TitleRule", canvas.transform, new Color(1f, 1f, 1f, 0.38f));
        PlaceTitleRect(rule.rectTransform, new Vector2(0.5f, 0.535f), new Vector2(360f, 1.5f));

        foreach (Toggle toggle in canvas.GetComponentsInChildren<Toggle>(true))
            if (toggle.name == "BGMToggle") toggle.gameObject.SetActive(false);
        Image audioControl = CreateTitlePanel("BGMButton", canvas.transform, Color.white);
        audioControl.raycastTarget = true;
        Button audioButton = audioControl.gameObject.AddComponent<Button>();
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(audioControl.transform, false);
        bgmLabel = labelObject.GetComponent<TextMeshProUGUI>();
        bgmLabel.font = font;
        bgmLabel.text = "BGM：" + (bgmSource != null && bgmSource.mute ? "OFF" : "ON");
        ConfigureTitleButton(audioButton, canvas.transform, 0.095f, 21f, new Vector2(200f, 48f));
        PlaceTitleRect(audioControl.rectTransform, new Vector2(0.72f, 0.095f), new Vector2(200f, 48f));
        audioButton.onClick.AddListener(() =>
        {
            if (isTransitioning || ArchiveManager.IsOpen || bgmSource == null) return;
            SetBGM(bgmSource.mute);
        });
        if (fadePanel != null) fadePanel.transform.SetAsLastSibling();
    }

    private void OnDestroy()
    {
        if (titleLetterMaterial != null) Destroy(titleLetterMaterial);
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
        PlaceTitleRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, y), size ?? new Vector2(350f, 66f));
        Image image = button.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0.52f);
        colors.highlightedColor = new Color(0.10f, 0.12f, 0.16f, 0.72f);
        colors.pressedColor = new Color(0.02f, 0.03f, 0.05f, 0.82f);
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
        TitleMenuFocus focus = button.gameObject.AddComponent<TitleMenuFocus>();
        focus.Initialize(label);
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

// Code-native shade; no illustration or generated texture is added.
public sealed class TitleBottomShade : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount == 0) return;
        UIVertex seed = default;
        vertices.PopulateUIVertex(ref seed, 0);
        Rect rect = ((RectTransform)transform).rect;
        vertices.Clear();
        const int rows = 24;
        for (int i = 0; i <= rows; i++)
        {
            float y = (float)i / rows;
            UIVertex vertex = seed;
            Color color = seed.color;
            color.a *= 0.45f * Mathf.Pow(1f - y, 3f);
            vertex.color = color;
            vertex.position = new Vector3(rect.xMin, Mathf.Lerp(rect.yMin, rect.yMax, y));
            vertices.AddVert(vertex);
            vertex.position.x = rect.xMax;
            vertices.AddVert(vertex);
            if (i == 0) continue;
            int offset = (i - 1) * 2;
            vertices.AddTriangle(offset, offset + 2, offset + 1);
            vertices.AddTriangle(offset + 1, offset + 2, offset + 3);
        }
    }
}

public sealed class TitleMenuFocus : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.ISelectHandler,
    UnityEngine.EventSystems.IDeselectHandler
{
    private TMP_Text label;
    private bool hovered;
    private bool selected;
    public void Initialize(TMP_Text text) { label = text; }
    private void Refresh() { if (label != null) label.color = hovered || selected ? new Color(1f, 0.82f, 0.89f) : Color.white; }
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { hovered = true; Refresh(); }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { hovered = false; Refresh(); }
    public void OnSelect(UnityEngine.EventSystems.BaseEventData e) { selected = true; Refresh(); }
    public void OnDeselect(UnityEngine.EventSystems.BaseEventData e) { selected = false; Refresh(); }
}
