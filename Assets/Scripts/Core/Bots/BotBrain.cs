using System;
using System.Collections.Generic;
using System.Linq;

namespace ChimeraGedon.Core
{
    /// <summary>
    /// 簡易ボット（TECH_SPEC 3-4）。ソロ・人数補充・切断時の引き継ぎ・バランス検証に使う。
    /// 見てよいのは「公開情報＋自分の情報＋自分に見えるイベント」だけ。他人の未公開部位は読まない。
    /// </summary>
    public sealed class BotBrain
    {
        readonly Random rng;

        public BotBrain(int seed)
        {
            rng = new Random(seed);
        }

        public Area ChooseInitialArea(MatchState s, PlayerState me) => (Area)rng.Next(AreaMap.Count);

        public Area? ChooseMove(MatchState s, PlayerState me)
        {
            if (me.ImmobileRound == s.Round) return null;
            if (me.Hp <= me.MaxHp / 2 && me.Area != Area.Nest && AreaMap.IsAdjacent(me.Area, Area.Nest)) return Area.Nest;
            if (rng.NextDouble() < 0.3) return (Area)(((int)me.Area + (rng.Next(2) == 0 ? 1 : 3)) % AreaMap.Count);
            return null;
        }

        public ActionPlan ChoosePlan(MatchState s, PlayerState me, MatchEngine engine)
        {
            var plan = new ActionPlan { KeepIndex = 0, InterrogateSlot = UnrevealedSlots(me).DefaultIfEmpty(Slot.Head).First() };
            var others = s.Players.Where(o => o != me && o.IsAlive).ToList();
            var sameArea = others.Where(o => o.Area == me.Area && o.UntargetableRound != s.Round).ToList();
            var enemies = sameArea.Where(o => Suspicion(s, me, o) < 0).ToList();

            if (me.Hand.Count > 0 && rng.NextDouble() < 0.6 && me.SealedRound != s.Round)
                plan.Card = PlanCard(s, me, others);

            var hidden = UnrevealedSlots(me).ToList();
            bool sealedNow = me.SealedRound == s.Round;
            if (hidden.Count > 0 && !sealedNow && rng.NextDouble() < 0.22 + 0.05 * s.Round / 2.0)
            {
                plan.Action = ActionType.Reveal;
                plan.RevealSlot = hidden[rng.Next(hidden.Count)];
                FillSkillParams(s, me, plan, others, sameArea, enemies);
                return plan;
            }

            if (sameArea.Count > 0 && me.EN >= 2 && rng.NextDouble() < (enemies.Count > 0 ? 0.85 : 0.55))
            {
                var pool = enemies.Count > 0 ? enemies : sameArea;
                var target = pool[rng.Next(pool.Count)];
                plan.Action = ActionType.Attack;
                plan.Target = target.Seat;
                plan.Bid = Math.Min(me.EN, Math.Max(1, Math.Min(target.PublicEN + 1, 1 + rng.Next(me.EN))));
                return plan;
            }

            if (me.Hand.Count + (plan.Card != null ? -1 : 0) < Balance.HandLimit && me.EN >= engine.DrawCostFor(me) && rng.NextDouble() < 0.35)
            {
                plan.Action = ActionType.Draw;
                plan.Deck = (DeckType)rng.Next(3);
                return plan;
            }

            plan.Action = ActionType.Charge;
            return plan;
        }

        /// <summary>攻撃者の公開EN（予約前）を見て、ある程度読んでガードする。</summary>
        public int ChooseGuard(MatchState s, PlayerState me, IEnumerable<int> attackers)
        {
            int maxThreat = attackers.Select(a => s.P(a).PublicEN).DefaultIfEmpty(0).Max();
            if (me.EN == 0 || rng.NextDouble() < 0.15) return 0;
            int g = Math.Min(me.EN, 1 + rng.Next(Math.Max(1, maxThreat)));
            return g;
        }

        /// <summary>正なら味方っぽい、負なら敵っぽい。公開済みの色と、自分に見えている解析結果だけを使う。</summary>
        public static int Suspicion(MatchState s, PlayerState me, PlayerState other)
        {
            var mine = me.Faction;
            int score = 0;
            foreach (var part in other.Parts)
                if (part.Revealed) score += part.PublicColor == mine ? 1 : -1;
            foreach (var e in s.Log)
            {
                if (e.Type != GameEventType.ScanResult || e.VisibleTo != me.Seat || e.Target != other.Seat) continue;
                if (e.Detail == "color" || e.Detail == "owl") score += (Faction)e.Value2 == mine ? 1 : -1;
                else if (e.Detail == "compat") score += e.Value == 1 ? 2 : -2;
            }
            return score;
        }

        static IEnumerable<Slot> UnrevealedSlots(PlayerState p)
        {
            for (int i = 0; i < 3; i++) if (!p.Parts[i].Revealed) yield return (Slot)i;
        }

        void FillSkillParams(MatchState s, PlayerState me, ActionPlan plan, List<PlayerState> others, List<PlayerState> sameArea, List<PlayerState> enemies)
        {
            PlayerState Pick(List<PlayerState> pool) => pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
            var near = others.Where(o => AreaMap.IsSameOrAdjacent(o.Area, me.Area)).ToList();
            var target = Pick(enemies) ?? Pick(sameArea) ?? Pick(near) ?? Pick(others);
            plan.Target = target?.Seat ?? -1;
            var second = others.Where(o => o != target).ToList();
            plan.Target2 = Pick(second)?.Seat ?? -1;
            var mySwappable = UnrevealedSlots(me).Where(sl => sl != plan.RevealSlot).ToList();
            plan.SwapSlot = mySwappable.Count > 0 ? mySwappable[rng.Next(mySwappable.Count)] : (Slot)rng.Next(3);
            plan.Deck = (DeckType)rng.Next(3);
            plan.DisplayColor = me.Faction == Faction.Red ? Faction.Blue : Faction.Red;

            switch (me.Part(plan.RevealSlot).PartId)
            {
                case PartIds.Cheetah:
                    var victim = Pick(others);
                    plan.Target = victim?.Seat ?? -1;
                    plan.DestArea = victim?.Area ?? me.Area;
                    break;
                case PartIds.Frog:
                    plan.DestArea = Area.Nest;
                    break;
                case PartIds.Horse:
                    if (target != null) plan.DestArea = (Area)(((int)target.Area + 1) % AreaMap.Count);
                    break;
                case PartIds.Kangaroo:
                    var k = Pick(enemies) ?? Pick(sameArea);
                    plan.Target = k?.Seat ?? -1;
                    plan.Bid = Math.Max(1, Math.Min(me.EN, 2));
                    break;
            }
        }

        CardPlay PlanCard(MatchState s, PlayerState me, List<PlayerState> others)
        {
            int idx = rng.Next(me.Hand.Count);
            var def = Catalog.Card(me.Hand[idx]);
            if (def.IsEquipment && me.Equipment.Count >= Balance.EquipmentLimit) return null;
            var play = new CardPlay { HandIndex = idx, Slot = (Slot)rng.Next(3), Slot2 = (Slot)rng.Next(3) };

            List<PlayerState> inRange;
            switch (def.Range)
            {
                case EffectRange.SameArea: inRange = others.Where(o => o.Area == me.Area).ToList(); break;
                case EffectRange.Adjacent: inRange = others.Where(o => AreaMap.IsSameOrAdjacent(o.Area, me.Area)).ToList(); break;
                default: inRange = others; break;
            }

            if (def.Deck == DeckType.Support)
            {
                play.Target = me.Seat;
                if (def.Id == CardIds.BloodGift)
                {
                    var friend = inRange.OrderByDescending(o => Suspicion(s, me, o)).FirstOrDefault();
                    if (friend == null || Suspicion(s, me, friend) <= 0) return null;
                    play.Target = friend.Seat;
                }
                return play;
            }

            if (def.Id == CardIds.Bomb)
            {
                play.Area = (Area)(((int)me.Area + 1) % AreaMap.Count);
                return play;
            }

            if (inRange.Count == 0 && def.Range != EffectRange.Self && def.Id != CardIds.Footprints) return null;
            var ordered = inRange.OrderBy(o => Suspicion(s, me, o)).ToList();
            play.Target = ordered.Count > 0 ? ordered[0].Seat : -1;
            play.Target2 = ordered.Count > 1 ? ordered[1].Seat : -1;
            return play;
        }
    }
}
