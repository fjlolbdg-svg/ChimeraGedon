using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeraGedon.Game
{
    /// <summary>
    /// 選択肢を縦に並べて1つ選ばせるモーダル。await で結果（選んだ index、キャンセルは -1）を受け取れる。
    /// 対象・部位・エリア・入札額など、計画の入力はすべてこれで行う（スマホで押しやすい大きなボタン）。
    /// </summary>
    public sealed class OptionModal
    {
        public struct Option
        {
            public string Label;
            public bool Enabled;
            public Option(string label, bool enabled = true) { Label = label; Enabled = enabled; }
        }

        readonly RectTransform root;
        readonly Image dim;
        readonly RectTransform panelRt;
        readonly Text title;
        readonly Text body;
        readonly RectTransform list;
        TaskCompletionSource<int> pending;

        public bool IsOpen => root.gameObject.activeSelf;

        public OptionModal(Canvas canvas)
        {
            dim = Ui.Box(canvas.transform, "OptionModal", 0, 0, 1, 1, new Color(0, 0, 0, 0.6f));
            root = dim.rectTransform;
            var panel = Ui.Box(root, "Panel", 0.05f, 0.08f, 0.95f, 0.92f, Ui.Panel);
            panelRt = panel.rectTransform;
            title = Ui.Label(panel.transform, "", 40, TextAnchor.MiddleCenter, Ui.Accent, 0.04f, 0.88f, 0.96f, 0.98f);
            body = Ui.Label(panel.transform, "", 28, TextAnchor.UpperLeft, Ui.TextSub, 0.05f, 0.66f, 0.95f, 0.87f);
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 18;
            body.resizeTextMaxSize = 28;

            list = Ui.Rect(panel.transform, "List", 0.05f, 0.03f, 0.95f, 0.65f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;
            root.gameObject.SetActive(false);
        }

        /// <summary>full=false のときは画面下部だけに出し、上の盤面を見ながら選べるようにする。</summary>
        public Task<int> Choose(string titleText, string bodyText, IList<Option> options, bool cancellable = true, bool full = false)
        {
            if (full)
            {
                panelRt.anchorMin = new Vector2(0.05f, 0.1f);
                panelRt.anchorMax = new Vector2(0.95f, 0.9f);
                dim.color = new Color(0, 0, 0, 0.6f);
            }
            else
            {
                panelRt.anchorMin = new Vector2(0.01f, 0.005f);
                panelRt.anchorMax = new Vector2(0.99f, 0.335f);
                dim.color = new Color(0, 0, 0, 0f);
            }
            pending?.TrySetResult(-1);
            pending = new TaskCompletionSource<int>();
            title.text = titleText;
            body.text = bodyText ?? "";
            Ui.ClearChildren(list);

            int count = options.Count + (cancellable ? 1 : 0);
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = full ? 14 : 6;
            float height = Mathf.Clamp((full ? 900f : 390f) / Mathf.Max(1, count) - layout.spacing, full ? 70 : 36, full ? 140 : 90);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                AddButton(options[i].Label, height, options[i].Enabled, Ui.ButtonColor, () => Finish(index));
            }
            if (cancellable) AddButton("もどる", height, true, Ui.ButtonDisabled, () => Finish(-1));

            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            return pending.Task;
        }

        public Task<int> Choose(string titleText, string bodyText, IEnumerable<string> labels, bool cancellable = true, bool full = false)
        {
            var opts = new List<Option>();
            foreach (var l in labels) opts.Add(new Option(l));
            return Choose(titleText, bodyText, opts, cancellable, full);
        }

        void AddButton(string label, float height, bool enabled, Color color, System.Action onClick)
        {
            var b = Ui.Button(list, label, 32, onClick, color: color);
            b.interactable = enabled;
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var txt = b.GetComponentInChildren<Text>();
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 20;
            txt.resizeTextMaxSize = 32;
        }

        void Finish(int index)
        {
            root.gameObject.SetActive(false);
            var p = pending;
            pending = null;
            p?.TrySetResult(index);
        }

        public void Close() => Finish(-1);
    }
}
