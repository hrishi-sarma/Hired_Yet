using UnityEngine;
using UnityEngine.SceneManagement;

public static class StylizedHDRISkyBootstrap
{
    private const string SettingsResourceName = "StylizedHDRISkySettings";
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (!subscribed)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            subscribed = true;
        }
        EnsureSky();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSky();
    }

    private static void EnsureSky()
    {
        var existing = Object.FindFirstObjectByType<StylizedHDRISkyController>();
        if (existing != null) return;

        var settings = Resources.Load<StylizedHDRISkySettings>(SettingsResourceName);
        if (settings == null)
        {
            Debug.LogWarning("[Stylized HDRI Sky] No Resources/StylizedHDRISkySettings.asset found. Create the settings asset and place it in Assets/Resources.");
            return;
        }

        var go = new GameObject("Stylized HDRI Sky (Auto)");
        var controller = go.AddComponent<StylizedHDRISkyController>();
        controller.settings = settings;
    }
}
