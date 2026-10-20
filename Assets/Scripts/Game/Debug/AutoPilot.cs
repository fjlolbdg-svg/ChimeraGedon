#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeraGedon.Game
{
    /// <summary>
    /// 検証用オートパイロット: 表示中のボタンから「もどる」以外をランダムに押し続け、画面経由で試合を最後まで通す。
    /// stopAt で始まるボタンが現れたら止まる。使い方: AutoPilot.Run(seed)。
    /// </summary>
    public sealed class AutoPilot : MonoBehaviour
    {
        public static AutoPilot Instance;
        public int Clicks;
        public string LastClick;
        public bool Stopped;
        public string StopAt = "もう一度";
        public int Seed = 1;
        System.Random rng;
        float next;

        public static AutoPilot Run(int seed, string stopAt = "もう一度")
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = new GameObject("AutoPilot").AddComponent<AutoPilot>();
            Instance.Seed = seed;
            Instance.StopAt = stopAt;
            return Instance;
        }

        void Update()
        {
            if (Stopped || Time.unscaledTime < next) return;
            if (rng == null) rng = new System.Random(Seed);
            next = Time.unscaledTime + 0.02f;
            var candidates = new List<Button>();
            foreach (var b in FindObjectsByType<Button>())
            {
                if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                var t = b.GetComponentInChildren<Text>();
                if (t == null) continue;
                if (!string.IsNullOrEmpty(StopAt) && t.text.StartsWith(StopAt)) { Stopped = true; return; }
                if (t.text == "もどる" || t.text.StartsWith("あそびかた") || t.text.StartsWith("人数を変える") || t.text.StartsWith("デッキを組む")) continue;
                candidates.Add(b);
            }
            if (candidates.Count == 0) return;
            var pick = candidates[rng.Next(candidates.Count)];
            LastClick = pick.GetComponentInChildren<Text>().text;
            Clicks++;
            pick.onClick.Invoke();
        }
    }
}
#endif
