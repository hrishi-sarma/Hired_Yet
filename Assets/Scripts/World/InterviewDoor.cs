using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public class InterviewDoor : MonoBehaviour
{
    public string sceneName = "InterviewRoom";
    public string fallbackSceneName = "InterviewWorld";
    public string promptText = "[ PRESS E TO START INTERVIEW ]";
    public string playerTag = "Player";
    public TMP_FontAsset font;

    public Vector3 doorPoint;
    public bool hasDoorPoint;
    public float promptRadius = 4.5f;
    BoxCollider zone;
    Transform player;
    GameObject promptRoot;
    GameObject canvasGo;
    float findTimer;
    bool loading;

    void Awake()
    {
        zone = GetComponent<BoxCollider>();
        zone.isTrigger = true;
    }

    void OnDestroy()
    {
        if (canvasGo != null) WorldUtil.Kill(canvasGo);
    }

    public int triggerCount;

    public static bool IsPlayerCollider(Collider c, string tag)
    {
        if (c.GetComponentInParent<CharacterController>() != null) return true;
        if (!string.IsNullOrEmpty(tag))
        {
            try { if (c.CompareTag(tag) || c.transform.root.CompareTag(tag)) return true; }
            catch (System.Exception) { }
        }
        return false;
    }

    void MakeProximitySphere()
    {
        GameObject g = new GameObject("DoorProximitySphere");
        g.transform.SetParent(transform, true);
        g.transform.position = doorPoint + Vector3.up * 1f;
        SphereCollider sc = g.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = promptRadius;
        Rigidbody rb = g.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        g.AddComponent<DoorProximityRelay>().door = this;
    }

    bool Inside()
    {
        if (triggerCount > 0) return true;
        if (hasDoorPoint)
        {
            Vector3 d = player.position - doorPoint;
            if (d.x * d.x + d.z * d.z <= promptRadius * promptRadius && d.y > -4f && d.y < 8f) return true;
            Bounds zb = zone.bounds;
            Vector3 pp = player.position;
            return pp.x >= zb.min.x && pp.x <= zb.max.x && pp.z >= zb.min.z && pp.z <= zb.max.z && pp.y > zb.min.y - 4f && pp.y < zb.max.y + 2f;
        }
        Vector3 local = transform.InverseTransformPoint(player.position) - zone.center;
        Vector3 h = zone.size * 0.5f;
        return Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.z) <= h.z && local.y >= -h.y - 2f && local.y <= h.y + 1f;
    }

    void Update()
    {
        if (loading) return;
        // the real player always wins: during world generation the door may have latched onto a placeholder (scene player / camera) that never moves
        if (WorldLauncher.Player != null && player != WorldLauncher.Player) player = WorldLauncher.Player;
        if (player == null)
        {
            if (WorldLauncher.Player != null) player = WorldLauncher.Player;
            else
            {
                findTimer -= Time.unscaledDeltaTime;
                if (findTimer > 0f) return;
                findTimer = 0.5f;
                GameObject p = GameObject.FindGameObjectWithTag(playerTag);
                if (p != null) player = p.transform;
            }
            if (player == null)
            {
                CharacterController cc = FindFirstObjectByType<CharacterController>();
                if (cc != null) player = cc.transform;
            }
            if (player == null && Camera.main != null) player = Camera.main.transform;
            if (player == null) return;
        }

        EnsureGlowLight();
        showing = Inside();
        if (showing && promptRoot == null && !promptFailed)
        {
            try { BuildPrompt(); }
            catch (System.Exception e) { promptFailed = true; Debug.LogWarning("InterviewDoor: UI prompt failed, using IMGUI fallback. " + e.Message); }
        }
        if (promptRoot != null) promptRoot.SetActive(showing);

        if (showing && EPressed()) Enter();
    }

    bool showing;
    bool promptFailed;
    Light glowLight;
    float glowCheck;

    // the golden door light is rebuilt / re-enabled if anything switched it off or removed it (e.g. after a save is reloaded)
    void EnsureGlowLight()
    {
        if (Time.unscaledTime < glowCheck || !hasDoorPoint) return;
        glowCheck = Time.unscaledTime + 1f;
        if (glowLight == null)
        {
            Transform t = transform.Find("DoorGlowLight");
            if (t != null) glowLight = t.GetComponent<Light>();
        }
        if (glowLight == null)
        {
            var g = new GameObject("DoorGlowLight"); g.transform.SetParent(transform, true);
            g.transform.position = doorPoint + Vector3.up * 1.3f;
            glowLight = g.AddComponent<Light>();
            glowLight.type = LightType.Point; glowLight.color = new Color(1f, 0.78f, 0.1f); glowLight.range = 14f; glowLight.intensity = 4f; glowLight.shadows = LightShadows.None;
        }
        if (!glowLight.enabled) glowLight.enabled = true;
        if (!glowLight.gameObject.activeSelf) glowLight.gameObject.SetActive(true);
        if (glowLight.intensity < 4f) glowLight.intensity = 4f;     // the world lighting pass caps local lights at 1.5; the door beacon has to stay bright
    }

    static bool EPressed()
    {
        try
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) return true;
        }
        catch (System.Exception) { }
#if ENABLE_LEGACY_INPUT_MANAGER
        try { if (UnityEngine.Input.GetKeyDown(KeyCode.E)) return true; }
        catch (System.Exception) { }
#endif
        return false;
    }

    void OnGUI()
    {
        if (loading) return;
        GUIStyle st = new GUIStyle(GUI.skin.box);
        st.fontSize = 22;
        st.normal.textColor = new Color(1f, 0.82f, 0.25f);
        if (showing && promptFailed) GUI.Box(new Rect(Screen.width * 0.5f - 260f, Screen.height - 140f, 520f, 44f), promptText, st);
    }

    void Start()
    {
        if (!hasDoorPoint)
        {
            Transform marker = null;
            Transform[] ts = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++) if (ts[i].name == "DoorMarker") { marker = ts[i]; break; }
            if (marker != null) doorPoint = marker.position + marker.forward * 1.5f + Vector3.down * 1.1f;
            else doorPoint = zone.bounds.center + Vector3.down * zone.bounds.extents.y;
            hasDoorPoint = true;
        }
        MakeProximitySphere();
        Debug.Log("InterviewDoor: ready at " + doorPoint + ", radius " + promptRadius);
    }

    string errorText;
    float errorUntil;

    static bool CanLoad(string n)
    {
        return !string.IsNullOrEmpty(n) && Application.CanStreamedLevelBeLoaded(n);
    }

    string ResolveScene()
    {
        if (CanLoad(sceneName)) return sceneName;
        if (CanLoad(fallbackSceneName)) return fallbackSceneName;
        int n = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < n; i++)
        {
            string nm = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            if (nm.ToLowerInvariant().Contains("interview")) return nm;
        }
        return null;
    }

    void Enter()
    {
        string target = ResolveScene();
        if (target == null)
        {
            errorText = "Interview scene '" + sceneName + "' is not in Build Settings (File > Build Profiles > Scene List)";
            errorUntil = Time.unscaledTime + 6f;
            Debug.LogError("InterviewDoor: " + errorText);
            return;
        }
        loading = true;
        if (promptRoot != null) promptRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SaveSystem.EnterInterview(target);          // from now on this slot resumes in the interview room
        LoadingScreen.TransitionToScene(target);   // gradual darkness, then the cartoon loading screen
    }

    void BuildPrompt()
    {
        PromptStack stack = PromptStack.Create("InterviewDoorPrompt", 100, font, false, 90f);
        stack.Show(promptText);
        canvasGo = stack.gameObject;
        promptRoot = stack.gameObject;
    }

    PromptStack errStack;

    void LateUpdate()
    {
        if (loading) return;
        if (Time.unscaledTime < errorUntil)
        {
            if (errStack == null) errStack = PromptStack.Create("InterviewDoorError", 101, font, false, 190f);
            errStack.Show(errorText);
        }
        else if (errStack != null) errStack.Hide();
    }
}

public class DoorProximityRelay : MonoBehaviour
{
    public InterviewDoor door;

    void OnTriggerEnter(Collider other)
    {
        if (door != null && InterviewDoor.IsPlayerCollider(other, door.playerTag)) door.triggerCount++;
    }

    void OnTriggerExit(Collider other)
    {
        if (door != null && InterviewDoor.IsPlayerCollider(other, door.playerTag)) door.triggerCount = Mathf.Max(0, door.triggerCount - 1);
    }
}
