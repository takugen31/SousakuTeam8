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
    private TMP_Text continueStatus;

    private void Awake()
    {
        Button start = null, quit = null, load = null, resume = null;
        foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (button.gameObject.scene != gameObject.scene) continue;
            if (button.name == "StartButton") start = button;
            if (button.name == "QuitButton") quit = button;
            if (button.name == "LoadButton") load = button;
            if (button.name == "ContinueButton") resume = button;
        }
        if (start == null) return;
        if (load == null) load = Instantiate(start, start.transform.parent);
        load.name = "LoadButton";
        load.onClick = new Button.ButtonClickedEvent();
        load.onClick.AddListener(OpenLoad);
        if (resume == null) resume = Instantiate(start, start.transform.parent);
        resume.name = "ContinueButton";
        resume.onClick = new Button.ButtonClickedEvent();
        resume.onClick.AddListener(ContinueGame);
        resume.interactable = GameSaveService.ReadContinue(out _) != null;
        ConfigureTitleLayout(start, resume, load, quit);
    }

    private void ConfigureTitleLayout(Button start, Button resume, Button load, Button quit)
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
        ConfigureTitleButton(start, canvas.transform, 0.34f, 32f, new Vector2(430f, 72f));
        ConfigureTitleButton(resume, canvas.transform, 0.26f, 32f, new Vector2(430f, 72f));
        ConfigureTitleButton(load, canvas.transform, 0.18f, 30f, new Vector2(350f, 60f));
        if (quit != null) ConfigureTitleButton(quit, canvas.transform, 0.10f, 30f, new Vector2(350f, 56f));
        resume.colors = start.colors;
        var resumeLabel = resume.GetComponentInChildren<TMP_Text>();
        if (resumeLabel != null) resumeLabel.text = "\u3064\u3065\u304d\u304b\u3089";
        var loadLabel = load.GetComponentInChildren<TMP_Text>();
        if (loadLabel != null) loadLabel.text = "\u30ed\u30fc\u30c9";


        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "TitleText")
            {
                text.gameObject.SetActive(false);
            }
            else if (text.name == "TeamText")
            {
                text.transform.SetParent(canvas.transform, false);
                PlaceTitleRect(text.rectTransform, new Vector2(0.5f, 0.035f), new Vector2(470f, 40f));
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

        ConfigureTitleLogo(canvas);
        var statusObject = new GameObject("ContinueStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        statusObject.transform.SetParent(canvas.transform, false);
        continueStatus = statusObject.GetComponent<TextMeshProUGUI>();
        continueStatus.font = font;
        continueStatus.fontSize = 22f;
        continueStatus.color = Color.white;
        continueStatus.alignment = TextAlignmentOptions.Center;
        continueStatus.raycastTarget = false;
        PlaceTitleRect(continueStatus.rectTransform, new Vector2(0.5f, 0.40f), new Vector2(1100f, 48f));


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
        ConfigureTitleButton(audioButton, canvas.transform, 0.045f, 21f, new Vector2(200f, 44f));
        PlaceTitleRect(audioControl.rectTransform, new Vector2(0.76f, 0.045f), new Vector2(200f, 44f));
        audioButton.onClick.AddListener(() =>
        {
            if (isTransitioning || ArchiveManager.IsOpen || bgmSource == null) return;
            SetBGM(bgmSource.mute);
        });
        if (fadePanel != null) fadePanel.transform.SetAsLastSibling();
    }

    private static void ConfigureTitleLogo(Canvas canvas)
    {
        Texture2D texture = Resources.Load<Texture2D>("TitleLogo");
        if (texture == null)
        {
            Debug.LogError("タイトルロゴ画像が見つかりません。");
            return;
        }
        var logoObject = new GameObject("TitleLogo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        logoObject.transform.SetParent(canvas.transform, false);
        RawImage logo = logoObject.GetComponent<RawImage>();
        logo.texture = texture;
        logo.raycastTarget = false;
        // Visible bounds of the supplied 3508 x 2480 PNG, with two pixels of padding.
        // Crop only the UI's UVs so the original artwork and transparency stay intact.
        Rect artwork = new Rect(346f, 475f, 2380f, 1537f);
        logo.uvRect = new Rect(artwork.x / 3508f, artwork.y / 2480f, artwork.width / 3508f, artwork.height / 2480f);
        float scale = Mathf.Min(880f / artwork.width, 520f / artwork.height);
        PlaceTitleRect(logo.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(artwork.width, artwork.height) * scale);
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
        colors.normalColor = button.name == "StartButton"
            ? new Color(0.14f, 0.14f, 0.14f, 0.55f)
            : new Color(0.06f, 0.06f, 0.06f, 0.4f);
        colors.highlightedColor = new Color(0.25f, 0.25f, 0.25f, 0.68f);
        colors.pressedColor = new Color(0.03f, 0.03f, 0.03f, 0.72f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.06f, 0.06f, 0.06f, 0.25f);
        colors.fadeDuration = 0.15f;
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

    public void ContinueGame()
    {
        if (isTransitioning || ArchiveManager.IsOpen) return;
        if (GameSaveService.ReadContinue(out string message) == null)
        {
            if (continueStatus != null) continueStatus.text = message;
            return;
        }
        StartCoroutine(FadeOutAndLoadScene(true));
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

    private IEnumerator FadeOutAndLoadScene(bool continuing = false)
    {
        isTransitioning = true;

        fadePanel.blocksRaycasts = true;

        float startVolume = bgmSource.volume;
        float elapsedTime = 0f;

        while (elapsedTime < TitleDialogueTransition.FadeOutDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsedTime / TitleDialogueTransition.FadeOutDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            fadePanel.alpha = smoothT;

            bgmSource.volume = startVolume * (1f - smoothT);

            yield return null;
        }

        fadePanel.alpha = 1f;

        bgmSource.volume = 0f;
        bgmSource.Stop();

        // A separate persistent canvas covers the destination before its first frame.
        TitleDialogueTransition transition = TitleDialogueTransition.Create();
        if (continuing)
        {
            if (!GameSaveService.Continue(out string message))
            {
                transition.Cancel();
                if (continueStatus != null) continueStatus.text = message;
                bgmSource.Play();
                yield return FadeIn();
                yield break;
            }
        }
        else
        {
            SceneManager.LoadSceneAsync("NovelScene");
        }
    }
}

// Kept across scene loading so neither the new scene nor its dialogue flashes early.
public sealed class TitleDialogueTransition : MonoBehaviour
{
    public const float FadeOutDuration = 1f;
    public const float HoldDuration = 1.7f;
    public const float FadeInDuration = 1f;
    public static bool IsActive { get; private set; }
    private CanvasGroup cover;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => IsActive = false;

    public static TitleDialogueTransition Create()
    {
        var root = new GameObject("TitleDialogueTransition", typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        DontDestroyOnLoad(root);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var panel = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        Image image = panel.GetComponent<Image>();
        image.color = Color.black;
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        var transition = root.AddComponent<TitleDialogueTransition>();
        transition.cover = root.GetComponent<CanvasGroup>();
        SceneManager.sceneLoaded += transition.OnDestinationLoaded;
        IsActive = true;
        return transition;
    }

    private void OnDestinationLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnDestinationLoaded;
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        yield return new WaitForSecondsRealtime(HoldDuration);
        float elapsed = 0f;
        while (elapsed < FadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cover.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / FadeInDuration));
            yield return null;
        }
        Cancel();
    }

    public void Cancel()
    {
        SceneManager.sceneLoaded -= OnDestinationLoaded;
        IsActive = false;
        Destroy(gameObject);
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

