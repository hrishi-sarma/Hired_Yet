using UnityEngine;
using EasyPeasyFirstPersonController;
namespace FPAnimation
{
    /// <summary>Auto-installs and (re)attaches the rig whenever a FirstPersonController exists. No scene setup.</summary>
    public class FPAnimBootstrap : MonoBehaviour
    {
        static FPAnimBootstrap instance;
        FPAnimationSystem current;
        float nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            UnityEngine.InputSystem.InputSystem.settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            var go = new GameObject("FP Anim Bootstrap");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FPAnimBootstrap>();
        }

        void Update()
        {
            if (current != null) return;                       // Unity null check also covers destroyed rigs
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.25f;              // player is spawned at runtime, so poll
#if UNITY_2023_1_OR_NEWER
            var c = FindFirstObjectByType<FirstPersonController>();
#else
            var c = FindObjectOfType<FirstPersonController>();
#endif
            if (c != null && c.cam != null && c.characterController != null && c.playerCamera != null)
                current = FPAnimationSystem.Create(c);
        }
    }
}
