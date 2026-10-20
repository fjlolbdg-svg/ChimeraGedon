using System.Collections.Generic;

namespace ChimeraGedon.Core
{
    /// <summary>部位の定義。表は docs/PARTS_AND_CARDS.md 1章。効果の処理は MatchEngine.Effects.cs。</summary>
    public sealed class PartDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly Slot Slot;
        public readonly int Hp;
        public readonly EffectRange Range;
        public readonly string RevealText;
        public readonly string HiddenText;

        public PartDef(string id, string name, Slot slot, int hp, EffectRange range, string revealText, string hiddenText = null)
        {
            Id = id; Name = name; Slot = slot; Hp = hp; Range = range; RevealText = revealText; HiddenText = hiddenText;
        }

        public bool HasHiddenSkill => HiddenText != null;
    }

    /// <summary>カードの定義。表は docs/PARTS_AND_CARDS.md 2章。</summary>
    public sealed class CardDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly DeckType Deck;
        public readonly int Count;
        public readonly EffectRange Range;
        public readonly bool IsEquipment;
        public readonly string Text;

        public CardDef(string id, string name, DeckType deck, int count, EffectRange range, string text, bool isEquipment = false)
        {
            Id = id; Name = name; Deck = deck; Count = count; Range = range; Text = text; IsEquipment = isEquipment;
        }
    }

    /// <summary>緑の使命。表は docs/PARTS_AND_CARDS.md 3章。判定は MatchEngine.EndRound.cs。</summary>
    public sealed class MissionDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Text;

        public MissionDef(string id, string name, string text)
        {
            Id = id; Name = name; Text = text;
        }
    }

    public static class PartIds
    {
        public const string Owl = "H01", Chameleon = "H02", Fox = "H03", Lion = "H04", Octopus = "H05", Snake = "H06";
        public const string Turtle = "T01", Bear = "T02", Bee = "T03", Porcupine = "T04", Slime = "T05", Ghost = "T06";
        public const string Cheetah = "L01", Frog = "L02", Scorpion = "L03", Horse = "L04", Spider = "L05", Kangaroo = "L06";
    }

    public static class CardIds
    {
        public const string Clairvoyance = "S01", Compatibility = "S02", Peek = "S03", Interrogate = "S04", Footprints = "S05", Magnifier = "S06";
        public const string FirstAid = "R01", Nutrient = "R02", Barrier = "R03", Antidote = "R04", BloodGift = "R05", Amulet = "R06";
        public const string CurseNail = "C01", Bomb = "C02", Transplant = "C03", Shackles = "C04", Drain = "C05", SpikeCollar = "C06";
    }

    public static class MissionIds
    {
        public const string Immortal = "M01", Collector = "M02", Parasite = "M03", Hermit = "M04";
        public const string Complete = "M05", Balance = "M06", Opportunist = "M07", DragAlong = "M08";
    }

    public static class Catalog
    {
        public static readonly IReadOnlyList<PartDef> Parts = new[]
        {
            new PartDef(PartIds.Owl, "フクロウの頭", Slot.Head, 3, EffectRange.Adjacent, "1人の未公開部位を2つまで色だけ見る"),
            new PartDef(PartIds.Chameleon, "カメレオンの頭", Slot.Head, 3, EffectRange.SameArea, "1人と同じスロットの未公開部位を交換"),
            new PartDef(PartIds.Fox, "キツネの頭", Slot.Head, 3, EffectRange.SameArea, "1人の手札を1枚ランダムに奪う", "解析カードに偽の答えを返せる（2回）"),
            new PartDef(PartIds.Lion, "ライオンの頭", Slot.Head, 4, EffectRange.SameArea, "同エリアの自分以外全員に2ダメージ"),
            new PartDef(PartIds.Octopus, "タコの頭", Slot.Head, 2, EffectRange.All, "2人の同じスロットの未公開部位を交換させる", "自分への交換を1回無効にする"),
            new PartDef(PartIds.Snake, "ヘビの頭", Slot.Head, 3, EffectRange.Adjacent, "1人は次のラウンド公開と手札が使えない"),

            new PartDef(PartIds.Turtle, "カメの胴", Slot.Torso, 5, EffectRange.Self, "このラウンドと次のラウンド、被ダメージ半分"),
            new PartDef(PartIds.Bear, "クマの胴", Slot.Torso, 5, EffectRange.Self, "HP+4"),
            new PartDef(PartIds.Bee, "ハチの胴", Slot.Torso, 3, EffectRange.SameArea, "1人に3ダメージ（ガード不能）"),
            new PartDef(PartIds.Porcupine, "ヤマアラシの胴", Slot.Torso, 4, EffectRange.Self, "【常時】攻撃を命中させた相手に2ダメージ"),
            new PartDef(PartIds.Slime, "スライムの胴", Slot.Torso, 4, EffectRange.Self, "HP+2、好きな山札から1枚引く", "4以上のダメージを1度だけ2に減らす"),
            new PartDef(PartIds.Ghost, "ユウレイの胴", Slot.Torso, 4, EffectRange.Self, "表示色を自分で選べる", "透視されたとき好きな色を見せる"),

            new PartDef(PartIds.Cheetah, "チーターの脚", Slot.Legs, 3, EffectRange.All, "好きなエリアへ移動し、1人に投入EN2の攻撃"),
            new PartDef(PartIds.Frog, "カエルの脚", Slot.Legs, 3, EffectRange.Self, "好きなエリアへ移動。次のラウンド攻撃されない", "罠と爆弾のダメージを受けない"),
            new PartDef(PartIds.Scorpion, "サソリの脚", Slot.Legs, 3, EffectRange.SameArea, "1人に毒（3ラウンド毎1ダメージ）"),
            new PartDef(PartIds.Horse, "ウマの脚", Slot.Legs, 4, EffectRange.Adjacent, "1人を隣のエリアへ蹴り出し、次のラウンド移動不可"),
            new PartDef(PartIds.Spider, "クモの脚", Slot.Legs, 3, EffectRange.SameArea, "今いるエリアに罠（入ってきた人に3ダメージ）"),
            new PartDef(PartIds.Kangaroo, "カンガルーの脚", Slot.Legs, 3, EffectRange.SameArea, "公開と同時に攻撃。投入EN+2として判定"),
        };

        public static readonly IReadOnlyList<CardDef> Cards = new[]
        {
            new CardDef(CardIds.Clairvoyance, "透視", DeckType.Scan, 4, EffectRange.All, "1人の未公開部位1つの色を見る"),
            new CardDef(CardIds.Compatibility, "相性診断", DeckType.Scan, 3, EffectRange.Adjacent, "1人が自分と同じ陣営か分かる"),
            new CardDef(CardIds.Peek, "盗み見", DeckType.Scan, 3, EffectRange.SameArea, "1人の手札を全部見る"),
            new CardDef(CardIds.Interrogate, "尋問", DeckType.Scan, 2, EffectRange.SameArea, "1人は次のラウンド部位を1つ公開させられる"),
            new CardDef(CardIds.Footprints, "足跡", DeckType.Scan, 2, EffectRange.All, "直前のラウンドに解析カードを使った人が分かる"),
            new CardDef(CardIds.Magnifier, "虫眼鏡", DeckType.Scan, 2, EffectRange.Self, "透視・相性診断の結果がもう1つ増える", true),

            new CardDef(CardIds.FirstAid, "応急処置", DeckType.Support, 4, EffectRange.Adjacent, "自分か1人のHP+3"),
            new CardDef(CardIds.Nutrient, "栄養剤", DeckType.Support, 3, EffectRange.Self, "EN+2"),
            new CardDef(CardIds.Barrier, "防壁", DeckType.Support, 3, EffectRange.Self, "このラウンド被ダメージ-3"),
            new CardDef(CardIds.Antidote, "解毒", DeckType.Support, 2, EffectRange.Adjacent, "毒・行動封じを解除しHP+1"),
            new CardDef(CardIds.BloodGift, "献血", DeckType.Support, 2, EffectRange.SameArea, "自分のHP-2、1人のHP+4"),
            new CardDef(CardIds.Amulet, "お守り", DeckType.Support, 2, EffectRange.Self, "各ラウンド最初の被ダメージ-1", true),

            new CardDef(CardIds.CurseNail, "呪いの釘", DeckType.Hex, 4, EffectRange.SameArea, "1人に2ダメージ（ガード不能）"),
            new CardDef(CardIds.Bomb, "爆弾", DeckType.Hex, 2, EffectRange.Adjacent, "隣のエリア1つの全員に1ダメージ"),
            new CardDef(CardIds.Transplant, "移植手術", DeckType.Hex, 3, EffectRange.SameArea, "1人と同じスロットの未公開部位を交換"),
            new CardDef(CardIds.Shackles, "枷", DeckType.Hex, 3, EffectRange.Adjacent, "1人は次のラウンド移動できない"),
            new CardDef(CardIds.Drain, "吸血", DeckType.Hex, 2, EffectRange.SameArea, "1人からENを2奪う"),
            new CardDef(CardIds.SpikeCollar, "トゲの首輪", DeckType.Hex, 2, EffectRange.Self, "通常攻撃が命中したらダメージ+1", true),
        };

        public static readonly IReadOnlyList<MissionDef> Missions = new[]
        {
            new MissionDef(MissionIds.Immortal, "不死身", "試合終了時に生存していて、HPが5以上"),
            new MissionDef(MissionIds.Collector, "収集家", "試合終了時に装備を2個以上付けている"),
            new MissionDef(MissionIds.Parasite, "寄生", "自分が関わる交換が2回以上起き、試合終了時に生存"),
            new MissionDef(MissionIds.Hermit, "隠者", "試合終了時に生存していて、公開された部位が1つ以下"),
            new MissionDef(MissionIds.Complete, "完全体", "試合終了時に生存していて、3部位すべて公開されている"),
            new MissionDef(MissionIds.Balance, "天秤", "試合終了時、ダウンした赤と青の人数が同じ（1人以上）"),
            new MissionDef(MissionIds.Opportunist, "漁夫の利", "2人以上をダウンさせ、試合終了時に生存"),
            new MissionDef(MissionIds.DragAlong, "道連れ", "自分がダウンしたラウンドに、自分を攻撃した人も1人以上ダウンする"),
        };

        static Dictionary<string, PartDef> partById;
        static Dictionary<string, CardDef> cardById;
        static Dictionary<string, MissionDef> missionById;

        public static PartDef Part(string id)
        {
            if (partById == null)
            {
                partById = new Dictionary<string, PartDef>();
                foreach (var p in Parts) partById[p.Id] = p;
            }
            return partById[id];
        }

        public static CardDef Card(string id)
        {
            if (cardById == null)
            {
                cardById = new Dictionary<string, CardDef>();
                foreach (var c in Cards) cardById[c.Id] = c;
            }
            return cardById[id];
        }

        public static MissionDef Mission(string id)
        {
            if (missionById == null)
            {
                missionById = new Dictionary<string, MissionDef>();
                foreach (var m in Missions) missionById[m.Id] = m;
            }
            return missionById[id];
        }

        public static List<PartDef> PartsFor(Slot slot)
        {
            var list = new List<PartDef>();
            foreach (var p in Parts) if (p.Slot == slot) list.Add(p);
            return list;
        }
    }
}
