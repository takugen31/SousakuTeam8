using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adapts dialogue playback to the scene progress store; no manual-slot operations.</summary>
public sealed partial class NovelDialogueController
{
    [Header("Scene Progress")]
    [SerializeField, Tooltip("会話進行の識別子。空欄ならHierarchyパスを使用。動的生成時は同じ名前・階層にしてください。")]
    private string progressId;

    private string progressKey;
    private int progressGeneration;
    private bool progressCompleted;
    private bool progressDeparted;
    private bool restoringSceneProgress;
    private string progressResumeLineId;
    private DialogueProgressState lastProgressState;
    private readonly HashSet<string> progressAppliedLineIds = new HashSet<string>(StringComparer.Ordinal);

    private void BeginProgressSession(bool embedded = false)
    {
        string controllerId = progressId;
        if (string.IsNullOrEmpty(controllerId))
        {
            var parts = new List<string>();
            for (Transform node = transform; node != null; node = node.parent)
            {
                int ordinal = 0;
                if (node.parent != null)
                {
                    for (int i = 0; i < node.GetSiblingIndex(); i++)
                        if (node.parent.GetChild(i).name == node.name) ordinal++;
                }
                else
                {
                    foreach (var root in gameObject.scene.GetRootGameObjects())
                    {
                        if (root.transform == node) break;
                        if (root.name == node.name) ordinal++;
                    }
                }
                parts.Add(node.name + "[" + ordinal + "]");
            }
            parts.Reverse();
            controllerId = string.Join("/", parts);
        }
        string sceneId = string.IsNullOrEmpty(gameObject.scene.path) ? gameObject.scene.name : gameObject.scene.path;
        progressKey = sceneId + "|" + controllerId + (embedded
            ? "|" + scenario?.name + "|" + startLineId + "|" + stopAfterLineId : "|scene");
        progressGeneration = DialogueProgressStore.Generation;
        progressCompleted = progressDeparted = false;
        progressResumeLineId = null;
        lastProgressState = null;
        progressAppliedLineIds.Clear();
        if (DialogueProgressStore.TryGet(progressKey, out var previous))
        {
            progressAppliedLineIds.UnionWith(previous.appliedLineIds);
            revealedCharacterIds.UnionWith(previous.revealedNames);
            var gauge = GetComponent<KayoGaugeOutcomeController>();
            if (gauge != null) gauge.SetResult(previous.kayoGaugeSucceeded);
        }
    }

    private void OnProgressSettingChanged()
    {
        progressGeneration = DialogueProgressStore.Generation;
        progressCompleted = progressDeparted = false;
        progressResumeLineId = null;
        lastProgressState = null;
        progressAppliedLineIds.Clear();
        // Enabling in the middle of a line must not apply its effects again on return.
        if (currentLine != null && currentScenario != null)
            progressAppliedLineIds.Add(currentScenario.name + "|" + currentLine.lineId);
    }

    private bool ShouldApplyProgressLine(DialogueLine line)
    {
        return !DialogueProgressStore.Enabled || line == null || currentScenario == null ||
            progressAppliedLineIds.Add(currentScenario.name + "|" + line.lineId);
    }

    private bool TryGetProgressEntryMusic(out DialogueScenarioSO entryScenario, out DialogueLine entryLine)
    {
        entryScenario = null;
        entryLine = null;
        // Music is queried before Start(), so build the same stable key without consuming data.
        string previousKey = progressKey;
        if (progressKey == null) BeginProgressSession();
        bool found = DialogueProgressStore.TryGet(progressKey, out var state) &&
            TryFindProgressLine(state.departed ? state.resumeLineId : state.lineId,
                state.departed ? null : state.scenario, out entryScenario, out entryLine, out _);
        if (found) return true;
        entryScenario = null;
        entryLine = null;
        progressKey = previousKey;
        return false;
    }

    private bool TryRestoreSceneProgress()
    {
        if (!DialogueProgressStore.TryGet(progressKey, out var state)) return false;
        KayoGaugeOutcomeController gauge = GetComponent<KayoGaugeOutcomeController>();
        if (gauge != null) gauge.SetResult(state.kayoGaugeSucceeded);
        string lineId = state.departed ? state.resumeLineId : state.lineId;
        // A departed final row without a successor is a completed conversation.
        bool completed = state.completed || (state.departed && string.IsNullOrEmpty(lineId));
        if (completed) lineId = state.lineId;
        if (!TryFindProgressLine(lineId, state.departed && !completed ? null : state.scenario,
            out var restoredScenario, out var line, out int index))
        {
            Debug.LogWarning("保存された会話プログレスの行が見つからないため、通常の開始位置を使用します。", this);
            progressAppliedLineIds.Clear();
            return false;
        }

        CancelChapterTransition();
        HideChoices();
        currentScenario = restoredScenario;
        currentScenarioIndex = index;
        stopAfterLineId = state.stopLineId;
        revealedCharacterIds.UnionWith(state.revealedNames);
        ResetKnownCharacterNames();
        shownConsultationLineIds.Clear();
        shownConsultationLineIds.UnionWith(state.consultationLines);
        autoPlayEnabled = false;
        UpdateAutoPlayButton();
        isPlaying = true;
        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        ResetBackgroundForScenario();
        ResetPortraitsForScenario();

        restoringSceneProgress = true;
        try
        {
            // A new chapter starts from its own defaults; within a chapter keep inherited art.
            if (!state.departed || state.scenario == restoredScenario.name)
                RestoreProgressImages(state.images);
            if (state.departed && !completed)
            {
                ShowLine(line);
            }
            else
            {
                restoringCheckpointLine = true;
                try
                {
                    PrepareLine(line);
                    StartTyping(GetDialogueDisplayText(line, omitOuterDialogueQuotes));
                    RestoreProgressImages(state.images);
                    if (state.textFullyDisplayed || completed) CompleteTyping();
                    else if (bodyText != null) bodyText.maxVisibleCharacters = state.visibleCharacters;
                }
                finally { restoringCheckpointLine = false; }
            }
            if (completed)
            {
                progressCompleted = true;
                lastProgressState = state;
                EndDialogueImmediately();
            }
        }
        finally { restoringSceneProgress = false; }
        return true;
    }

    private bool TryFindProgressLine(string lineId, string scenarioName,
        out DialogueScenarioSO foundScenario, out DialogueLine line, out int index)
    {
        int count = 1 + (followingScenarios?.Count ?? 0);
        for (int i = 0; i < count; i++)
        {
            var candidate = i == 0 ? scenario : followingScenarios[i - 1];
            if (candidate != null && (scenarioName == null || candidate.name == scenarioName) &&
                !string.IsNullOrEmpty(lineId) && candidate.TryGetLine(lineId, out line))
            { foundScenario = candidate; index = i; return true; }
        }
        foundScenario = null;
        line = null;
        index = -1;
        return false;
    }

    private string FindProgressSuccessor(DialogueLine transition)
    {
        if (!string.IsNullOrWhiteSpace(transition.nextLineId)) return transition.nextLineId;
        if (!TryFindProgressLine(transition.lineId, null, out var owner, out _, out int index)) return null;
        if (owner.TryGetNextLine(transition.lineId, out var next)) return next.lineId;
        for (int i = index + 1; i <= (followingScenarios?.Count ?? 0); i++)
            if (followingScenarios[i - 1] != null && followingScenarios[i - 1].TryGetFirstLine(out next))
                return next.lineId;
        return null;
    }

    // No scene whitelist: every NovelDialogueController, including embedded conversations, participates.
    private void LateUpdate()
    {
        if (!DialogueProgressStore.Enabled || progressDeparted || progressCompleted || restoringSceneProgress) return;
        RefreshProgressState();
    }

    private void RefreshProgressState()
    {
        if (string.IsNullOrEmpty(progressKey) || progressGeneration != DialogueProgressStore.Generation ||
            currentLine == null || currentScenario == null || bodyText == null || progressDeparted || progressCompleted ||
            isRoomLookPlaying || isChapterTransitioning) return;
        var gauge = GetComponent<KayoGaugeOutcomeController>();
        lastProgressState = new DialogueProgressState
        {
            key = progressKey, scenario = currentScenario.name, lineId = currentLine.lineId,
            stopLineId = stopAfterLineId, textFullyDisplayed = !isTyping,
            visibleCharacters = bodyText != null ? Math.Max(0, bodyText.maxVisibleCharacters) : 0,
            revealedNames = CaptureRevealedNames(), consultationLines = new List<string>(shownConsultationLineIds).ToArray(),
            appliedLineIds = new List<string>(progressAppliedLineIds).ToArray(),
            kayoGaugeSucceeded = gauge != null && gauge.CurrentResult,
            images = new[] { CaptureProgressImage(backgroundImage), CaptureProgressImage(leftPortraitImage), CaptureProgressImage(rightPortraitImage) }
        };
    }

    private void SaveSceneProgress()
    {
        if (!DialogueProgressStore.Enabled || restoringSceneProgress) return;
        RefreshProgressState();
        if (lastProgressState == null) return;
        lastProgressState.completed = progressCompleted;
        lastProgressState.departed = progressDeparted;
        lastProgressState.resumeLineId = progressResumeLineId;
        DialogueProgressStore.Save(lastProgressState, progressGeneration);
    }

    private void SaveSceneDeparture(DialogueLine transition)
    {
        RefreshProgressState();
        if (lastProgressState == null) return;
        // During chapter skip the transition may be in a later scenario, not the displayed one.
        if (TryFindProgressLine(transition.lineId, null, out var owner, out _, out _))
        {
            lastProgressState.scenario = owner.name;
            lastProgressState.lineId = transition.lineId;
        }
        progressResumeLineId = FindProgressSuccessor(transition);
        progressDeparted = true;
        lastProgressState.textFullyDisplayed = true;
        SaveSceneProgress();
    }

    private void SaveSceneCompletion()
    {
        if (restoringSceneProgress || progressCompleted) return;
        RefreshProgressState();
        progressCompleted = true;
        SaveSceneProgress();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveSceneProgress();
    }

    private void OnApplicationQuit() => SaveSceneProgress();

    private static DialogueProgressImage CaptureProgressImage(Image image)
    {
        if (image == null) return new DialogueProgressImage();
        var rect = image.rectTransform;
        return new DialogueProgressImage
        {
            sprite = image.sprite != null ? image.sprite.name : null,
            texture = image.sprite != null ? image.sprite.texture.name : null,
            rect = image.sprite != null ? image.sprite.rect : default,
            enabled = image.enabled, color = image.color,
            anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot,
            size = rect.sizeDelta, position = rect.anchoredPosition,
            scale = rect.localScale, rotation = rect.localRotation
        };
    }

    private void RestoreProgressImages(DialogueProgressImage[] images)
    {
        RestoreProgressImage(backgroundImage, images[0]);
        RestoreProgressImage(leftPortraitImage, images[1]);
        RestoreProgressImage(rightPortraitImage, images[2]);
    }

    private static void RestoreProgressImage(Image image, DialogueProgressImage state)
    {
        if (image == null) return;
        Sprite sprite = null;
        if (!string.IsNullOrEmpty(state.sprite))
            foreach (var candidate in Resources.FindObjectsOfTypeAll<Sprite>())
                if (candidate.name == state.sprite && candidate.texture.name == state.texture && candidate.rect == state.rect)
                { sprite = candidate; break; }
        image.sprite = sprite;
        image.enabled = state.enabled && sprite != null;
        image.color = state.color;
        var rect = image.rectTransform;
        rect.anchorMin = state.anchorMin; rect.anchorMax = state.anchorMax; rect.pivot = state.pivot;
        rect.sizeDelta = state.size; rect.anchoredPosition = state.position;
        rect.localScale = state.scale; rect.localRotation = state.rotation;
    }
}
