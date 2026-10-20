// Run only in the isolated Unity project used by verify-moteru-search.ps1.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueCanvasGroupChecks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if (SessionState.GetBool("DialogueCanvasGroupChecks", false))
            new GameObject("CanvasGroup regression checks").AddComponent<DialogueCanvasGroupChecks>();
    }

    private IEnumerator Start()
    {
        var canvasObject = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var root = new GameObject("DialogueRoot", typeof(RectTransform));
        root.transform.SetParent(canvasObject.transform, false);
        var panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var body = new GameObject("BodyText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        body.transform.SetParent(panel.transform, false);
        var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(root.transform, false);
        var texture = new Texture2D(32, 32);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f));
        background.sprite = sprite;
        var database = ScriptableObject.CreateInstance<CharacterDatabaseSO>();
        var characters = new List<CharacterData>();
        foreach (string id in new[] { "doute", "yowashi", "kayo" })
            characters.Add(new CharacterData { characterId = id, expressions = new List<CharacterExpression> {
                new CharacterExpression { expressionId = "normal", portrait = sprite } } });
        database.ReplaceAll(characters);
        var controllerObject = new GameObject("Inactive controller fixture");
        controllerObject.SetActive(false);
        var controller = controllerObject.AddComponent<NovelDialogueController>();
        Set(controller, "dialogueRoot", root);
        Set(controller, "bodyText", body.GetComponent<TextMeshProUGUI>());
        Set(controller, "backgroundImage", background);
        Set(controller, "characterDatabase", database);

        // The pre-fix expression skips AddComponent for Unity's fake-null wrapper.
        CanvasGroup legacy = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
        bool reproduced = false;
        try { float alpha = legacy.alpha; }
        catch (MissingComponentException) { reproduced = true; }
        Check(reproduced, "pre-fix missing-component exception was not reproduced");
        Debug.Log("PASS: original ?? expression reproduces MissingComponentException on a panel without CanvasGroup.");

        foreach (string id in new[] { "chapter_yowashi_diary_002", "chapter_yowashi_diary_003",
            "chapter_yowashi_diary_014", "chapter_kayo_diary_002", "chapter3_009" })
        {
            Call(controller, "StartDouteMonologueView", new DialogueLine { lineId = id });
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            Check(group != null && group.alpha == 0f, "missing CanvasGroup was not created: " + id);
            Check(body.transform.parent != panel.transform, "narration text was not moved: " + id);
            Call(controller, "StopDouteMonologueView");
            Check(body.transform.parent == panel.transform && group.alpha == 1f,
                "normal dialogue panel was not restored: " + id);
            Check(panel.GetComponents<CanvasGroup>().Length == 1, "duplicate CanvasGroups accumulated");
            yield return null;
        }
        CanvasGroup existing = panel.GetComponent<CanvasGroup>();
        existing.alpha = .37f;
        Call(controller, "StartDouteMonologueView", new DialogueLine { lineId = "chapter_yowashi_diary_002" });
        Call(controller, "StopDouteMonologueView");
        Check(existing.alpha == .37f && panel.GetComponents<CanvasGroup>().Length == 1,
            "existing CanvasGroup alpha was not preserved");
        DestroyImmediate(existing);
        Call(controller, "ShowChoiceOnlyWindow");
        CanvasGroup choiceGroup = panel.GetComponent<CanvasGroup>();
        Check(choiceGroup != null && choiceGroup.alpha == 0f && !choiceGroup.blocksRaycasts,
            "choice-only window failed without a CanvasGroup");
        Call(controller, "RestoreChoiceOnlyWindow");
        Check(choiceGroup.alpha == 1f && choiceGroup.blocksRaycasts, "choice-only window was not restored");
        Debug.Log("PASS: Yowashi/Kayo diary and Doute narration create/reuse CanvasGroup, restore text and alpha, and avoid duplicate components; choice-only window also recovers from a missing group.");
        SessionState.SetBool("DialogueCanvasGroupChecks", false);
        EditorApplication.Exit(0);
    }

    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string method, params object[] args)
    {
        try { target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); throw; }
    }
    private static void Check(bool condition, string message)
    {
        if (condition) return;
        Debug.LogError("FAIL: " + message);
        EditorApplication.Exit(1);
        throw new Exception(message);
    }
}

public static class DialogueCanvasGroupBootstrap
{
    public static void Run()
    {
        if (!Application.dataPath.Contains("MoteruSearchChecks-")) throw new Exception("Use an isolated project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("DialogueCanvasGroupChecks", true);
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }
}
