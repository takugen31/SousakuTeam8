using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenu : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private CanvasGroup fadePanel;
    [SerializeField] private float fadeDuration = 1.0f;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;

    private bool isTransitioning = false;

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

        StartCoroutine(FadeOutAndLoadScene());
    }

    public void QuitGame()
    {
        Application.Quit();
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

        SceneManager.LoadSceneAsync("GameScene");
    }
}