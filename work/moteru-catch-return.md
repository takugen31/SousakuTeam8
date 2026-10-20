# モテルのキャッチゲーム終了後の会話再開

`falling_verMoteru` のゲームオーバー後は、スコア画面を **2秒** 表示し、**0.8秒** で黒くフェードアウトして `NovelScene_Moteru` に戻る。

待ち時間は `falling_verKayo` の `resultDisplayDuration`、フェード時間は同シーンの `fadeDuration` と同じ。黒の不透明度を実時間で直線的に変化させるフェード処理も `CatchGameFade.cs` で共通化した。カヨ側の演出の速度・見た目・入力を止める処理は維持する。

戻った後は、`Chapter_Moteru_2.asset` の先頭 `chapter_moteru_2_001` から会話を再開する。点数による分岐は行わない。ゲーム前に予約された再開位置があっても、この先頭行で置き換える。到着側は既存のシーン入場フェードインを使う（現在のモテルシーンでは1秒）。

## 設定場所

`Assets/FallingGame/Scenes/falling_verMoteru.unity` の **Catch Mini Game** オブジェクトに `MoteruCatchGameFlowController` を追加済み。

| Inspector項目 | 設定 |
| --- | --- |
| Mini Game | 同じオブジェクトの `CatchMiniGameController` |
| Result Display Duration | `2` 秒 |
| Fade Duration | `0.8` 秒 |
| Return Scene Name | `NovelScene_Moteru` |
| Resume Scenario | `Assets/Dialogue/Data/Moteru/Chapter_Moteru_2.asset` |

再開行はシナリオアセットの先頭から取得するため、CSV同期で先頭のIDが変わっても、この設定に固定のIDを書き直す必要はない。再開先アセットは、戻り先の `NovelDialogueController` の **Scenario / Following Scenarios** に含める。現在は `Following Scenarios` の先頭に設定済み。

`NovelScene_Moteru` と `falling_verMoteru` は、Build Profilesの有効なScene Listに登録する。戻り先や先頭行が不正な場合はエラーを表示し、スコア画面にとどまる。

## 変更ファイル

- `Assets/FallingGame/Scripts/MoteruCatchGameFlowController.cs`: 終了イベントから待機・暗転・シーン移動・先頭行の再開予約を行う。
- `Assets/FallingGame/Scripts/CatchGameFade.cs`: カヨとモテルで使う黒フェードと既定時間を共有。
- `Assets/FallingGame/Scripts/KayoCatchGameFlowController.cs`: 従来の黒フェードの計算を共通処理へ移動。
- `Assets/FallingGame/Scenes/falling_verMoteru.unity`: 終了後のフローと再開シナリオを接続。

元の `CatchMiniGameController` はスコア画面を表示してから `RoundEnded` を通知する。その通知後に待機を開始するため、スコア表示前に遷移しない。待機とフェードは `Time.timeScale` に依存しない。

## 検証

ビルドとシーンの接続確認を実施。`work/verify-moteru-catch-return.ps1` では、実際のシーンに保存された待ち時間・フェード時間がカヨと一致し、再開アセットがモテルの会話一覧に含まれることを確認する。

独立したUnityプロジェクトで本番のフロー・フェード処理を動かし、0・3000・10000点の各場合に2秒待機、0.8秒の黒フェード、シーン読み込み、先頭行の再開予約が通ることを確認済み。時間停止中の進行、終了通知の重複防止、古い再開予約の上書きも確認した。終了イベントと会話再開の受け口は検証用に置き換えているため、本編全体の通しプレイと画面の目視確認は含まない。
