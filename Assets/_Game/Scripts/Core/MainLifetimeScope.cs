using System;
using _Game.Scripts.Utils.VContainer;
using Ads;
using Analytics;
using Analytics.AppsFlyer;
using Clutch;
using Firebase.Analytics;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core
{
    public class MainLifetimeScope : LifetimeScopeWrapper<MainLifetimeScope>
    {
        // Auto initializing by VContainerSettings

        public static Type AdServiceTypeToUse { get; set; } = null;

        protected override void Awake()
        {
            base.Awake();

            ForceSetInstance(this);
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IMMPService, AppsFlyerMMPService>(Lifetime.Singleton).AsSelf();
            builder.Register<IAnalyticsService, FirebaseAnalyticsService>(Lifetime.Singleton).AsSelf();
            builder.Register<IClutchConfigService, ClutchConfigService>(Lifetime.Singleton).AsSelf();

            // Ads. Order of declaration does not matter for VContainer resolution, but the runtime
            // dependency chain is: AdConfigProvider -> (reads) IClutchConfigService;
            // AdGatingService -> IAdConfigProvider; MaxAdService -> IAdConfigProvider + IAdGatingService.
            builder.Register<IAdConfigProvider, AdConfigProvider>(Lifetime.Singleton).AsSelf();
            builder.Register<IAdGatingService, AdGatingService>(Lifetime.Singleton).AsSelf();
            builder.Register<IAdService, MaxAdService>(Lifetime.Singleton).AsSelf();
        }
    }
}