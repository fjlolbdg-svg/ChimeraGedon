using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChimeraGedon.Core;

namespace ChimeraGedon.Game
{
    /// <summary>
    /// ソロ対戦1試合の進行（自分＋ボット）。入力は IChooser、表示は IMatchView 経由なので、
    /// EditMode テストでは画面なしで最後まで自動進行できる（Tests/EditMode/SoloMatchFlowTests）。
    /// オンライン対戦を作るときは「入力を集めてエンジンに渡す」部分をネットワーク経由に差し替える。
    /// </summary>
    public sealed partial class SoloMatch
    {
        public const int HumanSeat = 0;

        static readonly string[] BotNames = { "ミケ", "ポチ", "ハチ", "ココ", "モモ", "タマ", "クロ" };

        readonly IChooser modal;
        readonly IMatchView screen;
        readonly string[] deck;
        readonly int playerCount;
        readonly int seed;
        /// <summary>ログ1行ごとの演出待ち（ミリ秒）。0 なら待たない。</summary>
        int eventDelayMs;
        bool askedSpectate;

        MatchEngine engine;
        List<BotBrain> brains;
        int logCursor;
        readonly List<string> history = new List<string>();

        public MatchEngine Engine => engine;
        MatchState S => engine.State;
        PlayerState Me => S.P(HumanSeat);

        public SoloMatch(IChooser chooser, IMatchView view, string[] deck, int playerCount, int seed, int eventDelayMs)
        {
            modal = chooser;
            screen = view;
            this.deck = deck;
            this.playerCount = playerCount;
            this.seed = seed;
            this.eventDelayMs = eventDelayMs;
        }

        // ───────────────────────── 試合 ─────────────────────────

        /// <summary>1試合を最後まで進める。戻り値 true は「もう一度」。</summary>
        public async Task<bool> Run()
        {
            var rng = new System.Random(seed);
            var setups = new List<PlayerSetup> { new PlayerSetup("あなた", false, deck[0], deck[1], deck[2]) };
            for (int i = 1; i < playerCount; i++)
            {
                var h = Catalog.PartsFor(Slot.Head); var t = Catalog.PartsFor(Slot.Torso); var l = Catalog.PartsFor(Slot.Legs);
                setups.Add(new PlayerSetup(BotNames[i - 1], true, h[rng.Next(h.Count)].Id, t[rng.Next(t.Count)].Id, l[rng.Next(l.Count)].Id));
            }
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
            return await ShowResult();
        }

        async Task ShowGacha()
        {
            screen.Refresh(S, HumanSeat);
            screen.SetPhase("ガチャマシンがまわる…");
            if (eventDelayMs > 0) await Task.Delay(700);
            var lines = Me.Parts.Select((p, i) => $"{Texts.Slot((Slot)i)}: {Texts.Chip(p.Color)} {p.Def.Name}").ToList();
            string missions = MatchEngine.ActiveMissions(Me).Any() || Me.Parts.Any(p => p.MissionId != null)
                ? "\n緑カプセルの使命: " + string.Join(" / ", Me.Parts.Where(p => p.MissionId != null).Select(p => Texts.MissionLine(p.MissionId)))
                : "";
            string goal = Me.Faction == Faction.Green ? "使命を達成しよう（赤青と一緒に勝てる）" :
                $"{Texts.Faction(Me.Faction == Faction.Red ? Faction.Blue : Faction.Red)}陣営を全員倒そう";
            await modal.Choose($"あなたは {Texts.FactionColored(Me.Faction)} 陣営",
                string.Join("\n", lines) + missions + $"\n目的: {goal}", new[] { "装着する" }, cancellable: false);
        }

        void Log(string line)
        {
            history.Add(line);
            screen.AppendLog(line);
        }

        async Task MovePhase()
        {
            if (!Me.IsAlive && !askedSpectate)
            {
                askedSpectate = true;
                int c = await modal.Choose("ダウンしてしまった…", "あなたの陣営が勝てば、ダウンしていても勝利です。", new[] { "観戦する", "結果まで早送り" }, cancellable: false);
                if (c == 1) eventDelayMs = 0;
            }
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
                Log($"{Texts.Name(S, a.Attacker)} が {Texts.Name(S, a.Target)} を狙っている");

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
                Log(line);
                screen.Refresh(S, HumanSeat);
                if (log[logCursor].Type != GameEventType.RoundStarted && eventDelayMs > 0) await Task.Delay(eventDelayMs);
            }
            screen.Refresh(S, HumanSeat);
        }

        async Task<bool> ShowResult()
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
            return pick == 0;
        }
    }
}
