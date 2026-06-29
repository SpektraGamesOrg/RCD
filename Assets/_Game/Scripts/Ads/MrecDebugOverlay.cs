using System.Collections.Generic;
using UnityEngine;

namespace Ads
{
    /// <summary>
    /// EDITOR-ONLY debug aid. The MAX SDK editor stub does not render real MREC ad content, so MREC
    /// positions are invisible in-editor. This draws a white "MREC" box at the exact screen rect the
    /// controller sends to the SDK, keyed by ad unit id, so positions/offsets can be tuned in Play
    /// mode. Auto-spawned on first use; not compiled into device builds.
    /// </summary>
    public sealed class MrecDebugOverlay : MonoBehaviour
    {
        private static MrecDebugOverlay _instance;

        // Screen-pixel rects (top-left origin, matching IMGUI), keyed by ad unit id.
        private readonly Dictionary<string, Rect> _rects = new Dictionary<string, Rect>();

        private GUIStyle _boxStyle;
        private Texture2D _bg;

        private static MrecDebugOverlay Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[MrecDebugOverlay]");
                    Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MrecDebugOverlay>();
                }

                return _instance;
            }
        }

        /// <summary>Show/update the debug box for an ad unit at the given screen-pixel rect (top-left origin).</summary>
        public static void Set(string adUnitId, Rect screenRectTopLeft)
        {
            if (string.IsNullOrEmpty(adUnitId))
                return;
            Instance._rects[adUnitId] = screenRectTopLeft;
        }

        /// <summary>Remove the debug box for an ad unit (on hide/destroy).</summary>
        public static void Clear(string adUnitId)
        {
            if (_instance == null || string.IsNullOrEmpty(adUnitId))
                return;
            _instance._rects.Remove(adUnitId);
        }

        private void OnGUI()
        {
            if (_rects.Count == 0)
                return;

            EnsureStyle();
            foreach (var kvp in _rects)
                GUI.Box(kvp.Value, "MREC", _boxStyle);
        }

        private void EnsureStyle()
        {
            if (_boxStyle != null)
                return;

            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.85f)); // near-opaque white
            _bg.Apply();

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 28,
            };
            _boxStyle.normal.background = _bg;
            _boxStyle.normal.textColor = Color.black;
        }
    }
}