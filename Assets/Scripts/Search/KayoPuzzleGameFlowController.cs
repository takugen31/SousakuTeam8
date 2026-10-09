using System.Collections;
using SousakuTeam8.PuzzleGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Only a route's diary request activates this bridge. Opening PuzzleGame
/// directly keeps the existing standalone puzzle behaviour.
/// </summary>
[DisallowMultipleComponent]
public sealed class KayoPuzzleGameFlowController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float completedDisplayDuration = 1f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.8f;

    private static Request pendingRequest;
    private Request activeRequest;
    private PuzzleGameController puzzle;
    private bool isReturning;
    private bool cursorCaptured;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private sealed class Request
    {
        public string PuzzleSceneName;
        public string ReturnSceneName;
        public string ResumeLineId;
        public Texture2D Image;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPendingRequest()
    {
        pendingRequest = null;
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
    }

    private void Start()
    {
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

        puzzle.Completed += OnPuzzleCompleted;
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
            "Kayo Puzzle Return Fade",
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
        // Overwrite any earlier search resume request; the diary must be read first.
        NovelDialogueController.QueueResumeLine(activeRequest.ResumeLineId, true);
        SceneManager.LoadScene(activeRequest.ReturnSceneName, LoadSceneMode.Single);
    }
}
