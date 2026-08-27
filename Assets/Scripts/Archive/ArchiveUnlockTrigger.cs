using UnityEngine;
using UnityEngine.Events;

public sealed class ArchiveUnlockTrigger : MonoBehaviour
{
    public enum UnlockTiming
    {
        Manual,
        OnStart,
        OnEnable
    }

    [SerializeField]
    private string entryId;

    [SerializeField]
    private UnlockTiming timing = UnlockTiming.Manual;

    [SerializeField]
    private UnityEvent onFirstUnlocked;

    private void Start()
    {
        if (timing == UnlockTiming.OnStart)
        {
            Unlock();
        }
    }

    private void OnEnable()
    {
        if (timing == UnlockTiming.OnEnable)
        {
            Unlock();
        }
    }

    public void Unlock()
    {
        if (ArchiveManager.Unlock(entryId))
        {
            onFirstUnlocked?.Invoke();
        }
    }
}
