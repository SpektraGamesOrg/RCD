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
using UnityEngine;

namespace SkrilStudio
{
    public class RCC_RES2 : MonoBehaviour
    {
        private RealisticEngineSound[] res2;
        private RCC_CarControllerV4 rcc;
        RCC_Camera rccCamera;
        private GameObject car;
        private GameObject rccCarCamera;
        private float gasPedalSensity = 0.01f; // sets the sensity of detecting gas pedal pressing
        private int currentActivePrefab = 2; // 0 = exterior, 1 = interior, 2 = scene start
        public string rccCameraName = "RCCCamera";
        void Start()
        {
            res2 = GetComponentsInChildren<RealisticEngineSound>();
            car = gameObject.GetFirstParentWithComponent<RCC_CarControllerV4>();
            rcc = car.GetComponent<RCC_CarControllerV4>();
            rccCarCamera = GameObject.Find("" + rccCameraName);
            rccCamera = rccCarCamera.GetComponent<RCC_Camera>();
            // prepare res2 prefabs
            for (int i = 0; i < res2.Length; i++)
            {
                res2[i].maxRPMLimit = rcc.maxEngineRPM;
                res2[i].carMaxSpeed = rcc.maxspeed;
            }
            rcc.audioType = RCC_CarControllerV4.AudioType.Off;
        }
        void Update()
        {
            if (currentActivePrefab != 2) // avoid enabling two prefabs at the same time
            {
                if (res2[currentActivePrefab].enabled)
                {
                    if (rcc.engineRunning)
                        res2[currentActivePrefab].engineCurrentRPM = rcc.engineRPM; // get rcc car's current rpm
                    res2[currentActivePrefab].carCurrentSpeed = rcc.speed; // get rcc car's current speed
                    res2[currentActivePrefab].isShifting = rcc.changingGear; // needed for shifting sounds script
                    if (rcc.throttleInput >= gasPedalSensity) // gas pedal is pressed
                    {
                        if (rcc.changingGear)
                        {
                            res2[currentActivePrefab].gasPedalPressing = false;
                        }
                        else
                        {
                            res2[currentActivePrefab].gasPedalPressing = true;
                        }
                    }
                    if (rcc.throttleInput < gasPedalSensity && rcc.throttleInput > -gasPedalSensity) // gas pedal is not pressing
                    {
                        res2[currentActivePrefab].gasPedalPressing = false;
                    }
                    if (rcc.direction == -1) // RCC car is in reverse gear, play reversing sound
                    {
                        if (rcc.throttleInput <= -gasPedalSensity) // gas pedal is pressing
                        {
                            res2[currentActivePrefab].gasPedalPressing = true;
                        }
                        if (rcc.changingGear)
                        {
                            res2[currentActivePrefab].gasPedalPressing = false;
                        }
                        if (res2[currentActivePrefab].enableReverseGear)
                            res2[currentActivePrefab].isReversing = true;
                    }
                    else
                    {
                        res2[currentActivePrefab].isReversing = false;
                    }
                }
                // turn off prefab when RCC car's engine is not running
                if (!rcc.engineRunning)
                    res2[currentActivePrefab].engineCurrentRPM = 0;
            }
        }
        void LateUpdate()
        {
            if (currentActivePrefab != 2)
            {
                if (res2[currentActivePrefab].enabled && res2.Length > 1)
                    CameraUpdate();
            }
            else // scene start
            {
                if (res2.Length > 1)
                    CameraUpdate();
                else // only one prefab added
                    currentActivePrefab = 0;
            }
        }
        private void CameraUpdate()
        {
            // interior camera
            if (rccCamera.cameraMode == RCC_Camera.CameraMode.FPS)
            {
                if (currentActivePrefab != 1)
                {
                    // switch sounds
                    res2[0].gameObject.SetActive(false); // exterior prefab
                    res2[1].gameObject.SetActive(true); // interior prefab
                    currentActivePrefab = 1;
                }
            }
            else // exterior cameras
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
