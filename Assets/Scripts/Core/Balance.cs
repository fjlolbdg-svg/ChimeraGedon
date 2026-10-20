namespace ChimeraGedon.Core
{
    /// <summary>
    /// 数値の初期値。変更したら docs/GAME_SPEC.md の「バランス調整ログ」に理由を残すこと。
    /// </summary>
    public static class Balance
    {
        public const int MinPlayers = 4;
        public const int MaxPlayers = 8;

        public const int StartEN = 3;
        public const int MaxEN = 8;
        public const int RoundIncomeEN = 1;
        public const int ChargeEN = 3;
        public const int DrawCost = 2;
        public const int LabDrawDiscount = 1;
        public const int HandLimit = 2;
        public const int EquipmentLimit = 3;

        public const int RoundDamageCap = 5;
        public const int AltarRevealEN = 1;
        public const int ArenaHitBonus = 1;
        public const int NestHeal = 1;

        public const int LiarUses = 2;

        /// <summary>このラウンドの終了時から終末ダメージが始まる。</summary>
        public const int DoomStartRound = 7;
        /// <summary>このラウンドが終わっても決着しなければ「最終審判」（GAME_SPEC 4-5）。</summary>
        public const int FinalRound = 10;

        /// <summary>終末ダメージ: 7R=1, 8R=1, 9R=2, 10R=2。</summary>
        public static int DoomDamage(int round)
        {
            if (round < DoomStartRound) return 0;
            return round - DoomStartRound < 2 ? 1 : 2;
        }
    }
}
