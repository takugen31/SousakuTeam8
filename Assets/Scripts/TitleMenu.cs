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
        if (isTransitioning)
            return;

        GameProgress.ResetAll();
        StartCoroutine(FadeOutAndLoadScene());
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void SetBGM(bool enabled)
    {
        bgmSource.mute = !enabled;
        StoryMusicController.SetEnabled(enabled);
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
