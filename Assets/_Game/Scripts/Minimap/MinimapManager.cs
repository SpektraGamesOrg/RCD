using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Vehicles;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Minimap
{
    /// <summary>
    /// Scene-local owner of the minimap. Lives in the Game scene, defines the world area the minimap covers,
    /// loads the baked top-down map texture asynchronously from Resources (no serialized texture reference on
    /// any script or UI), and every frame feeds the <see cref="MinimapWidget"/> in the gameplay HUD. The
    /// minimap is GPS-style: the vehicle is pinned to the circle centre while the map scrolls and rotates so
    /// the heading always points up, and markers rotate with it and clamp to the rim so they never leave the
    /// border.
    ///
    /// It picks up <see cref="GameManager.SpawnedVehicle"/> the moment it exists, so following auto-starts as
    /// soon as <see cref="GameManager.SpawnCurrentVehicleAsync"/> completes. The loaded texture is released by
    /// <see cref="UnloadMapTexture"/>, called from CustomSceneManager when returning to the main menu. All
    /// per-frame work is allocation free.
    /// </summary>
    public sealed class MinimapManager : SingletonComponent<MinimapManager>
    {
        [Title("Minimap", "Top-down radar HUD driven from the Game scene", TitleAlignments.Centered)]

        [BoxGroup("World Area")]
        [InfoBox("Frame the drivable world with the Scene view handles (or the fields below). The baked map " +
                 "texture is captured for this square; the circle around the vehicle is what stays visible.")]
        [Tooltip("World-space centre of the area the baked map covers.")]
        [SerializeField] private Vector3 areaCenter = Vector3.zero;

        [BoxGroup("World Area")]
        [Tooltip("World-space side length (metres) of the square the baked map covers.")]
        [SerializeField, MinValue(1f)] private float areaSize = 1000f;

#if UNITY_EDITOR
        [BoxGroup("Map Texture")]
        [InfoBox("The baked map is loaded async from Resources at runtime and freed on return to the main " +
                 "menu - it is never serialized onto a script or UI. Keep the capture path under a 'Resources' " +
                 "folder.")]
        [ShowInInspector, ReadOnly, PreviewField(72, ObjectFieldAlignment.Left)]
        [LabelText("Baked Map (preview)")]
        private Texture2D BakedMapPreview => UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(captureOutputPath);
#endif

        [BoxGroup("View")]
        [LabelText("Zoom (world units across)")]
        [Tooltip("ZOOM: how many world units span across the circle's diameter. Smaller = more zoomed in. " +
                 "Editable live in play mode - changes take effect immediately.")]
        [SerializeField, MinValue(1f)] private float viewWorldDiameter = 300f;

        [BoxGroup("View")]
        [Tooltip("Markers stay within this fraction of the radius, so they never touch the border.")]
        [SerializeField, Range(0.5f, 1f)] private float markerEdgeClamp = 0.92f;

        [BoxGroup("View")]
        [Tooltip("Optional transform the map heading aligns to (its forward on the XZ plane). Leave empty to " +
                 "use the gameplay camera (GameManager's RCC camera actualCamera). The minimap is still " +
                 "centred on the vehicle - only the rotation comes from here.")]
        [SerializeField] private Transform rotationSource;

        [BoxGroup("View")]
        [Tooltip("Rotate the centre player icon to the vehicle's heading relative to the (camera-aligned) " +
                 "map. Off keeps it pointing straight up.")]
        [SerializeField] private bool rotatePlayerToVehicle = true;

        [BoxGroup("View")]
        [Tooltip("Flip if the map rotates the wrong way relative to the camera direction.")]
        [SerializeField] private bool invertMapRotation;

        [BoxGroup("Capture")]
        [Tooltip("Resolution (pixels) of the baked square map texture.")]
        [SerializeField, MinValue(64)] private int captureResolution = 1024;

        [BoxGroup("Capture")]
        [Tooltip("Which layers the top-down capture renders.")]
        [SerializeField] private LayerMask captureLayers = ~0;

        [BoxGroup("Capture")]
        [Tooltip("Background colour for the captured map.")]
        [SerializeField] private Color captureBackground = new Color(0.15f, 0.16f, 0.18f, 1f);

        [BoxGroup("Capture")]
        [Tooltip("Render scene fog into the baked map. Off gives a cleaner, flatter top-down map.")]
        [SerializeField] private bool captureFog;

        [BoxGroup("Capture")]
        [Tooltip("Render post-processing (bloom, colour grading, vignette...) into the baked map. " +
                 "Off is usually cleaner for a minimap.")]
        [SerializeField] private bool capturePostProcessing;

        [BoxGroup("Capture")]
        [Tooltip("Render real-time shadows into the baked map.")]
        [SerializeField] private bool captureShadows = true;

        [BoxGroup("Capture")]
        [Tooltip("Height (metres) above the area centre the capture camera is placed.")]
        [SerializeField, MinValue(1f)] private float captureHeight = 500f;

        [BoxGroup("Capture")]
        [Tooltip("Project-relative path the baked PNG is written to. MUST be under a 'Resources' folder so it " +
                 "can be loaded async at runtime (the part after '/Resources/' is the load path).")]
        [SerializeField] private string captureOutputPath = "Assets/_Game/Resources/Minimap/MinimapBaked.png";

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        private bool HasVehicle => _target;

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        private bool HudConnected => MinimapWidget.Instance;

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        private bool MapLoaded => _mapTexture;

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        private int ActiveMarkers => Markers.Count;

        // Every enabled marker in the scene. Markers register/unregister themselves, so this never needs a
        // scene scan and stays allocation free to iterate.
        private static readonly List<MinimapMarker> Markers = new List<MinimapMarker>(32);

        // The async-loaded baked map. Static so it can be released by UnloadMapTexture after the Game scene
        // (and this manager) are torn down on the way back to the main menu.
        private static Texture2D _mapTexture;

        private Transform _target;
        private bool _trackingActive;
        private bool _mapLoadStarted;

        // How many view-diameters the full baked map spans. Used to size the map image inside the widget.
        private float MapExtentRatio => areaSize / Mathf.Max(1f, viewWorldDiameter);

        public static void RegisterMarker(MinimapMarker marker)
        {
            if (marker && !Markers.Contains(marker)) Markers.Add(marker);
        }

        public static void UnregisterMarker(MinimapMarker marker)
        {
            Markers.Remove(marker);
        }

        /// <summary>
        /// Releases the async-loaded baked map texture from memory and clears it off the (persistent) HUD
        /// widget. Called by CustomSceneManager when returning to the main menu, before its shared memory
        /// cleanup, so the gameplay-only texture is fully reclaimed.
        /// </summary>
        public static void UnloadMapTexture()
        {
            if (MinimapWidget.Instance) MinimapWidget.Instance.ClearMap();

            if (_mapTexture)
            {
                Resources.UnloadAsset(_mapTexture);
                _mapTexture = null;
            }
        }

        private void Start()
        {
            // Kick the async load early so the map is ready by the time the gameplay HUD appears. Reuses a
            // texture still resident from a previous session (i.e. if it was not unloaded yet).
            if (!_mapTexture && !_mapLoadStarted)
            {
                _mapLoadStarted = true;
                LoadMapAsync().Forget();
            }
        }

        private async UniTaskVoid LoadMapAsync()
        {
            string resourcePath = ResolveResourcePath();
            if (string.IsNullOrEmpty(resourcePath))
            {
                Debug.LogError($"[MinimapManager] Capture path '{captureOutputPath}' is not under a 'Resources' " +
                               "folder, so the baked map cannot be loaded at runtime.");
                return;
            }

            ResourceRequest request = Resources.LoadAsync<Texture2D>(resourcePath);
            await request.ToUniTask();
            _mapTexture = request.asset as Texture2D;

            if (!_mapTexture)
                Debug.LogError($"[MinimapManager] No baked minimap texture found at 'Resources/{resourcePath}'.");
        }

        // Extracts the Resources-relative load path (no extension) from the capture path, or null when the
        // capture path is not under a 'Resources' folder.
        private string ResolveResourcePath()
        {
            if (string.IsNullOrEmpty(captureOutputPath)) return null;

            string normalized = captureOutputPath.Replace('\\', '/');
            const string marker = "/Resources/";
            int index = normalized.LastIndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;

            string relative = normalized.Substring(index + marker.Length);
            int dot = relative.LastIndexOf('.');
            return dot >= 0 ? relative.Substring(0, dot) : relative;
        }

        private void LateUpdate()
        {
            MinimapWidget widget = MinimapWidget.Instance;
            if (!widget || !widget.isActiveAndEnabled) return; // HUD is hidden; nothing to draw.

            // Acquire the spawned vehicle as soon as it exists, then keep following it.
            if (!_target && GameManager.Exists())
            {
                MainVehicleBehaviour vehicle = GameManager.Instance.SpawnedVehicle;
                if (vehicle) _target = vehicle.transform;
            }

            if (!_target)
            {
                if (_trackingActive)
                {
                    widget.SetTrackingActive(false);
                    _trackingActive = false;
                }

                return;
            }

            // Push the map onto the widget once it has loaded (and re-push if the HUD cleared it on hide).
            if (_mapTexture && widget.CurrentMap != _mapTexture)
                widget.ConfigureMap(_mapTexture);

            if (!_trackingActive)
            {
                widget.SetTrackingActive(true);
                _trackingActive = true;
            }

            UpdateMinimap(widget);
        }

        // The heading (degrees) the map aligns to: the rotation source if set, else the gameplay camera, with
        // the vehicle as a last resort. Uses the forward vector projected on the XZ plane so it stays correct
        // even though the camera is pitched down.
        private float ResolveViewYaw()
        {
            Transform source = rotationSource;
            if (!source && GameManager.Exists())
            {
                RCC_Camera rccCamera = GameManager.Instance.RccCamera;
                if (rccCamera && rccCamera.actualCamera)
                    source = rccCamera.actualCamera.transform;
            }

            if (!source) source = _target;
            if (!source) return 0f;

            Vector3 forward = source.forward;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        private void UpdateMinimap(MinimapWidget widget)
        {
            float viewHalf = viewWorldDiameter * 0.5f;
            if (viewHalf < 0.0001f) return;
            float invHalf = 1f / viewHalf;

            Vector3 p = _target.position;
            float yaw = ResolveViewYaw();
            float rotZ = invertMapRotation ? -yaw : yaw;

            // Scroll the map so the vehicle sits at the centre, and rotate so its heading points up. The
            // extent ratio is pushed every frame so changing the zoom (viewWorldDiameter) responds live.
            Vector2 rawPlayerNorm = new Vector2((p.x - areaCenter.x) * invHalf, (p.z - areaCenter.z) * invHalf);
            widget.SetMapView(rawPlayerNorm, rotZ, MapExtentRatio);

            // Rotate the centre player icon to the vehicle heading relative to the camera-aligned map (so it
            // shows which way the car points while "up" stays the camera/view direction).
            float playerZ = 0f;
            if (rotatePlayerToVehicle)
            {
                Vector3 vehicleForward = _target.forward;
                float vehicleYaw = Mathf.Atan2(vehicleForward.x, vehicleForward.z) * Mathf.Rad2Deg;
                playerZ = rotZ - vehicleYaw;
            }

            widget.SetPlayerHeading(playerZ);

            float rad = rotZ * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            int active = 0;
            for (int i = 0; i < Markers.Count; i++)
            {
                MinimapMarker marker = Markers[i];
                if (!marker) continue;

                Vector3 wp = marker.transform.position;
                float dx = (wp.x - p.x) * invHalf;
                float dz = (wp.z - p.z) * invHalf;

                // Rotate the world-aligned offset by the map rotation so markers track the rotated map.
                float sx = dx * cos - dz * sin;
                float sy = dx * sin + dz * cos;

                float mag = Mathf.Sqrt(sx * sx + sy * sy);
                if (marker.ClampToEdge)
                {
                    // Pin to the rim so it never leaves the border.
                    if (mag > markerEdgeClamp && mag > 0.0001f)
                    {
                        float k = markerEdgeClamp / mag;
                        sx *= k;
                        sy *= k;
                    }
                }
                else if (mag > 1f)
                {
                    // Not clamped: let the circular mask clip it so it slides in/out smoothly instead of
                    // popping in. Only cull (deactivate) once it is fully past the rim - i.e. already
                    // invisible - so there is no visible spawn at the edge.
                    float iconHalfNorm = Mathf.Max(marker.Size.x, marker.Size.y) * 0.5f /
                                         Mathf.Max(1f, widget.Radius);
                    if (mag > 1f + iconHalfNorm + 0.02f) continue;
                }

                // Keep the icon upright, or spin it with the map when the marker opts out of staying upright.
                float iconRotation = marker.KeepUpright ? 0f : rotZ;
                widget.SetMarker(active, marker.Icon, marker.Size, marker.IconColor, new Vector2(sx, sy), iconRotation);
                active++;
            }

            widget.HideMarkersFrom(active);
        }

#if UNITY_EDITOR
        // Surfaces a top-of-inspector error when Scene view gizmos are off, because the minimap area square,
        // its resize handles and the view circle are gizmo-drawn and become invisible / uneditable without
        // them. The Fix button re-enables gizmos on the active Scene view.
        [OnInspectorGUI, PropertyOrder(-1000)]
        private void DrawGizmoStateWarning()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.drawGizmos) return;

            EditorGUILayout.HelpBox(
                "Gizmos are disabled in the active Scene view, so the minimap area handles and preview are " +
                "hidden - you cannot see or edit the area. Turn Gizmos on to edit it.",
                MessageType.Error);

            if (GUILayout.Button("Fix: Enable Gizmos in the Scene View"))
            {
                sceneView.drawGizmos = true;
                sceneView.Repaint();
            }

            EditorGUILayout.Space();
        }

        [BoxGroup("Capture")]
        [Button("Capture Top-Down Map Texture", ButtonSizes.Large), GUIColor(0.4f, 0.9f, 0.55f)]
        private void CaptureMapTexture()
        {
            if (string.IsNullOrEmpty(ResolveResourcePath()))
            {
                Debug.LogError($"[MinimapManager] Capture path '{captureOutputPath}' must be under a " +
                               "'Resources' folder so the baked map can be loaded async at runtime.");
                return;
            }

            int res = Mathf.Max(64, captureResolution);

            GameObject camObject = new GameObject("~MinimapCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
            Camera cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = areaSize * 0.5f;
            cam.aspect = 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = captureBackground;
            cam.cullingMask = captureLayers;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = captureHeight * 2f + areaSize + 1000f;

            // Look straight down with image up = +Z (north) and image right = +X (east), matching the runtime
            // mapping in UpdateMinimap.
            cam.transform.position = areaCenter + Vector3.up * captureHeight;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Per-camera URP effect toggles so the baked map is exactly as clean (or rich) as wanted.
            UniversalAdditionalCameraData urpData = cam.GetUniversalAdditionalCameraData();
            if (urpData)
            {
                urpData.renderPostProcessing = capturePostProcessing;
                urpData.renderShadows = captureShadows;
                urpData.antialiasing = AntialiasingMode.None;
            }

            // Fog is a global render setting, so toggle it around the render and restore it afterwards.
            bool previousFog = RenderSettings.fog;
            RenderSettings.fog = captureFog;

            RenderTexture rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32)
            {
                name = "~MinimapCaptureRT",
                antiAliasing = 1
            };
            cam.targetTexture = rt;

            // Pipeline-agnostic render request (works under URP in Unity 6); fall back to legacy if needed.
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();

            RenderSettings.fog = previousFog;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D texture = new Texture2D(res, res, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, res, res), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            cam.targetTexture = null;
            byte[] png = texture.EncodeToPNG();

            DestroyImmediate(texture);
            rt.Release();
            DestroyImmediate(rt);
            DestroyImmediate(camObject);

            string directory = Path.GetDirectoryName(captureOutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(captureOutputPath, png);
            // Refresh (not just ImportAsset) so a freshly created Resources folder is discovered too.
            AssetDatabase.Refresh();

            if (AssetImporter.GetAtPath(captureOutputPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = Mathf.Max(res, 32);
                importer.SaveAndReimport();
            }

            // No serialized reference is kept: the texture is loaded async from Resources at runtime.
            Debug.Log($"[MinimapManager] Captured baked map ({res}x{res}) to '{captureOutputPath}' " +
                      $"(Resources path '{ResolveResourcePath()}').");
        }

        private void OnDrawGizmosSelected()
        {
            float h = areaSize * 0.5f;
            Vector3 a = areaCenter + new Vector3(-h, 0f, -h);
            Vector3 b = areaCenter + new Vector3(h, 0f, -h);
            Vector3 c = areaCenter + new Vector3(h, 0f, h);
            Vector3 d = areaCenter + new Vector3(-h, 0f, h);

            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
#endif
    }
}
