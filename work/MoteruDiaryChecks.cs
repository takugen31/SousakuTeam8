using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SousakuTeam8.PuzzleGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MoteruDiaryChecks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if (SessionState.GetBool("MoteruDiaryChecks", false))
            new GameObject("Moteru diary integration checks").AddComponent<MoteruDiaryChecks>();
    }

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        PuzzleGameFlowController.Queue("PuzzleGame", "NovelScene_Moteru", "chapter_moteru_diary_001", null);
        SceneManager.LoadScene("PuzzleGame");
        yield return null;
        yield return null;
        var puzzle = FindFirstObjectByType<PuzzleGameController>();
        Check(puzzle != null, "production puzzle bootstrap failed");
        float started = Time.realtimeSinceStartup;
        // Use the production completion event; solving the board itself is outside this check.
        Call(puzzle, "BeginCompletion");
        yield return WaitForScene("NovelScene_Moteru");
        Check(Time.realtimeSinceStartup - started >= 1.75f, "puzzle return hold/fade skipped");
        Debug.Log("PASS: production puzzle completion returns to NovelScene_Moteru and queues the diary.");

        for (int selected = 0; selected < 2; selected++)
        {
            if (selected == 1)
            {
                NovelDialogueController.QueueResumeLine("chapter_moteru_diary_001", true);
                SceneManager.LoadScene("NovelScene_Moteru");
                yield return null;
            }
            NovelDialogueController dialogue = FindFirstObjectByType<NovelDialogueController>();
            Check(dialogue != null, "novel controller missing");
            yield return WaitForLine(dialogue, "chapter_moteru_diary_001");
            for (int row = 1; row <= 13; row++)
            {
                yield return Ready(dialogue);
                DialogueLine line = (DialogueLine)Get(dialogue, "currentLine");
                Check(line.lineId == "chapter_moteru_diary_" + row.ToString("000"), "wrong diary page: " + line.lineId);
                GameObject narration = (GameObject)Get(dialogue, "douteMonologueView");
                bool centered = row >= 2 && row <= 11;
                Check((narration != null) == centered, "monologue range incorrect: " + row);
                if (centered)
                {
                    Check(narration.name == "moteruCenteredNarration", "wrong monologue character");
                    var database = (CharacterDatabaseSO)Get(dialogue, "characterDatabase");
                    Check(database.TryGetCharacter("moteru", out CharacterData character), "Moteru missing from database");
                    Image portrait = narration.transform.Find("moteruBust").GetComponent<Image>();
                    Check(portrait.sprite == character.GetPortrait("normal"), "monologue does not use Moteru normal portrait");
                }
                Call(dialogue, "CompleteTyping");
                dialogue.Advance();
                yield return null;
            }
            yield return WaitForLine(dialogue, "chapter_moteru_diary_014");
            Call(dialogue, "CompleteTyping");
            float choiceStarted = Time.realtimeSinceStartup;
            while (!(bool)Get(dialogue, "isChoiceSelectionOpen"))
            {
                Check(Time.realtimeSinceStartup - choiceStarted < 5f, "choice window never opened");
                yield return null;
            }
            var buttons = (List<Button>)Get(dialogue, "spawnedChoiceButtons");
            Check(buttons.Count == 2, "expected two diary choices");
            buttons[selected].onClick.Invoke();
            if (selected == 0)
            {
                yield return WaitForScene("SelectionPart");
                Check(typeof(NovelDialogueController).GetField("pendingResumeLineId", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) == null,
                    "upper choice carried the declined Moteru branch into another route");
                Debug.Log("PASS: diary upper choice fades to SelectionPart without an extra empty page.");
            }
            else
            {
                foreach (int row in new[] { 16, 17 })
                {
                    yield return WaitForLine(dialogue, "chapter_moteru_diary_" + row.ToString("000"));
                    Check((GameObject)Get(dialogue, "douteMonologueView") == null, "normal dialogue not restored after diary");
                    Call(dialogue, "CompleteTyping");
                    dialogue.Advance();
                    yield return null;
                }
                yield return WaitForLine(dialogue, "chapter_moteru_4_001");
                Check(((DialogueScenarioSO)Get(dialogue, "currentScenario")).name == "Chapter_Moteru_After_Mini_Game",
                    "lower choice did not reach the existing conversation");
                Debug.Log("PASS: diary lower choice plays the two requested lines and fades into Chapter_Moteru_After_Mini_Game.");
            }
        }
        Check(!NovelDialogueController.IsMoteruDiaryLine("chapter_moteru_diary_001") &&
            !NovelDialogueController.IsMoteruDiaryLine("chapter_moteru_diary_012"), "diary boundaries incorrect");
        SessionState.SetBool("MoteruDiaryChecks", false);
        Debug.Log("PASS: Moteru diary integration completed.");
        EditorApplication.Exit(0);
    }

    private static IEnumerator Ready(NovelDialogueController dialogue)
    {
        float started = Time.realtimeSinceStartup;
        while ((bool)Get(dialogue, "isRoomLookPlaying") || (bool)Get(dialogue, "isChapterTransitioning") ||
            (bool)Get(dialogue, "isSceneLoading"))
        {
            Check(Time.realtimeSinceStartup - started < 15f, "dialogue transition stalled");
            yield return null;
        }
    }
    private static IEnumerator WaitForLine(NovelDialogueController dialogue, string id)
    {
        float started = Time.realtimeSinceStartup;
        while (((DialogueLine)Get(dialogue, "currentLine"))?.lineId != id)
        {
            Check(Time.realtimeSinceStartup - started < 15f, "line not reached: " + id);
            yield return null;
        }
        yield return Ready(dialogue);
    }
    private static IEnumerator WaitForScene(string scene)
    {
        float started = Time.realtimeSinceStartup;
        while (SceneManager.GetActiveScene().name != scene)
        {
            Check(Time.realtimeSinceStartup - started < 15f, "scene not reached: " + scene);
            yield return null;
        }
        yield return null;
    }
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Call(object target, string method, params object[] args)
    {
        try { target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); throw; }
    }
    private static void Check(bool ok, string message)
    {
        if (ok) return;
        Debug.LogError("FAIL: " + message);
        EditorApplication.Exit(1);
        throw new Exception(message);
    }
}

[InitializeOnLoad]
public static class MoteruDiaryBootstrap
{
    static MoteruDiaryBootstrap()
    {
        EditorApplication.update += () =>
        {
            if (Application.dataPath.Contains("MoteruSearchChecks-") && EditorApplication.isPaused)
                EditorApplication.isPaused = false;
        };
    }
    public static void Run()
    {
        if (!Application.dataPath.Contains("MoteruSearchChecks-")) throw new Exception("Use an isolated project.");
        var selection = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(selection, "Assets/SelectionPart.unity");
        // The importer validates the CSV path; create its scene asset at that path too.
        EditorSceneManager.SaveScene(selection, "Assets/Scenes/GameMap/SelectionPart.unity");
        var prepared = DialogueCsvImportService.Prepare(
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Dialogue/CSV/Characters.csv"),
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Dialogue/CSV/Moteru/chapter_moteru_diary.csv"));
        var scenario = AssetDatabase.LoadAssetAtPath<DialogueScenarioSO>("Assets/Dialogue/Data/Moteru/Chapter_Moteru_Diary.asset");
        if (scenario.Lines.Count != 17 || prepared.Lines.Count != 17) throw new Exception("Expected 17 diary rows");
        for (int i = 0; i < 17; i++)
            if (JsonUtility.ToJson(prepared.Lines[i]) != JsonUtility.ToJson(scenario.Lines[i]))
                throw new Exception("Diary CSV/asset mismatch: " + i);
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/GameMap/NovelScene_Moteru.unity", true),
            new EditorBuildSettingsScene("Assets/PuzzleGame/Scenes/PuzzleGame.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameMap/SelectionPart.unity", true)
        };
        Debug.Log("PASS: all 17 diary rows import identically to the asset.");
        SessionState.SetBool("MoteruDiaryChecks", true);
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }
}
