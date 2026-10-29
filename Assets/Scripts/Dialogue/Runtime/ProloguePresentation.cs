using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Doute's introductory profile, using the existing dialogue input.</summary>
public sealed class ProloguePresentation
{
    public const string ProfileLineId = "prologue1_profile";
    private readonly RectTransform parent;
    private readonly TMP_FontAsset font;
    private readonly TMP_Text dialogueText;
    private GameObject profileView;
    private Image profilePortrait;
    private TMP_Text profileBody;
    private CanvasGroup dialogueGroup;
    private float originalDialogueAlpha;

    public ProloguePresentation(RectTransform parent, TMP_Text dialogueText)
    {
        this.parent = parent;
        this.dialogueText = dialogueText;
        font = dialogueText.font;
    }

    public void ShowProfile(string body, Sprite portrait)
    {
        Hide();
        if (profileView == null)
        {
            Image shade = ImageAt("Doute Profile", parent, new Color(0f, 0f, 0f, 0.75f));
            profileView = shade.gameObject;
            Region(shade.rectTransform, Vector2.zero, Vector2.one);
            Image card = ImageAt("Profile Card", shade.transform, new Color(0.055f, 0.055f, 0.055f, 0.97f));
            Region(card.rectTransform, new Vector2(0.12f, 0.09f), new Vector2(0.88f, 0.91f));
            card.gameObject.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(24f, 14f));
            profilePortrait = ImageAt("Doute Portrait", card.transform, Color.white);
            profilePortrait.preserveAspect = true;
            Region(profilePortrait.rectTransform, new Vector2(0.035f, 0.05f), new Vector2(0.44f, 0.94f));
            TMP_Text heading = TextAt("Profile Heading", card.transform, "プロフィール", 23f,
                new Vector2(0.5f, 0.76f), new Vector2(0.94f, 0.9f), TextAlignmentOptions.MidlineLeft);
            heading.color = new Color(0.78f, 0.8f, 0.8f);
            profileBody = TextAt("Profile Body", card.transform, "", 28f,
                new Vector2(0.5f, 0.25f), new Vector2(0.94f, 0.75f), TextAlignmentOptions.MidlineLeft);
            profileBody.enableAutoSizing = true;
            profileBody.fontSizeMin = 20f;
            profileBody.fontSizeMax = 28f;
            TextAt("Continue Hint", card.transform, "クリックで続ける", 18f,
                new Vector2(0.5f, 0.09f), new Vector2(0.94f, 0.2f), TextAlignmentOptions.MidlineLeft);
        }
        if (dialogueGroup == null)
        {
            dialogueGroup = dialogueText.GetComponent<CanvasGroup>();
            if (dialogueGroup == null) dialogueGroup = dialogueText.gameObject.AddComponent<CanvasGroup>();
            originalDialogueAlpha = dialogueGroup.alpha;
        }
        dialogueGroup.alpha = 0f;
        profilePortrait.sprite = portrait;
        profileBody.text = body;
        profileView.transform.SetAsLastSibling();
        profileView.SetActive(true);
    }

    public void Hide()
    {
        if (profileView != null) profileView.SetActive(false);
        if (dialogueGroup != null) dialogueGroup.alpha = originalDialogueAlpha;
    }

    private Image ImageAt(string name, Transform owner, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(owner, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text TextAt(string name, Transform owner, string content, float size, Vector2 min, Vector2 max, TextAlignmentOptions alignment)
    {
        TMP_Text text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.transform.SetParent(owner, false);
        text.font = font;
        text.fontSize = size;
        text.color = new Color(0.96f, 0.96f, 0.94f);
        text.alignment = alignment;
        text.raycastTarget = false;
        text.text = content;
        Region(text.rectTransform, min, max);
        return text;
    }

    private static void Region(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
