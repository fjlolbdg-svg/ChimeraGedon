using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeraGedon.EditorTools
{
    /// <summary>
    /// Play モード中の画面をコードから操作・撮影する検証用ヘルパー（AI エージェントの動作確認用）。
    /// 例: Playtest.Click("ソロで遊ぶ"); Playtest.Snapshot("Temp/shot.png");
    /// </summary>
    public static class Playtest
    {
        /// <summary>表示中のボタンのうち、ラベルが prefix で始まる最初のものを押す。</summary>
        public static bool Click(string prefix)
        {
            foreach (var b in Object.FindObjectsByType<Button>())
            {
                if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                var t = b.GetComponentInChildren<Text>();
                if (t != null && t.text.StartsWith(prefix))
                {
                    b.onClick.Invoke();
                    return true;
                }
            }
            return false;
        }

        /// <summary>表示中で押せるボタンのラベル一覧。</summary>
        public static List<string> Buttons()
        {
            var list = new List<string>();
            foreach (var b in Object.FindObjectsByType<Button>())
            {
                if (!b.gameObject.activeInHierarchy) continue;
                var t = b.GetComponentInChildren<Text>();
                if (t != null) list.Add((b.interactable ? "" : "(x) ") + t.text.Replace("\n", " / "));
            }
            list.Reverse();
            return list;
        }

        /// <summary>UI を含めて 540x960 で描画し PNG に保存する（エディタが非フォーカスでも撮れる）。</summary>
        public static void Snapshot(string path, int width = 540, int height = 960)
        {
            var cam = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>();
            var rt = new RenderTexture(width, height, 24);
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1;
            }
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
