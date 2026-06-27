using System;

namespace Vehicles
{
    [Flags]
    public enum VehicleObtainType
    {
        ByGold              = 1 << 0, // 0001
        ByWatchAds          = 1 << 1, // 0010
        DistanceMilestoneKm = 1 << 2, // 0100
        Free                = 1 << 3, // 1000
    }
}