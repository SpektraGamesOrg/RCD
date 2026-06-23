using System.IO;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class VertexDamagePainter : MonoBehaviour
{
    [Header("Boyama")]
    [Tooltip("Local uzayda darbe yarýçapý")]
    public float radius = 0.4f;

    [Tooltip("Açýk: hasar birikir. Kapalý: sadece en sert darbe kalýr")]
    public bool accumulate = true;

    [Header("Kayýt")]
    [Tooltip("Bu panel için BENZERSÝZ kayýt adý (her panelde farklý olmalý, örn. car01_body)")]
    public string saveKey = "panel";

    Mesh mesh;
    Vector3[] verts;
    Color[] colors;
    float invScale;

    string SavePath => Path.Combine(Application.persistentDataPath, $"dmg_{saveKey}.bytes");

    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh; // runtime kopyasý, asset bozulmaz
        verts = mesh.vertices;                  // local uzayda
        colors = new Color[verts.Length];       // hepsi siyah = hasar yok (R=0)
        mesh.colors = colors;

        // uniform scale telafisi
        invScale = 1f / Mathf.Max(0.0001f, transform.lossyScale.x);

        LoadDamage(); // kayýtlý hasarý geri yükle
    }

    // worldPoint = dünya uzayýndaki temas noktasý
    public void ApplyDamage(Vector3 worldPoint, float amount)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        float r = radius * invScale;
        float sqrR = r * r;
        bool changed = false;

        for (int i = 0; i < verts.Length; i++)
        {
            float sqrD = (verts[i] - local).sqrMagnitude; // karekök yok
            if (sqrD > sqrR) continue;

            float d = Mathf.Sqrt(sqrD);                   // sadece içeridekiler için
            float add = amount * (1f - d / r);            // merkeze yakýn = güçlü

            float newVal = accumulate
                ? Mathf.Clamp01(colors[i].r + add)
                : Mathf.Max(colors[i].r, add);

            if (newVal != colors[i].r)
            {
                colors[i].r = newVal;
                changed = true;
            }
        }

        if (changed) mesh.colors = colors;
    }

    // sadece R kanalýný diske yaz (4 byte * vertex sayýsý)
    public void SaveDamage()
    {
        using (var fs = new FileStream(SavePath, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(colors.Length);
            for (int i = 0; i < colors.Length; i++)
                bw.Write(colors[i].r);
        }
    }

    public void LoadDamage()
    {
        if (!File.Exists(SavePath)) return;

        using (var fs = new FileStream(SavePath, FileMode.Open))
        using (var br = new BinaryReader(fs))
        {
            int count = br.ReadInt32();
            // vertex sayýsý uyuþmuyorsa mesh deðiþmiþ demektir, yükleme
            if (count != colors.Length) return;

            for (int i = 0; i < count; i++)
                colors[i].r = br.ReadSingle();
        }
        mesh.colors = colors;
    }

    // hasarý sýfýrla ve kaydý sil
    public void ResetDamage()
    {
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.black;
        mesh.colors = colors;

        if (File.Exists(SavePath)) File.Delete(SavePath);
    }
}