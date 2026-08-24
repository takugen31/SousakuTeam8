# Catch Mini Game Prototype

`Assets/yoogen/Scenes/SampleScene.unity` を開いて再生ボタンを押すと起動します。
主要オブジェクトはシーンに保存されており、HierarchyとInspectorから編集できます。

## 操作

- マウス左右移動: 画面下の青いキャッチャーを移動
- 黄色い落下物: キャッチすると +100 点
- 赤い落下物: キャッチすると -150 点
- Rキー: スコアと画面内の落下物をリセット

## 現在の仮ビジュアル

- 紫: 画面上部を移動して物を落とすキャラクター
- 青: プレイヤーが操作するキャッチャー
- 黄色: キャッチすべき物
- 赤: キャッチしてはいけない物

ゲーム設定と落下物の見た目は `Catch Mini Game` の `CatchMiniGameController`、
キャラクターの位置・大きさ・色は各子オブジェクトから変更できます。
未使用の落下物はゲーム中に `Item Pool` の非アクティブな子として保持されます。
使用中の落下物だけが `Spawned Items` へ移動し、回収後は `Item Pool` に戻ります。
見た目は `Item Pool` 内の `Good Item Visual` と `Bad Item Visual` を編集してください。
