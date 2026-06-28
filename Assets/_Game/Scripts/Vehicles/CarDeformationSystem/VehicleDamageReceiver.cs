using UnityEngine;

namespace Vehicles.CarDeformationSystem
{
    public class VehicleDamageReceiver : VehicleBehaviourBase
    {
        [Header("Impact")]
        [SerializeField, Tooltip("Speed (relativeVelocity.magnitude) that maps to full damage.")]
        private float maxImpact = 25f;

        [SerializeField, Tooltip("Contacts slower than this are ignored.")]
        private float minImpact = 3f;

        [SerializeField, Tooltip("Damage painters under this vehicle. Auto-wired in the editor.")]
        private VertexDamagePainter[] painters;

        // Reused contact buffer, allocated once to avoid per-collision GC.
        private readonly ContactPoint[] contacts = new ContactPoint[16];
        private bool dirty; // damage happened this frame and still needs uploading/saving

        private void Awake()
        {
            // Editor wiring is the source of truth; this fallback only runs if the array is empty.
            if (painters == null || painters.Length == 0)
                painters = GetComponentsInChildren<VertexDamagePainter>(true);
        }

        private void OnCollisionEnter(Collision col)
        {
            float impact = col.relativeVelocity.magnitude;
            if (impact < minImpact) return;

            float amount = Mathf.Clamp01(impact / maxImpact);

            int count = col.GetContacts(contacts);
            for (int k = 0; k < count; k++)
            {
                Vector3 worldPoint = contacts[k].point;
                for (int p = 0; p < painters.Length; p++)
                    painters[p].ApplyDamage(worldPoint, amount);
            }

            dirty = true;
        }

        private void LateUpdate()
        {
            // Once every collision of this frame has been applied, upload and persist in a single
            // pass, and only for the painters that actually changed, to avoid redundant mesh
            // uploads and disk writes for untouched panels.
            if (!dirty) return;
            dirty = false;

            for (int p = 0; p < painters.Length; p++)
            {
                VertexDamagePainter painter = painters[p];
                if (!painter.HasPendingChanges) continue;

                painter.SaveDamage();
                painter.FlushColors();
            }
        }

        /// <summary>Forces every painter to persist its current damage to disk.</summary>
        public void SaveAllDamage()
        {
            for (int p = 0; p < painters.Length; p++)
                painters[p].SaveDamage();
        }

        /// <summary>Clears all damage and deletes the saved files.</summary>
        public void ResetAllDamage()
        {
            for (int p = 0; p < painters.Length; p++)
                painters[p].ResetDamage();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (Application.isPlaying) return;

            bool changed = false;

            VertexDamagePainter[] found = GetComponentsInChildren<VertexDamagePainter>(true);
            if (!PaintersMatch(found))
            {
                painters = found;
                changed = true;
            }

            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
        }

        private bool PaintersMatch(VertexDamagePainter[] found)
        {
            if (painters == null || painters.Length != found.Length) return false;

            for (int i = 0; i < found.Length; i++)
                if (painters[i] != found[i])
                    return false;

            return true;
        }
#endif
    }
}