using System;
using System.Collections.Generic;

namespace ChimeraGedon.Core
{
    /// <summary>部位の公開スキルとカードの効果。データ表は docs/PARTS_AND_CARDS.md。</summary>
    public sealed partial class MatchEngine
    {
        // ───────────────────────── 公開 ─────────────────────────

        void RevealAction(PlayerState p, ActionPlan plan)
        {
            var part = p.Part(plan.RevealSlot);
            if (part.Revealed) return; // 尋問などで先に公開済み

            if (part.PartId == PartIds.Ghost)
            {
                part.HasDisplayColor = true;
                part.DisplayColor = plan.DisplayColor;
            }
            MarkRevealed(p, part, plan.RevealSlot);
            if (p.Area == Area.Altar) GainEN(p, Balance.AltarRevealEN);
            ApplyRevealSkill(p, part.PartId, plan);
        }

        void ForcedReveal(PlayerState p, Slot preferred)
        {
            Slot? slot = null;
            if (!p.Part(preferred).Revealed) slot = preferred;
            else
                for (int s = 0; s < 3; s++)
                    if (!p.Parts[s].Revealed) { slot = (Slot)s; break; }
            if (slot == null) return;
            MarkRevealed(p, p.Part(slot.Value), slot.Value, forced: true);
        }

        void MarkRevealed(PlayerState p, PartInstance part, Slot slot, bool forced = false)
        {
            part.Revealed = true;
            p.RevealCount++;
            Emit(GameEventType.PartRevealed, p.Seat, -1, (int)slot, (int)part.PublicColor,
                forced ? $"{part.PartId}:forced" : part.PartId);
        }

        void ApplyRevealSkill(PlayerState p, string partId, ActionPlan plan)
        {
            var def = Catalog.Part(partId);
            PlayerState target = plan.Target >= 0 && plan.Target < State.Players.Count ? State.P(plan.Target) : null;

            bool TargetOk() => target != null && target != p && InRange(p, target, def.Range);

            switch (partId)
            {
                case PartIds.Owl:
                    if (!TargetOk()) { Fizzle(p, partId); return; }
                    int shown = 0;
                    for (int s = 0; s < 3 && shown < 2; s++)
                    {
                        var tp = target.Parts[s];
                        if (tp.Revealed) continue;
                        Emit(GameEventType.ScanResult, p.Seat, target.Seat, s, (int)tp.Color, "owl", p.Seat);
                        shown++;
                    }
                    break;

                case PartIds.Chameleon:
                    if (!TargetOk()) { Fizzle(p, partId); return; }
                    Swap(p, p, target, plan.SwapSlot);
                    break;

                case PartIds.Fox:
                    if (!TargetOk() || target.Hand.Count == 0) { Fizzle(p, partId); return; }
                    int idx = State.Rng.Next(target.Hand.Count);
                    string stolen = target.Hand[idx];
                    target.Hand.RemoveAt(idx);
                    if (p.Hand.Count < Balance.HandLimit) p.Hand.Add(stolen);
                    else State.DiscardPiles[Catalog.Card(stolen).Deck].Add(stolen);
                    Emit(GameEventType.CardStolen, p.Seat, target.Seat);
                    Emit(GameEventType.ScanResult, p.Seat, target.Seat, detail: "stolen:" + stolen, visibleTo: p.Seat);
                    break;

                case PartIds.Lion:
                    foreach (var o in State.Alive())
                        if (o != p && o.Area == p.Area) DealDamage(o, 2, DamageKind.Effect, p.Seat);
                    break;

                case PartIds.Octopus:
                    var t2 = plan.Target2 >= 0 && plan.Target2 < State.Players.Count ? State.P(plan.Target2) : null;
                    if (target == null || t2 == null || target == p || t2 == p || target == t2) { Fizzle(p, partId); return; }
                    Swap(p, target, t2, plan.SwapSlot);
                    break;

                case PartIds.Snake:
                    if (!TargetOk()) { Fizzle(p, partId); return; }
                    target.SealedRound = State.Round + 1;
                    Emit(GameEventType.StatusApplied, p.Seat, target.Seat, detail: "sealed");
                    break;

                case PartIds.Turtle:
                    p.HalfDamageUntilRound = State.Round + 1;
                    Emit(GameEventType.StatusApplied, p.Seat, p.Seat, detail: "half");
                    break;

                case PartIds.Bear:
                    Heal(p, 4);
                    break;

                case PartIds.Bee:
                    if (!TargetOk()) { Fizzle(p, partId); return; }
                    DealDamage(target, 3, DamageKind.Effect, p.Seat);
                    break;

                case PartIds.Porcupine:
                case PartIds.Ghost:
                    break; // 常時効果・表示色は RevealAction / ResolveAttack で処理

                case PartIds.Slime:
                    Heal(p, 2);
                    if (p.Hand.Count < Balance.HandLimit)
                    {
                        var c = DrawTop(plan.Deck);
                        if (c != null)
                        {
                            p.Hand.Add(c);
                            Emit(GameEventType.CardDrawn, p.Seat, -1, 1, detail: c, visibleTo: p.Seat);
                        }
                    }
                    break;

                case PartIds.Cheetah:
                    MovePlayer(p, plan.DestArea); // 攻撃は pendingAttacks 側で判定
                    break;

                case PartIds.Frog:
                    MovePlayer(p, plan.DestArea);
                    p.UntargetableRound = State.Round + 1;
                    Emit(GameEventType.StatusApplied, p.Seat, p.Seat, detail: "untargetable");
                    break;

                case PartIds.Scorpion:
                    if (!TargetOk()) { Fizzle(p, partId); return; }
                    target.PoisonRounds = 3;
                    target.PoisonSource = p.Seat;
                    Emit(GameEventType.StatusApplied, p.Seat, target.Seat, detail: "poison");
                    break;

                case PartIds.Horse:
                    if (!TargetOk() || !AreaMap.IsAdjacent(target.Area, plan.DestArea)) { Fizzle(p, partId); return; }
                    MovePlayer(target, plan.DestArea);
                    target.ImmobileRound = State.Round + 1;
                    Emit(GameEventType.StatusApplied, p.Seat, target.Seat, detail: "kicked");
                    break;

                case PartIds.Spider:
                    State.Traps.Add(new Trap { Area = p.Area, OwnerSeat = p.Seat });
                    Emit(GameEventType.StatusApplied, p.Seat, -1, (int)p.Area, detail: "trap", visibleTo: p.Seat);
                    break;

                case PartIds.Kangaroo:
                    break; // 攻撃は pendingAttacks 側で判定

                default:
                    throw new InvalidOperationException("未実装の部位: " + partId);
            }
        }

        void Fizzle(PlayerState p, string id) => Emit(GameEventType.SkillFizzled, p.Seat, detail: id);

        bool InRange(PlayerState user, PlayerState target, EffectRange range)
        {
            if (target.IsDown) return false;
            switch (range)
            {
                case EffectRange.Self: return target == user;
                case EffectRange.SameArea: return target.Area == user.Area;
                case EffectRange.Adjacent: return AreaMap.IsSameOrAdjacent(user.Area, target.Area);
                default: return true;
            }
        }

        // ───────────────────────── 交換 ─────────────────────────

        /// <summary>
        /// a と b の同じスロットの未公開部位を入れ替える（GAME_SPEC 7章）。
        /// initiator 以外の当事者は、タコの頭（潜伏）で1回だけ無効にできる。
        /// </summary>
        bool Swap(PlayerState initiator, PlayerState a, PlayerState b, Slot slot)
        {
            var pa = a.Part(slot);
            var pb = b.Part(slot);
            if (a.IsDown || b.IsDown || pa.Revealed || pb.Revealed)
            {
                Fizzle(initiator, "swap");
                return false;
            }
            foreach (var victim in new[] { a, b })
            {
                if (victim == initiator || victim.InkUsed || !victim.HasHidden(PartIds.Octopus)) continue;
                victim.InkUsed = true;
                Emit(GameEventType.SwapCancelled, initiator.Seat, victim.Seat, (int)slot);
                return false;
            }

            var factionA = a.Faction;
            var factionB = b.Faction;
            a.Parts[(int)slot] = pb;
            b.Parts[(int)slot] = pa;
            ClampAfterSwap(a);
            ClampAfterSwap(b);
            a.SwapCount++;
            b.SwapCount++;

            Emit(GameEventType.Swapped, a.Seat, b.Seat, (int)slot, initiator.Seat);
            Emit(GameEventType.SwapReceived, b.Seat, a.Seat, (int)slot, (int)pb.Color, pb.PartId, a.Seat);
            Emit(GameEventType.SwapReceived, a.Seat, b.Seat, (int)slot, (int)pa.Color, pa.PartId, b.Seat);
            if (a.Faction != factionA) Emit(GameEventType.FactionChanged, -1, a.Seat, (int)a.Faction, visibleTo: a.Seat);
            if (b.Faction != factionB) Emit(GameEventType.FactionChanged, -1, b.Seat, (int)b.Faction, visibleTo: b.Seat);
            return true;
        }

        /// <summary>交換ではダウンしない: HPが0以下になるなら1で止める。</summary>
        static void ClampAfterSwap(PlayerState p)
        {
            if (p.Damage >= p.MaxHp) p.Damage = p.MaxHp - 1;
        }

        // ───────────────────────── カード ─────────────────────────

        void PlayCard(PlayerState p, CardPlay play)
        {
            if (play.HandIndex < 0 || play.HandIndex >= p.Hand.Count) return;
            string id = p.Hand[play.HandIndex];
            p.Hand.RemoveAt(play.HandIndex);
            var def = Catalog.Card(id);
            Emit(GameEventType.CardUsed, p.Seat, play.Target, detail: id);

            if (def.IsEquipment)
            {
                p.Equipment.Add(id);
                Emit(GameEventType.Equipped, p.Seat, -1, detail: id);
                return;
            }

            State.DiscardPiles[def.Deck].Add(id);
            if (def.Deck == DeckType.Scan) p.LastScanRound = State.Round;

            PlayerState target = play.Target >= 0 && play.Target < State.Players.Count ? State.P(play.Target) : null;
            bool TargetOk(bool allowSelf) => target != null && (allowSelf || target != p) && InRange(p, target, def.Range);
            bool magnifier = p.HasEquipment(CardIds.Magnifier);

            switch (id)
            {
                case CardIds.Clairvoyance:
                    if (!TargetOk(false)) { Fizzle(p, id); return; }
                    ScanColor(p, target, play.Slot);
                    if (magnifier && play.Slot2 != play.Slot) ScanColor(p, target, play.Slot2);
                    break;

                case CardIds.Compatibility:
                    if (!TargetOk(false)) { Fizzle(p, id); return; }
                    ScanCompat(p, target);
                    if (magnifier && play.Target2 >= 0 && play.Target2 != play.Target && play.Target2 < State.Players.Count)
                    {
                        var t2 = State.P(play.Target2);
                        if (t2 != p && InRange(p, t2, def.Range)) ScanCompat(p, t2);
                    }
                    break;

                case CardIds.Peek:
                    if (!TargetOk(false)) { Fizzle(p, id); return; }
                    Emit(GameEventType.ScanResult, p.Seat, target.Seat, target.Hand.Count, 0, "hand:" + string.Join(",", target.Hand), p.Seat);
                    break;

                case CardIds.Interrogate:
                    if (!TargetOk(false)) { Fizzle(p, id); return; }
                    target.InterrogateRound = State.Round + 1;
                    Emit(GameEventType.StatusApplied, p.Seat, target.Seat, detail: "interrogated");
                    break;

                case CardIds.Footprints:
                    var seats = new List<string>();
                    foreach (var o in State.Players)
                        if (o != p && o.LastScanRound == State.Round - 1 && State.Round > 1) seats.Add(o.Seat.ToString());
                    Emit(GameEventType.ScanResult, p.Seat, -1, seats.Count, 0, "footprints:" + string.Join(",", seats), p.Seat);
                    break;

                case CardIds.FirstAid:
                    if (target == null) target = p;
                    if (!TargetOk(true)) { Fizzle(p, id); return; }
                    Heal(target, 3);
                    break;

                case CardIds.Nutrient:
                    GainEN(p, 2);
                    break;

                case CardIds.Barrier:
                    p.BarrierPool += 3;
                    break;

                case CardIds.Antidote:
                    if (target == null) target = p;
                    if (!TargetOk(true)) { Fizzle(p, id); return; }
                    target.PoisonRounds = 0;
                    if (target.SealedRound > State.Round) target.SealedRound = 0;
                    if (target.ImmobileRound > State.Round) target.ImmobileRound = 0;
                    Heal(target, 1);
                    break;

                case CardIds.BloodGift:
                    if (!TargetOk(false)) { Fizzle(p, id); return; }
                    p.Damage = Math.Min(p.Damage + 2, p.MaxHp - 1);
                    Heal(target, 4);
                    break;

                case CardIds.CurseNail:
                    if (!TargetOk(false)) { Fizzle(p, id); return; }
                    DealDamage(target, 2, DamageKind.Effect, p.Seat);
                    break;

                case CardIds.Bomb:
                    if (!AreaMap.IsAdjacent(p.Area, play.Area)) { Fizzle(p, id); return; }
                    foreach (var o in State.Alive())
                        if (o.Area == play.Area && !o.HasHidden(PartIds.Frog))
                            DealDamage(o, 1, DamageKind.Effect, p.Seat);
                    break;

                case CardIds.Transplant:
                    if (!TargetOk(false)) { Fizzle(p, id); return; }
                    Swap(p, p, target, play.Slot);
                    break;

                case CardIds.Shackles:
                    if (!TargetOk(false)) { Fizzle(p, id); return; }
                    target.ImmobileRound = State.Round + 1;
                    Emit(GameEventType.StatusApplied, p.Seat, target.Seat, detail: "shackled");
                    break;

                case CardIds.Drain:
                    if (!TargetOk(false)) { Fizzle(p, id); return; }
                    int taken = Math.Min(2, target.EN);
                    target.EN -= taken;
                    GainEN(p, taken);
                    Emit(GameEventType.EnergyChanged, p.Seat, target.Seat, -taken);
                    break;

                default:
                    throw new InvalidOperationException("未実装のカード: " + id);
            }
        }

        /// <summary>透視。ユウレイの胴（潜伏）とキツネの頭（潜伏）は偽装プロファイルの色を見せる。</summary>
        void ScanColor(PlayerState user, PlayerState target, Slot slot)
        {
            var part = target.Part(slot);
            Faction shown = part.Revealed ? part.PublicColor : part.Color;
            if (!part.Revealed && target.LieColor.HasValue)
            {
                if (part.PartId == PartIds.Ghost)
                    shown = target.LieColor.Value;
                else if (target.LiarUsesLeft > 0 && target.HasHidden(PartIds.Fox))
                {
                    target.LiarUsesLeft--;
                    shown = target.LieColor.Value;
                }
            }
            Emit(GameEventType.ScanResult, user.Seat, target.Seat, (int)slot, (int)shown, "color", user.Seat);
        }

        void ScanCompat(PlayerState user, PlayerState target)
        {
            bool same = user.Faction == target.Faction;
            if (target.LieCompat != CompatAnswer.Honest && target.LiarUsesLeft > 0 && target.HasHidden(PartIds.Fox))
            {
                target.LiarUsesLeft--;
                same = target.LieCompat == CompatAnswer.Same;
            }
            Emit(GameEventType.ScanResult, user.Seat, target.Seat, same ? 1 : 0, 0, "compat", user.Seat);
        }
    }
}
