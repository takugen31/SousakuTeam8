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
