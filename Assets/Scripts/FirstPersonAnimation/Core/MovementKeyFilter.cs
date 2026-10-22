using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using EasyPeasyFirstPersonController;

namespace FPAnimation
{
    /// <summary>
    /// Keeps player movement on W A S D only: every arrow-key binding of the controller's "Move" action is switched off at runtime
    /// (a binding override, the asset on disk is not modified). Arrow keys still work in menus and the inventory.
    /// Finds the actions on a PlayerInput, or on any serialized InputActionAsset / InputAction / InputActionReference of the player's input component.
    /// </summary>
    public sealed class MovementKeyFilter : MonoBehaviour
    {
        FirstPersonController done;
        float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindFirstObjectByType<MovementKeyFilter>() != null) return;
            var go = new GameObject("[Movement Key Filter]");
            DontDestroyOnLoad(go);
            go.AddComponent<MovementKeyFilter>();
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            var c = FindFirstObjectByType<FirstPersonController>();
            if (c == null || c == done) return;
            done = c;
            Apply(c);
        }

        static void Apply(FirstPersonController c)
        {
            foreach (var pi in c.GetComponentsInChildren<PlayerInput>(true))
                if (pi.actions != null) Asset(pi.actions);
            foreach (var mb in c.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                foreach (var f in mb.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object v = f.GetValue(mb);
                    if (v is InputActionAsset a) Asset(a);
                    else if (v is InputAction act) Action(act);
                    else if (v is InputActionReference r && r.action != null) Action(r.action);
                }
            }
        }

        static void Asset(InputActionAsset a)
        {
            foreach (var act in a) Action(act);
        }

        static void Action(InputAction a)
        {
            if (!a.name.ToLowerInvariant().Contains("move")) return;
            for (int i = 0; i < a.bindings.Count; i++)
            {
                var b = a.bindings[i];
                if (b.isComposite) continue;
                string p = b.effectivePath != null ? b.effectivePath.ToLowerInvariant() : "";
                if (p.Contains("<keyboard>") && p.Contains("arrow")) a.ApplyBindingOverride(i, string.Empty);
            }
        }
    }
}
