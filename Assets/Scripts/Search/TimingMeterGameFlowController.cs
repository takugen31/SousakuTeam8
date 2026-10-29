using System.Collections;
using SousakuTeam8.MeterGame.Timing;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Receives a Novel scene's transition reservation, shows the matching chibi
/// partner, and returns to the source scene after the result screen.
/// Opening TimingMeterGame directly keeps the standalone Kayo default.
/// </summary>
[DisallowMultipleComponent]
public sealed class TimingMeterGameFlowController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float resultDisplayDuration = 2f;

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

        ArchiveManager.Close();
        string resumeLineId = success
            ? activeRequest.SuccessResumeLineId
            : activeRequest.FailureResumeLineId;
        if (DialogueProgressTransitions.ReturnToScene(activeRequest.ReturnSceneName, resumeLineId))
            PlayerPrefs.DeleteKey(PendingReturnKey);
    }

}
