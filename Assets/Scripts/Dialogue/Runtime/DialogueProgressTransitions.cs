using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Writes an entry point into the progress timeline and loads its dialogue scene.</summary>
public static class DialogueProgressTransitions
{
    public static bool ReturnToScene(string sceneName, string lineId, bool fadeIn = true,
        DialogueProgressEntryMode mode = DialogueProgressEntryMode.OverrideSavedProgress)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(lineId) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("会話プログレスの戻り先シーンまたは開始行が無効です。");
            return false;
        }

        string scene = Path.GetFileNameWithoutExtension(sceneName.Replace('\\', '/'));
        DialogueProgressStore.TryGetSceneEntry(scene, out var previous);
        try
        {
            DialogueProgressStore.SetSceneEntry(new DialogueProgressSceneEntry
            {
                scene = scene, lineId = lineId.Trim(), fadeIn = fadeIn, mode = mode
            });
            // OFF still loads the scene, whose normal entry ignores history and entry points.
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return true;
        }
        catch (Exception exception)
        {
            if (previous != null) DialogueProgressStore.SetSceneEntry(previous);
            else DialogueProgressStore.RemoveSceneEntry(scene);
            Debug.LogError("会話シーンに戻れませんでした。 " + exception.Message);
            return false;
        }
    }
}
