using ChimeraGedon.Core;

namespace ChimeraGedon.Game
{
    /// <summary>表示用の日本語。ローカライズ導入時（TECH_SPEC フェーズ4）に String Table へ移す。</summary>
    public static class Texts
    {
        public static string Faction(Faction f) => f == Core.Faction.Red ? "赤" : f == Core.Faction.Blue ? "青" : "緑";

        public static string FactionColored(Faction f) => Ui.Colored(Faction(f), Ui.FactionColor(f));

        /// <summary>色覚多様性向けの形の記号（PARTS_AND_CARDS 4章）。</summary>
        public static string Mark(Faction f) => f == Core.Faction.Red ? "▲" : f == Core.Faction.Blue ? "●" : "◆";

        public static string Chip(Faction f) => Ui.Colored(Mark(f) + Faction(f), Ui.FactionColor(f));

        public static string Slot(Slot s) => s == Core.Slot.Head ? "頭" : s == Core.Slot.Torso ? "胴" : "脚";

        public static string Area(Area a)
        {
            switch (a)
            {
                case Core.Area.Altar: return "①祭壇";
                case Core.Area.Lab: return "②研究所";
                case Core.Area.Arena: return "③闘技場";
                default: return "④巣";
            }
        }

        public static string AreaEffect(Area a)
        {
            switch (a)
            {
                case Core.Area.Altar: return "公開でEN+1";
                case Core.Area.Lab: return "ドローEN-1";
                case Core.Area.Arena: return "攻撃命中+1";
                default: return "終了時HP+1";
            }
        }

        public static string Deck(DeckType d) => d == DeckType.Scan ? "解析（推理）" : d == DeckType.Support ? "治療（回復）" : "呪術（ダメージ）";

        public static string Name(MatchState s, int seat) => seat >= 0 && seat < s.Players.Count ? s.P(seat).Name : "?";

        /// <summary>ログ1行。見えないイベントは null。</summary>
        public static string Event(MatchState s, GameEvent e, int viewer)
        {
            if (!e.IsVisibleTo(viewer)) return null;
            string A = Name(s, e.Actor), T = Name(s, e.Target);
            switch (e.Type)
            {
                case GameEventType.RoundStarted: return $"―― ラウンド {e.Value} ――";
                case GameEventType.Moved: return $"{A} が {Area((Area)e.Value)} へ移動";
                case GameEventType.TrapTriggered: return e.Value > 0 ? $"{T} が罠にかかった！" : $"{T} は罠を跳び越えた";
                case GameEventType.PartRevealed:
                    var id = e.Detail.Split(':')[0];
                    bool forced = e.Detail.EndsWith(":forced");
                    return $"{A} が{Slot((Slot)e.Value)}を公開: {Catalog.Part(id).Name} {Chip((Faction)e.Value2)}{(forced ? "（尋問）" : "")}";
                case GameEventType.SkillFizzled: return $"{A} の効果は不発";
                case GameEventType.CardUsed: return $"{A} がカード「{Catalog.Card(e.Detail).Name}」を使用" + (e.Target >= 0 ? $" → {T}" : "");
                case GameEventType.Equipped: return $"{A} が装備「{Catalog.Card(e.Detail).Name}」";
                case GameEventType.AttackDeclared: return null;
                case GameEventType.AttackResolved:
                    return e.Detail == "hit"
                        ? $"{A} → {T} 攻撃 {e.Value} vs ガード {e.Value2} 命中！"
                        : $"{A} → {T} 攻撃 {e.Value} vs ガード {e.Value2} 防いだ";
                case GameEventType.AttackFizzled: return $"{A} の攻撃は {T} に届かなかった";
                case GameEventType.Damaged:
                    string kind = e.Value2 == (int)DamageKind.Doom ? "終末で" : e.Value2 == (int)DamageKind.Poison ? "毒で" : "";
                    return $"{T} に{kind} {e.Value} ダメージ";
                case GameEventType.Healed: return e.Value > 0 ? $"{T} が HP {e.Value} 回復" : null;
                case GameEventType.EnergyChanged: return e.Actor >= 0 && e.Value < 0 ? $"{A} が {T} から EN {-e.Value} を奪った" : null;
                case GameEventType.Swapped: return $"{A} と {T} の{Slot((Slot)e.Value)}が入れ替わった！";
                case GameEventType.SwapCancelled: return $"{T} は交換を墨でかわした";
                case GameEventType.SwapReceived:
                    return $"受け取った{Slot((Slot)e.Value)}: {Catalog.Part(e.Detail).Name} {Chip((Faction)e.Value2)}";
                case GameEventType.FactionChanged: return Ui.Colored($"★ あなたの陣営が {Faction((Faction)e.Value)} に変わった！", Ui.Accent);
                case GameEventType.ScanResult: return ScanText(s, e);
                case GameEventType.StatusApplied:
                    switch (e.Detail)
                    {
                        case "sealed": return $"{T} は封じられた（次R 公開・手札不可）";
                        case "half": return $"{A} はダメージ半減の構え";
                        case "untargetable": return $"{A} は次R 狙われない";
                        case "poison": return $"{T} に毒";
                        case "kicked": return $"{T} は蹴り出された";
                        case "trap": return $"罠を {Area((Area)e.Value)} に仕掛けた";
                        case "interrogated": return $"{T} は尋問された（次R 部位を公開）";
                        case "shackled": return $"{T} に枷（次R 移動不可）";
                    }
                    return null;
                case GameEventType.CardDrawn: return "カードを見た: " + CardList(e.Detail);
                case GameEventType.CardStolen: return $"{A} が {T} の手札を奪った";
                case GameEventType.Down: return Ui.Colored($"{T} がダウン（{Faction((Faction)e.Value)}陣営）", Ui.Accent);
                case GameEventType.Doom: return Ui.Colored($"終末が迫る… 全員に {e.Value} ダメージ", Ui.FactionColor(Core.Faction.Red));
                case GameEventType.MatchEnded: return "試合終了";
                default: return null;
            }
        }

        static string ScanText(MatchState s, GameEvent e)
        {
            string T = Name(s, e.Target);
            if (e.Detail == "color" || e.Detail == "owl")
                return $"【解析】{T} の{Slot((Slot)e.Value)}は {Chip((Faction)e.Value2)}";
            if (e.Detail == "compat") return $"【解析】{T} はあなたと{(e.Value == 1 ? "同じ陣営" : "違う陣営")}";
            if (e.Detail.StartsWith("hand:")) return $"【解析】{T} の手札: {CardList(e.Detail.Substring(5))}";
            if (e.Detail.StartsWith("stolen:")) return $"奪ったカード: {CardList(e.Detail.Substring(7))}";
            if (e.Detail.StartsWith("footprints:"))
            {
                var seats = e.Detail.Substring(11);
                if (seats.Length == 0) return "【解析】前のラウンドに解析を使った人はいない";
                var names = new System.Collections.Generic.List<string>();
                foreach (var x in seats.Split(',')) names.Add(Name(s, int.Parse(x)));
                return "【解析】前のラウンドに解析を使った: " + string.Join("、", names);
            }
            return null;
        }

        public static string CardList(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return "なし";
            var names = new System.Collections.Generic.List<string>();
            foreach (var id in csv.Split(',')) names.Add(Catalog.Card(id).Name);
            return string.Join("、", names);
        }

        public static string MissionLine(string id)
        {
            var m = Catalog.Mission(id);
            return $"{m.Name}: {m.Text}";
        }
    }
}
