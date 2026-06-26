using UnityEngine;

namespace Gold
{
    /// <summary>
    /// Keeps this object facing the camera (billboard), so world-space UI like the gold cooldown timer stays
    /// readable from any angle. Aligns the object's forward with the camera's forward in LateUpdate (after the
    /// camera has moved), which avoids the mirrored-text problem of Transform.LookAt and is cheaper.
    ///
    /// The camera is resolved once and cached. Assign it explicitly in the Inspector when possible; otherwise
    /// it falls back to Camera.main a single time (lazy, never per frame) to respect the no-scene-scan rule.
    /// </summary>
    public sealed class BillboardToCamera : MonoBehaviour
    {
        [Tooltip("Camera to face. Leave empty to lazily resolve Camera.main once and cache it.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Lock the billboard to rotate only around the world Y axis (upright). Off = full free " +
                 "rotation toward the camera.")]
        [SerializeField] private bool lockYAxis = false;

        private Transform _cameraTransform;

        private void LateUpdate()
        {
            Transform cam = ResolveCameraTransform();
            if (cam == null)
                return;

            if (lockYAxis)
            {
                // Face the camera but stay upright: project the camera direction onto the horizontal plane.
                Vector3 toCamera = transform.position - cam.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude < 0.0001f)
                    return;

                transform.rotation = Quaternion.LookRotation(toCamera);
            }
            else
            {
                // Align our forward with the camera's forward so text reads correctly (not mirrored).
                transform.rotation = Quaternion.LookRotation(cam.forward, cam.up);
            }
        }

        private Transform ResolveCameraTransform()
        {
            if (_cameraTransform != null)
                return _cameraTransform;

            if (targetCamera == null)
                targetCamera = Camera.main; // lazy, one-time fallback

            if (targetCamera != null)
                _cameraTransform = targetCamera.transform;

            return _cameraTransform;
        }
    }
}
