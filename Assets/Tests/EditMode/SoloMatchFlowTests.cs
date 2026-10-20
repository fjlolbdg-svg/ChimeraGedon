using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChimeraGedon.Core;
using ChimeraGedon.Game;
using NUnit.Framework;

namespace ChimeraGedon.Tests
{
    /// <summary>
    /// 画面なしでソロ対戦の進行（SoloMatch）を最後まで回す。人間の入力はランダム（ときどき「もどる」）。
    /// 例外・無限ループ・途中で止まる不具合を見つけるためのテスト。
    /// </summary>
    public class SoloMatchFlowTests
    {
        sealed class RandomChooser : IChooser
        {
            readonly Random rng;
            public int Calls;
            public RandomChooser(int seed) { rng = new Random(seed); }

            public Task<int> Choose(string title, string body, IList<OptionModal.Option> options, bool cancellable = true, bool full = false)
            {
                if (++Calls > 20000) throw new InvalidOperationException("選択が多すぎる（無限ループ？）: " + title);
                for (int i = 0; i < options.Count; i++)
                    if (options[i].Label == "タイトルへ") return Task.FromResult(i);
                if (cancellable && rng.NextDouble() < 0.1) return Task.FromResult(-1);
                var enabled = new List<int>();
                for (int i = 0; i < options.Count; i++) if (options[i].Enabled) enabled.Add(i);
                if (enabled.Count == 0)
                {
                    Assert.IsTrue(cancellable, "選べる選択肢がなく、もどるも無い: " + title);
                    return Task.FromResult(-1);
                }
                return Task.FromResult(enabled[rng.Next(enabled.Count)]);
            }
        }

        sealed class NullView : IMatchView
        {
            public int Lines;
            public void SetVisible(bool visible) { }
            public void SetPhase(string text) { }
            public void ClearLog() { }
            public void AppendLog(string line) { Lines++; }
            public void Refresh(MatchState state, int viewer) { }
        }

        [Test]
        public void SoloMatch_RunsToEndWithRandomInput([Values(4, 6, 8)] int players)
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var r = new Random(seed);
                var deck = new[]
                {
                    Catalog.PartsFor(Slot.Head)[r.Next(6)].Id,
                    Catalog.PartsFor(Slot.Torso)[r.Next(6)].Id,
                    Catalog.PartsFor(Slot.Legs)[r.Next(6)].Id,
                };
                var chooser = new RandomChooser(seed);
                var view = new NullView();
                var match = new SoloMatch(chooser, view, deck, players, seed, eventDelayMs: 0);
                var task = match.Run();
                Assert.IsTrue(task.IsCompleted, $"seed {seed}: 入力待ちのまま止まった");
                Assert.IsFalse(task.Result, "タイトルへを選んだので false");
                Assert.AreEqual(MatchPhase.Finished, match.Engine.State.Phase);
                Assert.Greater(view.Lines, 0);
            }
        }

        [Test]
        public void EventText_CoversEveryEventWithoutThrowing()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var e = BotSimulator.RunOne(seed, 4 + seed % 5);
                foreach (var ev in e.State.Log)
                    for (int viewer = 0; viewer < e.State.Players.Count; viewer++)
                        Texts.Event(e.State, ev, viewer);
            }
        }
    }
}
