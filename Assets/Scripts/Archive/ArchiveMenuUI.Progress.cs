using TMPro;
using UnityEngine;

public sealed partial class ArchiveMenuUI
{
    private TMP_Text progressSaveLabel;

    private void BuildProgressSetting()
    {
        var button = CreateMenuItem("ProgressSave", "", 0.10f);
        progressSaveLabel = button.GetComponentInChildren<TMP_Text>();
        progressSaveLabel.fontSize = 25f;
        progressSaveLabel.enableAutoSizing = true;
        progressSaveLabel.fontSizeMin = 18f;
        progressSaveLabel.fontSizeMax = 25f;
        button.onClick.AddListener(() =>
        {
            DialogueProgressStore.Enabled = !DialogueProgressStore.Enabled;
            UpdateProgressSetting();
        });
        UpdateProgressSetting();
    }

    private void UpdateProgressSetting()
    {
        if (progressSaveLabel != null)
            progressSaveLabel.text = "会話プログレス：" + (DialogueProgressStore.Enabled ? "ON（続きから）" : "OFF（先頭から）");
    }
}
