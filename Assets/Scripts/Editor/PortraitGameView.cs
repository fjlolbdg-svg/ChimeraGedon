using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ChimeraGedon.EditorTools
{
    /// <summary>ゲームビューを縦 540x960（1080x1920 の半分）に切り替える。Unity の内部 API をリフレクションで使う。</summary>
    public static class PortraitGameView
    {
        const string Label = "ChimeraGedon Portrait";

        [MenuItem("ChimeraGedon/Game View: Portrait 540x960")]
        public static void Apply()
        {
            var asm = typeof(EditorWindow).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
            var groupType = group.GetType();
            var sizeType = asm.GetType("UnityEditor.GameViewSize");
            var sizeKind = asm.GetType("UnityEditor.GameViewSizeType");

            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < count; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((string)sizeType.GetProperty("baseText").GetValue(size) == Label) index = i;
            }
            if (index < 0)
            {
                var ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new object[] { Enum.Parse(sizeKind, "FixedResolution"), 540, 960, Label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = count;
            }

            var gameViewType = asm.GetType("UnityEditor.GameView");
            var gameView = EditorWindow.GetWindow(gameViewType);
            var select = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            select.Invoke(gameView, new object[] { index, null });
        }
    }
}
