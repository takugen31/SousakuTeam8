# アーカイブシステム

## プレイヤー操作

- どのシーンでも `B` キーで情報アーカイブを開閉できます。
- `ESC` キーでも閉じられます。
- カテゴリ、検索欄、一覧から獲得済み情報を絞り込めます。
- 新しく獲得した項目には `NEW` が表示され、詳細を開くと既読になります。

獲得状態と既読状態は `PlayerPrefs` の `ArchiveState.v1` に保存され、シーン遷移後やゲーム再起動後も維持されます。

## 項目を追加する

`Assets/Resources/Archive/ArchiveDatabase.asset` の `Entries` に項目を追加します。

| 項目 | 内容 |
| --- | --- |
| Id | セーブデータと解除処理で使う一意のID。公開後は変更しない |
| Category | 人物、場所、手がかり、記録、ガイド |
| Title / Subtitle / Body | 一覧と詳細に表示する文章 |
| Icon | 任意の詳細画像 |
| Sort Order | 小さい項目から表示 |
| Unlocked At Start | 最初から獲得済みにする |
| Show Before Unlock | 未獲得時も「？？？？？？」として一覧に出す |

## ゲーム中に情報を獲得させる

コードからは、項目IDを指定します。同じIDを複数回解除しても初回だけ通知されます。

```csharp
ArchiveManager.Unlock("clue.old_letter");
```

シーン上のGameObjectから設定する場合は `ArchiveUnlockTrigger` を追加します。`Entry Id` と解除タイミングを設定するか、Buttonやイベントから公開メソッド `Unlock()` を呼びます。

会話CSVから解除する場合は、任意列 `archiveUnlockIds` を追加します。1つのセリフで複数解除するときはセミコロンで区切ります。

```csv
lineId,speakerId,text,nextLineId,archiveUnlockIds
chapter1_010,kayo,古い手紙を見つけた。,chapter1_011,clue.old_letter
chapter1_020,kayo,二人のことが分かった。,chapter1_021,person.kayo;person.yowashi
```

CSVにこの列がない既存シナリオは、そのままインポートできます。

## 実装上の動作

- `ArchiveManager` はゲーム開始前に自動生成され、`DontDestroyOnLoad` で常駐します。各シーンへのPrefab配置は不要です。
- アーカイブ表示中は `Time.timeScale` を一時停止し、閉じたときに元の値へ戻します。
- 会話画面はアーカイブ表示中の入力と自動送りを停止します。
- シーンにEventSystemがない場合だけ一時的なEventSystemを生成します。シーン側にある場合は既存設定を利用します。
