using System;
using System.Collections;
using System.Reflection;
using SousakuTeam8.MeterGame.Timing;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Invoked only by the isolated-project runner; never injected into the game project.
public static class TimingStartGateChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static IEnumerator Run()
    {
        ArchiveManager.Close();
        TimingMeterGameFlowController.ResetPendingRequest();
        SceneManager.LoadScene("TimingMeterGame"); yield return null;
        var game = UnityEngine.Object.FindFirstObjectByType<TimingMeterController>();
        Check(game != null && game.Phase == TimingMeterPhase.AwaitingStart, "scene starts playing immediately");
        var overlay = GameObject.Find("TimingStartOverlay");
        Check(overlay != null && overlay.GetComponent<Image>().color == new Color(0f,0f,0f,0.65f), "black overlay missing");
        var text = overlay.GetComponentInChildren<Text>();
        Check(text.color == Color.white && text.text == "クリックorSpaceキーで開始してください", "white start prompt missing");
        yield return new WaitForSecondsRealtime(0.05f);
        game.SubmitPress();
        Check(game.Session.Round.ElapsedSeconds == 0f && game.Session.Results.Count == 0, "waiting state advanced the game");

        // Batchmode has no focused Game view; route injected input to the game explicitly.
        var originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        var originalUpdateMode = InputSystem.settings.updateMode;
        var originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
        typeof(TimingMeterController).GetField("waitingSinceFrame", Private).SetValue(game, Time.frameCount);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
        Check(keyboard.spaceKey.isPressed, $"Injected Space did not reach keyboard; enabled={keyboard.enabled}, focus={Application.isFocused}");
        CallUpdate(game);
        Check(game.Phase == TimingMeterPhase.AwaitingStart, "scene-entry input started the game");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
        CallUpdate(game); game.SubmitPress();
        Check(game.Phase == TimingMeterPhase.Playing && !overlay.activeSelf &&
            game.Session.Round.ElapsedSeconds == 0f && game.Session.Results.Count == 0,
            $"Space start failed: phase={game.Phase}, overlay={overlay.activeSelf}, elapsed={game.Session.Round.ElapsedSeconds}, results={game.Session.Results.Count}, timeScale={Time.timeScale}, pressed={keyboard.spaceKey.wasPressedThisFrame}");
        InputSystem.Update(); // A new input update clears the press edge while Space remains held.
        yield return null;
        Check(game.Session.Results.Count == 0, "holding start Space judged the first set");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();

        game.RestartGame(); yield return null;
        Check(game.Phase == TimingMeterPhase.AwaitingStart && overlay.activeSelf, "restart did not restore start gate");
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); InputSystem.Update();
        CallUpdate(game);
        Check(game.Phase == TimingMeterPhase.Playing && !overlay.activeSelf && game.Session.Results.Count == 0,
            "mouse click failed to start or judged a set");
        InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.Update();
        InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
        InputSystem.settings.updateMode = originalUpdateMode;
        InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
        SceneManager.LoadScene("ProgressOther"); yield return null;
        Debug.Log("PASS: TimingMeter starts frozen under a black overlay, Space/click begin, entry/start input does not judge, and restart restores the gate.");
    }

    private static void CallUpdate(TimingMeterController game) =>
        typeof(TimingMeterController).GetMethod("Update", Private).Invoke(game, null);

    private static void Check(bool value, string message)
    {
        if (value) return;
        Debug.LogError("FAIL: " + message);
        EditorApplication.Exit(1);
        throw new Exception(message);
    }
}
