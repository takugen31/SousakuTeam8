# シーンごとの会話プログレス

最終確認: 2026-10-23。現在のシーンファイル・会話CSV・スクリプトの接続に基づきます。手動セーブ枠の仕様は [SaveLoad.md](SaveLoad.md) を参照してください。

## 動作

会話シーンAから別シーンBへ移動したとき、Aの会話位置を自動保存します。その後Aへ戻ると、保存した位置と表示状態から再開します。手動セーブの6枠とは別の保存先・処理です。

`NovelDialogueController` を使うすべてのシーンが対象で、保存・復元の有効状態を事前のJSONで指定します。探索画面などの埋め込み会話も、同じコントローラーの会話再生APIを通して記録し、所属シーンの設定に従います。探索画面自体やミニゲーム盤面の復元は、会話プログレスの対象外です。

| 設定 | 次回の会話シーン入場時 |
| --- | --- |
| ON（既定） | 保存した位置から再開。ミニゲーム結果などの明示的な再開要求があれば、その行を優先 |
| OFF | シーンに設定された通常の開始位置から開始。プログレス保存・復元と、シーン間の再開要求を無効化 |

### ON/OFFを事前設定する場所

**[Assets/Resources/DialogueSceneProgressSettings.json](../Assets/Resources/DialogueSceneProgressSettings.json) を、ゲーム起動前・Play Mode開始前に編集します。Escメニューの切り替えボタンは削除しました。InspectorにもON/OFFのチェック項目はありません。**

設定例（以下は抜粋で、実ファイルには現在の会話対象8シーンを列挙済みです）:

```json
{
  "enabled": true,
  "defaultSceneEnabled": true,
  "scenes": [
    { "scene": "NovelScene", "enabled": true },
    { "scene": "NovelScene_Kayo", "enabled": false },
    { "scene": "NovelScene_Moteru", "enabled": true },
    { "scene": "NovelScene_Yowashi", "enabled": true }
  ]
}
```

| 項目 | 設定方法 |
| --- | --- |
| ルートの `enabled` | 全体スイッチ。`false` なら、個別設定にかかわらずすべてOFF。すべてONにする場合は `true` にし、各シーンもONにする |
| `defaultSceneEnabled` | `scenes` に書いていないシーンの既定値。`false` にすれば、個別にONとしたシーンだけ有効 |
| `scenes[].scene` | 拡張子なしのシーン名。大文字・小文字を区別。同名シーンを複数作らず、1シーンにつき1項目とする。既存の `.unity` パスでも指定可能 |
| `scenes[].enabled` | そのシーンだけ `true`（続きから）／`false`（通常の先頭から）。同じシーン内のすべての会話コントローラーに適用 |

現在のJSONは全対象シーンONです。たとえばカヨ本編だけOFFにするなら `NovelScene_Kayo` の `enabled` を `false` にします。カヨ探索内のアイテム会話もOFFにするなら `Chapter_Kayo_SearchScene`、キャッチ後のイベント会話もOFFにするなら `falling_verKayo` をそれぞれOFFにしてください。

設定は起動時に読み込み、その実行中は固定です。編集後はPlay Modeを開始し直してください。ビルド版はJSONをResourcesアセットとして同梱するため、変更後に再ビルドします。プレイヤーがゲーム内で変更する設定ではありません。旧Escメニューで保存したPlayerPrefsのON/OFFは参照しません。JSONの欠落・不正なJSON・シーン名の重複などで設定を読めない場合は、警告を出し全シーンONに戻します。

Inspectorの `NovelDialogueController > Scene Progress > Progress Id` は保存する会話の識別子です。設定ファイルの読み取りは [DialogueProgressSettings.cs](../Assets/Scripts/Dialogue/Runtime/DialogueProgressSettings.cs)、保存・復元時のシーン別制御は `DialogueProgressStore` と `NovelDialogueController.Progress.cs` が担当します。確認用APIは `DialogueProgressSettings.IsEnabled("NovelScene_Kayo")` です。実行中に設定を変更するAPIはありません。

起動時にOFFのシーンの古い会話履歴とシーン別開始位置を消去し、その実行中は記録しません。後から設定をONへ戻して起動しても、OFFにする前の古い位置は復活しません。ONの別シーンの履歴は維持します。好感度・アーカイブ・探索アイテムや手動セーブ枠は、この設定変更では初期化しません。

戻り先の会話シーンがOFFなら、ミニゲームから会話へ戻った場合も、会話シーンの先頭へ戻ります。シーンの序盤にミニゲームへの遷移がある場合は、再度その遷移に到達します。

## 現在のシーンで使われている場所

### 会話を記録・復元するコンポーネント

| シーン | Hierarchyで確認する場所 | 使用される場面・処理 |
| --- | --- | --- |
| [NovelScene](../Assets/Scenes/GameMap/NovelScene.unity) | `DialogueController` の `NovelDialogueController` | 共通ルートの会話全体。入場時に保存済みの行・表示状態を復元し、探索などへの離脱時に進行を保存 |
| [NovelScene_Kayo](../Assets/Scenes/GameMap/NovelScene_Kayo.unity) | `DialogueController` の `NovelDialogueController` | カヨの会話全体。キャッチ・探索・パズルをまたぐ会話再開。同じオブジェクトの `KayoGaugeOutcomeController` の結果も履歴に保存 |
| [NovelScene_Moteru](../Assets/Scenes/GameMap/NovelScene_Moteru.unity) | `DialogueController` の `NovelDialogueController` | モテルの会話全体。キャッチ後の章、探索・パズル後の日記、選択肢後の進行を再開 |
| [NovelScene_Yowashi](../Assets/Scenes/GameMap/NovelScene_Yowashi.unity) | `DialogueController` の `NovelDialogueController` | ヨワシの会話全体。日記パズルとキャッチの成功・失敗に応じた再開 |
| [Chapter1_SearchScene](../Assets/Scenes/GameMap/Chapter1_SearchScene.unity) | `Main Camera` の `Chapter1SearchController` → 実行時生成の `EmbeddedItemDialogueController` | 取得アイテムの会話を `PlayDialogueRange()` で開始すると、会話範囲ごとの履歴を復元 |
| [Chapter_Kayo_SearchScene](../Assets/Scenes/GameMap/Chapter_Kayo_SearchScene.unity) | `Main Camera` の `SearchSceneController` → 実行時生成の `EmbeddedItemDialogueController` | カヨ探索中のアイテム会話。対象の会話範囲が開始された時点で復元 |
| [Chapter_Moteru_SearchScene](../Assets/Scenes/GameMap/Chapter_Moteru_SearchScene.unity) | `Main Camera` の `SearchSceneController` → 実行時生成の `EmbeddedItemDialogueController` | モテル探索中のアイテム会話と、設定された探索完了時の会話範囲。モテル用プロファイルを使用 |
| [falling_verKayo](../Assets/FallingGame/Scenes/falling_verKayo.unity) | `Catch Mini Game` の `KayoCatchGameFlowController` → 実行時生成の `KayoCatchEventDialogueController` | ゲーム後の成功・失敗イベント会話。`StartEmbeddedDialogue()` でシナリオ別の履歴を復元 |

`EmbeddedItemDialogueController` と `KayoCatchEventDialogueController` は実行時に生成されるため、通常のEdit ModeのHierarchyには表示されません。いずれも `NovelDialogueController` を追加して使うため、同じプログレス処理が適用されます。

探索シーンへ入っただけでは、最後に開いていたアイテム会話の画面までは自動で開きません。探索側が対象の会話を開始した時点で、その範囲の続きを復元します。探索する部屋・調査画面の復元は [手動セーブ・ロード](SaveLoad.md) 側の機能です。また、カヨのゲーム内会話が復元対象でも、キャッチゲームのスコア・残り時間・イベント所有側のフェーズ全体を復元する機能ではありません。

### ミニゲーム・探索からの戻り先を指定する場所

次の処理は、ミニゲームの途中経過を保存するのではなく、会話へ戻るときの開始位置をプログレスへ書き込みます。

| シーンとGameObject | 担当コンポーネント | 完了時の処理 |
| --- | --- | --- |
| `Chapter1_SearchScene` / `Main Camera` | [Chapter1SearchController](../Assets/Scripts/Search/Chapter1SearchController.cs) | 探索完了後、`NovelScene` の `chapter1_006` を指定して戻る。入場フェードなし |
| `Chapter_Kayo_SearchScene` / `Main Camera` | [SearchSceneController](../Assets/Scripts/Search/SearchSceneController.cs) | 探索完了後に `PuzzleGame` へ進み、戻り先を `NovelScene_Kayo` / `chapter_kayo_diary_001` としてパズルへ渡す |
| `Chapter_Moteru_SearchScene` / `Main Camera` | `SearchSceneController` | 探索完了後に `PuzzleGame` へ進み、戻り先を `NovelScene_Moteru` / `chapter_moteru_diary_001` として渡す |
| `NovelScene_Yowashi` / `DialogueController` | [DialoguePuzzleEntryController](../Assets/Scripts/Search/DialoguePuzzleEntryController.cs) | `chapter_yowashi_1_035` からパズルへ出る際、戻り先を `NovelScene_Yowashi` / `chapter_yowashi_diary_001` として渡す。通常送り・章スキップの両方で動作 |
| [PuzzleGame](../Assets/PuzzleGame/Scenes/PuzzleGame.unity) / `Puzzle Game` | [PuzzleGameFlowController](../Assets/Scripts/Search/PuzzleGameFlowController.cs) | `Completed` イベント後、上記コンテキストにあるシーン・日記行へ `ReturnToScene()` で戻る |
| `falling_verKayo` / `Catch Mini Game` | [KayoCatchGameFlowController](../Assets/FallingGame/Scripts/KayoCatchGameFlowController.cs) | 結果イベント会話の後に `NovelScene_Kayo` へ戻る。保存履歴優先、履歴なしなら `chapter_kayo_2_001` |
| [falling_verMoteru](../Assets/FallingGame/Scenes/falling_verMoteru.unity) / `Catch Mini Game` | [MoteruCatchGameFlowController](../Assets/FallingGame/Scripts/MoteruCatchGameFlowController.cs) | `RoundEnded` 後に `NovelScene_Moteru` へ戻る。`resumeScenario` の先頭行を優先し、現在は `chapter_moteru_2_001` |
| [falling_verYowashi](../Assets/FallingGame/Scenes/falling_verYowashi.unity) / `Catch Mini Game` | [YowashiCatchGameFlowController](../Assets/FallingGame/Scripts/YowashiCatchGameFlowController.cs) | `RoundEnded` 後に `NovelScene_Yowashi` へ戻る。成功は `chapter_yowashi_catchsuccess_001`、失敗は `chapter_yowashi_catchfailure_001`。現在は3000点を超えると成功 |

`SearchSceneController` と `DialoguePuzzleEntryController` は、画像・戻り経路・日記行を `PuzzleGameFlowController.Queue()` で渡します。パズル完了時の会話開始位置の保存とシーン読み込みは、`DialogueProgressTransitions.ReturnToScene()` が担当します。パズルを単独で開いて戻りコンテキストがない場合は、この会話復帰処理は動作しません。

### 現在の具体的な往復経路

| 出発する会話行 | 遷移経路 | ONで戻ったときの開始位置 |
| --- | --- | --- |
| 共通 `chapter1_005` | `NovelScene` → `Chapter1_SearchScene` → `NovelScene` | `chapter1_006` |
| カヨ `chapter_kayo_1_002` | `NovelScene_Kayo` → `falling_verKayo` → 結果イベント会話 → `NovelScene_Kayo` | 保存済みの続き。初回の通常経路は次章の `chapter_kayo_2_001` |
| カヨ `chapter_kayo_2_027` | `NovelScene_Kayo` → `Chapter_Kayo_SearchScene` → `PuzzleGame` → `NovelScene_Kayo` | `chapter_kayo_diary_001` |
| モテル `chapter_moteru_1_040` | `NovelScene_Moteru` → `falling_verMoteru` → `NovelScene_Moteru` | `chapter_moteru_2_001` |
| モテル `chapter_moteru_2_013` | `NovelScene_Moteru` → `Chapter_Moteru_SearchScene` → `PuzzleGame` → `NovelScene_Moteru` | `chapter_moteru_diary_001` |
| ヨワシ `chapter_yowashi_1_035` | `NovelScene_Yowashi` → `PuzzleGame` → `NovelScene_Yowashi` | `chapter_yowashi_diary_001` |
| ヨワシ `chapter_yowashi_beforecatch_019` | `NovelScene_Yowashi` → `falling_verYowashi` → `NovelScene_Yowashi` | 成功・失敗それぞれの開始行 |

出発行は各ルートの `Assets/Dialogue/CSV`、戻り先は各Sceneに保存されている設定値と接続スクリプトで確認できます。会話本編ではCSVからインポートした `DialogueScenarioSO` を使用します。

### 相手選択・タイトル・共通メニュー

- [SelectionPart](../Assets/Scenes/GameMap/SelectionPart.unity) の `SelectionPartController` は選択先を読み込みます。移動先の会話シーンが自分の履歴を復元するため、選択側で会話行を指定する必要はありません。相手選択画面自身は `NovelDialogueController` を持たず、会話履歴の保存対象ではありません。
- [Title](../Assets/Scenes/GameMap/Title.unity) の `TitleMenu.StartGame()` と、Escメニューの「最初から」は `GameProgress.ResetAll()` を通して会話履歴・開始位置を消去します。タイトルからの手動ロードとの関係は [SaveLoad.md](SaveLoad.md) を参照してください。
- ON/OFFは `Assets/Resources/DialogueSceneProgressSettings.json` で事前指定します。`[Archive System]` 上の `ArchiveMenuUI` に切り替えボタンはありません。各シーンに設定用コンポーネントを配置する必要もありません。
- 現在のゲージ結果はカヨ会話内の `KayoGaugeOutcomeController` が扱い、今回のプログレスはその結果を保存します。独立したゲージゲーム盤面や、別途追加したシーンへ自動的に戻り経路を配線する機能ではありません。

### コード上で保存・復元を呼ぶタイミング

| タイミング | 呼び出しと役割 |
| --- | --- |
| 本編会話シーンへの入場 | `NovelDialogueController.Start()` → `StartDialogue()` → `StartSceneWithProgress()`。手動ロードのデータがあれば `RestoreCheckpoint()` が先に実行される |
| 探索中の範囲会話の開始 | `PlayDialogueRange()` → `BeginProgressSession(true)` → `TryRestoreSceneProgress()` |
| カヨのゲーム内イベント会話の開始 | `StartEmbeddedDialogue()` → `BeginProgressSession(true)` → `TryRestoreSceneProgress()` |
| 会話再生中 | `LateUpdate()`、文字送り開始・全文表示時に `RefreshProgressState()` でメモリ上の状態を更新。毎フレームPlayerPrefsへ書き込む処理ではない |
| 会話行から別シーンへ出る直前 | `LoadDialogueScene()` → `SaveSceneDeparture()`。遷移行を通過済みとして次の行を記録 |
| 外部からのシーン離脱・無効化、一時停止・終了 | `OnDisable()`、`OnApplicationPause(true)`、`OnApplicationQuit()` → `SaveSceneProgress()` |
| 会話終了 | `EndDialogueImmediately()` → `SaveSceneCompletion()`。再訪時も完了状態を復元 |
| ミニゲームなどからの会話復帰 | `DialogueProgressTransitions.ReturnToScene()` → `DialogueProgressStore.SetSceneEntry()` → シーン読み込み。戻り先が開始位置を使ってから消費する |

Inspectorでは本編の `DialogueController` にある `Scenario`、`Following Scenarios`、`Play On Start`、`Scene Progress > Progress Id` を確認します。戻り先の変更は探索・キャッチ側の `completionSceneName` / `completionResumeLineId`、`returnSceneName` / `resumeLineId`、`resumeScenario` など、上表の担当コンポーネントで行います。

## 保存内容とタイミング

- シーンとコントローラーを識別するキー
- シナリオ名、現在のセリフID、会話範囲の終了ID
- 文字送り済みの文字数、全文表示の完了状態
- 選択肢を選ぶ前の現在行、選択後の遷移先
- 会話全体の完了状態
- 通過済みのシーン遷移と、その後に再開する行
- 公開済みの人物名、表示済みの相談行、効果を適用済みの行
- 背景と左右立ち絵の画像識別情報、表示・配置・色
- カヨのゲージ結果

安定した会話状態をメモリ上に控え、コントローラーの無効化・シーン離脱、会話完了、会話内のシーン遷移、アプリの一時停止・終了時に `PlayerPrefs` へ保存します。文字送りは表示済み文字数から再開し、全文表示後なら全文のまま復元します。AUTOは復元時にOFFにします。

会話内の `nextScenePath` から移動したときは、その遷移行をもう一度実行しないよう、戻り位置を次の優先順で決めます。

1. 遷移行に明示された `nextLineId`
2. 同じシナリオ内の直後の行
3. 後続の有効なシナリオの先頭行
4. 続きがなければ会話完了として扱う

任意の外部スクリプトが `SceneManager.LoadScene()` した場合は、直前の会話行と文字表示状態を復元します。会話を最後まで読み終えている場合、再訪時も完了状態にし、会話の完了イベントを所有側へ通知します。

好感度や獲得情報は既存の管理機能が現在値を維持します。Aに戻るためにBで獲得した情報を過去へ巻き戻しません。また、ON中に同じ保存済み会話行へ戻っても、その行の好感度増減などを重複適用しません。

## 保存場所と初期化

| キー | 内容 |
| --- | --- |
| `Dialogue.SceneProgress.v1` | `entries`（会話履歴）と `sceneEntries`（シーン別開始位置）を持つ共通JSON |
| `Dialogue.SceneProgress.Enabled.v1`（旧） | 旧Escメニューの共通ON/OFF設定。現在は参照せず、JSON設定を使用 |

製品実行時はONのシーンの履歴が終了後にも残ります。OFFのシーンの履歴・開始位置は起動時に破棄します。新規開始時は既存の進行データと一緒に初期化します。Unity Editorでは、好感度・探索などの既存の開発用初期化に合わせ、Play Modeに入るたび全会話履歴を削除します。ON/OFFは毎回JSONから読み込みます。

手動ロードは、プログレスがOFFでも保存枠の内容を復元します。ロード成功時は古いシーン履歴を持ち越さず、ロードした時点から新しい履歴を作ります。ロード処理がシーン読み込み前に失敗した場合は、元の履歴に戻します。新規開始・手動ロードでは世代番号を更新し、離脱中の旧コントローラーから未来の履歴が再保存されることを防ぎます。

## 責務の分割

| ファイル | 責務 |
| --- | --- |
| [DialogueProgressState.cs](../Assets/Scripts/Dialogue/Runtime/DialogueProgressState.cs) | 会話履歴・画像状態のデータ形式 |
| [DialogueSceneProgressSettings.json](../Assets/Resources/DialogueSceneProgressSettings.json) | 全体・既定値・シーン別のON/OFFを事前設定 |
| [DialogueProgressSettings.cs](../Assets/Scripts/Dialogue/Runtime/DialogueProgressSettings.cs) | ResourcesのJSON読み込み、検証、シーンごとの有効状態の解決 |
| [DialogueProgressStore.cs](../Assets/Scripts/Dialogue/Runtime/DialogueProgressStore.cs) | 履歴JSON保存・読み込み、OFFシーンの履歴除去、履歴消去、世代管理 |
| [DialogueProgressTransitions.cs](../Assets/Scripts/Dialogue/Runtime/DialogueProgressTransitions.cs) | 開始位置の記録、シーン読み込み、失敗時の開始位置復旧 |
| [NovelDialogueController.Progress.cs](../Assets/Scripts/Dialogue/Runtime/NovelDialogueController.Progress.cs) | 会話状態と履歴の変換、再開位置の解決、表示復元 |
| [NovelDialogueController.cs](../Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs) | 通常の会話再生。開始・終了・遷移時にプログレス側を呼び出す |

`GameSaveService` の枠へ自動保存する処理はありません。プログレス専用のデータ形式と保存キーを使います。`GameProgress.ResetAll()` と手動ロードには、履歴の初期化を接続しています。

## 開始位置と復元の優先順位

1. 手動セーブ枠のロード
2. ON時のみ、プログレス内に記録された対象シーンの開始位置
3. ON時のみ、その会話の保存履歴
4. 通常の開始位置

開始位置には次の2つのモードがあります。

| モード | 動作・利用箇所 |
| --- | --- |
| `OverrideSavedProgress`（既定） | 指定行を優先。ヨワシの成否、モテルのキャッチ完了、各日記パズル、共通探索 |
| `PreferSavedProgress` | 保存履歴を先に復元し、履歴なし・復元不可なら指定行を使う。カヨのキャッチ完了 |

開始位置は履歴と同じJSONに保存します。複数シーンが互いの開始位置を上書きすることはありません。対象行を持つコントローラーが開始できた時点で消費します。別シーンや、その行を持たない別コントローラーは消費しません。BGMの入場判定は読み取りのみです。

```csharp
// 成功・失敗などの結果で開始行を決め、そのシーンへ戻る。
DialogueProgressTransitions.ReturnToScene(
    "NovelScene_Example", "chapter_example_success_001");

// 保存された続きがあれば優先する。
DialogueProgressTransitions.ReturnToScene(
    "NovelScene_Example", "chapter_example_after_game_001", true,
    DialogueProgressEntryMode.PreferSavedProgress);

// 事前設定されたシーンの有効状態を確認する（実行中は読み取りのみ）。
bool enabled = DialogueProgressSettings.IsEnabled("NovelScene_Kayo");

// 履歴とシーン別開始位置を初期化する。
DialogueProgressStore.Clear();
```

`ReturnToScene()` が開始位置の記録とシーン読み込みを行います。戻り先シーンがOFFでも移動は行い、開始位置は記録せず先頭から再生します。呼び出し元のミニゲームの設定ではなく、戻り先の会話シーンの設定で判断します。読み込み要求に失敗した場合は以前の開始位置へ戻し、`false` を返します。

通常の会話からの離脱は出発元自身の履歴に続きを保存します。会話A→会話B→会話Aでも、BがAの会話位置を消費しません。

### 旧方式からの移行

会話コントローラー内のstaticの単一再開ID、フェード予約、旧再開APIと検索処理は削除しました。互換ラッパーも残していません。人物名の共有状態と通常の会話再生は継続して使います。

キャッチ3ルート、共通探索、各日記パズルは、すべて `DialogueProgressTransitions` に移行しました。Sceneに保存された戻り先・行ID・シナリオのフィールドは維持し、SceneやCSVの設定を変更せず移行しています。

以前の `Dialogue.SceneProgress.v1` に `sceneEntries` が含まれていなくても、会話履歴は引き続き読み込めます。手動スロットのデータ形式は変更していません。

パズルに使用画像と戻り経路を渡すコンテキストは残しています。会話復元の責務はプログレス側にあり、コンテキストは会話復元やパズル盤面のセーブではありません。

## 識別子とシーン追加

会話コンポーネントの主ファイルは `NovelDialogueController.cs` です。ファイル名とクラス名を一致させ、`.meta` のGUIDを維持してください。主ファイルの名前が異なる状態で `partial` 分割すると、C#のコンパイルが通ってもUnityがシーンのスクリプトを解決できず、会話が始まらない場合があります。検証では `MonoScript.GetClass()` と実際の4会話シーンのコンポーネント・設定参照も確認します。

開いているシーンの会話Inspector設定だけを保存時点に戻すには、Edit Modeで `Tools > Dialogue > Restore Inspector From Saved Scene` を使います。シーン全体は再読み込みせず、会話コンポーネントの値と参照だけを復元します。ほかの未保存変更は保持し、復元操作はUndoで戻せます。変更後のシーンは自動保存しません。保存する前にInspectorを確認してください。

通常のキーは、シーンのアセットパスとコントローラーのHierarchyパスから自動作成します。同名の兄弟オブジェクトは同名内の順番で区別します。シーンの移動、オブジェクト名や階層の変更でキーが変わると、旧履歴は参照されません。

Inspectorの `Scene Progress > Progress Id` を設定すると、Hierarchyパスの代わりに固定IDを使えます。動的生成する会話や階層を変更する予定の会話には、シーン内で重複しない固定IDを設定してください。

埋め込み会話はさらにシナリオ名と範囲の開始・終了行で区別します。同じ探索用コントローラーで複数アイテムの会話を再生しても、範囲ごとに履歴を持ちます。所有側がその会話を開始するAPIを呼んだ時点で復元します。

行ID・シナリオ名は保存データの参照先です。存在しない行や壊れたJSONを読み込んだ場合は、警告を出して通常開始へ戻します。背景や立ち絵は画像データ自体を保存せず、ロード済みSpriteの名前・Texture名・矩形から解決します。

一時的な見回し・フェードなどの演出は、その内部の秒数まで保存する対象ではありません。途中で外部から離脱した場合は、取得できた直近の安定した会話状態から再開します。

## 検証

`work/verify-dialogue-scene-progress.ps1` は専用のUnityプロジェクトと専用PlayerPrefsで、本番の会話コントローラー・保存処理・シーン読み込みを使って検証します。通常のプロジェクトのシーンやセーブデータをテストのために書き換えません。

検証対象は、A→B→A、部分表示と全文表示、画像配置、通過済み遷移、ミニゲーム結果の優先、選択前後、会話完了、複数コントローラー、埋め込み会話、JSONの全体・シーン別ON/OFFと未指定シーンの既定値、旧設定の無視、OFF履歴の破棄とONシーンの履歴維持、Esc切り替えボタンの削除、新規開始、手動ロード、破損データ、共通遷移API、履歴優先、開始位置の消費、BGM参照です。
