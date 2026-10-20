namespace ChimeraGedon.Core
{
    /// <summary>陣営 = カプセルの色。GAME_SPEC 2章。</summary>
    public enum Faction { Red, Blue, Green }

    public enum Slot { Head = 0, Torso = 1, Legs = 2 }

    /// <summary>2×2 マップ。輪状に隣接（0-1-2-3-0）。GAME_SPEC 6章。</summary>
    public enum Area { Altar = 0, Lab = 1, Arena = 2, Nest = 3 }

    public enum EffectRange { Self, SameArea, Adjacent, All }

    public enum DeckType { Scan, Support, Hex }

    public enum ActionType { Charge, Attack, Draw, Reveal }

    public enum DamageKind { Attack, Effect, Poison, Doom }

    public enum MatchPhase { AreaSelect, Move, Plan, Guard, Finished }

    /// <summary>キツネの頭（潜伏）で相性診断にどう答えるかの事前設定。</summary>
    public enum CompatAnswer { Honest, Same, Different }

    public static class AreaMap
    {
        public const int Count = 4;

        public static bool IsAdjacent(Area a, Area b)
        {
            int d = ((int)a - (int)b + Count) % Count;
            return d == 1 || d == Count - 1;
        }

        public static bool IsSameOrAdjacent(Area a, Area b) => a == b || IsAdjacent(a, b);
    }
}
