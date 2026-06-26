using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Collect VFX for a coin/gold pickup: plays an optional particle burst and shrinks the object toward a
/// target scale over a short duration. It does NOT handle triggering or spinning - the owning <c>Gold</c>
/// component owns the pickup trigger and the spin, and calls <see cref="TriggerShrink"/> with the target
/// (passive) scale when the gold is collected.
/// </summary>
public class ShrinkOnTrigger : MonoBehaviour
{
    [SerializeField] private ParticleSystem particles;    // particle burst played on collect
    [SerializeField] private float shrinkDuration = 0.2f; // seconds the shrink takes
    [SerializeField] private Transform shrinkTarget;      // what gets scaled; defaults to this transform

    private bool _isShrinking;
    private Coroutine _shrinkRoutine;

    private Transform Target => shrinkTarget != null ? shrinkTarget : transform;

    /// <summary>
    /// Plays the collect VFX: the particle burst (if assigned) and a shrink animation toward
    /// <paramref name="targetScale"/> (an absolute local scale - the passive/cooldown size, computed by the
    /// caller so the coin's non-uniform proportions are preserved). Safe to call repeatedly; a new call
    /// restarts the animation toward the new target.
    /// </summary>
    public void TriggerShrink(Vector3 targetScale)
    {
        if (particles != null)
            particles.Play();

        if (_shrinkRoutine != null)
            StopCoroutine(_shrinkRoutine);

        _shrinkRoutine = StartCoroutine(ShrinkTo(targetScale));
    }

    private IEnumerator ShrinkTo(Vector3 targetScale)
    {
        _isShrinking = true;
        Transform target = Target;
        Vector3 startScale = target.localScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            target.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        target.localScale = targetScale;
        _isShrinking = false;
        _shrinkRoutine = null;
    }
}
