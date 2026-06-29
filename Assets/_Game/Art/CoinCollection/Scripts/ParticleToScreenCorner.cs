using Core;
using UI;
using UIManager;
using UnityEngine;

/// <summary>
/// Saçılan parçacıkları, belirli bir gecikmeden sonra hedef coin'e doğru çeker.
/// Hedef hem 3D dünya objesi hem de UI (Overlay / Screen Space - Camera) olabilir.
/// Her parçacık kendi rastgele tohumuna göre farklı hız ve gecikmeyle hareket eder.
///
/// EK: Araba hareket halindeyken parçacıklar geride kalmasın diye, her frame
/// arabanın yer değiştirmesi (carDelta) tüm parçacıklara eklenir. Böylece
/// parçacıklar arabanın referans çerçevesinde kalır ve onunla birlikte ilerler.
///
/// The camera, car and target gold widget are gameplay-runtime objects that cannot be wired in the
/// prefab, so they are resolved lazily the first time they are needed - mirroring how MinimapManager
/// picks up the spawned vehicle the moment it exists. The camera and car come from
/// <see cref="GameManager"/> (the gameplay camera and the spawned vehicle); the UI target
/// (<see cref="coinUI"/>) is the top-bar gold counter exposed by <see cref="GameplayScreen.GoldWidget"/>
/// via <see cref="GameUIManager"/>. They can still be assigned explicitly in the Inspector for
/// non-gameplay scenes; an explicit assignment is never overwritten.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ParticlesToCoin : MonoBehaviour
{
    public enum TargetMode { Coin3D, CoinUI_Overlay, CoinUI_Camera, ScreenCornerFallback }

    [Header("Referanslar")]
    public Camera cam;                       // boşsa Camera.main kullanılır
    public Canvas coinCanvas;                // CoinUI_Camera modunda gereklidir
    public Transform car;                    // hareket eden araba (parçacıklar onu takip eder)

    [Header("Hedef")]
    public TargetMode targetMode = TargetMode.CoinUI_Overlay;
    public Transform coin3D;                 // Coin3D modunda hedef
    public RectTransform coinUI;             // CoinUI_* modlarında hedef
    public Vector2 screenCorner = new Vector2(0.92f, 0.92f); // fallback köşe (viewport 0-1)
    public float targetDepth = 5f;           // UI/köşe modunda kameradan derinlik

    [Header("Zamanlama (saniye)")]
    public float pullDelay = 1.1f;           // bu süre dolmadan parçacığa dokunulmaz
    public float arriveDistance = 0.1f;      // hedefe bu kadar yaklaşan parçacık yok edilir

    [Header("Hareket")]
    public float pullStrength = 25f;
    public float pullSharpness = 12f;

    [Header("Parçacık başına çeşitlilik")]
    [Range(0f, 1f)] public float strengthVariation = 0.4f; // ±% hız farkı
    public float delayVariation = 0.35f;                   // ekstra rastgele gecikme (sn)

    [Header("Araba takibi")]
    [Tooltip("Parçacıklar arabanın hareketini takip etsin mi? Kapalıysa çarpışma noktasında sabit kalırlar.")]
    public bool followCar = true;

    ParticleSystem ps;
    ParticleSystem.Particle[] particles;
    Vector3 lastCarPos;
    bool hasLastCarPos;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        ResolveGameplayRefs();
        if (car)
        {
            lastCarPos = car.position;
            hasLastCarPos = true;
        }
    }

    /// <summary>
    /// Fills the camera, car and UI target when they have not been assigned. They all come up
    /// asynchronously, so this is safe to call repeatedly: it only ever fills a missing reference and never
    /// overwrites one set in the Inspector or already resolved. The car is initialized for the follow-delta
    /// the first time it resolves. The gold widget is only looked up once the car is live (HUD is present
    /// by then) so GameUIManager.GetScreen never logs a "not found" before the gameplay HUD exists.
    /// </summary>
    void ResolveGameplayRefs()
    {
        if (GameManager.Exists())
        {
            GameManager gm = GameManager.Instance;

            if (!cam && gm.RccCamera)
                cam = gm.RccCamera.actualCamera;

            if (!car && gm.SpawnedVehicle)
            {
                car = gm.SpawnedVehicle.transform;
                lastCarPos = car.position;
                hasLastCarPos = true;
            }
        }

        // The on-screen gold counter is the pull target. Resolve it only once the car is live, so the
        // lookup happens while the gameplay HUD exists (GetScreen logs an error on a miss otherwise).
        if (!coinUI && car && GameUIManager.Exists())
        {
            GameplayScreen hud = GameUIManager.Instance.GetScreen<GameplayScreen>();
            if (hud && hud.GoldWidget)
                coinUI = hud.GoldWidget;
        }
    }

    void LateUpdate()
    {
        // Pick up the gameplay camera/car the moment they exist (gold may be spawned before the vehicle).
        if (!cam || !car)
            ResolveGameplayRefs();

        int maxCount = ps.main.maxParticles;
        if (particles == null || particles.Length < maxCount)
            particles = new ParticleSystem.Particle[maxCount];

        int count = ps.GetParticles(particles);

        // Arabanın bu frame'deki yer değiştirmesini hesapla.
        Vector3 carDelta = Vector3.zero;
        if (followCar && car)
        {
            if (hasLastCarPos)
                carDelta = car.position - lastCarPos;
            lastCarPos = car.position;
            hasLastCarPos = true;
        }

        // Hedefin dünya pozisyonunu HER FRAME hesapla (kamera hareketli olduğu için şart).
        Vector3 target = ResolveTargetWorldPosition();

        for (int i = 0; i < count; i++)
        {
            // Parçacıkları arabanın hareketi kadar kaydır — böylece geride kalmazlar.
            // Bu, çekim fazından ÖNCE de uygulanır ki havadaki parçacıklar da arabayla aksın.
            particles[i].position += carDelta;

            // Parçacığa özgü, ömrü boyunca SABİT kalan 0-1 rastgele değer.
            float rnd = (particles[i].randomSeed % 1000) / 1000f;

            float thisDelay = pullDelay + rnd * delayVariation;
            float age = particles[i].startLifetime - particles[i].remainingLifetime;
            if (age < thisDelay) continue; // saçılma + havada durma fazına karışma

            Vector3 toTarget = target - particles[i].position;

            // Hedefe vardıysa parçacığı yok et (birikme/titreşim olmasın).
            if (toTarget.magnitude < arriveDistance)
            {
                particles[i].remainingLifetime = 0f;
                continue;
            }

            // Her parçacık biraz farklı hızda gitsin.
            float thisStrength = pullStrength *
                (1f - strengthVariation + rnd * 2f * strengthVariation);

            Vector3 dir = toTarget.normalized;
            particles[i].velocity = Vector3.Lerp(
                particles[i].velocity,
                dir * thisStrength,
                Time.deltaTime * pullSharpness);
        }

        ps.SetParticles(particles, count);
    }

    Vector3 ResolveTargetWorldPosition()
    {
        switch (targetMode)
        {
            case TargetMode.Coin3D:
                if (coin3D) return coin3D.position;
                break;

            case TargetMode.CoinUI_Overlay:
                // Overlay'de RectTransform.position zaten ekran (pixel) pozisyonudur.
                if (coinUI)
                {
                    Vector3 sp = coinUI.position;
                    sp.z = targetDepth;
                    return cam.ScreenToWorldPoint(sp);
                }
                break;

            case TargetMode.CoinUI_Camera:
                // Screen Space - Camera: önce ekran pozisyonuna çevir.
                if (coinUI)
                {
                    Camera uiCam = (coinCanvas && coinCanvas.worldCamera)
                        ? coinCanvas.worldCamera : cam;
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, coinUI.position);
                    Vector3 sp = new Vector3(screen.x, screen.y, targetDepth);
                    return cam.ScreenToWorldPoint(sp);
                }
                break;
        }

        // Fallback: ekran köşesi.
        return cam.ViewportToWorldPoint(
            new Vector3(screenCorner.x, screenCorner.y, targetDepth));
    }

    // Hedefi gözle görmek için (Scene view'da kırmızı küre + sarı çizgi).
    void OnDrawGizmos()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        Vector3 target = ResolveTargetWorldPosition();

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(target, 0.3f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target);
    }
}