# 3 × 3 Puzzle Game

`PuzzleGame.unity` を開くと、前後の画面を挟まずパズル本体だけが起動します。

## 遊び方

- ピースを別のピースへドラッグすると、2枚の場所が入れ替わります。
- 盤面の外へ離した場合は元の場所へ戻ります。
- 左上から `1, 2, 3 / 4, 5, 6 / 7, 8, 9` になれば完成です。
- 正解するとピース間の隙間が閉じ、9枚が1枚の完成画像としてつながった後、わずかに拡大して強調されます。

## 画像を差し替える

`Resources/PuzzleGame/PuzzleSource.png` を、使用したい1枚の画像へ差し替えてください。ゲームが左上から3 × 3へ自動分割し、9つのパーツとして使用します。

画像は正方形かつ縦横が3で割り切れるサイズを推奨します（例: 1536 × 1536）。CPU上で画像を複製せず、9パーツすべてが同じ画像を共有するため、Textureの `Read/Write` は不要です。コードを変更せず、`PuzzleGamePanel.prefab` の `Puzzle Image` に任意の1枚を直接設定することもできます。

実行中に画像を変更する場合は、`SetPuzzleImage(texture)` を呼び出すと新しい画像でパズルを再開します。現在の並びを維持したい場合は `SetPuzzleImage(texture, false)` を使用します。

## 他のゲームへ組み込む

既存の Canvas 配下へ `PuzzleGamePanel.prefab` を配置します。Prefabには Camera、Canvas、EventSystem、シーン遷移を含めていません。

ホスト側は `PuzzleGameController.Completed` または Inspector の `On Completed` から完成を受け取れます。手数はイベント引数です。

```csharp
puzzle.Completed += moveCount => Debug.Log($"Completed in {moveCount} moves");
```

画面内に再シャッフルボタンはありません。ホスト側から新しいゲームを始める必要がある場合は、`Restart()`、`Restart(seed)`、`SetInteractable(bool)` を呼び出せます。
