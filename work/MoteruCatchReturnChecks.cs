// Isolated Unity integration test. The game-end event and dialogue entry boundary
// are test doubles; the flow, fade, scene loading and Unity timing are production code.
using System;
using System.Collections;
using System.Reflection;
using Sousakusai8.MiniGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MoteruCatchReturnChecks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        Debug.Log("Moteru return checks: launching play-mode probe.");
        new GameObject("Moteru Return Integration Checks").AddComponent<MoteruCatchReturnChecks>();
    }

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        foreach (int score in new[] { 0, 3000, 10000 })
        {
            if (SceneManager.GetActiveScene().name != "Source")
            {
                SceneManager.LoadScene("Source");
                yield return null;
            }
            var resume = Resources.Load<DialogueScenarioSO>("Resume");
            var game = new GameObject("Mini Game Event Probe").AddComponent<CatchMiniGameController>();
            var flowObject = new GameObject("Moteru Return");
            flowObject.SetActive(false);
            var flow = flowObject.AddComponent<MoteruCatchGameFlowController>();
            Set(flow, "miniGame", game);
            Set(flow, "resumeScenario", resume);
            flowObject.SetActive(true);
            yield return null;
            Image overlay = (Image)Get(flow, "fadeOverlay");
            Check(overlay != null && !overlay.gameObject.activeSelf, "overlay blocks gameplay");
            Canvas canvas = overlay.GetComponentInParent<Canvas>();
            Check(canvas.sortingOrder == 500 && canvas.renderMode == RenderMode.ScreenSpaceOverlay, "overlay differs from Kayo canvas");
            NovelDialogueController.QueueResumeLine("stale_game_entry", false);
            ArchiveManager.Closed = false;
            Time.timeScale = 0f;
            float startedAt = Time.realtimeSinceStartup;
            game.Finish(score);
            game.Finish(score); // A duplicate signal must not start a second transition.
            float lastAlpha = 0f;
            bool sawFade = false;
            while (SceneManager.GetActiveScene().name == "Source")
            {
                float elapsed = Time.realtimeSinceStartup - startedAt;
                Check(elapsed < 8f, "return transition stalled");
                Check(game.PresentationVisible, "score presentation was hidden before unloading");
                if (elapsed < 1.95f) Check(!overlay.gameObject.activeSelf, "fade started before the two-second result hold");
                if (overlay.gameObject.activeSelf)
                {
                    sawFade = true;
                    Check(overlay.raycastTarget, "fade does not block input");
                    Check(overlay.color.r == 0f && overlay.color.g == 0f && overlay.color.b == 0f, "fade is not black");
                    Check(overlay.color.a >= lastAlpha && overlay.color.a <= 1f, "fade is not monotonic");
                    float expected = Mathf.Clamp01((elapsed - 2f) / 0.8f);
                    Check(Mathf.Abs(overlay.color.a - expected) < 0.15f, "fade differs from Kayo's linear duration");
                    lastAlpha = overlay.color.a;
                }
                yield return null;
            }
            Check(sawFade && Time.realtimeSinceStartup - startedAt >= 2.75f, "result/fade timing was skipped");
            Check(SceneManager.GetActiveScene().name == "NovelScene_Moteru", "wrong return scene");
            Check(NovelDialogueController.ResumeLine == "chapter_moteru_2_001" && NovelDialogueController.FadeIn, "wrong resume line or missing arrival fade");
            Check(ArchiveManager.Closed, "archive not closed before transition");
            Time.timeScale = 1f;
            Debug.Log($"PASS: Moteru return score={score}: 2s result, 0.8s black fade, destination, Chapter_Moteru_2 first line, stale queue override, duplicate guard, unscaled timing.");
        }
        Debug.Log("PASS: Moteru catch return integration completed (3 scores).");
        EditorApplication.Exit(0);
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool value, string message)
    {
        if (value) return;
        Debug.LogError("FAIL: " + message);
        EditorApplication.Exit(1);
        throw new Exception(message);
    }
}

[InitializeOnLoad]
public static class MoteruCatchReturnBootstrap
{
    static MoteruCatchReturnBootstrap()
    {
        EditorApplication.playModeStateChanged += state => Debug.Log("Moteru return checks: " + state);
    }

    public static void Run()
    {
        if (!Application.dataPath.Contains("MoteruCatchReturnChecks-"))
            throw new Exception("Use the isolated test project.");
        AssetDatabase.CreateFolder("Assets", "Resources");
        var resume = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        resume.ReplaceAll(new System.Collections.Generic.List<DialogueLine>
        {
            new DialogueLine { lineId = "chapter_moteru_2_001", text = "resume" }
        });
        AssetDatabase.CreateAsset(resume, "Assets/Resources/Resume.asset");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/NovelScene_Moteru.unity");
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Source.unity");
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Source.unity", true),
            new EditorBuildSettingsScene("Assets/NovelScene_Moteru.unity", true)
        };
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }
}
