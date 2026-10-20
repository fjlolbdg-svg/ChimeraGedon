using System.Collections.Generic;
using System.Linq;
using ChimeraGedon.Core;
using NUnit.Framework;

namespace ChimeraGedon.Tests
{
    /// <summary>GAME_SPEC のルールを1つずつ確かめる。数値は Balance の初期値前提。</summary>
    public class RulesTests
    {
        static MatchEngine NewMatch(int players = 4, int seed = 1)
        {
            var setups = new List<PlayerSetup>();
            for (int i = 0; i < players; i++)
                setups.Add(new PlayerSetup("P" + i, true, PartIds.Owl, PartIds.Bear, PartIds.Frog));
            return MatchEngine.Create(seed, setups);
        }

        static void SetColors(PlayerState p, Faction head, Faction torso, Faction legs)
        {
            p.Parts[0].Color = head; p.Parts[1].Color = torso; p.Parts[2].Color = legs;
            foreach (var part in p.Parts) part.MissionId = null;
        }

        /// <summary>全員を同じエリアに置き、移動フェイズを終えて計画フェイズにする。</summary>
        static void ToPlanPhase(MatchEngine e, Area area = Area.Altar)
        {
            e.SubmitInitialAreas(e.State.Players.ToDictionary(p => p.Seat, p => area));
            e.ResolveMoves(null);
        }

        [TestCase(Faction.Red, Faction.Red, Faction.Blue, Faction.Red)]
        [TestCase(Faction.Blue, Faction.Red, Faction.Blue, Faction.Blue)]
        [TestCase(Faction.Red, Faction.Blue, Faction.Green, Faction.Green)]
        [TestCase(Faction.Green, Faction.Green, Faction.Red, Faction.Green)]
        [TestCase(Faction.Red, Faction.Red, Faction.Green, Faction.Red)]
        [TestCase(Faction.Red, Faction.Red, Faction.Red, Faction.Red)]
        public void Faction_IsMajorityOrGreenWhenAllDifferent(Faction a, Faction b, Faction c, Faction expected)
        {
            var parts = new[] { new PartInstance { Color = a }, new PartInstance { Color = b }, new PartInstance { Color = c } };
            Assert.AreEqual(expected, FactionRules.Compute(parts));
        }

        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void Gacha_InitialFactionsMatchAnnouncement(int n)
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var s = NewMatch(n, seed).State;
                Assert.AreEqual(s.AnnouncedRed, s.Players.Count(p => p.Faction == Faction.Red));
                Assert.AreEqual(s.AnnouncedBlue, s.Players.Count(p => p.Faction == Faction.Blue));
                Assert.AreEqual(s.AnnouncedGreen, s.Players.Count(p => p.Faction == Faction.Green));
                foreach (var part in s.Players.SelectMany(p => p.Parts))
                    Assert.AreEqual(part.Color == Faction.Green, part.MissionId != null, "緑カプセルにだけ使命が入る");
            }
        }

        [Test]
        public void Gacha_IsDeterministicForSeed()
        {
            var a = NewMatch(6, 42).State;
            var b = NewMatch(6, 42).State;
            for (int i = 0; i < 6; i++)
                for (int s = 0; s < 3; s++)
                    Assert.AreEqual(a.Players[i].Parts[s].Color, b.Players[i].Parts[s].Color);
        }

        [Test]
        public void MaxHp_IsSumOfParts()
        {
            var p = NewMatch().State.P(0);
            Assert.AreEqual(3 + 5 + 3, p.MaxHp);
            Assert.AreEqual(p.MaxHp, p.Hp);
        }

        [Test]
        public void Attack_HitsWhenBidBeatsGuard_AndBothPay()
        {
            var e = NewMatch();
            ToPlanPhase(e);
            var plans = new Dictionary<int, ActionPlan> { [0] = new ActionPlan { Action = ActionType.Attack, Target = 1, Bid = 3 } };
            var arrows = e.LockPlans(plans);
            Assert.AreEqual(1, arrows.Count);
            e.ResolveRound(new Dictionary<int, int> { [1] = 2 });

            var a = e.State.P(0); var d = e.State.P(1);
            Assert.AreEqual(3, d.Damage);
            Assert.AreEqual(Balance.StartEN - 3 + Balance.RoundIncomeEN, a.EN);
            Assert.AreEqual(Balance.StartEN - 2 + Balance.ChargeEN + Balance.RoundIncomeEN, d.EN, "防御側は計画なし＝チャージ");
        }

        [Test]
        public void Attack_TieIsBlocked()
        {
            var e = NewMatch();
            ToPlanPhase(e);
            e.LockPlans(new Dictionary<int, ActionPlan> { [0] = new ActionPlan { Action = ActionType.Attack, Target = 1, Bid = 2 } });
            e.ResolveRound(new Dictionary<int, int> { [1] = 2 });
            Assert.AreEqual(0, e.State.P(1).Damage);
        }

        [Test]
        public void Guard_IsPaidOnceAgainstAllAttackers()
        {
            var e = NewMatch();
            ToPlanPhase(e);
            e.LockPlans(new Dictionary<int, ActionPlan>
            {
                [0] = new ActionPlan { Action = ActionType.Attack, Target = 3, Bid = 2 },
                [1] = new ActionPlan { Action = ActionType.Attack, Target = 3, Bid = 3 },
            });
            e.ResolveRound(new Dictionary<int, int> { [3] = 2 });
            var d = e.State.P(3);
            Assert.AreEqual(3, d.Damage, "2は防いで3だけ命中");
            Assert.AreEqual(Balance.StartEN - 2 + Balance.ChargeEN + Balance.RoundIncomeEN, d.EN, "ガードの支払いは1回分（計画なし＝チャージ）");
        }

        [Test]
        public void Damage_IsCappedPerRound()
        {
            var e = NewMatch(5);
            foreach (var p in e.State.Players) p.EN = 5;
            ToPlanPhase(e);
            e.LockPlans(new Dictionary<int, ActionPlan>
            {
                [0] = new ActionPlan { Action = ActionType.Attack, Target = 4, Bid = 4 },
                [1] = new ActionPlan { Action = ActionType.Attack, Target = 4, Bid = 4 },
            });
            e.ResolveRound(null);
            Assert.AreEqual(Balance.RoundDamageCap, e.State.P(4).Damage);
        }

        [Test]
        public void Attack_RequiresSameArea()
        {
            var e = NewMatch();
            e.SubmitInitialAreas(new Dictionary<int, Area> { [0] = Area.Altar, [1] = Area.Lab, [2] = Area.Arena, [3] = Area.Nest });
            e.ResolveMoves(null);
            e.LockPlans(new Dictionary<int, ActionPlan> { [0] = new ActionPlan { Action = ActionType.Attack, Target = 1, Bid = 2 } });
            e.ResolveRound(null);
            Assert.AreEqual(0, e.State.P(1).Damage);
            Assert.AreEqual(Balance.StartEN + Balance.ChargeEN + Balance.RoundIncomeEN, e.State.P(0).EN, "不正な攻撃はチャージ扱い");
        }

        [Test]
        public void Move_OnlyToAdjacentArea()
        {
            var e = NewMatch();
            e.SubmitInitialAreas(e.State.Players.ToDictionary(p => p.Seat, p => Area.Altar));
            e.ResolveMoves(new Dictionary<int, Area?> { [0] = Area.Lab, [1] = Area.Arena, [2] = Area.Nest });
            Assert.AreEqual(Area.Lab, e.State.P(0).Area);
            Assert.AreEqual(Area.Altar, e.State.P(1).Area, "斜め（祭壇→闘技場）は移動できない");
            Assert.AreEqual(Area.Nest, e.State.P(2).Area);
        }

        [Test]
        public void Reveal_ChangesNothingButShowsColor_AndBearHeals()
        {
            var e = NewMatch();
            ToPlanPhase(e, Area.Lab);
            e.State.P(0).Damage = 5;
            e.LockPlans(new Dictionary<int, ActionPlan> { [0] = new ActionPlan { Action = ActionType.Reveal, RevealSlot = Slot.Torso } });
            e.ResolveRound(null);
            var p = e.State.P(0);
            Assert.IsTrue(p.Part(Slot.Torso).Revealed);
            Assert.AreEqual(1, p.Damage);
        }

        [Test]
        public void Swap_MovesColorAndChangesFaction()
        {
            var setups = new List<PlayerSetup>
            {
                new PlayerSetup("A", true, PartIds.Chameleon, PartIds.Bear, PartIds.Frog),
                new PlayerSetup("B", true, PartIds.Owl, PartIds.Turtle, PartIds.Horse),
                new PlayerSetup("C", true, PartIds.Owl, PartIds.Bear, PartIds.Frog),
                new PlayerSetup("D", true, PartIds.Owl, PartIds.Bear, PartIds.Frog),
            };
            var e = MatchEngine.Create(1, setups);
            var a = e.State.P(0); var b = e.State.P(1);
            SetColors(a, Faction.Red, Faction.Red, Faction.Blue);
            SetColors(b, Faction.Blue, Faction.Blue, Faction.Red);
            ToPlanPhase(e);

            e.LockPlans(new Dictionary<int, ActionPlan>
            {
                [0] = new ActionPlan { Action = ActionType.Reveal, RevealSlot = Slot.Head, Target = 1, SwapSlot = Slot.Torso },
            });
            e.ResolveRound(null);

            Assert.AreEqual(PartIds.Turtle, a.Part(Slot.Torso).PartId);
            Assert.AreEqual(PartIds.Bear, b.Part(Slot.Torso).PartId);
            Assert.AreEqual(Faction.Blue, a.Faction, "赤赤青 → 赤青青");
            Assert.AreEqual(Faction.Red, b.Faction, "青青赤 → 青赤赤");
            Assert.IsTrue(e.State.Log.Any(ev => ev.Type == GameEventType.FactionChanged && ev.VisibleTo == 0));
        }

        [Test]
        public void Swap_NeverDownsByMaxHpDrop()
        {
            var setups = new List<PlayerSetup>
            {
                new PlayerSetup("A", true, PartIds.Owl, PartIds.Bee, PartIds.Frog),
                new PlayerSetup("B", true, PartIds.Owl, PartIds.Turtle, PartIds.Frog),
                new PlayerSetup("C", true, PartIds.Owl, PartIds.Bear, PartIds.Frog),
                new PlayerSetup("D", true, PartIds.Owl, PartIds.Bear, PartIds.Frog),
            };
            var e = MatchEngine.Create(1, setups);
            var b = e.State.P(1);
            b.Damage = b.MaxHp - 2; // HP2
            ToPlanPhase(e);
            e.State.P(0).Hand.Add(CardIds.Transplant);
            e.LockPlans(new Dictionary<int, ActionPlan>
            {
                [0] = new ActionPlan { Card = new CardPlay { HandIndex = 0, Target = 1, Slot = Slot.Torso } },
            });
            e.ResolveRound(null);
            Assert.IsFalse(b.IsDown);
            Assert.GreaterOrEqual(b.Hp, 1);
        }

        [Test]
        public void Scan_IsPrivate_AndFoxCanLie()
        {
            var setups = new List<PlayerSetup>
            {
                new PlayerSetup("A", true, PartIds.Owl, PartIds.Bear, PartIds.Frog),
                new PlayerSetup("B", true, PartIds.Fox, PartIds.Bear, PartIds.Frog),
                new PlayerSetup("C", true, PartIds.Owl, PartIds.Bear, PartIds.Frog),
                new PlayerSetup("D", true, PartIds.Owl, PartIds.Bear, PartIds.Frog),
            };
            var e = MatchEngine.Create(1, setups);
            var b = e.State.P(1);
            SetColors(b, Faction.Red, Faction.Red, Faction.Blue);
            b.LieColor = Faction.Blue;
            ToPlanPhase(e);
            e.State.P(0).Hand.Add(CardIds.Clairvoyance);
            e.LockPlans(new Dictionary<int, ActionPlan>
            {
                [0] = new ActionPlan { Card = new CardPlay { HandIndex = 0, Target = 1, Slot = Slot.Torso } },
            });
            e.ResolveRound(null);
            var result = e.State.Log.Single(ev => ev.Type == GameEventType.ScanResult);
            Assert.AreEqual(0, result.VisibleTo);
            Assert.AreEqual((int)Faction.Blue, result.Value2, "本当は赤だが、キツネの潜伏スキルで青と見せる");
            Assert.AreEqual(Balance.LiarUses - 1, b.LiarUsesLeft);
        }

        [Test]
        public void Victory_RedWinsWhenNoBlueAlive()
        {
            var e = NewMatch();
            var s = e.State;
            SetColors(s.P(0), Faction.Red, Faction.Red, Faction.Red);
            SetColors(s.P(1), Faction.Red, Faction.Red, Faction.Red);
            SetColors(s.P(2), Faction.Blue, Faction.Blue, Faction.Blue);
            SetColors(s.P(3), Faction.Blue, Faction.Blue, Faction.Blue);
            s.P(2).Damage = 100;
            s.P(3).Damage = 100;
            ToPlanPhase(e);
            e.LockPlans(null);
            e.ResolveRound(null);
            Assert.AreEqual(MatchPhase.Finished, s.Phase);
            Assert.AreEqual(Faction.Red, s.Result.WinningFaction);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, s.Result.Winners);
        }

        [Test]
        public void Victory_GreenWinsAlongsideWithMission()
        {
            var e = NewMatch(5);
            var s = e.State;
            SetColors(s.P(0), Faction.Red, Faction.Red, Faction.Red);
            SetColors(s.P(1), Faction.Red, Faction.Red, Faction.Red);
            SetColors(s.P(2), Faction.Blue, Faction.Blue, Faction.Blue);
            SetColors(s.P(3), Faction.Blue, Faction.Blue, Faction.Blue);
            SetColors(s.P(4), Faction.Green, Faction.Green, Faction.Red);
            s.P(4).Parts[0].MissionId = MissionIds.Immortal;
            s.P(2).Damage = 100;
            s.P(3).Damage = 100;
            ToPlanPhase(e);
            e.LockPlans(null);
            e.ResolveRound(null);
            Assert.AreEqual(Faction.Red, s.Result.WinningFaction);
            CollectionAssert.Contains(s.Result.Winners, 4, "HP5以上で生存 → 不死身達成");
        }

        [Test]
        public void Match_AlwaysEndsByFinalRound()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var e = BotSimulator.RunOne(seed, 4 + seed % 5);
                Assert.AreEqual(MatchPhase.Finished, e.State.Phase, "seed " + seed);
                Assert.LessOrEqual(e.State.Round, Balance.FinalRound);
            }
        }

        [Test]
        public void Doom_Schedule()
        {
            Assert.AreEqual(0, Balance.DoomDamage(6));
            Assert.AreEqual(1, Balance.DoomDamage(7));
            Assert.AreEqual(1, Balance.DoomDamage(8));
            Assert.AreEqual(2, Balance.DoomDamage(9));
            Assert.AreEqual(2, Balance.DoomDamage(10));
        }
    }
}
