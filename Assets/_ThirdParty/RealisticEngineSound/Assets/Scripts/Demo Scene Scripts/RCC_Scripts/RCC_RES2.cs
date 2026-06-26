//______________________________________________//
//___________Realistic Engine Sounds____________//
//______________________________________________//
//_______Copyright © 2025 Skril Studio__________//
//______________________________________________//
//__________ http://skrilstudio.com/ ___________//
//______________________________________________//
//________ http://fb.com/yugelmobile/ __________//
//______________________________________________//

using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SkrilStudio
{
    public class RCC_RES2 : MonoBehaviour
    {
        [InfoBox(
            "RCC_CarControllerV4 reference is missing. Engine sounds will NOT play. " +
            "Add an RCC_CarControllerV4 to this object or one of its parents.",
            InfoMessageType.Error, "@this.rcc == null")]
        [SerializeField] private RCC_CarControllerV4 rcc; // resolved in OnValidate (edit mode) or set in the inspector

        [InfoBox(
            "No RealisticEngineSound prefabs assigned. Engine sounds will NOT play. " +
            "Add RealisticEngineSound components in the children of this object.",
            InfoMessageType.Error, "@this.res2 == null || this.res2.Length == 0")]
        [SerializeField] private RealisticEngineSound[] res2; // resolved in OnValidate (edit mode) or set in the inspector
        //private RCC_Camera rccCamera;
        private float gasPedalSensity = 0.01f; // sets the sensity of detecting gas pedal pressing
        private int currentActivePrefab = 2; // 0 = exterior, 1 = interior, 2 = scene start

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Resolve references in edit mode so they are baked into the prefab/scene,
            // avoiding any runtime scene traversal or GetComponent lookups.
            if (rcc == null)
                rcc = GetComponentInParent<RCC_CarControllerV4>(true);

            if (res2 == null || res2.Length == 0)
                res2 = GetComponentsInChildren<RealisticEngineSound>(true);
        }
#endif

        void Start()
        {
            //rccCamera = RCC_Camera.Instance;

            // Configuration dependencies are baked at edit time. If they are missing the engine
            // sounds can never play, so report a clear error instead of failing silently.
            if (rcc == null || res2 == null || res2.Length == 0)
            {
                Debug.LogError(
                    $"[RCC_RES2] Engine sounds disabled on '{name}': " +
                    (rcc == null
                        ? "RCC_CarControllerV4 reference is missing (none found on this object or its parents). "
                        : "") +
                    (res2 == null || res2.Length == 0
                        ? "No RealisticEngineSound prefabs assigned (none found in children). "
                        : "") +
                    "Sounds will NOT play until this is fixed on the prefab.", this);
                enabled = false;
                return;
            }

            // Runtime-only dependency: there is no RCC camera in scenes such as the main menu,
            // which is expected. Disable so Update/LateUpdate don't NRE every frame.
            // if (rccCamera == null)
            // {
            //     Debug.LogError(
            //         $"[RCC_RES2] Engine sounds disabled on '{name}': RCC_Camera.Instance is null " +
            //         "(no RCC camera in the scene). Sounds will NOT play.", this);
            //     enabled = false;
            //     return;
            // }

            // prepare res2 prefabs (cache the controller values once, they do not change here)
            float maxRPM = rcc.maxEngineRPM;
            float maxSpeed = rcc.maxspeed;
            for (int i = 0; i < res2.Length; i++)
            {
                res2[i].maxRPMLimit = maxRPM;
                res2[i].carMaxSpeed = maxSpeed;
            }
            rcc.audioType = RCC_CarControllerV4.AudioType.Off;
        }

        void Update()
        {
            if (currentActivePrefab == 2) // avoid enabling two prefabs at the same time
                return;

            RealisticEngineSound active = res2[currentActivePrefab]; // cache, avoids repeated array indexing
            bool engineRunning = rcc.engineRunning;

            if (active.enabled)
            {
                if (engineRunning)
                    active.engineCurrentRPM = rcc.engineRPM; // get rcc car's current rpm
                active.carCurrentSpeed = rcc.speed; // get rcc car's current speed

                bool changingGear = rcc.changingGear;
                active.isShifting = changingGear; // needed for shifting sounds script

                float throttleInput = rcc.throttleInput; // cache, queried several times below
                if (throttleInput >= gasPedalSensity) // gas pedal is pressed
                {
                    active.gasPedalPressing = !changingGear;
                }
                else if (throttleInput > -gasPedalSensity) // gas pedal is not pressing
                {
                    active.gasPedalPressing = false;
                }

                if (rcc.direction == -1) // RCC car is in reverse gear, play reversing sound
                {
                    if (throttleInput <= -gasPedalSensity) // gas pedal is pressing
                        active.gasPedalPressing = true;
                    if (changingGear)
                        active.gasPedalPressing = false;
                    if (active.enableReverseGear)
                        active.isReversing = true;
                }
                else
                {
                    active.isReversing = false;
                }
            }
            // turn off prefab when RCC car's engine is not running
            if (!engineRunning)
                active.engineCurrentRPM = 0;
        }

        void LateUpdate()
        {
            if (res2.Length <= 1) // only one (or no) prefab added, no camera switching possible
            {
                if (currentActivePrefab == 2)
                    currentActivePrefab = 0;
                return;
            }

            if (currentActivePrefab == 2) // scene start
            {
                CameraUpdate();
            }
            else if (res2[currentActivePrefab].enabled)
            {
                CameraUpdate();
            }
        }

        private void CameraUpdate()
        {
            // interior camera
            // if (rccCamera.cameraMode == RCC_Camera.CameraMode.FPS)
            // {
            //     if (currentActivePrefab != 1)
            //     {
            //         // switch sounds
            //         res2[0].gameObject.SetActive(false); // exterior prefab
            //         res2[1].gameObject.SetActive(true); // interior prefab
            //         currentActivePrefab = 1;
            //     }
            // }
            // else // exterior cameras
            {
                if (currentActivePrefab != 0)
                {
                    // switch sounds
                    res2[0].gameObject.SetActive(true); // exterior prefab
                    res2[1].gameObject.SetActive(false); // interior prefab
                    currentActivePrefab = 0;
                }
            }
        }
    }
}