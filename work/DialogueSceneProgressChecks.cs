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
        yield return CheckTitleContinuation();
        Debug.Log("PASS: dialogue scene progress integration completed.");
        EditorApplication.Exit(0);
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
        prologue.SkipToNextScenario(); yield return null;
        Check((bool)Get(prologue,"isChoiceSelectionOpen") && ((DialogueLine)Get(prologue,"currentLine")).HasChoices,
            "Prologue2 skip bypassed the player decision");
        string choiceId = Id(prologue);
        prologue.SkipToNextScenario(); yield return null;
        Check(Id(prologue) == choiceId && (bool)Get(prologue,"isChoiceSelectionOpen"), "skip chose an unanswered decision");

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
