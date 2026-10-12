# タイトル再設計

## 調査した資料

- VA-11 HALL-A 開発元の制作記事：背景の街とキャラクターで作品の世界観を伝えるタイトル用イラスト。
  https://sukeban.moe/index.php/2022/12/31/daily-va-11-hall-a-2022-24/
- DDLC Plus 公式プレスキット：ロゴ、色、キャラクターの見せ方とゲーム画面の資料。
  https://mail.serenityforge.com/games/doki-doki-literature-club-plus/press-kit
- Ren'Py 公式 GUI サンプル：背景を広く見せ、操作をコンパクトにまとめるメインメニューの実例。
  https://www.renpy.org/doc/html/gui.html#game-and-main-menu-background-images

## 今回の判断

上記をそのまま模倣せず、背景・タイトル・操作の優先順位を分ける考え方を採用。
既存の空のイラストを維持し、大きな右側パネルを撤去。
中央上に白いタイトル（ハートのみ淡いピンク）、中央下に開始／ロード／終了を配置。
ボタンの半透明黒と端のフェザーはゲーム内 UI と共通にし、ホバー文字を淡いピンクに変更。
BGM とクレジットは画面下に小さく分離。基準解像度は 1920×1080。

## 確認範囲

`title-preview.html` は既存背景を使った配置確認用のブラウザープレビューであり、Unity の実行画像ではない。
Unity 用 C# のビルドと静的回帰検査を行う。Unity Play Mode の実表示は別途確認が必要。
イラスト・シナリオ・音楽素材は編集していない。
