using _Game.Scripts.Utils.VContainer;

namespace Clutch
{
    /// <summary>
    /// One-line typed Clutch config access for callers that are NOT dependency-injected and may run before
    /// the DI service exists - static boot paths (<see cref="Save.SaveManager"/>,
    /// <see cref="Milestones.DistanceMilestoneManager"/>) and plain MonoBehaviours (Gold pickups).
    ///
    /// Resolution:
    ///   1) the live <see cref="IClutchConfigService"/> when registered - so the value is remote-aware
    ///      (prefs cache -> SO fallback) and memoized; the service also re-parses after each init, so a
    ///      value read from the SO fallback at boot is automatically upgraded to the resolved Clutch value.
    ///   2) the <see cref="ClutchConfig"/> SO fallback parsed directly, for the brief window before the
    ///      service is registered (mirrors how SaveManager reads ClutchConfig.Instance for the boot
    ///      starter-vehicle grant).
    ///   3) a default-constructed <typeparamref name="T"/> as a last resort, so callers never get null.
    ///
    /// The <see cref="ServiceLocator"/> lookup is a validated dictionary hit (self-healing across scope
    /// rebuilds) and <see cref="IClutchConfigService.GetConfig{T}"/> is memoized, so this is cheap enough to
    /// call every frame.
    /// </summary>
    public static class ClutchConfigResolver
    {
        public static T Get<T>(string flagKey) where T : class, new()
        {
            if (ServiceLocator.TryGetService(out IClutchConfigService service))
                return service.GetConfig<T>(flagKey);

            if (ClutchConfig.Instance && ClutchConfig.Instance.TryGetConfig(flagKey, out T fallback))
                return fallback;

            return new T();
        }
    }
}
