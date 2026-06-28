using System.Collections.Generic;
using Sirenix.OdinInspector;
using UISystem.Runtime.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Minimap
{
    /// <summary>
    /// Pure renderer for the circular minimap. Lives inside the persistent GameplayScreen HUD and is driven
    /// every frame by <see cref="MinimapManager"/> in the Game scene. It owns the circular mask, the baked
    /// map image (which scrolls and rotates under a fixed, centred player arrow), a reusable pool of marker
    /// icons, and the optional decorative frame. It performs no per-frame allocations and holds no gameplay
    /// logic: the manager computes every normalised position and pushes it in through the setter methods.
    /// </summary>
    public sealed class MinimapWidget : UIWidget
    {
        // Exactly one minimap HUD exists in the persistent canvas. It self-registers here for its whole
        // lifetime (set in Awake), so the Game-scene MinimapManager can reach it without any scene scan - even
        // when the HUD is hidden, which the unload path needs. The manager skips work while it is not
        // isActiveAndEnabled (i.e. while the HUD is hidden).
        public static MinimapWidget Instance { get; private set; }

        [Title("References")]
        [Tooltip("Square, circularly-masked area. Its half-size defines the on-screen radius for all mapping.")]
        [SerializeField] private RectTransform viewRoot;

        [Tooltip("Centred pivot that holds the map image. Rotated so the vehicle heading always points up.")]
        [SerializeField] private RectTransform mapPivot;

        [Tooltip("The baked top-down world texture. Scrolled so the vehicle stays at the circle centre.")]
        [SerializeField] private RawImage mapImage;

        [Tooltip("Centred, non-rotating container that holds the pooled marker icons.")]
        [SerializeField] private RectTransform markersRoot;

        [Tooltip("Inactive template cloned to build the marker icon pool.")]
        [SerializeField] private Image markerIconTemplate;

        [Tooltip("Optional fixed arrow drawn at the circle centre (your art). Always points up.")]
        [SerializeField] private RectTransform playerArrow;

        // Reused icon pool. Grows only when more markers are visible than ever before; never shrinks.
        private readonly List<Image> _iconPool = new List<Image>(16);

        private float _mapRatio = 1f;
        private float _lastRadius = -1f;

        /// <summary>On-screen radius of the circular view, in canvas units.</summary>
        public float Radius => viewRoot ? Mathf.Min(viewRoot.rect.width, viewRoot.rect.height) * 0.5f : 0f;

        /// <summary>The texture currently shown on the map, or null. Lets the manager (re)push the map only
        /// when it actually changed.</summary>
        public Texture CurrentMap => mapImage ? mapImage.texture : null;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            if (markerIconTemplate) markerIconTemplate.gameObject.SetActive(false);
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private void OnDisable()
        {
            HideMarkersFrom(0);
        }

        /// <summary>Drops the baked map texture so the (Resources) asset can be unloaded from memory.</summary>
        public void ClearMap()
        {
            if (mapImage)
            {
                mapImage.texture = null;
                mapImage.enabled = false;
            }

            HideMarkersFrom(0);
        }

        /// <summary>
        /// Assigns the baked map texture. Called by the manager once the async Resources load completes (and
        /// re-pushed if the HUD had cleared the texture). The on-screen size/zoom is driven live by
        /// <see cref="SetMapView"/>.
        /// </summary>
        public void ConfigureMap(Texture texture)
        {
            if (mapImage)
            {
                mapImage.texture = texture;
                mapImage.enabled = texture;
            }

            // Force a resize on the next view update against the live radius/zoom.
            _lastRadius = -1f;
        }

        /// <summary>
        /// Positions and rotates the map so the vehicle sits at the centre and its heading points up.
        /// <paramref name="rawPlayerNorm"/> is the vehicle offset from the map centre in units of the view
        /// half-size (1 == circle radius). <paramref name="rotationZ"/> is the map rotation in degrees.
        /// <paramref name="mapExtentRatio"/> is how many view-diameters the full map spans; passing it every
        /// frame makes the zoom respond live (e.g. when changed in the inspector during play).
        /// </summary>
        public void SetMapView(Vector2 rawPlayerNorm, float rotationZ, float mapExtentRatio)
        {
            float r = Radius;
            mapExtentRatio = Mathf.Max(0.0001f, mapExtentRatio);
            if (!Mathf.Approximately(r, _lastRadius) || !Mathf.Approximately(mapExtentRatio, _mapRatio))
                ApplyMapSize(r, mapExtentRatio);

            if (mapImage)
                mapImage.rectTransform.anchoredPosition = new Vector2(-rawPlayerNorm.x * r, -rawPlayerNorm.y * r);

            if (mapPivot)
            {
                Vector3 e = mapPivot.localEulerAngles;
                e.z = rotationZ;
                mapPivot.localEulerAngles = e;
            }
        }

        /// <summary>
        /// Configures a single marker icon, growing the pool as needed. <paramref name="normPos"/> is the
        /// final on-screen position in units of the view half-size (already rotated and clamped by the
        /// manager). <paramref name="sizePx"/> is the icon's pixel size on the minimap.
        /// <paramref name="iconRotationZ"/> rotates the icon itself: 0 keeps it upright, while passing the map
        /// rotation makes it spin with the map.
        /// </summary>
        public void SetMarker(int index, Sprite sprite, Vector2 sizePx, Color color, Vector2 normPos,
            float iconRotationZ)
        {
            Image icon = GetIcon(index);
            if (!icon) return;

            icon.sprite = sprite;
            icon.color = color;

            RectTransform rt = icon.rectTransform;
            rt.sizeDelta = sizePx;
            rt.anchoredPosition = new Vector2(normPos.x * Radius, normPos.y * Radius);
            rt.localEulerAngles = new Vector3(0f, 0f, iconRotationZ);

            if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
        }

        /// <summary>Hides every pooled icon at or beyond <paramref name="from"/> (called after the manager
        /// has filled the active range each frame).</summary>
        public void HideMarkersFrom(int from)
        {
            for (int i = from; i < _iconPool.Count; i++)
            {
                Image icon = _iconPool[i];
                if (icon && icon.gameObject.activeSelf) icon.gameObject.SetActive(false);
            }
        }

        /// <summary>Rotates the centre player arrow to show the vehicle heading relative to the
        /// (camera-aligned) map. Pass 0 to keep it pointing up.</summary>
        public void SetPlayerHeading(float rotationZ)
        {
            if (playerArrow) playerArrow.localEulerAngles = new Vector3(0f, 0f, rotationZ);
        }

        /// <summary>Shows or hides the map + player arrow. Used while no vehicle is being tracked.</summary>
        public void SetTrackingActive(bool on)
        {
            if (mapImage) mapImage.enabled = on && mapImage.texture;
            if (playerArrow) playerArrow.gameObject.SetActive(on);
            if (!on) HideMarkersFrom(0);
        }

        private void ApplyMapSize(float radius, float ratio)
        {
            _mapRatio = ratio;
            _lastRadius = radius;

            if (mapImage)
            {
                float diameter = radius * 2f * ratio;
                mapImage.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            }
        }

        private Image GetIcon(int index)
        {
            if (!markerIconTemplate) return null;

            while (_iconPool.Count <= index)
            {
                Image clone = Instantiate(markerIconTemplate, markersRoot);
                clone.gameObject.SetActive(false);
                _iconPool.Add(clone);
            }

            return _iconPool[index];
        }
    }
}
