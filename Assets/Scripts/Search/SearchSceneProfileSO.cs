using System;
using System.Collections.Generic;
using UnityEngine;

// Optional data for another chapter using the Kayo search UI and acquisition flow.
[CreateAssetMenu(menuName = "Dialogue/Search Scene Profile", fileName = "SearchProfile")]
public sealed class SearchSceneProfileSO : ScriptableObject
{
    public string saveKey = "MoteruSearch.Acquired.v1";
    public string saveLabel = "モテル編の探索";
    [Tooltip("全探索と取得後会話が終わった後に再生する会話の先頭ID。空欄なら直接遷移します。")]
    public string completionFirstLineId;
    [Tooltip("この会話の終了後に、既存のフェードアウトとパズル遷移を開始します。")]
    public string completionLastLineId;
    public List<SearchItemSettings> items = new List<SearchItemSettings>();
}

[Serializable]
public sealed class SearchItemSettings
{
    public string id;
    public string archiveId;
    public string title;
    [Range(0, 1)] public int room;
    public Vector2 anchorMin;
    public Vector2 anchorMax;
    [Tooltip("背景に描かれている対象を使い、画像を重ねて配置しません。取得画面にはspriteを使います。")]
    public bool useBackgroundArt;
    [Tooltip("背景全体に対するクリック・強調範囲。空欄の場合はanchorMin/Maxの矩形です。")]
    public Vector2[] hotspotPolygon = new Vector2[0];
    public Sprite sprite;
    public Sprite secondarySprite;
    [Tooltip("専用画像が未配置の場合に紙などへ表示する仮の文字。画像設定後は空欄にします。")]
    public string placeholderText;
    public Rect uvRect = new Rect(0f, 0f, 1f, 1f);
    public string firstLineId;
    public string lastLineId;
    public string requiresItemId;
    public bool hideImageWhenAcquired;
    [Tooltip("取得後の調査画面・会話中も、背景側の配置画像を非表示にします。")]
    public bool hideImageDuringAcquisitionDialogue;
}
