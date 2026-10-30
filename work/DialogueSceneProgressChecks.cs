// Runs only inside the isolated project created by verify-dialogue-scene-progress.ps1.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class DialogueSceneProgressChecks : MonoBehaviour
{
    private const string A = "NovelScene_Moteru";
    private const string B = "ProgressOther";
    private DialogueScenarioSO main, other;
    private CharacterDatabaseSO characters;
    private Sprite sprite;
    private AffectionManager affection;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if (SessionState.GetBool("DialogueSceneProgressChecks", false))
        {
            SessionState.SetBool("DialogueSceneProgressChecks", false);
            new GameObject("Progress checks").AddComponent<DialogueSceneProgressChecks>();
        }
    }

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Check(Resources.Load<TextAsset>("DialogueSceneProgressSettings") != null, "production progress settings JSON was not bundled");
        PlayerPrefs.SetInt("Dialogue.SceneProgress.Enabled.v1", 0);
        Check(DialogueProgressSettings.IsEnabled(A), "obsolete player preference overrode the JSON setting");
        UseConfiguration("{\"enabled\":false,\"scenes\":[{\"scene\":\"NovelScene_Moteru\",\"enabled\":true}]}");
        Check(!DialogueProgressSettings.IsEnabled(A) && !DialogueProgressSettings.IsEnabled(B), "global OFF did not override scene settings");
        UseConfiguration("{\"defaultSceneEnabled\":true,\"scenes\":[{\"scene\":\"NovelScene_Moteru\",\"enabled\":false}]}");
        Check(!DialogueProgressSettings.IsEnabled(A) && DialogueProgressSettings.IsEnabled(B), "explicit scene OFF did not override default ON");
        UseConfiguration("{broken");
        Check(DialogueProgressSettings.IsEnabled(A), "invalid settings did not use the documented ON fallback");
        UseConfiguration("{\"scenes\":[{\"scene\":\"NovelScene_Moteru\"},{\"scene\":\"Assets/Scenes/NovelScene_Moteru.unity\"}]}");
        Check(DialogueProgressSettings.IsEnabled(A), "duplicate normalized scene names did not use the fallback");
        UseConfiguration(null);
        var menu = ArchiveManager.Instance.GetComponent<ArchiveMenuUI>();
        foreach (var button in menu.GetComponentsInChildren<Button>(true))
        {
            Check(button.name != "ProgressSave", "Esc menu still exposes the progress toggle");
            Check(button.name != "AutoSaveSetting" && button.name != "AutoLoad" && button.name != "ManualLoad",
                "Removed automatic save UI is still present");
        }
        Call(menu, "ShowSaveLoad", true);
        Check(((GameObject)Get(menu, "saveLoadRoot")).activeSelf, "Manual load screen did not open directly");
        Check(((Button[])Get(menu, "saveSlotButtons")).Length == 6, "Manual load slots changed");
        Call(menu, "HideSaveLoad");
        Debug.Log("PASS: bundled JSON is authoritative, global OFF overrides scene settings, legacy preferences are ignored, and the Esc toggle is absent.");
        GameProgress.ResetAll();
        affection = new GameObject("Affection").AddComponent<AffectionManager>();
        affection.SetAffection("kayo", 0);
        var texture = new Texture2D(8, 8) { name = "ProgressTexture", hideFlags = HideFlags.DontUnloadUnusedAsset };
        sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one / 2);
        sprite.name = "ProgressSprite";
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        characters = ScriptableObject.CreateInstance<CharacterDatabaseSO>();
        characters.hideFlags = HideFlags.DontUnloadUnusedAsset;
        characters.ReplaceAll(new List<CharacterData> { new CharacterData { characterId = "doute", displayName = "Doute",
            expressions = new List<CharacterExpression> { new CharacterExpression { expressionId = "normal", portrait = sprite } } } });
        main = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        main.name = "ProgressMain"; main.hideFlags = HideFlags.DontUnloadUnusedAsset;
        main.SetDefaultBackground(sprite);
        main.ReplaceAll(new List<DialogueLine> {
            Line("a1"), Line("a2", 5), new DialogueLine { lineId="a3", text="Leave for the minigame.", nextScenePath="Assets/Scenes/ProgressOther.unity" },
            Line("a4", 2), new DialogueLine { lineId="a5", text="Choose", choices=new List<DialogueChoice> { new DialogueChoice { text="Continue", nextLineId="a6" } } },
            Line("a6"), Line("a7") });
        other = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        other.name = "ProgressOtherScenario"; other.hideFlags = HideFlags.DontUnloadUnusedAsset;
        other.SetDefaultBackground(sprite);
        other.ReplaceAll(new List<DialogueLine> { Line("b1"), Line("b2") });

        SceneManager.LoadScene(A); yield return null;
        var a = Create(main); yield return null;
        Check(Id(a) == "a1", "first visit did not start at the first line");
        Complete(a); a.Advance(); yield return null;
        Check(Id(a) == "a2" && affection.GetAffection("kayo") == 5, "line effects did not apply");
        Body(a).maxVisibleCharacters = 7;
        Image background = (Image)Get(a, "backgroundImage");
        background.rectTransform.anchoredPosition = new Vector2(21, 34);
        SceneManager.LoadScene(B); yield return null;
        var b = Create(other); yield return null;
        Complete(b); b.Advance(); yield return null;
        Check(Id(b) == "b2", "B did not advance before leaving");
        affection.AddAffection("kayo", 10);
        // Force a disk/JSON read, instead of accidentally testing only static references.
        typeof(DialogueProgressStore).GetField("entries", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a2" && Body(a).maxVisibleCharacters >= 7 && !(bool)Get(a, "progressCompleted"), "mid-line cursor was not restored");
        Check(((Image)Get(a, "backgroundImage")).rectTransform.anchoredPosition == new Vector2(21, 34), "presentation not restored");
        Check(affection.GetAffection("kayo") == 15, "return duplicated effects or rewound minigame progress");
        Complete(a);
        SceneManager.LoadScene(B); yield return null;
        b = Create(other); yield return null;
        Check(Id(b) == "b2", "scene B history was overwritten by A: " + Id(b) + " / " + PlayerPrefs.GetString("Dialogue.SceneProgress.v1"));
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a2" && !(bool)Get(a, "isTyping"), "fully displayed text restarted typing");
        Debug.Log("PASS: arbitrary A/B/A scene loads preserve independent cursors, partial/full text, presentation and current global affection; JSON reload works.");

        a.Advance(); yield return null; Complete(a); a.Advance();
        for (int i=0; i<10 && SceneManager.GetActiveScene().name != B; i++) yield return null;
        Check(SceneManager.GetActiveScene().name == B, "production dialogue scene transition failed");
        b = Create(other); yield return null;
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a4" && affection.GetAffection("kayo") == 17, "departed row replayed or successor effects missing");
        SceneManager.LoadScene(B); yield return null;
        DialogueProgressStore.SetSceneEntry(new DialogueProgressSceneEntry { scene=A, lineId="a6", fadeIn=false });
        // An unrelated dialogue scene must not consume A's result request.
        b = Create(other); yield return null;
        Check(Id(b) == "b2", "targeted request affected the wrong scene");
        SceneManager.LoadScene(A); yield return null;
        a = Create(main);
        Check(a.TryGetSceneEntryMusic(out _, out var preview) && preview.lineId == "a6" &&
            DialogueProgressStore.TryGetSceneEntry(A, out _), "music preview consumed or ignored the progress entry");
        yield return null;
        Check(Id(a) == "a6", "explicit minigame outcome did not override saved scene cursor");
        Check(!DialogueProgressStore.TryGetSceneEntry(A, out _), "entry point was not consumed after restoration");
        Debug.Log("PASS: passed transition resumes at successor without replaying game; explicit result requests are scoped to destination and override scene history.");

        SceneManager.LoadScene(B); yield return null;
        Check(DialogueProgressTransitions.ReturnToScene(A, "a5", false), "new transition API did not load the destination");
        yield return null;
        a = Create(main); yield return null; Complete(a); yield return null;
        Check((bool)Get(a, "isChoiceSelectionOpen"), "choice not opened");
        SceneManager.LoadScene(B); yield return null;
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a5" && (bool)Get(a, "isChoiceSelectionOpen"), "unselected choice lost on reentry");
        Call(a, "SelectChoice", main.Lines[4].choices[0]); yield return null;
        SceneManager.LoadScene(B); yield return null;
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a6", "selected choice was requested again");
        Complete(a); a.Advance(); yield return null; Complete(a); a.Advance();
        Check(!(bool)Get(a, "isPlaying"), "conversation not completed");
        SceneManager.LoadScene(B); yield return null;
        SceneManager.LoadScene(A); yield return null;
        int completions = 0;
        a = Create(main); a.DialogueCompleted += () => completions++; yield return null;
        Check(!(bool)Get(a, "isPlaying") && completions == 1, "completed conversation replayed or completion event missing");
        Debug.Log("PASS: unselected choices reopen, chosen branches stay advanced, and completed conversations remain completed with owner notification.");

        SceneManager.LoadScene(B); yield return null;
        UseConfiguration("{\"enabled\":true,\"defaultSceneEnabled\":false,\"scenes\":[{\"scene\":\"ProgressOther\",\"enabled\":true}]}");
        Check(!DialogueProgressSettings.IsEnabled("Assets/Scenes/NovelScene_Moteru.unity") &&
            DialogueProgressSettings.IsEnabled("Assets/Scenes/ProgressOther.unity"), "scene path and default/OFF resolution failed");
        b = Create(other); yield return null;
        Check(Id(b) == "b2", "disabling A discarded enabled B's saved progress");
        Check(DialogueProgressTransitions.ReturnToScene(A, "a6", false), "OFF blocked scene navigation");
        yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a1", "OFF did not ignore saved cursor and explicit request");
        Complete(a); a.Advance(); yield return null;
        SceneManager.LoadScene(B); yield return null;
        Check(!PlayerPrefs.GetString("Dialogue.SceneProgress.v1").Contains("ProgressMain"), "disabled scene retained or wrote playback history");
        Check(!DialogueProgressStore.TryGetSceneEntry(A, out _), "OFF retained a result entry point");
        UseConfiguration(null);
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a1", "re-enabling A resurrected history from before OFF");
        Complete(a); a.Advance(); yield return null;
        SceneManager.LoadScene(B); yield return null;
        Check(DialogueProgressTransitions.ReturnToScene(A, "a6", false, DialogueProgressEntryMode.PreferSavedProgress), "history-first return failed");
        yield return null;
        a=Create(main); yield return null;
        Check(Id(a)=="a2", "history-first return used its fallback despite saved progress");
        Debug.Log("PASS: mixed scene ON/OFF preserves enabled scenes, clears disabled history, suppresses OFF playback/result restoration and recording, navigates normally, and resumes fresh progress after re-enabling; history-first entries and music preview work.");
        string savedKey=(string)Get(a,"progressKey");
        Check(DialogueProgressStore.TryGet(savedKey,out var oldState), "missing history for compatibility fixture");
        PlayerPrefs.SetString("Dialogue.SceneProgress.v1", JsonUtility.ToJson(new LegacyArchive { entries = new List<DialogueProgressState>{oldState} }));
        typeof(DialogueProgressStore).GetField("entries", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null,null);
        Check(DialogueProgressStore.TryGet(savedKey,out _) && !DialogueProgressStore.TryGetSceneEntry(A,out _), "v1 history without scene entry data failed to load");
        DialogueProgressStore.SetSceneEntry(new DialogueProgressSceneEntry { scene=A, lineId="a6" });
        DialogueProgressStore.SetSceneEntry(new DialogueProgressSceneEntry { scene=B, lineId="b2" });
        typeof(DialogueProgressStore).GetField("entries", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null,null);
        Check(DialogueProgressStore.TryGetSceneEntry(A,out var pointA) && pointA.lineId=="a6" &&
            DialogueProgressStore.TryGetSceneEntry(B,out var pointB) && pointB.lineId=="b2", "scene entry points overwrite each other or fail JSON reload");
        Debug.Log("PASS: existing v1 playback history loads without scene entries; multiple scene destinations persist independently in the shared archive.");
        GameProgress.ResetAll();
        SceneManager.LoadScene(B); yield return null;
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a1", "old unload callback resurrected progress after new game");
        Debug.Log("PASS: OFF starts at first line, ON resumes newly recorded progress, and new-game generation prevents outgoing callbacks resurrecting history.");

        // Two controllers and embedded ranges in the same scene cannot steal each other's history.
        var side = Create(other, "side"); yield return null;
        Complete(side); side.Advance(); yield return null;
        var embedded = Create(other, "embedded", false);
        embedded.PlayDialogueRange(other, "b1", "b1"); yield return null;
        Complete(embedded); embedded.Advance();
        SceneManager.LoadScene(B); yield return null;
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); side = Create(other, "side"); yield return null;
        Check(Id(a) == "a1" && Id(side) == "b2", "multiple controllers shared a progress slot");
        embedded = Create(other, "embedded", false); int embeddedCompletions=0;
        embedded.DialogueCompleted += () => embeddedCompletions++;
        embedded.PlayDialogueRange(other, "b1", "b1"); yield return null;
        Check(!(bool)Get(embedded, "isPlaying") && embeddedCompletions == 1, "embedded range completion not restored");
        Debug.Log("PASS: same-scene controllers and embedded dialogue ranges retain separate progress and completion states.");

        // Manual slots remain authoritative even when scene progress is OFF.
        SceneManager.LoadScene("NovelScene"); yield return null;
        var manual = Create(main); yield return null;
        Complete(manual); manual.Advance(); yield return null; Complete(manual);
        Check(GameSaveService.Save(0, out string message), "manual save failed: " + message);
        manual.Advance(); yield return null;
        SceneManager.LoadScene(B); yield return null;
        UseConfiguration("{\"enabled\":false}");
        Check(GameSaveService.Load(0, out message), "manual load failed: " + message);
        yield return null;
        manual = Create(main); yield return null;
        Check(Id(manual) == "a2" && !(bool)Get(manual, "isTyping"), "manual slot did not override OFF/scene history");
        UseConfiguration(null);
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a1", "manual load retained future history in another scene");
        Debug.Log("PASS: manual slot loading works with progress OFF and clears future scene history.");

        var boundary = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        boundary.name="Boundary"; boundary.hideFlags=HideFlags.DontUnloadUnusedAsset;
        boundary.ReplaceAll(new List<DialogueLine> { new DialogueLine { lineId="boundary", text="Leave at chapter end.", nextScenePath="Assets/Scenes/ProgressOther.unity" } });
        var following = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        following.name="Following"; following.hideFlags=HideFlags.DontUnloadUnusedAsset;
        following.ReplaceAll(new List<DialogueLine> { Line("following1") });
        var chapter = Create(boundary,"chapter"); Set(chapter,"followingScenarios",new List<DialogueScenarioSO>{following}); yield return null;
        Complete(chapter); chapter.Advance();
        while (SceneManager.GetActiveScene().name != B) yield return null;
        SceneManager.LoadScene(A); yield return null;
        chapter = Create(boundary,"chapter"); Set(chapter,"followingScenarios",new List<DialogueScenarioSO>{following}); yield return null;
        Check(Id(chapter)=="following1", "chapter-end departure did not resume the following scenario");
        var final = Create(boundary,"final"); yield return null;
        Complete(final); final.Advance();
        while (SceneManager.GetActiveScene().name != B) yield return null;
        SceneManager.LoadScene(A); yield return null;
        final=Create(boundary,"final"); yield return null;
        Check(!(bool)Get(final,"isPlaying"), "final departure without successor replayed the transition");
        Debug.Log("PASS: chapter-end departures resume the following scenario; final departures without successors remain completed.");

        var automatic = Create(other,"automatic"); Set(automatic,"progressId",""); yield return null;
        Complete(automatic); automatic.Advance(); yield return null;
        SceneManager.LoadScene(B); yield return null;
        SceneManager.LoadScene(A); yield return null;
        new GameObject("Unrelated new root");
        automatic=Create(other,"automatic"); Set(automatic,"progressId",""); yield return null;
        Check(Id(automatic)=="b2", "automatic controller identity changed with unrelated root ordering");
        Debug.Log("PASS: automatic Hierarchy identity remains stable when unrelated scene roots are added.");

        SceneManager.LoadScene(B); yield return null;
        PlayerPrefs.SetString("Dialogue.SceneProgress.v1", "{broken");
        typeof(DialogueProgressStore).GetField("entries", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a1", "corrupt history prevented normal start");
        Debug.Log("PASS: corrupt progress falls back to normal start.");
        yield return CheckSkipBoundaries();
        yield return CheckSkipFade();
        yield return TimingStartGateChecks.Run();
        yield return CheckTitleContinuation();
        yield return CheckPrologueProfile();
        yield return CheckGatheringPresentation();
        yield return CheckEggPortraitVisibility();
        yield return CheckWallInspectionPresentation();
        yield return CheckYowashiMiniGameSkip();
        yield return CheckMoteruEnding();
        yield return CheckEndingStills();
        yield return CheckRemainingPresentationRisks();
        yield return CheckKayoGaugeOutcomeContract();
        yield return CheckStoryStills();
        yield return CheckCatchObjectSizing();
        Debug.Log("PASS: dialogue scene progress integration completed.");
        EditorApplication.Exit(0);
    }

    private IEnumerator CheckCatchObjectSizing()
    {
        SceneManager.LoadScene(B); yield return null;
        var camera=new GameObject("SizingCamera",typeof(Camera)).GetComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=5f; camera.aspect=16f/9f;
        camera.transform.position=new Vector3(0,0,-10);
        var owner=new GameObject("CatchSizing"); owner.SetActive(false);
        var game=owner.AddComponent<Sousakusai8.MiniGame.CatchMiniGameController>();
        var player=new GameObject("Catcher",typeof(SpriteRenderer),typeof(Sousakusai8.MiniGame.PlayerCatcherController));
        var dropper=new GameObject("Dropper",typeof(SpriteRenderer),typeof(Sousakusai8.MiniGame.DropperController));
        player.transform.SetParent(owner.transform); dropper.transform.SetParent(owner.transform);
        player.transform.position=new Vector3(0,-4,0); dropper.transform.position=new Vector3(0,4,0);
        var playerVisual=player.GetComponent<SpriteRenderer>(); var dropperVisual=dropper.GetComponent<SpriteRenderer>();
        playerVisual.sprite=dropperVisual.sprite=sprite;
        Bounds beforePlayer=new Bounds(player.transform.TransformPoint(sprite.bounds.center), Vector3.Scale(sprite.bounds.size,player.transform.lossyScale));
        Bounds beforeDropper=new Bounds(dropper.transform.TransformPoint(sprite.bounds.center), Vector3.Scale(sprite.bounds.size,dropper.transform.lossyScale));
        var pool=new GameObject("Pool"); pool.transform.SetParent(owner.transform);
        var spawned=new GameObject("Spawned"); spawned.transform.SetParent(owner.transform);
        var visual=new GameObject("ItemVisual",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        visual.transform.SetParent(pool.transform); visual.sprite=sprite;
        Set(game,"gameCamera",camera); Set(game,"catcher",player.GetComponent<Sousakusai8.MiniGame.PlayerCatcherController>());
        Set(game,"dropper",dropper.GetComponent<Sousakusai8.MiniGame.DropperController>());
        Set(game,"spawnedItemsRoot",spawned.transform); Set(game,"itemPoolRoot",pool.transform);
        Set(game,"goodItemVisuals",new[]{visual}); Set(game,"badItemVisuals",new[]{visual});
        Set(game,"stackedBadItemSprite",sprite); Set(game,"forceHighestQualityLevel",false); Set(game,"initialPoolSize",1);
        owner.SetActive(true);
        Check((int)Get(game,"goodItemScore")==50 && (int)Get(game,"badItemPenalty")==200 &&
            (int)Get(game,"jumpUnlockScore")==1000 && (int)Get(game,"sweepUnlockScore")==3000,
            "catch balance defaults or ability thresholds differ");
        foreach(string scene in new[]{"SampleScene","falling_verKayo","falling_verMoteru","falling_verYowashi"})
        {
            string path=System.IO.Path.GetFullPath(Application.dataPath+"/../../../Assets/FallingGame/Scenes/"+scene+".unity");
            string yaml=System.IO.File.ReadAllText(path).Replace("\r\n","\n");
            foreach(string setting in new[]{"goodItemScore: 50","badItemPenalty: 200","jumpUnlockScore: 1000","sweepUnlockScore: 3000"})
                Check(yaml.Contains("  "+setting+"\n"),scene+": outdated catch balance "+setting);
        }
        Call(game,"PrepareForStart");
        for(int i=0;i<19;i++) game.RecordCatch(Sousakusai8.MiniGame.FallingItemKind.Good);
        Check(game.CurrentScore==950 && !game.CanPlayerJump,"jump unlocked too early after scoring adjustment");
        game.RecordCatch(Sousakusai8.MiniGame.FallingItemKind.Good);
        Check(game.CurrentScore==1000 && game.CanPlayerJump,"jump no longer unlocks on the twentieth small item");
        for(int i=0;i<40;i++) game.RecordCatch(Sousakusai8.MiniGame.FallingItemKind.Good);
        Check(game.CurrentScore==3000 && (bool)Get(game,"sweepUnlocked"),"sweep threshold drifted");
        Check(!Sousakusai8.MiniGame.YowashiCatchGameFlowController.IsSuccess(game.CurrentScore,3000),"3000 incorrectly clears the strict threshold");
        game.RecordCatch(Sousakusai8.MiniGame.FallingItemKind.Good);
        Check(Sousakusai8.MiniGame.YowashiCatchGameFlowController.IsSuccess(game.CurrentScore,3000),"61 clean catches do not clear");
        game.RecordCatch(Sousakusai8.MiniGame.FallingItemKind.Bad);
        Check(game.CurrentScore==2850,"plate penalty is not 200 points");
        Call(game,"PrepareForStart");
        Check(Vector2.Distance((Vector2)playerVisual.bounds.size,(Vector2)beforePlayer.size*1.4f)<0.0001f &&
            Vector2.Distance((Vector2)dropperVisual.bounds.size,(Vector2)beforeDropper.size*1.4f)<0.0001f,"catch actors are not exactly 1.4x: player="+playerVisual.bounds.size+" before="+beforePlayer.size+" dropper="+dropperVisual.bounds.size+" before="+beforeDropper.size);
        Check(Mathf.Abs(playerVisual.bounds.min.y-beforePlayer.min.y)<0.0001f &&
            Mathf.Abs(dropperVisual.bounds.max.y-beforeDropper.max.y)<0.0001f,"resizing moved the floor or top anchor");
        var catcher=player.GetComponent<Sousakusai8.MiniGame.PlayerCatcherController>();
        Check(catcher.CatchBounds==playerVisual.bounds,"catcher hit bounds do not follow enlarged art");
        foreach(var kind in new[]{Sousakusai8.MiniGame.FallingItemKind.Good,Sousakusai8.MiniGame.FallingItemKind.Bad})
        {
            Call(game,"SpawnItem",new Vector3(100,4,0),kind);
            var item=spawned.GetComponentInChildren<Sousakusai8.MiniGame.FallingItem>();
            var image=item.GetComponent<SpriteRenderer>();
            float expected=(kind==Sousakusai8.MiniGame.FallingItemKind.Good?0.62f:0.84f)*1.4f;
            Check(Mathf.Abs(image.bounds.size.x-expected)<0.0001f,"falling object size multiplier is incorrect");
            Check(image.bounds.max.x<=game.GetRightEdge(0)+0.0001f && image.bounds.max.y<=dropperVisual.bounds.min.y+0.0001f,
                "enlarged spawn clips the screen edge or overlaps dropper");
            if(kind==Sousakusai8.MiniGame.FallingItemKind.Bad)
            {
                Call(item,"StackAtBottom");
                Check(Mathf.Abs(image.bounds.size.x-0.784f*1.4f)<0.0001f &&
                    Mathf.Abs(image.bounds.min.y-catcher.GroundSurfaceY)<0.0001f,"broken plate scale or grounding is incorrect");
            }
            int score=game.CurrentScore;
            float contactX=catcher.CatchBounds.max.x+image.bounds.extents.x;
            item.transform.position=new Vector3(contactX+0.01f,catcher.CatchBounds.center.y,0);
            Call(item,"Update"); Check(game.CurrentScore==score,"item caught outside enlarged bounds");
            item.transform.position=new Vector3(contactX-0.01f,catcher.CatchBounds.center.y,0);
            Call(item,"Update"); Check(game.CurrentScore==score+(kind==Sousakusai8.MiniGame.FallingItemKind.Good?50:-200),
                "item touching enlarged catcher did not score");
        }
        Debug.Log("PASS: catch actors/items are 1.4x; enlarged hit edges score correctly, spawn stays onscreen, pooled/broken plates retain scale and ground alignment.");
        var canvas=new GameObject("CounterCanvas",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var labels=new List<Text>();
        foreach(string name in new[]{"Score","Time","Prompt","Feedback"})
        {
            var text=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(canvas.transform,false); labels.Add(text);
        }
        var hud=Sousakusai8.MiniGame.CatchGameHud.Create(labels[0],labels[1],null,labels[2],labels[3],null,null,null,camera);
        Set(game,"hudPresentation",hud); Set(game,"scoreText",labels[0]); Set(game,"timeText",labels[1]); Set(game,"feedbackText",labels[3]);
        Call(game,"UpdateHudPresentation"); Canvas.ForceUpdateCanvases();
        RectTransform panel=labels[0].transform.parent as RectTransform;
        Vector2 panelSize=panel.sizeDelta, counterPosition=labels[0].rectTransform.anchoredPosition;
        foreach(int value in new[]{950,9950,-100,99950})
        {
            Set(game,"score",value);
            // A catch can run after the controller's Update, immediately before rendering.
            game.RecordCatch(Sousakusai8.MiniGame.FallingItemKind.Good);
            string expected=(value+50).ToString("N0");
            Check(labels[0].text==expected,"catch briefly wrote the legacy score label before the next frame");
            Call(game,"UpdateTimeText");
            Check(labels[1].text.Contains(":"),"timer briefly changed format");
            Canvas.ForceUpdateCanvases();
            Check(panel.sizeDelta==panelSize && labels[0].rectTransform.anchoredPosition==counterPosition,
                "score counter shifted when its digit count changed");
            Vector2 shownFeedback=((RectTransform)labels[3].transform.parent).anchoredPosition;
            yield return null; Canvas.ForceUpdateCanvases();
            Check(labels[0].text==expected && panel.sizeDelta==panelSize &&
                labels[0].rectTransform.anchoredPosition==counterPosition &&
                ((RectTransform)labels[3].transform.parent).anchoredPosition==shownFeedback,
                "counter or feedback shifted on the frame after a catch");
        }
        Debug.Log("PASS: score changes across digit/sign boundaries keep fixed geometry and formatted text even when a catch happens after the HUD update.");
    }

    private IEnumerator CheckStoryStills()
    {
        foreach (var entry in new[] {
            new { Character="yowashi", First=12, Last=14, Art="YowashiPrologue" },
            new { Character="kayo", First=12, Last=21, Art="KayoPrologue" },
            new { Character="moteru", First=13, Last=18, Art="MoteruPrologue" } })
        {
            SceneManager.LoadScene("NovelScene"); yield return null;
            GameProgress.ResetAll();
            var sequence = ScriptableObject.CreateInstance<DialogueScenarioSO>();
            sequence.name="Still_"+entry.Character;
            string prefix="chapter2_"+entry.Character+"_";
            var lines=new List<DialogueLine> { Line("still_setup") };
            for(int n=entry.First-1;n<=entry.Last+1;n++) lines.Add(Line(prefix+n.ToString("000")));
            sequence.ReplaceAll(lines); sequence.SetDefaultBackground(sprite);
            var controller=Create(sequence,"still",false);
            var root=(GameObject)Get(controller,"dialogueRoot");
            var rect=root.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(1920,1080);
            ((Image)Get(controller,"backgroundImage")).transform.SetAsFirstSibling();
            foreach(string field in new[]{"leftPortraitImage","rightPortraitImage"})
            {
                var image=new GameObject(field,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(root.transform,false); image.sprite=sprite;
                Set(controller,field,image);
            }
            Body(controller).transform.SetAsLastSibling();
            var stillCharacters=ScriptableObject.CreateInstance<CharacterDatabaseSO>();
            var cast=new List<CharacterData>();
            foreach(string id in new[]{"doute","kayo","yowashi","moteru"})
                cast.Add(new CharacterData { characterId=id, displayName=id, expressions=new List<CharacterExpression> {
                    new CharacterExpression { expressionId="normal", portrait=sprite } } });
            stillCharacters.ReplaceAll(cast); Set(controller,"characterDatabase",stillCharacters);
            controller.StartDialogue();
            Call(controller,"PrepareLine",lines[1]);
            Check(Get(controller,"storyStillView")==null,"still started before the script marker: "+entry.Character);
            var line=lines[2];
            Call(controller,"PrepareLine",line);
            if(entry.Character=="kayo")
            {
                // Exercise the existing egg motion independently of the advance-mark fixture.
                Call(controller,"StopStoryStill");
                var egg=new GameObject("Egg",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
                egg.transform.SetParent(root.transform,false);
                Set(controller,"kayoEggView",egg); Set(controller,"kayoEggRect",egg.GetComponent<RectTransform>());
                Set(controller,"kayoEggSlideStart",new Vector2(900,-600)); Set(controller,"kayoEggSlideElapsed",0f);
                Set(controller,"isKayoEggViewActive",true);
                Set(controller,"eggLeftOriginalColor",Color.white); Set(controller,"eggRightOriginalColor",Color.white);
                Call(controller,"StartTyping",line.text);
                Check((bool)Get(controller,"isRoomLookPlaying") && Get(controller,"storyStillView")==null && Body(controller).text==string.Empty,
                    "Kayo still/text hid the original egg motion");
                float deadline=Time.realtimeSinceStartup+5f;
                while((bool)Get(controller,"isRoomLookPlaying") && Time.realtimeSinceStartup<deadline) yield return null;
                Check(!(bool)Get(controller,"isKayoEggViewActive"),"egg exit was not completed before the still");
            }
            if(entry.Character=="moteru")
            {
                Check((bool)Get(controller,"isMoteruRevealViewActive"),"original Moteru split view was removed");
                Call(controller,"StartTyping",line.text);
                Check(Get(controller,"storyStillView")==null && (bool)Get(controller,"isRoomLookPlaying"),
                    "fast page advance cut off Moteru's approach");
                float deadline=Time.realtimeSinceStartup+4f;
                while((bool)Get(controller,"isRoomLookPlaying") && Time.realtimeSinceStartup<deadline) yield return null;
                Check((float)Get(controller,"moteruApproachElapsed")>=2.4f,"Moteru approach did not finish");
            }
            if(entry.Character=="yowashi")
                Check((bool)Get(controller,"isYowashiBandageViewActive"),"original Yowashi arm framing was removed");
            var view=(GameObject)Get(controller,"storyStillView");
            Check(view!=null && view.activeSelf,"still missing at marker: "+entry.Character);
            var art=view.transform.Find("Artwork").GetComponent<RawImage>();
            Check(art.texture==Resources.Load<Texture2D>("StoryStills/"+entry.Art),"wrong imported still");
            var leftFill=view.transform.Find("LeftEdgeFill").GetComponent<RawImage>();
            var rightFill=view.transform.Find("RightEdgeFill").GetComponent<RawImage>();
            Check(leftFill.texture==art.texture && rightFill.texture==art.texture &&
                leftFill.uvRect==new Rect(0.5f/art.texture.width,0f,0f,1f) &&
                rightFill.uvRect==new Rect(1f-0.5f/art.texture.width,0f,0f,1f),
                "side fills do not extend the outermost pixel columns");
            Check(leftFill.rectTransform.anchorMin==Vector2.zero &&
                leftFill.rectTransform.anchorMax==new Vector2(0.5f,1f) &&
                rightFill.rectTransform.anchorMin==new Vector2(0.5f,0f) &&
                rightFill.rectTransform.anchorMax==Vector2.one &&
                leftFill.transform.GetSiblingIndex()<art.transform.GetSiblingIndex() &&
                rightFill.transform.GetSiblingIndex()<art.transform.GetSiblingIndex(),
                "side fills leave a gap or cover the original artwork");
            Canvas.ForceUpdateCanvases();
            Vector2 size=art.rectTransform.rect.size;
            Check(Mathf.Abs(size.x/size.y-3508f/2480f)<0.01f,"still was stretched");
            Check(view.transform.GetSiblingIndex()<Body(controller).transform.GetSiblingIndex(),"still covered dialogue");
            for(int i=3;i<lines.Count-1;i++)
            {
                Call(controller,"PrepareLine",lines[i]);
                Check((GameObject)Get(controller,"storyStillView")==view,"still recreated while paging");
            }
            var saved=new GameSaveData();
            Check(controller.CaptureCheckpoint(saved) && controller.RestoreCheckpoint(saved),"still checkpoint failed");
            Check(Get(controller,"storyStillView")!=null,"loading did not restore still");
            Call(controller,"PrepareLine",lines[lines.Count-1]);
            Check(Get(controller,"storyStillView")==null,"still remained beyond script end");
            Call(controller,"PrepareLine",line);
            Call(controller,"CancelChapterTransition",false);
            Check(Get(controller,"storyStillView")==null,"still survived cancellation");
        }
        Debug.Log("PASS: all three script still ranges, original egg/approach motion before text, imported art/aspect/layers, stable paging, checkpoint restore and cleanup.");
    }

    private IEnumerator CheckKayoGaugeOutcomeContract()
    {
        SceneManager.LoadScene(A); yield return null;
        GameProgress.ResetAll();
        var original=AssetDatabase.LoadAssetAtPath<DialogueScenarioSO>("Assets/Dialogue/Data/Kayo/Chapter_Kayo_6.asset");
        Check(original!=null && original.TryGetLine("chapter_kayo_6_057",out _),"production success-only rescue line missing");
        int count=original.Lines.Count;
        var controller=Create(main,"gaugeOutcome",false);
        Set(controller,"followingScenarios",new List<DialogueScenarioSO>{original});
        var go=new GameObject("GaugeOutcome"); go.SetActive(false);
        var bridge=go.AddComponent<KayoGaugeOutcomeController>();
        Set(bridge,"dialogueController",controller); Set(bridge,"rescueAftermath",original);
        go.SetActive(true);
        bridge.SetResult(false);
        var failure=((List<DialogueScenarioSO>)Get(controller,"followingScenarios"))[0];
        Check(failure!=original && !failure.TryGetLine("chapter_kayo_6_057",out _) &&
            failure.Lines[failure.Lines.Count-1].lineId=="chapter_kayo_6_056","failure exposes success-only dialogue");
        Check(original.Lines.Count==count && original.TryGetLine("chapter_kayo_6_057",out _),"gauge mutated the source scenario");
        bridge.SetResult(true);
        Check(((List<DialogueScenarioSO>)Get(controller,"followingScenarios"))[0]==original,"success failed to restore the full aftermath");
        Destroy(go);
        Debug.Log("PASS: production Kayo gauge failure excludes success-only lines, preserves the source asset, and success restores it.");
    }

    private IEnumerator CheckRemainingPresentationRisks()
    {
        SceneManager.LoadScene(A); yield return null;
        GameProgress.ResetAll();
        var scenario=ScriptableObject.CreateInstance<DialogueScenarioSO>(); scenario.name="LegacyBackground";
        scenario.SetDefaultBackground(sprite); scenario.ReplaceAll(new List<DialogueLine>{Line("restoredRow")});
        var dialogue=Create(scenario,"legacyBackground"); yield return null;
        var data=new GameSaveData(); Check(dialogue.CaptureCheckpoint(data),"legacy background checkpoint creation failed");
        data.images[0].sprite=null; data.images[0].enabled=false;
        data.presentation[0].sprite=null; data.presentation[0].enabled=false;
        Check(dialogue.RestoreCheckpoint(data),"legacy checkpoint restore failed");
        var background=(Image)Get(dialogue,"backgroundImage");
        bool manualRestored=background.enabled && background.sprite==sprite;
        Debug.Log("AUDIT: legacy blank manual background recovered="+manualRestored);
        Call(dialogue,"RefreshProgressState");
        var state=(DialogueProgressState)Get(dialogue,"lastProgressState");
        state.images[0].sprite=null; state.images[0].enabled=false;
        DialogueProgressStore.Save(state,DialogueProgressStore.Generation);
        Call(dialogue,"TryRestoreSceneProgress");
        bool progressRestored=background.enabled && background.sprite==sprite;
        Debug.Log("AUDIT: legacy blank scene-progress background recovered="+progressRestored);

        var canvas=new GameObject("GenericInspectionCanvas",typeof(RectTransform),typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var go=new GameObject("GenericInspectionOwner"); go.SetActive(false);
        var search=go.AddComponent<SearchSceneController>();
        var profile=ScriptableObject.CreateInstance<SearchSceneProfileSO>();
        profile.items.Add(new SearchItemSettings {id="audit",title="Audit image",sprite=sprite});
        Set(search,"searchProfile",profile);
        Call(search,"BuildModal",canvas.transform);
        var root=(GameObject)Get(search,"modalRoot"); var still=(RawImage)Get(search,"modalStill");
        bool guarded=!root.activeSelf && !still.enabled;
        Debug.Log("AUDIT: untextured generic inspection guarded="+guarded);
        Set(search,"leftArrow",new GameObject("LeftArrow",typeof(RectTransform),typeof(Button)).GetComponent<Button>());
        Set(search,"rightArrow",new GameObject("RightArrow",typeof(RectTransform),typeof(Button)).GetComponent<Button>());
        Call(search,"BuildItemDefinitions");
        object item=((System.Collections.IList)Get(search,"items"))[0];
        ((System.Collections.IDictionary)Get(search,"placedImages")).Add("audit",new GameObject("PlacedAudit"));
        Call(search,"ShowInspection",item);
        Rect first=still.rectTransform.rect;
        yield return null; Canvas.ForceUpdateCanvases();
        bool stable=Vector2.Distance(first.size,still.rectTransform.rect.size)<0.1f;
        Debug.Log("AUDIT: generic inspection first-frame layout stable="+stable);
        profile.items[0].secondarySprite=sprite;
        root.SetActive(false); Call(search,"ShowInspection",item);
        var secondary=(RawImage)Get(search,"modalSecondaryStill");
        Check(secondary.gameObject.activeSelf && secondary.texture==sprite.texture &&
            Mathf.Approximately(still.rectTransform.anchorMax.x,0.5f),"paired inspection layout lost its second image");
        profile.items[0].secondarySprite=null; profile.items[0].sprite=null;
        profile.items[0].placeholderText="Paper placeholder";
        Call(search,"BuildItemDefinitions"); item=((System.Collections.IList)Get(search,"items"))[0];
        root.SetActive(false); Call(search,"ShowInspection",item);
        Check(still.texture==null && still.color!=Color.white && !secondary.gameObject.activeSelf &&
            ((TMP_Text)Get(search,"modalPlaceholderText")).text=="Paper placeholder","intentional paper placeholder was hidden or retained stale art");
        Destroy(go); Destroy(canvas); Destroy(profile);
        Check(manualRestored && progressRestored,"old blank saves override the repaired background");
        Check(guarded && stable,"generic inspection exposes an unprepared image/layout");
        Debug.Log("PASS: old blank manual/progress backgrounds recover; generic inspection is prepared before display.");
    }

    private IEnumerator CheckEndingStills()
    {
        foreach(var entry in new[] {
            new { Id="chapter_kayo_end_004", Art="KayoPrologue" },
            new { Id="chapter_yowashi_rescue_038", Art="YowashiPrologue" },
            new { Id="chapter_moteru_end_002", Art="MoteruPrologue" },
            new { Id="chapter_kayo_3_032", Art="" },
            new { Id="chapter_yowashi_catchfailure_009", Art="" },
            new { Id="chapter_yowashi_rescue_042", Art="" } })
        {
            SceneManager.LoadScene(A); yield return null; GameProgress.ResetAll();
            var scenario=ScriptableObject.CreateInstance<DialogueScenarioSO>(); scenario.name="EndingStill_"+entry.Id;
            var line=Line(entry.Id); line.nextScenePath="Assets/Scenes/Title.unity";
            scenario.ReplaceAll(new List<DialogueLine>{line}); scenario.SetDefaultBackground(sprite);
            var controller=Create(scenario,"endingStill",false);
            Set(controller,"endingFadeOutDuration",0f); Set(controller,"endingBlackHoldDuration",0f);
            Set(controller,"endingTitleFadeInDuration",0f);
            controller.StartDialogue(); Complete(controller); controller.Advance();
            var card=(GameObject)Get(controller,"endingCard");
            Check(card!=null,"ending confirmation is missing: "+entry.Id);
            var still=card.transform.Find("EndingStill");
            Check((still!=null)==(entry.Art!=""),"incorrect ending background selection: "+entry.Id);
            var caption=card.transform.Find("EndingTitle").GetComponent<TMP_Text>();
            Check(!caption.text.Contains("仮"),"ending still has a provisional title");
            var button=card.transform.Find("ReturnToTitle").GetComponent<Button>();
            float deadline=Time.realtimeSinceStartup+2f;
            while(!button.interactable && Time.realtimeSinceStartup<deadline) yield return null;
            Check(button.interactable,"ending button did not become usable");
            if(still!=null)
            {
                Check(still.GetComponent<CanvasGroup>().alpha==1f,"ending still never became visible");
                var art=still.Find("Artwork").GetComponent<RawImage>();
                Check(art.texture==Resources.Load<Texture2D>("StoryStills/"+entry.Art),"ending uses the wrong character still");
                Check(still.Find("LeftEdgeFill")!=null && still.Find("RightEdgeFill")!=null,"ending reintroduced black side bars");
                Check(still.GetSiblingIndex()<caption.transform.GetSiblingIndex() && still.GetSiblingIndex()<button.transform.GetSiblingIndex(),
                    "ending artwork covers the caption or button");
                Check(!still.GetComponent<CanvasGroup>().blocksRaycasts,"ending artwork intercepts the title button");
            }
            else Check(card.GetComponent<Image>().color==Color.black,"failure ending no longer has its black background");
            Call(controller,"StopEndingCard"); Check(Get(controller,"endingCard")==null,"ending cleanup failed");
        }
        Debug.Log("PASS: three successful endings fade in their character still beneath the title/button, failures stay black, titles omit provisional labels and cards clean up.");
    }

    private IEnumerator CheckMoteruEnding()
    {
        SceneManager.LoadScene(A); yield return null;
        GameProgress.ResetAll();
        var ending=AssetDatabase.LoadAssetAtPath<DialogueScenarioSO>("Assets/Dialogue/Data/Moteru/Chapter_Moteru_End.asset");
        Check(ending!=null && ending.DefaultBackground!=null,"Moteru ending background is missing");
        var controller=Create(ending,"moteruEnding");
        Set(controller,"endingFadeOutDuration",0f); Set(controller,"endingBlackHoldDuration",0f);
        Set(controller,"endingTitleFadeInDuration",0f);
        yield return null;
        var background=(Image)Get(controller,"backgroundImage");
        Check(background.enabled && background.sprite==ending.DefaultBackground,"Moteru ending starts without its room background");
        Complete(controller); controller.Advance();
        Check(Id(controller)=="chapter_moteru_end_002" && background.enabled,"Moteru final reply lost its background");
        Complete(controller); controller.Advance();
        yield return null;
        var card=(GameObject)Get(controller,"endingCard");
        Check(card!=null && SceneManager.GetActiveScene().name==A,"ending bypassed its clear screen");
        Check(Array.Exists(card.GetComponentsInChildren<TMP_Text>(),text=>text.text.Contains("END No.07") && text.text.Contains("モテル")),
            "Moteru clear screen has the wrong title or number");
        var button=card.transform.Find("ReturnToTitle").GetComponent<Button>();
        float deadline=Time.realtimeSinceStartup+2f;
        while(!button.interactable && Time.realtimeSinceStartup<deadline) yield return null;
        Check(button.interactable,"Moteru title-return button stayed disabled");
        button.onClick.Invoke(); yield return null;
        Check(SceneManager.GetActiveScene().name=="Title","Moteru ending did not return to Title");
        Debug.Log("PASS: production Moteru ending retains its room, displays END No.07 and returns to Title from its button.");
    }

    private IEnumerator CheckYowashiMiniGameSkip()
    {
        foreach(string kind in new[]{"puzzle","catch","timing"})
        {
            SceneManager.LoadScene(B); yield return null;
            GameProgress.ResetAll();
            var go=new GameObject("SkipFlow"); go.SetActive(false);
            MonoBehaviour flow;
            if(kind=="puzzle") flow=go.AddComponent<PuzzleGameFlowController>();
            else if(kind=="timing") flow=go.AddComponent<TimingMeterGameFlowController>();
            else
            {
                var gameObject=new GameObject("InactiveCatchGame"); gameObject.SetActive(false);
                var mini=gameObject.AddComponent<Sousakusai8.MiniGame.CatchMiniGameController>();
                flow=go.AddComponent<Sousakusai8.MiniGame.YowashiCatchGameFlowController>();
                Set(flow,"miniGame",mini);
            }
            string destination=kind=="puzzle"?"chapter_yowashi_diary_001":kind=="catch"?
                "chapter_yowashi_catchsuccess_001":"chapter_yowashi_rescue_015";
            if(kind!="catch")
            {
                Type requestType=flow.GetType().GetNestedType("Request",BindingFlags.NonPublic);
                object request=Activator.CreateInstance(requestType,true);
                requestType.GetField("ReturnSceneName").SetValue(request,"NovelScene_Yowashi");
                requestType.GetField(kind=="puzzle"?"ResumeLineId":"SuccessResumeLineId").SetValue(request,destination);
                if(kind=="timing") requestType.GetField("CharacterId").SetValue(request,"yowashi");
                Set(flow,"activeRequest",request);
            }
            go.SetActive(true);
            Check((bool)flow.GetType().GetProperty("CanSkipFromMenu").GetValue(flow),kind+": Yowashi skip is unavailable");
            var menu=ArchiveManager.Instance.GetComponent<ArchiveMenuUI>();
            ArchiveManager.Instance.OpenArchive();
            Check(Time.timeScale==0f,"menu did not pause the minigame");
            Check((bool)menu.GetType().GetMethod("CanSkipMiniGame",Private).Invoke(menu,null),kind+": menu cannot find the skip flow");
            var confirm=(GameObject)Get(menu,"miniGameSkipConfirmation"); confirm.SetActive(true);
            Call(menu,"ConfirmMiniGameSkip");
            yield return null;
            Check(SceneManager.GetActiveScene().name=="NovelScene_Yowashi" && Time.timeScale==1f,kind+": skip failed to return or left time paused");
            Check(DialogueProgressStore.TryGetSceneEntry("NovelScene_Yowashi",out var entry) && entry.lineId==destination,
                kind+": skip did not select the production success continuation");
            if(kind=="timing") Check(TimingGameResults.Get("yowashi").SuccessCount==1 && TimingGameResults.Get("yowashi").FailureCount==0,
                "timing skip did not record exactly one success");
            Debug.Log("PASS: Yowashi "+kind+" menu skip resumes the success conversation and restores time.");
        }
    }

    private IEnumerator CheckWallInspectionPresentation()
    {
        SceneManager.LoadScene("NovelScene"); yield return null;
        var canvas=new GameObject("InspectionCanvas",typeof(RectTransform),typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var owner=new GameObject("InspectionOwner"); owner.SetActive(false);
        var search=owner.AddComponent<Chapter1SearchController>();
        Set(search,"whiteRoom1",sprite); Set(search,"whiteRoom2",sprite);
        Call(search,"BuildModal",canvas.transform);
        var root=(GameObject)Get(search,"modalRoot");
        var still=(RawImage)Get(search,"modalStill");
        Check(!root.activeSelf && !still.enabled,"unprepared inspection can render a white image");
        Set(search,"leftArrow",new GameObject("LeftArrow",typeof(RectTransform),typeof(Button)).GetComponent<Button>());
        Set(search,"rightArrow",new GameObject("RightArrow",typeof(RectTransform),typeof(Button)).GetComponent<Button>());
        Call(search,"BuildItemDefinitions");
        object wall=null;
        foreach(object item in (System.Collections.IEnumerable)Get(search,"items"))
            if((string)item.GetType().GetProperty("Id").GetValue(item)=="facing_wall") wall=item;
        Check(wall!=null,"production wall inspection definition missing");
        for(int attempt=0;attempt<2;attempt++)
        {
            Call(search,"ShowInspection",wall);
            Check(still.enabled && still.texture==sprite.texture && root.GetComponent<CanvasGroup>().alpha==1f,
                "wall appears before its texture is ready");
            Rect first=still.rectTransform.rect;
            Check(first.width>0 && first.height>0,"inspection layout is empty on activation");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Check(Vector2.Distance(first.size,still.rectTransform.rect.size)<0.1f,"wall inspection changes size after its first frame");
            root.SetActive(false);
        }
        Destroy(owner); Destroy(canvas);
        Debug.Log("PASS: wall inspection is hidden until prepared and retains its first-frame layout when reopened.");
    }

    private IEnumerator CheckEggPortraitVisibility()
    {
        SceneManager.LoadScene("NovelScene"); yield return null;
        GameProgress.ResetAll();
        var controller=Create(main,"eggVisibility",false);
        var root=(GameObject)Get(controller,"dialogueRoot");
        var left=new GameObject("Left",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
        var right=new GameObject("Right",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
        left.transform.SetParent(root.transform,false); right.transform.SetParent(root.transform,false);
        Set(controller,"leftPortraitImage",left); Set(controller,"rightPortraitImage",right);
        controller.StartDialogue();
        right.sprite=sprite; right.enabled=true;
        // Reproduce the egg overlay's state at text start, independent of imported egg artwork.
        Set(controller,"eggLeftOriginalColor",Color.white); Set(controller,"eggRightOriginalColor",Color.white);
        Set(controller,"isKayoEggViewActive",true);
        left.color=right.color=new Color(0.45f,0.45f,0.45f,1f);
        for(int page=0;page<3;page++)
        {
            Call(controller,"StartTyping","Egg dialogue page "+page);
            Check(left.enabled && right.enabled && left.color==new Color(0.45f,0.45f,0.45f,1f) &&
                right.color==left.color,"egg dialogue hides Doute or loses portrait dimming at text start");
            yield return null;
            Check(left.enabled,"Doute disappeared during egg dialogue");
        }
        Call(controller,"StopKayoEggView");
        Check(left.enabled && right.enabled && left.color==Color.white && right.color==Color.white,
            "leaving egg close-up failed to restore both portraits");
        Debug.Log("PASS: egg dialogue retains both dimmed portraits at text start and restores brightness on exit.");
    }

    private IEnumerator CheckGatheringPresentation()
    {
        foreach (string target in new[] { "yowashi", "kayo" })
        {
            SceneManager.LoadScene("NovelScene"); yield return null;
            GameProgress.ResetAll();
            characters.ReplaceAll(new List<CharacterData> {
                new CharacterData { characterId="doute", displayName="Doute", expressions=new List<CharacterExpression> {
                    new CharacterExpression { expressionId="normal", portrait=sprite } } },
                new CharacterData { characterId=target, displayName=target, expressions=new List<CharacterExpression> {
                    new CharacterExpression { expressionId="normal", portrait=sprite } } } });
            var scenario = ScriptableObject.CreateInstance<DialogueScenarioSO>();
            scenario.name = "GatheringCheck_" + target;
            scenario.hideFlags = HideFlags.DontUnloadUnusedAsset;
            scenario.SetDefaultBackground(sprite);
            var reply = Line("chapter1_"+target+"_002"); reply.speakerId=target;
            scenario.ReplaceAll(new List<DialogueLine> {
                Line("chapter1_"+target+"_001"), reply, Line("chapter1_"+target+"_003"), Line("outsideGathering") });
            var controller = Create(scenario, "gathering_"+target, false);
            var root = (GameObject)Get(controller,"dialogueRoot");
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920,1080);
            Image left = new GameObject("Left",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
            Image right = new GameObject("Right",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
            left.transform.SetParent(root.transform,false); right.transform.SetParent(root.transform,false);
            Set(controller,"leftPortraitImage",left); Set(controller,"rightPortraitImage",right);
            Set(controller,"normalizeBustPortraitSizes",true);
            controller.StartDialogue();
            Image closeUp=(Image)Get(controller,"gatheringDoutePortrait");
            RectTransform rect=closeUp.rectTransform;
            float expectedHeight=root.GetComponent<RectTransform>().rect.height*2.7f*0.55f;
            Check(closeUp.enabled && !left.enabled && Mathf.Abs(rect.sizeDelta.y-expectedHeight)<0.1f,
                target+": first greeting renders the small ordinary portrait instead of the close-up");
            Vector2 firstPosition=rect.anchoredPosition;
            yield return null;
            Check(Mathf.Abs(rect.sizeDelta.y-expectedHeight)<0.1f, target+": first rendered frame changed portrait size");
            Set(controller,"gatheringDouteSlideElapsed",2.75f);
            Call(controller,"UpdateGatheringDouteSlide");
            Check(rect.anchoredPosition.y<firstPosition.y, target+": portrait did not slide downward");
            Transform frame=closeUp.transform.parent;
            Transform rightFrame=right.transform.parent;
            for(int page=0;page<2;page++)
            {
                Vector2 position=rect.anchoredPosition;
                float elapsed=(float)Get(controller,"gatheringDouteSlideElapsed");
                Complete(controller); controller.Advance();
                Canvas.ForceUpdateCanvases();
                Check(closeUp.enabled && closeUp.gameObject.activeInHierarchy && !closeUp.canvasRenderer.cull &&
                    !left.enabled && ReferenceEquals(closeUp,Get(controller,"gatheringDoutePortrait")),
                    target+": page advance hid or replaced the visible close-up");
                Check(closeUp.transform.parent==frame && right.transform.parent==rightFrame,
                    target+": advancing rebuilt the split view");
                Check(Vector2.Distance(rect.anchoredPosition,position)<0.1f && Mathf.Abs(rect.sizeDelta.y-expectedHeight)<0.1f &&
                    Mathf.Approximately((float)Get(controller,"gatheringDouteSlideElapsed"),elapsed),
                    target+": advancing flashed the normal portrait or restarted the slide");
                yield return null;
                Canvas.ForceUpdateCanvases();
                Check(closeUp.enabled && !closeUp.canvasRenderer.cull && !left.enabled,
                    target+": close-up disappeared on the rendered page");
                Check(Mathf.Abs(rect.sizeDelta.y-expectedHeight)<0.1f, target+": rendered page flashed the normal size");
            }
            var checkpoint=new GameSaveData();
            Check(controller.CaptureCheckpoint(checkpoint), target+": gathering checkpoint failed");
            // A checkpoint from before the dedicated close-up may include the ordinary portrait.
            checkpoint.images[1].enabled=true;
            if(checkpoint.presentation!=null) checkpoint.presentation[1].enabled=true;
            Check(controller.RestoreCheckpoint(checkpoint), target+": gathering checkpoint restore failed");
            closeUp=(Image)Get(controller,"gatheringDoutePortrait");
            Check(closeUp!=null && closeUp.enabled && !left.enabled, target+": restored ordinary portrait overlaps the close-up");
            // Production Chapter2 begins with Moteru, before Doute speaks again.
            // StartScenario clears portraits before PrepareLine tears down the split.
            var next=ScriptableObject.CreateInstance<DialogueScenarioSO>();
            next.name="AfterGathering_"+target; next.SetDefaultBackground(sprite);
            var opening=Line("chapter2_001"); opening.speakerId=target;
            next.ReplaceAll(new List<DialogueLine> { opening, Line("chapter2_002") });
            Call(controller,"StartScenario",next,opening,1);
            Check(!(bool)Get(controller,"isGatheringCompositionActive") && left.enabled && left.sprite==sprite,
                target+": next chapter restored an empty white Doute rectangle");
            yield return null;
            Check(left.enabled && left.sprite==sprite && left.transform.parent==root.transform,
                target+": Doute did not survive the first non-protagonist page after gathering");
            Complete(controller); controller.Advance(); yield return null;
            Check(left.enabled && left.sprite==sprite,"Doute's next line lost the restored portrait");
            Debug.Log("PASS: "+target+" gathering -> chapter2 retains Doute's textured portrait before he speaks.");
            Complete(controller); controller.Advance();
            Check(!(bool)Get(controller,"isGatheringCompositionActive") && left.transform.parent==root.transform,
                target+": leaving the sequence did not restore the normal hierarchy");
            Check(Mathf.Abs(left.rectTransform.sizeDelta.y-root.GetComponent<RectTransform>().rect.height*0.99f)<0.1f,
                target+": normal dialogue did not regain ordinary portrait sizing");
            yield return null;
            Check(root.transform.Find("GatheringLeftHalf")==null && root.transform.Find("GatheringRightHalf")==null,
                target+": stale split masks remain after leaving");
            Debug.Log("PASS: "+target+" starts with close-up framing, slides down, advances both speakers without pose/frame resets, and restores normal dialogue.");
        }
    }

    private IEnumerator CheckPrologueProfile()
    {
        SceneManager.LoadScene("NovelScene"); yield return null;
        GameProgress.ResetAll();
        var scenario = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        scenario.name = "ProfileCheck";
        scenario.hideFlags = HideFlags.DontUnloadUnusedAsset;
        scenario.ReplaceAll(new List<DialogueLine> {
            Line("beforeProfile"), new DialogueLine { lineId=ProloguePresentation.ProfileLineId, text="Doute profile" }, Line("afterProfile") });
        var controller = Create(scenario, "profile"); yield return null;
        Complete(controller); controller.Advance(); yield return null;
        var root = (GameObject)Get(controller, "dialogueRoot");
        Check(Id(controller)==ProloguePresentation.ProfileLineId && !(bool)Get(controller,"isTyping") &&
            root.transform.Find("Doute Profile").gameObject.activeSelf, "profile did not appear fully displayed");
        Check(Body(controller).GetComponent<CanvasGroup>().alpha==0f, "profile duplicates the dialogue body");
        var saved = new GameSaveData();
        Check(controller.CaptureCheckpoint(saved), "profile checkpoint failed");
        controller.Advance(); yield return null;
        Check(Id(controller)=="afterProfile" && !root.transform.Find("Doute Profile").gameObject.activeSelf &&
            Body(controller).GetComponent<CanvasGroup>().alpha==1f, "profile did not clean up on advance");
        Check(controller.RestoreCheckpoint(saved), "profile checkpoint restore failed");
        Check(root.transform.Find("Doute Profile").gameObject.activeSelf, "checkpoint did not restore profile");
        SceneManager.LoadScene(B); yield return null;
        SceneManager.LoadScene("NovelScene"); yield return null;
        controller = Create(scenario, "profile"); yield return null;
        root = (GameObject)Get(controller,"dialogueRoot");
        Check(Id(controller)==ProloguePresentation.ProfileLineId && root.transform.Find("Doute Profile").gameObject.activeSelf,
            "scene progress did not restore profile");
        controller.Advance(); yield return null;
        Check(Id(controller)=="afterProfile" && Body(controller).GetComponent<CanvasGroup>().alpha==1f,
            "restored profile did not return to normal dialogue");
        Debug.Log("PASS: profile display, cleanup, manual checkpoint and scene-progress restore.");
    }

    private static DialogueLine Line(string id, int delta=0) => new DialogueLine
    {
        lineId=id, speakerId="doute", expressionId="normal",
        text="A long dialogue line for checking the exact character cursor when returning to this scene. " + id,
        affectionChanges = delta == 0 ? new List<AffectionDelta>() : new List<AffectionDelta> { new AffectionDelta { characterId="kayo", value=delta } }
    };

    // Simulate a new launch with another pre-authored JSON, without exposing a runtime setting API.
    private static void UseConfiguration(string json)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var type = typeof(DialogueProgressSettings);
        object configuration = json == null ? null : type.GetMethod("Parse", flags).Invoke(null, new object[] { json });
        type.GetField("settings", flags).SetValue(null, configuration);
        typeof(DialogueProgressStore).GetField("entries", flags).SetValue(null, null);
        DialogueProgressStore.TryGetSceneEntry(A, out _); // Load/filter saved state as startup does.
    }

    [Serializable]
    private sealed class LegacyArchive
    {
        public int version=1;
        public List<DialogueProgressState> entries;
    }

    private NovelDialogueController Create(DialogueScenarioSO scenario, string id="main", bool autoStart=true)
    {
        var canvas = new GameObject("Canvas_"+id, typeof(RectTransform), typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var root = new GameObject("DialogueRoot", typeof(RectTransform)); root.transform.SetParent(canvas.transform, false);
        var body = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); body.transform.SetParent(root.transform, false);
        var text = body.GetComponent<TextMeshProUGUI>(); text.rectTransform.sizeDelta = new Vector2(900,200); text.fontSize=24;
        text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(root.transform, false);
        var choices = new GameObject("Choices", typeof(RectTransform)); choices.transform.SetParent(root.transform, false);
        var template = new GameObject("ChoiceTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); template.transform.SetParent(choices.transform, false); template.SetActive(false);
        var go = new GameObject("Dialogue_"+id); go.SetActive(false);
        var controller = go.AddComponent<NovelDialogueController>();
        Set(controller,"scenario",scenario); Set(controller,"characterDatabase",characters);
        Set(controller,"dialogueRoot",root); Set(controller,"bodyText",text); Set(controller,"backgroundImage",background);
        Set(controller,"progressId",id); Set(controller,"playOnStart",autoStart);
        Set(controller,"useSceneTransitionFade",false); Set(controller,"useChapterTransitionFade",false);
        Set(controller,"charactersPerSecond",10f); Set(controller,"choiceOptionsRoot",choices.GetComponent<RectTransform>()); Set(controller,"choiceButtonTemplate",template.GetComponent<Button>());
        go.SetActive(true);
        return controller;
    }
    private IEnumerator CheckSkipBoundaries()
    {
        SceneManager.LoadScene("NovelScene"); yield return null;
        GameProgress.ResetAll();
        var first = AssetDatabase.LoadAssetAtPath<DialogueScenarioSO>("Assets/Dialogue/Data/Prologue/Prologue1.asset");
        var second = AssetDatabase.LoadAssetAtPath<DialogueScenarioSO>("Assets/Dialogue/Data/Prologue/Prologue2.asset");
        Check(first != null && second != null, "production prologues missing");
        var prologue = Create(first, "skipPrologue");
        Set(prologue, "followingScenarios", new List<DialogueScenarioSO> { second, main });
        yield return null;
        prologue.SkipToNextScenario(); yield return null;
        Check(SceneManager.GetActiveScene().name == "NovelScene" && Get(prologue,"currentScenario") == second &&
            Id(prologue) == second.Lines[0].lineId, "Prologue1 skip jumped past Prologue2 to a later scene");
        Call(prologue,"ShowLine",second.Lines[2]);
        Complete(prologue);
        Check(Id(prologue)=="prologue2_003" && (bool)Get(prologue,"isChoiceSelectionOpen"), "production first choice did not open");
        prologue.SkipToNextScenario(); yield return null;
        Check(Get(prologue,"currentScenario")==main && Id(prologue)=="a1" &&
            !(bool)Get(prologue,"isChoiceSelectionOpen"), "skip at the first prologue choice did not reach the next chapter");
        SceneManager.LoadScene("NovelScene"); yield return null;
        GameProgress.ResetAll();
        prologue=Create(second,"skipPrologueBeforeChoice");
        Set(prologue,"followingScenarios",new List<DialogueScenarioSO>{main}); yield return null;
        prologue.SkipToNextScenario(); yield return null;
        Check(Get(prologue,"currentScenario")==main && Id(prologue)=="a1",
            "skip before the first prologue choice stopped at a choice instead of the next chapter");
        Debug.Log("PASS: production prologue skip bypasses its choices and reaches the next chapter, both before and during the first choice.");

        SceneManager.LoadScene("NovelScene"); yield return null;
        var route = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        route.name = "SkipRoute"; route.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var entry = Line("skipEntry");
        entry.branches.Add(new DialogueBranch { nextLineId="skipChoice", affectionChanges=new List<AffectionDelta> {
            new AffectionDelta { characterId="kayo", value=50 } } });
        var unreachable = Line("unreachable"); unreachable.nextScenePath="Assets/Scenes/ProgressOther.unity";
        var decision = Line("skipChoice"); decision.text="Choose.";
        decision.choices.Add(new DialogueChoice { text="Continue", nextLineId="skipEnd" });
        var end = Line("skipEnd"); end.text="End of route."; end.nextScenePath="Assets/Scenes/ProgressOther.unity";
        route.ReplaceAll(new List<DialogueLine> { entry, unreachable, decision, end });
        var controller = Create(route, "skipRoute"); yield return null;
        int before = affection.GetAffection("kayo");
        controller.SkipToNextScenario(); yield return null;
        Check(Id(controller)=="skipChoice" && (bool)Get(controller,"isChoiceSelectionOpen") &&
            SceneManager.GetActiveScene().name=="NovelScene", "skip inspected an unreachable scene transition instead of the active branch");
        Check(affection.GetAffection("kayo")==before, "skip inspection applied branch effects");
        string transitionId=null; controller.SceneTransitionStarting += line => transitionId=line.lineId;
        Call(controller,"SelectChoice",decision.choices[0]); yield return null;
        controller.SkipToNextScenario(); yield return null;
        Check(SceneManager.GetActiveScene().name=="NovelScene" && Id(controller)=="skipEnd" &&
            !(bool)Get(controller,"isTyping") && transitionId==null, "skip executed rather than displayed the scene boundary");
        controller.Advance(); yield return null;
        Check(SceneManager.GetActiveScene().name==B && transitionId=="skipEnd", "selected route did not preserve scene entry callbacks");

        SceneManager.LoadScene("NovelScene"); yield return null;
        var both = Line("choiceAndScene"); both.text="Choose first.";
        both.nextScenePath="Assets/Scenes/ProgressOther.unity";
        both.choices.Add(new DialogueChoice { text="Continue", nextLineId="choiceAndScene" });
        route = ScriptableObject.CreateInstance<DialogueScenarioSO>(); route.name="ChoicePriority";
        route.ReplaceAll(new List<DialogueLine>{both});
        controller=Create(route,"choicePriority"); yield return null;
        controller.SkipToNextScenario(); yield return null;
        Check(SceneManager.GetActiveScene().name=="NovelScene" && (bool)Get(controller,"isChoiceSelectionOpen"),
            "a choice did not take priority over a scene transition on the same row");

        SceneManager.LoadScene("NovelScene"); yield return null;
        route = ScriptableObject.CreateInstance<DialogueScenarioSO>(); route.name="SceneBeforeChoice";
        entry=Line("intro"); entry.nextLineId="firstScene";
        var firstScene=Line("firstScene"); firstScene.text="Stop here."; firstScene.nextScenePath="Assets/Scenes/ProgressOther.unity";
        var laterChoice=Line("laterChoice"); laterChoice.choices.Add(new DialogueChoice { text="Later", nextLineId="lastScene" });
        var lastScene=Line("lastScene"); lastScene.nextScenePath="Assets/Scenes/ProgressOther.unity";
        route.ReplaceAll(new List<DialogueLine>{entry,firstScene,laterChoice,lastScene});
        controller=Create(route,"sceneBeforeChoice"); yield return null;
        controller.SkipToNextScenario(); yield return null;
        Check(Id(controller)=="firstScene" && !(bool)Get(controller,"isTyping") && SceneManager.GetActiveScene().name=="NovelScene",
            "a later choice or scene took precedence over the first scene boundary");

        SceneManager.LoadScene("NovelScene"); yield return null;
        route=ScriptableObject.CreateInstance<DialogueScenarioSO>(); route.name="MultipleChoices";
        entry=Line("intro"); entry.nextLineId="firstChoice";
        var firstChoice=Line("firstChoice"); firstChoice.choices.Add(new DialogueChoice { text="Next", nextLineId="between" });
        var between=Line("between"); between.nextLineId="secondChoice";
        var secondChoice=Line("secondChoice"); secondChoice.choices.Add(new DialogueChoice { text="End", nextLineId="finalScene" });
        var finalScene=Line("finalScene"); finalScene.nextScenePath="Assets/Scenes/ProgressOther.unity";
        route.ReplaceAll(new List<DialogueLine>{entry,firstChoice,between,secondChoice,finalScene});
        controller=Create(route,"multipleChoices"); yield return null;
        controller.SkipToNextScenario(); yield return null;
        Check(Id(controller)=="firstChoice" && (bool)Get(controller,"isChoiceSelectionOpen"), "first choice bypassed");
        Call(controller,"SelectChoice",firstChoice.choices[0]); yield return null;
        controller.SkipToNextScenario(); yield return null;
        Check(Id(controller)=="secondChoice" && (bool)Get(controller,"isChoiceSelectionOpen"), "next choice bypassed after selection");
        Debug.Log("PASS: skip stops at the earliest choice/scene row within its asset, displays scene-boundary text without loading, advances one asset when no boundary exists, and follows selected routes.");
    }

    private IEnumerator CheckSkipFade()
    {
        SceneManager.LoadScene("NovelScene"); yield return null;
        var scenario = ScriptableObject.CreateInstance<DialogueScenarioSO>(); scenario.name="SkipFade";
        var intro=Line("fadeIntro"); intro.text="Before the fade."; intro.nextLineId="fadeChoice";
        var choice=Line("fadeChoice"); choice.text="Choose after the fade.";
        choice.choices.Add(new DialogueChoice { text="Continue", nextLineId="fadeScene" });
        var scene=Line("fadeScene"); scene.text="Stop before loading."; scene.nextScenePath="Assets/Scenes/ProgressOther.unity";
        scenario.ReplaceAll(new List<DialogueLine>{intro,choice,scene});
        var controller=Create(scenario,"skipFade"); yield return null;
        Set(controller,"useChapterTransitionFade",true);
        Set(controller,"chapterFadeOutDuration",0.12f);
        Set(controller,"chapterFadeHoldDuration",0.12f);
        Set(controller,"chapterFadeInDuration",0.12f);
        controller.SkipToNextScenario();
        Check((bool)Get(controller,"isChapterTransitioning"),"same-asset skip did not start the chapter fade");
        controller.Advance(); controller.SkipToNextScenario();
        Check(Id(controller)=="fadeIntro","input advanced dialogue during fade-out");
        float deadline=Time.realtimeSinceStartup+3f;
        while(Id(controller)!="fadeChoice" && Time.realtimeSinceStartup<deadline) yield return null;
        Check(Id(controller)=="fadeChoice","skip fade never prepared the choice");
        var overlay=(Image)Get(controller,"chapterTransitionOverlay");
        Check(Mathf.Approximately(overlay.color.a,1f) && overlay.gameObject.activeSelf,"destination changed outside the black hold");
        float holdBegan=Time.realtimeSinceStartup;
        Check(!(bool)Get(controller,"isTyping") && (bool)Get(controller,"isChoiceSelectionOpen"),"choice destination was not fully displayed");
        Call(controller,"SelectChoice",choice.choices[0]);
        Check(Id(controller)=="fadeChoice","choice was accepted during the fade");
        while((bool)Get(controller,"isChapterTransitioning") && Time.realtimeSinceStartup<deadline) yield return null;
        Check(!(bool)Get(controller,"isChapterTransitioning") && !overlay.gameObject.activeSelf && overlay.color.a==0f,"skip fade did not finish transparently");
        Check(Time.realtimeSinceStartup-holdBegan>=0.10f,"skip did not preserve the configured black hold interval");
        Call(controller,"SelectChoice",choice.choices[0]); yield return null;
        controller.SkipToNextScenario();
        Check((bool)Get(controller,"isChapterTransitioning"),"scene-boundary skip did not fade");
        deadline=Time.realtimeSinceStartup+3f;
        while((bool)Get(controller,"isChapterTransitioning") && Time.realtimeSinceStartup<deadline) yield return null;
        Check(Id(controller)=="fadeScene" && SceneManager.GetActiveScene().name=="NovelScene", "scene-boundary fade loaded instead of displaying the row");

        SceneManager.LoadScene("NovelScene"); yield return null;
        var next=ScriptableObject.CreateInstance<DialogueScenarioSO>();next.name="NextFadeAsset";
        next.ReplaceAll(new List<DialogueLine>{Line("nextFadeFirst")});
        var plain=ScriptableObject.CreateInstance<DialogueScenarioSO>();plain.name="PlainFadeAsset";
        plain.ReplaceAll(new List<DialogueLine>{Line("plainFirst")});
        controller=Create(plain,"assetFade");Set(controller,"followingScenarios",new List<DialogueScenarioSO>{next});yield return null;
        Set(controller,"useChapterTransitionFade",true);
        Set(controller,"chapterFadeOutDuration",0.12f);Set(controller,"chapterFadeHoldDuration",0.12f);Set(controller,"chapterFadeInDuration",0.12f);
        controller.SkipToNextScenario();
        Check((bool)Get(controller,"isChapterTransitioning"),"next-asset skip lost its existing fade");
        deadline=Time.realtimeSinceStartup+3f;
        while((bool)Get(controller,"isChapterTransitioning") && Time.realtimeSinceStartup<deadline)yield return null;
        Check(Id(controller)=="nextFadeFirst" && !(bool)Get(controller,"isChapterTransitioning"),"next-asset fade did not finish at the first line");
        Debug.Log("PASS: skip shares chapter fade-out/hold/fade-in, swaps under black, locks input, shows choices/scene boundaries and preserves next-asset fades.");
    }

    private IEnumerator CheckTitleContinuation()
    {
        SceneManager.LoadScene("NovelScene"); yield return null;
        GameProgress.ResetAll();
        var source = Create(main); yield return null;
        Body(source).maxVisibleCharacters = 3;
        Set(source, "autoPlayEnabled", true);
        var background = (Image)Get(source, "backgroundImage");
        background.sprite = sprite; background.enabled = true;
        background.color = new Color(0.6f, 0.7f, 0.8f, 0.9f);
        background.rectTransform.anchoredPosition = new Vector2(17f, 23f);
        TimingGameResults.Save("kayo", true); TimingGameResults.Save("moteru", false);
        Check(GameSaveService.Save(0, out string message), "manual baseline failed: " + message);
        string manualSlot = PlayerPrefs.GetString("GameSave.Slot.v1.0");
        var menu = ArchiveManager.Instance.GetComponent<ArchiveMenuUI>();
        ArchiveManager.Instance.OpenArchive();
        var returnButton = Array.Find(menu.GetComponentsInChildren<Button>(true), b => b.name == "ReturnToTitle");
        Check(returnButton != null, "return-to-title menu item missing");
        returnButton.onClick.Invoke();
        var confirmation = (GameObject)Get(menu, "returnToTitleConfirmation");
        Check(confirmation.activeSelf, "return-to-title warning missing");
        Array.Find(confirmation.GetComponentsInChildren<Button>(), b => b.name == "Cancel").onClick.Invoke();
        Check(!confirmation.activeSelf && SceneManager.GetActiveScene().name == "NovelScene", "cancel changed scenes");
        Check(GameSaveService.ReadContinue(out _) == null, "cancel created a continuation");
        Check(GameSaveService.ReturnToTitle(out message), "save/return failed: " + message);
        yield return null;
        Check(SceneManager.GetActiveScene().name == "Title", "did not return to Title");
        Check(PlayerPrefs.GetString("GameSave.Slot.v1.0") == manualSlot, "continue overwrote manual slot");
        var saved = GameSaveService.ReadContinue(out message);
        Check(saved != null && saved.lineId == "a1" && saved.visibleCharacters == 3, "partial text not captured");
        Check(saved.autoPlayEnabled && saved.presentation[0].color == new Color(0.6f, 0.7f, 0.8f, 0.9f), "UI state not captured");
        var canvas = new GameObject("TitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        foreach (string name in new[] { "StartButton", "QuitButton" })
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            button.transform.SetParent(canvas.transform, false);
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.transform.SetParent(button.transform, false);
            label.GetComponent<TextMeshProUGUI>().font = TMP_Settings.defaultFontAsset;
        }
        var titleObject = new GameObject("TitleMenu"); titleObject.SetActive(false);
        var title = titleObject.AddComponent<TitleMenu>();
        Set(title, "fadePanel", new GameObject("FadePanel", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>());
        Set(title, "bgmSource", titleObject.AddComponent<AudioSource>()); Set(title, "fadeDuration", 0f);
        titleObject.SetActive(true); yield return null;
        float[] centers = { 0.34f, 0.26f, 0.18f, 0.10f };
        Vector2[] sizes = { new Vector2(430f,72f), new Vector2(430f,72f), new Vector2(350f,60f), new Vector2(350f,56f) };
        string[] names = { "StartButton", "ContinueButton", "LoadButton", "QuitButton" };
        for (int i = 0; i < names.Length; i++)
        {
            var button = Array.Find(canvas.GetComponentsInChildren<Button>(), b => b.name == names[i]);
            var rect = button.GetComponent<RectTransform>();
            Check(rect.sizeDelta == sizes[i] && Mathf.Approximately(rect.anchorMin.y, centers[i]), "title size/order/spacing incorrect");
        }
        Check(Array.Find(canvas.GetComponentsInChildren<Button>(), b => b.name == "ContinueButton").interactable, "valid continue disabled");
        GameProgress.ResetAll(); TimingGameResults.Save("yowashi", true);
        Check(GameSaveService.Continue(out message), "continue load failed: " + message);
        yield return null;
        var resumed = Create(main); yield return null;
        Check(Id(resumed) == "a1" && Body(resumed).maxVisibleCharacters >= 3, "continue cursor/partial text not restored");
        Check(resumed.IsAutoPlayEnabled, "AUTO not restored");
        background = (Image)Get(resumed, "backgroundImage");
        Check(background.sprite == sprite && background.rectTransform.anchoredPosition == new Vector2(17f,23f), "continue background not restored");
        Check(TimingGameResults.Get("kayo").Latest == TimingGameResults.Outcome.Success &&
            TimingGameResults.Get("moteru").Latest == TimingGameResults.Outcome.Failure &&
            TimingGameResults.Get("yowashi").Latest == TimingGameResults.Outcome.NotPlayed, "character results leaked across continuation");
        SceneManager.LoadScene(B); yield return null;
        Check(!GameSaveService.ReturnToTitle(out _), "unsupported scene returned without saving");
        Check(GameSaveService.ReadContinue(out _) != null, "failed return destroyed previous continuation");
        Debug.Log("PASS: title confirmation/cancel, independent continuation, partial text/art/AUTO/results restore, title layout and unsupported-scene protection.");
    }

    private static string Id(NovelDialogueController c) => ((DialogueLine)Get(c,"currentLine"))?.lineId;
    private static TMP_Text Body(NovelDialogueController c) => (TMP_Text)Get(c,"bodyText");
    private static void Complete(NovelDialogueController c) => Call(c,"CompleteTyping");
    private static object Get(object o,string field) => o.GetType().GetField(field,Private).GetValue(o);
    private static void Set(object o,string field,object value) => o.GetType().GetField(field,Private).SetValue(o,value);
    private static void Call(object o,string method,params object[] args) => o.GetType().GetMethod(method,Private).Invoke(o,args);
    private static void Check(bool value,string message)
    {
        if (value) return;
        Debug.LogError("FAIL: " + message);
        EditorApplication.Exit(1);
        throw new Exception(message);
    }
}
