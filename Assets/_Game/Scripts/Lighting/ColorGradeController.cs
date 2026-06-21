using UnityEngine;

[ExecuteAlways]
public class ColorGradeController : MonoBehaviour
{
   

    [Header("Exposure")]
    public float postExposure = 0f;

    [Header("Contrast")]
    [Range(-100f, 100f)] public float contrast = 0f;

    public bool garage = false;
    public float Backface = 0.2f;
    public float TireAoIntensity = 0.5f;

    static readonly int WBId = Shader.PropertyToID("_WBMatrix");
    static readonly int ExposureId = Shader.PropertyToID("_Exp");
    static readonly int ContrastId = Shader.PropertyToID("_Contrast");
    static readonly int BackfaceID = Shader.PropertyToID("_Backface");
    static readonly int TireAoIntensityID = Shader.PropertyToID("_TireAOIntensity");



    // LIN_TO_LMS / LMS_TO_LIN ve BuildWB önceki scriptteki gibi kalýyor

    void OnEnable() => Apply();
    void OnValidate() => Apply();
    void Update() => Apply();

    void Apply()
    {
        //Shader.SetGlobalMatrix(WBId, BuildWB(temperature, tint));
        Shader.SetGlobalFloat(ExposureId, Mathf.Pow(2f, postExposure));
        Shader.SetGlobalFloat(ContrastId, contrast);
        Shader.SetGlobalFloat(BackfaceID, Backface);
        Shader.SetGlobalFloat(TireAoIntensityID, TireAoIntensity);
        Shader.SetGlobalFloat("_Garage", garage ? 1f : 0f);
    }
}