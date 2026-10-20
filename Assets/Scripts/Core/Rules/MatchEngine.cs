using System;
using System.Collections.Generic;

namespace ChimeraGedon.Core
{
    /// <summary>
    /// 1試合のルールエンジン（純C#）。ホスト・ソロ・テスト・シミュレーターで共通に使う。
    /// 呼び出し順: Create → SubmitInitialAreas → [ResolveMoves → LockPlans → ResolveRound] をくり返し。
    /// 各フェイズの意味は docs/GAME_SPEC.md 4章。
    /// </summary>
    public sealed partial class MatchEngine
    {
        public MatchState State { get; }

        /// <summary>LockPlans で確定した計画（解決まで他人には見せない）。</summary>
        readonly Dictionary<int, ActionPlan> lockedPlans = new Dictionary<int, ActionPlan>();
        readonly List<PendingAttack> pendingAttacks = new List<PendingAttack>();

        sealed class PendingAttack
        {
            public int Attacker;
            public int Target;
            public int Bid;
            /// <summary>判定だけに足す値（カンガルー）。</summary>
            public int BidBonus;
            public bool Free;
        }

        MatchEngine(MatchState state)
        {
            State = state;
        }

        public static MatchEngine Create(int seed, IList<PlayerSetup> setups)
        {
            if (setups.Count < Balance.MinPlayers || setups.Count > Balance.MaxPlayers)
                throw new ArgumentException($"人数は{Balance.MinPlayers}〜{Balance.MaxPlayers}人");

            var state = new MatchState { Seed = seed, Rng = new Random(seed), Phase = MatchPhase.AreaSelect };
            for (int seat = 0; seat < setups.Count; seat++)
            {
                var s = setups[seat];
                var player = new PlayerState { Seat = seat, Name = s.Name, IsBot = s.IsBot, EN = Balance.StartEN };
                for (int slot = 0; slot < 3; slot++)
                {
                    var def = Catalog.Part(s.PartIds[slot]);
                    if ((int)def.Slot != slot)
                        throw new ArgumentException($"{s.Name}: {def.Id} は {(Slot)slot} に付けられない");
                    player.Parts[slot] = new PartInstance { PartId = def.Id };
                }
                player.PublicEN = player.EN;
                state.Players.Add(player);
            }

            GachaMachine.Assign(state);
            foreach (DeckType deck in Enum.GetValues(typeof(DeckType)))
            {
                var pile = new List<string>();
                foreach (var c in Catalog.Cards)
                    if (c.Deck == deck)
                        for (int i = 0; i < c.Count; i++) pile.Add(c.Id);
                GachaMachine.Shuffle(pile, state.Rng);
                state.DrawPiles[deck] = pile;
                state.DiscardPiles[deck] = new List<string>();
            }
            state.StartSeat = state.Rng.Next(setups.Count);

            var engine = new MatchEngine(state);
            engine.Emit(GameEventType.MatchStarted, detail: $"seed={seed}");
            return engine;
        }

        // ───────────────────────── フェイズ0: 初期エリア ─────────────────────────

        /// <summary>選ばなかった人はランダム。</summary>
        public void SubmitInitialAreas(IDictionary<int, Area> choices)
        {
            RequirePhase(MatchPhase.AreaSelect);
            foreach (var p in State.Players)
                p.Area = choices != null && choices.TryGetValue(p.Seat, out var a) ? a : (Area)State.Rng.Next(AreaMap.Count);
            BeginRound();
        }

        void BeginRound()
        {
            State.Round++;
            State.Phase = MatchPhase.Move;
            foreach (var p in State.Players)
            {
                p.RoundDamageTaken = 0;
                p.BarrierPool = 0;
                p.AmuletUsedThisRound = false;
                p.AttackersThisRound.Clear();
            }
            Emit(GameEventType.RoundStarted, value: State.Round);
        }

        // ───────────────────────── フェイズ1: 移動 ─────────────────────────

        /// <summary>moves[seat] = 移動先。含まれない／null／隣接でない／移動不可なら留まる。</summary>
        public void ResolveMoves(IDictionary<int, Area?> moves)
        {
            RequirePhase(MatchPhase.Move);
            foreach (var p in State.Alive())
            {
                if (moves == null || !moves.TryGetValue(p.Seat, out var dest) || dest == null) continue;
                if (p.ImmobileRound == State.Round) continue;
                if (!AreaMap.IsAdjacent(p.Area, dest.Value)) continue;
                MovePlayer(p, dest.Value);
            }
            foreach (var p in State.Players) p.PublicEN = p.EN;
            State.Phase = MatchPhase.Plan;
        }

        // ───────────────────────── フェイズ2: 計画 ─────────────────────────

        /// <summary>
        /// 全員の計画を確定し、ENを予約する。戻り値はガードフェイズで全員に見せる攻撃の矢印。
        /// </summary>
        public List<AttackArrow> LockPlans(IDictionary<int, ActionPlan> plans)
        {
            RequirePhase(MatchPhase.Plan);
            lockedPlans.Clear();
            pendingAttacks.Clear();

            foreach (var p in State.Alive())
            {
                ActionPlan plan = plans != null && plans.TryGetValue(p.Seat, out var given) && given != null ? given : ActionPlan.Charge();
                ResolvePendingDraw(p, plan.KeepIndex);
                plan = Sanitize(p, plan);
                lockedPlans[p.Seat] = plan;

                switch (plan.Action)
                {
                    case ActionType.Attack:
                        p.EN -= plan.Bid;
                        pendingAttacks.Add(new PendingAttack { Attacker = p.Seat, Target = plan.Target, Bid = plan.Bid });
                        break;
                    case ActionType.Draw:
                        p.EN -= DrawCostFor(p);
                        break;
                    case ActionType.Reveal:
                        var part = p.Part(plan.RevealSlot);
                        if (part.PartId == PartIds.Kangaroo && plan.Target >= 0)
                        {
                            p.EN -= plan.Bid;
                            pendingAttacks.Add(new PendingAttack { Attacker = p.Seat, Target = plan.Target, Bid = plan.Bid, BidBonus = 2 });
                        }
                        else if (part.PartId == PartIds.Cheetah && plan.Target >= 0)
                        {
                            pendingAttacks.Add(new PendingAttack { Attacker = p.Seat, Target = plan.Target, Bid = 2, Free = true });
                        }
                        break;
                }
            }

            var arrows = new List<AttackArrow>();
            foreach (var a in pendingAttacks)
            {
                arrows.Add(new AttackArrow { Attacker = a.Attacker, Target = a.Target });
                State.P(a.Target).AttackersThisRound.Add(a.Attacker);
                Emit(GameEventType.AttackDeclared, a.Attacker, a.Target);
            }
            State.Phase = MatchPhase.Guard;
            return arrows;
        }

        /// <summary>計画フェイズでガードに回せる EN の上限（予約後の残り）。</summary>
        public int GuardableEN(int seat) => State.P(seat).EN;

        public int DrawCostFor(PlayerState p) =>
            Balance.DrawCost - (p.Area == Area.Lab ? Balance.LabDrawDiscount : 0);

        ActionPlan Sanitize(PlayerState p, ActionPlan plan)
        {
            bool sealedNow = p.SealedRound == State.Round;

            if (plan.Card != null)
            {
                if (sealedNow || plan.Card.HandIndex < 0 || plan.Card.HandIndex >= p.Hand.Count)
                    plan.Card = null;
                else if (Catalog.Card(p.Hand[plan.Card.HandIndex]).IsEquipment && p.Equipment.Count >= Balance.EquipmentLimit)
                    plan.Card = null;
            }

            switch (plan.Action)
            {
                case ActionType.Attack:
                    if (plan.Bid < 1 || plan.Bid > p.EN || !CanBeAttacked(p, plan.Target, requireSameArea: true))
                        return Fallback(plan);
                    break;
                case ActionType.Draw:
                    if (p.Hand.Count >= Balance.HandLimit || p.EN < DrawCostFor(p))
                        return Fallback(plan);
                    break;
                case ActionType.Reveal:
                    if (sealedNow || p.Part(plan.RevealSlot).Revealed)
                        return Fallback(plan);
                    var id = p.Part(plan.RevealSlot).PartId;
                    if (id == PartIds.Kangaroo)
                    {
                        // 攻撃なしの公開も可。攻撃するなら通常攻撃と同じ条件。
                        if (plan.Target >= 0 && (plan.Bid < 1 || plan.Bid > p.EN || !CanBeAttacked(p, plan.Target, requireSameArea: true)))
                            plan.Target = -1;
                    }
                    else if (id == PartIds.Cheetah)
                    {
                        // 着地先にいる相手を狙う。到着後に相手が移動していれば不発。
                        if (plan.Target >= 0 && !CanBeAttacked(p, plan.Target, requireSameArea: false))
                            plan.Target = -1;
                    }
                    break;
            }
            return plan;
        }

        static ActionPlan Fallback(ActionPlan plan)
        {
            var charge = ActionPlan.Charge();
            charge.Card = plan.Card;
            charge.InterrogateSlot = plan.InterrogateSlot;
            charge.KeepIndex = plan.KeepIndex;
            return charge;
        }

        bool CanBeAttacked(PlayerState attacker, int targetSeat, bool requireSameArea)
        {
            if (targetSeat < 0 || targetSeat >= State.Players.Count || targetSeat == attacker.Seat) return false;
            var t = State.P(targetSeat);
            if (t.IsDown || t.UntargetableRound == State.Round) return false;
            return !requireSameArea || t.Area == attacker.Area;
        }

        // ───────────────────────── フェイズ3〜5: ガード・解決・終了 ─────────────────────────

        /// <summary>guards[seat] = ガードEN。狙われた人だけ意味がある。省略は0。</summary>
        public void ResolveRound(IDictionary<int, int> guards)
        {
            RequirePhase(MatchPhase.Guard);

            var guardValue = new Dictionary<int, int>();
            foreach (var a in pendingAttacks)
            {
                if (guardValue.ContainsKey(a.Target)) continue;
                var t = State.P(a.Target);
                int g = guards != null && guards.TryGetValue(a.Target, out var v) ? v : 0;
                g = Math.Max(0, Math.Min(g, t.EN));
                t.EN -= g;
                guardValue[a.Target] = g;
            }

            // 1. 公開スキル・カード（先手から時計回り）
            foreach (var seat in TurnOrder())
            {
                var p = State.P(seat);
                if (!lockedPlans.TryGetValue(seat, out var plan)) continue;
                if (p.InterrogateRound == State.Round) ForcedReveal(p, plan.InterrogateSlot);
                if (plan.Card != null) PlayCard(p, plan.Card);
                if (plan.Action == ActionType.Reveal) RevealAction(p, plan);
            }

            // 2〜3. 攻撃とガード（ダメージ上限は DealDamage 内で適用）
            foreach (var a in pendingAttacks) ResolveAttack(a, guardValue[a.Target]);

            // 4. チャージ・ドロー・エリア効果
            foreach (var seat in TurnOrder())
            {
                var p = State.P(seat);
                if (!lockedPlans.TryGetValue(seat, out var plan)) continue;
                if (plan.Action == ActionType.Charge) GainEN(p, Balance.ChargeEN);
                else if (plan.Action == ActionType.Draw) StartDraw(p, plan.Deck);
            }

            EndRound();
        }

        IEnumerable<int> TurnOrder()
        {
            int n = State.Players.Count;
            for (int i = 0; i < n; i++) yield return (State.StartSeat + i) % n;
        }

        void ResolveAttack(PendingAttack a, int guard)
        {
            var attacker = State.P(a.Attacker);
            var target = State.P(a.Target);
            if (attacker.Area != target.Area || target.UntargetableRound == State.Round)
            {
                Emit(GameEventType.AttackFizzled, a.Attacker, a.Target, a.Bid);
                return;
            }

            int power = a.Bid + a.BidBonus;
            bool hit = power > guard;
            Emit(GameEventType.AttackResolved, a.Attacker, a.Target, a.Bid, guard, hit ? "hit" : "blocked");
            if (!hit) return;

            int damage = a.Bid;
            if (attacker.Area == Area.Arena) damage += Balance.ArenaHitBonus;
            if (attacker.HasEquipment(CardIds.SpikeCollar)) damage += 1;
            DealDamage(target, damage, DamageKind.Attack, attacker.Seat);

            if (target.HasRevealed(PartIds.Porcupine))
                DealDamage(attacker, 2, DamageKind.Effect, target.Seat);
        }

        // ───────────────────────── 共通処理 ─────────────────────────

        /// <summary>被ダメージの軽減と1ラウンドの上限を適用して与える。戻り値は実際のダメージ。</summary>
        int DealDamage(PlayerState target, int amount, DamageKind kind, int source)
        {
            if (target.IsDown || amount <= 0) return 0;

            if (kind == DamageKind.Attack || kind == DamageKind.Effect)
            {
                if (amount >= 4 && !target.SlimeUsed && target.HasHidden(PartIds.Slime))
                {
                    amount = 2;
                    target.SlimeUsed = true;
                }
                if (State.Round <= target.HalfDamageUntilRound) amount /= 2;
                if (target.BarrierPool > 0)
                {
                    int absorbed = Math.Min(target.BarrierPool, amount);
                    target.BarrierPool -= absorbed;
                    amount -= absorbed;
                }
                if (amount > 0 && !target.AmuletUsedThisRound && target.HasEquipment(CardIds.Amulet))
                {
                    amount -= 1;
                    target.AmuletUsedThisRound = true;
                }
                amount = Math.Min(amount, Balance.RoundDamageCap - target.RoundDamageTaken);
                if (amount <= 0) return 0;
                target.RoundDamageTaken += amount;
            }

            target.Damage += amount;
            if (source >= 0 && source != target.Seat) target.LastDamageSource = source;
            Emit(GameEventType.Damaged, source, target.Seat, amount, (int)kind);
            return amount;
        }

        void Heal(PlayerState p, int amount)
        {
            if (p.IsDown || amount <= 0) return;
            int healed = Math.Min(amount, p.Damage);
            p.Damage -= healed;
            Emit(GameEventType.Healed, -1, p.Seat, healed);
        }

        void GainEN(PlayerState p, int amount)
        {
            int before = p.EN;
            p.EN = Math.Max(0, Math.Min(Balance.MaxEN, p.EN + amount));
            if (p.EN != before) Emit(GameEventType.EnergyChanged, -1, p.Seat, p.EN - before);
        }

        void MovePlayer(PlayerState p, Area dest)
        {
            if (p.Area == dest) return;
            p.Area = dest;
            Emit(GameEventType.Moved, p.Seat, -1, (int)dest);

            for (int i = State.Traps.Count - 1; i >= 0; i--)
            {
                var trap = State.Traps[i];
                if (trap.Area != dest || trap.OwnerSeat == p.Seat) continue;
                State.Traps.RemoveAt(i);
                if (p.HasHidden(PartIds.Frog))
                {
                    Emit(GameEventType.TrapTriggered, trap.OwnerSeat, p.Seat, 0, detail: "frog");
                    continue;
                }
                Emit(GameEventType.TrapTriggered, trap.OwnerSeat, p.Seat, 3);
                DealDamage(p, 3, DamageKind.Effect, trap.OwnerSeat);
            }
        }

        string DrawTop(DeckType deck)
        {
            var pile = State.DrawPiles[deck];
            if (pile.Count == 0)
            {
                var discard = State.DiscardPiles[deck];
                if (discard.Count == 0) return null;
                pile.AddRange(discard);
                discard.Clear();
                GachaMachine.Shuffle(pile, State.Rng);
            }
            string id = pile[pile.Count - 1];
            pile.RemoveAt(pile.Count - 1);
            return id;
        }

        /// <summary>ドロー: 上から2枚を見る。どちらを残すかは次の計画で選ぶ（解決中に入力待ちを作らないため）。</summary>
        void StartDraw(PlayerState p, DeckType deck)
        {
            var seen = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                var c = DrawTop(deck);
                if (c != null) seen.Add(c);
            }
            if (seen.Count == 0) return;
            p.PendingDraw = seen;
            p.PendingDrawDeck = deck;
            Emit(GameEventType.CardDrawn, p.Seat, -1, seen.Count, detail: string.Join(",", seen), visibleTo: p.Seat);
        }

        void ResolvePendingDraw(PlayerState p, int keepIndex)
        {
            if (p.PendingDraw == null) return;
            var seen = p.PendingDraw;
            p.PendingDraw = null;
            if (keepIndex < 0 || keepIndex >= seen.Count) keepIndex = 0;
            for (int i = 0; i < seen.Count; i++)
            {
                if (i == keepIndex && p.Hand.Count < Balance.HandLimit) p.Hand.Add(seen[i]);
                else State.DrawPiles[p.PendingDrawDeck].Insert(0, seen[i]); // 山札の下へ
            }
        }

        void Emit(GameEventType type, int actor = -1, int target = -1, int value = 0, int value2 = 0, string detail = null, int visibleTo = -1)
        {
            State.Log.Add(new GameEvent
            {
                Round = State.Round, Type = type, Actor = actor, Target = target,
                Value = value, Value2 = value2, Detail = detail, VisibleTo = visibleTo,
            });
        }

        void RequirePhase(MatchPhase phase)
        {
            if (State.Phase != phase)
                throw new InvalidOperationException($"フェイズが違う: 今は {State.Phase}、要求は {phase}");
        }
    }
}
