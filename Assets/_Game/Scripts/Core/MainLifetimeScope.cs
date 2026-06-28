using System;
using _Game.Scripts.Utils.VContainer;
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
            // builder.Register<IMMPService, AppsFlyerMMPService>(Lifetime.Singleton).AsSelf();
            // builder.RegisterInstance(new ClutchFeatureFlagService(FeatureKeys.AllKeys, FeatureKeys.PreAuthKeys))
            //     .As<IFeatureFlagService>()
            //     .AsSelf();
            // builder.Register<Coordinator.Runtime.FeatureFlagCache>(Lifetime.Singleton).AsSelf();
            // builder.Register<IInGameAdsService, GadsmeInGameAdsService>(Lifetime.Singleton).AsSelf();
            //
            // builder.Register<ClutchAnalyticsService>(Lifetime.Singleton);
            // builder.Register<MaxAdService>(Lifetime.Singleton);
            // builder.Register<AdmobAdService>(Lifetime.Singleton);
            //
            // // Register AnalyticEventService (provider will be initialized manually in GameStarter)
            // builder.Register<IAnalyticEventService, AnalyticEventService>(Lifetime.Singleton).AsSelf();
            //
            // builder.Register<IAdService>(container =>
            // {
            //     if (AdServiceTypeToUse == null)
            //     {
            //         Debug.LogError("AdServiceTypeToUse is null");
            //     }
            //     else if (AdServiceTypeToUse == typeof(MaxAdService))
            //     {
            //         return container.Resolve<MaxAdService>();
            //     }
            //     else if (AdServiceTypeToUse == typeof(AdmobAdService))
            //     {
            //         return container.Resolve<AdmobAdService>();
            //     }
            //     else
            //     {
            //         Debug.LogError("AdServiceTypeToUse is not supported: " + AdServiceTypeToUse.Name);
            //     }
            //
            //     return null;
            // }, Lifetime.Singleton);
            //
            // builder.Register<Scripts.Ads.AdGatingService>(Lifetime.Singleton)
            //     .As<Scripts.Ads.IAdGatingService>()
            //     .AsSelf();
        }
    }
}