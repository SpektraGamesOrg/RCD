using System;
using System.Text;
using Sirenix.OdinInspector;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;
using TMPro;

namespace UISystem.Runtime.Scripts.Mono
{
    public enum TimerFormat
    {
        Auto, // HH:MM or MM:SS or DD:HH
        Clock, // HH:MM:SS
        Short, // 2h 15m
        Verbose, // 2d 4h 15m 12s
        FullText // 2 days 4 hours 15 minutes 12 seconds ✅
    }

    /// <summary>
    /// A widget for displaying and managing a timer with customizable formats and prefix/suffix support.
    /// </summary>
    public class TimerTextWidget : TextWidget
    {
        public Action OnTimerCompleted;

        [SerializeField] private TimerFormat timerFormat = TimerFormat.Auto;
        [ShowInInspector, ReadOnly] private SpektraHelpers.Timer _timer;
        private long _startTimestamp;
        private long _endTimestamp;
        private char[] _timeBuffer = new char[15];

        public void SetTimeText(double totalSeconds)
        {
            int days = (int)(totalSeconds / 86400);
            int hours = (int)((totalSeconds % 86400) / 3600);
            int minutes = (int)((totalSeconds % 3600) / 60);
            int seconds = (int)(totalSeconds % 60);

            string formattedTime;

            switch (timerFormat)
            {
                case TimerFormat.Clock:
                    formattedTime = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
                    break;

                case TimerFormat.Short:
                    if (days > 0)
                        formattedTime = $"{days}d {hours}h";
                    else if (hours > 0)
                        formattedTime = $"{hours}h {minutes}m";
                    else
                        formattedTime = $"{minutes}m {seconds}s";
                    break;

                case TimerFormat.Verbose:
                    formattedTime = BuildVerbose(days, hours, minutes, seconds);
                    break;

                case TimerFormat.FullText:
                    formattedTime = BuildFullText(days, hours, minutes, seconds);
                    break;

                case TimerFormat.Auto:
                default:
                    if (days > 0)
                        formattedTime = $"{days:D2}:{hours:D2}";
                    else if (hours > 0)
                        formattedTime = $"{hours:D2}:{minutes:D2}";
                    else
                        formattedTime = $"{minutes:D2}:{seconds:D2}";
                    break;
            }

            Sb.Clear();

            // Prefix (localized or static)
            if (!string.IsNullOrEmpty(prefix))
            {
                Sb.Append(prefix);
            }

            Sb.Append(formattedTime);

            // Suffix (localized or static)
            if (!string.IsNullOrEmpty(suffix))
            {
                Sb.Append(suffix);
            }

            textMeshProUGUI.SetText(Sb.ToString());
        }

        private static string BuildVerbose(int d, int h, int m, int s)
        {
            var sb = new StringBuilder();
            if (d > 0) sb.Append(d).Append("d ");
            if (h > 0 || d > 0) sb.Append(h).Append("h ");
            if (m > 0 || h > 0 || d > 0) sb.Append(m).Append("m ");
            sb.Append(s).Append("s");
            return sb.ToString().TrimEnd();
        }

        private static string BuildFullText(int d, int h, int m, int s)
        {
            var sb = new StringBuilder();

            if (d > 0)
                sb.Append(d).Append(d == 1 ? " day " : " days ");
            if (h > 0)
                sb.Append(h).Append(h == 1 ? " hour " : " hours ");
            if (m > 0)
                sb.Append(m).Append(m == 1 ? " minute " : " minutes ");
            if (s > 0 || (d == 0 && h == 0 && m == 0))
                sb.Append(s).Append(s == 1 ? " second" : " seconds");

            return sb.ToString().TrimEnd();
        }

        public void StartTimer(long startTimestamp, long endTimestamp)
        {
            _startTimestamp = startTimestamp;
            _endTimestamp = endTimestamp;
            SetTimeText(endTimestamp - startTimestamp);

            StopTimer();
            _timer = new SpektraHelpers.Timer(_endTimestamp - _startTimestamp);
            _timer.OnStarted += OnStarted;
            _timer.OnUpdated += OnUpdated;
            _timer.OnCompleted += OnCompleted;
            SpektraHelpers.Time.RegisterTimer(_timer);
        }

        [Button]
        public void StartTimer(long timeInSeconds)
        {
            SetTimeText(timeInSeconds);

            StopTimer();
            _timer = new SpektraHelpers.Timer(timeInSeconds);
            _timer.OnStarted += OnStarted;
            _timer.OnUpdated += OnUpdated;
            _timer.OnCompleted += OnCompleted;
            SpektraHelpers.Time.RegisterTimer(_timer);
        }

        /// <summary>
        /// Stops and disposes the running timer (unregisters it from the global ticker and clears its
        /// callbacks). Safe to call when no timer is running. Public so pooled/reused consumers can halt
        /// the timer on return instead of relying on OnDestroy (which never fires for pooled objects).
        /// </summary>
        public void StopTimer()
        {
            if (_timer == null)
                return;

            SpektraHelpers.Time.UnregisterTimer(_timer);
            _timer.OnStarted -= OnStarted;
            _timer.OnUpdated -= OnUpdated;
            _timer.OnCompleted -= OnCompleted;
            _timer = null;
        }

        private void OnUpdated(double remainingTime) => SetTime(remainingTime);
        private void OnStarted() => SetTime(_timer.GetRemainingTime());
        private void OnCompleted() { SetTime(0); OnTimerCompleted?.Invoke(); }
        private void SetTime(double time) => SetTimeText(time);
        public SpektraHelpers.Timer GetTimer() => _timer;
        public int GetRemainingTime() => (int)_timer.GetRemainingTime();

        // Stop the timer whenever the widget is deactivated (e.g. returned to an object pool or its
        // screen hidden) so it stops ticking and unregisters from the global ticker. A subsequent
        // StartTimer re-registers it, so reactivation works as expected.
        private void OnDisable()
        {
            StopTimer();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            StopTimer();
        }
    }
}