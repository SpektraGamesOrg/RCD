using UnityEngine;

namespace Ads
{
    /// <summary>Which ad format produced this <see cref="AdInfo"/>.</summary>
    public enum AdType
    {
        Unknown = 0,
        Interstitial,
        Rewarded,
    }

    public class AdInfo
    {
        // TODO: Common properties of MaxSdk.AdInfo will be added here
        public object AdDataObject { get; }
        public string Placement { get; set; }
        public string AdUnitIdentifier { get; set; }

        /// <summary>The ad format this info describes. Set by the handler that raised it.</summary>
        public AdType AdType { get; set; }

        public AdInfo(object adData)
        {
            AdDataObject = adData;
        }

        /// <summary>
        /// Retrieves the ad data in the desired format.
        /// </summary>
        public T GetAdData<T>() where T : class
        {
            var data = AdDataObject as T; // TODO: Possible garbage if used occasionally
            if (data == null)
            {
                Debug.LogError($"Failed to cast AdData to {typeof(T)}");
            }

            return data;
        }
    }
}