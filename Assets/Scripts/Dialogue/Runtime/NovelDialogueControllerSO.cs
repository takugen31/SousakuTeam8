using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class NovelDialogueController : MonoBehaviour
{
    [Header("Dialogue Data")]
    [SerializeField]
    private DialogueScenarioSO scenario;

    [SerializeField]
    private CharacterDatabaseSO characterDatabase;

    [SerializeField]
    private string startLineId;

    [Header("Affection")]
    [SerializeField]
    private AffectionManager affectionManager;

    [Header("UI")]
    [SerializeField]
    private GameObject dialogueRoot;

    [SerializeField]
    private GameObject namePlate;

    [SerializeField]
    private TMP_Text speakerNameText;

    [SerializeField]
    private TMP_Text bodyText;

    [SerializeField]
    private Image portraitImage;

    [SerializeField]
    private Button nextButton;

    [Header("Text Animation")]
    [SerializeField]
    [Min(1f)]
    private float charactersPerSecond = 40f;

    [SerializeField]
    private bool playOnStart = true;

    [Header("Events")]
    [SerializeField]
    private UnityEvent onDialogueCompleted;

    private DialogueLine currentLine;
    private Coroutine typingCoroutine;

    private bool isPlaying;
    private bool isTyping;

    private void Awake()
    {
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(Advance);
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartDialogue();
        }
    }

    private void OnDestroy()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(Advance);
        }
    }

    public void StartDialogue()
    {
        if (scenario == null)
        {
            Debug.LogError("DialogueScenarioが設定されていません。");
            return;
        }

        DialogueLine firstLine;

        if (!string.IsNullOrWhiteSpace(startLineId))
        {
            if (!scenario.TryGetLine(startLineId, out firstLine))
            {
                Debug.LogError(
                    $"開始セリフ「{startLineId}」が存在しません。");
                return;
            }
        }
        else
        {
            if (!scenario.TryGetFirstLine(out firstLine))
            {
                Debug.LogError("シナリオにセリフがありません。");
                return;
            }
        }

        isPlaying = true;

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(true);
        }

        ShowLine(firstLine);
    }

    public void StartDialogueAt(string lineId)
    {
        startLineId = lineId;
        StartDialogue();
    }

    public void Advance()
    {
        if (!isPlaying || currentLine == null)
        {
            return;
        }

        // タイプライター表示中なら全文表示する
        if (isTyping)
        {
            CompleteTyping();
            return;
        }

        if (!TryResolveNextLine(out DialogueLine nextLine))
        {
            EndDialogue();
            return;
        }

        ShowLine(nextLine);
    }

    private bool TryResolveNextLine(out DialogueLine nextLine)
    {
        nextLine = null;

        AffectionManager affection = ResolveAffectionManager();

        // 1. 好感度に応じた分岐（先頭から順に評価）
        if (currentLine.branches != null &&
            currentLine.branches.Count > 0)
        {
            if (affection == null)
            {
                Debug.LogWarning(
                    $"セリフ「{currentLine.lineId}」に分岐がありますが、" +
                    "AffectionManagerが見つかりません。分岐をスキップします。");
            }
            else
            {
                foreach (DialogueBranch branch in currentLine.branches)
                {
                    if (branch == null)
                    {
                        continue;
                    }

                    if (!affection.EvaluateAll(branch.conditions))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(branch.nextLineId))
                    {
                        continue;
                    }

                    if (!scenario.TryGetLine(
                            branch.nextLineId,
                            out nextLine))
                    {
                        Debug.LogError(
                            $"分岐先のセリフ「{branch.nextLineId}」が" +
                            "存在しません。");
                        return false;
                    }

                    affection.ApplyDeltas(branch.affectionChanges);
                    return true;
                }
            }
        }

        // 2. nextLineIdが設定されていればそこへ移動
        if (!string.IsNullOrWhiteSpace(currentLine.nextLineId))
        {
            if (!scenario.TryGetLine(
                    currentLine.nextLineId,
                    out nextLine))
            {
                Debug.LogError(
                    $"次のセリフ「{currentLine.nextLineId}」が" +
                    "存在しません。");
                return false;
            }

            return true;
        }

        // 3. nextLineIdが空ならCSV上の次の行へ進む
        return scenario.TryGetNextLine(
            currentLine.lineId,
            out nextLine);
    }

    private AffectionManager ResolveAffectionManager()
    {
        if (affectionManager != null)
        {
            return affectionManager;
        }

        return AffectionManager.Instance;
    }

    private void ShowLine(DialogueLine line)
    {
        currentLine = line;

        ApplyAffectionChanges(line);
        ApplyCharacter(line);
        StartTyping(line.text);
    }

    private void ApplyAffectionChanges(DialogueLine line)
    {
        if (line == null ||
            line.affectionChanges == null ||
            line.affectionChanges.Count == 0)
        {
            return;
        }

        AffectionManager affection = ResolveAffectionManager();

        if (affection == null)
        {
            Debug.LogWarning(
                $"セリフ「{line.lineId}」に好感度の変化がありますが、" +
                "AffectionManagerが見つかりません。");
            return;
        }

        affection.ApplyDeltas(line.affectionChanges);
    }

    private void ApplyCharacter(DialogueLine line)
    {
        // speakerIdが空なら地の文
        if (string.IsNullOrWhiteSpace(line.speakerId))
        {
            if (namePlate != null)
            {
                namePlate.SetActive(false);
            }

            if (speakerNameText != null)
            {
                speakerNameText.text = string.Empty;
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.enabled = false;
            }

            return;
        }

        if (!characterDatabase.TryGetCharacter(
                line.speakerId,
                out CharacterData character))
        {
            Debug.LogError(
                $"キャラクター「{line.speakerId}」が存在しません。");

            return;
        }

        if (namePlate != null)
        {
            namePlate.SetActive(true);
        }

        if (speakerNameText != null)
        {
            speakerNameText.text = character.displayName;
            speakerNameText.color = character.nameColor;
        }

        if (portraitImage != null)
        {
            Sprite portrait =
                character.GetPortrait(line.expressionId);

            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }
    }

    private void StartTyping(string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        bodyText.text = text;
        bodyText.maxVisibleCharacters = 0;

        typingCoroutine = StartCoroutine(TypeText());
    }

    private IEnumerator TypeText()
    {
        isTyping = true;

        // TMPの文字情報が更新されるのを待つ
        yield return null;

        bodyText.ForceMeshUpdate();

        int totalCharacters =
            bodyText.textInfo.characterCount;

        float visibleCharacters = 0f;

        while (bodyText.maxVisibleCharacters < totalCharacters)
        {
            visibleCharacters +=
                charactersPerSecond * Time.unscaledDeltaTime;

            bodyText.maxVisibleCharacters =
                Mathf.Min(
                    Mathf.FloorToInt(visibleCharacters),
                    totalCharacters);

            yield return null;
        }

        bodyText.maxVisibleCharacters = int.MaxValue;

        isTyping = false;
        typingCoroutine = null;
    }

    private void CompleteTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        bodyText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
    }

    private void EndDialogue()
    {
        CompleteTyping();

        isPlaying = false;
        currentLine = null;

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(false);
        }

        onDialogueCompleted?.Invoke();
    }
}
