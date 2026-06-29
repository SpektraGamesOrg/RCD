using UnityEditor;
#if UNITY_EDITOR_OSX
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
#endif

namespace EditorScript
{
    /// <summary>
    /// On macOS, the native Firebase plugins (.bundle / .dylib) ship with the
    /// com.apple.quarantine attribute, which makes Gatekeeper block them from
    /// loading in the Editor ("could not verify ... is free of malware").
    /// This clears that attribute automatically once per Editor session when an
    /// Assets/Firebase folder is present. No-op on other platforms.
    /// </summary>
    [InitializeOnLoad]
    public static class FirebaseQuarantineStripper
    {
#if UNITY_EDITOR_OSX
        private const string SessionKey = "FirebaseQuarantineStripper.Ran";

        static FirebaseQuarantineStripper()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            // Defer so we never block the domain reload.
            EditorApplication.delayCall += StripQuarantine;
        }

        [MenuItem("Tools/Firebase/Strip Quarantine")]
        private static void StripQuarantine()
        {
            string firebasePath = Path.Combine(Application.dataPath, "Firebase");
            if (!Directory.Exists(firebasePath))
                return;

            SessionState.SetBool(SessionKey, true);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/xattr",
                    Arguments = $"-dr com.apple.quarantine \"{firebasePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Debug.LogError("[FirebaseQuarantineStripper] Failed to start xattr process.");
                    return;
                }

                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                    Debug.Log("[FirebaseQuarantineStripper] Cleared com.apple.quarantine from Assets/Firebase.");
                else
                    Debug.LogError($"[FirebaseQuarantineStripper] xattr exited with code {process.ExitCode}: {error}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FirebaseQuarantineStripper] Failed to strip quarantine: {e.Message}");
            }
        }
#else
        static FirebaseQuarantineStripper() { }
#endif
    }
}
