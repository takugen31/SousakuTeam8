using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public sealed class ArchiveManager : MonoBehaviour
{
    private const string SaveKey = "ArchiveState.v1";
    private const string DatabaseResourcePath = "Archive/ArchiveDatabase";

    private static ArchiveManager instance;

    private readonly HashSet<string> unlockedIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> readIds =
        new HashSet<string>(StringComparer.Ordinal);

    private ArchiveDatabase database;
    private ArchiveMenuUI menu;
    private GameObject ownedEventSystem;

    public static ArchiveManager Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    public static bool IsOpen => instance != null && instance.menu != null && instance.menu.IsOpen;
    public ArchiveDatabase Database => database;
    public IReadOnlyList<ArchiveEntry> Entries => database.Entries;

    public event Action<ArchiveEntry> EntryUnlocked;
    public event Action ArchiveChanged;
    public event Action<bool> OpenStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        ArchiveManager existing = FindFirstObjectByType<ArchiveManager>();

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject root = new GameObject("[Archive System]");
        instance = root.AddComponent<ArchiveManager>();
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

        database = Resources.Load<ArchiveDatabase>(DatabaseResourcePath);

        if (database == null)
        {
            database = ArchiveDatabase.CreateFallback();
            Debug.LogWarning(
                $"Resources/{DatabaseResourcePath} が見つからないため、" +
                "組み込みのアーカイブ項目を使用します。");
        }

        LoadState();
        UnlockInitialEntries();

        menu = gameObject.AddComponent<ArchiveMenuUI>();
        menu.Initialize(this, database.UiFont);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SaveState();
        instance = null;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        bool isEditingSearch = menu != null && menu.IsEditingSearch;

        if (keyboard.bKey.wasPressedThisFrame && !isEditingSearch)
        {
            ToggleArchive();
            return;
        }

        if (!IsOpen)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseArchive();
            return;
        }

        menu.HandleKeyboard(keyboard);
    }

    private void OnApplicationQuit()
    {
        SaveState();
    }

    public static bool Unlock(string entryId)
    {
        return Instance.UnlockEntry(entryId);
    }

    public static bool IsUnlocked(string entryId)
    {
        return Instance.unlockedIds.Contains(entryId);
    }

    public static void Open()
    {
        Instance.OpenArchive();
    }

    public static void Close()
    {
        if (instance != null)
        {
            instance.CloseArchive();
        }
    }

    public bool UnlockEntry(string entryId)
    {
        if (!database.TryGetEntry(entryId, out ArchiveEntry entry))
        {
            Debug.LogWarning($"アーカイブID「{entryId}」はデータベースに存在しません。");
            return false;
        }

        if (!unlockedIds.Add(entryId))
        {
            return false;
        }

        readIds.Remove(entryId);
        SaveState();
        EntryUnlocked?.Invoke(entry);
        ArchiveChanged?.Invoke();

        if (menu != null)
        {
            menu.ShowUnlockNotification(entry.Title);
        }

        return true;
    }

    public bool IsEntryUnlocked(ArchiveEntry entry)
    {
        return entry != null && unlockedIds.Contains(entry.Id);
    }

    public bool IsEntryRead(ArchiveEntry entry)
    {
        return entry != null && readIds.Contains(entry.Id);
    }

    public void MarkRead(ArchiveEntry entry)
    {
        if (!IsEntryUnlocked(entry) || !readIds.Add(entry.Id))
        {
            return;
        }

        SaveState();
        ArchiveChanged?.Invoke();
    }

    /// <summary>
    /// ニューゲーム時にアーカイブの解放状態を初期化します。
    /// </summary>
    public void ResetProgress()
    {
        unlockedIds.Clear();
        readIds.Clear();
        UnlockInitialEntries();
        SaveState();
        ArchiveChanged?.Invoke();
    }

    public void ToggleArchive()
    {
        if (IsOpen)
        {
            CloseArchive();
        }
        else
        {
            OpenArchive();
        }
    }

    public void OpenArchive()
    {
        if (menu == null || menu.IsOpen)
        {
            return;
        }

        EnsureEventSystem();
        menu.Open();
        OpenStateChanged?.Invoke(true);
    }

    public void CloseArchive()
    {
        if (menu == null || !menu.IsOpen)
        {
            return;
        }

        menu.Close();
        OpenStateChanged?.Invoke(false);
    }

    private void UnlockInitialEntries()
    {
        bool changed = false;

        foreach (ArchiveEntry entry in database.Entries)
        {
            if (entry != null && entry.UnlockedAtStart)
            {
                changed |= unlockedIds.Add(entry.Id);
            }
        }

        if (changed)
        {
            SaveState();
        }
    }

    private void LoadState()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            return;
        }

        ArchiveSaveData state = JsonUtility.FromJson<ArchiveSaveData>(
            PlayerPrefs.GetString(SaveKey));

        if (state == null)
        {
            return;
        }

        unlockedIds.Clear();
        readIds.Clear();

        if (state.unlockedIds != null)
        {
            unlockedIds.UnionWith(state.unlockedIds);
        }

        if (state.readIds != null)
        {
            readIds.UnionWith(state.readIds);
        }
    }

    private void SaveState()
    {
        ArchiveSaveData state = new ArchiveSaveData
        {
            unlockedIds = new List<string>(unlockedIds),
            readIds = new List<string>(readIds)
        };

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(state));
        PlayerPrefs.Save();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EventSystem[] systems = FindObjectsByType<EventSystem>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        bool hasSceneEventSystem = false;

        foreach (EventSystem system in systems)
        {
            if (system != null && system.gameObject != ownedEventSystem)
            {
                hasSceneEventSystem = true;
                break;
            }
        }

        if (hasSceneEventSystem && ownedEventSystem != null)
        {
            Destroy(ownedEventSystem);
            ownedEventSystem = null;
        }
        else if (!hasSceneEventSystem && ownedEventSystem == null && IsOpen)
        {
            EnsureEventSystem();
        }
    }

    private void EnsureEventSystem()
    {
        EventSystem existing = FindFirstObjectByType<EventSystem>(
            FindObjectsInactive.Exclude);

        if (existing != null)
        {
            return;
        }

        ownedEventSystem = new GameObject(
            "[Archive EventSystem]",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(ownedEventSystem);
    }

    [Serializable]
    private sealed class ArchiveSaveData
    {
        public List<string> unlockedIds = new List<string>();
        public List<string> readIds = new List<string>();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Archive/Clear Saved Data")]
    private static void ClearSavedData()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        Debug.Log("アーカイブの保存データを削除しました。");
    }

    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ClearArchiveProgressOnEnterPlayMode()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }
#endif
}
