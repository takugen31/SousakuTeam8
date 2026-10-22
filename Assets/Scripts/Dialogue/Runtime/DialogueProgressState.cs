using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local playback history, independent of the six manual save slots.</summary>
[Serializable]
public sealed class DialogueProgressState
{
    public string key;
    public string scenario;
    public string lineId;
    public string stopLineId;
    public bool textFullyDisplayed;
    public int visibleCharacters;
    public bool completed;
    public bool departed;
    public string resumeLineId;
    public string[] revealedNames;
    public string[] consultationLines;
    public string[] appliedLineIds;
    public bool kayoGaugeSucceeded;
    public DialogueProgressImage[] images;
}

[Serializable]
public sealed class DialogueProgressImage
{
    public string sprite;
    public string texture;
    public Rect rect;
    public bool enabled;
    public Color color;
    public Vector2 anchorMin, anchorMax, pivot, size, position;
    public Vector3 scale;
    public Quaternion rotation;
}

[Serializable]
public sealed class DialogueProgressArchive
{
    public int version = 1;
    public List<DialogueProgressState> entries = new List<DialogueProgressState>();
}
