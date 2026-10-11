using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Common/Kayo/Yowashi BGM cues live outside the scenario text and CSV importer.</summary>
public sealed class StoryMusicController : MonoBehaviour
{
    [Serializable] public sealed class Cue
    {
        public string startLineId;
        public string track;
    }

    [Serializable] public sealed class ScenarioCues
    {
        public string scenario;
        public Cue[] cues;
    }

    [Serializable] public sealed class SceneCue
    {
        public string scene;
        public string track;
    }

    [Serializable] public sealed class MusicPlan
    {
        public float volume = 0.35f;
        public float fadeSeconds = 1.2f;
        public string[] dialogueScenes;
        public SceneCue[] sceneCues;
        public ScenarioCues[] scenarios;
    }

    private static StoryMusicController instance;
    private static bool musicEnabled = true;
    private readonly Dictionary<string, AudioClip> clips = new(StringComparer.Ordinal);
    private AudioSource[] sources;
    private MusicPlan plan;
    private Coroutine transition;
    private string requestedTrack;
    private string previousScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        musicEnabled = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance == null) new GameObject("[Story Music]").AddComponent<StoryMusicController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        TextAsset settings = Resources.Load<TextAsset>("StoryMusic/MusicPlan");
        if (settings == null)
        {
            Debug.LogError("BGMの配置設定が見つかりません。", this);
            return;
        }
        plan = JsonUtility.FromJson<MusicPlan>(settings.text);
        sources = new[] { gameObject.AddComponent<AudioSource>(), gameObject.AddComponent<AudioSource>() };
        foreach (AudioSource source in sources)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.mute = !musicEnabled;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    public static void SetEnabled(bool enabled)
    {
        musicEnabled = enabled;
        if (instance == null || instance.sources == null) return;
        foreach (AudioSource source in instance.sources) source.mute = !enabled;
    }

    public static void ApplyDialogue(Scene scene, DialogueScenarioSO scenario, DialogueLine line)
    {
        if (instance == null || instance.plan == null || scenario == null || line == null) return;
        // Shared controllers also run in other routes; never add cues to those routes.
        if (Array.IndexOf(instance.plan.dialogueScenes, scene.name) < 0) return;
        string track = ResolveTrack(instance.plan, scenario, line.lineId);
        if (!string.IsNullOrEmpty(track)) instance.PlayTrack(track);
    }

    // Resolve the segment from the destination row, not just an arrival event.
    // This handles skips, branch jumps, consultation retries and queued scene resumes.
    public static string ResolveTrack(MusicPlan musicPlan, DialogueScenarioSO scenario, string lineId)
    {
        if (musicPlan?.scenarios == null || scenario == null) return null;
        ScenarioCues segment = Array.Find(musicPlan.scenarios, item => item.scenario == scenario.name);
        if (segment?.cues == null) return null;
        int row = FindRow(scenario, lineId);
        if (row < 0) return null;
        string result = null;
        int closest = -2;
        foreach (Cue cue in segment.cues)
        {
            int start = string.IsNullOrEmpty(cue.startLineId) ? -1 : FindRow(scenario, cue.startLineId);
            if (!string.IsNullOrEmpty(cue.startLineId) && start < 0) continue;
            if (start <= row && start >= closest)
            {
                closest = start;
                result = cue.track;
            }
        }
        return result;
    }

    private static int FindRow(DialogueScenarioSO scenario, string lineId)
    {
        for (int i = 0; i < scenario.Lines.Count; i++)
            if (scenario.Lines[i].lineId == lineId) return i;
        return -1;
    }

    public static bool ShouldKeepPuzzleMusic(string sceneName, string originSceneName)
    {
        return sceneName == "PuzzleGame" &&
            (originSceneName == "Chapter_Kayo_SearchScene" || originSceneName == "NovelScene_Yowashi");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (plan == null) return;
        if (mode == LoadSceneMode.Additive) return;
        SceneCue cue = Array.Find(plan.sceneCues, item => item.scene == scene.name);
        if (cue != null) PlayTrack(cue.track);
        else if (ShouldKeepPuzzleMusic(scene.name, previousScene))
        {
            // Keep the clue-discovery music through either route's diary puzzle.
        }
        else if (Array.IndexOf(plan.dialogueScenes, scene.name) < 0)
        {
            // Title owns its original music source. Stop immediately to prevent overlap.
            PlayTrack("stop", scene.name == "Title" || scene.name == "title" ? 0f : plan.fadeSeconds);
        }
        previousScene = scene.name;
    }

    private void PlayTrack(string track, float? fadeSeconds = null)
    {
        if (sources == null || track == requestedTrack) return;
        AudioClip clip = null;
        if (track != "stop")
        {
            if (!clips.TryGetValue(track, out clip))
            {
                clip = Resources.Load<AudioClip>("StoryMusic/Tracks/" + track);
                if (clip == null)
                {
                    Debug.LogError($"BGM「{track}」が見つかりません。", this);
                    return;
                }
                clips.Add(track, clip);
            }
        }
        if (transition != null) StopCoroutine(transition);
        requestedTrack = track;
        AudioSource incoming = null;
        if (clip != null)
        {
            incoming = Array.Find(sources, source => source.clip == clip && source.isPlaying);
            if (incoming == null)
            {
                // A rapid cue change replaces the quieter source, not the audible one.
                incoming = sources[0].volume <= sources[1].volume ? sources[0] : sources[1];
                incoming.Stop();
                incoming.clip = clip;
                incoming.volume = 0f;
                incoming.Play();
            }
        }
        transition = StartCoroutine(Crossfade(incoming, fadeSeconds ?? plan.fadeSeconds));
    }

    private IEnumerator Crossfade(AudioSource incoming, float duration)
    {
        float first = sources[0].volume;
        float second = sources[1].volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            sources[0].volume = Mathf.Lerp(first, sources[0] == incoming ? plan.volume : 0f, t);
            sources[1].volume = Mathf.Lerp(second, sources[1] == incoming ? plan.volume : 0f, t);
            yield return null;
        }
        foreach (AudioSource source in sources)
        {
            source.volume = source == incoming ? plan.volume : 0f;
            if (source != incoming)
            {
                source.Stop();
                source.clip = null;
            }
        }
        transition = null;
    }
}
