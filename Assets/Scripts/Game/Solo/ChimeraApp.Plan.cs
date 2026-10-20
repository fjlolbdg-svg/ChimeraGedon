using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChimeraGedon.Core;

namespace ChimeraGedon.Game
{
    /// <summary>
    /// 計画フェイズの入力。メニュー → 必要な項目（対象・部位・エリアなど）を順番に聞いて ActionPlan を組み立てる。
    /// 部位・カードごとに何を聞くかは AskSkillParams / AskCardParams の switch（PARTS_AND_CARDS.md と対応）。
    /// </summary>
    public sealed partial class ChimeraApp
    {
        List<AttackArrow> lastArrows = new List<AttackArrow>();

        async Task PlanPhase()
        {
            screen.Refresh(S, HumanSeat);
            ActionPlan plan = ActionPlan.Charge();

            if (Me.IsAlive)
            {
                if (Me.PendingDraw != null)
                {
                    int keep = await modal.Choose("ドローしたカード", "どちらを手札に残す？（もう1枚は山札の下へ）",
                        Me.PendingDraw.Select(CardLabel), cancellable: false);
                    plan.KeepIndex = keep;
                    // 選んだカードを計画中に使えるよう、手札表示用に先に反映しておく
                    // （エンジンは LockPlans の最初に同じ選択を適用する）
                }
                if (Me.InterrogateRound == S.Round)
                {
                    var slots = Enumerable.Range(0, 3).Where(i => !Me.Parts[i].Revealed).ToList();
                    if (slots.Count > 0)
                    {
                        int i = await modal.Choose("尋問された", "公開する部位を選ぶ（スキルは発動しない）",
                            slots.Select(x => PartLabel(Me, x)), cancellable: false);
                        plan.InterrogateSlot = (Slot)slots[i];
                    }
                }
                plan = await BuildPlan(plan);
            }

            var plans = S.Players.Where(p => p.IsAlive && p.IsBot).ToDictionary(p => p.Seat, p => brains[p.Seat].ChoosePlan(S, p, engine));
            if (Me.IsAlive) plans[HumanSeat] = plan;
            lastArrows = engine.LockPlans(plans);
        }

        async Task<ActionPlan> BuildPlan(ActionPlan basePlan)
        {
            CardPlay card = null;
            // 前ラウンドのドロー分はまだ手札にないので、使えるカードの一覧に含めて見せる
            var hand = new List<string>(Me.Hand);
            if (Me.PendingDraw != null && hand.Count < Balance.HandLimit) hand.Add(Me.PendingDraw[basePlan.KeepIndex]);

            while (true)
            {
                bool sealedNow = Me.SealedRound == S.Round;
                bool canAttack = Me.EN >= 1 && AttackTargets().Any();
                int drawCost = engine.DrawCostFor(Me);
                bool canDraw = hand.Count < Balance.HandLimit && Me.EN >= drawCost;
                bool canReveal = !sealedNow && Me.Parts.Any(p => !p.Revealed);
                bool canCard = !sealedNow && hand.Count > 0;
                bool canLie = Me.HasHidden(PartIds.Fox) || Me.HasHidden(PartIds.Ghost);

                string cardLine = card != null ? $"\nサブ: カード「{Catalog.Card(hand[card.HandIndex]).Name}」を使う予定" : "";
                var opts = new List<OptionModal.Option>
                {
                    new OptionModal.Option("攻撃（同じエリアの1人にENを賭ける）", canAttack),
                    new OptionModal.Option($"チャージ（EN+{Balance.ChargeEN}）"),
                    new OptionModal.Option($"ドロー（EN{drawCost}、2枚見て1枚）", canDraw),
                    new OptionModal.Option("公開（部位のスキルを使う）", canReveal),
                    new OptionModal.Option(card == null ? "カードを使う（サブ）" : "カードをやめる", canCard || card != null),
                };
                if (canLie) opts.Add(new OptionModal.Option(LieLabel()));

                int pick = await modal.Choose($"R{S.Round} 行動を選ぶ",
                    $"EN {Me.EN}　{(sealedNow ? "封じられている（公開・カード不可）" : "")}{cardLine}", opts, cancellable: false);

                if (pick == 4)
                {
                    if (card != null) card = null;
                    else card = await AskCard(hand);
                    continue;
                }
                if (pick == 5) { await EditLieProfile(); continue; }

                var plan = new ActionPlan { KeepIndex = basePlan.KeepIndex, InterrogateSlot = basePlan.InterrogateSlot, Card = card };
                bool ok = true;
                switch (pick)
                {
                    case 0:
                        plan.Action = ActionType.Attack;
                        plan.Target = await PickPlayer("誰を攻撃する？", AttackTargets());
                        ok = plan.Target >= 0;
                        if (ok)
                        {
                            plan.Bid = await PickBid($"{Texts.Name(S, plan.Target)}（EN{S.P(plan.Target).PublicEN}）に賭けるEN", Me.EN);
                            ok = plan.Bid > 0;
                        }
                        break;
                    case 1:
                        plan.Action = ActionType.Charge;
                        break;
                    case 2:
                        plan.Action = ActionType.Draw;
                        int d = await modal.Choose("どの山札から引く？", null, new[] { DeckType.Scan, DeckType.Support, DeckType.Hex }.Select(Texts.Deck));
                        ok = d >= 0;
                        plan.Deck = (DeckType)System.Math.Max(0, d);
                        break;
                    case 3:
                        plan.Action = ActionType.Reveal;
                        ok = await AskReveal(plan);
                        break;
                }
                if (!ok) continue;
                screen.SetPhase("ほかのプレイヤーを待っています…");
                return plan;
            }
        }

        // ───────────────────────── 公開 ─────────────────────────

        async Task<bool> AskReveal(ActionPlan plan)
        {
            var slots = Enumerable.Range(0, 3).Where(i => !Me.Parts[i].Revealed).ToList();
            int i = await modal.Choose("どの部位を公開する？", "公開すると色がバレて、その部位は交換されなくなる。潜伏スキルは消える。",
                slots.Select(x => PartLabel(Me, x) + "\n" + Me.Parts[x].Def.RevealText));
            if (i < 0) return false;
            plan.RevealSlot = (Slot)slots[i];
            return await AskSkillParams(Me.Part(plan.RevealSlot).PartId, plan);
        }

        async Task<bool> AskSkillParams(string partId, ActionPlan plan)
        {
            var others = S.Players.Where(p => p.IsAlive && p.Seat != HumanSeat).ToList();
            List<PlayerState> InRange(EffectRange r) => others.Where(o => InRangeOf(Me, o, r)).ToList();

            switch (partId)
            {
                case PartIds.Owl:
                case PartIds.Fox:
                case PartIds.Snake:
                case PartIds.Bee:
                case PartIds.Scorpion:
                    plan.Target = await PickPlayer("対象は？", InRange(Catalog.Part(partId).Range));
                    return plan.Target >= 0;

                case PartIds.Chameleon:
                    plan.Target = await PickPlayer("誰と交換する？", InRange(EffectRange.SameArea));
                    if (plan.Target < 0) return false;
                    return await PickSwapSlot(plan, excludeRevealSlot: true);

                case PartIds.Octopus:
                    plan.Target = await PickPlayer("交換させる1人目", others);
                    if (plan.Target < 0) return false;
                    plan.Target2 = await PickPlayer("交換させる2人目", others.Where(o => o.Seat != plan.Target).ToList());
                    if (plan.Target2 < 0) return false;
                    int s = await modal.Choose("どの部位を交換させる？", null, new[] { "頭", "胴", "脚" });
                    if (s < 0) return false;
                    plan.SwapSlot = (Slot)s;
                    return true;

                case PartIds.Slime:
                    int d = await modal.Choose("どの山札から1枚引く？", null, new[] { DeckType.Scan, DeckType.Support, DeckType.Hex }.Select(Texts.Deck));
                    if (d < 0) return false;
                    plan.Deck = (DeckType)d;
                    return true;

                case PartIds.Ghost:
                    int c = await modal.Choose("みんなに見せる色は？", "本当の色は隠れたまま。陣営判定は本当の色で行う。",
                        new[] { Faction.Red, Faction.Blue, Faction.Green }.Select(f => Texts.Chip(f)));
                    if (c < 0) return false;
                    plan.DisplayColor = (Faction)c;
                    return true;

                case PartIds.Cheetah:
                    int a = await modal.Choose("どこへ跳ぶ？", "着地したエリアの1人に投入EN2の攻撃（EN消費なし）",
                        Enumerable.Range(0, 4).Select(x => Texts.Area((Area)x)));
                    if (a < 0) return false;
                    plan.DestArea = (Area)a;
                    var there = others.Where(o => o.Area == plan.DestArea && o.UntargetableRound != S.Round).ToList();
                    plan.Target = there.Count > 0 ? await PickPlayer("誰を攻撃する？", there) : -1;
                    return true;

                case PartIds.Frog:
                    int f = await modal.Choose("どこへ跳ぶ？", "次のラウンド、攻撃の対象にならない",
                        Enumerable.Range(0, 4).Select(x => Texts.Area((Area)x)));
                    if (f < 0) return false;
                    plan.DestArea = (Area)f;
                    return true;

                case PartIds.Horse:
                    plan.Target = await PickPlayer("誰を蹴り出す？", InRange(EffectRange.Adjacent));
                    if (plan.Target < 0) return false;
                    var from = S.P(plan.Target).Area;
                    var dests = new[] { (Area)(((int)from + 1) % 4), (Area)(((int)from + 3) % 4) };
                    int k = await modal.Choose("どこへ蹴り出す？", null, dests.Select(Texts.Area));
                    if (k < 0) return false;
                    plan.DestArea = dests[k];
                    return true;

                case PartIds.Kangaroo:
                    var targets = AttackTargets();
                    if (targets.Count == 0) return true; // 攻撃なしの公開
                    plan.Target = await PickPlayer("一緒に攻撃する相手（判定+2）", targets);
                    if (plan.Target < 0) return false;
                    plan.Bid = await PickBid("賭けるEN（判定は+2される）", Me.EN);
                    if (plan.Bid <= 0) plan.Target = -1;
                    return true;

                default:
                    return true; // ライオン・カメ・クマ・ヤマアラシ・クモ: 入力なし
            }
        }

        async Task<bool> PickSwapSlot(ActionPlan plan, bool excludeRevealSlot)
        {
            var target = S.P(plan.Target);
            var slots = Enumerable.Range(0, 3).Where(i =>
                (!excludeRevealSlot || i != (int)plan.RevealSlot) && !Me.Parts[i].Revealed && !target.Parts[i].Revealed).ToList();
            if (slots.Count == 0)
            {
                await modal.Choose("交換できる部位がない", "おたがい未公開の同じ部位が必要。", new[] { "もどる" }, cancellable: false);
                return false;
            }
            int i = await modal.Choose("どの部位を交換する？", "未公開どうしの同じ部位を入れ替える（能力も色も移る）",
                slots.Select(x => $"{Texts.Slot((Slot)x)}: 自分の {Me.Parts[x].Def.Name} {Texts.Chip(Me.Parts[x].Color)}"));
            if (i < 0) return false;
            plan.SwapSlot = (Slot)slots[i];
            return true;
        }

        // ───────────────────────── カード ─────────────────────────

        async Task<CardPlay> AskCard(List<string> hand)
        {
            int i = await modal.Choose("どのカードを使う？", "メインの行動とは別に1枚使える", hand.Select(CardLabel));
            if (i < 0) return null;
            var id = hand[i];
            var def = Catalog.Card(id);
            var play = new CardPlay { HandIndex = i };
            if (def.IsEquipment)
            {
                if (Me.Equipment.Count >= Balance.EquipmentLimit) return null;
                return play;
            }

            var others = S.Players.Where(p => p.IsAlive && p.Seat != HumanSeat).ToList();
            var inRange = others.Where(o => InRangeOf(Me, o, def.Range)).ToList();
            bool magnifier = Me.HasEquipment(CardIds.Magnifier);

            switch (id)
            {
                case CardIds.Clairvoyance:
                    play.Target = await PickPlayer("誰を透視する？", inRange);
                    if (play.Target < 0) return null;
                    int s1 = await modal.Choose("どの部位を見る？", null, new[] { "頭", "胴", "脚" });
                    if (s1 < 0) return null;
                    play.Slot = (Slot)s1;
                    play.Slot2 = play.Slot;
                    if (magnifier)
                    {
                        int s2 = await modal.Choose("虫眼鏡: もう1つ見る部位", null, new[] { "頭", "胴", "脚" });
                        if (s2 >= 0) play.Slot2 = (Slot)s2;
                    }
                    return play;

                case CardIds.Compatibility:
                    play.Target = await PickPlayer("誰と相性診断する？", inRange);
                    if (play.Target < 0) return null;
                    if (magnifier) play.Target2 = await PickPlayer("虫眼鏡: もう1人", inRange.Where(o => o.Seat != play.Target).ToList());
                    return play;

                case CardIds.Footprints:
                case CardIds.Nutrient:
                case CardIds.Barrier:
                    return play;

                case CardIds.FirstAid:
                case CardIds.Antidote:
                    var withSelf = new List<PlayerState> { Me };
                    withSelf.AddRange(inRange);
                    play.Target = await PickPlayer("誰に使う？", withSelf);
                    return play.Target < 0 ? null : play;

                case CardIds.Bomb:
                    var adj = new[] { (Area)(((int)Me.Area + 1) % 4), (Area)(((int)Me.Area + 3) % 4) };
                    int b = await modal.Choose("どのエリアに投げる？", "そのエリアの全員に1ダメージ", adj.Select(Texts.Area));
                    if (b < 0) return null;
                    play.Area = adj[b];
                    return play;

                case CardIds.Transplant:
                    play.Target = await PickPlayer("誰と移植する？", inRange);
                    if (play.Target < 0) return null;
                    var t = S.P(play.Target);
                    var slots = Enumerable.Range(0, 3).Where(x => !Me.Parts[x].Revealed && !t.Parts[x].Revealed).ToList();
                    if (slots.Count == 0) return null;
                    int si = await modal.Choose("どの部位を交換する？", null, slots.Select(x => $"{Texts.Slot((Slot)x)}: 自分の {Me.Parts[x].Def.Name} {Texts.Chip(Me.Parts[x].Color)}"));
                    if (si < 0) return null;
                    play.Slot = (Slot)slots[si];
                    return play;

                default:
                    play.Target = await PickPlayer("対象は？", inRange);
                    return play.Target < 0 ? null : play;
            }
        }

        // ───────────────────────── 偽装設定 ─────────────────────────

        string LieLabel()
        {
            string color = Me.LieColor.HasValue ? Texts.Chip(Me.LieColor.Value) : "正直";
            string compat = Me.LieCompat == CompatAnswer.Honest ? "正直" : Me.LieCompat == CompatAnswer.Same ? "同じ" : "違う";
            return $"偽装設定（色: {color} / 相性: {compat}）";
        }

        async Task EditLieProfile()
        {
            int c = await modal.Choose("透視されたときに見せる色", "キツネの頭（2回）・ユウレイの胴の潜伏スキルで使う",
                new[] { "正直に答える", Texts.Chip(Faction.Red), Texts.Chip(Faction.Blue), Texts.Chip(Faction.Green) });
            if (c < 0) return;
            Me.LieColor = c == 0 ? (Faction?)null : (Faction)(c - 1);
            int k = await modal.Choose("相性診断されたときの答え", "キツネの頭（2回）で使う", new[] { "正直に答える", "同じ陣営と答える", "違う陣営と答える" });
            if (k >= 0) Me.LieCompat = (CompatAnswer)k;
        }

        // ───────────────────────── 共通 ─────────────────────────

        List<PlayerState> AttackTargets() =>
            S.Players.Where(p => p.IsAlive && p.Seat != HumanSeat && p.Area == Me.Area && p.UntargetableRound != S.Round).ToList();

        static bool InRangeOf(PlayerState user, PlayerState target, EffectRange range)
        {
            switch (range)
            {
                case EffectRange.Self: return target == user;
                case EffectRange.SameArea: return target.Area == user.Area;
                case EffectRange.Adjacent: return AreaMap.IsSameOrAdjacent(user.Area, target.Area);
                default: return true;
            }
        }

        async Task<int> PickPlayer(string title, List<PlayerState> candidates)
        {
            if (candidates.Count == 0)
            {
                await modal.Choose(title, "届く範囲に相手がいない。", new[] { "もどる" }, cancellable: false);
                return -1;
            }
            int i = await modal.Choose(title, null, candidates.Select(PlayerLabel));
            return i < 0 ? -1 : candidates[i].Seat;
        }

        async Task<int> PickBid(string title, int max)
        {
            if (max <= 0) return 0;
            int i = await modal.Choose(title, "相手のガードより大きければ命中。同じなら防がれる。使ったENは戻らない。",
                Enumerable.Range(1, max).Select(v => $"EN {v}"));
            return i < 0 ? 0 : i + 1;
        }

        string PlayerLabel(PlayerState p)
        {
            if (p.Seat == HumanSeat) return $"自分（HP{p.Hp}/{p.MaxHp}）";
            string chips = string.Concat(p.Parts.Select(x => x.Revealed ? Texts.Chip(x.PublicColor) : "？"));
            return $"{p.Name}  {chips}  HP{p.Hp} EN{p.PublicEN}  {Texts.Area(p.Area)}";
        }

        static string PartLabel(PlayerState p, int slot)
        {
            var part = p.Parts[slot];
            return $"{Texts.Slot((Slot)slot)}: {part.Def.Name} {Texts.Chip(part.Color)}";
        }

        static string CardLabel(string id)
        {
            var c = Catalog.Card(id);
            return $"{c.Name}{(c.IsEquipment ? "【装備】" : "")}: {c.Text}";
        }
    }
}
