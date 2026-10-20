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

## モテル Chapter 2 の選択肢

`chapter_moteru_2.csv` の `chapter_moteru_2_008` で、次の順番に選択肢を表示します。対応する `Chapter_Moteru_2.asset` にも反映済みです。

| 選択肢 | 遷移先 |
| --- | --- |
| そうだな、色々と話してみるよっ！ | `009` の遷移設定を使い、黒くフェードアウトして `SelectionPart.unity` へ |
| モテルと話してるとなんだか懐かしい感じがして……もう少し話してもいいか？ | `010`「それは——」から会話を続ける |

IDは `001〜012` の連番です。以前の `009` のセリフは下の選択肢に移し、重複して表示しません。現在の `009` は本文が空のシーン遷移専用行です。選択肢の行の `nextLineId` は空欄にし、`choice1NextLineId` を `chapter_moteru_2_009`、`choice2NextLineId` を `chapter_moteru_2_010` に設定します。

選択先が「本文が空・選択肢なし・`nextScenePath` あり」の行の場合、追加の会話送りなしでシーン遷移を始めます。通常の空のセリフは従来どおり表示します。暗転は `NovelDialogueController` の **Use Scene Transition Fade / Scene Fade Out Duration / Scene Fade Hold Duration** に従います。現在の `NovelScene_Moteru` はフェードアウト1秒、暗転保持0.15秒です。

## 会話とパズルの共通遷移処理

- `DialoguePuzzleEntryController.cs`：会話からパズルへ進む直前に、戻る会話シーンと再開行を予約します。Inspectorの **Dialogue Controller / Puzzle Scene Name / Puzzle Entry Line Id / Diary Resume Line Id / Diary Puzzle Image** で各ルートの設定を行います。現在のヨワシシーンは `chapter_yowashi_1_035` から進み、`chapter_yowashi_diary_001` に戻る設定です。
- `PuzzleGameFlowController.cs`：パズル側で予約を受け取り、クリア後に待機・暗転を行って指定の会話シーンへ戻ります。カヨ・ヨワシなどで共有します。パズルシーンを直接Playした場合は、戻り先の予約がないため単体起動として動作します。

改名前のスクリプトのGUIDを引き継いでいるため、シーン上のコンポーネントの付け直しは不要です。既存の遷移タイミングと再開位置は維持しています。

### 日記・独白表示時のCanvasGroup

ヨワシ／カヨの日記やドウテの独白では、本文を画面中央へ移す間、通常の `DialoguePanel` を `CanvasGroup.alpha` で隠します。CanvasGroupがない場合はコードで追加するため、シーンへ手動で付ける必要はありません。

Unityの未接続コンポーネントはC#の `??` で正しく判定できない場合があります。`GetComponent<CanvasGroup>()` の取得後に `== null` で判定して追加する処理へ修正しました。同じ書き方だった選択肢だけの表示にも適用しています。独立したUnity環境で修正前の例外を再現し、修正後の日記・独白・選択肢表示、元の透明度への復帰、コンポーネントの重複がないことを確認済みです。検証コードは `work/DialogueCanvasGroupChecks.cs` です。

## モテルの探索パート

`Assets/Scenes/GameMap/Chapter_Kayo_SearchScene.unity` と `Chapter_Moteru_SearchScene.unity` は、Main Cameraに付けた共通の `SearchSceneController` を使用します。モテル側は `Assets/Dialogue/Data/Moteru/MoteruSearchProfile.asset` で対象・画像・配置・会話範囲を切り替えます。左右の矢印、調査時の金色の強調、取得画面、会話UIはカヨと共通です。

| 対象 | 配置 | 取得画面を閉じた後の会話 |
| --- | --- | --- |
| 本棚 | 左の部屋の左側 | `search_moteru_bookshelf_001〜004` |
| 紙片 | 左の部屋の本棚右手前。本棚の会話後に出現 | `search_moteru_paper_001〜003` |
| カヨとヨワシ | 右の部屋のベッドの左側 | `search_moteru_people_001〜017` |

会話原稿は `Assets/Dialogue/CSV/Moteru/chapter_moteru_search.csv`、ゲーム用データは `Assets/Dialogue/Data/Moteru/Chapter_Moteru_Search.asset` です。24行を作成し、CSV Sync Settingsへペア登録済みなので、CSVを保存してUnityが再読み込みするとアセットへ反映します。各対象の最終行で会話を止め、別の対象の会話へ続けて流れないようにしています。

3か所すべての取得と最後の会話が終わると、カヨと同じ処理で2.5秒かけて暗転し、2秒待って `Assets/PuzzleGame/Scenes/PuzzleGame.unity` へ進みます。カヨの日記パズル用の戻り先は予約しません。パズル完了後のモテル専用遷移は、この探索設定には含めていません。

画像と位置を変える場合は **MoteruSearchProfile > Items** を編集します。`room` は0が左、1が右。`anchorMin` / `anchorMax` は画面の左下を0、右上を1とする配置範囲で、画像とクリック範囲を共有します。`sprite` が主画像、`secondarySprite` が2人目の画像です。背景を切り出す場合だけ `uvRect` に切り出し範囲を指定し、専用画像なら `{x: 0, y: 0, width: 1, height: 1}` にします。`firstLineId` / `lastLineId` は会話の先頭と末尾、`requiresItemId` は先に調べる対象です。

**画像設定：** 紙片は `Assets/Art/Items/SearchGame/paper_fragment.png` を探索画面・取得画面の両方で使用します。仮表示の文字は削除済みです。本棚は `useBackgroundArt` を有効にして背景に描かれた本棚をそのまま使い、画像を重ねません。大きさ・周辺の余白・`hotspotPolygon` のクリック／強調範囲をカヨ側とそろえています。取得画面には従来どおり本棚部分の切り出しを表示します。2人は `kayo_normal.PNG` と `yowashi_normal.png` を並べる暫定設定です。専用画像を追加したら `sprite` を設定し、`placeholderText` を空欄にしてください。

アセットをテキストで編集する場合、`uvRect` は `{serializedVersion: 2, x: 0, y: 0, width: 1, height: 1}` のようにバージョンも記載します。省略するとUnityが幅・高さを0として読み込み、画像が見えなくなる場合があります。不正な範囲は実行時にも画像全体へ補正します。専用画像は `Image.preserveAspect` で表示し、非表示からの復帰時にサイズ計算が不安定になるのを防いでいます。

紙片などの専用画像を更新した場合、探索画面・取得画面ともに現在のスプライトの切り出し範囲と縦横比を使用します。取得画面だけPNG全体の余白が入ったり、以前の比率に引き伸ばされたりしないようにしています。本棚などの明示的な `uvRect` の切り出しは維持します。再生中に差し替えた場合は、停止して再実行すると生成済みのUIも作り直されます。

取得した情報は `chapter_moteru.bookshelf`・`chapter_moteru.paper_scrap`・`chapter_moteru.kayo_yowashi` としてアーカイブに保存します。取得済み判定は `MoteruSearch.Acquired.v1` に保存し、カヨの進捗とは分離しています。ニューゲームで初期化し、探索中のセーブ・ロードでも復元します。Editorの再生開始時にも初期化します。手動で初期化する場合は **Tools > Moteru Search > Clear Saved Data** を使います。

検証用の `work/verify-moteru-search.ps1` は、独立したUnityプロジェクトへ本番コードとデータをコピーして実行します。CSVとアセット24行の一致、3通りの探索順、紙片の出現条件、取得UI、取得情報、会話の停止位置、時間停止中の暗転とパズルへのシーン読み込み、旧セーブ形式の受け入れ、カヨ既定の探索対象を確認済みです。検証先のパズルシーンは空の同名シーンに置き換えており、パズル本体のプレイや画面の目視確認は含みません。

## プログラム査読結果

適用を妨げるコード上の不具合は見つかりませんでした。

注意点は以下です。

- インポーターは共通処理 `Assets/Scripts/Dialogue/Editor/DialogueCsvImportService.cs` の `scenario.ReplaceAll(...)` により、出力先アセットの会話データを全置換します。
- チャプター間はCSVの `nextLineId` ではなく、`Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs` の `Following Scenarios` で接続します。両CSVの最終行は正しく空欄になっています。
- 選択肢行では通常の `nextLineId` を設定できません。現在の `prologue2.csv` はこの制約を満たしています。
- `CharacterDatabase.asset` もインポートごとに `Characters.csv` の内容で全置換されます。キャラクター設定はCSVを正として管理してください。
- 現在の `Assets/Dialogue/CSV/Characters.csv` では、`doute` の表示名が「ユウ」です。ゲーム内で「ドウテ」や「蓮愛ドウテ」と表示したい場合は、インポート前に `displayName` を変更してください。

基本仕様は `Docs/DialogueSystem.md` のCSV作成・インポート手順と一致しています。

## CSVと会話アセットの自動同期（2026-10-19追加）

**CSVを原稿、`.asset` をゲーム用データとして扱います。一度ペアを登録すると、CSVを編集・保存してUnityが読み込み直した時点で、対応する `.asset` を自動更新します。** セリフ、選択肢、好感度、分岐、背景、アーカイブ、行ごとの暗転設定も、従来のインポーターと同じルールで反映します。

### 最初に既存ファイルを登録する

Unityのメニューから以下を開きます。

```text
Tools > Dialogue > CSV Sync Settings
```

1. **Character CSV** にキャラクター一覧のCSVを指定します。通常は `Assets/Dialogue/CSV/Characters.csv` です。
2. **Character Database** に、その会話シーンが使う既存のキャラクターデータを指定します。シーンの `Novel Dialogue Controller` の設定から確認できます。
3. **Dialogue CSV** に原稿、**Dialogue Scenario** に反映先の既存 `.asset` を指定します。
4. **「このペアを登録し、CSVから同期」**を押します。その場で内容を反映し、今後の自動同期に登録します。

例えば、`chapter_moteru_1.csv` と `Assets/Dialogue/Data/Moteru/Chapter_Moteru_1.asset` を1組として登録します。最初の名前が違っていても個別登録できます。登録した時点では元の名前を維持し、その後にどちらかを改名したとき、または改名専用メニューを実行したとき、次の命名ルールで双方の名前をそろえます。

同名のCSVと `.asset` が多数ある場合は、同じ画面の **CSV Folder / Asset Folder** を指定して **「同名の既存ペアを一括登録し、CSVから同期」**を押します。サブフォルダも検索します。大文字・小文字を除いて同じ名前の既存ペアを対象にし、同名候補が複数ある組は除外します。除外された組は1組ずつ登録してください。一括登録では、すべてに画面上部のキャラクター設定を使います。

従来の **Tools > Dialogue > CSV Importer** にも **「インポート成功時に自動同期へ登録する」** を追加しました。初期状態ではオンです。いつものインポートが成功すると、その組を自動同期へ登録します。チェックを外すと新たな登録はしませんが、以前の登録は解除されません。

**初回は登録が必要です。登録していないファイルを勝手に組み合わせたり、相手のない `.asset` を新規作成したりはしません。** 同じCSVを複数の会話アセットに、または複数のCSVを同じ会話アセットに登録することもできません。

### 1. CSVの内容を変更する

`text` のセルは空欄でも構いません。インポート・自動同期ではエラーにせず、その行を残して本文が空白の会話ページとして表示します。通常の会話送りで次の行へ進めます。空白文字だけのセルも使用できます。`text` 列そのものと、その行の `lineId` は必要です。選択肢付きで本文が空欄の場合は、従来どおり選択肢だけを表示します。

登録後は **CSVを編集して保存し、Unityに戻る**だけです。Unityの読み込みが終わると `.asset` に反映されます。Unityの自動読み込みを無効にしている場合は、Refreshまたは次の一括同期メニューを実行してください。

```text
Tools > Dialogue > Update Registered Contents (CSV → Assets)
```

このメニューは、最新ファイルを読み込み直してから、有効な登録ペアすべてを **CSVの内容に合わせて** 更新します。**ファイル名は変更しません。** 登録一覧の **「全登録ペアの内容だけ更新（CSV → Asset）」** でも実行できます。処理件数とエラーはConsoleへ表示します。

一括メニューは、目的ごとに次の2つへ分離しました。

| Tools > Dialogue のメニュー | 行うこと |
| --- | --- |
| **Sync Registered Names Only** | 前回からの名前変更を検知し、命名規則に合わせて改名する。会話・キャラクターの内容は更新しない |
| **Update Registered Contents (CSV → Assets)** | 登録済みの全CSVから会話・キャラクターの内容を更新する。改名はしない |

登録一覧にも全ペア用の2つのボタンと、個別ペア用の **「改名だけ反映」「内容だけ更新」** があります。どちらも一時停止中のペアは対象外です。従来の `Sync All Registered CSVs (CSV → Assets)` メニューは、この2つに置き換えました。

登録済みペアは、現在のCSVファイル名のアルファベット順（大文字・小文字を区別しない）で表示します。改名や登録の追加後も自動で並び直します。同名のCSVは保存先のパス順に並べ、CSVが見つからない場合は前回の名前を使います。

手動で片方だけを実行した場合、同時に検知したもう片方の変更は、その操作や後続の読み込みで自動実行しません。必要になったらもう片方のメニューを実行してください。その後、新たにCSVを編集したりファイルを改名した場合は、通常の自動検知が再開します。Unity再起動やスクリプト再読み込み後は、通常どおり両方を確認します。

- 内容は常に **CSV → `.asset` の一方向**です。`.asset` を編集してもCSVへ書き戻しません。
- `.asset` だけに加えた会話の修正は、次のCSV同期で上書きされます。継続して使う設定はCSVに記入してください。
- `.asset` の **Default Background** と、シーン側の会話順序・参照は維持します。
- 登録したキャラクターCSVの変更も検知し、関連する会話とキャラクターデータを同期します。同じキャラクターデータベースには、同じキャラクターCSVを指定してください。
- Play Mode中は自動同期を待機し、編集モードに戻ってから反映します。Unity起動時やスクリプトの再読み込み後にも登録済みペアを確認します。
- 自動検知は名前と内容を分けて扱います。CSV・キャラクターCSVの内容が前回の反映時から変わった場合にだけ内容更新し、ファイル名だけが変わった場合は改名処理だけを行います。

### 2. CSVまたは `.asset` の名前を変更する

**UnityのProjectウィンドウで、どちらか片方だけ名前を変更してください。** Unityの読み込みが終わった後、以下のルールで双方の名前をそろえます。拡張子と保存先フォルダはそれぞれ維持します。

- **CSV**: 英字をすべて小文字にする。例: `chapter_moteru_1.csv`
- **アセット**: `_` で区切った各単語の先頭を大文字、残りを小文字にする。例: `Chapter_Moteru_1.asset`
- 数字と `_` は維持する。入力した側がこのルールと異なる場合、そのファイルの表記もそろえる。
- 登録・内容だけの同期では、既存ファイル名をまとめて改名しない。自動検知では改名があったペアを対象にし、手動の改名専用操作では未変更のペアも命名規則を確認する。

```text
chapter_moteru_1.csv を chapter_moteru_intro.csv に変更
    → 対応する .asset も Chapter_Moteru_Intro.asset になる

Chapter_Moteru_Intro.asset を Moteru_Opening.asset に変更
    → 対応する CSV も moteru_opening.csv になる
```

名前の連動は双方向ですが、**内容の同期方向は改名後もCSVから `.asset` です。** `.asset` を改名したことを理由に、その中の文章をCSVへ書き戻すことはありません。

名前だけを手動で反映する場合は **Tools > Dialogue > Sync Registered Names Only** を実行します。前回からの改名を反映するとともに、**登録後に改名していないペアも命名規則を確認して修正します。** 例えば、既存の `Chapter_Moteru_1.csv` は `chapter_moteru_1.csv` に、対応するアセットは `Chapter_Moteru_1.asset` にそろえます。変更が検知されていないペアではCSVの名前を基準にします。既に規則どおりなら何もしません。登録一覧の個別の **「改名だけ反映」** でも同じ修正を行います。この操作はCSVの本文・選択肢・好感度などを解析せず、会話やキャラクターデータも更新しません。

同じ読み込みのタイミングで両方を別々の名前にした場合は、大文字・小文字を除いて比較し、単語が異なっていればエラーにします。`chapter_moteru_2.csv` と `Chapter_Moteru_2.asset` のように対応する名前へ直すか、登録を解除して登録し直してください。相手の保存先に同名ファイルが既にある場合も、そのファイルを上書きせず停止します。

なお、**BGM設定の `MusicPlan.json` や、古いセーブに記録されたシナリオ名までは自動変更しません。** シナリオ名を使った設定とセーブの互換性は、改名時に別途確認してください。CSV内の `lineId` / `nextLineId` はファイル名とは別なので、改名に合わせて変更する必要はありません。

### フォルダ移動・一時停止・エラーへの対応

- フォルダ移動もUnityのProjectウィンドウで行ってください。各ファイルの「管理番号」で対応を保存しているので、移動後もペアは維持されます。相手のフォルダまで一緒に移動する機能ではありません。
- 管理番号は `.meta` に保存されています。ファイルと `.meta` を切り離したり、削除して作り直したりすると、元の相手を追跡できなくなります。
- 登録一覧の **「自動同期」** をオフにすると、その組の内容・名前の同期を止めます。一括同期からも除外します。オンに戻すとCSVから再同期します。
- **「登録解除（ファイルは残す）」** は対応だけを削除します。CSVと `.asset` 自体は削除しません。
- 内容更新と改名のエラーは別々に扱い、Consoleと登録一覧に理由を表示します。CSVの記述ミスや存在しないセリフへの分岐では内容更新を止めますが、名前だけの同期は実行できます。名前の衝突では改名を止めますが、内容更新専用メニューは利用できます。読み取り専用ファイルは、書き換えが必要な処理を止めます。
- 片方を削除しても相手は削除しません。登録も残し、ファイルが見つからないエラーを表示します。復元するか、不要なら登録を解除してください。
- 同期の登録情報は **`ProjectSettings/DialogueCsvSyncSettings.asset`** に保存します。初回登録時に作成されます。チームで共有する場合、このファイルとCSV・`.asset`・それぞれの `.meta` を一緒にGitへ追加してください。

### 実装ファイル

すべてEditor専用で、ゲームの実行用コードには同期処理を追加していません。

| ファイル | 役割 |
| --- | --- |
| `Assets/Scripts/Dialogue/Editor/DialogueCsvImportService.cs` | 既存のCSV解析・検証を移動して共有。検証を終えてから会話とキャラクターデータを更新し、同じ内容の再保存を避ける |
| `Assets/Scripts/Dialogue/Editor/DialogueCsvSyncRegistry.cs` | CSV・会話アセット・キャラクター設定の管理番号、前回の名前、反映済み内容の識別値を保存。二重登録を防ぐ |
| `Assets/Scripts/Dialogue/Editor/DialogueCsvSyncProcessor.cs` | 名前と内容を別々に検知・反映。改名専用と内容更新専用の一括メニューを担当 |
| `Assets/Scripts/Dialogue/Editor/DialogueCsvSyncWindow.cs` | ペアの個別登録・同名一括登録・一覧・一時停止・登録解除・手動同期 |
| `Assets/Scripts/Dialogue/Editor/DialogueCsvImporterWindow.cs` | 従来の手動インポート画面。共通処理を利用し、成功時に自動同期へ登録できる |

### 検証方法

`work/verify-dialogue-csv-sync.ps1` は、`Temp` 内に独立した小さなUnityプロジェクトを作り、本番と同じ同期コードをコピーして検証します。本体の会話ファイルや登録情報は変更しません。実際のUnityの読み込み処理を通して、CSV編集、双方の改名、フォルダ移動、改名専用メニューで内容が変わらないこと、内容更新専用メニューで名前が変わらないこと、不正CSVでも改名だけ行えること、二重登録、名前衝突、読み取り専用ファイル、停止と再開、削除などを確認します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File work/verify-dialogue-csv-sync.ps1
```

Unityのインストール先が違う場合は `-UnityEditor "Unity.exeのフルパス"` を指定してください。検証内容は `work/DialogueCsvSyncIntegrationChecks.cs` にまとめています。
