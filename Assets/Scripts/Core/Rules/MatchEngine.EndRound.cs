using System.Collections.Generic;
using System.Linq;

namespace ChimeraGedon.Core
{
    /// <summary>ラウンド終了処理・ダウン・終末・勝利判定（GAME_SPEC 2-2, 4-2 手順5, 4-5）。</summary>
    public sealed partial class MatchEngine
    {
        void EndRound()
        {
            foreach (var p in State.Alive()) GainEN(p, Balance.RoundIncomeEN);
            foreach (var p in State.Alive())
                if (p.Area == Area.Nest) Heal(p, Balance.NestHeal);

            foreach (var p in State.Alive())
            {
                if (p.PoisonRounds <= 0) continue;
                p.PoisonRounds--;
                DealDamage(p, 1, DamageKind.Poison, p.PoisonSource);
            }

            var downedThisRound = new List<PlayerState>();
            DownCheck(downedThisRound);

            // 全滅したときの判定用に、終末ダメージ前の HP 合計を取っておく
            int redHpBefore = State.Alive().Where(p => p.Faction == Faction.Red).Sum(p => p.Hp);
            int blueHpBefore = State.Alive().Where(p => p.Faction == Faction.Blue).Sum(p => p.Hp);

            int doom = Balance.DoomDamage(State.Round);
            if (doom > 0)
            {
                Emit(GameEventType.Doom, value: doom);
                foreach (var p in State.Alive().ToList()) DealDamage(p, doom, DamageKind.Doom, -1);
                DownCheck(downedThisRound);
            }

            foreach (var p in downedThisRound)
                if (p.AttackersThisRound.Any(a => downedThisRound.Any(d => d.Seat == a)))
                    p.DragAlongAchieved = true;

            if (CheckVictory(redHpBefore, blueHpBefore))
            {
                State.Phase = MatchPhase.Finished;
                Emit(GameEventType.MatchEnded, value: State.Result.WinningFaction.HasValue ? (int)State.Result.WinningFaction.Value : -1,
                    detail: State.Result.Reason);
                return;
            }

            int n = State.Players.Count;
            State.StartSeat = (State.StartSeat + 1) % n;
            BeginRound();
        }

        void DownCheck(List<PlayerState> downedThisRound)
        {
            foreach (var p in State.Players)
            {
                if (p.IsDown || p.Hp > 0) continue;
                p.FrozenFaction = FactionRules.Compute(p.Parts);
                p.IsDown = true;
                p.DownRound = State.Round;
                p.EquipmentAtDown = p.Equipment.Count;
                foreach (var e in p.Equipment) State.DiscardPiles[Catalog.Card(e).Deck].Add(e);
                p.Equipment.Clear();
                foreach (var c in p.Hand) State.DiscardPiles[Catalog.Card(c).Deck].Add(c);
                p.Hand.Clear();
                p.PendingDraw = null;
                foreach (var part in p.Parts) part.Revealed = true;

                if (p.LastDamageSource >= 0 && p.LastDamageSource != p.Seat)
                    State.P(p.LastDamageSource).Kills++;
                downedThisRound.Add(p);
                Emit(GameEventType.Down, p.LastDamageSource, p.Seat, (int)p.FrozenFaction);
            }
        }

        bool CheckVictory(int redHpBefore, int blueHpBefore)
        {
            var alive = State.Alive().ToList();
            bool redAlive = alive.Any(p => p.Faction == Faction.Red);
            bool blueAlive = alive.Any(p => p.Faction == Faction.Blue);
            bool greenAlive = alive.Any(p => p.Faction == Faction.Green);

            var result = new MatchResult();
            if (alive.Count == 0)
            {
                if (redHpBefore > blueHpBefore) result.WinningFaction = Faction.Red;
                else if (blueHpBefore > redHpBefore) result.WinningFaction = Faction.Blue;
                result.Reason = "wipeout";
            }
            else if (!redAlive && !blueAlive && greenAlive)
            {
                result.Reason = "green-only";
            }
            else if (redAlive && !blueAlive)
            {
                result.WinningFaction = Faction.Red;
                result.Reason = "blue-eliminated";
            }
            else if (blueAlive && !redAlive)
            {
                result.WinningFaction = Faction.Blue;
                result.Reason = "red-eliminated";
            }
            else if (State.Round >= Balance.FinalRound)
            {
                // 最終審判: 生存人数 → 生存者の HP 合計 の順で比べる。緑は使命で判定。
                int redCount = alive.Count(p => p.Faction == Faction.Red);
                int blueCount = alive.Count(p => p.Faction == Faction.Blue);
                int redHp = alive.Where(p => p.Faction == Faction.Red).Sum(p => p.Hp);
                int blueHp = alive.Where(p => p.Faction == Faction.Blue).Sum(p => p.Hp);
                if (redCount != blueCount) result.WinningFaction = redCount > blueCount ? Faction.Red : Faction.Blue;
                else if (redHp != blueHp) result.WinningFaction = redHp > blueHp ? Faction.Red : Faction.Blue;
                result.Reason = "final-judgement";
            }
            else
            {
                return false;
            }

            foreach (var p in State.Players)
            {
                var f = p.Faction;
                if (result.WinningFaction.HasValue && f == result.WinningFaction.Value)
                    result.Winners.Add(p.Seat);
                else if (f == Faction.Green)
                {
                    bool won = (result.Reason == "green-only" && p.IsAlive) || ActiveMissions(p).Any(m => IsMissionAchieved(p, m));
                    if (won)
                    {
                        result.Winners.Add(p.Seat);
                        result.GreenMissionWinners.Add(p.Seat);
                    }
                }
            }
            State.Result = result;
            return true;
        }

        /// <summary>緑陣営のときだけ、持っている緑カプセルの使命が有効（GAME_SPEC 2-5）。</summary>
        public static IEnumerable<string> ActiveMissions(PlayerState p)
        {
            if (p.Faction != Faction.Green) yield break;
            foreach (var part in p.Parts)
                if (part.MissionId != null) yield return part.MissionId;
        }

        public bool IsMissionAchieved(PlayerState p, string missionId)
        {
            bool alive = p.IsAlive;
            switch (missionId)
            {
                case MissionIds.Immortal: return alive && p.Hp >= 5;
                case MissionIds.Collector: return (alive ? p.Equipment.Count : p.EquipmentAtDown) >= 2;
                case MissionIds.Parasite: return alive && p.SwapCount >= 2;
                case MissionIds.Hermit: return alive && p.Parts.Count(x => x.Revealed) <= 1;
                case MissionIds.Complete: return alive && p.Parts.All(x => x.Revealed);
                case MissionIds.Balance:
                    int r = State.Players.Count(x => x.IsDown && x.FrozenFaction == Faction.Red);
                    int b = State.Players.Count(x => x.IsDown && x.FrozenFaction == Faction.Blue);
                    return r == b && r > 0;
                case MissionIds.Opportunist: return alive && p.Kills >= 2;
                case MissionIds.DragAlong: return p.DragAlongAchieved;
                default: return false;
            }
        }
    }
}
