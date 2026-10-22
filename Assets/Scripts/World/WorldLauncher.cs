using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldLauncher
{
    public static string WorldSceneName = "worldtest1";
    public static bool Launching;
    public static string FallbackScene = "";
    public static Transform Player;

    public static void Begin(string fallbackScene)
    {
        if (Launching) return;
        FallbackScene = fallbackScene;
        if (!Application.CanStreamedLevelBeLoaded(WorldSceneName))
        {
            Debug.LogError("WorldLauncher: scene '" + WorldSceneName + "' is not in Build Settings.");
            if (!string.IsNullOrEmpty(fallbackScene)) SceneManager.LoadScene(fallbackScene);
            return;
        }
        Launching = true;
        Player = null;
        LoadingScreen.Kind = LoadingScreen.LoadKind.World;
        ScreenFade.ToBlack(0.5f, () =>   // fade out first, then the loading screen comes up out of the dark
        {
            LoadingScreen screen = LoadingScreen.Show();
            screen.SetProgress(0f, "loading scene");
            screen.StartCoroutine(LoadRoutine(screen));
        });
    }

    static IEnumerator LoadRoutine(LoadingScreen screen)
    {
        yield return null;
        AsyncOperation op = SceneManager.LoadSceneAsync(WorldSceneName, LoadSceneMode.Single);
        while (!op.isDone)
        {
            screen.SetProgress(op.progress * 0.1f, "loading scene");
            yield return null;
        }
        screen.SetProgress(0.1f, "starting generator");
        yield return null;
        yield return null;
        if (Object.FindFirstObjectByType<WorldBootstrap>() == null)
            Fail("no active WorldBootstrap found in scene '" + WorldSceneName + "'. Add it to the World Generator object.");
    }

    public static void Fail(string message)
    {
        Launching = false;
        Debug.LogError("WorldLauncher: " + message);
        LoadingScreen screen = LoadingScreen.Show();
        screen.StartCoroutine(FailRoutine(screen, message));
    }

    static IEnumerator FailRoutine(LoadingScreen screen, string message)
    {
        screen.ShowError(message);
        yield return new WaitForSecondsRealtime(3f);
        if (!string.IsNullOrEmpty(FallbackScene) && Application.CanStreamedLevelBeLoaded(FallbackScene))
            SceneManager.LoadScene(FallbackScene);
        screen.Finish();
    }
}