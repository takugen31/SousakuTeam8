// Copied into an isolated Unity project by verify-moteru-search.ps1.
using System;
using System.Collections;
using System.Reflection;
using SousakuTeam8.PuzzleGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MoteruSearchChecks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if (Application.dataPath.Contains("MoteruSearchChecks-"))
            new GameObject("Search integration checks").AddComponent<MoteruSearchChecks>();
    }

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        foreach (string[] order in new[] {
            new[] { "bookshelf", "paper_scrap", "kayo_yowashi" },
            new[] { "bookshelf", "kayo_yowashi", "paper_scrap" },
            new[] { "kayo_yowashi", "bookshelf", "paper_scrap" } })
        {
            PlayerPrefs.DeleteKey("MoteruSearch.Acquired.v1");
            PlayerPrefs.SetString("KayoSearch.Acquired.v1", "{\"acquiredItemIds\":[\"bookshelf\",\"floor\",\"paper_scrap\"]}");
            SceneManager.LoadScene("Chapter_Moteru_SearchScene");
            yield return null;
            SearchSceneController search = FindFirstObjectByType<SearchSceneController>();
            Check(search != null, "search controller missing");
            float started = Time.realtimeSinceStartup;
            while ((bool)Get(search, "isFadingIn"))
            {
                Check(Time.realtimeSinceStartup - started < 4f, "entry fade stalled");
                yield return null;
            }
            Check(!search.CanInspect("paper_scrap"), "paper available before bookshelf");
            Check(search.CanInspect("bookshelf") && search.CanInspect("kayo_yowashi"), "Kayo progress leaked into Moteru");
            Check(!search.CanInspect("floor"), "copied Kayo floor is still collectible");
            SearchSceneProfileSO profile = (SearchSceneProfileSO)Get(search, "searchProfile");
            foreach (SearchItemSettings item in profile.items)
            {
                GameObject placed = FindChild(search.transform, "PlacedImage_" + item.id);
                Check(placed != null, "placed art missing: " + item.id);
                RectTransform rect = placed.GetComponent<RectTransform>();
                Check(rect.anchorMin == item.anchorMin && rect.anchorMax == item.anchorMax, "art/hotspot positions diverged");
                if (item.id == "kayo_yowashi") Check(placed.GetComponentsInChildren<Graphic>(true).Length == 2, "paired portraits missing");
            }
            foreach (string id in order)
            {
                SearchItemSettings item = profile.items.Find(candidate => candidate.id == id);
                Type roomType = search.GetType().GetNestedType("RoomView", BindingFlags.NonPublic);
                Call(search, "ShowRoom", Enum.ToObject(roomType, item.room));
                Check(search.CanInspect(id), "item unavailable: " + id);
                SearchSceneHotspot hotspot = Array.Find(search.GetComponentsInChildren<SearchSceneHotspot>(true), candidate => candidate.ItemId == id);
                hotspot.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                Check((bool)Get(search, "isModalOpen"), "acquisition UI not open");
                Check(ArchiveManager.IsUnlocked(item.archiveId), "archive entry missing: " + item.archiveId);
                if (id == "paper_scrap")
                {
                    RawImage still = (RawImage)Get(search, "modalStill");
                    Rect slice = item.sprite.rect;
                    Rect expectedUv = new Rect(slice.x / item.sprite.texture.width, slice.y / item.sprite.texture.height,
                        slice.width / item.sprite.texture.width, slice.height / item.sprite.texture.height);
                    Check(still.texture == item.sprite.texture && still.uvRect == expectedUv,
                        "acquisition UI ignores the updated paper sprite slice");
                    float ratio = ((AspectRatioFitter)Get(search, "modalStillAspect")).aspectRatio;
                    Check(Mathf.Abs(ratio - slice.width / slice.height) < 0.001f,
                        "updated paper is stretched in acquisition UI");
                }
                Call(search, "CloseInspection");
                yield return null;
                yield return null;
                var dialogue = (NovelDialogueController)Get(search, "itemDialogueController");
                Check((bool)Get(search, "isDialogueOpen"), "post-acquisition dialogue did not start");
                if (id == "bookshelf")
                    Check(!FindChild(search.transform, "PlacedImage_paper_scrap").activeSelf,
                        "paper appeared before the bookshelf conversation finished");
                if (id == "kayo_yowashi")
                    Check(FindChild(search.transform, "PlacedImage_kayo_yowashi").activeSelf,
                        "people disappeared before their conversation finished");
                int pages = 0;
                object acquisitionDialogue = Get(search, "currentDialogueItem");
                while ((bool)Get(search, "isDialogueOpen") && ReferenceEquals(Get(search, "currentDialogueItem"), acquisitionDialogue))
                {
                    Check(pages++ < 25, "dialogue failed to stop at configured last line");
                    Check(!search.CanInspect(id), "collected item can be inspected twice");
                    Call(dialogue, "CompleteTyping");
                    dialogue.Advance();
                    yield return null;
                }
                int expectedPages = id == "kayo_yowashi" ? 17 : id == "bookshelf" ? 4 : 3;
                Check(pages == expectedPages, "dialogue range mismatch: " + id + " / " + pages);
                if (id == "bookshelf")
                {
                    Canvas.ForceUpdateCanvases();
                    GameObject paper = FindChild(search.transform, "PlacedImage_paper_scrap");
                    Check(paper.activeInHierarchy, "paper stays hidden after bookshelf dialogue");
                    Image art = paper.GetComponentInChildren<Image>();
                    Check(art != null && art.sprite != null && art.preserveAspect && art.rectTransform.rect.width > 1f && art.rectTransform.rect.height > 1f,
                        "paper art is missing or has a zero-size rect: texture=" + art?.mainTexture +
                        ", rect=" + art?.rectTransform.rect + ", parent=" + paper.GetComponent<RectTransform>().rect +
                        ", screen=" + Screen.width + "x" + Screen.height);
                }
            }
            Check((bool)Get(search, "isDialogueOpen") && !(bool)Get(search, "isCompleting"),
                "search faded before the ending conversation");
            yield return null;
            yield return null;
            var ending = (NovelDialogueController)Get(search, "itemDialogueController");
            for (int row = 1; row <= 3; row++)
            {
                float lineStarted = Time.realtimeSinceStartup;
                while ((bool)Get(ending, "isChapterTransitioning"))
                {
                    Check(Time.realtimeSinceStartup - lineStarted < 10f, "ending line fade stalled");
                    yield return null;
                }
                Check(((DialogueLine)Get(ending, "currentLine")).lineId == "search_moteru_" + row.ToString("000"),
                    "wrong ending conversation row");
                Check(!(bool)Get(search, "isCompleting") && !search.CanInspect("bookshelf"),
                    "fade or inspection started during the ending conversation");
                Call(ending, "CompleteTyping");
                if (row == 2)
                {
                    var checkpoint = new GameSaveData();
                    Check(search.CaptureCheckpoint(checkpoint) && checkpoint.itemId == "search_completion" &&
                        checkpoint.lineId == "search_moteru_002" && checkpoint.stopLineId == "search_moteru_003",
                        "ending conversation checkpoint was not captured");
                    Call(search, "RestoreCheckpoint", checkpoint);
                    Check(((DialogueLine)Get(ending, "currentLine")).lineId == "search_moteru_002" &&
                        !(bool)Get(search, "isCompleting"), "ending conversation checkpoint skipped to the fade");
                }
                ending.Advance();
                yield return null;
            }
            Check((bool)Get(search, "isCompleting"), "fade did not start after ending row 003");
            var overlay = (Image)Get(search, "completionFadeOverlay");
            started = Time.realtimeSinceStartup;
            Time.timeScale = 0f;
            float previousAlpha = overlay.color.a;
            while (SceneManager.GetActiveScene().name == "Chapter_Moteru_SearchScene")
            {
                Check(Time.realtimeSinceStartup - started < 8f, "completion transition stalled");
                Check(overlay.color.a >= previousAlpha, "completion fade is not monotonic");
                Check(overlay.raycastTarget, "completion fade does not block input");
                previousAlpha = overlay.color.a;
                yield return null;
            }
            Time.timeScale = 1f;
            Check(SceneManager.GetActiveScene().name == "PuzzleGame", "wrong completion scene");
            Check(Time.realtimeSinceStartup - started >= 4.3f, "Kayo 2.5s fade + 2s hold was skipped");
            object request = typeof(PuzzleGameFlowController).GetField("pendingRequest", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var flow = FindFirstObjectByType<PuzzleGameFlowController>();
            if (request == null && flow != null) request = Get(flow, "activeRequest");
            Check(request != null && (string)request.GetType().GetField("ReturnSceneName").GetValue(request) == "NovelScene_Moteru" &&
                (string)request.GetType().GetField("ResumeLineId").GetValue(request) == "chapter_moteru_diary_001",
                "Moteru diary return was not queued");
            yield return null;
            yield return null;
            var puzzle = FindFirstObjectByType<PuzzleGameController>();
            Check(puzzle != null, "production puzzle controller missing");
            started = Time.realtimeSinceStartup;
            // Trigger the real completion event; manually solving the board is outside this check.
            Call(puzzle, "BeginCompletion");
            while (SceneManager.GetActiveScene().name == "PuzzleGame")
            {
                Check(Time.realtimeSinceStartup - started < 8f, "puzzle did not return to the diary");
                yield return null;
            }
            Check(SceneManager.GetActiveScene().name == "NovelScene_Moteru", "wrong puzzle return scene");
            Check(Time.realtimeSinceStartup - started >= 1.75f, "shared puzzle completion hold/fade skipped");
            yield return null;
            var novel = FindFirstObjectByType<NovelDialogueController>();
            Check(novel != null, "production novel controller missing");
            while ((bool)Get(novel, "isChapterTransitioning") || Get(novel, "currentLine") == null)
            {
                Check(Time.realtimeSinceStartup - started < 12f, "diary entry fade stalled");
                yield return null;
            }
            Check(((DialogueLine)Get(novel, "currentLine")).lineId == "chapter_moteru_diary_001" &&
                ((DialogueScenarioSO)Get(novel, "currentScenario")).name == "Chapter_Moteru_Diary",
                "puzzle returned to the beginning of the route instead of the diary");
            Debug.Log("PASS: Moteru search order " + string.Join(" -> ", order) + ": ending 001-003, checkpoint restore, search fade, production PuzzleGame completion, NovelScene_Moteru diary 001.");
        }
        Check(GameProgress.IsValidCheckpoint(new[] {
            new GameSavePreference { key="ArchiveState.v1" }, new GameSavePreference { key="AffectionState" },
            new GameSavePreference { key="Chapter1Search.Acquired.v1" }, new GameSavePreference { key="KayoSearch.Acquired.v1" }
        }), "legacy save compatibility lost");
        var progress = GameProgress.CaptureCheckpoint();
        Check(progress.Length == 5 && GameProgress.IsValidCheckpoint(progress) &&
            Array.Exists(progress, entry => entry.key == "MoteruSearch.Acquired.v1" && entry.present),
            "new search progress not captured for save/load");
        var kayoObject = new GameObject("Kayo default regression");
        kayoObject.SetActive(false);
        var kayo = kayoObject.AddComponent<SearchSceneController>();
        Call(kayo, "BuildItemDefinitions");
        var kayoItems = (IList)Get(kayo, "items");
        Check(kayoItems.Count == 3 && (string)kayoItems[1].GetType().GetProperty("Id").GetValue(kayoItems[1]) == "floor",
            "Kayo default item definitions changed");
        Destroy(kayoObject);
        Debug.Log("PASS: Moteru search integration completed (3 valid acquisition orders).");
        EditorApplication.Exit(0);
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static GameObject FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child.gameObject;
        return null;
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
public static class MoteruSearchBootstrap
{
    static MoteruSearchBootstrap()
    {
        // An unrelated editor import error must not pause the batch-mode player loop.
        EditorApplication.update += () =>
        {
            if (Application.dataPath.Contains("MoteruSearchChecks-") && EditorApplication.isPaused)
                EditorApplication.isPaused = false;
        };
    }

    // Explicit control-flow fixture for an unsaved ending script; never touches the game project.
    public static void RunWithCompletionFixture()
    {
        if (!Application.dataPath.Contains("MoteruSearchChecks-")) throw new Exception("Use the isolated test project.");
        const string csvPath = "Assets/Dialogue/CSV/Moteru/chapter_moteru_search.csv";
        var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);
        if (!csv.text.Contains("search_moteru_001,"))
        {
            string text = csv.text.TrimEnd() + "\n";
            for (int i = 1; i <= 3; i++)
                text += "search_moteru_" + i.ToString("000") + ",doute,normal,,TEST ENDING " + i + "," +
                    (i < 3 ? "search_moteru_" + (i + 1).ToString("000") : "") + "\n";
            System.IO.File.WriteAllText(csvPath, text);
            AssetDatabase.ImportAsset(csvPath, ImportAssetOptions.ForceSynchronousImport);
            var prepared = DialogueCsvImportService.Prepare(
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Dialogue/CSV/Characters.csv"),
                AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath));
            var scenario = AssetDatabase.LoadAssetAtPath<DialogueScenarioSO>("Assets/Dialogue/Data/Moteru/Chapter_Moteru_Search.asset");
            scenario.ReplaceAll(prepared.Lines);
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssetIfDirty(scenario);
            Debug.Log("INFO: ending text is a test-only fixture in the isolated project; production CSV was not modified.");
        }
        Run();
    }

    public static void Run()
    {
        if (!Application.dataPath.Contains("MoteruSearchChecks-")) throw new Exception("Use the isolated test project.");
        var characters = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Dialogue/CSV/Characters.csv");
        var csv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Dialogue/CSV/Moteru/chapter_moteru_search.csv");
        var prepared = DialogueCsvImportService.Prepare(characters, csv);
        var scenario = AssetDatabase.LoadAssetAtPath<DialogueScenarioSO>("Assets/Dialogue/Data/Moteru/Chapter_Moteru_Search.asset");
        var profile = AssetDatabase.LoadAssetAtPath<SearchSceneProfileSO>("Assets/Dialogue/Data/Moteru/MoteruSearchProfile.asset");
        var paper = profile.items.Find(item => item.id == "paper_scrap");
        if (paper.sprite == null || AssetDatabase.GetAssetPath(paper.sprite) != "Assets/Art/Items/SearchGame/paper_fragment.png")
            throw new Exception("Paper sprite reference is unresolved or points to the wrong asset");
        if (paper.uvRect != new Rect(0, 0, 1, 1))
            throw new Exception("Paper UV rectangle is invalid: " + paper.uvRect);
        if (prepared.Lines.Count != 27 || scenario.Lines.Count != 27) throw new Exception("Expected 24 acquisition pages and 3 ending pages; save/sync the CSV first");
        for (int i=0; i<27; i++)
            if (JsonUtility.ToJson(prepared.Lines[i]) != JsonUtility.ToJson(scenario.Lines[i]))
                throw new Exception("CSV/asset mismatch at " + i);
        EditorSceneManager.OpenScene("Assets/Scenes/GameMap/Chapter_Moteru_SearchScene.unity");
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/GameMap/Chapter_Moteru_SearchScene.unity", true),
            new EditorBuildSettingsScene("Assets/PuzzleGame/Scenes/PuzzleGame.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameMap/NovelScene_Moteru.unity", true)
        };
        Debug.Log("PASS: CSV imports identically to the scenario asset (27 pages).");
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }
}
