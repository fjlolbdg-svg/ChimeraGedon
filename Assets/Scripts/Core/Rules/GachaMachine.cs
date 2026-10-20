using System;
using System.Collections.Generic;

namespace ChimeraGedon.Core
{
    /// <summary>
    /// ホストのガチャマシン。どの部位がどの色のカプセルに入るかと、緑カプセルの使命を決める（GAME_SPEC 2-3〜2-5）。
    /// </summary>
    public static class GachaMachine
    {
        /// <summary>人数ごとの初期陣営の配分。6人の「+1」は Assign 内で赤か青に振る。</summary>
        public static (int red, int blue, int green, bool extraRedOrBlue) Composition(int players)
        {
            switch (players)
            {
                case 4: return (2, 2, 0, false);
                case 5: return (2, 2, 1, false);
                case 6: return (2, 2, 1, true);
                case 7: return (3, 3, 1, false);
                case 8: return (3, 3, 2, false);
                default: throw new ArgumentOutOfRangeException(nameof(players), players, "4〜8人");
            }
        }

        struct Template
        {
            public Faction A, B, C;
            public int Weight;
            public Template(Faction a, Faction b, Faction c, int w) { A = a; B = b; C = c; Weight = w; }
        }

        static readonly Template[] RedTemplates =
        {
            new Template(Faction.Red, Faction.Red, Faction.Red, 20),
            new Template(Faction.Red, Faction.Red, Faction.Blue, 40),
            new Template(Faction.Red, Faction.Red, Faction.Green, 40),
        };

        static readonly Template[] BlueTemplates =
        {
            new Template(Faction.Blue, Faction.Blue, Faction.Blue, 20),
            new Template(Faction.Blue, Faction.Blue, Faction.Red, 40),
            new Template(Faction.Blue, Faction.Blue, Faction.Green, 40),
        };

        static readonly Template[] GreenTemplates =
        {
            new Template(Faction.Green, Faction.Green, Faction.Red, 35),
            new Template(Faction.Green, Faction.Green, Faction.Blue, 35),
            new Template(Faction.Red, Faction.Blue, Faction.Green, 30),
        };

        /// <summary>全プレイヤーの部位に色と使命を割り当て、陣営人数の発表値を state に書く。</summary>
        public static void Assign(MatchState state)
        {
            var rng = state.Rng;
            int n = state.Players.Count;
            var (red, blue, green, extra) = Composition(n);
            if (extra)
            {
                if (rng.Next(2) == 0) red++; else blue++;
            }
            state.AnnouncedRed = red;
            state.AnnouncedBlue = blue;
            state.AnnouncedGreen = green;
            state.AnnouncedRedBlueRange = extra;

            var factions = new List<Faction>();
            for (int i = 0; i < red; i++) factions.Add(Faction.Red);
            for (int i = 0; i < blue; i++) factions.Add(Faction.Blue);
            for (int i = 0; i < green; i++) factions.Add(Faction.Green);
            Shuffle(factions, rng);

            var missions = new List<string>();
            for (int seat = 0; seat < n; seat++)
            {
                var colors = PickColors(factions[seat], rng);
                var player = state.Players[seat];
                for (int s = 0; s < 3; s++)
                {
                    player.Parts[s].Color = colors[s];
                    if (colors[s] == Faction.Green)
                        player.Parts[s].MissionId = NextMission(missions, rng);
                }
            }
        }

        /// <summary>初期陣営の型を重み付きで選び、3部位にランダムな順で並べる。</summary>
        public static Faction[] PickColors(Faction faction, Random rng)
        {
            var table = faction == Faction.Red ? RedTemplates : faction == Faction.Blue ? BlueTemplates : GreenTemplates;
            int total = 0;
            foreach (var t in table) total += t.Weight;
            int roll = rng.Next(total);
            var pick = table[0];
            foreach (var t in table)
            {
                if (roll < t.Weight) { pick = t; break; }
                roll -= t.Weight;
            }
            var colors = new List<Faction> { pick.A, pick.B, pick.C };
            Shuffle(colors, rng);
            return colors.ToArray();
        }

        /// <summary>同じ試合で使命は重複させない。足りなくなったら（緑カプセルが9個以上）重複を許して補充。</summary>
        static string NextMission(List<string> bag, Random rng)
        {
            if (bag.Count == 0)
            {
                foreach (var m in Catalog.Missions) bag.Add(m.Id);
                Shuffle(bag, rng);
            }
            string id = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            return id;
        }

        public static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
