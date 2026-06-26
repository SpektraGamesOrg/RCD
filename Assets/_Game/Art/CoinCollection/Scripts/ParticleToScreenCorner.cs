using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticlesToScreenCorner : MonoBehaviour
{
    [Header("Referanslar")]
    public Camera cam;

    [Header("Hedef ekran köşesi (viewport 0-1)")]
    public Vector2 screenTarget = new Vector2(0.92f, 0.92f);
    public float targetDepth = 5f;

    [Header("Zamanlama (saniye)")]
    public float pullDelay = 1.1f;
    public float arriveDistance = 0.3f;

    [Header("Hareket")]
    public float pullStrength = 25f;
    public float pullSharpness = 12f;

    [Header("Parçacık başına çeşitlilik")]
    public float strengthVariation = 0.5f;  // 0 = hepsi aynı, 0.5 = ±%50 hız farkı
    public float delayVariation = 0.4f;     // parçacıklar arası ekstra rastgele gecikme (sn)

    ParticleSystem ps;
    ParticleSystem.Particle[] particles;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        if (cam == null) cam = Camera.main;
    }

    void LateUpdate()
    {
        int maxCount = ps.main.maxParticles;
        if (particles == null || particles.Length < maxCount)
            particles = new ParticleSystem.Particle[maxCount];

        int count = ps.GetParticles(particles);

        Vector3 target = cam.ViewportToWorldPoint(
            new Vector3(screenTarget.x, screenTarget.y, targetDepth));

        for (int i = 0; i < count; i++)
        {
            // Parçacığa özgü, frame'den frame'e SABİT kalan 0-1 rastgele değer.
            float rnd = (particles[i].randomSeed % 1000) / 1000f;

            // Her parçacık biraz farklı anda harekete geçsin.
            float thisDelay = pullDelay + rnd * delayVariation;

            float age = particles[i].startLifetime - particles[i].remainingLifetime;
            if (age < thisDelay) continue;

            Vector3 toTarget = target - particles[i].position;

            if (toTarget.magnitude < arriveDistance)
            {
                particles[i].remainingLifetime = 0f;
                continue;
            }

            // Her parçacık biraz farklı hızda gitsin.
            // rnd 0-1 → çarpan (1 - var) ile (1 + var) arası.
            float thisStrength = pullStrength * (1f - strengthVariation + rnd * 2f * strengthVariation);

            Vector3 dir = toTarget.normalized;
            particles[i].velocity = Vector3.Lerp(
                particles[i].velocity,
                dir * thisStrength,
                Time.deltaTime * pullSharpness);
        }

        ps.SetParticles(particles, count);
    }
}