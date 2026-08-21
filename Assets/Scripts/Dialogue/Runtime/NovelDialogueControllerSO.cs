using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class NovelDialogueController : MonoBehaviour
{
    [Header("Dialogue Data")]
    [SerializeField]
    [Tooltip("最初に再生するシナリオです。通常はプロローグを登録します。")]
    private DialogueScenarioSO scenario;

    [SerializeField]
    [Tooltip(
        "プロローグの後に再生するシナリオを、" +
        "Chapter 1、Chapter 2の順に登録します。")]
    private List<DialogueScenarioSO> followingScenarios =
        new List<DialogueScenarioSO>();

    [SerializeField]
    private CharacterDatabaseSO characterDatabase;

    [SerializeField]
    [Tooltip("画面左側へ常時表示する主人公のcharacterIdです。")]
    private string protagonistCharacterId = "doute";

    [SerializeField]
    private string startLineId;

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
    [FormerlySerializedAs("portraitImage")]
    private Image leftPortraitImage;

    [SerializeField]
    private Image rightPortraitImage;

    [Header("Playback Controls")]
    [SerializeField]
    private RectTransform playbackControlsRoot;

    [SerializeField]
    private Button autoPlayButton;

    [SerializeField]
    private TMP_Text autoPlayButtonText;

    [SerializeField]
    private Button skipChapterButton;

    [SerializeField]
    private GameObject skipConfirmationRoot;

    [SerializeField]
    private Button confirmSkipButton;

    [SerializeField]
    private Button cancelSkipButton;

    [SerializeField]
    [Min(0f)]
    [Tooltip("タイプライター表示完了後、次のセリフへ進むまでの秒数です。")]
    private float autoAdvanceDelaySeconds = 0.5f;

    [SerializeField]
    private bool autoPlayOnStart;

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
    private DialogueScenarioSO currentScenario;
    private Coroutine typingCoroutine;

    private int currentScenarioIndex = -1;
    private float autoAdvanceAt = -1f;
    private float autoAdvanceRemainingBeforeConfirmation = -1f;
    private float timeScaleBeforeConfirmation = 1f;
    private bool autoPlayEnabled;
    private bool isPlaying;
    private bool isSkipConfirmationOpen;
    private bool isTyping;

    private void Awake()
    {
        if (autoPlayButton != null)
        {
            autoPlayButton.onClick.AddListener(ToggleAutoPlay);
        }

        if (skipChapterButton != null)
        {
            skipChapterButton.onClick.AddListener(ShowSkipConfirmation);
        }

        if (confirmSkipButton != null)
        {
            confirmSkipButton.onClick.AddListener(ConfirmSkip);
        }

        if (cancelSkipButton != null)
        {
            cancelSkipButton.onClick.AddListener(CancelSkip);
        }

        if (skipConfirmationRoot != null)
        {
            skipConfirmationRoot.SetActive(false);
        }

        autoPlayEnabled = autoPlayOnStart;
        UpdateAutoPlayButton();
    }

    private void Update()
    {
        if (isSkipConfirmationOpen)
        {
            return;
        }

        if (ShouldAutoAdvance())
        {
            autoAdvanceAt = -1f;
            Advance();
            return;
        }

        if (WasDialogueAdvancePressed())
        {
            Advance();
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartDialogue();
        }
    }

    public void StartDialogue()
    {
        if (scenario == null)
        {
            Debug.LogError("DialogueScenarioが設定されていません。");
            return;
        }

        if (characterDatabase == null)
        {
            Debug.LogError("CharacterDatabaseが設定されていません。");
            return;
        }

        currentScenario = scenario;
        currentScenarioIndex = 0;

        DialogueLine firstLine;

        if (!string.IsNullOrWhiteSpace(startLineId))
        {
            if (!currentScenario.TryGetLine(startLineId, out firstLine))
            {
                Debug.LogError(
                    $"開始セリフ「{startLineId}」が存在しません。");

                currentScenario = null;
                currentScenarioIndex = -1;
                return;
            }
        }
        else
        {
            if (!currentScenario.TryGetFirstLine(out firstLine))
            {
                Debug.LogError("シナリオにセリフがありません。");

                currentScenario = null;
                currentScenarioIndex = -1;
                return;
            }
        }

        isPlaying = true;

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(true);
        }

        ResetPortraitsForScenario();
        ShowLine(firstLine);
    }

    public void StartDialogueAt(string lineId)
    {
        startLineId = lineId;
        StartDialogue();
    }

    public void Advance()
    {
        if (!isPlaying ||
            currentLine == null ||
            currentScenario == null)
        {
            return;
        }

        // タイプ表示中の入力では、現在のセリフを全文表示する
        if (isTyping)
        {
            CompleteTyping();
            return;
        }

        DialogueLine nextLine;

        // nextLineIdが設定されていればそこへ移動
        if (!string.IsNullOrWhiteSpace(currentLine.nextLineId))
        {
            if (!currentScenario.TryGetLine(
                    currentLine.nextLineId,
                    out nextLine))
            {
                Debug.LogError(
                    $"次のセリフ「{currentLine.nextLineId}」が" +
                    "存在しません。");

                EndDialogue();
                return;
            }

            ShowLine(nextLine);
            return;
        }

        // nextLineIdが空ならCSV上の次の行へ進む
        if (currentScenario.TryGetNextLine(
                currentLine.lineId,
                out nextLine))
        {
            ShowLine(nextLine);
            return;
        }

        if (!TryStartNextScenario())
        {
            EndDialogue();
        }
    }

    public void ToggleAutoPlay()
    {
        autoPlayEnabled = !autoPlayEnabled;
        UpdateAutoPlayButton();

        if (autoPlayEnabled &&
            isPlaying &&
            currentLine != null &&
            !isTyping)
        {
            ScheduleAutoAdvance();
        }
        else if (!autoPlayEnabled)
        {
            autoAdvanceAt = -1f;
        }
    }

    public void ShowSkipConfirmation()
    {
        if (!isPlaying ||
            currentScenario == null ||
            isSkipConfirmationOpen)
        {
            return;
        }

        if (skipConfirmationRoot == null)
        {
            Debug.LogError(
                "スキップ確認画面が設定されていません。");
            return;
        }

        autoAdvanceRemainingBeforeConfirmation =
            autoAdvanceAt >= 0f
                ? Mathf.Max(0f, autoAdvanceAt - Time.unscaledTime)
                : -1f;
        autoAdvanceAt = -1f;
        timeScaleBeforeConfirmation = Time.timeScale;
        isSkipConfirmationOpen = true;

        skipConfirmationRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ConfirmSkip()
    {
        if (!isSkipConfirmationOpen)
        {
            return;
        }

        CloseSkipConfirmation(false);
        SkipToNextScenario();
    }

    public void CancelSkip()
    {
        if (!isSkipConfirmationOpen)
        {
            return;
        }

        CloseSkipConfirmation(true);
    }

    public void SkipToNextScenario()
    {
        if (!isPlaying || currentScenario == null)
        {
            return;
        }

        if (isSkipConfirmationOpen)
        {
            CloseSkipConfirmation(false);
        }

        autoAdvanceAt = -1f;

        if (!TryStartNextScenario())
        {
            EndDialogue();
        }
    }

    private void OnDestroy()
    {
        if (autoPlayButton != null)
        {
            autoPlayButton.onClick.RemoveListener(ToggleAutoPlay);
        }

        if (skipChapterButton != null)
        {
            skipChapterButton.onClick.RemoveListener(ShowSkipConfirmation);
        }

        if (confirmSkipButton != null)
        {
            confirmSkipButton.onClick.RemoveListener(ConfirmSkip);
        }

        if (cancelSkipButton != null)
        {
            cancelSkipButton.onClick.RemoveListener(CancelSkip);
        }

        if (isSkipConfirmationOpen)
        {
            Time.timeScale = timeScaleBeforeConfirmation;
        }
    }

    private bool TryStartNextScenario()
    {
        int scenarioCount = 1 +
            (followingScenarios?.Count ?? 0);

        for (int nextIndex = currentScenarioIndex + 1;
             nextIndex < scenarioCount;
             nextIndex++)
        {
            DialogueScenarioSO nextScenario =
                followingScenarios[nextIndex - 1];

            if (nextScenario == null)
            {
                Debug.LogWarning(
                    $"Dialogueの{nextIndex + 1}番目のシナリオが" +
                    "設定されていないためスキップします。");
                continue;
            }

            if (!nextScenario.TryGetFirstLine(
                    out DialogueLine firstLine))
            {
                Debug.LogWarning(
                    $"シナリオ「{nextScenario.name}」にセリフがないため" +
                    "スキップします。");
                continue;
            }

            currentScenario = nextScenario;
            currentScenarioIndex = nextIndex;
            ResetPortraitsForScenario();
            ShowLine(firstLine);
            return true;
        }

        return false;
    }

    private void ShowLine(DialogueLine line)
    {
        currentLine = line;
        autoAdvanceAt = -1f;

        ApplyCharacter(line);
        StartTyping(line.text);
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

        Sprite portrait =
            character.GetPortrait(line.expressionId);

        if (string.Equals(
                line.speakerId,
                protagonistCharacterId,
                System.StringComparison.Ordinal))
        {
            SetPortrait(leftPortraitImage, portrait);
        }
        else
        {
            SetPortrait(rightPortraitImage, portrait);
        }
    }

    private void ResetPortraitsForScenario()
    {
        SetPortrait(rightPortraitImage, null);

        if (string.IsNullOrWhiteSpace(protagonistCharacterId))
        {
            Debug.LogWarning(
                "主人公のcharacterIdが設定されていません。");
            SetPortrait(leftPortraitImage, null);
            return;
        }

        if (!characterDatabase.TryGetCharacter(
                protagonistCharacterId,
                out CharacterData protagonist))
        {
            Debug.LogWarning(
                $"主人公「{protagonistCharacterId}」が存在しません。");
            SetPortrait(leftPortraitImage, null);
            return;
        }

        SetPortrait(
            leftPortraitImage,
            protagonist.GetPortrait(null));
    }

    private static void SetPortrait(
        Image image,
        Sprite portrait)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = portrait;
        image.enabled = portrait != null;
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
            if (isSkipConfirmationOpen)
            {
                yield return null;
                continue;
            }

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
        ScheduleAutoAdvance();
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
        ScheduleAutoAdvance();
    }

    private bool ShouldAutoAdvance()
    {
        return autoPlayEnabled &&
            isPlaying &&
            !isTyping &&
            currentLine != null &&
            autoAdvanceAt >= 0f &&
            Time.unscaledTime >= autoAdvanceAt;
    }

    private bool WasDialogueAdvancePressed()
    {
        Pointer pointer = Pointer.current;

        if (pointer == null ||
            !pointer.press.wasPressedThisFrame)
        {
            return false;
        }

        if (playbackControlsRoot == null)
        {
            return true;
        }

        return !RectTransformUtility.RectangleContainsScreenPoint(
            playbackControlsRoot,
            pointer.position.ReadValue());
    }

    private void ScheduleAutoAdvance()
    {
        autoAdvanceAt = autoPlayEnabled
            ? Time.unscaledTime + autoAdvanceDelaySeconds
            : -1f;
    }

    private void UpdateAutoPlayButton()
    {
        if (autoPlayButtonText == null)
        {
            return;
        }

        autoPlayButtonText.text =
            autoPlayEnabled ? "AUTO: ON" : "AUTO: OFF";

        autoPlayButtonText.color =
            autoPlayEnabled
                ? new Color(0.45f, 1f, 0.55f)
                : Color.white;
    }

    private void CloseSkipConfirmation(bool resumeAutoAdvance)
    {
        if (skipConfirmationRoot != null)
        {
            skipConfirmationRoot.SetActive(false);
        }

        isSkipConfirmationOpen = false;
        Time.timeScale = timeScaleBeforeConfirmation;

        if (resumeAutoAdvance &&
            autoPlayEnabled &&
            isPlaying &&
            !isTyping &&
            autoAdvanceRemainingBeforeConfirmation >= 0f)
        {
            autoAdvanceAt =
                Time.unscaledTime +
                autoAdvanceRemainingBeforeConfirmation;
        }
        else
        {
            autoAdvanceAt = -1f;
        }

        autoAdvanceRemainingBeforeConfirmation = -1f;
    }

    private void EndDialogue()
    {
        CompleteTyping();

        isPlaying = false;
        currentLine = null;
        currentScenario = null;
        currentScenarioIndex = -1;
        autoAdvanceAt = -1f;

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(false);
        }

        onDialogueCompleted?.Invoke();
    }
}
