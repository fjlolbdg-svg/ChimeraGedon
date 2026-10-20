using System;
using System.Collections.Generic;

namespace ChimeraGedon.Core
{
    /// <summary>カプセル1つ＝装着された部位1つ。交換では丸ごと移動する（HP・スキル・色・使命）。</summary>
    public sealed class PartInstance
    {
        public string PartId;
        /// <summary>本当の色。陣営判定はこれで行う。</summary>
        public Faction Color;
        /// <summary>緑カプセルにだけ入る使命。それ以外は null。</summary>
        public string MissionId;
        public bool Revealed;
        /// <summary>ユウレイの胴の公開時に選んだ表示色。</summary>
        public bool HasDisplayColor;
        public Faction DisplayColor;

        public PartDef Def => Catalog.Part(PartId);

        /// <summary>公開済みのとき他人に見える色。</summary>
        public Faction PublicColor => HasDisplayColor ? DisplayColor : Color;
    }

    public sealed class PlayerState
    {
        public int Seat;
        public string Name;
        public bool IsBot;

        public readonly PartInstance[] Parts = new PartInstance[3];
        /// <summary>受けたダメージの累計。HP = 最大HP − Damage（GAME_SPEC 3章）。</summary>
        public int Damage;
        public int EN;
        /// <summary>計画フェイズ開始時の EN。予約で減った値を他人に見せないよう、公開表示はこちらを使う。</summary>
        public int PublicEN;
        public readonly List<string> Hand = new List<string>();
        public readonly List<string> Equipment = new List<string>();
        /// <summary>ドローで見た2枚。次の計画で1枚を選ぶ。</summary>
        public List<string> PendingDraw;
        public DeckType PendingDrawDeck;
        public Area Area;

        public bool IsDown;
        public Faction FrozenFaction;
        public int DownRound;
        public int EquipmentAtDown;

        // 状態異常（「そのラウンド番号のときだけ有効」で持つ）
        public int SealedRound;
        public int ImmobileRound;
        public int UntargetableRound;
        public int InterrogateRound;
        public int HalfDamageUntilRound;
        public int PoisonRounds;
        public int PoisonSource = -1;

        // 潜伏スキルの使用状況
        public bool SlimeUsed;
        public bool InkUsed;
        public int LiarUsesLeft = Balance.LiarUses;

        /// <summary>偽装プロファイル（キツネ・ユウレイの潜伏スキル用）。null なら正直に答える。</summary>
        public Faction? LieColor;
        public CompatAnswer LieCompat = CompatAnswer.Honest;

        // ラウンドごと
        public int RoundDamageTaken;
        public int BarrierPool;
        public bool AmuletUsedThisRound;
        public readonly List<int> AttackersThisRound = new List<int>();
        public int LastDamageSource = -1;
        public int LastScanRound;

        // 使命・統計
        public int SwapCount;
        public int Kills;
        public bool DragAlongAchieved;
        public int RevealCount;

        public int MaxHp
        {
            get
            {
                int sum = 0;
                foreach (var p in Parts) if (p != null) sum += p.Def.Hp;
                return sum;
            }
        }

        public int Hp => Math.Max(0, MaxHp - Damage);

        public bool IsAlive => !IsDown;

        public Faction Faction => IsDown ? FrozenFaction : FactionRules.Compute(Parts);

        public PartInstance Part(Slot slot) => Parts[(int)slot];

        /// <summary>未公開の部位に指定IDがあるか（潜伏スキル判定）。</summary>
        public bool HasHidden(string partId)
        {
            foreach (var p in Parts) if (p != null && !p.Revealed && p.PartId == partId) return true;
            return false;
        }

        public bool HasRevealed(string partId)
        {
            foreach (var p in Parts) if (p != null && p.Revealed && p.PartId == partId) return true;
            return false;
        }

        public bool HasEquipment(string cardId) => Equipment.Contains(cardId);
    }

    public sealed class Trap
    {
        public Area Area;
        public int OwnerSeat;
    }

    public sealed class MatchResult
    {
        /// <summary>勝った赤か青。緑だけが勝った・引き分けの場合は null。</summary>
        public Faction? WinningFaction;
        public readonly List<int> Winners = new List<int>();
        public readonly List<int> GreenMissionWinners = new List<int>();
        public string Reason;
    }

    public sealed class MatchState
    {
        public int Seed;
        public Random Rng;
        public int Round;
        public int StartSeat;
        public MatchPhase Phase;
        public readonly List<PlayerState> Players = new List<PlayerState>();
        public readonly Dictionary<DeckType, List<string>> DrawPiles = new Dictionary<DeckType, List<string>>();
        public readonly Dictionary<DeckType, List<string>> DiscardPiles = new Dictionary<DeckType, List<string>>();
        public readonly List<Trap> Traps = new List<Trap>();
        public readonly List<GameEvent> Log = new List<GameEvent>();
        /// <summary>試合開始時の陣営人数（公開情報）。6人戦は赤青どちらかが+1になる。</summary>
        public int AnnouncedRed, AnnouncedBlue, AnnouncedGreen;
        public bool AnnouncedRedBlueRange;
        public MatchResult Result;

        public PlayerState P(int seat) => Players[seat];

        public IEnumerable<PlayerState> Alive()
        {
            foreach (var p in Players) if (p.IsAlive) yield return p;
        }
    }
}
