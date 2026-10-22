public static partial class GameSaveService
{
    private const string AutoSaveKey = "GameSave.Auto.v1";

    public static GameSaveData ReadAuto(out string message) => ReadCheckpoint(AutoSaveKey, out message);

    public static bool SaveAuto(out string message, bool sceneDeparture = false)
    {
        if (!GameAutoSaveSettings.Enabled) { message = "オートセーブはOFFです。"; return false; }
        if (!TryCapture(out var data, out message, sceneDeparture)) return false;
        return WriteCheckpoint(AutoSaveKey, data, out message);
    }

    public static bool LoadAuto(out string message)
    {
        var data = ReadAuto(out message);
        if (data == null) { message ??= "オートセーブのデータがありません。"; return false; }
        return LoadCheckpoint(data, out message);
    }

    public static bool SupportsCheckpoint(string scene) => System.Array.IndexOf(scenes, scene) >= 0;
}
