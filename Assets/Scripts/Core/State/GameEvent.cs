namespace ChimeraGedon.Core
{
    public enum GameEventType
    {
        MatchStarted,
        RoundStarted,
        Moved,
        TrapTriggered,
        PartRevealed,
        SkillFizzled,
        CardUsed,
        Equipped,
        AttackDeclared,
        AttackResolved,
        AttackFizzled,
        Damaged,
        Healed,
        EnergyChanged,
        Swapped,
        SwapCancelled,
        /// <summary>交換で受け取った部位の中身。当事者にだけ見える。</summary>
        SwapReceived,
        /// <summary>自分の陣営が変わった。本人にだけ見える。</summary>
        FactionChanged,
        /// <summary>解析系の結果。使った本人にだけ見える。</summary>
        ScanResult,
        StatusApplied,
        CardDrawn,
        CardStolen,
        Down,
        Doom,
        MatchEnded,
    }

    /// <summary>
    /// 解決の結果を UI が順に再生するための記録。VisibleTo が -1 なら全員、それ以外はその席だけに見せる。
    /// ネットワーク配信時はこの VisibleTo で受信者ごとにフィルタする（TECH_SPEC 3-2）。
    /// </summary>
    public sealed class GameEvent
    {
        public int Round;
        public GameEventType Type;
        public int Actor = -1;
        public int Target = -1;
        public int Value;
        public int Value2;
        public string Detail;
        public int VisibleTo = -1;

        public bool IsVisibleTo(int seat) => VisibleTo < 0 || VisibleTo == seat;

        public override string ToString()
        {
            string who = VisibleTo < 0 ? "" : $" [only {VisibleTo}]";
            return $"R{Round} {Type} actor={Actor} target={Target} v={Value}/{Value2} {Detail}{who}";
        }
    }
}
