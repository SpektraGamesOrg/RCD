using System;
using NWH.VehiclePhysics2;
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
        private VehicleController vehicleController = null;
        public VehicleController VehicleController => vehicleController;

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

            if (vehicleController != GetComponent<VehicleController>())
            {
                vehicleController = GetComponent<VehicleController>();
                anyChange = true;
            }

            if (anyChange)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }
    }
}