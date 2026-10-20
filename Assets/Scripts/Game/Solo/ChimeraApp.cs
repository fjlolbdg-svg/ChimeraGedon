using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChimeraGedon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeraGedon.Game
{
    /// <summary>
    /// アプリの入口（Main シーンに1つ置く）。タイトル → 人数・デッキ選択 → ソロ対戦（自分＋ボット）→ リザルト。
    /// 試合の進行は MatchEngine に任せ、ここは入力の受け付けと演出だけを行う。
    /// オンライン対戦を作るときは、このクラスの「入力を集めてエンジンに渡す」部分をネットワーク経由に差し替える。
    /// </summary>
    public sealed partial class ChimeraApp : MonoBehaviour
    {
        /// <summary>ログ1行ごとの演出待ち（ミリ秒）。</summary>
        public static int EventDelayMs = 450;

        Canvas canvas;
        GameScreen screen;
        OptionModal modal;
        RectTransform titleRoot;

        int playerCount = 6;
        readonly string[] deck = { PartIds.Owl, PartIds.Bear, PartIds.Frog };

        void Awake()
        {
            Application.targetFrameRate = 60;
            Ui.Font = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
            Ui.EnsureEventSystem();
            if (Camera.main != null) Camera.main.backgroundColor = Ui.Bg;

            LoadPrefs();
            canvas = Ui.CreateCanvas("UI");
            Ui.Box(canvas.transform, "Background", 0, 0, 1, 1, Ui.Bg);
            screen = new GameScreen(canvas);
            BuildTitle();
            modal = new OptionModal(canvas);
        }

        async void Start()
        {
            try
            {
                while (this != null)
                {
                    await RunTitle();
                    var rng = new System.Random();
                    bool again;
                    do
                    {
                        var match = new SoloMatch(modal, screen, (string[])deck.Clone(), playerCount, rng.Next(), EventDelayMs);
                        again = await match.Run();
                    } while (again);
                }
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                Debug.LogException(e);
            }
        }

        // ───────────────────────── タイトル・デッキ ─────────────────────────

        void BuildTitle()
        {
            titleRoot = Ui.Rect(canvas.transform, "Title", 0, 0, 1, 1);
            Ui.Label(titleRoot, "キメラゲドン", 110, TextAnchor.MiddleCenter, Ui.Accent, 0, 0.62f, 1, 0.8f);
            Ui.Label(titleRoot, "ガチャから出た色で、味方が変わる。", 40, TextAnchor.MiddleCenter, Ui.TextSub, 0, 0.55f, 1, 0.62f);
        }

        async Task RunTitle()
        {
            screen.SetVisible(false);
            titleRoot.gameObject.SetActive(true);
            while (true)
            {
                int pick = await modal.Choose("キメラゲドン", $"人数: {playerCount}人（あなた＋ボット{playerCount - 1}）\nデッキ: {DeckLabel()}",
                    new[] { "ソロで遊ぶ", $"人数を変える（いま {playerCount}人）", "デッキを組む", "あそびかた" }, cancellable: false);
                if (pick == 0) break;
                if (pick == 1) await ChoosePlayerCount();
                if (pick == 2) await ChooseDeck();
                SavePrefs();
                if (pick == 3) await ShowRules();
            }
            titleRoot.gameObject.SetActive(false);
        }

        const string PrefPlayers = "cg.players", PrefDeck = "cg.deck";

        void LoadPrefs()
        {
            playerCount = Mathf.Clamp(PlayerPrefs.GetInt(PrefPlayers, playerCount), Balance.MinPlayers, Balance.MaxPlayers);
            var saved = PlayerPrefs.GetString(PrefDeck, "").Split(',');
            if (saved.Length != 3) return;
            for (int i = 0; i < 3; i++)
                if (Catalog.Parts.Any(p => p.Id == saved[i] && (int)p.Slot == i)) deck[i] = saved[i];
        }

        void SavePrefs()
        {
            PlayerPrefs.SetInt(PrefPlayers, playerCount);
            PlayerPrefs.SetString(PrefDeck, string.Join(",", deck));
            PlayerPrefs.Save();
        }

        string DeckLabel() => string.Join(" / ", deck.Select(id => Catalog.Part(id).Name));

        async Task ChoosePlayerCount()
        {
            var counts = Enumerable.Range(Balance.MinPlayers, Balance.MaxPlayers - Balance.MinPlayers + 1).ToList();
            int i = await modal.Choose("人数", "ボットが足りない席を埋めます。おすすめは6人。", counts.Select(c => $"{c}人"), full: true);
            if (i >= 0) playerCount = counts[i];
        }

        async Task ChooseDeck()
        {
            for (int slot = 0; slot < 3; slot++)
            {
                var parts = Catalog.PartsFor((Slot)slot);
                var labels = parts.Select(p => $"{p.Name}  HP{p.Hp}\n{p.RevealText}" + (p.HasHiddenSkill ? $" / 潜伏: {p.HiddenText}" : "")).ToList();
                int i = await modal.Choose($"{Texts.Slot((Slot)slot)}を選ぶ（{slot + 1}/3）",
                    "スキルが強い部位ほどHPが低い。色は試合開始時のガチャで決まります。", labels, full: true);
                if (i < 0) return;
                deck[slot] = parts[i].Id;
            }
        }

        async Task ShowRules()
        {
            const string rules =
                "・3つのカプセル（頭・胴・脚）の色で陣営が決まる。同じ色が2つ以上ならその色、3色バラバラなら緑。\n" +
                "・赤は生き残っている青を、青は赤を全員倒せば勝ち。緑はカプセルに入った秘密の使命を達成すれば勝ち。\n" +
                "・毎ラウンド、移動 → 行動（攻撃・チャージ・ドロー・公開）を全員同時に決める。\n" +
                "・攻撃はENの秘密入札。攻撃が防御より大きければ命中。使ったENは戻らない。\n" +
                "・部位を公開すると強いスキルが使えるが、色がバレる。未公開の部位は交換されると陣営が変わることも。\n" +
                "・7R目から終末ダメージ。10R目が終わったら生存人数で最終審判。";
            await modal.Choose("あそびかた", rules, new[] { "わかった" }, cancellable: false, full: true);
        }
    }
}
