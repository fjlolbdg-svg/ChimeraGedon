namespace ChimeraGedon.Core
{
    /// <summary>GAME_SPEC 2-1: 同じ色が2つ以上ならその色、3色バラバラなら緑。</summary>
    public static class FactionRules
    {
        public static Faction Compute(PartInstance[] parts)
        {
            int red = 0, blue = 0, green = 0;
            foreach (var p in parts)
            {
                if (p == null) continue;
                switch (p.Color)
                {
                    case Faction.Red: red++; break;
                    case Faction.Blue: blue++; break;
                    default: green++; break;
                }
            }
            return Compute(red, blue, green);
        }

        public static Faction Compute(int red, int blue, int green)
        {
            if (red >= 2) return Faction.Red;
            if (blue >= 2) return Faction.Blue;
            return Faction.Green;
        }
    }
}
