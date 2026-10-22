using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Saves only requested boundaries once their destination is ready for a checkpoint.</summary>
public sealed class GameAutoSaveController : MonoBehaviour
{
    private static GameAutoSaveController instance;
    private bool pendingSave;
    private int pendingScene;
    private bool suppressLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        instance = new GameObject("[AutoSave System]").AddComponent<GameAutoSaveController>();
        DontDestroyOnLoad(instance.gameObject);
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        pendingSave = false;
        if (suppressLoad) { suppressLoad = false; return; }
        RequestCheckpoint();
    }

    public static void RequestCheckpoint()
    {
        if (instance == null || instance.suppressLoad || !GameAutoSaveSettings.Enabled) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!GameSaveService.SupportsCheckpoint(scene.name)) return;
        instance.pendingSave = true;
        instance.pendingScene = scene.handle;
    }

    // Save the source before its controller becomes unavailable during a fade/unload.
    // Unsupported destinations (minigames) can then resume the boundary from this checkpoint.
    public static void SaveBeforeSceneChange()
    {
        if (instance == null || instance.suppressLoad || !GameAutoSaveSettings.Enabled) return;
        if (GameSaveService.SaveAuto(out _, true)) instance.pendingSave = false;
    }

    public static void CancelPending()
    {
        if (instance != null) instance.pendingSave = false;
    }

    public static void PrepareForLoad()
    {
        if (instance == null) return;
        instance.pendingSave = false;
        instance.suppressLoad = true;
    }

    public static void CancelLoadSuppression()
    {
        if (instance != null) instance.suppressLoad = false;
    }

    private void LateUpdate()
    {
        if (!pendingSave) return;
        if (!GameAutoSaveSettings.Enabled || SceneManager.GetActiveScene().handle != pendingScene)
        {
            pendingSave = false;
            return;
        }
        // Initial room-look animations and entrance/chapter fades must settle before capture.
        if (!GameSaveService.CanSave(out _)) return;
        pendingSave = false;
        if (!GameSaveService.SaveAuto(out string message))
            Debug.LogWarning("オートセーブできませんでした。 " + message);
    }
}
