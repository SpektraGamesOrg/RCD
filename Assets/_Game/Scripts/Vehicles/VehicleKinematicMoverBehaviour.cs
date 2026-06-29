using System;
using System.Collections.Generic;
using Core;
using DevOps;
using DG.Tweening;
using Sirenix.OdinInspector;
using SpektraGames.RuntimeUI.Runtime;
using SpektraGames.SpektraUtilities.Runtime;
using UI;
using UIManager;
using Unity.Cinemachine;
using UnityEngine;
using Utils;
using VContainer;

namespace Vehicles
{
    [DefaultExecutionOrder(int.MaxValue)]
    public class VehicleKinematicMoverBehaviour : VehicleBehaviourBase
    {
#if DEV_GAME_ENVIRONMENT || UNITY_EDITOR || !DISABLE_SRDEBUGGER
        [ShowInInspector, ReadOnly] private bool _isKinematicMoverActive = false;
        private bool _flipOverWasActiveBeforeKinematic = true;
        private bool _cameraFreeFallWasEnabledBeforeKinematic = true;

        private readonly float _minRadiusRatio = 0.36f; // 20% of screen height

        [SerializeField, BoxGroup("Circle Detection")]
        public float radiusTolerance = 0.3f;
        [SerializeField, BoxGroup("Circle Detection")]
        public float angleThreshold = 300f;
        private readonly List<Vector2> _points = new List<Vector2>();
        private bool _isDrawing = false;

        private bool _autoForward = false;
        private float _kinematicMoveSpeed = 100f;
        private static KinematicControllersUI _kinematicControllersUI = null;

        private Rigidbody _rigidbody = null;
        private Rigidbody Rigidbody
        {
            get
            {
                if (!_rigidbody)
                    _rigidbody = MainVehicleBehaviour.GetComponent<Rigidbody>();

                return _rigidbody;
            }
        }

        private Vector3 _lastPosition;
        public Vector3 Velocity { get; private set; }

        private void Start()
        {
        }

        private void Update()
        {
            if (!IsValidVehicle())
            {
                if (_isKinematicMoverActive)
                    ToggleKinematicMover();

                return;
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                ToggleKinematicMover();
            }

            DetectCircle();

            if (_isKinematicMoverActive)
                MoveKinematic();
        }

        private bool IsValidVehicle()
        {
            if (CustomSceneManager.IsGameSceneActiveNow)
            {
                return true;
            }

            return false;
        }

        internal void ToggleKinematicMover(bool showToast = true)
        {
            if (!IsValidVehicle())
                return;

            _isKinematicMoverActive = !_isKinematicMoverActive;

            if (_isKinematicMoverActive)
            {
                if (showToast)
                    RuntimeUI.ShowToast(
                        "Kinematic move mode activated. Use Arrow keys, LeftShift, LeftControl, (+) and (-) keys for move");
                Rigidbody.isKinematic = true;
                Rigidbody.interpolation = RigidbodyInterpolation.None;
                MainVehicleBehaviour.transform.rotation =
                    Quaternion.Euler(0f, MainVehicleBehaviour.transform.eulerAngles.y, 0f);
                _lastPosition = Vector3.zero;

                if (!_kinematicControllersUI && !Application.isEditor)
                {
                    _kinematicControllersUI = GameObject
                        .Instantiate(Resources.Load<GameObject>("VehicleKinematicControllers"))
                        .GetComponent<KinematicControllersUI>();
                    _kinematicControllersUI.transform.SetParent(GameUIManager.Instance.GetScreen<GameplayScreen>().Content);
                    _kinematicControllersUI.transform.localPosition = Vector3.zero;
                    _kinematicControllersUI.transform.localRotation = Quaternion.identity;
                    _kinematicControllersUI.transform.localScale = Vector3.one;
                    RectTransform rect = _kinematicControllersUI.GetComponent<RectTransform>();
                    rect.SetLRTB(new Vector4(0f, 0f, 0f, 0f));

                    _kinematicControllersUI.speedUp.pointerClickAction += KinematicSpeedUp;
                    _kinematicControllersUI.speedDown.pointerClickAction += KinematicSpeedDown;
                }

                if (RCC_Settings.Instance)
                    RCC_Settings.Instance.autoReset = false;

                // While kinematic the rigidbody is kinematic, so the wheel colliders stop reporting
                // grounded and RCC_CarControllerV4.isGrounded becomes false. RCC's TPS camera zeroes
                // its rotation damping while the vehicle is "airborne" (TPSFreeFall), which freezes the
                // camera yaw and stops it from following the Q/E rotation. Temporarily disable
                // TPSFreeFall so the camera keeps tracking the vehicle's heading, and restore it on exit.
                if (RCC_Camera.Instance)
                {
                    _cameraFreeFallWasEnabledBeforeKinematic = RCC_Camera.Instance.TPSFreeFall;
                    RCC_Camera.Instance.TPSFreeFall = false;
                }
            }
            else
            {
                if (showToast)
                    RuntimeUI.ShowToast("Kinematic move mode deactivated");
                Rigidbody.isKinematic = false;
                Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

                if (_kinematicControllersUI != null)
                {
                    _kinematicControllersUI.speedUp.pointerClickAction -= KinematicSpeedUp;
                    _kinematicControllersUI.speedDown.pointerClickAction -= KinematicSpeedDown;
                    GameObject.Destroy(_kinematicControllersUI.gameObject);
                }

                float forwardSpeed = Vector3.Dot(Velocity, MainVehicleBehaviour.transform.forward);
                float speedKmh = Mathf.Clamp(forwardSpeed * 3.6f, 0, 1000);
                MainVehicleBehaviour.SetSpeed((int)speedKmh);

                if (RCC_Settings.Instance)
                    RCC_Settings.Instance.autoReset = true;

                // Restore the camera's free-fall behaviour now that physics is driving the vehicle again.
                if (RCC_Camera.Instance)
                    RCC_Camera.Instance.TPSFreeFall = _cameraFreeFallWasEnabledBeforeKinematic;
            }
        }

        private void KinematicSpeedUp()
        {
            _kinematicMoveSpeed += 30f;
            RuntimeUI.ShowToast("Kinematic move speed: " + _kinematicMoveSpeed.ToString());
        }

        private void KinematicSpeedDown()
        {
            _kinematicMoveSpeed -= 30f;
            RuntimeUI.ShowToast("Kinematic move speed: " + _kinematicMoveSpeed.ToString());
        }

        internal void SetKinematicSpeed(float speed)
        {
            _kinematicMoveSpeed = speed;
        }

        internal void SetAutoForward(bool val)
        {
            _autoForward = val;
        }

        private void MoveKinematic()
        {
            if (!Rigidbody.isKinematic)
            {
                Rigidbody.isKinematic = true;
            }

            if (Rigidbody.interpolation != RigidbodyInterpolation.None)
            {
                Rigidbody.interpolation = RigidbodyInterpolation.None;
            }

            if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                KinematicSpeedUp();
            }

            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                KinematicSpeedDown();
            }

            Vector3 position = MainVehicleBehaviour.transform.localPosition;

            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || (_kinematicControllersUI != null &&
                                                                             _kinematicControllersUI.forwardMove
                                                                                 .isPressedNow))
            {
                // FORWARD
                position += new Vector3(0f, 0f, _kinematicMoveSpeed * Time.deltaTime * 1f);
            }

            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || (_kinematicControllersUI != null &&
                                                                               _kinematicControllersUI.backwardMove
                                                                                   .isPressedNow))
            {
                // BACKWARD
                position += new Vector3(0f, 0f, _kinematicMoveSpeed * Time.deltaTime * -1f);
            }

            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || (_kinematicControllersUI != null &&
                                                                               _kinematicControllersUI.leftMove.isPressedNow))
            {
                // LEFT
                position += new Vector3(_kinematicMoveSpeed * Time.deltaTime * -1f * 0.35f, 0f, 0f);
            }

            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) || (_kinematicControllersUI != null &&
                                                                                _kinematicControllersUI.rightMove.isPressedNow))
            {
                // RIGHT
                position += new Vector3(_kinematicMoveSpeed * Time.deltaTime * 1f * 0.35f, 0f, 0f);
            }

            if (Input.GetKey(KeyCode.LeftShift) ||
                (_kinematicControllersUI != null && _kinematicControllersUI.upMove.isPressedNow))
            {
                position += new Vector3(0f, _kinematicMoveSpeed * (Time.deltaTime / 6f) * 1f, 0f);
            }

            if (Input.GetKey(KeyCode.LeftControl) ||
                (_kinematicControllersUI != null && _kinematicControllersUI.downMove.isPressedNow))
            {
                position += new Vector3(0f, _kinematicMoveSpeed * (Time.deltaTime / 6f) * -1f, 0f);
            }

            float rotateSpeed = 110f;
            if (Input.GetKey(KeyCode.Q) ||
                (_kinematicControllersUI != null && _kinematicControllersUI.leftRotate.isPressedNow))
            {
                MainVehicleBehaviour.transform.Rotate(new Vector3(0f, Time.deltaTime * rotateSpeed * -1f, 0f), Space.Self);
            }
            else if (Input.GetKey(KeyCode.E) ||
                     (_kinematicControllersUI != null && _kinematicControllersUI.rightRotate.isPressedNow))
            {
                MainVehicleBehaviour.transform.Rotate(new Vector3(0f, Time.deltaTime * rotateSpeed * 1f, 0f), Space.Self);
            }

            MainVehicleBehaviour.transform.Translate(position - MainVehicleBehaviour.transform.localPosition, Space.Self);

            Velocity = (position - _lastPosition) / Time.deltaTime;
            _lastPosition = position;
        }

        private void DetectCircle()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                if (Input.touchCount > 1)
                {
                    _isDrawing = false;
                    _points.Clear();
                    return;
                }
#endif

            // Start input
            if (GetInputDown(out Vector2 p0))
            {
                _isDrawing = true;
                _points.Clear();
                _points.Add(p0);
            }
            // Continue input
            else if (_isDrawing && GetInputMove(out Vector2 pm))
            {
                _points.Add(pm);
            }
            // End input
            else if (_isDrawing && GetInputUp(out Vector2 p1))
            {
                _isDrawing = false;
                _points.Add(p1);
                if (IsCircle(_points))
                    ToggleKinematicMover();
            }
        }

        bool GetInputDown(out Vector2 p)
        {
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                p = Input.GetTouch(0).position;
                return true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                p = Input.mousePosition;
                return true;
            }

            p = default;
            return false;
        }

        bool GetInputMove(out Vector2 p)
        {
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved)
            {
                p = Input.GetTouch(0).position;
                return true;
            }

            if (Input.GetMouseButton(0))
            {
                p = Input.mousePosition;
                return true;
            }

            p = default;
            return false;
        }

        bool GetInputUp(out Vector2 p)
        {
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended)
            {
                p = Input.GetTouch(0).position;
                return true;
            }

            if (Input.GetMouseButtonUp(0))
            {
                p = Input.mousePosition;
                return true;
            }

            p = default;
            return false;
        }

        bool IsCircle(List<Vector2> pts)
        {
            if (pts.Count < 10) return false;
            // 1. centroid
            Vector2 c = Vector2.zero;
            foreach (var v in pts) c += v;
            c /= pts.Count;
            // 2. radii and angles
            float sumAngle = 0f;
            float prevAngle = Mathf.Atan2(pts[0].y - c.y, pts[0].x - c.x) * Mathf.Rad2Deg;
            List<float> radii = new List<float>(pts.Count);
            foreach (var v in pts)
            {
                Vector2 d = v - c;
                radii.Add(d.magnitude);
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(prevAngle, ang);
                sumAngle += delta;
                prevAngle = ang;
            }

            // 3. check radius variance
            float avgR = 0f;
            foreach (var r in radii) avgR += r;
            avgR /= radii.Count;
            float var = 0f;
            foreach (var r in radii) var += Mathf.Abs(r - avgR);
            var /= (avgR * radii.Count);

            float dynamicMinRadius = Screen.height * _minRadiusRatio;

            // 4. final decision
            return avgR >= dynamicMinRadius
                   && var <= radiusTolerance
                   && Mathf.Abs(sumAngle) >= angleThreshold;
        }

        private void OnDestroy()
        {
            if (RCC_Settings.Instance)
                RCC_Settings.Instance.autoReset = true;

            // If we are torn down while still kinematic, make sure the camera's free-fall flag is restored.
            if (_isKinematicMoverActive && RCC_Camera.Instance)
                RCC_Camera.Instance.TPSFreeFall = _cameraFreeFallWasEnabledBeforeKinematic;
        }
#endif
    }
}