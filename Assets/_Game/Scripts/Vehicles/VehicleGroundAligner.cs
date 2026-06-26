using UnityEngine;

namespace Vehicles
{
    /// <summary>
    /// Tunables for <see cref="VehicleGroundAligner"/>. Lives as a serialized block on whoever spawns
    /// a static vehicle (e.g. <see cref="GarageManager"/>) so the grounding behaviour can be framed in
    /// the Inspector without touching code.
    /// </summary>
    [System.Serializable]
    public class VehicleGroundingSettings
    {
        [Tooltip("Layers the downward wheel rays may hit. The vehicle's OWN colliders are always ignored " +
                 "automatically, so leaving this as Everything is fine; narrow it only if other props share the floor area.")]
        public LayerMask groundMask = ~0;

        [Tooltip("How high above each wheel centre the ray starts. Must clear the floor so the ray cannot start " +
                 "already below the floor when the car spawns clipped into it.")]
        [Min(0f)] public float rayStartHeight = 5f;

        [Tooltip("How far below the ray start it searches for ground. Big enough to cover a car that spawned " +
                 "floating well above the floor.")]
        [Min(0.1f)] public float rayMaxDistance = 30f;

        [Tooltip("Fallback tyre radius used only if a wheel's WheelCollider radius cannot be read (≈ tyre size).")]
        [Min(0.01f)] public float fallbackWheelRadius = 0.35f;

        [Tooltip("Extra clearance kept between the tyres and the floor. 0 = tyres touch the ground exactly. " +
                 "A tiny positive value avoids z-fighting / intersection on a perfectly flat floor.")]
        public float groundClearance = 0f;

        [Tooltip("When on, the car also tilts to match the slope under its 4 wheels (uneven ground). " +
                 "Leave OFF for a flat showroom floor — vertical grounding alone is what you want there.")]
        public bool alignToSlope = false;
    }

    /// <summary>
    /// Drops (or lifts) a freshly-spawned vehicle so all four tyres rest on the ground, using a raycast
    /// from each wheel. This removes the "spawned inside the floor / spawned floating" problem that comes
    /// from every prefab having a different pivot-to-wheel offset.
    ///
    /// The rays always skip the vehicle's own colliders, so the car can never ground itself on its own body
    /// or wheels (which would otherwise launch it skyward).
    ///
    /// Pure utility: no per-frame work, no component to attach. Call <see cref="Align"/> once right after
    /// the car is instantiated and posed at the spawn point.
    /// </summary>
    public static class VehicleGroundAligner
    {
        // Shared scratch buffer for self-filtering raycasts (one-shot at spawn, so a small fixed size is plenty).
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        /// <summary>
        /// Aligns <paramref name="root"/> so its four wheels rest on the ground beneath them.
        /// Returns true if at least one wheel found ground and the car was placed; false if nothing was hit
        /// (in which case the car is left exactly where it was, so a bad raycast never teleports it away).
        /// </summary>
        public static bool Align(Transform root, RCC_CarControllerV4 controller, VehicleGroundingSettings settings)
        {
            if (root == null || controller == null || settings == null)
            {
                Debug.LogError("[VehicleGroundAligner] Missing root, controller, or settings; cannot ground vehicle.");
                return false;
            }

            RCC_WheelCollider fl = controller.FrontLeftWheelCollider;
            RCC_WheelCollider fr = controller.FrontRightWheelCollider;
            RCC_WheelCollider rl = controller.RearLeftWheelCollider;
            RCC_WheelCollider rr = controller.RearRightWheelCollider;

            if (fl == null || fr == null || rl == null || rr == null)
            {
                Debug.LogError($"[VehicleGroundAligner] {root.name} is missing one or more wheel colliders; cannot ground vehicle.");
                return false;
            }

            // Optional tilt pass first: reorient to the slope, THEN settle vertically against the new pose.
            if (settings.alignToSlope)
                AlignToSlope(root, fl, fr, rl, rr, settings);

            // Gather a ground contact under each wheel. The target is the floor point lifted by the tyre
            // radius (+ clearance), i.e. where the wheel CENTRE must end up for the tyre to kiss the floor.
            int hits = 0;
            float maxLift = float.NegativeInfinity;

            hits += SampleWheel(root, fl, settings, ref maxLift);
            hits += SampleWheel(root, fr, settings, ref maxLift);
            hits += SampleWheel(root, rl, settings, ref maxLift);
            hits += SampleWheel(root, rr, settings, ref maxLift);

            if (hits == 0)
            {
                Debug.LogError($"[VehicleGroundAligner] No ground found under any wheel of {root.name}. " +
                               $"Check the groundMask / ray distance. Vehicle left at spawn pose.");
                return false;
            }

            // Lift by the largest single-wheel requirement so the lowest wheel just touches and NO wheel is
            // left buried in the floor. On flat ground all four requirements are equal, so this is exact.
            root.position += Vector3.up * maxLift;
            return true;
        }

        // Casts straight down from one wheel and, on a hit, records how far the car must rise (can be
        // negative = drop) for that tyre to rest on the floor. Returns 1 on hit, 0 on miss.
        private static int SampleWheel(Transform root, RCC_WheelCollider wheel, VehicleGroundingSettings settings, ref float maxLift)
        {
            Vector3 centre = wheel.transform.position;

            if (!CastGround(root, centre, settings, out RaycastHit hit))
                return 0;

            float radius = wheel.WheelCollider != null && wheel.WheelCollider.radius > 0f
                ? wheel.WheelCollider.radius
                : settings.fallbackWheelRadius;

            // Where the wheel centre needs to sit for the tyre to rest on this point.
            float targetCentreY = hit.point.y + radius + settings.groundClearance;
            float lift = targetCentreY - centre.y;

            if (lift > maxLift)
                maxLift = lift;

            return 1;
        }

        // Downward raycast from above a wheel that ignores the vehicle's own colliders, returning the nearest
        // hit that actually belongs to the world (the floor). This is what stops the car grounding on its own
        // roof/body and rocketing upward.
        private static bool CastGround(Transform root, Vector3 wheelCentre, VehicleGroundingSettings settings, out RaycastHit best)
        {
            Vector3 origin = wheelCentre + Vector3.up * settings.rayStartHeight;
            float distance = settings.rayStartHeight + settings.rayMaxDistance;

            int count = Physics.RaycastNonAlloc(origin, Vector3.down, HitBuffer, distance, settings.groundMask, QueryTriggerInteraction.Ignore);

            best = default;
            float nearest = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit h = HitBuffer[i];

                // Skip any collider that is part of this vehicle (body, wheels, anything under the root).
                if (h.collider == null || h.collider.transform.IsChildOf(root))
                    continue;

                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    best = h;
                    found = true;
                }
            }

            return found;
        }

        // Tilts the car so its up-axis matches the plane under the four wheels, preserving heading (yaw).
        // Only meaningful on uneven ground; a no-op-ish nudge on a flat floor.
        private static void AlignToSlope(Transform root, RCC_WheelCollider fl, RCC_WheelCollider fr,
            RCC_WheelCollider rl, RCC_WheelCollider rr, VehicleGroundingSettings settings)
        {
            if (!CastGround(root, fl.transform.position, settings, out RaycastHit hfl) ||
                !CastGround(root, fr.transform.position, settings, out RaycastHit hfr) ||
                !CastGround(root, rl.transform.position, settings, out RaycastHit hrl) ||
                !CastGround(root, rr.transform.position, settings, out RaycastHit hrr))
            {
                return; // Need all four to define a reliable plane; otherwise just do vertical grounding.
            }

            // Newell's method over the wheel quad (FL → FR → RR → RL) for a stable averaged plane normal.
            Vector3 normal = NewellNormal(hfl.point, hfr.point, hrr.point, hrl.point);
            if (normal.sqrMagnitude < 1e-6f)
                return;

            normal.Normalize();
            if (Vector3.Dot(normal, Vector3.up) < 0f)
                normal = -normal;

            Quaternion tilt = Quaternion.FromToRotation(root.up, normal);
            root.rotation = tilt * root.rotation;
        }

        private static Vector3 NewellNormal(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.zero;
            n += NewellEdge(a, b);
            n += NewellEdge(b, c);
            n += NewellEdge(c, d);
            n += NewellEdge(d, a);
            return n;
        }

        private static Vector3 NewellEdge(Vector3 cur, Vector3 next) => new Vector3(
            (cur.y - next.y) * (cur.z + next.z),
            (cur.z - next.z) * (cur.x + next.x),
            (cur.x - next.x) * (cur.y + next.y));
    }
}
