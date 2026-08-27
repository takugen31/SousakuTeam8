# プロローグCSVのゲーム適用手順

最終更新: 2026-08-26

`prologue1.csv` と `prologue2.csv` は、ゲームへ適用できる形式になっています。ただし、現在の `NovelScene` は旧 `Prologue.asset` を参照しているため、`Prologue1.asset` と `Prologue2.asset` の作成・インポート・シーン登録が必要です。

## 1. シナリオアセットを作成する

UnityのProjectウィンドウで `Assets/Dialogue/Data` を開き、右クリックして以下を2回実行します。

```text
Create > Dialogue > Dialogue Scenario
```

作成するアセットは次の2つです。

```text
Assets/Dialogue/Data/Prologue1.asset
Assets/Dialogue/Data/Prologue2.asset
```

同じアセットへ2つのCSVをインポートしないでください。インポーターは出力先の全セリフを置き換えます。

## 2. prologue1.csvをインポートする

Unityメニューから以下を開きます。

```text
Tools > Dialogue > CSV Importer
```

次のように設定します。

| 項目 | 設定 |
| --- | --- |
| Character CSV | `Assets/Dialogue/CSV/Characters.csv` |
| Dialogue CSV | `Assets/Dialogue/CSV/Prologue/prologue1.csv` |
| Character Database | `Assets/Dialogue/Data/CharacterDatabase.asset` |
| Dialogue Scenario | 作成した `Prologue1.asset` |

`CSVをインポート` を押します。成功後、`Prologue1.asset` の `Lines` が5件になっていることを確認してください。

## 3. prologue2.csvをインポートする

同じ画面で以下の2項目を変更します。

| 項目 | 設定 |
| --- | --- |
| Dialogue CSV | `Assets/Dialogue/CSV/Prologue/prologue2.csv` |
| Dialogue Scenario | 作成した `Prologue2.asset` |

再びインポートします。`Lines` が32件になっていることを確認してください。

## 4. 背景を設定する

今回のCSVには `backgroundPath` が設定されていないため、何もしなければ背景は表示されません。

各アセットのInspectorで設定します。

```text
Prologue1.asset
  Default Background: ドウテの部屋

Prologue2.asset
  Default Background: 白い部屋
```

現状のプロジェクトには、この2場面に合う背景素材はまだありません。背景を用意した後、画像のImport Settingsを次のように設定してください。

```text
Texture Type: Sprite (2D and UI)
```

チャプター途中で背景を変更する場合は、CSVの `backgroundPath` に次のようなパスを記入して再インポートします。

```csv
Assets/Art/Backgrounds/example.png
```

`Default Background` はCSVを再インポートしても維持されます。

## 5. NovelSceneへ登録する

`Assets/Scenes/GameMap/NovelScene.unity` を開き、`Canvas/DialogueController` を選択します。

`Novel Dialogue Controller` を次のように設定します。

```text
Scenario: Prologue1
Following Scenarios:
  Size: 1
  Element 0: Prologue2
Start Line Id: 空欄
Play On Start: ON
```

現在は以下の状態なので変更が必要です。

```text
Scenario: 旧Prologue.asset
Following Scenarios: 空
```

設定後、シーンを保存してください。

## 6. Play Modeで確認する

期待される進行は次のとおりです。

```text
Prologue1の5セリフ
    ↓ 自動的に次チャプターへ
Prologue2
    ↓
プレイヤー選択肢
    ↓
返答・再選択ループ
    ↓
最後のセリフ
    ↓
会話終了
```

## プログラム査読結果

適用を妨げるコード上の不具合は見つかりませんでした。

注意点は以下です。

- インポーターは `Assets/Scripts/Dialogue/Editor/DialogueCsvImporterWindow.cs` の `dialogueScenario.ReplaceAll(dialogueLines)` により、出力先アセットを全置換します。
- チャプター間はCSVの `nextLineId` ではなく、`Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs` の `Following Scenarios` で接続します。両CSVの最終行は正しく空欄になっています。
- 選択肢行では通常の `nextLineId` を設定できません。現在の `prologue2.csv` はこの制約を満たしています。
- `CharacterDatabase.asset` もインポートごとに `Characters.csv` の内容で全置換されます。キャラクター設定はCSVを正として管理してください。
- 現在の `Assets/Dialogue/CSV/Characters.csv` では、`doute` の表示名が「ユウ」です。ゲーム内で「ドウテ」や「蓮愛ドウテ」と表示したい場合は、インポート前に `displayName` を変更してください。

基本仕様は `Docs/DialogueSystem.md` のCSV作成・インポート手順と一致しています。
