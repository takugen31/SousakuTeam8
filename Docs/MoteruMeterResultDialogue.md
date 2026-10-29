# モテルのゲージゲーム結果会話

原稿は `Docs/Scenario/凄久モテル.txt` のゲージゲーム結果分岐。

| CSV（Assets/Dialogue/CSV/Moteru/） | 最初の行ID | 行数 |
| --- | --- | ---: |
| chapter_moteru_meter_failure.csv | chapter_moteru_meter_failure_001 | 3 |
| chapter_moteru_meter_success.csv | chapter_moteru_meter_success_001 | 15 |

それぞれ独立した会話として既存のDialogue CSV Importerまたは実行時CSVローダーで読み込める。話者は既存の `moteru` / `doute`、地の文・効果音は話者なし。両CSVとも最終行の `nextScenePath` に `Assets/Scenes/GameMap/NovelScene_Moteru.unity` を設定している。最終行を読み進めると既存の会話コントローラーがシーン遷移する。

成功時の「モテルがドウテを押し倒しているスチル」は、素材未指定のため008行目の `notes` 列に記録している。`notes` は表示・インポートの対象外で、スチル指示がセリフとして表示されることはない。素材が用意できたら同じ行の `backgroundPath` にSpriteのパスを設定する。

今回の追加は会話CSVと遷移指定。ゲームの成否イベントへのCSV再生処理の接続や、遷移先の会話再開行の予約は含まない。
