using System;
using _Game.Scripts.Utils.VContainer;

namespace Ads
{
    /// <summary>
    /// The resolved, typed <see cref="AdConfig"/> view of the Clutch "AdConfig" flag. This is the single
    /// seam every ad system reads its tuning through (gating, banners/MRECs, rewarded multipliers), so no
    /// consumer fabricates a throwaway <c>new AdConfig()</c> or reparses the flag JSON itself.
    ///
    /// The value is parsed once at boot (after <see cref="Clutch.IClutchConfigService"/> resolves) and does
    /// not change mid-session — Clutch's <c>OnConfigUpdated</c> fires exactly once per launch. Registered as
    /// a singleton in MainLifetimeScope; resolve via ServiceLocator.
    /// </summary>
    public interface IAdConfigProvider : IService
    {
        /// <summary>
        /// The resolved config. NEVER null: before parse (or on a parse failure) this is an empty
        /// <see cref="AdConfig"/> whose typed accessors return the RCD doc defaults, so callers can read it
        /// unconditionally without a null check.
        /// </summary>
        AdConfig Current { get; }

        /// <summary>
        /// Raised when <see cref="Current"/> is (re)parsed. In this codebase that is once per launch, when
        /// Clutch config resolves. Systems that cache derived state from the config (e.g. a rebuilt
        /// whitelist set) may rebuild on this event; it will not fire again during the session.
        /// </summary>
        event Action OnAdConfigChanged;

        /// <summary>
        /// Subscribes to Clutch config resolution and parses <see cref="Current"/> from the resolved flag.
        /// Idempotent and safe to call once at startup after the Clutch service is registered. If Clutch has
        /// already resolved, parses immediately.
        /// </summary>
        void Initialize();
    }
}
