using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChimeraGedon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeraGedon.Game
{
    /// <summary>
    /// アプリの入口（Main シーンに1つ置く）。タイトル → 人数・デッキ選択 → ソロ対戦（自分＋ボット）→ リザルト。
    /// 試合の進行は MatchEngine に任せ、ここは入力の受け付けと演出だけを行う。
    /// オンライン対戦を作るときは、このクラスの「入力を集めてエンジンに渡す」部分をネットワーク経由に差し替える。
    /// </summary>
    public sealed partial class ChimeraApp : MonoBehaviour
    {
        const int HumanSeat = 0;
        const int EventDelayMs = 450;

        static readonly string[] BotNames = { "ミケ", "ポチ", "ハチ", "ココ", "モモ", "タマ", "クロ" };

        Canvas canvas;
        GameScreen screen;
        OptionModal modal;
        RectTransform titleRoot;
        MatchEngine engine;
        List<BotBrain> brains;
        int logCursor;

        int playerCount = 6;
        readonly string[] deck = { PartIds.Owl, PartIds.Bear, PartIds.Frog };

        MatchState S => engine.State;
        PlayerState Me => S.P(HumanSeat);

        void Awake()
        {
            Application.targetFrameRate = 60;
            Ui.Font = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
            Ui.EnsureEventSystem();
            if (Camera.main != null) Camera.main.backgroundColor = Ui.Bg;

            canvas = Ui.CreateCanvas("UI");
            Ui.Box(canvas.transform, "Background", 0, 0, 1, 1, Ui.Bg);
            screen = new GameScreen(canvas);
            BuildTitle();
            modal = new OptionModal(canvas);
        }

        async void Start()
        {
            try
            {
                while (this != null)
                {
                    await RunTitle();
                    await RunMatch();
                }
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                Debug.LogException(e);
            }
        }

        // ───────────────────────── タイトル・デッキ ─────────────────────────

        void BuildTitle()
        {
            titleRoot = Ui.Rect(canvas.transform, "Title", 0, 0, 1, 1);
            Ui.Label(titleRoot, "キメラゲドン", 110, TextAnchor.MiddleCenter, Ui.Accent, 0, 0.62f, 1, 0.8f);
            Ui.Label(titleRoot, "ガチャから出た色で、味方が変わる。", 40, TextAnchor.MiddleCenter, Ui.TextSub, 0, 0.55f, 1, 0.62f);
        }

        async Task RunTitle()
        {
            screen.SetVisible(false);
            titleRoot.gameObject.SetActive(true);
            while (true)
            {
                int pick = await modal.Choose("キメラゲドン", $"人数: {playerCount}人（あなた＋ボット{playerCount - 1}）\nデッキ: {DeckLabel()}",
                    new[] { "ソロで遊ぶ", $"人数を変える（いま {playerCount}人）", "デッキを組む", "あそびかた" }, cancellable: false);
                if (pick == 0) break;
                if (pick == 1) await ChoosePlayerCount();
                if (pick == 2) await ChooseDeck();
                if (pick == 3) await ShowRules();
            }
            titleRoot.gameObject.SetActive(false);
        }

        string DeckLabel() => string.Join(" / ", deck.Select(id => Catalog.Part(id).Name));

        async Task ChoosePlayerCount()
        {
            var counts = Enumerable.Range(Balance.MinPlayers, Balance.MaxPlayers - Balance.MinPlayers + 1).ToList();
            int i = await modal.Choose("人数", "ボットが足りない席を埋めます。おすすめは6人。", counts.Select(c => $"{c}人"), full: true);
            if (i >= 0) playerCount = counts[i];
        }

        async Task ChooseDeck()
        {
            for (int slot = 0; slot < 3; slot++)
            {
                var parts = Catalog.PartsFor((Slot)slot);
                var labels = parts.Select(p => $"{p.Name}  HP{p.Hp}\n{p.RevealText}" + (p.HasHiddenSkill ? $" / 潜伏: {p.HiddenText}" : "")).ToList();
                int i = await modal.Choose($"{Texts.Slot((Slot)slot)}を選ぶ（{slot + 1}/3）",
                    "スキルが強い部位ほどHPが低い。色は試合開始時のガチャで決まります。", labels, full: true);
                if (i < 0) return;
                deck[slot] = parts[i].Id;
            }
        }

        async Task ShowRules()
        {
            const string rules =
                "・3つのカプセル（頭・胴・脚）の色で陣営が決まる。同じ色が2つ以上ならその色、3色バラバラなら緑。\n" +
                "・赤は生き残っている青を、青は赤を全員倒せば勝ち。緑はカプセルに入った秘密の使命を達成すれば勝ち。\n" +
                "・毎ラウンド、移動 → 行動（攻撃・チャージ・ドロー・公開）を全員同時に決める。\n" +
                "・攻撃はENの秘密入札。攻撃が防御より大きければ命中。使ったENは戻らない。\n" +
                "・部位を公開すると強いスキルが使えるが、色がバレる。未公開の部位は交換されると陣営が変わることも。\n" +
                "・7R目から終末ダメージ。10R目が終わったら生存人数で最終審判。";
            await modal.Choose("あそびかた", rules, new[] { "わかった" }, cancellable: false, full: true);
        }

        // ───────────────────────── 試合 ─────────────────────────

        async Task RunMatch()
        {
            var rng = new System.Random();
            var setups = new List<PlayerSetup> { new PlayerSetup("あなた", false, deck[0], deck[1], deck[2]) };
            for (int i = 1; i < playerCount; i++)
            {
                var h = Catalog.PartsFor(Slot.Head); var t = Catalog.PartsFor(Slot.Torso); var l = Catalog.PartsFor(Slot.Legs);
                setups.Add(new PlayerSetup(BotNames[i - 1], true, h[rng.Next(h.Count)].Id, t[rng.Next(t.Count)].Id, l[rng.Next(l.Count)].Id));
            }
            int seed = rng.Next();
            engine = MatchEngine.Create(seed, setups);
            brains = S.Players.Select(p => new BotBrain(seed + p.Seat * 7919)).ToList();
            logCursor = 0;

            screen.ClearLog();
            screen.SetVisible(true);
            screen.SetPhase("");
            await ShowGacha();

            int area = await modal.Choose("はじめのエリア", "どこから始める？　攻撃は同じエリアの相手にしか届きません。",
                Enumerable.Range(0, 4).Select(a => $"{Texts.Area((Area)a)}  {Texts.AreaEffect((Area)a)}"), cancellable: false);
            var areas = S.Players.Where(p => p.IsBot).ToDictionary(p => p.Seat, p => brains[p.Seat].ChooseInitialArea(S, p));
            areas[HumanSeat] = (Area)area;
            engine.SubmitInitialAreas(areas);
            await PlayEvents();

            while (S.Phase != MatchPhase.Finished)
            {
                await MovePhase();
                await PlanPhase();
                await GuardAndResolve();
            }
            await ShowResult();
        }

        async Task ShowGacha()
        {
            screen.Refresh(S, HumanSeat);
            screen.SetPhase("ガチャマシンがまわる…");
            await Task.Delay(700);
            var lines = Me.Parts.Select((p, i) => $"{Texts.Slot((Slot)i)}: {Texts.Chip(p.Color)} {p.Def.Name}").ToList();
            string missions = MatchEngine.ActiveMissions(Me).Any() || Me.Parts.Any(p => p.MissionId != null)
                ? "\n緑カプセルの使命: " + string.Join(" / ", Me.Parts.Where(p => p.MissionId != null).Select(p => Texts.MissionLine(p.MissionId)))
                : "";
            string goal = Me.Faction == Faction.Green ? "使命を達成しよう（赤青と一緒に勝てる）" :
                $"{Texts.Faction(Me.Faction == Faction.Red ? Faction.Blue : Faction.Red)}陣営を全員倒そう";
            await modal.Choose($"あなたは {Texts.FactionColored(Me.Faction)} 陣営",
                string.Join("\n", lines) + missions + $"\n目的: {goal}", new[] { "装着する" }, cancellable: false);
        }

        async Task MovePhase()
        {
            screen.Refresh(S, HumanSeat);
            screen.SetPhase("");
            Area? dest = null;
            if (Me.IsAlive)
            {
                var here = Me.Area;
                var left = (Area)(((int)here + 3) % 4);
                var right = (Area)(((int)here + 1) % 4);
                bool immobile = Me.ImmobileRound == S.Round;
                var opts = new List<OptionModal.Option>
                {
                    new OptionModal.Option($"留まる（{Texts.Area(here)}）"),
                    new OptionModal.Option($"{Texts.Area(left)}へ  {Texts.AreaEffect(left)}", !immobile),
                    new OptionModal.Option($"{Texts.Area(right)}へ  {Texts.AreaEffect(right)}", !immobile),
                };
                int i = await modal.Choose($"R{S.Round} 移動", immobile ? "枷で動けない" : "隣のエリアへ1つ動ける（斜めは不可）", opts, cancellable: false);
                dest = i == 1 ? left : i == 2 ? right : (Area?)null;
            }
            var moves = S.Players.Where(p => p.IsAlive && p.IsBot).ToDictionary(p => p.Seat, p => brains[p.Seat].ChooseMove(S, p));
            moves[HumanSeat] = dest;
            engine.ResolveMoves(moves);
            await PlayEvents();
        }

        async Task GuardAndResolve()
        {
            screen.Refresh(S, HumanSeat);
            var arrows = lastArrows;
            foreach (var a in arrows)
                screen.AppendLog($"{Texts.Name(S, a.Attacker)} が {Texts.Name(S, a.Target)} を狙っている");

            var guards = new Dictionary<int, int>();
            foreach (var g in arrows.GroupBy(a => a.Target))
            {
                var target = S.P(g.Key);
                if (target.IsBot) guards[g.Key] = brains[g.Key].ChooseGuard(S, target, g.Select(a => a.Attacker));
            }
            var mine = arrows.Where(a => a.Target == HumanSeat).ToList();
            if (mine.Count > 0)
            {
                string who = string.Join("、", mine.Select(a => $"{Texts.Name(S, a.Attacker)}（EN{S.P(a.Attacker).PublicEN}）"));
                var labels = Enumerable.Range(0, Me.EN + 1).Select(v => v == 0 ? "ガードしない（0）" : $"EN {v} でガード").ToList();
                int g = await modal.Choose("攻撃された！", $"{who} から狙われている。\n攻撃の投入ENより大きくないと防げない（同じなら防げる）。ガードは全員に1回分で効く。",
                    labels, cancellable: false);
                guards[HumanSeat] = Math.Max(0, g);
            }

            screen.SetPhase("解決中…");
            engine.ResolveRound(guards);
            await PlayEvents();
            screen.SetPhase("");
        }

        async Task PlayEvents()
        {
            var log = S.Log;
            for (; logCursor < log.Count; logCursor++)
            {
                var line = Texts.Event(S, log[logCursor], HumanSeat);
                if (line == null) continue;
                screen.AppendLog(line);
                screen.Refresh(S, HumanSeat);
                if (log[logCursor].Type != GameEventType.RoundStarted) await Task.Delay(EventDelayMs);
            }
            screen.Refresh(S, HumanSeat);
        }

        async Task ShowResult()
        {
            var r = S.Result;
            bool won = r.Winners.Contains(HumanSeat);
            string head = r.WinningFaction.HasValue ? $"{Texts.FactionColored(r.WinningFaction.Value)}陣営の勝利" : "赤青の勝者なし";
            string reason = r.Reason == "final-judgement" ? "（最終審判）" : r.Reason == "wipeout" ? "（全滅）" : r.Reason == "green-only" ? "（緑だけが生き残った）" : "";
            var lines = S.Players.Select(p =>
            {
                string colors = string.Concat(p.Parts.Select(x => Texts.Chip(x.Color)));
                string mission = p.Faction == Faction.Green
                    ? " 使命: " + string.Join("/", p.Parts.Where(x => x.MissionId != null).Select(x =>
                        Catalog.Mission(x.MissionId).Name + (engine.IsMissionAchieved(p, x.MissionId) ? "○" : "×")))
                    : "";
                string mark = r.Winners.Contains(p.Seat) ? Ui.Colored("WIN", Ui.Accent) : "   ";
                return $"{mark} {p.Name} {Texts.FactionColored(p.Faction)} {colors}{(p.IsDown ? " ダウン" : $" HP{p.Hp}")}{mission}";
            });
            int pick = await modal.Choose(won ? "あなたの勝ち！" : "あなたの負け…", head + reason + "\n" + string.Join("\n", lines),
                new[] { "もう一度", "タイトルへ" }, cancellable: false, full: true);
            if (pick == 0) await RunMatch();
        }
    }
}
