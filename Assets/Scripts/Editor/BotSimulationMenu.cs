using ChimeraGedon.Core;
using UnityEditor;
using UnityEngine;

namespace ChimeraGedon.EditorTools
{
    /// <summary>ボット同士の自動対戦でバランス指標を出す（GAME_SPEC 11章）。結果は Console に出力。</summary>
    public static class BotSimulationMenu
    {
        [MenuItem("ChimeraGedon/Simulate Bot Matches (6 players x 1000)")]
        static void Simulate6() => Run(6, 1000);

        [MenuItem("ChimeraGedon/Simulate Bot Matches (4-8 players x 500)")]
        static void SimulateAll()
        {
            for (int n = Balance.MinPlayers; n <= Balance.MaxPlayers; n++) Run(n, 500);
        }

        static void Run(int players, int matches)
        {
            var stats = BotSimulator.Run(matches, players, seed: System.Environment.TickCount);
            Debug.Log(stats.ToReport());
        }
    }
}
