using Sirenix.OdinInspector;
using UnityEngine;

namespace Vehicles
{
    public abstract class VehicleBehaviourBase : MonoBehaviour
    {
        [BoxGroup("Vehicle Base")]
        [SerializeField, ReadOnly] private MainVehicleBehaviour mainVehicleBehaviour = null;
        public MainVehicleBehaviour MainVehicleBehaviour => mainVehicleBehaviour;

        protected virtual void Reset()
        {
            mainVehicleBehaviour = GetComponent<MainVehicleBehaviour>();
        }

        protected virtual void OnValidate()
        {
            if (!mainVehicleBehaviour)
                mainVehicleBehaviour = GetComponent<MainVehicleBehaviour>();
        }
    }
}