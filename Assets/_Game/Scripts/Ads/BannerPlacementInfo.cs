using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ads
{
    /// <summary>Immutable description of a banner placement: format, anchor, optional pixel offset, and config toggle.</summary>
    public readonly struct BannerPlacementInfo
    {
        public readonly BannerFormat Format;
        public readonly BannerAnchor Anchor;

        /// <summary>
        /// Optional Unity-style pixel offset from the <see cref="Anchor"/> reference point, applied by
        /// the controller (e.g. anchor Left + offset (-300, 0) snaps to the left edge then shifts 300px
        /// left). Null => use the plain SDK enum anchor with no coordinate math. Offsets are in screen
        /// pixels: +X right, +Y down (matching MAX's top-left coordinate origin).
        /// </summary>
        public readonly Vector2? Offset;

        private readonly Func<AdConfig, bool?> _toggleSelector;

        public BannerPlacementInfo(BannerFormat format, BannerAnchor anchor, Func<AdConfig, bool?> toggleSelector,
            Vector2? offset = null)
        {
            Format = format;
            Anchor = anchor;
            Offset = offset;
            _toggleSelector = toggleSelector;
        }

        /// <summary>Whether this placement is enabled in config. Missing/null flag => false (OFF).</summary>
        public bool IsEnabled(AdConfig config)
        {
            if (config == null)
                return false;
            return _toggleSelector(config) ?? false;
        }
    }

    /// <summary>Single source of truth mapping each BannerPlacement to its format, anchor, and toggle.</summary>
    public static class BannerPlacementTable
    {
        private static readonly Dictionary<BannerPlacement, BannerPlacementInfo> Entries =
            new Dictionary<BannerPlacement, BannerPlacementInfo>
            {
                {
                    BannerPlacement.MainmenuBelowButtons,
                    new BannerPlacementInfo(BannerFormat.Adaptive, BannerAnchor.BottomCenter,
                        c => c.BannerGroupMainmenuBelowbuttons)
                },
                {
                    BannerPlacement.MapsBottom,
                    new BannerPlacementInfo(BannerFormat.Adaptive, BannerAnchor.BottomCenter,
                        c => c.BannerGroupMapsBottom)
                },
                {
                    BannerPlacement.OrganizeBottom,
                    new BannerPlacementInfo(BannerFormat.Adaptive, BannerAnchor.BottomCenter,
                        c => c.BannerGroupOrganizeBottom)
                },
                {
                    BannerPlacement.DriverlicenseBottom,
                    new BannerPlacementInfo(BannerFormat.Adaptive, BannerAnchor.BottomCenter,
                        c => c.BannerGroupDriverlicenseBottom)
                },
                {
                    BannerPlacement.PausemenuMiddle,
                    // Centered, nudged 100px left.
                    new BannerPlacementInfo(BannerFormat.Mrec, BannerAnchor.Center,
                        c => c.BannerGroupPausemenuMiddle, offset: new Vector2(-50f, 0f))
                },
                {
                    BannerPlacement.LoadingscreenRight,
                    // 200px in from the right edge (CenterRight base hugs the right edge; -X moves left).
                    new BannerPlacementInfo(BannerFormat.Mrec, BannerAnchor.CenterRight,
                        c => c.BannerGroupLoadingscreenRight, offset: new Vector2(-200f, 0f))
                },
            };

        /// <summary>Returns the placement info. Throws if the placement is not registered (programmer error).</summary>
        public static BannerPlacementInfo Get(BannerPlacement placement)
        {
            if (Entries.TryGetValue(placement, out var info))
                return info;
            throw new ArgumentOutOfRangeException(nameof(placement), placement, "Unregistered banner placement");
        }

        /// <summary>True if the placement is registered (defensive lookups).</summary>
        public static bool TryGet(BannerPlacement placement, out BannerPlacementInfo info)
        {
            return Entries.TryGetValue(placement, out info);
        }
    }
}