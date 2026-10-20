// Copied into an isolated Unity project by verify-moteru-search.ps1.
using System;
using System.Collections;
using System.Reflection;
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
                while ((bool)Get(search, "isDialogueOpen"))
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
            Check((bool)Get(search, "isCompleting"), "completion did not wait for the final conversation");
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
            Check(typeof(PuzzleGameFlowController).GetField("pendingRequest", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) == null,
                "Moteru queued a Kayo diary return");
            Debug.Log("PASS: Moteru search order " + string.Join(" -> ", order) + ": art, prerequisite, acquisition UI, archives, all 24 dialogue pages, unscaled Kayo fade, PuzzleGame.");
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
        if (prepared.Lines.Count != 24 || scenario.Lines.Count != 24) throw new Exception("Expected 24 pages");
        for (int i=0; i<24; i++)
            if (JsonUtility.ToJson(prepared.Lines[i]) != JsonUtility.ToJson(scenario.Lines[i]))
                throw new Exception("CSV/asset mismatch at " + i);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/PuzzleGame.unity");
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/GameMap/Chapter_Moteru_SearchScene.unity", true),
            new EditorBuildSettingsScene("Assets/PuzzleGame.unity", true)
        };
        Debug.Log("PASS: production CSV imports identically to the scenario asset (24 pages).");
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }
}
