using System.Collections.Generic;
using System.Linq;
using ChimeraGedon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeraGedon.Game
{
    /// <summary>
    /// 試合画面（TECH_SPEC 4章の縦レイアウト）。上から: 上部バー / 2×2 マップ / 自分のキメラ / ログ / 操作エリア。
    /// 状態は持たず、Refresh で MatchState から毎回描き直す。viewer の席から見える情報だけを表示する。
    /// </summary>
    public sealed class GameScreen : IMatchView
    {
        readonly RectTransform root;
        readonly Text topBar;
        readonly Text[] areaHeaders = new Text[4];
        readonly RectTransform[] areaTokens = new RectTransform[4];
        readonly Image[] areaBoxes = new Image[4];
        readonly Text[] partHeads = new Text[3];
        readonly Text[] partTexts = new Text[3];
        readonly Image[] partBoxes = new Image[3];
        readonly Text status;
        readonly Text handText;
        readonly Text logText;
        readonly Text phaseText;
        readonly List<string> logLines = new List<string>();

        const int LogLines = 6;

        public GameScreen(Canvas canvas)
        {
            root = Ui.Rect(canvas.transform, "GameScreen", 0, 0, 1, 1);

            var bar = Ui.Box(root, "TopBar", 0, 0.955f, 1, 1, Ui.Panel);
            topBar = Ui.Label(bar.transform, "", 30, TextAnchor.MiddleCenter, Ui.TextMain, 0.02f, 0, 0.98f, 1);

            // マップ: ①祭壇(左上) ②研究所(右上) ③闘技場(右下) ④巣(左下) → 輪状に隣接
            var map = Ui.Rect(root, "Map", 0.01f, 0.635f, 0.99f, 0.952f);
            var cellRect = new[]
            {
                new Vector4(0, 0.5f, 0.5f, 1), new Vector4(0.5f, 0.5f, 1, 1),
                new Vector4(0.5f, 0, 1, 0.5f), new Vector4(0, 0, 0.5f, 0.5f),
            };
            for (int i = 0; i < 4; i++)
            {
                var r = cellRect[i];
                var cell = Ui.Box(map, "Area" + i, r.x, r.y, r.z, r.w, Ui.Panel);
                var outline = cell.gameObject.AddComponent<Outline>();
                outline.effectColor = Ui.Bg;
                outline.effectDistance = new Vector2(4, 4);
                areaBoxes[i] = cell;
                areaHeaders[i] = Ui.Label(cell.transform, "", 26, TextAnchor.MiddleLeft, Ui.Accent, 0.04f, 0.84f, 0.98f, 0.99f);
                var tokens = Ui.Rect(cell.transform, "Tokens", 0.02f, 0.02f, 0.98f, 0.84f);
                var grid = tokens.gameObject.AddComponent<VerticalLayoutGroup>();
                grid.spacing = 2;
                grid.childControlHeight = true;
                grid.childControlWidth = true;
                grid.childForceExpandHeight = false;
                areaTokens[i] = tokens;
            }

            // 自分のキメラ
            var me = Ui.Box(root, "MyPanel", 0.01f, 0.435f, 0.99f, 0.632f, Ui.Panel);
            for (int s = 0; s < 3; s++)
            {
                float x0 = 0.01f + s * 0.33f;
                partBoxes[s] = Ui.Box(me.transform, "Part" + s, x0, 0.30f, x0 + 0.32f, 0.98f, Ui.PanelLight);
                partHeads[s] = Ui.Label(partBoxes[s].transform, "", 22, TextAnchor.UpperLeft, Ui.TextMain, 0.05f, 0.62f, 0.97f, 0.97f);
                partHeads[s].resizeTextForBestFit = true;
                partHeads[s].resizeTextMinSize = 16;
                partHeads[s].resizeTextMaxSize = 22;
                partTexts[s] = Ui.Label(partBoxes[s].transform, "", 19, TextAnchor.UpperLeft, Ui.TextSub, 0.05f, 0.03f, 0.97f, 0.62f);
                partTexts[s].resizeTextForBestFit = true;
                partTexts[s].resizeTextMinSize = 13;
                partTexts[s].resizeTextMaxSize = 19;
            }
            status = Ui.Label(me.transform, "", 26, TextAnchor.MiddleLeft, Ui.TextMain, 0.02f, 0.15f, 0.98f, 0.30f);
            handText = Ui.Label(me.transform, "", 22, TextAnchor.MiddleLeft, Ui.TextSub, 0.02f, 0.0f, 0.98f, 0.15f);

            var log = Ui.Box(root, "Log", 0.01f, 0.338f, 0.99f, 0.432f, new Color(0, 0, 0, 0.35f));
            logText = Ui.Label(log.transform, "", 22, TextAnchor.LowerLeft, Ui.TextSub, 0.02f, 0.02f, 0.98f, 0.98f);
            logText.verticalOverflow = VerticalWrapMode.Overflow;
            var mask = log.gameObject.AddComponent<RectMask2D>();

            phaseText = Ui.Label(root, "", 34, TextAnchor.MiddleCenter, Ui.TextSub, 0.05f, 0.05f, 0.95f, 0.3f);
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void SetPhase(string text) => phaseText.text = text;

        public void ClearLog()
        {
            logLines.Clear();
            logText.text = "";
        }

        public void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            logLines.Add(line);
            while (logLines.Count > LogLines) logLines.RemoveAt(0);
            logText.text = string.Join("\n", logLines);
        }

        public void Refresh(MatchState s, int viewer)
        {
            var me = s.P(viewer);
            string redBlue = s.AnnouncedRedBlueRange
                ? $"赤{Mathf.Min(s.AnnouncedRed, s.AnnouncedBlue)}〜{Mathf.Max(s.AnnouncedRed, s.AnnouncedBlue)} 青{Mathf.Min(s.AnnouncedRed, s.AnnouncedBlue)}〜{Mathf.Max(s.AnnouncedRed, s.AnnouncedBlue)}"
                : $"赤{s.AnnouncedRed} 青{s.AnnouncedBlue}";
            string doom = s.Round >= Balance.DoomStartRound
                ? Ui.Colored($"終末 {Balance.DoomDamage(s.Round)}ダメ/R", Ui.FactionColor(Faction.Red))
                : $"終末まで {Balance.DoomStartRound - s.Round}R";
            topBar.text = $"R{s.Round}/{Balance.FinalRound}  {doom}  開始時 {redBlue} 緑{s.AnnouncedGreen}";

            for (int a = 0; a < 4; a++)
            {
                var area = (Area)a;
                areaHeaders[a].text = $"{Texts.Area(area)} <size=20><color=#{Ui.Hex(Ui.TextSub)}>{Texts.AreaEffect(area)}</color></size>";
                areaBoxes[a].color = me.IsAlive && me.Area == area ? new Color(0.22f, 0.20f, 0.34f) : Ui.Panel;
                Ui.ClearChildren(areaTokens[a]);
                if (s.Phase == MatchPhase.AreaSelect) continue; // 初期エリアが決まるまで駒は置かない
                foreach (var p in s.Players.Where(x => x.Area == area))
                    AddToken(areaTokens[a], s, p, viewer);
            }

            for (int i = 0; i < 3; i++)
            {
                var part = me.Parts[i];
                var def = part.Def;
                string state = part.Revealed ? "公開済" : "未公開";
                string skill = part.Revealed || !def.HasHiddenSkill ? def.RevealText : $"{def.RevealText}\n潜伏: {def.HiddenText}";
                string mission = part.MissionId == null ? "" :
                    $"\n使命: {Catalog.Mission(part.MissionId).Name}{(me.Faction == Faction.Green ? "" : "（緑のとき有効）")}";
                partHeads[i].text = $"{Texts.Slot((Slot)i)} {Texts.Chip(part.Color)} <b>{def.Name}</b>\nHP{def.Hp}  {state}";
                partTexts[i].text = skill + mission;
                partBoxes[i].color = part.Revealed ? new Color(0.30f, 0.27f, 0.40f) : Ui.PanelLight;
            }

            string statusFx = StatusMarks(s, me);
            status.text = $"あなた: {Texts.Chip(me.Faction)}陣営  HP {me.Hp}/{me.MaxHp}  EN {me.EN}/{Balance.MaxEN}  {statusFx}";
            string equip = me.Equipment.Count > 0 ? string.Join("、", me.Equipment.Select(e => Catalog.Card(e).Name)) : "なし";
            string hand = me.Hand.Count > 0 ? string.Join("、", me.Hand.Select(c => Catalog.Card(c).Name)) : "なし";
            handText.text = $"手札: {hand}　装備: {equip}";
        }

        void AddToken(RectTransform parent, MatchState s, PlayerState p, int viewer)
        {
            var row = Ui.Rect(parent, "Token" + p.Seat, 0, 0, 1, 1);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 34;
            string chips = string.Concat(p.Parts.Select(part =>
                part.Revealed || p.Seat == viewer ? Ui.Colored(Texts.Mark(part.Revealed ? part.PublicColor : part.Color), Ui.FactionColor(part.Revealed ? part.PublicColor : part.Color))
                    : Ui.Colored("？", Ui.Unknown)));
            string name = p.Seat == viewer ? Ui.Colored("★" + p.Name, Ui.Accent) : p.Name;
            string line;
            if (p.IsDown)
                line = Ui.Colored($"{p.Name} ダウン（{Texts.Faction(p.FrozenFaction)}）", Ui.Unknown) + " " + chips;
            else
                line = $"{name} {chips} HP{p.Hp} EN{p.PublicEN} 手{p.Hand.Count}{(p.Equipment.Count > 0 ? $" 装{p.Equipment.Count}" : "")} {StatusMarks(s, p)}";
            var t = Ui.Label(row, line, 22, TextAnchor.MiddleLeft);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 14;
            t.resizeTextMaxSize = 22;
        }

        static string StatusMarks(MatchState s, PlayerState p)
        {
            var marks = new List<string>();
            if (s.Round == 0) return "";
            if (p.SealedRound == s.Round) marks.Add("封");
            if (p.ImmobileRound == s.Round) marks.Add("枷");
            if (p.UntargetableRound == s.Round) marks.Add("隠");
            if (p.PoisonRounds > 0) marks.Add("毒");
            if (s.Round <= p.HalfDamageUntilRound) marks.Add("甲");
            if (p.InterrogateRound == s.Round) marks.Add("問");
            return marks.Count == 0 ? "" : Ui.Colored("[" + string.Join("", marks) + "]", Ui.Accent);
        }
    }
}
