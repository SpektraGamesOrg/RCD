using UnityEngine;
using UnityEditor;

public class GrassPlacerEditor : EditorWindow
{
    public GameObject grassPrefab;  // Çimen prefab'ý
    public MeshFilter targetMesh;   // Çimenlerin yerleþtirileceði mesh
    public float brushSize = 1.0f;  // Fýrça boyutu
    public float offsetFromSurface = 0.1f; // Yüzeyden yükseklik offseti
    public int grassPerClick = 10;  // Her týklamada eklenen çimen sayýsý
    public Vector2 scaleRange = new Vector2(0.8f, 1.2f); // Çimen ölçek aralýðý
    public bool randomRotation = true; // Rastgele dönüþ aktif mi?

    private bool isPainting = false; // Fýrça aktif mi?

    [MenuItem("Tools/Art/Grass Placer Brush")]
    public static void ShowWindow()
    {
        GetWindow<GrassPlacerEditor>("Grass Placer Brush");
    }

    private void OnGUI()
    {
        GUILayout.Label("Grass Placement Brush Tool", EditorStyles.boldLabel);

        grassPrefab = (GameObject)EditorGUILayout.ObjectField("Grass Prefab", grassPrefab, typeof(GameObject), false);
        targetMesh = (MeshFilter)EditorGUILayout.ObjectField("Target Mesh", targetMesh, typeof(MeshFilter), true);
        brushSize = EditorGUILayout.FloatField("Brush Size", brushSize);
        offsetFromSurface = EditorGUILayout.FloatField("Offset From Surface", offsetFromSurface);
        grassPerClick = EditorGUILayout.IntField("Grass Per Click", grassPerClick);
        scaleRange = EditorGUILayout.Vector2Field("Scale Range", scaleRange);
        randomRotation = EditorGUILayout.Toggle("Random Rotation", randomRotation);

        if (GUILayout.Button("Enable Brush"))
        {
            if (grassPrefab == null || targetMesh == null)
            {
                Debug.LogError("Grass Prefab veya Target Mesh eksik!");
                return;
            }

            isPainting = true;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        if (GUILayout.Button("Disable Brush"))
        {
            isPainting = false;
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPainting || targetMesh == null || grassPrefab == null)
            return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject == targetMesh.gameObject)
            {
                // Fýrça görselleþtirme
                Handles.color = new Color(0, 1, 0, 0.2f);
                Handles.DrawSolidDisc(hit.point, hit.normal, brushSize);
                Handles.color = Color.green;
                Handles.DrawWireDisc(hit.point, hit.normal, brushSize);

                SceneView.RepaintAll();

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (e.shift)
                    {
                        // Shift basýlýyken sil
                        RemoveGrass(hit.point);
                    }
                    else
                    {
                        // Normal týklamada çimen ekle
                        PlaceGrass(hit.point, hit.normal);
                    }
                    e.Use();
                }
            }
        }
    }

    private void PlaceGrass(Vector3 center, Vector3 normal)
    {
        for (int i = 0; i < grassPerClick; i++)
        {
            // Fýrça alanýnda rastgele bir nokta
            Vector3 randomPoint = center + Random.insideUnitSphere * brushSize;
            randomPoint.y = center.y; // Yüzeyde tut

            // Çimen Prefab'ini oluþtur
            GameObject grass = (GameObject)PrefabUtility.InstantiatePrefab(grassPrefab);

            // Random Scale
            float randomScale = Random.Range(scaleRange.x, scaleRange.y);
            grass.transform.localScale = Vector3.one * randomScale;

            // Random Rotation
            if (randomRotation)
            {
                float randomYRotation = Random.Range(0f, 360f);
                grass.transform.rotation = Quaternion.Euler(0, randomYRotation, 0);
            }

            // Pozisyon ve yön ayarý
            grass.transform.position = randomPoint + normal * offsetFromSurface;
            grass.transform.up = normal; // Yüzeye dik

            // Parent ayarla
            grass.transform.SetParent(targetMesh.transform);

            // Undo kaydý
            Undo.RegisterCreatedObjectUndo(grass, "Add Grass");
        }

        Debug.Log($"{grassPerClick} çimen eklendi.");
    }

    private void RemoveGrass(Vector3 center)
    {
        // Fýrça alanýndaki tüm çimenleri bul
        Transform[] children = targetMesh.transform.GetComponentsInChildren<Transform>();

        // Silinecek çimenlerin listesi
        var objectsToRemove = new System.Collections.Generic.List<GameObject>();

        foreach (Transform child in children)
        {
            if (child == targetMesh.transform) continue; // Ana objeyi atla

            float distance = Vector3.Distance(child.position, center);
            if (distance <= brushSize)
            {
                objectsToRemove.Add(child.gameObject);
            }
        }

        // Tüm çimenleri tek iþlemle sil
        if (objectsToRemove.Count > 0)
        {
            Undo.RecordObject(targetMesh, "Remove Grass"); // Undo için mesh'i kaydet
            foreach (GameObject obj in objectsToRemove)
            {
                Undo.DestroyObjectImmediate(obj);
            }

            Debug.Log($"Fýrça alanýndaki {objectsToRemove.Count} çimen silindi.");
        }
        else
        {
            Debug.Log("Fýrça alanýnda silinecek çimen bulunamadý.");
        }
    }


    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }
}
