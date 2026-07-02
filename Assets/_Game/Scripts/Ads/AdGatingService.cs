using System;
using System.Globalization;
using Save;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    /// <summary>
    /// Owns fullscreen ad policy for App Open + Interstitial (see <see cref="IAdGatingService"/>). Reads all
    /// tuning through <see cref="IAdConfigProvider"/> so the cooldown/caps track the resolved Clutch AdConfig.
    ///
    /// Time model:
    ///   * The shared cooldown uses <see cref="Time.realtimeSinceStartup"/> — a within-session, timescale- and
    ///     pause-independent clock. The cooldown is a session-local gate; it intentionally does not persist
    ///     across launches.
    ///   * The daily cap uses a persisted UTC calendar day (yyyy-MM-dd) + count in PlayerPrefs, so it survives
    ///     app restarts and resets when the day rolls over. The "today" string is memoized and only recomputed
    ///     when the UTC date changes, so the check does not allocate a new string on every call.
    /// </summary>
    public sealed class AdGatingService : IAdGatingService
    {
        private static readonly InfoLogger Logger = new InfoLogger("AdGatingService", "yellow");

        private readonly IAdConfigProvider _configProvider;

        // Session-local interstitial count and the last time either fullscreen format armed the cooldown.
        private int _sessionInterstitials;
        private float _lastFullscreenRealtime = float.NegativeInfinity; // no cooldown pending at boot

        // Memoized UTC "today" (yyyy-MM-dd) and its cached day-number so we only re-stringify on date change.
        private string _todayCached;
        private int _todayDayNumberCached = int.MinValue;

        private AdConfig Config => _configProvider.Current;

        public AdGatingService(IAdConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        // ------------------------------------------------------------------
        // Gate checks
        // ------------------------------------------------------------------

        public bool CanShowInterstitial()
        {
            if (!CooldownElapsed())
                return false;

            if (_sessionInterstitials >= Config.MaxIntersPerSession)
                return false;

            if (DailyInterstitialCount() >= Config.DailyInterCap)
                return false;

            return true;
        }

        public bool CanShowAppOpen()
        {
            // App Open shares only the cooldown; it is not bound by the interstitial caps.
            return CooldownElapsed();
        }

        // ------------------------------------------------------------------
        // Show accounting
        // ------------------------------------------------------------------

        public void ArmSharedCooldown()
        {
            _lastFullscreenRealtime = Time.realtimeSinceStartup;
        }

        public void NoteInterstitialShown()
        {
            ArmSharedCooldown();
            _sessionInterstitials++;
            IncrementDailyInterstitialCount();
            Logger.Log(
                $"Interstitial shown. Session {_sessionInterstitials}/{Config.MaxIntersPerSession}, " +
                $"day {DailyInterstitialCount()}/{Config.DailyInterCap}.");
        }

        // ------------------------------------------------------------------
        // Config passthroughs
        // ------------------------------------------------------------------

        public bool CommercialBreakEnabled => Config.CommercialBreakEnabled;
        public int CommercialBreakIntervalSec => Config.CommercialBreakIntervalSec;
        public bool MilestoneCloseInterEnabled => Config.MilestoneCloseInter;

        public bool IsButtonInterAllowed(string buttonId)
        {
            return Config.IsButtonInterWhitelisted(buttonId) && CanShowInterstitial();
        }

        public int RemainingSessionInterstitials
        {
            get
            {
                int remaining = Config.MaxIntersPerSession - _sessionInterstitials;
                return remaining < 0 ? 0 : remaining;
            }
        }

        // ------------------------------------------------------------------
        // Cooldown
        // ------------------------------------------------------------------

        private bool CooldownElapsed()
        {
            float elapsed = Time.realtimeSinceStartup - _lastFullscreenRealtime;
            return elapsed >= Config.FullscreenCooldownSec;
        }

        // ------------------------------------------------------------------
        // Daily cap (persisted, per-UTC-calendar-day, memoized today string)
        // ------------------------------------------------------------------

        // Returns today's UTC yyyy-MM-dd, re-stringifying only when the calendar day actually changes.
        private string Today()
        {
            // DayNumber is a cheap int comparison; only format the string when the day rolls over.
            int dayNumber = DateTime.UtcNow.DayOfYear + DateTime.UtcNow.Year * 366;
            if (dayNumber != _todayDayNumberCached || _todayCached == null)
            {
                _todayDayNumberCached = dayNumber;
                _todayCached = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return _todayCached;
        }

        // The interstitial count for today. Returns 0 (and does not mutate storage) when the stored day is
        // stale — the count is treated as reset until the next increment writes today's date.
        private int DailyInterstitialCount()
        {
            string storedDay = PlayerPrefs.GetString(SaveKeys.AdDailyInterDay, string.Empty);
            if (storedDay != Today())
                return 0;

            return PlayerPrefs.GetInt(SaveKeys.AdDailyInterCount, 0);
        }

        private void IncrementDailyInterstitialCount()
        {
            string today = Today();
            string storedDay = PlayerPrefs.GetString(SaveKeys.AdDailyInterDay, string.Empty);

            int count;
            if (storedDay != today)
            {
                // New day: reset the window to today.
                count = 1;
                PlayerPrefs.SetString(SaveKeys.AdDailyInterDay, today);
            }
            else
            {
                count = PlayerPrefs.GetInt(SaveKeys.AdDailyInterCount, 0) + 1;
            }

            PlayerPrefs.SetInt(SaveKeys.AdDailyInterCount, count);
            PlayerPrefs.Save();
        }
    }
}
