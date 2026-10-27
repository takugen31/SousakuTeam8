using System.Collections;
using SousakuTeam8.MeterGame.Timing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Receives a Novel scene's transition reservation, shows the matching chibi
/// partner, and returns to the source scene after the result screen and fade.
/// Opening TimingMeterGame directly keeps the standalone Kayo default.
/// </summary>
[DisallowMultipleComponent]
public sealed class TimingMeterGameFlowController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float resultDisplayDuration = 2f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.8f;

    [SerializeField] private TimingMeterController game;
    [SerializeField] private GameObject kayoChibi;
    [SerializeField] private GameObject moteruChibi;
    [SerializeField] private GameObject yowashiChibi;

    private const string PendingReturnKey = "TimingMeter.PendingReturn.v1";

    private static Request pendingRequest;
    // Runtime state only: Unity inline serialization can create an empty Request
    // when this scene is played directly.
    private Request activeRequest;
    private bool isReturning;
    private bool resultRecorded;

    [System.Serializable]
    private sealed class Request
    {
        public string MeterSceneName;
        public string ReturnSceneName;
        public string CharacterId;
        public string SuccessResumeLineId;
        public string FailureResumeLineId;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetPendingRequest()
    {
        pendingRequest = null;
        PlayerPrefs.DeleteKey(PendingReturnKey);
    }

    public static void Queue(
        string meterSceneName,
        string returnSceneName,
        string characterId,
        string successResumeLineId,
        string failureResumeLineId)
    {
        pendingRequest = new Request
        {
            MeterSceneName = meterSceneName,
            ReturnSceneName = returnSceneName,
            CharacterId = characterId,
            SuccessResumeLineId = successResumeLineId,
            FailureResumeLineId = failureResumeLineId
        };
        PlayerPrefs.SetString(PendingReturnKey, JsonUtility.ToJson(pendingRequest));
    }

    private void Start()
    {
        activeRequest = null;
        if (game == null) game = FindFirstObjectByType<TimingMeterController>();

        string characterId = "kayo";
        if (pendingRequest == null && PlayerPrefs.HasKey(PendingReturnKey))
            pendingRequest = JsonUtility.FromJson<Request>(PlayerPrefs.GetString(PendingReturnKey));

        if (pendingRequest != null && gameObject.scene.name == pendingRequest.MeterSceneName &&
            !string.IsNullOrWhiteSpace(pendingRequest.ReturnSceneName) &&
            Application.CanStreamedLevelBeLoaded(pendingRequest.ReturnSceneName))
        {
            activeRequest = pendingRequest;
            pendingRequest = null;
            ArchiveManager.Close();
            if (!string.IsNullOrWhiteSpace(activeRequest.CharacterId))
                characterId = activeRequest.CharacterId;
        }

        ApplyCharacter(characterId);
    }

    private void Update()
    {
        if (game == null) return;
        if (game.Phase != TimingMeterPhase.Complete)
        {
            resultRecorded = false;
            return;
        }

        bool success = game.Session != null && game.Session.IsSuccessful;
        if (!resultRecorded)
        {
            TimingGameResults.Save(activeRequest?.CharacterId, success);
            resultRecorded = true;
        }
        if (activeRequest == null || isReturning) return;

        isReturning = true;
        StartCoroutine(ReturnToSource(success));
    }

    private void ApplyCharacter(string characterId)
    {
        SetActive(kayoChibi, characterId == "kayo");
        SetActive(moteruChibi, characterId == "moteru");
        SetActive(yowashiChibi, characterId == "yowashi");
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    private IEnumerator ReturnToSource(bool success)
    {
        // Keep the final result visible unless a valid source scene was reserved.
        if (activeRequest == null || string.IsNullOrWhiteSpace(activeRequest.ReturnSceneName) ||
            !Application.CanStreamedLevelBeLoaded(activeRequest.ReturnSceneName))
            yield break;

        yield return new WaitForSecondsRealtime(resultDisplayDuration);

        Image overlay = BuildFadeOverlay();
        yield return Fade(overlay, 0f, 1f, fadeDuration);

        ArchiveManager.Close();
        string resumeLineId = success
            ? activeRequest.SuccessResumeLineId
            : activeRequest.FailureResumeLineId;
        NovelDialogueController.QueueResumeLine(resumeLineId, true);
        PlayerPrefs.DeleteKey(PendingReturnKey);
        SceneManager.LoadScene(activeRequest.ReturnSceneName, LoadSceneMode.Single);
    }

    private Image BuildFadeOverlay()
    {
        var canvasObject = new GameObject(
            "TimingMeterReturnFade",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var overlayObject = new GameObject(
            "Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = overlayObject.GetComponent<RectTransform>();
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var overlay = overlayObject.GetComponent<Image>();
        overlay.color = Color.clear;
        overlay.raycastTarget = true;
        return overlay;
    }

    private IEnumerator Fade(Image overlay, float from, float to, float duration)
    {
        overlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(from));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        overlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(to));
    }
}
