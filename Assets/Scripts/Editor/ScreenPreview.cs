using System.IO;
using ChimeraGedon.Core;
using ChimeraGedon.Game;
using UnityEditor;
using UnityEngine;

namespace ChimeraGedon.EditorTools
{
    /// <summary>
    /// Play せずに試合画面を PNG に描き出す（見た目の確認用）。ボット対戦を指定ラウンドまで進めた状態を、席0の視点で描く。
    /// 出力: Temp/ScreenPreview.png
    /// </summary>
    public static class ScreenPreview
    {
        [MenuItem("ChimeraGedon/Render Game Screen Preview")]
        static void RenderMenu() => Render(seed: 3, players: 6, rounds: 4, path: "Temp/ScreenPreview.png");

        public static void Render(int seed, int players, int rounds, string path)
        {
            var engine = BotSimulator.RunOne(seed, players, roundLimit: rounds);
            Ui.Font = Resources.Load<Font>("Fonts/NotoSansJP-Regular");

            var camGo = new GameObject("PreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ui.Bg;
            cam.orthographic = true;
            var canvas = Ui.CreateCanvas("PreviewCanvas");
            canvas.gameObject.hideFlags = HideFlags.HideAndDontSave;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1;
            Ui.Box(canvas.transform, "Background", 0, 0, 1, 1, Ui.Bg);
            var screen = new GameScreen(canvas);
            foreach (var e in engine.State.Log)
                screen.AppendLog(Texts.Event(engine.State, e, 0));
            screen.Refresh(engine.State, 0);
            screen.SetPhase("（プレビュー）");

            const int w = 540, h = 960;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(canvas.gameObject);
            Object.DestroyImmediate(camGo);
            Debug.Log("ScreenPreview: " + Path.GetFullPath(path));
        }
    }
}
