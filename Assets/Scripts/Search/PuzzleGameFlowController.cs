using System.Collections;
using SousakuTeam8.PuzzleGame;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Only a route's diary request activates this bridge. Opening PuzzleGame
/// directly keeps the existing standalone puzzle behaviour.
/// </summary>
[DisallowMultipleComponent]
public sealed class PuzzleGameFlowController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float completedDisplayDuration = 1f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.8f;
    private const string PendingReturnKey = "Puzzle.PendingReturn.v1";

    private static Request pendingRequest;
    [SerializeField] private Request activeRequest;
    [SerializeField] private PuzzleGameController puzzle;
    private bool isReturning;
    private bool cursorCaptured;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    [System.Serializable]
    private sealed class Request
    {
        public string PuzzleSceneName;
        public string ReturnSceneName;
        public string ResumeLineId;
        public Texture2D Image;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetPendingRequest()
    {
        pendingRequest = null;
        PlayerPrefs.DeleteKey(PendingReturnKey);
    }

    public static void Queue(
        string puzzleSceneName,
        string returnSceneName,
        string resumeLineId,
        Texture2D image)
    {
        pendingRequest = new Request
        {
            PuzzleSceneName = puzzleSceneName,
            ReturnSceneName = returnSceneName,
            ResumeLineId = resumeLineId,
            Image = image
        };
        PlayerPrefs.SetString(PendingReturnKey, JsonUtility.ToJson(pendingRequest));
    }

    private void OnEnable()
    {
        // Serialized context survives script reloads while the puzzle is open.
        ConnectCompletionEvent();
    }

    private void OnDisable()
    {
        if (puzzle != null) puzzle.Completed -= OnPuzzleCompleted;
    }

    private void ConnectCompletionEvent()
    {
        if (puzzle == null || activeRequest == null) return;
        puzzle.Completed -= OnPuzzleCompleted;
        puzzle.Completed += OnPuzzleCompleted;
        // Recover completion that occurred before the bridge reconnected.
        if (puzzle.IsComplete) OnPuzzleCompleted(puzzle.MoveCount);
    }

    private void Start()
    {
        if (pendingRequest == null && PlayerPrefs.HasKey(PendingReturnKey))
            pendingRequest = JsonUtility.FromJson<Request>(PlayerPrefs.GetString(PendingReturnKey));
        if (pendingRequest == null ||
            gameObject.scene.name != pendingRequest.PuzzleSceneName)
        {
            return;
        }

        // The standalone bootstrap creates the controller in Awake.
        puzzle = FindFirstObjectByType<PuzzleGameController>();
        if (puzzle == null)
        {
            Debug.LogError("日記パズルのコントローラーが見つかりません。", this);
            return;
        }

        activeRequest = pendingRequest;
        pendingRequest = null;
        ArchiveManager.Close();
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorCaptured = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (activeRequest.Image != null)
        {
            puzzle.SetPuzzleImage(activeRequest.Image);
        }

        ConnectCompletionEvent();
    }

    private void OnDestroy()
    {
        if (puzzle != null)
        {
            puzzle.Completed -= OnPuzzleCompleted;
        }

        if (cursorCaptured)
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }

    private void OnPuzzleCompleted(int moveCount)
    {
        if (isReturning || activeRequest == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(activeRequest.ResumeLineId) ||
            !Application.CanStreamedLevelBeLoaded(activeRequest.ReturnSceneName))
        {
            Debug.LogError("日記パズルの戻り先・再開位置が不正です。", this);
            return;
        }

        isReturning = true;
        puzzle.SetInteractable(false);
        StartCoroutine(ReturnToDiary());
    }

    private IEnumerator ReturnToDiary()
    {
        ArchiveManager.Close();
        yield return new WaitForSecondsRealtime(completedDisplayDuration);

        var canvasObject = new GameObject(
            "Puzzle Return Fade",
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

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        overlay.color = Color.black;
        ArchiveManager.Close();
        // The diary is the destination entry point in the shared progress timeline.
        if (DialogueProgressTransitions.ReturnToScene(activeRequest.ReturnSceneName, activeRequest.ResumeLineId))
            PlayerPrefs.DeleteKey(PendingReturnKey);
        else
        {
            isReturning = false;
            overlay.gameObject.SetActive(false);
            puzzle.SetInteractable(true);
        }
    }
}
