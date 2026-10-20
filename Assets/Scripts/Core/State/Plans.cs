namespace ChimeraGedon.Core
{
    /// <summary>試合に参加するプレイヤーの設定。PartIds は頭・胴・脚の順。</summary>
    public sealed class PlayerSetup
    {
        public string Name;
        public bool IsBot;
        public string[] PartIds = new string[3];

        public PlayerSetup(string name, bool isBot, string head, string torso, string legs)
        {
            Name = name;
            IsBot = isBot;
            PartIds = new[] { head, torso, legs };
        }
    }

    /// <summary>
    /// 計画フェイズの入力。使わない項目は既定値のままでよい。
    /// 不正な計画はエンジン側で「チャージ」に置き換わる（GAME_SPEC 4-1 の時間切れと同じ扱い）。
    /// </summary>
    public sealed class ActionPlan
    {
        public ActionType Action = ActionType.Charge;

        /// <summary>攻撃・公開スキルの対象。</summary>
        public int Target = -1;
        /// <summary>タコの頭の2人目。</summary>
        public int Target2 = -1;
        /// <summary>攻撃（とカンガルー）の投入EN。</summary>
        public int Bid;
        /// <summary>ドローする山札（スライムの胴の追加ドローにも使う）。</summary>
        public DeckType Deck;
        /// <summary>公開する部位。</summary>
        public Slot RevealSlot;
        /// <summary>交換するスロット（カメレオン・タコ）。</summary>
        public Slot SwapSlot;
        /// <summary>移動先（チーター・カエル）、蹴り出し先（ウマ）。</summary>
        public Area DestArea;
        /// <summary>ユウレイの胴の表示色。</summary>
        public Faction DisplayColor;

        /// <summary>サブアクションの手札使用。null なら使わない。</summary>
        public CardPlay Card;

        /// <summary>前のラウンドのドローで見た2枚のうち、手札に残す方（0 か 1）。</summary>
        public int KeepIndex;

        /// <summary>尋問されたとき公開する部位。</summary>
        public Slot InterrogateSlot;

        public static ActionPlan Charge() => new ActionPlan { Action = ActionType.Charge };
    }

    public sealed class CardPlay
    {
        public int HandIndex;
        public int Target = -1;
        public int Target2 = -1;
        public Slot Slot;
        public Slot Slot2;
        public Area Area;
    }

    /// <summary>ガードフェイズで全員に見せる「誰が誰を狙っているか」。投入ENは含めない。</summary>
    public struct AttackArrow
    {
        public int Attacker;
        public int Target;
    }
}
