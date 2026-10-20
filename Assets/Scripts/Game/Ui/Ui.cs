using System;
using ChimeraGedon.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ChimeraGedon.Game
{
    /// <summary>
    /// uGUI をコードで組み立てるための小さな部品集。シーンの YAML に UI を持たせず、
    /// レイアウトはすべてスクリプト側に置く（DOUBT! の GameSceneBuilder と同じ考え方）。
    /// 基準解像度は 1080x1920（縦）。
    /// </summary>
    public static class Ui
    {
        public static Font Font;

        public static readonly Color Bg = new Color(0.08f, 0.07f, 0.12f);
        public static readonly Color Panel = new Color(0.15f, 0.13f, 0.22f, 0.95f);
        public static readonly Color PanelLight = new Color(0.24f, 0.21f, 0.33f, 1f);
        public static readonly Color TextMain = new Color(0.95f, 0.94f, 1f);
        public static readonly Color TextSub = new Color(0.72f, 0.70f, 0.82f);
        public static readonly Color Accent = new Color(1f, 0.78f, 0.25f);
        public static readonly Color ButtonColor = new Color(0.33f, 0.28f, 0.52f);
        public static readonly Color ButtonDisabled = new Color(0.22f, 0.21f, 0.26f);
        public static readonly Color Unknown = new Color(0.45f, 0.45f, 0.5f);

        public static Color FactionColor(Faction f)
        {
            switch (f)
            {
                case Faction.Red: return new Color(0.92f, 0.28f, 0.30f);
                case Faction.Blue: return new Color(0.28f, 0.52f, 0.95f);
                default: return new Color(0.30f, 0.80f, 0.40f);
            }
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>アンカーを親に対する割合（0〜1）で指定して矩形を作る。</summary>
        public static RectTransform Rect(Transform parent, string name, float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Box(Transform parent, string name, float xMin, float yMin, float xMax, float yMax, Color color)
        {
            var rt = Rect(parent, name, xMin, yMin, xMax, yMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null,
            float xMin = 0, float yMin = 0, float xMax = 1, float yMax = 1)
        {
            var rt = Rect(parent, "Label", xMin, yMin, xMax, yMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextMain;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, int size, Action onClick,
            float xMin = 0, float yMin = 0, float xMax = 1, float yMax = 1, Color? color = null)
        {
            var img = Box(parent, "Button", xMin, yMin, xMax, yMax, color ?? ButtonColor);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            btn.colors = colors;
            var txt = Label(img.transform, label, size, TextAnchor.MiddleCenter);
            var pad = txt.rectTransform;
            pad.offsetMin = new Vector2(12, 4);
            pad.offsetMax = new Vector2(-12, -4);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static void SetLabel(Button b, string text) => b.GetComponentInChildren<Text>().text = text;

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var go = t.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go); // エディタでのプレビュー描画用
            }
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static string Colored(string text, Color c) => $"<color=#{Hex(c)}>{text}</color>";
    }
}
