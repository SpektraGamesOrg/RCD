namespace Ads
{
    /// <summary>
    /// Banner/MREC ad surfaces. Each maps to an AdConfig toggle in <see cref="BannerPlacementTable"/>.
    /// The original six (Mainmenu/Maps/Pausemenu/Loadingscreen/Organize/Driverlicense) map to the legacy
    /// BannerGroup* toggles and are UNCHANGED. The RCD "Ad Placements" surfaces below map to the new
    /// ad_*_enabled schema keys via the typed AdConfig accessors.
    /// </summary>
    public enum BannerPlacement
    {
        MainmenuBelowButtons,
        MapsBottom,
        PausemenuMiddle,
        LoadingscreenRight,
        OrganizeBottom,
        DriverlicenseBottom,

        // --- RCD "Ad Placements" surfaces (new schema keys) ---
        GameplayBottom,   // persistent in-game banner  -> ad_banner_enabled
        LoadingMrec,      // loading-screen MREC         -> ad_loading_mrec_enabled
        PauseMrec,        // pause-menu MREC             -> ad_pause_mrec_enabled
        PauseBanner,      // pause-menu banner           -> ad_pause_mrec_enabled
        CityStartMrec,    // city-start overlay MREC     -> ad_city_start_overlay
        CityStartBanner,  // city-start overlay banner   -> ad_city_start_overlay
        TutorialMrec,     // first-session tutorial MREC -> tutorial_enabled
    }

    /// <summary>Banner ad format.</summary>
    public enum BannerFormat
    {
        Adaptive,
        Mrec,
        Standard,
    }

    /// <summary>SDK-neutral banner anchor. Each controller maps it to its SDK's position type.</summary>
    public enum BannerAnchor
    {
        BottomCenter,
        TopCenter,
        Center,
        CenterRight,
    }
}