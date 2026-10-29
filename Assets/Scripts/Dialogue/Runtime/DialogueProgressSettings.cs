using System;
using System.IO;
using UnityEngine;

/// <summary>Read-only, build-time configuration shared by all dialogue controllers.</summary>
public static class DialogueProgressSettings
{
    private const string ResourcePath = "DialogueSceneProgressSettings";
    private static Configuration settings;

    [Serializable]
    private sealed class Configuration
    {
        public bool enabled = true;
        public bool defaultSceneEnabled = true;
        public SceneSetting[] scenes = Array.Empty<SceneSetting>();
    }

    [Serializable]
    private sealed class SceneSetting
    {
        public string scene;
        public bool enabled = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => settings = null;

    public static bool IsEnabled(string scene)
    {
        if (settings == null)
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            settings = Parse(asset != null ? asset.text : null);
        }
        if (!settings.enabled) return false;
        string name = SceneName(scene);
        foreach (var entry in settings.scenes)
            if (string.Equals(entry.scene, name, StringComparison.Ordinal)) return entry.enabled;
        return settings.defaultSceneEnabled;
    }

    private static string SceneName(string scene) => string.IsNullOrEmpty(scene)
        ? "" : Path.GetFileNameWithoutExtension(scene.Replace('\\', '/'));

    private static Configuration Parse(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("設定ファイルがありません。");
            var result = new Configuration();
            JsonUtility.FromJsonOverwrite(json, result);
            if (result.scenes == null) throw new FormatException("scenesには配列を指定してください。");
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in result.scenes)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.scene))
                    throw new FormatException("scenesのsceneにシーン名を指定してください。");
                entry.scene = SceneName(entry.scene.Trim());
                if (!names.Add(entry.scene)) throw new FormatException("シーン名が重複しています: " + entry.scene);
            }
            return result;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("会話プログレス設定を読み込めないため、全シーンONで動作します。 " + exception.Message);
            return new Configuration();
        }
    }
}
