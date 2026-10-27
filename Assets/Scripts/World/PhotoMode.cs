using System.Collections;
using System.Collections.Generic;
using System.IO;
using EasyPeasyFirstPersonController;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Photo mode (F8). Freezes the world, hides every HUD canvas, and gives a free-fly camera with a few colour filters.
//   F8 / Esc : enter / leave      WASD : fly      Q / E : down / up      Shift : fast     Ctrl : slow
//   Mouse    : look               Scroll : zoom (field of view)           Left / Right arrow : filter
//   Enter / Space : save a PNG to <persistentDataPath>/Photos            H : hide / show the help text
//   P : pause / resume the world (the world is paused by default when photo mode opens)
// Dev camera: the console command "devcam" opens the same camera without the distance / zoom limits (and with a higher top speed).
// Safety: separate camera (the player camera and its hand overlay are untouched), the player controller is only paused and then
// re-enabled, time scale and HUD canvases are restored exactly, nothing runs unless F8 is pressed. Not available in menus,
// interview rooms, dialogs, the pause menu or the dev console.
public class PhotoMode : MonoBehaviour
{
    public static bool DevCam { get { return active && devMode; } }
    public static void RequestDev() { pendingDev = true; }

    /// <summary>The free-fly camera while photo mode / devcam is open, otherwise null. Weather uses it so the rain follows the freecam.</summary>
    public static Camera FreeCamera { get { return active && inst != null ? inst.cam : null; } }

    public static bool Active { get { return active || Time.frameCount == exitFrame; } }   // stays true for the exit frame so Esc doesn't also open the pause menu

    public float moveSpeed = 5f;
    public float fastMultiplier = 4f;
    public float lookSensitivity = 0.12f;
    public float maxDistanceFromPlayer = 70f;
    public float minFov = 20f, maxFov = 90f;

    static bool active, devMode, pendingDev;
    static int exitFrame = -10;
    static PhotoMode inst;

    struct Filter
    {
        public string name; public float exposure, contrast, saturation, temperature, tint, vignette;
        public Filter(string n, float e, float c, float s, float t, float ti, float v) { name = n; exposure = e; contrast = c; saturation = s; temperature = t; tint = ti; vignette = v; }
    }

    static readonly Filter[] Filters =
    {
        new Filter("Natural", 0f, 0f, 0f, 0f, 0f, 0f),
        new Filter("Vivid", 0.05f, 12f, 30f, 4f, 0f, 0.12f),
        new Filter("Warm", 0.1f, 6f, 10f, 24f, 4f, 0.22f),
        new Filter("Cool", 0f, 8f, 4f, -24f, -6f, 0.18f),
        new Filter("Noir", 0.05f, 28f, -100f, 0f, 0f, 0.38f),
        new Filter("Dreamy", 0.3f, -12f, 14f, 8f, 6f, 0.28f),
    };

    Camera cam, src;
    Transform player;
    FirstPersonController controller;
    bool controllerWasOn;
    float prevScale;
    float yaw, pitch;
    Vector3 vel;
    int filter;
    bool showHelp = true, hideUi, busy, worldPaused = true;
    public bool pauseByDefault = true;
    float toastUntil, flashUntil; string toast = "";
    readonly List<Canvas> hidden = new List<Canvas>();

    Volume volume; VolumeProfile profile;
    ColorAdjustments adj; WhiteBalance wb; Vignette vig;
    GUIStyle label, big;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("PhotoMode");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<PhotoMode>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (!active)
        {
            if (pendingDev)
            {
                if (DevConsole.Active) return;                       // wait until the console has closed and restored the time scale
                pendingDev = false;
                if (CanEnter(true)) Enter(true);
            }
            else if (kb.f8Key.wasPressedThisFrame && CanEnter(false)) Enter(false);
            return;
        }

        // leave on F8/Esc, or if something else took over
        if (kb.f8Key.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || !StillValid()) { Exit(); return; }

        if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
        if (kb.pKey.wasPressedThisFrame) SetWorldPaused(!worldPaused);
        if (kb.rightArrowKey.wasPressedThisFrame) SetFilter(filter + 1);
        if (kb.leftArrowKey.wasPressedThisFrame) SetFilter(filter - 1);
        if ((kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) && !busy) StartCoroutine(Capture());

        Fly(kb);
    }

    // ------------------------------------------------------------------ enter / exit
    static bool CanEnter(bool dev)
    {
        if (!SaveSystem.InGame || WorldLauncher.Launching || WorldLauncher.Player == null) return false;
        if (Time.timeScale <= 0f || PauseMenu.Paused || DevConsole.Active || Items.InventoryUI.IsOpen || EventPanelUI.InputLock) return false;
        if (FindAnyObjectByType<InterviewUI>() != null) return false;
        return Camera.main != null;
    }

    bool StillValid()
    {
        return SaveSystem.InGame && WorldLauncher.Player != null && !DevConsole.Active && cam != null;
    }

    void Enter(bool dev)
    {
        devMode = dev;
        src = Camera.main;
        player = WorldLauncher.Player;

        GameObject go = new GameObject("PhotoCamera");
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        cam = go.AddComponent<Camera>();
        cam.CopyFrom(src);                                    // clear flags, fov, clip planes, culling mask (the hand layer stays hidden)
        cam.targetTexture = null;
        cam.depth = src.depth + 10f;
        var data = cam.GetUniversalAdditionalCameraData();
        var sd = src.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        if (sd != null) { data.antialiasing = sd.antialiasing; data.renderShadows = sd.renderShadows; data.volumeLayerMask = sd.volumeLayerMask; }

        Vector3 e = src.transform.eulerAngles;
        yaw = e.y; pitch = e.x > 180f ? e.x - 360f : e.x;
        vel = Vector3.zero;

        // pause the player controller (re-enabled on exit) and the world
        controller = player.GetComponentInChildren<FirstPersonController>();
        controllerWasOn = controller != null && controller.enabled;
        if (controllerWasOn) controller.enabled = false;
        prevScale = Time.timeScale;
        worldPaused = pauseByDefault;
        Time.timeScale = worldPaused ? 0f : prevScale;

        // hide every HUD canvas (restored exactly on exit)
        hidden.Clear();
        Canvas[] cs = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cs.Length; i++)
            if (cs[i].isRootCanvas && cs[i].enabled) { cs[i].enabled = false; hidden.Add(cs[i]); }

        EnsureVolume();
        SetFilter(filter);
        active = true;
        showHelp = true; hideUi = false; busy = false;
    }

    void Exit()
    {
        if (!active && cam == null) return;
        active = false;
        exitFrame = Time.frameCount;
        StopAllCoroutines();

        for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].enabled = true;
        hidden.Clear();
        if (volume != null) volume.weight = 0f;
        if (controllerWasOn && controller != null) controller.enabled = true;
        controllerWasOn = false;
        Time.timeScale = prevScale > 0f ? prevScale : 1f;
        devMode = false;
        if (cam != null) Destroy(cam.gameObject);
        cam = null;
    }

    void OnDisable() { if (active || cam != null) Exit(); }

    void OnDestroy()
    {
        if (inst == this) inst = null;
        if (volume != null) Destroy(volume.gameObject);
        if (profile != null) Destroy(profile);
    }

    // ------------------------------------------------------------------ camera
    void Fly(Keyboard kb)
    {
        float dt = Time.unscaledDeltaTime;
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue() * lookSensitivity;
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -88f, 88f);
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - Mathf.Sign(scroll) * 3f, devMode ? 1f : minFov, devMode ? 150f : maxFov);
        }
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 wish = Vector3.zero;
        if (kb.wKey.isPressed) wish += Vector3.forward;
        if (kb.sKey.isPressed) wish += Vector3.back;
        if (kb.dKey.isPressed) wish += Vector3.right;
        if (kb.aKey.isPressed) wish += Vector3.left;
        Vector3 move = rot * wish;
        if (kb.eKey.isPressed) move += Vector3.up;
        if (kb.qKey.isPressed) move += Vector3.down;
        float speed = moveSpeed * (kb.leftShiftKey.isPressed ? (devMode ? fastMultiplier * 8f : fastMultiplier) : 1f) * (kb.leftCtrlKey.isPressed ? 0.25f : 1f);
        vel = Vector3.Lerp(vel, move.normalized * speed, 1f - Mathf.Exp(-10f * dt));

        Vector3 p = cam.transform.position + vel * dt;
        if (player != null && !devMode)
        {
            Vector3 off = p - player.position;
            if (off.sqrMagnitude > maxDistanceFromPlayer * maxDistanceFromPlayer) p = player.position + off.normalized * maxDistanceFromPlayer;
        }
        cam.transform.SetPositionAndRotation(p, rot);
    }

    // ------------------------------------------------------------------ filters (own tiny global volume; weight 0 outside photo mode)
    void EnsureVolume()
    {
        if (volume != null) return;
        GameObject go = new GameObject("PhotoFilterVolume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 150f;
        volume.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;
        adj = profile.Add<ColorAdjustments>(true);
        adj.postExposure.overrideState = true; adj.contrast.overrideState = true; adj.saturation.overrideState = true;
        wb = profile.Add<WhiteBalance>(true);
        wb.temperature.overrideState = true; wb.tint.overrideState = true;
        vig = profile.Add<Vignette>(true);
        vig.intensity.overrideState = true; vig.smoothness.overrideState = true; vig.smoothness.value = 0.45f;
        volume.sharedProfile = profile;
    }

    void SetFilter(int i)
    {
        filter = ((i % Filters.Length) + Filters.Length) % Filters.Length;
        if (volume == null) return;
        Filter f = Filters[filter];
        adj.postExposure.value = f.exposure; adj.contrast.value = f.contrast; adj.saturation.value = f.saturation;
        wb.temperature.value = f.temperature; wb.tint.value = f.tint;
        vig.intensity.value = f.vignette;
        volume.weight = filter == 0 ? 0f : 1f;
        Toast("Filter: " + f.name);
    }

    // ------------------------------------------------------------------ capture
    IEnumerator Capture()
    {
        busy = true;
        hideUi = true;                          // help text / toast must not be in the picture
        yield return null;                      // let one frame draw without them
        yield return new WaitForEndOfFrame();
        string dir = Path.Combine(Application.persistentDataPath, "Photos");
        string name = "Photo_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        try
        {
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name));
            toast = "Saved: Photos/" + name;
        }
        catch (System.Exception e) { toast = "Could not save photo (" + e.Message + ")"; }
        yield return null;
        hideUi = false;
        flashUntil = Time.unscaledTime + 0.12f;
        toastUntil = Time.unscaledTime + 3.5f;
        busy = false;
    }

    void SetWorldPaused(bool pause)
    {
        worldPaused = pause;
        Time.timeScale = pause ? 0f : (prevScale > 0f ? prevScale : 1f);
        Toast(pause ? "World paused" : "World running");
    }

    void Toast(string s) { toast = s; toastUntil = Time.unscaledTime + 1.8f; }

    // ------------------------------------------------------------------ overlay text
    void OnGUI()
    {
        if (!active) return;
        if (flashUntil > Time.unscaledTime)
        {
            Color c = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = c;
        }
        if (hideUi) return;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.LowerCenter, richText = true };
            label.normal.textColor = Color.white;
            big = new GUIStyle(label) { fontSize = 20, alignment = TextAnchor.UpperCenter };
        }
        if (toastUntil > Time.unscaledTime) Shadow(new Rect(0, 24, Screen.width, 40), toast, big);
        if (showHelp)
            Shadow(new Rect(0, Screen.height - 54, Screen.width, 40),
                (devMode ? "DEV CAM (no limits)" : "PHOTO MODE") + "   WASD fly   Q/E down/up   Shift fast   Scroll zoom   \u2190 \u2192 filter (" + Filters[filter].name + ")   Enter capture   P world " + (worldPaused ? "PAUSED" : "running") + "   H hide help   F8 exit", label);
    }

    static void Shadow(Rect r, string s, GUIStyle st)
    {
        Color c = st.normal.textColor;
        st.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), s, st);
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }
}
