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
    }
}