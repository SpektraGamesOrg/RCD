using System.Collections.Generic;
using _Game.Scripts.Utils.VContainer;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    /// <summary>
    /// Manages persistent MAX banners/MRECs. MAX keys ad views by AD UNIT ID (not by placement),
    /// so this controller tracks one created view per ad unit id. Placements that share a unit id
    /// (e.g. all adaptive bottom banners) reuse the SAME view: showing a second placement on the
    /// same unit re-shows and, if needed, repositions the existing view rather than creating a
    /// second (which MAX rejects). Gating (AdConfig toggle, missing => off) lives here.
    /// </summary>
    public sealed class MaxBannerController
    {
        private static readonly InfoLogger Logger = new InfoLogger("MaxBannerController", "yellow");

        /// <summary>State of a single created MAX ad view, keyed by ad unit id.</summary>
        private sealed class AdViewState
        {
            public bool IsMrec;

            // The placement currently driving this view. Repositioning compares the requested
            // placement's resolved position against this one to decide whether to move the view.
            public BannerPlacement Placement;
        }

        // Keyed by ad unit id, NOT placement — mirrors how the MAX SDK identifies ad views.
        private readonly Dictionary<string, AdViewState> _views = new Dictionary<string, AdViewState>();

        // Resolved AdConfig source. Placement gating reads the live config through this rather than
        // fabricating a throwaway AdConfig.
        private readonly IAdConfigProvider _configProvider;

        public MaxBannerController(IAdConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public void ShowBanner(BannerPlacement placement)
        {
            if (!BannerPlacementTable.TryGet(placement, out var info))
            {
                Logger.LogError($"No table entry for banner placement {placement}");
                return;
            }

            AdConfig config = _configProvider.Current;
            if (!info.IsEnabled(config))
            {
                Logger.Log($"Banner {placement} gated off (config).");
                return;
            }

            bool isMrec = info.Format == BannerFormat.Mrec;
            string adUnitId = isMrec ? MaxAdConfig.MrecAdUnitId : MaxAdConfig.BannerAdUnitId;

            if (!_views.TryGetValue(adUnitId, out var view))
            {
                // First use of this ad unit: create the view for the requested placement.
                if (isMrec)
                    MaxSdk.CreateMRec(adUnitId, BuildMRecConfig(info));
                else
                    MaxSdk.CreateBanner(adUnitId, BuildBannerConfig(info));

                view = new AdViewState { IsMrec = isMrec, Placement = placement };
                _views[adUnitId] = view;
                Logger.Log($"Created MAX {(isMrec ? "MREC" : "banner")} for {placement} ({DescribePosition(info)}).");
            }
            else if (view.Placement != placement)
            {
                // Same ad unit already exists but a different placement wants it: reposition the
                // existing view instead of creating a second one (MAX keys views by ad unit id).
                RepositionView(adUnitId, isMrec, info);
                view.Placement = placement;
                Logger.Log($"Repositioned MAX {(isMrec ? "MREC" : "banner")} for {placement} ({DescribePosition(info)}).");
            }

            if (isMrec)
                MaxSdk.ShowMRec(adUnitId);
            else
                MaxSdk.ShowBanner(adUnitId);

#if UNITY_EDITOR
            if (isMrec)
                UpdateMrecDebugOverlay(adUnitId, info);
#endif
        }

        public void HideBanner(BannerPlacement placement)
        {
            if (!BannerPlacementTable.TryGet(placement, out var info))
                return;

            bool isMrec = info.Format == BannerFormat.Mrec;
            string adUnitId = isMrec ? MaxAdConfig.MrecAdUnitId : MaxAdConfig.BannerAdUnitId;
            if (!_views.ContainsKey(adUnitId))
                return;

            if (isMrec)
                MaxSdk.HideMRec(adUnitId);
            else
                MaxSdk.HideBanner(adUnitId);

#if UNITY_EDITOR
            if (isMrec)
                MrecDebugOverlay.Clear(adUnitId);
#endif
        }

        public void DestroyBanner(BannerPlacement placement)
        {
            if (!BannerPlacementTable.TryGet(placement, out var info))
                return;

            bool isMrec = info.Format == BannerFormat.Mrec;
            string adUnitId = isMrec ? MaxAdConfig.MrecAdUnitId : MaxAdConfig.BannerAdUnitId;
            DestroyByAdUnit(adUnitId, isMrec);
        }

        public void DestroyAll()
        {
            var views = new List<KeyValuePair<string, AdViewState>>(_views);
            for (var i = 0; i < views.Count; i++)
                DestroyByAdUnit(views[i].Key, views[i].Value.IsMrec);
        }

        private void DestroyByAdUnit(string adUnitId, bool isMrec)
        {
            if (!_views.ContainsKey(adUnitId))
                return;

            if (isMrec)
                MaxSdk.DestroyMRec(adUnitId);
            else
                MaxSdk.DestroyBanner(adUnitId);

            _views.Remove(adUnitId);
            Logger.Log($"Destroyed MAX {(isMrec ? "MREC" : "banner")} ad unit {adUnitId}.");

#if UNITY_EDITOR
            if (isMrec)
                MrecDebugOverlay.Clear(adUnitId);
#endif
        }

        // MREC is a fixed 300x250 dp ad. Used to convert anchor + pixel offset into an absolute
        // top-left coordinate when a placement specifies an offset.
        private const int MrecWidthDp = 300;
        private const int MrecHeightDp = 250;

        private static MaxSdkBase.AdViewConfiguration BuildBannerConfig(BannerPlacementInfo info)
        {
            // Banners don't use coordinate offsets (no placement requests one); keep the enum path.
            return new MaxSdkBase.AdViewConfiguration(ToBannerPosition(info.Anchor))
            {
                IsAdaptive = info.Format == BannerFormat.Adaptive
            };
        }

        private static MaxSdkBase.AdViewConfiguration BuildMRecConfig(BannerPlacementInfo info)
        {
            // With an offset, position by absolute coordinates; otherwise use the SDK's named anchor.
            if (info.Offset.HasValue)
            {
                Vector2 px = ResolveMRecCoordinates(info.Anchor, info.Offset.Value);
                return new MaxSdkBase.AdViewConfiguration(px.x, px.y);
            }

            return new MaxSdkBase.AdViewConfiguration(ToMRecPosition(info.Anchor));
        }

        private static void RepositionView(string adUnitId, bool isMrec, BannerPlacementInfo info)
        {
            if (isMrec)
            {
                if (info.Offset.HasValue)
                {
                    Vector2 px = ResolveMRecCoordinates(info.Anchor, info.Offset.Value);
                    MaxSdk.UpdateMRecPosition(adUnitId, px.x, px.y);
                }
                else
                {
                    MaxSdk.UpdateMRecPosition(adUnitId, ToMRecPosition(info.Anchor));
                }
            }
            else
            {
                MaxSdk.UpdateBannerPosition(adUnitId, ToBannerPosition(info.Anchor));
            }
        }

        /// <summary>
        /// Resolves a Unity-style (anchor + offset) into MAX's absolute coordinate. MAX's CreateMRec/
        /// UpdateMRecPosition coordinates are in DENSITY-INDEPENDENT PIXELS (dp), relative to the
        /// screen's top-left — NOT raw screen pixels. So everything here is computed in dp: the MREC is
        /// a fixed 300x250 dp, and the screen size is converted from pixels via the SDK's density. The
        /// offset is therefore in dp too (+X right, +Y down) and is resolution-independent.
        /// </summary>
        private static Vector2 ResolveMRecCoordinates(BannerAnchor anchor, Vector2 offset)
        {
            // px-per-dp (e.g. 3.0 on a 3x device, 1 in editor). Guard against 0/negative.
            float density = MaxSdkUtils.GetScreenDensity();
            if (density <= 0f)
                density = 1f;

            float w = MrecWidthDp; // MREC is 300x250 dp; MAX coords are dp, so no px conversion.
            float h = MrecHeightDp;
            float screenW = Screen.width / density; // screen size in dp
            float screenH = Screen.height / density;

            // Base X for the MREC's left edge given the horizontal intent of the anchor (dp).
            float baseX;
            switch (anchor)
            {
                case BannerAnchor.CenterRight:
                    baseX = screenW - w; // hug right edge
                    break;
                case BannerAnchor.Center:
                case BannerAnchor.BottomCenter:
                case BannerAnchor.TopCenter:
                default:
                    baseX = (screenW - w) * 0.5f; // horizontally centered
                    break;
            }

            // Base Y for the MREC's top edge given the vertical intent of the anchor (dp).
            float baseY;
            switch (anchor)
            {
                case BannerAnchor.TopCenter:
                    baseY = 0f;
                    break;
                case BannerAnchor.BottomCenter:
                    baseY = screenH - h;
                    break;
                case BannerAnchor.Center:
                case BannerAnchor.CenterRight:
                default:
                    baseY = (screenH - h) * 0.5f; // vertically centered
                    break;
            }

            float x = Mathf.Clamp(baseX + offset.x, 0f, Mathf.Max(0f, screenW - w));
            float y = Mathf.Clamp(baseY + offset.y, 0f, Mathf.Max(0f, screenH - h));
            return new Vector2(x, y);
        }

#if UNITY_EDITOR
        // Editor-only: draw a white "MREC" box at the exact screen rect the SDK is told to use, so
        // MREC positions are visible/tunable in-editor (the MAX editor stub renders no real MREC).
        private static void UpdateMrecDebugOverlay(string adUnitId, BannerPlacementInfo info)
        {
            float density = MaxSdkUtils.GetScreenDensity();
            if (density <= 0f)
                density = 1f;

            // Resolver returns dp (top-left origin); IMGUI uses screen pixels (top-left origin).
            Vector2 dp = ResolveMRecCoordinates(info.Anchor, info.Offset ?? Vector2.zero);
            var rect = new Rect(dp.x * density, dp.y * density, MrecWidthDp * density, MrecHeightDp * density);
            MrecDebugOverlay.Set(adUnitId, rect);
        }
#endif

        private static string DescribePosition(BannerPlacementInfo info)
        {
            return info.Offset.HasValue ? $"{info.Anchor}+offset{info.Offset.Value}" : info.Anchor.ToString();
        }

        private static MaxSdkBase.AdViewPosition ToBannerPosition(BannerAnchor anchor)
        {
            switch (anchor)
            {
                case BannerAnchor.TopCenter:
                    return MaxSdkBase.AdViewPosition.TopCenter;
                case BannerAnchor.BottomCenter:
                default:
                    return MaxSdkBase.AdViewPosition.BottomCenter;
            }
        }

        private static MaxSdkBase.AdViewPosition ToMRecPosition(BannerAnchor anchor)
        {
            switch (anchor)
            {
                case BannerAnchor.CenterRight:
                    return MaxSdkBase.AdViewPosition.CenterRight;
                case BannerAnchor.Center:
                default:
                    return MaxSdkBase.AdViewPosition.Centered;
            }
        }
    }
}