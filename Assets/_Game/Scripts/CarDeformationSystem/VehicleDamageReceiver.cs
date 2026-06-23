using UnityEngine;

public class VehicleDamageReceiver : MonoBehaviour
{
    [Header("Etki")]
    [Tooltip("Tam hasar hýzý (relativeVelocity.magnitude)")]
    public float maxImpact = 25f;

    [Tooltip("Bu hýzýn altýndaki temaslar yoksayýlýr")]
    public float minImpact = 3f;

    VertexDamagePainter[] painters;
    ContactPoint[] contacts = new ContactPoint[16]; // tekrar kullanýlýr, sýfýr GC
    bool dirty = false; // bu frame'de hasar oldu mu

    void Awake()
    {
        painters = GetComponentsInChildren<VertexDamagePainter>();
    }

    void OnCollisionEnter(Collision col)
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

        dirty = true; // kaydedilmesi gerekiyor
    }

    void LateUpdate()
    {
        // ayný frame'deki tüm çarpýþmalar iþlendikten sonra tek seferde kaydet
        if (dirty)
        {
            SaveAllDamage();
            dirty = false;
        }
    }

    public void SaveAllDamage()
    {
        for (int p = 0; p < painters.Length; p++)
            painters[p].SaveDamage();
    }

    public void ResetAllDamage()
    {
        for (int p = 0; p < painters.Length; p++)
            painters[p].ResetDamage();
    }
}