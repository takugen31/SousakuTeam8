using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Restores just the dialogue component from its saved scene, retaining other unsaved edits.</summary>
public static class DialogueInspectorRestore
{
    private const string RequestPath = "Temp/RestoreDialogueInspector.request.json";
    private const string ResultPath = "Temp/RestoreDialogueInspector.result.json";

    [Serializable] private sealed class Request { public string scene; }
    [Serializable] private sealed class Result
    {
        public bool success;
        public string scene;
        public string message;
        public int restoredFields;
        public int remappedReferences;
    }

    [InitializeOnLoadMethod]
    private static void ProcessRequestedRestore()
    {
        EditorApplication.delayCall += RunRequestedRestore;
    }

    private static void RunRequestedRestore()
    {
        if (!File.Exists(RequestPath)) return;
        var result = new Result();
        try
        {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
            File.Delete(RequestPath);
            if (request == null || string.IsNullOrEmpty(request.scene)) throw new Exception("復元対象シーンが指定されていません。");
            result.scene = request.scene;
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Play Modeを終了してから実行してください。");
            Scene live = SceneManager.GetSceneByPath(request.scene);
            if (!live.IsValid() || !live.isLoaded) throw new Exception("復元対象のシーンが開かれていません。");
            Restore(live, result);
            result.success = true;
            result.message = "保存済みの会話Inspector設定を復元しました。シーン内の他の未保存変更は保持しています。Undoで元に戻せます。";
            Debug.Log(result.message);
        }
        catch (Exception exception)
        {
            result.message = exception.Message;
            Debug.LogError("会話Inspector設定を復元できませんでした。 " + exception.Message);
        }
        File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
    }

    [MenuItem("Tools/Dialogue/Restore Inspector From Saved Scene")]
    private static void RestoreActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        File.WriteAllText(RequestPath, JsonUtility.ToJson(new Request { scene = scene.path }));
        RunRequestedRestore();
    }

    private static void Restore(Scene live, Result result)
    {
        Scene saved = EditorSceneManager.OpenPreviewScene(live.path);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore saved dialogue Inspector");
        try
        {
            var liveObjects = BuildObjectMap(live);
            foreach (var root in saved.GetRootGameObjects())
            foreach (var source in root.GetComponentsInChildren<NovelDialogueController>(true))
            {
                ulong ownerId = GlobalObjectId.GetGlobalObjectIdSlow(source.gameObject).targetObjectId;
                if (!liveObjects.TryGetValue(ownerId, out var ownerObject) || !(ownerObject is GameObject owner))
                    throw new Exception("保存済み会話のGameObjectを現在のシーンで特定できません。");

                var target = owner.GetComponent<NovelDialogueController>();
                if (target == null) target = Undo.AddComponent<NovelDialogueController>(owner);
                string backup = "Temp/DialogueInspectorBeforeRestore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + ownerId + ".json";
                File.WriteAllText(backup, EditorJsonUtility.ToJson(target, true));
                Undo.RecordObject(target, "Restore saved dialogue Inspector");
                var from = new SerializedObject(source);
                var to = new SerializedObject(target);
                var field = from.GetIterator();
                bool enterChildren = true;
                while (field.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (field.propertyPath == "m_Script") continue;
                    if (to.FindProperty(field.propertyPath) == null) continue;
                    to.CopyFromSerializedProperty(field);
                    result.restoredFields++;
                }
                // Preview scene references must point to the existing live scene objects.
                var property = to.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var reference = property.objectReferenceValue;
                    if (reference == null || !BelongsToScene(reference, saved)) continue;
                    ulong id = GlobalObjectId.GetGlobalObjectIdSlow(reference).targetObjectId;
                    if (!liveObjects.TryGetValue(id, out var replacement))
                        throw new Exception("参照先を復元できません: " + property.propertyPath);
                    property.objectReferenceValue = replacement;
                    result.remappedReferences++;
                }
                to.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                EditorSceneManager.MarkSceneDirty(live);
                Selection.activeGameObject = owner;
            }
            if (result.restoredFields == 0) throw new Exception("保存された会話コンポーネントが見つかりません。");
            Undo.CollapseUndoOperations(undoGroup);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(saved);
        }
    }

    private static bool BelongsToScene(UnityEngine.Object reference, Scene scene) =>
        reference is GameObject go ? go.scene == scene : reference is Component component && component.gameObject.scene == scene;

    private static Dictionary<ulong, UnityEngine.Object> BuildObjectMap(Scene scene)
    {
        var objects = new Dictionary<ulong, UnityEngine.Object>();
        foreach (var root in scene.GetRootGameObjects())
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            var go = transform.gameObject;
            objects[GlobalObjectId.GetGlobalObjectIdSlow(go).targetObjectId] = go;
            foreach (var component in go.GetComponents<Component>())
                if (component != null) objects[GlobalObjectId.GetGlobalObjectIdSlow(component).targetObjectId] = component;
        }
        return objects;
    }
}
