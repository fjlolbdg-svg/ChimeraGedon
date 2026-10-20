# ChimeraGedon（キメラゲドン）

スマホ（Android / iOS）向けのオンライン正体隠匿チームバトル。Unity 6000.4.6f1 / URP 2D / 縦画面。
作者は FJ。会話・ドキュメントは日本語。

## まず読むもの

| ファイル | 内容 |
|---|---|
| `docs/GAME_SPEC.md` | ゲームルールの正。陣営・攻防・マップ・終末・勝利条件 |
| `docs/PARTS_AND_CARDS.md` | 部位18種・カード3山札・緑の使命のデータ表 |
| `docs/TECH_SPEC.md` | 技術構成・同期方式・画面・開発タスクの進捗 |
| `docs/DECISIONS.md` | 決定事項と未決の論点。「仮採用」は FJ の確認待ち |

## コードの場所

- `Assets/Scripts/Core/` … ルールエンジン（純C#、UnityEngine 不使用）。入口は `Rules/MatchEngine.cs`。データ表は `Data/Catalog.cs`、数値は `Balance.cs`。
- `Assets/Scripts/Core/Bots/` … ボットと自動対戦（バランス検証）。
- `Assets/Scripts/Editor/` … エディタメニュー（`ChimeraGedon/...`）。
- `Assets/Tests/EditMode/` … ルールのテスト。ルールを変えたらテストも直して全件通すこと。

ルールを変えるときは、先に `docs/` を更新してから実装する。数値調整は GAME_SPEC.md の「バランス調整ログ」に残す。

## 参考プロジェクト（読むだけ、編集禁止）

- `C:\Unity\Doubt!` … 同作者のスマホ向けオンラインカードゲーム。**マルチプレイ（NGO + Sessions API + Relay）、ホスト権威の受信者別スナップショット、4シーン構成、UI 部品の手本**。
  `Assets/Docs/SPEC_TASK_SHEET.md` に実装の要点がまとまっている。
- `C:\Unity\Merito` … 同作者の別作品。UI の参考のみ。ネットワークは旧 Relay SDK なので真似しない。

## 守ること

- スタンドアロンの `com.unity.services.lobby` / `.relay` / `.matchmaker` / `.multiplay` は入れない（`com.unity.services.multiplayer` を使う）。
- ルールエンジンは Unity に依存しない純C#で書き、EditMode テストを付ける（TECH_SPEC 3-1）。
- 他人の秘密（未公開部位の色・解析結果・計画）は、本人以外のクライアントへ送らない。
- コミット・push は FJ に確認してから。
