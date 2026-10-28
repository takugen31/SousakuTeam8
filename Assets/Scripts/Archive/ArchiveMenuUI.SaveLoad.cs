using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ArchiveMenuUI
{
    private Button saveMenuButton;
    private GameObject saveLoadRoot;
    private GameObject saveLoadConfirmation;
    private TMP_Text saveLoadTitle;
    private TMP_Text saveLoadMessage;
    private TMP_Text saveLoadWarning;
    private TMP_Text saveLoadBackLabel;
    private bool loadOpenedFromTitle;
    private readonly Button[] saveSlotButtons = new Button[GameSaveService.SlotCount];
    private readonly TMP_Text[] saveSlotLabels = new TMP_Text[GameSaveService.SlotCount];
    private bool loadingSlots;
    private Action confirmedSaveLoad;
    private float nextSaveAvailabilityCheck;

    private void UpdateSaveAvailability()
    {
        if (saveMenuButton == null) return;
        if (Time.unscaledTime < nextSaveAvailabilityCheck) return;
        nextSaveAvailabilityCheck = Time.unscaledTime + 0.2f;
        saveMenuButton.interactable = GameSaveService.CanSave(out _);
        saveMenuButton.GetComponentInChildren<TMP_Text>().color = saveMenuButton.interactable ? Color.white : LockedTextColor;
    }

    private void HideSaveLoad()
    {
        if (saveLoadRoot != null) saveLoadRoot.SetActive(false);
        if (saveLoadConfirmation != null) saveLoadConfirmation.SetActive(false);
        confirmedSaveLoad = null;
        nextSaveAvailabilityCheck = 0f;
        loadOpenedFromTitle = false;
    }

    public void ShowLoadFromTitle()
    {
        ShowSaveLoad(true);
        loadOpenedFromTitle = true;
        saveLoadBackLabel.text = "← タイトル";
    }

    private void BuildSaveLoadUI()
    {
        saveLoadRoot = CreatePanel("SaveLoad", windowRoot.transform, PanelColor);
        SetAnchors(saveLoadRoot, new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.90f), Vector2.zero, Vector2.zero);
        saveLoadRoot.AddComponent<DialogueWindowFeather>();
        saveLoadTitle = CreateText("Title", saveLoadRoot.transform, "セーブ", 46f, Color.white, FontStyles.Bold);
        SetAnchors(saveLoadTitle.gameObject, new Vector2(0.07f, 0.84f), new Vector2(0.9f, 0.95f), Vector2.zero, Vector2.zero);
        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            int slot = i;
            float x = i % 2 == 0 ? 0.06f : 0.52f;
            float y = 0.61f - (i / 2) * 0.23f;
            Button button = CreateButton("Slot" + (i + 1), saveLoadRoot.transform, "", 23f, new Color(0.08f, 0.08f, 0.08f, 0.95f));
            SetAnchors(button.gameObject, new Vector2(x, y), new Vector2(x + 0.42f, y + 0.20f), Vector2.zero, Vector2.zero);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            button.onClick.AddListener(() => SelectSaveSlot(slot));
            saveSlotButtons[i] = button;
            saveSlotLabels[i] = label;
        }
        saveLoadMessage = CreateText("Status", saveLoadRoot.transform, "", 21f, MutedTextColor);
        SetAnchors(saveLoadMessage.gameObject, new Vector2(0.07f, 0.025f), new Vector2(0.68f, 0.12f), Vector2.zero, Vector2.zero);
        Button back = CreateButton("BackToMenu", saveLoadRoot.transform, "← メニュー", 22f, AccentSoftColor);
        SetAnchors(back.gameObject, new Vector2(0.76f, 0.035f), new Vector2(0.94f, 0.11f), Vector2.zero, Vector2.zero);
        TMP_Text backLabel = back.GetComponentInChildren<TMP_Text>();
        saveLoadBackLabel = backLabel;
        Stretch(backLabel.rectTransform, 16f, 16f, 4f, 4f);
        backLabel.alignment = TextAlignmentOptions.Center;
        backLabel.enableAutoSizing = false;
        backLabel.textWrappingMode = TextWrappingModes.NoWrap;
        back.onClick.AddListener(() =>
        {
            if (loadOpenedFromTitle) manager.CloseArchive();
            else ShowMenu();
        });
        saveLoadRoot.SetActive(false);

        saveLoadConfirmation = CreatePanel("SaveLoadConfirmation", windowRoot.transform, new Color(0f, 0f, 0f, 0.55f));
        Stretch(saveLoadConfirmation.GetComponent<RectTransform>());
        GameObject box = CreatePanel("Confirmation", saveLoadConfirmation.transform, new Color(0f, 0f, 0f, 0.95f));
        SetAnchors(box, new Vector2(0.27f, 0.33f), new Vector2(0.73f, 0.67f), Vector2.zero, Vector2.zero);
        box.AddComponent<DialogueWindowFeather>();
        saveLoadWarning = CreateText("Warning", box.transform, "", 29f, Color.white, FontStyles.Bold);
        SetAnchors(saveLoadWarning.gameObject, new Vector2(0.07f, 0.45f), new Vector2(0.93f, 0.90f), Vector2.zero, Vector2.zero);
        saveLoadWarning.alignment = TextAlignmentOptions.Center;
        Button yes = CreateButton("Yes", box.transform, "はい", 28f, new Color(0.08f, 0.08f, 0.08f, 0.95f));
        SetAnchors(yes.gameObject, new Vector2(0.12f, 0.13f), new Vector2(0.46f, 0.35f), Vector2.zero, Vector2.zero);
        yes.onClick.AddListener(() => { Action action = confirmedSaveLoad; confirmedSaveLoad = null; saveLoadConfirmation.SetActive(false); action?.Invoke(); });
        Button no = CreateButton("No", box.transform, "いいえ", 28f, new Color(0.08f, 0.08f, 0.08f, 0.95f));
        SetAnchors(no.gameObject, new Vector2(0.54f, 0.13f), new Vector2(0.88f, 0.35f), Vector2.zero, Vector2.zero);
        no.onClick.AddListener(() => { confirmedSaveLoad = null; saveLoadConfirmation.SetActive(false); });
        saveLoadConfirmation.SetActive(false);
    }

    private void ShowSaveLoad(bool load)
    {
        loadOpenedFromTitle = false;
        saveLoadBackLabel.text = "← メニュー";
        loadingSlots = load;
        menuRoot.SetActive(false);
        archiveFrame.SetActive(false);
        restartConfirmation.SetActive(false);
        saveLoadRoot.SetActive(true);
        windowRoot.GetComponent<Image>().color = BackdropColor;
        saveLoadTitle.text = load ? "ロード" : "セーブ";
        saveLoadMessage.text = string.Empty;
        RefreshSaveSlots();
    }

    private void RefreshSaveSlots()
    {
        bool canSave = GameSaveService.CanSave(out string reason);
        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            GameSaveData data = GameSaveService.Read(i, out string error);
            string text = data == null ? error ?? "データなし"
                : data.timestamp + "  " + data.label + "\n" + (data.text ?? "").Replace("\n", " ");
            saveSlotLabels[i].text = "セーブ " + (i + 1).ToString("00") + "\n" + text;
            saveSlotButtons[i].interactable = loadingSlots ? data != null : canSave;
            saveSlotLabels[i].color = saveSlotButtons[i].interactable ? Color.white : LockedTextColor;
        }
        if (!loadingSlots && !canSave) saveLoadMessage.text = reason;
    }

    private void SelectSaveSlot(int slot)
    {
        GameSaveData old = GameSaveService.Read(slot, out _);
        Action action = () =>
        {
            if (loadingSlots)
            {
                if (!GameSaveService.Load(slot, out string error)) saveLoadMessage.text = error;
            }
            else
            {
                GameSaveService.Save(slot, out string message);
                RefreshSaveSlots();
                saveLoadMessage.text = message;
            }
        };
        if (loadingSlots || old != null)
        {
            saveLoadWarning.text = loadingSlots
                ? "このデータをロードします。\n現在の未保存の進行は失われます。\nよろしいですか？"
                : "この枠のデータを上書きします。\nよろしいですか？";
            confirmedSaveLoad = action;
            saveLoadConfirmation.SetActive(true);
            saveLoadConfirmation.transform.SetAsLastSibling();
        }
        else action();
    }
}
