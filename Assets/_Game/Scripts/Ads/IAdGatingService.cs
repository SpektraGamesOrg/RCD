using _Game.Scripts.Utils.VContainer;

namespace Ads
{
    /// <summary>
    /// The single owner of fullscreen ad policy (App Open + Interstitial), per the RCD "Ad Placements" doc.
    /// Rewarded, MREC, and Banner formats are EXEMPT from this gate and never call through it.
    ///
    /// Two fullscreen formats share ONE cooldown window (<c>ad_fullscreen_cooldown_sec</c>): showing either
    /// arms it, and neither may show again until it elapses. Interstitials additionally consume a per-session
    /// cap (<c>ad_max_inters_per_session</c>) and a per-day cap (<c>ad_daily_inter_cap</c>); App Open does NOT
    /// consume those interstitial caps — hence the split between <see cref="ArmSharedCooldown"/> (App Open)
    /// and <see cref="NoteInterstitialShown"/> (interstitial).
    ///
    /// Registered as a singleton in MainLifetimeScope; resolve via ServiceLocator.
    /// </summary>
    public interface IAdGatingService : IService
    {
        /// <summary>
        /// True if an interstitial may show right now: the shared cooldown has elapsed AND the session cap
        /// and daily cap both have headroom. Callers should still handle a false SDK show (no-fill) without
        /// consuming budget — see <see cref="NoteInterstitialShown"/>.
        /// </summary>
        bool CanShowInterstitial();

        /// <summary>
        /// True if an App Open may show right now. App Open shares only the fullscreen cooldown (it is not
        /// subject to the interstitial session/daily caps).
        /// </summary>
        bool CanShowAppOpen();

        /// <summary>
        /// Arms the shared fullscreen cooldown WITHOUT touching the interstitial caps. Called after an App
        /// Open is shown so a following interstitial still respects the cooldown but no interstitial budget
        /// is burned.
        /// </summary>
        void ArmSharedCooldown();

        /// <summary>
        /// Records that an interstitial was actually shown: arms the shared cooldown AND increments the
        /// session and daily counts. Call this only on a confirmed show, never on a no-fill/failed show.
        /// </summary>
        void NoteInterstitialShown();

        /// <summary>Master toggle for the timed Commercial Break interstitials (<c>ad_commercial_break_enabled</c>).</summary>
        bool CommercialBreakEnabled { get; }

        /// <summary>Active-gameplay seconds between Commercial Breaks (<c>ad_commercial_break_interval_sec</c>).</summary>
        int CommercialBreakIntervalSec { get; }

        /// <summary>Whether Close on a milestone popup may fire an interstitial (<c>ad_milestone_close_inter</c>).</summary>
        bool MilestoneCloseInterEnabled { get; }

        /// <summary>
        /// True if a screen-transition button with this id may fire an interstitial NOW: the button is
        /// whitelisted (<c>ad_button_inter_whitelist</c> + master <c>ad_button_inter_enabled</c>) AND
        /// <see cref="CanShowInterstitial"/> currently allows it. Functional game buttons
        /// (gas/brake/arrows/nitro/drift) are never whitelisted.
        /// </summary>
        bool IsButtonInterAllowed(string buttonId);

        /// <summary>Remaining interstitials allowed this session (max cap minus shown). Never negative.</summary>
        int RemainingSessionInterstitials { get; }
    }
}
