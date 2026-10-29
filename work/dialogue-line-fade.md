# 会話途中の暗転・フェード設定

## 動作

`Chapter_Moteru_1 → Chapter_Moteru_2` のシナリオ切り替えで使っている全画面のフェードを、同じシナリオ内の指定セリフの直前にも設定できるようにした。

再生順序は「直前のセリフを表示したままフェードアウト → 完全に暗くなったところで次の背景・立ち絵を準備して本文を消す → 暗転保持 → フェードイン → 指定セリフの本文をタイプ表示」。暗転中はクリック・キー・AUTO による会話送りを停止し、次のセリフを飛ばさない。好感度・アーカイブなど、その行の処理は一度だけ適用する。

設定は **演出後に表示したい行** に付ける。例えば `chapter_moteru_1_009` を読み終えて次へ進むときに暗転したい場合、`chapter_moteru_1_010` に設定する。選択肢・分岐からその行へ到達した場合も再生する。同じ行に戻ると再び再生する。

今回、演出を入れる具体的なセリフ位置は未指定のため、既存のシナリオ本文・CSV・シナリオアセットには演出設定を追加していない。

## 追加した設定

CSV の列名と `DialogueLine` のフィールド名は同じ。

| CSV 列名 | Inspector 表示 | 設定内容 |
| --- | --- | --- |
| `fadeBeforeLine` | Fade Before Line | `true` でその行の直前に演出する。省略・空欄・`false` は無効 |
| `lineFadeOutDuration` | Line Fade Out Duration | 元の画面から暗くなるまでの秒数 |
| `lineFadeHoldDuration` | Line Fade Hold Duration | 完全な暗転を保持する秒数 |
| `lineFadeInDuration` | Line Fade In Duration | 暗転から元の明るさに戻るまでの秒数 |

時間は `0` 以上。CSV は小数点に `.` を使う。空欄または `-1` は、そのシーンの `NovelDialogueController` の Chapter Transition 設定を引き継ぐ。現在の `NovelScene_Moteru` の値は Out **1 秒**、Hold **1.7 秒**、In **1 秒**。覆う色も既存の Chapter Fade Color を使う（同シーンは不透明な黒）。CSV の不正な真偽値、`-1` 以外の負数、NaN、Infinity はエラーにする。

時間を `0` にするとその段階を即時に切り替える。

| 見せ方 | Out / Hold / In の例 |
| --- | --- |
| なめらかに暗転して戻る | `0.5 / 0.8 / 0.5` |
| 即座に暗転してからフェードイン | `0 / 0.8 / 1` |
| フェードアウトしてから即座に戻す | `1 / 0.8 / 0` |
| 瞬間的に暗転し、しばらくして即座に戻す | `0 / 0.8 / 0` |

どの設定も最後には画面を戻す。黒画面のまま次の入力を待つ機能ではない。Out / Hold / In がすべて `0` なら、画面上の暗転はほぼ見えない。

## Unity の Inspector で設定する方法

1. Project で `Assets/Dialogue/Data/Moteru/Chapter_Moteru_1.asset` を選ぶ。
2. Inspector の **Lines** を展開し、対象の **Line Id** の行を探す。
3. **Fade Before Line** をオンにする。
4. **Line Fade Out Duration / Hold Duration / In Duration** を設定する。既存のチャプター切り替えと同じ長さなら、3 項目とも `-1` にする。
5. `NovelScene_Moteru` を再生し、対象の一つ前のセリフから進めて確認する。

現在の `NovelScene_Moteru` は保存済みの `DialogueScenarioSO` を参照する。**CSV を変更しただけでは、このシーンの再生には反映されない。** Inspector で直接編集した値は、後から CSV を再インポートすると CSV の値で上書きされるため、継続管理する設定は CSV にも記入する。

## CSV で設定する方法

1. `Assets/Dialogue/CSV/Chapter_Moteru/Chapter_Moteru_1.csv` の既存のヘッダー末尾に、次の 4 列を追加する。

   ```csv
   fadeBeforeLine,lineFadeOutDuration,lineFadeHoldDuration,lineFadeInDuration
   ```

2. 演出を入れる行の対応するセルを `true,0.5,0.8,0.5` にする。他の行は 4 セルとも空欄でよい。既存列の内容と列位置は維持する。
3. Unity の **Tools → Dialogue → CSV Importer** を開く。
4. **Character CSV** に `Assets/Dialogue/CSV/Characters.csv`、**Dialogue CSV** に編集した CSV を指定する。
5. **Character Database** はシーンの `NovelDialogueController` が使う既存のデータベース、**Dialogue Scenario** は `Assets/Dialogue/Data/Moteru/Chapter_Moteru_1.asset` を指定してインポートする。このツールはキャラクターデータベースも更新するため、対応する CSV とデータベースを組み合わせる。
6. アセットの対象行を確認し、Play Mode で再生する。

`DialogueCsvSequenceBootstrap` から実行時に CSV を生成するシーンでも、同じ 4 列を読み込む。その構成ではシナリオアセットへのインポートは不要。

**1 つの `text` の途中に暗転を挟む場合**は、暗転前と暗転後の文章を 2 行に分け、それぞれに固有の `lineId` を付ける。前半の `nextLineId` を後半へつなぎ、後半の `fadeBeforeLine` を `true` にする。発火単位は行の切り替えであり、文章内の文字位置を指定する方式ではない。改行 `\n` だけでは別の行として扱われない。

## 既存演出・セーブとの関係

- 4 列がない既存 CSV と、設定していない行はこれまでどおり再生する。
- `Use Chapter Transition Fade` はチャプター間の切り替え用。行の `Fade Before Line` は独立して有効にできる。
- チャプター切り替えの暗転中に次チャプターの先頭行へ進んだ場合、行のフェードは重ねて再生せず、既存のチャプター演出を優先する。その場合、行ごとの時間設定は使わない。
- 同じ行に `consultationTitle` と `fadeBeforeLine` がある場合は行のフェードを優先し、相談開始のフェードを重ねない。
- 立ち絵の拡大など、その行の構図は暗転中に準備する。通常表示経路にある別の構図切り替えフェードを同時実行しない。
- 演出中は会話の保存対象にしない。保存済みの行へロードする際は、直前のフェードを再生し直さない。
- 中断・ロード・コンポーネント無効化では、既存の中断処理でコルーチンと暗転を解除する。

## 主な変更ファイル

- `Assets/Scripts/Dialogue/Runtime/DialogueScenarioSO.cs`: 行ごとの有効フラグと 3 種類の時間設定を追加。
- `Assets/Scripts/Dialogue/Runtime/DialogueRuntimeCsv.cs`: 4 列の読み込みと値の検証を追加。
- `Assets/Scripts/Dialogue/Editor/DialogueCsvImporterWindow.cs`: 実行時 CSV と同じ設定解析を共有し、保存済みアセットにも反映。
- `Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs`: 指定行の直前のフェード、暗転中の行準備、復帰後の本文表示、既存フェードとの重複防止、無効化時の中断処理。
- `work/verify-dialogue-line-fade.ps1`: 本番の CSV 読み込みコード・行データ・演出コルーチンを模擬環境で実行する回帰検証。

## 検証

Visual Studio の MSBuild で `Assembly-CSharp-Editor.csproj` と参照先の実行用コードをビルド。既存の `System.Net.Http` 参照バージョン競合の警告あり。

`powershell -NoProfile -ExecutionPolicy Bypass -File work/verify-dialogue-line-fade.ps1` で、旧 CSV、新しい列、共通インポート、不正値の拒否、処理順序、既定値・ゼロ秒・指定時間、演出中のロック、重複防止、ロード時の抑止、Canvas 不在時の継続、終了時の解除を検証する。実際の Unity 描画と入力イベントの動作は、この模擬環境の検証には含まない。

Unity の Play Mode での見た目は未確認。設定した行の前からの再生、クリック連打、AUTO、選択肢からの到達、演出後の保存とロード、チャプター境界で暗転が二重にならないことを確認する。

作業はコミットしていない。取り込み中の main のマージも未コミットのまま維持する。
