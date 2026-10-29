using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared, per-character results for the current play session.</summary>
public static class TimingGameResults
{
    public enum Outcome { NotPlayed, Success, Failure }

    public readonly struct Record
    {
        public Outcome Latest { get; }
        public int SuccessCount { get; }
        public int FailureCount { get; }

        public Record(Outcome latest, int successCount, int failureCount)
        {
            Latest = latest;
            SuccessCount = successCount;
            FailureCount = failureCount;
        }
    }

    private static readonly Dictionary<string, Record> records = new();

    [System.Serializable]
    public sealed class Snapshot
    {
        public string characterId;
        public Outcome latest;
        public int successCount;
        public int failureCount;
    }

    public static Snapshot[] Capture()
    {
        var snapshots = new List<Snapshot>();
        foreach (var entry in records)
            snapshots.Add(new Snapshot { characterId = entry.Key, latest = entry.Value.Latest,
                successCount = entry.Value.SuccessCount, failureCount = entry.Value.FailureCount });
        return snapshots.ToArray();
    }

    public static void Restore(Snapshot[] snapshots)
    {
        records.Clear();
        if (snapshots == null) return;
        foreach (var entry in snapshots)
            if (entry != null && !string.IsNullOrWhiteSpace(entry.characterId) &&
                entry.latest >= Outcome.NotPlayed && entry.latest <= Outcome.Failure &&
                entry.successCount >= 0 && entry.failureCount >= 0)
                records[Normalize(entry.characterId)] = new Record(entry.latest, entry.successCount, entry.failureCount);
    }

    public static Record Get(string characterId)
    {
        return records.TryGetValue(Normalize(characterId), out Record record) ? record : default;
    }

    public static void Save(string characterId, bool success)
    {
        string key = Normalize(characterId);
        Record previous = Get(key);
        records[key] = new Record(success ? Outcome.Success : Outcome.Failure,
            previous.SuccessCount + (success ? 1 : 0), previous.FailureCount + (success ? 0 : 1));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetAll() => records.Clear();

    private static string Normalize(string characterId) =>
        string.IsNullOrWhiteSpace(characterId) ? "standalone" : characterId.Trim().ToLowerInvariant();
}
