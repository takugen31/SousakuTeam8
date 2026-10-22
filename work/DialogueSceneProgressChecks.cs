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
        DialogueProgressStore.Enabled = true;
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

        DialogueProgressStore.Enabled = false;
        SceneManager.LoadScene(B); yield return null;
        Check(DialogueProgressTransitions.ReturnToScene(A, "a6", false), "OFF blocked scene navigation");
        yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a1", "OFF did not ignore saved cursor and explicit request");
        Complete(a); a.Advance(); yield return null;
        DialogueProgressStore.Enabled = true;
        SceneManager.LoadScene(B); yield return null;
        SceneManager.LoadScene(A); yield return null;
        a = Create(main); yield return null;
        Check(Id(a) == "a2", "OFF/ON did not start recording the current session");
        SceneManager.LoadScene(B); yield return null;
        Check(DialogueProgressTransitions.ReturnToScene(A, "a6", false, DialogueProgressEntryMode.PreferSavedProgress), "history-first return failed");
        yield return null;
        a=Create(main); yield return null;
        Check(Id(a)=="a2", "history-first return used its fallback despite saved progress");
        Debug.Log("PASS: unified progress transitions load scenes with ON/OFF, prefer saved history when requested, and consume entry points only after successful entry; music preview is read-only.");
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
        DialogueProgressStore.Enabled = false;
        Check(GameSaveService.Load(0, out message), "manual load failed: " + message);
        yield return null;
        manual = Create(main); yield return null;
        Check(Id(manual) == "a2" && !(bool)Get(manual, "isTyping"), "manual slot did not override OFF/scene history");
        DialogueProgressStore.Enabled = true;
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
        Debug.Log("PASS: dialogue scene progress integration completed.");
        EditorApplication.Exit(0);
    }

    private static DialogueLine Line(string id, int delta=0) => new DialogueLine
    {
        lineId=id, speakerId="doute", expressionId="normal",
        text="A long dialogue line for checking the exact character cursor when returning to this scene. " + id,
        affectionChanges = delta == 0 ? new List<AffectionDelta>() : new List<AffectionDelta> { new AffectionDelta { characterId="kayo", value=delta } }
    };

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
