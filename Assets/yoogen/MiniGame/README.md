# Catch Mini Game Prototype

`Assets/yoogen/Scenes/SampleScene.unity` を開いて再生ボタンを押すと起動します。
主要オブジェクトはシーンに保存されており、HierarchyとInspectorから編集できます。

## 操作

- 再生直後は「大変！かよがピンチ！」を表示。待機中は自機を固定し、キーボードまたはマウスボタン入力後の3・2・1から操作開始
- マウス左右移動またはA/Dキー: 画面下の青いキャッチャーを移動
- スコア2000以上でジャンプ能力を獲得。獲得後はSpaceまたはWキーでジャンプ
- スコア6000以上で一掃能力を獲得。Shiftまたは右クリックで地面滞在中の赤を一掃
- 一掃はスコアを200消費し、再使用まで10秒必要。対象がない場合は消費・リキャストなし
- 黄色い落下物: キャッチすると +100 点
- 赤い落下物: キャッチすると -150 点
- 制限時間: 100秒。一度に落ちるアイテムが1個から最大8個へ増加し、4個以上では黄色と赤が必ず混在
- 時間経過に応じてドロッパー本体の横移動速度も上昇
- 回避された赤アイテムは自機の移動レーンに2秒間残り、接触すると従来どおり減点されて消滅
- 赤アイテムが地面滞在状態になると専用Spriteへ切り替わる。Spriteと表示サイズは `Catch Mini Game` の `Stacked Bad Item Visual` から変更可能
- Dropperは登録された2枚の立ち絵を1秒ごとに切り替え
- スコア帯によって常時メッセージが変化し、減点でスコア帯が下がった場合も表示を更新
- タイムアップ時は専用の終了画面を表示し、最終スコア・キャラクター立ち絵・セリフ・黄色取得数・赤取得数を表示

## 現在の仮ビジュアル

- 紫: 画面上部を移動して物を落とすキャラクター
- 青: プレイヤーが操作するキャッチャー
- 黄色: キャッチすべき物
- 赤: キャッチしてはいけない物

ゲーム設定と落下物の見た目は `Catch Mini Game` の `CatchMiniGameController`、
キャラクターの位置・大きさ・色は各子オブジェクトから変更できます。
背景は `Catch Mini Game/Game Background (Replace Sprite)` のSpriteRendererから差し替えできます。初期状態は白です。
未使用の落下物64個はシーン内の `Item Pool` に非アクティブな子として事前配置されています。
使用中の落下物だけが `Spawned Items` へ移動し、回収後は `Item Pool` に戻ります。
見た目は `Item Pool` 内の `Good Item Visual 01～03` と `Bad Item Visual 01～03` を編集してください。
同名プレフィックスのテンプレートを複製してControllerの配列へ追加すると、種類をさらに増やせます。
スコア・残り時間・加減点・スコア帯メッセージ・ジャンプ獲得通知・開始表示は `Catch Mini Game UI` の子としてHierarchyから編集できます。
終了画面は `Catch Mini Game UI/Game Over UI` 以下から編集でき、アップ立ち絵は `Game Over Portrait` のSpriteを差し替えて変更できます。
ミニゲーム中は最高品質レベル、Render Scale 2.0、4xアンチエイリアスを使用します。UIも `Main Camera` を通して描画するため、Render ScaleとSMAAの対象になります。値は `Catch Mini Game` の `Rendering Quality` から変更できます。
EditorのGame Viewは `GameViewQualityGuard` が低解像度プレビューを解除し、拡大時のフィルタをBilinearへ補正します。必要ならUnityメニューの `yoogen > 画質 > Game View の低解像度表示を解除` から手動でも再適用できます。
