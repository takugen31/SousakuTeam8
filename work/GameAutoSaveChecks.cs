using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class DialogueSceneProgressChecks
{
    private IEnumerator RunAutoSaveChecks()
    {
        const string key = "GameSave.Auto.v1";
        GameProgress.ResetAll();
        PlayerPrefs.DeleteKey(key);
        UseConfiguration("{\"enabled\":false}");
        GameAutoSaveSettings.Enabled = true;
        SceneManager.LoadScene(A); yield return null;
        var a = Create(main);
        yield return null; yield return null;
        var automatic = GameSaveService.ReadAuto(out _);
        Check(automatic != null && automatic.scene == A && automatic.lineId == "a1", "scene entry did not automatically save Moteru with progress OFF");
        var manualSnapshots = new string[GameSaveService.SlotCount];
        for (int slot = 0; slot < manualSnapshots.Length; slot++)
        {
            Check(GameSaveService.Save(slot, out _), "could not seed manual slot " + slot);
            manualSnapshots[slot] = PlayerPrefs.GetString("GameSave.Slot.v1." + slot);
        }
        string initialAuto = PlayerPrefs.GetString(key);
        Complete(a); a.Advance(); yield return null; yield return null;
        Check(PlayerPrefs.GetString(key) == initialAuto, "ordinary line advance overwrote the boundary checkpoint");

        // Follow the production chapter switch, including its deferred capture during a fade.
        var first = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        first.name = "AutoFirst"; first.hideFlags = HideFlags.DontUnloadUnusedAsset;
        first.ReplaceAll(new List<DialogueLine> { Line("auto_first") });
        var next = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        next.name = "AutoNext"; next.hideFlags = HideFlags.DontUnloadUnusedAsset;
        next.ReplaceAll(new List<DialogueLine> { Line("auto_next", 3), Line("auto_next_2") });
        // Only one dialogue owner should be selected by the checkpoint service.
        a.gameObject.SetActive(false);
        var chapter = Create(first, "chapter_auto");
        Set(chapter, "followingScenarios", new List<DialogueScenarioSO> { next });
        Set(chapter, "useChapterTransitionFade", true);
        Set(chapter, "chapterFadeOutDuration", 0.02f);
        Set(chapter, "chapterFadeHoldDuration", 0.02f);
        Set(chapter, "chapterFadeInDuration", 0.02f);
        yield return null;
        Complete(chapter); chapter.Advance();
        for (int frame = 0; frame < 90; frame++)
        {
            yield return new WaitForSecondsRealtime(0.01f);
            if (GameSaveService.ReadAuto(out _)?.scenario == next.name) break;
        }
        automatic = GameSaveService.ReadAuto(out _);
        GameSaveService.CanSave(out string boundaryReason);
        Check(automatic != null && automatic.scenario == next.name && automatic.lineId == "auto_next", "asset boundary did not capture the destination chapter: " +
            "current=" + Id(chapter) + ", playing=" + Get(chapter, "isPlaying") + ", fade=" + Get(chapter, "isChapterTransitioning") +
            ", roomLook=" + Get(chapter, "isRoomLookPlaying") + ", reason=" + boundaryReason + ", saved=" + JsonUtility.ToJson(automatic));
        for (int slot = 0; slot < manualSnapshots.Length; slot++)
            Check(PlayerPrefs.GetString("GameSave.Slot.v1." + slot) == manualSnapshots[slot], "automatic save overwrote manual slot " + slot);
        Debug.Log("PASS: scene entry and chapter asset boundaries create separate automatic checkpoints with dialogue progress OFF; ordinary line advances and all six manual slots remain unchanged.");

        // Exercise the actual menu buttons, title entry, nested back and confirmation paths.
        var menu = ArchiveManager.Instance.GetComponent<ArchiveMenuUI>();
        ArchiveManager.Instance.OpenArchive();
        Button setting = FindMenuButton(menu, "AutoSaveSetting");
        setting.onClick.Invoke();
        Check(!GameAutoSaveSettings.Enabled && setting.GetComponentInChildren<TMP_Text>().text.EndsWith("OFF"), "Esc auto-save setting did not turn OFF");
        initialAuto = PlayerPrefs.GetString(key);
        GameAutoSaveController.RequestCheckpoint();
        GameAutoSaveController.SaveBeforeSceneChange();
        yield return null;
        Check(PlayerPrefs.GetString(key) == initialAuto, "OFF still wrote an automatic checkpoint");
        FindMenuButton(menu, "Load").onClick.Invoke();
        Check(((GameObject)Get(menu, "loadSourceRoot")).activeSelf && !((GameObject)Get(menu, "manualSlotsRoot")).activeSelf, "Load did not show the source chooser first");
        FindMenuButton(menu, "ManualLoad").onClick.Invoke();
        Check(((GameObject)Get(menu, "manualSlotsRoot")).activeSelf && ((Button[])Get(menu, "saveSlotButtons")).Length == 6, "manual source did not show six slots");
        FindMenuButton(menu, "BackToMenu").onClick.Invoke();
        Check(((GameObject)Get(menu, "loadSourceRoot")).activeSelf, "manual Back did not return to source selection");
        FindMenuButton(menu, "AutoLoad").onClick.Invoke();
        Check(((GameObject)Get(menu, "saveLoadConfirmation")).activeSelf, "automatic load skipped confirmation");
        var confirmation = (GameObject)Get(menu, "saveLoadConfirmation");
        confirmation.transform.Find("Confirmation/No").GetComponent<Button>().onClick.Invoke();
        Check(!confirmation.activeSelf && PlayerPrefs.GetString(key) == initialAuto, "canceling automatic load changed the data");
        // Optional screenshot exercises use the same real production UI.
        yield return CaptureAutoSaveUI(menu);
        menu.Close();
        menu.Open(); menu.ShowLoadFromTitle();
        FindMenuButton(menu, "ManualLoad").onClick.Invoke();
        FindMenuButton(menu, "BackToMenu").onClick.Invoke();
        Check(((TMP_Text)Get(menu, "saveLoadBackLabel")).text == "← タイトル", "nested title Back lost its return destination");
        FindMenuButton(menu, "BackToMenu").onClick.Invoke();
        Check(!menu.IsOpen, "title Back did not close the load window");
        Debug.Log("PASS: production Esc toggle, load-source selection, six manual cards, automatic confirmation/cancel and nested title/menu Back routes work.");

        // An automatic checkpoint remains loadable while automatic writes are disabled.
        Check(GameSaveService.LoadAuto(out string message), "automatic load while OFF failed: " + message);
        yield return null;
        chapter = Create(first, "chapter_auto");
        Set(chapter, "followingScenarios", new List<DialogueScenarioSO> { next });
        yield return null; yield return null;
        Check(Id(chapter) == "auto_next", "automatic load did not restore the saved asset and line");
        Check(PlayerPrefs.GetString(key) == initialAuto, "loading an automatic checkpoint overwrote itself");

        // Load a manual slot with automatic writes enabled; arriving must not replace AutoSave.
        GameAutoSaveSettings.Enabled = true;
        Check(GameSaveService.Load(0, out message), "manual load with autosave enabled failed: " + message);
        yield return null;
        a = Create(main); yield return null; yield return null;
        Check(Id(a) == "a1" && PlayerPrefs.GetString(key) == initialAuto, "manual load overwrote automatic data or failed to restore");
        // Subsequent gameplay boundaries can save again.
        Complete(a); a.Advance(); yield return null;
        Complete(a); a.Advance(); yield return null;
        Complete(a); a.Advance();
        for (int frame = 0; frame < 30 && SceneManager.GetActiveScene().name != B; frame++) yield return null;
        automatic = GameSaveService.ReadAuto(out _);
        Check(SceneManager.GetActiveScene().name == B && automatic != null && automatic.scene == A && automatic.lineId == "a3", "minigame/unsupported destination lost the departing dialogue boundary");
        SceneManager.LoadScene("NovelScene"); yield return null;
        var destination = Create(other); yield return null; yield return null;
        automatic = GameSaveService.ReadAuto(out _);
        Check(automatic != null && automatic.scene == "NovelScene" && automatic.lineId == "b1", "scene arrival failed to replace the previous automatic checkpoint");
        initialAuto = PlayerPrefs.GetString(key);
        GameProgress.ResetAll();
        Check(PlayerPrefs.GetString(key) == initialAuto, "new game deleted automatic data before reaching a new checkpoint");
        Debug.Log("PASS: AutoSave loads while OFF; loading either source never overwrites AutoSave; later transitions resume saving, departures retain a replayable boundary, arrivals replace it, and new-game reset preserves saved slots.");

        // A completed search must replay its completion/return-context flow, rather than get stuck in an empty room.
        SceneManager.LoadScene("Chapter_Moteru_SearchScene"); yield return null;
        var profile = ScriptableObject.CreateInstance<SearchSceneProfileSO>();
        profile.hideFlags = HideFlags.DontUnloadUnusedAsset;
        profile.items.Add(new SearchItemSettings { id = "auto_item", title = "Auto item", sprite = sprite });
        PlayerPrefs.SetString(profile.saveKey, "{\"acquiredItemIds\":[\"auto_item\"]}");
        CreateCompletedSearch(profile);
        for (int frame = 0; frame < 90 && SceneManager.GetActiveScene().name != B; frame++)
            yield return new WaitForSecondsRealtime(0.01f);
        automatic = GameSaveService.ReadAuto(out _);
        Check(SceneManager.GetActiveScene().name == B && automatic != null && automatic.scene == "Chapter_Moteru_SearchScene" && automatic.restartScene,
            "completed search did not save a replayable checkpoint before the puzzle");
        PuzzleGameFlowController.ResetPendingRequest();
        Check(GameSaveService.LoadAuto(out message), "completed search automatic load failed: " + message);
        yield return null;
        Check(GameSaveService.Consume("Chapter_Moteru_SearchScene", "search") == null, "completed search load would bypass its normal completion flow");
        CreateCompletedSearch(profile);
        for (int frame = 0; frame < 90 && SceneManager.GetActiveScene().name != B; frame++)
            yield return new WaitForSecondsRealtime(0.01f);
        Check(SceneManager.GetActiveScene().name == B && PlayerPrefs.GetString("Puzzle.PendingReturn.v1").Contains("a4"),
            "completed search automatic load failed to recreate the puzzle return context and advance");
        Debug.Log("PASS: completed-search AutoSave reload replays the production completion flow and rebuilds the puzzle return context without trapping the player in a completed search room.");

        // No save and corrupt save states must be clear and noninteractive in the chooser.
        GameAutoSaveSettings.Enabled = false;
        PlayerPrefs.DeleteKey(key);
        menu.Open(); menu.ShowLoadFromTitle();
        Check(!FindMenuButton(menu, "AutoLoad").interactable, "empty automatic slot can be loaded");
        PlayerPrefs.SetString(key, "broken");
        menu.ShowLoadFromTitle();
        Check(!FindMenuButton(menu, "AutoLoad").interactable && GameSaveService.ReadAuto(out _) == null, "corrupt automatic slot can be loaded");
        menu.Close();
        PlayerPrefs.DeleteKey(key);
        UseConfiguration(null);
        Debug.Log("PASS: missing/corrupt automatic data is rejected and disabled in the source chooser.");
    }

    private static Button FindMenuButton(ArchiveMenuUI menu, string name)
    {
        foreach (var button in menu.GetComponentsInChildren<Button>(true))
            if (button.name == name && button.gameObject.activeInHierarchy) return button;
        throw new System.Exception("Missing production menu button: " + name);
    }

    private IEnumerator CaptureAutoSaveUI(ArchiveMenuUI menu)
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoSaveScreenshots") < 0) yield break;
        Canvas.ForceUpdateCanvases();
        RenderAutoSaveUI(menu, "autosave-load-sources.png");
        yield return null; yield return null;
        FindMenuButton(menu, "ManualLoad").onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        RenderAutoSaveUI(menu, "autosave-manual-slots.png");
        yield return null; yield return null;
        FindMenuButton(menu, "BackToMenu").onClick.Invoke();
        FindMenuButton(menu, "BackToMenu").onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        RenderAutoSaveUI(menu, "autosave-esc-menu.png");
        yield return null; yield return null;
    }

    private SearchSceneController CreateCompletedSearch(SearchSceneProfileSO profile)
    {
        var go = new GameObject("AutoSave Search"); go.SetActive(false);
        var search = go.AddComponent<SearchSceneController>();
        Set(search, "room1Background", sprite); Set(search, "room2Background", sprite);
        Set(search, "searchProfile", profile);
        Set(search, "uiFont", ArchiveManager.Instance.Database.UiFont);
        Set(search, "itemDialogueScenario", other); Set(search, "dialogueCharacterDatabase", characters);
        Set(search, "sceneFadeInDuration", 0f); Set(search, "fadeOutDuration", 0f); Set(search, "postFadeDelay", 0.02f);
        Set(search, "puzzleSceneName", B); Set(search, "completionSceneName", A); Set(search, "completionResumeLineId", "a4");
        go.SetActive(true);
        return search;
    }

    private static void RenderAutoSaveUI(ArchiveMenuUI menu, string filename)
    {
        Canvas canvas = (Canvas)Get(menu, "canvas");
        var originalMode = canvas.renderMode;
        var originalCamera = canvas.worldCamera;
        var cameraObject = new GameObject("UI preview camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
        var target = new RenderTexture(1672, 941, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../" + filename), image.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = originalMode; canvas.worldCamera = originalCamera;
        camera.targetTexture = null;
        target.Release(); Destroy(target); Destroy(image); Destroy(cameraObject);
    }
}
