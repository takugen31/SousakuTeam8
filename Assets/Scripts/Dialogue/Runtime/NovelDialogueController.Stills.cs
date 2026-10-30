using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class NovelDialogueController
{
    private GameObject storyStillView;
    private string activeStoryStillResource;

    // Docs/Scenario/プロローグ.txt: Chapter 2 [スチル] ... [スチル終].
    private static string StoryStillResource(string lineId)
    {
        if (InStoryStillRange(lineId, "chapter2_yowashi_", 12, 14)) return "StoryStills/YowashiPrologue";
        if (InStoryStillRange(lineId, "chapter2_kayo_", 12, 21)) return "StoryStills/KayoPrologue";
        if (InStoryStillRange(lineId, "chapter2_moteru_", 13, 18)) return "StoryStills/MoteruPrologue";
        return null;
    }

    private static bool InStoryStillRange(string lineId, string prefix, int first, int last)
    {
        return lineId != null && lineId.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(lineId.Substring(prefix.Length), out int row) && row >= first && row <= last;
    }

    private void ApplyStoryStill(DialogueLine line, bool movementCompleted = false)
    {
        if (!movementCompleted && !restoringCheckpointLine && line?.lineId == "chapter2_kayo_012" &&
            isKayoEggViewActive && storyStillView == null) return;
        if (!movementCompleted && !restoringCheckpointLine && line?.lineId == "chapter2_moteru_013" &&
            isMoteruRevealViewActive && moteruApproachElapsed < 2.4f && storyStillView == null) return;
        string resource = StoryStillResource(line?.lineId);
        if (resource == null || dialogueRoot == null) return;
        if (storyStillView != null && activeStoryStillResource == resource)
        {
            foreach (GameObject view in new[] { yowashiBandageView, moteruRevealView, kayoEggView })
                if (view != null && view.transform.parent == storyStillView.transform.parent &&
                    view.transform.GetSiblingIndex() > storyStillView.transform.GetSiblingIndex())
                    storyStillView.transform.SetSiblingIndex(view.transform.GetSiblingIndex());
            return;
        }
        Texture2D texture = Resources.Load<Texture2D>(resource);
        if (texture == null) return;
        StopStoryStill();

        // Prepare while inactive; keep the artwork below dialogue and above both portraits.
        storyStillView = new GameObject("StoryStill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        storyStillView.SetActive(false);
        RectTransform frame = storyStillView.GetComponent<RectTransform>();
        frame.SetParent(dialogueRoot.transform, false);
        int artIndex = 0;
        foreach (Image image in new[] { backgroundImage, leftPortraitImage, rightPortraitImage })
            if (image != null && image.transform.parent == frame.parent)
                artIndex = Mathf.Max(artIndex, image.transform.GetSiblingIndex() + 1);
        foreach (GameObject view in new[] { yowashiBandageView, moteruRevealView, kayoEggView })
            if (view != null && view.transform.parent == frame.parent)
                artIndex = Mathf.Max(artIndex, view.transform.GetSiblingIndex() + 1);
        frame.SetSiblingIndex(artIndex);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        Image matte = storyStillView.GetComponent<Image>();
        matte.color = Color.black;
        matte.raycastTarget = false;

        var artwork = new GameObject("Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
        artwork.transform.SetParent(frame, false);
        RawImage raw = artwork.GetComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
        var fitter = artwork.GetComponent<AspectRatioFitter>();
        fitter.aspectRatio = (float)texture.width / texture.height;
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        activeStoryStillResource = resource;
        storyStillView.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(frame);
    }

    private bool TryPlayMovementBeforeStill(string text)
    {
        if (restoringCheckpointLine || storyStillView != null) return false;
        if (currentLine?.lineId == "chapter2_moteru_013" && isMoteruRevealViewActive && moteruApproachElapsed < 2.4f)
        {
            roomLookCoroutine = StartCoroutine(PlayApproachThenStill(currentLine, text));
            return true;
        }
        if (currentLine?.lineId != "chapter2_kayo_012" || !isKayoEggViewActive) return false;
        roomLookCoroutine = StartCoroutine(PlayEggThenStill(currentLine, text));
        return true;
    }

    private IEnumerator PlayApproachThenStill(DialogueLine line, string text)
    {
        StopTypingAndRevealText();
        autoAdvanceAt = -1f;
        isRoomLookPlaying = true;
        if (bodyText != null) { bodyText.text = string.Empty; bodyText.maxVisibleCharacters = 0; }
        // Fast page advances must not cut off the existing lips-to-ear movement.
        while (moteruApproachElapsed < 2.4f && moteruRevealPortrait != null)
        {
            if (!ArchiveManager.IsOpen && !isChapterTransitioning && !isSkipConfirmationOpen)
            {
                moteruApproachElapsed = Mathf.Min(2.4f, moteruApproachElapsed + Time.unscaledDeltaTime);
                ApplyMoteruApproach();
            }
            yield return null;
        }
        if (isPlaying && currentLine == line) ApplyStoryStill(line, true);
        isRoomLookPlaying = false;
        roomLookCoroutine = null;
        if (isPlaying && currentLine == line) StartTyping(text);
    }

    private IEnumerator PlayEggThenStill(DialogueLine line, string text)
    {
        StopTypingAndRevealText();
        autoAdvanceAt = -1f;
        isRoomLookPlaying = true;
        if (bodyText != null) { bodyText.text = string.Empty; bodyText.maxVisibleCharacters = 0; }
        // Preserve the egg's original diagonal entrance, floating pose and exit.
        while (kayoEggSlideElapsed < 1.6f && kayoEggRect != null)
        {
            if (!ArchiveManager.IsOpen && !isChapterTransitioning && !isSkipConfirmationOpen)
            {
                kayoEggSlideElapsed += Time.unscaledDeltaTime;
                ApplyKayoEggSlide();
            }
            yield return null;
        }
        Vector2 start = kayoEggRect != null ? kayoEggRect.anchoredPosition : Vector2.zero;
        float elapsed = 0f;
        while (elapsed < 1.2f && kayoEggRect != null)
        {
            if (!ArchiveManager.IsOpen && !isChapterTransitioning && !isSkipConfirmationOpen)
            {
                elapsed += Time.unscaledDeltaTime;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / 1.2f), 3f);
                kayoEggRect.anchoredPosition = Vector2.Lerp(start, -kayoEggSlideStart, eased);
            }
            yield return null;
        }
        StopKayoEggView();
        if (isPlaying && currentLine == line) ApplyStoryStill(line, true);
        isRoomLookPlaying = false;
        roomLookCoroutine = null;
        if (isPlaying && currentLine == line) StartTyping(text);
    }

    private void StopStoryStill()
    {
        if (storyStillView != null)
        {
            storyStillView.SetActive(false);
            storyStillView.transform.SetParent(null, false);
            Destroy(storyStillView);
        }
        storyStillView = null;
        activeStoryStillResource = null;
    }
}
