using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The game Doute is playing and his introductory profile, using the existing dialogue input.</summary>
public sealed class ProloguePresentation
{
    public const string ProfileLineId = "prologue1_profile";
    private readonly RectTransform parent;
    private readonly TMP_FontAsset font;
    private readonly TMP_Text dialogueText;
    private GameObject gameView, profileView;
    private Image gameBackground, heroine, profilePortrait;
    private TMP_Text profileBody;
    private CanvasGroup dialogueGroup;
    private float originalDialogueAlpha;

    public ProloguePresentation(RectTransform parent, TMP_Text dialogueText)
    {
        this.parent = parent;
        this.dialogueText = dialogueText;
        font = dialogueText.font;
    }

    public static bool IsGameLine(string lineId)
    {
        return lineId == "prologue_001" || lineId == "prologue_002" || lineId == "prologue_003" ||
            lineId == "prologue_004" || lineId == "prologue_005";
    }

    public void ShowGame(Sprite background, Sprite heroineSprite)
    {
        Hide();
        if (gameView == null)
        {
            Image shade = ImageAt("Prologue Game Screen", parent, new Color(0f, 0f, 0f, 0.38f));
            gameView = shade.gameObject;
            Region(shade.rectTransform, Vector2.zero, Vector2.one);
            shade.transform.SetAsFirstSibling();
            Image border = ImageAt("Screen Frame", shade.transform, new Color(0.03f, 0.03f, 0.03f, 1f));
            Region(border.rectTransform, new Vector2(0.055f, 0.045f), new Vector2(0.945f, 0.935f));
            border.gameObject.AddComponent<RectMask2D>();
            gameBackground = ImageAt("Game Background", border.transform, Color.white);
            Region(gameBackground.rectTransform, new Vector2(0.005f, 0.008f), new Vector2(0.995f, 0.992f));
            heroine = ImageAt("Game Heroine", border.transform, Color.white);
            heroine.preserveAspect = true;
            Region(heroine.rectTransform, new Vector2(0.51f, -0.64f), new Vector2(1.01f, 0.98f));
            TextAt("Game Heading", shade.transform, "恋愛ゲーム", 20f,
                new Vector2(0.065f, 0.94f), new Vector2(0.7f, 0.99f), TextAlignmentOptions.MidlineLeft);
        }
        gameBackground.sprite = background;
        heroine.sprite = heroineSprite;
        heroine.enabled = heroineSprite != null;
        gameView.SetActive(true);
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
        if (gameView != null) gameView.SetActive(false);
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
