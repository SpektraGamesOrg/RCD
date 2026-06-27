using System;
using SpektraGames.RuntimeUI.Runtime;
using UnityEngine;

namespace Vehicles
{
    public class MainVehicleBehaviour : MonoBehaviour
    {
        [SerializeField]
        private VehicleID vehicleID = VehicleID.None;
        public VehicleID VehicleID
        {
            get => vehicleID;
            set => vehicleID = value;
        }

        [SerializeField]
        private RCC_CarControllerV4 vehicleController = null;
        public RCC_CarControllerV4 VehicleController => vehicleController;

        [SerializeField]
        private new Rigidbody rigidbody = null;
        public Rigidbody Rigidbody => rigidbody;

        [SerializeField]
        private VehicleKmTracker kmTracker = null;
        public VehicleKmTracker KmTracker => kmTracker;

#if UNITY_EDITOR
        // Guards against stacking duplicate delayCall callbacks while the tracker add is pending.
        [NonSerialized] private bool _kmTrackerEnsureQueued;
#endif

        private void OnValidate()
        {
            Validate();
        }

        public void Validate()
        {
#if UNITY_EDITOR

            bool anyChange = false;

            if (Application.isPlaying)
                return;

            if (vehicleController != GetComponent<RCC_CarControllerV4>())
            {
                vehicleController = GetComponent<RCC_CarControllerV4>();
                anyChange = true;
            }

            if (rigidbody != GetComponent<Rigidbody>())
            {
                rigidbody = GetComponent<Rigidbody>();
                anyChange = true;
            }

            // Only the physics root (the GameObject that actually has a Rigidbody) gets an odometer.
            // A MainVehicleBehaviour without a body (e.g. a nested visual-only copy) is skipped, so it
            // never receives a tracker that would have no Rigidbody to read and fail at runtime.
            if (rigidbody)
            {
                if (!kmTracker)
                    kmTracker = GetComponent<VehicleKmTracker>();

                if (!kmTracker)
                    EnsureKmTrackerDeferred();
                else if (kmTracker.EditorAutoWire())
                    anyChange = true;
            }

            Component[] components = GetComponents<Component>();
            // Index 0 is always the Transform, so this component should sit at index 1.
            int currentIndex = Array.IndexOf(components, this);
            while (currentIndex > 1)
            {
                if (!UnityEditorInternal.ComponentUtility.MoveComponentUp(this))
                    break;

                currentIndex--;
                anyChange = true;
            }

            if (anyChange)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: sets the back-reference to the odometer component. Used by the repair tool when
        /// it rebuilds trackers; pass null to clear it on body-less copies.
        /// </summary>
        public void EditorSetKmTracker(VehicleKmTracker tracker)
        {
            if (kmTracker == tracker)
                return;

            kmTracker = tracker;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // AddComponent must not run during OnValidate (Unity forbids SendMessage there), so the missing
        // tracker is added on the next editor tick instead, then bound and marked dirty so it persists.
        private void EnsureKmTrackerDeferred()
        {
            if (_kmTrackerEnsureQueued)
                return;

            _kmTrackerEnsureQueued = true;
            MainVehicleBehaviour self = this;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (self)
                    self._kmTrackerEnsureQueued = false;

                if (!self || Application.isPlaying)
                    return;

                VehicleKmTracker tracker = self.GetComponent<VehicleKmTracker>();
                if (!tracker)
                    tracker = self.gameObject.AddComponent<VehicleKmTracker>();

                self.kmTracker = tracker;
                tracker.EditorAutoWire();

                UnityEditor.EditorUtility.SetDirty(tracker);
                UnityEditor.EditorUtility.SetDirty(self);
                UnityEditor.EditorUtility.SetDirty(self.gameObject);
            };
        }
#endif
    }
}