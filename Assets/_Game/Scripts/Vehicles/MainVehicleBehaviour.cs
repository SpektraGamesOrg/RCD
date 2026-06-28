using System;
using SpektraGames.RuntimeUI.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
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

        [SerializeField]
        private VehicleKinematicMoverBehaviour vehicleKinematicMoverBehaviour = null;
        public VehicleKinematicMoverBehaviour VehicleKinematicMoverBehaviour => vehicleKinematicMoverBehaviour;

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

            EnsureBehavioursDeferred();

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

        // AddComponent must not run during OnValidate (Unity forbids SendMessage there)
        private void EnsureBehavioursDeferred()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (!this || Application.isPlaying)
                    return;

                bool anyChange = false;

                var kmTrackerLocal = gameObject.GetOrAddComponent<VehicleKmTracker>();
                kmTrackerLocal.EditorAutoWire();
                if (kmTracker != kmTrackerLocal)
                {
                    kmTracker = kmTrackerLocal;
                    anyChange = true;
                }

                var vehicleKinematicMoverBehaviourLocal = gameObject.GetOrAddComponent<VehicleKinematicMoverBehaviour>();
                //vehicleKinematicMoverBehaviourLocal.EditorAutoWire();
                if (vehicleKinematicMoverBehaviour != vehicleKinematicMoverBehaviourLocal)
                {
                    vehicleKinematicMoverBehaviour = vehicleKinematicMoverBehaviourLocal;
                    anyChange = true;
                }

                if (anyChange)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                    UnityEditor.EditorUtility.SetDirty(gameObject);
                }
            };
        }
#endif
    }
}