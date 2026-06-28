using UnityEngine;
using Vehicles;

namespace Utils
{
    public static class GameSpecificExtensions
    {
        /// <summary>
        /// Drives the vehicle to the given speed (km/h) along its forward axis and snaps the RCC
        /// gearbox to the matching gear. Used when handing control back from the kinematic mover so
        /// the car keeps rolling at the speed it had while being moved manually.
        /// RCC source is untouched; only its public fields are written.
        /// </summary>
        public static void SetSpeed(this MainVehicleBehaviour vehicle, int speedKmh)
        {
            if (!vehicle)
                return;

            RCC_CarControllerV4 rcc = vehicle.VehicleController;
            if (!rcc)
                return;

            Rigidbody rigid = vehicle.Rigidbody ? vehicle.Rigidbody : rcc.Rigid;
            if (!rigid)
                return;

            // RCC reads its displayed speed from Rigid.linearVelocity.magnitude * 3.6f, so setting the
            // rigidbody velocity is enough to drive both the physics and the HUD readout (km/h -> m/s).
            rigid.linearVelocity = vehicle.transform.forward * (speedKmh / 3.6f);

            // Snap the gearbox to the gear whose speed band contains this speed, and face the matching
            // direction. RCC's automatic gearbox keeps adjusting from here based on the live speed.
            rcc.NGear = false;
            rcc.currentGear = rcc.FindGearForSpeed(Mathf.Abs(speedKmh));
            rcc.direction = speedKmh < 0 ? -1 : 1;
        }

        /// <summary>
        /// Returns the forward gear index whose speed band contains <paramref name="speedKmh"/>, using
        /// RCC's own per-gear <c>maxSpeed</c> thresholds (each gear covers up to maxspeed/totalGears * (i+1)).
        /// Clamps to the top gear when the speed exceeds every band.
        /// </summary>
        public static int FindGearForSpeed(this RCC_CarControllerV4 rcc, int speedKmh)
        {
            if (!rcc || rcc.gears == null || rcc.gears.Length == 0)
                return 0;

            for (int i = 0; i < rcc.gears.Length; i++)
            {
                if (speedKmh <= rcc.gears[i].maxSpeed)
                    return i;
            }

            return rcc.gears.Length - 1;
        }
    }
}
