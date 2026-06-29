namespace Ads
{
    /// <summary>Banner ad groups. Each maps to an AdConfig BannerGroup* toggle (see BannerPlacementTable).</summary>
    public enum BannerPlacement
    {
        MainmenuBelowButtons,
        MapsBottom,
        PausemenuMiddle,
        LoadingscreenRight,
        OrganizeBottom,
        DriverlicenseBottom,
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