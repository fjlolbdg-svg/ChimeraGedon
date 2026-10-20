using System.Collections.Generic;
using System.Threading.Tasks;
using ChimeraGedon.Core;

namespace ChimeraGedon.Game
{
    /// <summary>プレイヤーに選択肢を出して1つ選ばせる（本番は OptionModal、テストは自動選択）。キャンセルは -1。</summary>
    public interface IChooser
    {
        Task<int> Choose(string title, string body, IList<OptionModal.Option> options, bool cancellable = true, bool full = false);
    }

    /// <summary>試合画面（本番は GameScreen、テストは何もしない実装）。</summary>
    public interface IMatchView
    {
        void SetVisible(bool visible);
        void SetPhase(string text);
        void ClearLog();
        void AppendLog(string line);
        void Refresh(MatchState state, int viewer);
    }

    public static class ChooserExtensions
    {
        public static Task<int> Choose(this IChooser chooser, string title, string body, IEnumerable<string> labels, bool cancellable = true, bool full = false)
        {
            var options = new List<OptionModal.Option>();
            foreach (var l in labels) options.Add(new OptionModal.Option(l));
            return chooser.Choose(title, body, options, cancellable, full);
        }
    }
}
