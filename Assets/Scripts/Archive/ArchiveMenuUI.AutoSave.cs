using TMPro;

public sealed partial class ArchiveMenuUI
{
    private TMP_Text autoSaveSettingLabel;

    private void BuildAutoSaveSetting()
    {
        var button = CreateMenuItem("AutoSaveSetting", "", 0.10f);
        autoSaveSettingLabel = button.GetComponentInChildren<TMP_Text>();
        autoSaveSettingLabel.fontSize = 25f;
        autoSaveSettingLabel.enableAutoSizing = true;
        autoSaveSettingLabel.fontSizeMin = 18f;
        autoSaveSettingLabel.fontSizeMax = 25f;
        button.onClick.AddListener(() =>
        {
            GameAutoSaveSettings.Enabled = !GameAutoSaveSettings.Enabled;
            UpdateAutoSaveSetting();
        });
        UpdateAutoSaveSetting();
    }

    private void UpdateAutoSaveSetting()
    {
        if (autoSaveSettingLabel != null)
            autoSaveSettingLabel.text = "オートセーブ：" + (GameAutoSaveSettings.Enabled ? "ON" : "OFF");
    }
}
