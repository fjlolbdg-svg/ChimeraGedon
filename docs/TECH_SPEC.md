# キメラゲドン 技術仕様・開発タスク

> [!AI-AGENT-INSTRUCTION]
> ルールは `docs/GAME_SPEC.md`、データは `docs/PARTS_AND_CARDS.md`。ここは「どう作るか」と進捗。
> マルチプレイの実装は**『DOUBT!』（`C:\Unity\Doubt!`）を手本にする**。読むのは自由、**編集は禁止**（別リポジトリ）。
> 『メリット』（`C:\Unity\Merito`）は旧 Relay SDK を使っているので、ネットワーク部分は真似しない（UI の参考のみ）。
> タスクのチェック状態は実装を確認してから更新する: `- [x]` 完了 / `- [~]` 部分・仮 / `- [ ]` 未着手。

---

## 1. 環境

| 項目 | 内容 |
|---|---|
| Unity | 6000.4.6f1（URP 2D、uGUI 2.0、New Input System） |
| 画面 | 縦 1080x1920、CanvasScaler ScaleWithScreenSize / match 0.5、SafeArea 対応 |
| 対象 | Android / iOS |
| リポジトリ | GitHub `fjlolbdg-svg/ChimeraGedon`（main） |

### 導入するパッケージ（『DOUBT!』と同じ構成・未導入）

| パッケージ | 版（DOUBT! 実績） | 用途 |
|---|---|---|
| `com.unity.netcode.gameobjects` | 2.13.2 | 通信本体。トポロジは ClientServer（ホスト権威） |
| `com.unity.services.multiplayer` | 2.1.3 | Sessions API（作成・クイックマッチ・コード参加）。Relay は `SessionOptions.WithRelayNetwork()` 経由 |
| `com.unity.multiplayer.playmode` | 2.0.2 | 1台で複数クライアント検証（MPPM） |
| `com.unity.localization` | 1.5.12 | 多言語（まずは ja / en） |
| `com.unity.multiplayer.center` | 1.0.1 | 導入済み |

**導入禁止**: スタンドアロンの `com.unity.services.lobby` / `.relay` / `.matchmaker` / `.multiplay`（統合パッケージと競合する）。
**使わない API**: `[ServerRpc]` / `[ClientRpc]`（→ `[Rpc(SendTo.X)]`）、`OnClientConnectedCallback` / `OnClientDisconnectCallback`（→ `OnConnectionEvent`）、`ISession.PlayerLeft`（→ `PlayerLeaving` / `PlayerHasLeft`）。

---

## 2. フォルダ構成（予定）

```
Assets/
  Scripts/
    Core/        ルールエンジン（MonoBehaviour に依存しない純C#）
    Data/        PartData / CardData（ScriptableObject）と DB
    Network/     セッション・同期（DOUBT! の Network/ を手本に）
    AI/          ボット
    UI/          画面ごと（Title / Lobby / Game / Result）
    Audio/ Graphics/ Localization/
    Editor/      シーン生成・検証メニュー（メニュー名は `ChimeraGedon/...`）
  Data/          PartData・CardData のアセット
  Scenes/        TitleScene / LobbyScene / GameScene / ResultScene
  Tests/         EditMode テスト（ルールエンジン）
docs/            仕様書（このフォルダ。Unity の外なので .meta は不要）
```

- 名前空間は `ChimeraGedon.*`（例: `ChimeraGedon.Core`）。

---

## 3. アーキテクチャ

### 3-1. ルールエンジンを純C#で分離する

> 実装済み: `Assets/Scripts/Core`（asmdef `ChimeraGedon.Core`、`noEngineReferences: true` なので UnityEngine は使えない）。
> 呼び出し順は `Create → SubmitInitialAreas → [ResolveMoves → LockPlans → ResolveRound] のくり返し`。
> 公開 EN は `PlayerState.PublicEN`（計画開始時の値）を表示すること。`EN` は予約後の値なので、そのまま見せると攻撃の投入額がバレる。

- `MatchState`（全員の HP / EN / 部位 / 手札 / エリア / ラウンド / 乱数状態）と、`RoundResolver`（計画の束を受け取って解決し、イベント列を返す）を **Unity に依存しない純C#** で書く。
- 理由: (1) EditMode テストで全ルールを検証できる、(2) AI 同士の高速自動対戦でバランス調整できる、(3) ホスト・ソロ・テストで同じコードが動く。
- 解決の順序は GAME_SPEC 4-2 のとおり。結果は `ResolveEvent` の列（「AがBを攻撃 a=3 g=2 命中」「AとBの頭を交換」など）で返し、UI はそれを順に再生する。

### 3-2. 同期方式（DOUBT! と同じ「ホスト権威＋受信者別スナップショット」）

- ホストだけが `MatchState` を持ち、ルールを進める。クライアントはレプリカ。
- 入力は `[Rpc(SendTo.Server)]`: `SubmitAreaRpc` / `SubmitMoveRpc` / `SubmitPlanRpc` / `SubmitGuardRpc` / `SendStampRpc`。送信者は `RpcParams.Receive.SenderClientId` から席を引き、なりすましを防ぐ。
- 配信は `RpcTarget.Single` で**受信者ごとに別のスナップショット**を作る。ここが正体隠匿の要。
  - 自分の部位: 色と能力を全部入れる。
  - 他人の未公開部位: `Hidden`（色も能力も入れない）。
  - 解析カードの結果: 使った本人のスナップショットにだけ入れる（潜伏スキルでウソに差し替えた後の値）。
  - 緑の使命: そのカプセルの持ち主にだけ入れる。リザルトで全員に開示。
  - 他人の計画: 解決まで一切送らない。ガードフェイズでは「攻撃の矢印」だけ送る。
- 乱数はホストの試合シード（`System.Random`）に一本化する。配色・山札・先手を再現できる。
- ロスターと席の対応付け、切断時の処理、ホスト離脱時の扱いは DOUBT! の `GameNetworkBridge` / `NetworkSessionManager` / `HostMigrationCoordinator` を参照。

### 3-3. 注意: ホストは全員の秘密を知っている

ホスト権威だとホスト端末のメモリには全員の色がある。改造クライアントでのぞかれるリスクはある。

- **MVP はこのまま進める**（DOUBT! と同じ。フレンド対戦では問題になりにくい）。
- ランダムマッチを本格運用する段階で、専用サーバー（Multiplay Hosting）か、Cloud Code でサーバー側が色を持つ方式を検討する（`DECISIONS.md` D-09）。

### 3-4. 切断とボット

- 正体隠匿は人数が減ると成り立たないので、**切断したプレイヤーはボットが引き継ぐ**（DOUBT! は脱落扱い。ここは変える）。
- 再接続できたら操作を本人に戻す（DOUBT! の `ReconnectContext` を参考）。
- ボットは「ルール上合法な手をそこそこ賢く選ぶ」レベルで良い: 序盤はチャージ／ドロー、HP が減ったら巣へ移動、推定敵にEN差があるときだけ攻撃、など。
- ソロモード（全員ボット）とランダムマッチの人数補充にも同じボットを使う。

### 3-5. フェイズのタイマー

- タイマーはホストが管理し、締め切り時刻（サーバー時刻）を配信する。クライアントは表示だけ。
- 全員が入力を終えたら締め切りを待たずに次へ進む。

---

## 4. 画面

DOUBT! と同じ4シーン構成。UI は DOUBT! のネオン調・SafeArea・モーダル部品を参考にする。

| シーン | 主な要素 |
|---|---|
| Title | 名前・アバター、ソロ / ランダムマッチ / プライベートマッチ、人数選択（4〜8）、チュートリアル、設定 |
| Lobby | 参加者一覧（最大8）、ルームコード（プライベートのみ）、**キメラデッキ編集**（頭・胴・脚を選ぶ、合計コスト表示、プリセット3つ）、READY |
| Game | 下記。開始時にガチャ演出（GAME_SPEC 10章） |
| Result | 勝利陣営の発表、全員の最終キメラ（色つき）と陣営の変遷、MVP 表示、再戦 / タイトル |

### Game 画面（縦）

```
┌─────────────────────────┐
│ R3 計画中 ⏱12   終末ゲージ │ ← 上部: ラウンド・フェイズ・残り時間
│ 陣営: 赤2 青2 緑1          │
├────────────┬────────────┤
│ ① 祭壇      │ ② 研究所     │ ← 中央: 2×2 マップ
│  🐺 🦊      │  🦉          │    各アバターは小さなキメラ（3部位、HP/EN）
├────────────┼────────────┤    タップで詳細（公開済み部位・ログ・疑惑メモ）
│ ④ 巣       │ ③ 闘技場     │
│  🐻         │  🦁 🐰       │
├────────────┴────────────┤
│ [自分のキメラ 頭/胴/脚 色つき]│ ← 下部: 自分
│ HP 8/10  EN 5   陣営: 赤    │
│ 手札 [透視] [防壁]           │
│ [攻撃] [チャージ] [ドロー] [公開]│ ← メインアクション
└─────────────────────────┘
```

- 攻撃は「相手をタップ → スライダーで投入EN → 決定」。ガードも同じスライダー。
- 解決は1イベントずつ演出（攻撃の矢印、入札額の同時オープン、部位の入れ替えアニメーション）。早送りボタンあり。
- 自分の陣営が変わったときは、自分のキメラの縁取りが新しい色に染まる演出＋通知。

---

## 5. 開発タスク

### フェーズ0: 土台
- [x] Unity プロジェクト作成（URP 2D）
- [x] GitHub と接続
- [x] 仕様書（docs/）と CLAUDE.md
- [ ] 縦画面・解像度・SafeArea の設定
- [ ] 4シーン作成と Build Settings 登録、`SceneController`
- [ ] パッケージ導入（1章の表）

### フェーズ1: ルールエンジン（オフライン）✅
- [x] 部位・カード・使命のデータ（`Core/Data/Catalog.cs`。表と ID は PARTS_AND_CARDS.md と一致）
  > Memo: いまは純C#の静的データ。アートや説明文を Inspector で編集したくなったら ScriptableObject を足し、ID で Catalog と結ぶ。
- [x] `MatchState` / 陣営判定（`FactionRules`）/ ガチャマシン（`GachaMachine`: 配色・使命封入・シード再現）/ 合計HP
- [x] `MatchEngine`（移動・計画・ガード・解決・終了処理・終末・最終審判・勝利判定）
  > Memo: `MatchEngine.cs`（フェイズ進行・攻防・ダメージ）、`.Effects.cs`（部位スキル・カード・交換・解析）、`.EndRound.cs`（ダウン・勝利・使命）。
  > 解決結果は `GameEvent` の列（`State.Log`）。`VisibleTo` で本人限定の情報を分けてあり、ネット配信時はこれでフィルタする。
- [x] 全部位18種・カード18種・使命8種の効果
- [x] EditMode テスト 27件（`Assets/Tests/EditMode/RulesTests.cs`、全件成功 2026-10-21）
- [x] ボット（`Core/Bots/BotBrain.cs`）と自動対戦（`BotSimulator`、メニュー `ChimeraGedon/Simulate Bot Matches`）
- [ ] ボットを賢くする（交換・装備・使命を狙う、敵を絞って攻撃する）

### フェーズ2: ソロで遊べる形
- [ ] Game 画面（マップ・自分パネル・アクション・ガード・解決演出・ログ・疑惑メモ）
- [ ] Lobby（デッキ編集）、Result
- [ ] チュートリアル（DOUBT! の `TutorialManager` 方式）

### フェーズ3: オンライン
- [ ] 匿名認証・セッション作成 / クイックマッチ / コード参加（DOUBT! の `NetworkSessionManager` を手本）
- [ ] ロビー同期（名前・アバター・READY・デッキ）
- [ ] ホスト権威の進行と受信者別スナップショット（3-2）
- [ ] 切断時のボット引き継ぎと再接続
- [ ] スタンプ・定型文
- [ ] MPPM での2〜4クライアント検証メニュー

### フェーズ4: 仕上げ
- [ ] ローカライズ（ja / en）
- [ ] サウンド・演出
- [ ] 広告・課金（DOUBT! / メリットの構成を参考に、要否はあとで判断）
