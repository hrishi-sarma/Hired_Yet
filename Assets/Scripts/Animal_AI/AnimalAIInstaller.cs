using UnityEngine;

/// <summary>
/// Creates the animal system automatically at runtime. You may also add this component
/// to a scene object to expose its settings in the Inspector; it persists between scenes.
/// </summary>
[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public sealed class AnimalAIInstaller : MonoBehaviour
{
    public AnimalAISettings settings = new AnimalAISettings();

    AnimalWorldController controller;
    FlyerWorldController flyerController;
    ButterflyVisitor butterflyVisitor;
    NpcDirector activeDirector;
    static AnimalAIInstaller instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallAutomatically()
    {
        AnimalAIInstaller existing = FindFirstObjectByType<AnimalAIInstaller>();
        if (existing != null)
        {
            existing.KeepAlive();
            return;
        }

        GameObject go = new GameObject("Animal AI Runtime");
        DontDestroyOnLoad(go);
        go.AddComponent<AnimalAIInstaller>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            WorldUtil.Kill(gameObject);
            return;
        }
        instance = this;
        KeepAlive();
        if (settings == null) settings = new AnimalAISettings();
        settings.Sanitize();
    }

    void KeepAlive()
    {
        if (transform.parent != null) transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        NpcDirector director = NpcDirector.Instance;
        if (director == null || !director.Running || director.Ctx == null || director.Player == null)
        {
            StopController();
            return;
        }

        if (activeDirector == director && controller != null) return;
        StopController();
        activeDirector = director;
        controller = gameObject.AddComponent<AnimalWorldController>();
        controller.Initialize(director, settings);
        flyerController = gameObject.AddComponent<FlyerWorldController>();
        flyerController.Initialize(director, settings);
        butterflyVisitor = gameObject.AddComponent<ButterflyVisitor>();
        butterflyVisitor.Initialize(director);
    }

    void StopController()
    {
        if (controller != null)
        {
            controller.Shutdown();
            WorldUtil.Kill(controller);
            controller = null;
        }
        if (flyerController != null)
        {
            flyerController.Shutdown();
            WorldUtil.Kill(flyerController);
            flyerController = null;
        }
        if (butterflyVisitor != null) { WorldUtil.Kill(butterflyVisitor); butterflyVisitor = null; }
        activeDirector = null;
    }

    void OnDestroy()
    {
        StopController();
        if (instance == this) instance = null;
    }
}
