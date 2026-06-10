namespace Vehicles
{
    /// <summary>
    /// Identifies a vehicle. Used instead of a raw string id everywhere in the game.
    ///
    /// IMPORTANT: values are persisted as ints, so always assign explicit numbers and only
    /// APPEND new vehicles. Never reorder or reuse a number, or existing saves will point at
    /// the wrong vehicle.
    /// </summary>
    public enum VehicleNameType
    {
        None    = 0,
        GTR_R35 = 1,
        // Add more vehicles here, appending new explicit values.
    }
}
