using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;


public class ShrinkOnTrigger : MonoBehaviour
{
    [SerializeField] private ParticleSystem particles;   // oynatýlacak particle
    [SerializeField] private float shrinkDuration = 0.2f; // saniye cinsinden süre
    [SerializeField] private string targetTag = "";      // boþsa her þeyle tetiklenir

    private bool isShrinking = false;
    public Vector3 rotationAngle = Vector3.zero;
    private readonly float _rotationMultiplier = 40f;
    private void OnTriggerEnter(Collider other)
    {
        if (isShrinking) return;

        // Ýstersen sadece belirli tag'le tetiklensin
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag)) return;

        if (particles != null)
            particles.Play();

        StartCoroutine(ShrinkToZero());
    }
    private void LateUpdate()
    {
        transform.Rotate(rotationAngle * (Time.deltaTime * _rotationMultiplier), Space.Self);
    }
    [Button]
    public void TriggerShrink()
    {
        if (particles != null) particles.Play();
        StartCoroutine(ShrinkToZero());
    }
    private IEnumerator ShrinkToZero()
    {
        isShrinking = true;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        // Tamamen yok etmek istersen:
        // Destroy(gameObject);
    }
}