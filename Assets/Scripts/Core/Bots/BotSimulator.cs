using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChimeraGedon.Core
{
    /// <summary>
    /// ボット同士の自動対戦でバランス指標を集計する（GAME_SPEC 11章の「検証で見る指標」）。
    /// Editor メニュー ChimeraGedon/Simulate Bot Matches から呼ぶ。
    /// </summary>
    public static class BotSimulator
    {
        public sealed class Stats
        {
            public int Matches;
            public int Players;
            public int TotalRounds;
            public int MaxRounds;
            public int RedWins, BlueWins, NoRedBlueWinner;
            public int GreenPlayersAtEnd, GreenMissionWins;
            public int Swaps, Reveals, Round1Downs, Doomed;
            public readonly Dictionary<string, int> EndReasons = new Dictionary<string, int>();
            public readonly Dictionary<string, int[]> MissionTally = new Dictionary<string, int[]>();
            public readonly Dictionary<string, int[]> PartWinTally = new Dictionary<string, int[]>();

            public string ToReport()
            {
                double m = Matches;
                var sb = new StringBuilder();
                sb.AppendLine($"=== {Matches} matches / {Players} players ===");
                sb.AppendLine($"平均ラウンド: {TotalRounds / m:F2}（最大 {MaxRounds}）");
                sb.AppendLine($"赤勝利 {RedWins / m:P1} / 青勝利 {BlueWins / m:P1} / 赤青なし {NoRedBlueWinner / m:P1}");
                sb.AppendLine($"終了時の緑プレイヤー: 1試合あたり {GreenPlayersAtEnd / m:F2}人、使命達成率 {(GreenPlayersAtEnd == 0 ? 0 : (double)GreenMissionWins / GreenPlayersAtEnd):P1}");
                sb.AppendLine($"交換: 1試合 {Swaps / m:F2}回 / 公開: 1人 {Reveals / (m * Players):F2}回 / 1R目のダウン: 1試合 {Round1Downs / m:F3}人 / 終末期(7R〜)のダウン: 1試合 {Doomed / m:F2}人");
                sb.AppendLine("終了理由: " + string.Join(", ", EndReasons.Select(kv => $"{kv.Key}={kv.Value / m:P1}")));
                sb.AppendLine("使命（達成/保持）: " + string.Join(", ", MissionTally.OrderBy(k => k.Key)
                    .Select(kv => $"{kv.Key} {Catalog.Mission(kv.Key).Name} {kv.Value[0]}/{kv.Value[1]}")));
                sb.AppendLine("部位の勝率（試合終了時に付けていた人）: " + string.Join(", ", PartWinTally.OrderBy(k => k.Key)
                    .Select(kv => $"{kv.Key} {(double)kv.Value[0] / Math.Max(1, kv.Value[1]):P0}")));
                return sb.ToString();
            }
        }

        public static Stats Run(int matches, int players, int seed = 1)
        {
            var stats = new Stats { Players = players };
            var rng = new Random(seed);
            for (int i = 0; i < matches; i++) RunOne(rng.Next(), players, stats);
            return stats;
        }

        public static MatchEngine RunOne(int seed, int players, Stats stats = null, int roundLimit = 30)
        {
            var rng = new Random(seed);
            var setups = new List<PlayerSetup>();
            for (int i = 0; i < players; i++)
            {
                var h = Catalog.PartsFor(Slot.Head);
                var t = Catalog.PartsFor(Slot.Torso);
                var l = Catalog.PartsFor(Slot.Legs);
                setups.Add(new PlayerSetup("Bot" + i, true, h[rng.Next(h.Count)].Id, t[rng.Next(t.Count)].Id, l[rng.Next(l.Count)].Id));
            }

            var engine = MatchEngine.Create(seed, setups);
            var s = engine.State;
            var brains = s.Players.Select(p => new BotBrain(seed * 31 + p.Seat)).ToList();

            engine.SubmitInitialAreas(s.Players.ToDictionary(p => p.Seat, p => brains[p.Seat].ChooseInitialArea(s, p)));
            while (s.Phase != MatchPhase.Finished && s.Round <= roundLimit)
            {
                engine.ResolveMoves(s.Players.Where(p => p.IsAlive).ToDictionary(p => p.Seat, p => brains[p.Seat].ChooseMove(s, p)));
                var arrows = engine.LockPlans(s.Players.Where(p => p.IsAlive).ToDictionary(p => p.Seat, p => brains[p.Seat].ChoosePlan(s, p, engine)));
                var guards = new Dictionary<int, int>();
                foreach (var g in arrows.GroupBy(a => a.Target))
                    guards[g.Key] = brains[g.Key].ChooseGuard(s, s.P(g.Key), g.Select(a => a.Attacker));
                engine.ResolveRound(guards);
            }

            if (stats != null && s.Result != null) Tally(engine, stats);
            return engine;
        }

        static void Tally(MatchEngine engine, Stats st)
        {
            var s = engine.State;
            var r = s.Result;
            st.Matches++;
            st.TotalRounds += s.Round;
            st.MaxRounds = Math.Max(st.MaxRounds, s.Round);
            if (r.WinningFaction == Faction.Red) st.RedWins++;
            else if (r.WinningFaction == Faction.Blue) st.BlueWins++;
            else st.NoRedBlueWinner++;
            st.EndReasons[r.Reason] = st.EndReasons.TryGetValue(r.Reason, out var c) ? c + 1 : 1;
            st.Swaps += s.Log.Count(e => e.Type == GameEventType.Swapped);
            st.Reveals += s.Players.Sum(p => p.RevealCount);
            st.Round1Downs += s.Log.Count(e => e.Type == GameEventType.Down && e.Round == 1);
            st.Doomed += s.Players.Count(p => p.IsDown && p.DownRound >= Balance.DoomStartRound);

            foreach (var p in s.Players)
            {
                bool won = r.Winners.Contains(p.Seat);
                foreach (var part in p.Parts)
                {
                    if (!st.PartWinTally.TryGetValue(part.PartId, out var pt)) st.PartWinTally[part.PartId] = pt = new int[2];
                    pt[1]++;
                    if (won) pt[0]++;
                }
                if (p.Faction != Faction.Green) continue;
                st.GreenPlayersAtEnd++;
                if (r.GreenMissionWinners.Contains(p.Seat)) st.GreenMissionWins++;
                foreach (var m in MatchEngine.ActiveMissions(p))
                {
                    if (!st.MissionTally.TryGetValue(m, out var t)) st.MissionTally[m] = t = new int[2];
                    t[1]++;
                    if (engine.IsMissionAchieved(p, m)) t[0]++;
                }
            }
        }
    }
}
