using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
    [Tooltip("名前がまだ公開されていないキャラクターの表示名です。")]
    private string unknownSpeakerName = "???";

    [SerializeField]
    private string startLineId;

    [Header("Affection")]
    [SerializeField]
    private AffectionManager affectionManager;

    [Header("Background")]
    [SerializeField]
    private Image backgroundImage;

    [Header("Chapter Transition")]
    [SerializeField]
    [Tooltip("チャプター切替時の暗転演出を有効にします。")]
    private bool useChapterTransitionFade = true;

    [SerializeField]
    [Min(0f)]
    [Tooltip("暗転するまでの秒数です。")]
    private float chapterFadeOutDuration = 1f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("暗転したまま待機する秒数です。")]
    private float chapterFadeHoldDuration = 1.7f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("暗転から画面を表示するまでの秒数です。")]
    private float chapterFadeInDuration = 1f;

    [SerializeField]
    [Tooltip("チャプター切替時に画面を覆う色です。")]
    private Color chapterFadeColor = Color.black;

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

    [Header("Choices")]
    [SerializeField]
    private RectTransform choiceOptionsRoot;

    [SerializeField]
    private Button choiceButtonTemplate;

    [SerializeField]
    [Min(1f)]
    private float choiceButtonHeight = 64f;

    [SerializeField]
    [Min(0f)]
    private float choiceButtonSpacing = 12f;

    [Header("Auto Play")]
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
    private Coroutine chapterTransitionCoroutine;
    private Coroutine choiceActivationCoroutine;
    private Coroutine typingCoroutine;
    private Image chapterTransitionOverlay;

    private readonly List<Button> spawnedChoiceButtons =
        new List<Button>();

    private readonly HashSet<string> knownCharacterIds =
        new HashSet<string>(System.StringComparer.Ordinal);

    private int currentScenarioIndex = -1;
    private float autoAdvanceAt = -1f;
    private float autoAdvanceRemainingBeforeConfirmation = -1f;
    private float timeScaleBeforeConfirmation = 1f;
    private bool autoPlayEnabled;
    private bool isChapterTransitioning;
    private bool isChoiceSelectionOpen;
    private bool isPlaying;
    private bool isSceneLoading;
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

        if (choiceButtonTemplate != null)
        {
            choiceButtonTemplate.gameObject.SetActive(false);
        }

        if (choiceOptionsRoot != null)
        {
            choiceOptionsRoot.gameObject.SetActive(false);
        }

        autoPlayEnabled = autoPlayOnStart;
        UpdateAutoPlayButton();
    }

    private void Update()
    {
        if (ArchiveManager.IsOpen ||
            isChapterTransitioning ||
            isSceneLoading ||
            isSkipConfirmationOpen ||
            isChoiceSelectionOpen)
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
        CancelChapterTransition();
        HideChoices();

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

        ResetKnownCharacterNames();

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

        ResetBackgroundForScenario();
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
            currentScenario == null ||
            isChapterTransitioning ||
            isSceneLoading ||
            isChoiceSelectionOpen)
        {
            return;
        }

        // タイプ表示中の入力では、現在のセリフを全文表示する
        if (isTyping)
        {
            CompleteTyping();
            return;
        }

        if (currentLine.HasSceneTransition)
        {
            LoadDialogueScene(currentLine.nextScenePath);
            return;
        }

        if (!TryResolveNextLine(
                out DialogueLine nextLine,
                out bool reachedScenarioEnd))
        {
            if (!reachedScenarioEnd || !TryStartNextScenario())
            {
                EndDialogue();
            }

            return;
        }

        ShowLine(nextLine);
    }

    private bool TryResolveNextLine(
        out DialogueLine nextLine,
        out bool reachedScenarioEnd)
    {
        nextLine = null;
        reachedScenarioEnd = false;

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

                    if (!currentScenario.TryGetLine(
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
            if (!currentScenario.TryGetLine(
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
        if (currentScenario.TryGetNextLine(
                currentLine.lineId,
                out nextLine))
        {
            return true;
        }

        reachedScenarioEnd = true;
        return false;
    }

    private AffectionManager ResolveAffectionManager()
    {
        if (affectionManager != null)
        {
            return affectionManager;
        }

        return AffectionManager.Instance;
    }

    public void ToggleAutoPlay()
    {
        autoPlayEnabled = !autoPlayEnabled;
        UpdateAutoPlayButton();

        if (autoPlayEnabled &&
            isPlaying &&
            currentLine != null &&
            !isTyping &&
            !isChoiceSelectionOpen)
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
            isChapterTransitioning ||
            isSceneLoading ||
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
        if (!isPlaying ||
            currentScenario == null ||
            isChapterTransitioning ||
            isSceneLoading)
        {
            return;
        }

        if (isSkipConfirmationOpen)
        {
            CloseSkipConfirmation(false);
        }

        autoAdvanceAt = -1f;

        if (TryLoadUpcomingSceneTransition())
        {
            return;
        }

        if (!TryStartNextScenario())
        {
            EndDialogue();
        }
    }

    private void OnDestroy()
    {
        CancelChapterTransition();

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
        if (!TryFindNextScenario(
                out DialogueScenarioSO nextScenario,
                out DialogueLine firstLine,
                out int nextScenarioIndex))
        {
            return false;
        }

        StopTypingAndRevealText();
        HideChoices();
        autoAdvanceAt = -1f;

        if (!useChapterTransitionFade)
        {
            StartScenario(
                nextScenario,
                firstLine,
                nextScenarioIndex);
            return true;
        }

        Image overlay = EnsureChapterTransitionOverlay();

        if (overlay == null)
        {
            Debug.LogWarning(
                "チャプター切替用のCanvasが見つからないため、" +
                "フェードせずに次のチャプターを開始します。");

            StartScenario(
                nextScenario,
                firstLine,
                nextScenarioIndex);
            return true;
        }

        isChapterTransitioning = true;
        overlay.transform.SetAsLastSibling();
        overlay.gameObject.SetActive(true);
        SetChapterTransitionAlpha(0f);

        chapterTransitionCoroutine = StartCoroutine(
            TransitionToScenario(
                nextScenario,
                firstLine,
                nextScenarioIndex));

        return true;
    }

    private bool TryFindNextScenario(
        out DialogueScenarioSO nextScenario,
        out DialogueLine firstLine,
        out int nextScenarioIndex)
    {
        nextScenario = null;
        firstLine = null;
        nextScenarioIndex = -1;

        int scenarioCount = 1 +
            (followingScenarios?.Count ?? 0);

        for (int nextIndex = currentScenarioIndex + 1;
             nextIndex < scenarioCount;
             nextIndex++)
        {
            DialogueScenarioSO candidateScenario =
                followingScenarios[nextIndex - 1];

            if (candidateScenario == null)
            {
                Debug.LogWarning(
                    $"Dialogueの{nextIndex + 1}番目のシナリオが" +
                    "設定されていないためスキップします。");
                continue;
            }

            if (!candidateScenario.TryGetFirstLine(
                    out DialogueLine candidateFirstLine))
            {
                Debug.LogWarning(
                    $"シナリオ「{candidateScenario.name}」にセリフがないため" +
                    "スキップします。");
                continue;
            }

            nextScenario = candidateScenario;
            firstLine = candidateFirstLine;
            nextScenarioIndex = nextIndex;
            return true;
        }

        return false;
    }

    private IEnumerator TransitionToScenario(
        DialogueScenarioSO nextScenario,
        DialogueLine firstLine,
        int nextScenarioIndex)
    {
        yield return FadeChapterTransition(
            0f,
            1f,
            chapterFadeOutDuration);

        StartScenario(
            nextScenario,
            firstLine,
            nextScenarioIndex);

        if (chapterFadeHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                chapterFadeHoldDuration);
        }

        yield return FadeChapterTransition(
            1f,
            0f,
            chapterFadeInDuration);

        if (chapterTransitionOverlay != null)
        {
            chapterTransitionOverlay.gameObject.SetActive(false);
        }

        isChapterTransitioning = false;
        chapterTransitionCoroutine = null;
    }

    private void StartScenario(
        DialogueScenarioSO nextScenario,
        DialogueLine firstLine,
        int nextScenarioIndex)
    {
        currentScenario = nextScenario;
        currentScenarioIndex = nextScenarioIndex;
        ResetBackgroundForScenario();
        ResetPortraitsForScenario();
        ShowLine(firstLine);
    }

    private IEnumerator FadeChapterTransition(
        float startAlpha,
        float endAlpha,
        float duration)
    {
        SetChapterTransitionAlpha(startAlpha);

        if (duration <= 0f)
        {
            SetChapterTransitionAlpha(endAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            SetChapterTransitionAlpha(
                Mathf.Lerp(
                    startAlpha,
                    endAlpha,
                    Mathf.Clamp01(elapsed / duration)));

            yield return null;
        }

        SetChapterTransitionAlpha(endAlpha);
    }

    private Image EnsureChapterTransitionOverlay()
    {
        if (chapterTransitionOverlay != null)
        {
            return chapterTransitionOverlay;
        }

        Canvas canvas =
            backgroundImage != null
                ? backgroundImage.canvas
                : GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            return null;
        }

        GameObject overlayObject = new GameObject(
            "ChapterTransitionFadeOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform overlayRect =
            overlayObject.GetComponent<RectTransform>();

        overlayRect.SetParent(canvas.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        chapterTransitionOverlay =
            overlayObject.GetComponent<Image>();
        chapterTransitionOverlay.raycastTarget = true;

        SetChapterTransitionAlpha(0f);
        overlayObject.SetActive(false);

        return chapterTransitionOverlay;
    }

    private void SetChapterTransitionAlpha(float alpha)
    {
        if (chapterTransitionOverlay == null)
        {
            return;
        }

        Color color = chapterFadeColor;
        color.a *= Mathf.Clamp01(alpha);
        chapterTransitionOverlay.color = color;
    }

    private void CancelChapterTransition()
    {
        if (chapterTransitionCoroutine != null)
        {
            StopCoroutine(chapterTransitionCoroutine);
            chapterTransitionCoroutine = null;
        }

        isChapterTransitioning = false;

        if (chapterTransitionOverlay == null)
        {
            return;
        }

        SetChapterTransitionAlpha(0f);
        chapterTransitionOverlay.gameObject.SetActive(false);
    }

    private void ShowLine(DialogueLine line)
    {
        HideChoices();

        currentLine = line;
        autoAdvanceAt = -1f;

        ApplyBackground(line);
        ApplyAffectionChanges(line);
        ApplyArchiveUnlocks(line);
        ApplyCharacter(line);
        StartTyping(line.text);
    }

    private void ResetBackgroundForScenario()
    {
        SetBackground(
            currentScenario != null
                ? currentScenario.DefaultBackground
                : null);
    }

    private void ApplyBackground(DialogueLine line)
    {
        if (line == null || line.background == null)
        {
            return;
        }

        SetBackground(line.background);
    }

    private void SetBackground(Sprite background)
    {
        if (backgroundImage == null)
        {
            return;
        }

        backgroundImage.sprite = background;
        backgroundImage.enabled = background != null;
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

    private static void ApplyArchiveUnlocks(DialogueLine line)
    {
        if (line == null || line.archiveUnlockIds == null)
        {
            return;
        }

        foreach (string entryId in line.archiveUnlockIds)
        {
            if (!string.IsNullOrWhiteSpace(entryId))
            {
                ArchiveManager.Unlock(entryId.Trim());
            }
        }
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
            speakerNameText.text =
                knownCharacterIds.Contains(line.speakerId)
                    ? character.displayName
                    : unknownSpeakerName;
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
            if (isChapterTransitioning || isSkipConfirmationOpen)
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
        HandleLineFullyDisplayed();
    }

    private void CompleteTyping()
    {
        StopTypingAndRevealText();
        HandleLineFullyDisplayed();
    }

    private void StopTypingAndRevealText()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        bodyText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
    }

    private void HandleLineFullyDisplayed()
    {
        RevealCurrentSpeakerName();

        if (currentLine != null && currentLine.HasSceneTransition)
        {
            // Scene遷移行はAUTOで先へ進めず、プレイヤーの入力を待つ
            autoAdvanceAt = -1f;
            return;
        }

        if (currentLine != null && currentLine.HasChoices)
        {
            ShowChoices(currentLine.choices);
            return;
        }

        ScheduleAutoAdvance();
    }

    private void ResetKnownCharacterNames()
    {
        knownCharacterIds.Clear();

        if (characterDatabase == null)
        {
            return;
        }

        foreach (CharacterData character in characterDatabase.Characters)
        {
            if (character == null ||
                !character.nameKnownInitially ||
                string.IsNullOrWhiteSpace(character.characterId))
            {
                continue;
            }

            knownCharacterIds.Add(character.characterId);
        }
    }

    private void RevealCurrentSpeakerName()
    {
        if (currentLine == null ||
            !currentLine.revealSpeakerName ||
            string.IsNullOrWhiteSpace(currentLine.speakerId))
        {
            return;
        }

        knownCharacterIds.Add(currentLine.speakerId);

        if (speakerNameText == null ||
            !characterDatabase.TryGetCharacter(
                currentLine.speakerId,
                out CharacterData character))
        {
            return;
        }

        speakerNameText.text = character.displayName;
        speakerNameText.color = character.nameColor;
    }

    private bool TryLoadUpcomingSceneTransition()
    {
        if (currentScenario != null &&
            currentLine != null &&
            currentScenario.TryGetSceneTransitionAtOrAfter(
                currentLine.lineId,
                out DialogueLine currentTransition))
        {
            LoadDialogueScene(currentTransition.nextScenePath);
            return true;
        }

        int scenarioCount = 1 +
            (followingScenarios?.Count ?? 0);

        for (int nextIndex = currentScenarioIndex + 1;
             nextIndex < scenarioCount;
             nextIndex++)
        {
            DialogueScenarioSO nextScenario =
                followingScenarios[nextIndex - 1];

            if (nextScenario == null ||
                !nextScenario.TryGetFirstSceneTransition(
                    out DialogueLine nextTransition))
            {
                continue;
            }

            LoadDialogueScene(nextTransition.nextScenePath);
            return true;
        }

        return false;
    }

    private void LoadDialogueScene(string scenePath)
    {
        if (isSceneLoading || string.IsNullOrWhiteSpace(scenePath))
        {
            return;
        }

        isSceneLoading = true;
        isPlaying = false;
        autoAdvanceAt = -1f;

        StopTypingAndRevealText();
        HideChoices();

        try
        {
            string runtimeScenePath = scenePath.EndsWith(
                    ".unity",
                    System.StringComparison.OrdinalIgnoreCase)
                ? scenePath.Substring(
                    0,
                    scenePath.Length - ".unity".Length)
                : scenePath;

            SceneManager.LoadScene(runtimeScenePath);
        }
        catch (System.Exception exception)
        {
            isSceneLoading = false;
            isPlaying = true;

            Debug.LogError(
                $"Unity Scene「{scenePath}」を読み込めませんでした。" +
                "Build ProfilesのScene Listへ追加されているか" +
                "確認してください。\n" +
                exception);
        }
    }

    private void ShowChoices(IReadOnlyList<DialogueChoice> choices)
    {
        if (choiceOptionsRoot == null || choiceButtonTemplate == null)
        {
            Debug.LogError(
                "選択肢UIが設定されていません。");
            EndDialogue();
            return;
        }

        autoAdvanceAt = -1f;
        isChoiceSelectionOpen = true;

        float totalHeight =
            choices.Count * choiceButtonHeight +
            Mathf.Max(0, choices.Count - 1) * choiceButtonSpacing;

        choiceOptionsRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            totalHeight);

        choiceOptionsRoot.gameObject.SetActive(true);

        for (int index = 0; index < choices.Count; index++)
        {
            DialogueChoice choice = choices[index];
            Button button = Instantiate(
                choiceButtonTemplate,
                choiceOptionsRoot);

            button.gameObject.name = $"ChoiceButton_{index + 1}";

            RectTransform buttonRect =
                (RectTransform)button.transform;

            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition =
                new Vector2(
                    0f,
                    -index *
                    (choiceButtonHeight + choiceButtonSpacing));
            buttonRect.sizeDelta =
                new Vector2(0f, choiceButtonHeight);

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = choice.text;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(
                () => SelectChoice(choice));
            button.interactable = false;
            button.gameObject.SetActive(true);

            spawnedChoiceButtons.Add(button);
        }

        choiceActivationCoroutine =
            StartCoroutine(EnableChoiceButtonsAfterInputRelease());
    }

    private IEnumerator EnableChoiceButtonsAfterInputRelease()
    {
        yield return null;

        while (Pointer.current?.press.isPressed == true)
        {
            yield return null;
        }

        foreach (Button button in spawnedChoiceButtons)
        {
            if (button != null)
            {
                button.interactable = true;
            }
        }

        choiceActivationCoroutine = null;
    }

    private void SelectChoice(DialogueChoice choice)
    {
        if (!isChoiceSelectionOpen ||
            choice == null ||
            currentScenario == null)
        {
            return;
        }

        HideChoices();

        if (!currentScenario.TryGetLine(
                choice.nextLineId,
                out DialogueLine nextLine))
        {
            Debug.LogError(
                $"選択肢「{choice.text}」の遷移先" +
                $"「{choice.nextLineId}」が存在しません。");
            EndDialogue();
            return;
        }

        ShowLine(nextLine);
    }

    private void HideChoices()
    {
        isChoiceSelectionOpen = false;

        if (choiceActivationCoroutine != null)
        {
            StopCoroutine(choiceActivationCoroutine);
            choiceActivationCoroutine = null;
        }

        foreach (Button button in spawnedChoiceButtons)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                Destroy(button.gameObject);
            }
        }

        spawnedChoiceButtons.Clear();

        if (choiceOptionsRoot != null)
        {
            choiceOptionsRoot.gameObject.SetActive(false);
        }
    }

    private bool ShouldAutoAdvance()
    {
        return autoPlayEnabled &&
            isPlaying &&
            !isTyping &&
            !isChoiceSelectionOpen &&
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
        autoAdvanceAt = autoPlayEnabled &&
            currentLine != null &&
            !currentLine.HasSceneTransition
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
        CancelChapterTransition();
        StopTypingAndRevealText();
        HideChoices();

        isPlaying = false;
        currentLine = null;
        currentScenario = null;
        currentScenarioIndex = -1;
        autoAdvanceAt = -1f;

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(false);
        }

        SetBackground(null);

        onDialogueCompleted?.Invoke();
    }
}
