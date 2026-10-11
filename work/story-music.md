# BGM配置（共通パート・カヨ編・ヨワシ編）

原文の出来事と、ユーザー承認済みの曲名・雰囲気による配置。セリフ原文・CSV・分岐は変更していません。曲の試聴とUnity実機での音量・ループ確認は未実施です。

## 素材

音楽班の20261010T095621Z配布フォルダーから7曲をコピーし、コピー前後のSHA256一致を確認しました。ポップ2は既存素材と同一なので重複追加せず、タイトルの既存設定を維持しています。

| 元の曲名 | Resources内の名前 |
|---|---|
| ポップ | Pop |
| 静寂 | Quiet |
| 不思議 | Mystery |
| ノスタルジック | Nostalgic |
| しんみり | Melancholy |
| ホラー | Horror |
| パニック | Panic |

## 主な切り替え位置

| 場面 | 曲 | 起点 |
|---|---|---|
| ゲーム内のデート・自室の軽い掛け合い | ポップ | Prologue / Prologue1 |
| 就寝 | フェードアウトして停止 | prologue1_005 |
| 白い部屋で目覚める | 静寂 | Prologue2 |
| 探索開始・探索中 | 不思議 | chapter1_005 / 探索シーン |
| 他のキャラとの会話・自己紹介 | ポップ | chapter1_007 / Chapter2系列 |
| 脱出条件の整理・共通相談 | 不思議 | chapter2_end_007 / Chapter3 |
| 共通の数十年後の分岐 | 静寂 | chapter3_009（相談へ戻ると不思議） |
| キャラ選択・カヨとの初めの掛け合い・キャッチ | ポップ | SelectionPart / Chapter_Kayo_1 / falling_verKayo |
| カヨが関わらない方がよいと伝える | しんみり | chapter_kayo_2_007 |
| 恋愛を避ける理由・手がかり探し | 不思議 | chapter_kayo_2_011 / カヨ探索（パズルまで継続） |
| 日記の幼少期 | ノスタルジック | Chapter_Kayo_Diary |
| 日記の母との別れ | しんみり | chapter_kayo_diary_008 |
| 日記の自己否定が強まる部分 | ホラー | chapter_kayo_diary_014 |
| 日記読了後の相談 | しんみり | Chapter_Kayo_3 |
| 特訓を続けるバッド分岐 | ホラー | chapter_kayo_3_014（別の相談分岐ではしんみり） |
| 日記の文字が歪む | ホラー | chapter_kayo_4_008 |
| 崩壊・救出 | パニック | chapter_kayo_4_010 / Chapter_Kayo_5 |
| カヨを気遣う言葉・告白・感謝 | ノスタルジック | chapter_kayo_6_011以降 |
| エピローグの朝食 | ポップ | chapter_kayo_end_002 |

## ヨワシ編の切り替え位置

ヨワシの原文Wordの出来事と実装済み9シナリオを照合して配置しています。曲名・場面の雰囲気による選定で、原文のBGM指定ではありません。セリフ・ゲーム・分岐の内容は変更していません。

| 場面 | 曲 | 起点 |
|---|---|---|
| 包帯・服装・漫画についての掛け合い | ポップ | Chapter_Yowashi_1 |
| 会話中に息切れし、休む | 静寂 | chapter_yowashi_1_028 |
| 本棚の下の本を発見・日記パズル | 不思議 | chapter_yowashi_1_035（パズルまで継続） |
| 日記の学校・体調への悩み | しんみり | Chapter_Yowashi_Diary |
| 日記で「大丈夫」を繰り返す部分 | 静寂 | chapter_yowashi_diary_008 |
| 日記の持ち主を考える | 不思議 | chapter_yowashi_diary_015 |
| 翌日の挨拶・野球の誘い・キャッチゲーム | ポップ | Chapter_Yowashi_BeforeCatch / falling_verYowashi |
| 日記を見せた時の沈黙・はぐらかす反応 | 不思議 | chapter_yowashi_beforecatch_005（野球の誘いでポップへ戻る） |
| キャッチ成功後の本の受け渡し | ポップ | Chapter_Yowashi_CatchSuccess |
| キャッチ成功後、苦しそうな様子・休息 | 静寂 | chapter_yowashi_catchsuccess_009 |
| もらった本を調べる・医学書の説明 | 不思議 | chapter_yowashi_catchsuccess_015 / Chapter_Yowashi_MedicalBook |
| キャッチ失敗後の疲労・無理を続ける | 静寂 | Chapter_Yowashi_CatchFailure |
| キャッチ失敗側の後日談 | しんみり | chapter_yowashi_catchfailure_006 |
| 4人でのテニス | ポップ | Chapter_Yowashi_Tennis |
| テニス中にヨワシが静かになる | 静寂 | chapter_yowashi_tennis_022 |
| ヨワシが倒れる・本棚が倒れる | パニック | chapter_yowashi_tennis_027 |
| 時間が止まる相談・情報の整理 | 不思議 | Chapter_Yowashi_Consultation |
| 手当・救出・ゲージの結果選択 | パニック | Chapter_Yowashi_Rescue |
| 成功側でヨワシが声を返す・告白 | ノスタルジック | chapter_yowashi_rescue_018 |
| 救出失敗側・最後の独白 | しんみり | chapter_yowashi_rescue_039 |

## 調整・確認

- 配置ファイル：`Assets/Resources/StoryMusic/MusicPlan.json`
- 共通音量：0.35、切替：1.2秒のクロスフェード。タイトルは元の音量設定のまま。
- 同じ曲は場面や会話が変わっても再生し直さない。
- 分岐・チャプタースキップ・途中再開は到着した行から曲を決定。
- Escメニュー・アーカイブではBGMを継続。タイトルのBGMオン／オフは新しいBGMにも反映。
- モテルの個別ルート、単独起動のパズルには新規BGMを割り当てず、共通BGMをフェードアウト。
- カヨ・ヨワシのルートから入った日記パズルでは、直前の手がかり発見の曲を継続。
- 音楽はストリーミング読み込み、2D再生、ループ設定。
- `work/verify-story-music.ps1`で配置参照・分岐／途中再開の判定を検査できます。
- Unityでは日記・崩壊・救出後・両方の分岐で切替と音量、曲末尾のループつなぎを確認してください。
