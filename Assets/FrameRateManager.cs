using UnityEngine;

public class FrameRateManager : MonoBehaviour
{
    [SerializeField] private int targetFPS = 60;

    void Awake()
    {
        // VSync kapalý olmalý, yoksa targetFrameRate çalýþmaz
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFPS;
    }
}