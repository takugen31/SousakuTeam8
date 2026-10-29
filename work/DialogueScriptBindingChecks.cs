using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DialogueScriptBindingChecks
{
    private const string ScriptGuid = "e628b47772e4bed449c9e78833eb4135";

    public static void Run()
    {
        string path = AssetDatabase.GUIDToAssetPath(ScriptGuid);
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        Type boundClass = script != null ? script.GetClass() : null;
        Debug.Log("Dialogue MonoScript binding: " + path + " -> " + (boundClass?.FullName ?? "null"));
        if (boundClass != typeof(NovelDialogueController))
            throw new Exception("Dialogue MonoScript binding failed: the serialized script GUID cannot resolve NovelDialogueController.");

        foreach (string name in new[] { "NovelScene", "NovelScene_Kayo", "NovelScene_Moteru", "NovelScene_Yowashi" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameMap/" + name + ".unity");
            var dialogues = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NovelDialogueController>(true)).ToArray();
            if (dialogues.Length != 1) throw new Exception(name + ": serialized dialogue component is missing.");
            var serialized = new SerializedObject(dialogues[0]);
            foreach (string field in new[] { "scenario", "characterDatabase", "bodyText", "dialogueRoot" })
                if (serialized.FindProperty(field).objectReferenceValue == null)
                    throw new Exception(name + ": lost serialized field " + field);
            if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(dialogues[0]))) != ScriptGuid)
                throw new Exception(name + ": dialogue component no longer uses the original script GUID.");
            Debug.Log("PASS: " + name + " deserializes its original dialogue component and scenario/database/UI references.");
            if (name == "NovelScene_Moteru") CheckInspectorRestore(dialogues[0]);
        }
    }

    private static void CheckInspectorRestore(NovelDialogueController dialogue)
    {
        string original = EditorJsonUtility.ToJson(dialogue);
        var fields = new SerializedObject(dialogue);
        fields.FindProperty("scenario").objectReferenceValue = null;
        fields.FindProperty("bodyText").objectReferenceValue = null;
        fields.FindProperty("charactersPerSecond").floatValue = 3f;
        fields.ApplyModifiedPropertiesWithoutUndo();
        var unsaved = new GameObject("Unrelated unsaved object");
        unsaved.transform.position = new Vector3(17, 23, 5);
        if (!EditorApplication.ExecuteMenuItem("Tools/Dialogue/Restore Inspector From Saved Scene"))
            throw new Exception("Inspector restore menu is unavailable.");
        if (EditorJsonUtility.ToJson(dialogue) != original)
            throw new Exception("Inspector restoration did not recover all serialized fields or scene references.");
        if (unsaved == null || unsaved.transform.position != new Vector3(17, 23, 5))
            throw new Exception("Inspector restoration overwrote unrelated unsaved scene edits.");
        Debug.Log("PASS: saved Inspector fields and live-scene references restored exactly; unrelated unsaved edits retained.");
    }
}
