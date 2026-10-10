using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using Sousakusai8.MiniGame;

/// <summary>Touch input and the browser's controls outside the fixed-aspect game area.</summary>
public sealed class BrowserGameControls : MonoBehaviour
{
    private static bool jumpRequested;
    private int previousState = -1;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void SetBrowserGameControls(int state);
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => jumpRequested = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if UNITY_WEBGL
        var root = new GameObject("Browser Game Controls");
        DontDestroyOnLoad(root);
        root.AddComponent<BrowserGameControls>();
#endif
    }

    public static bool AdvancePressed => Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true ||
        Pointer.current?.press.wasPressedThisFrame == true;
    public static Vector2 PointerPosition => Touchscreen.current?.primaryTouch.press.isPressed == true
        ? Touchscreen.current.primaryTouch.position.ReadValue()
        : Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;

    public static bool ConsumeJump()
    {
        bool requested = jumpRequested;
        jumpRequested = false;
        return requested;
    }

    public void ToggleMenu() => ArchiveManager.Instance.ToggleArchive();
    public void Jump()
    {
        if (!ArchiveManager.IsOpen) jumpRequested = true;
    }
    public void Sweep()
    {
        if (!ArchiveManager.IsOpen)
            FindFirstObjectByType<CatchMiniGameController>()?.UseTouchSweep();
    }

    private void Update()
    {
        var game = FindFirstObjectByType<CatchMiniGameController>();
        int state = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Title" ? 0 : 1;
        if (!ArchiveManager.IsOpen && game != null && game.IsGameRunning)
        {
            if (game.CanPlayerJump) state |= 2;
            if (game.CanPlayerSweep) state |= 4;
        }
        if (ArchiveManager.IsOpen || game == null || !game.IsGameRunning) jumpRequested = false;
        if (state == previousState) return;
        previousState = state;
#if UNITY_WEBGL && !UNITY_EDITOR
        SetBrowserGameControls(state);
#endif
    }
}
