using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Self-installing safety net. Independent of the generator stages, so it also works when the world
/// was baked into the scene or a stage failed. Adds real colliders to city walls, road blocks, plaza
/// pieces, benches, trees, fountain and lamps, and attaches the interview door logic to orphan door markers.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldRuntimeFixer : MonoBehaviour
{
    static readonly string[] Include =
    {
        "hedge", "shrub", "lamppost", "streetlamp", "planter", "bollard",
        "hydrant", "mailbox", "trashcan", "roadclosed", "citywall", "cityborder_", "plaza_", "barrier"
    };
    static readonly string[] Exclude =
    {
        "water", "orb", "canopy", "flower", "lamphead", "lampcap", "glow", "reflector", "sign", "light", "lawncurb", "path"
    };

    readonly HashSet<int> done = new HashSet<int>();
    float next;
    float stamp;
    bool scanning;
    bool triedDoor;
    int added, fixedTriggers, doors;
    string toast;
    float toastUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<WorldRuntimeFixer>() != null) return;
        GameObject go = new GameObject("World Runtime Fixer (Auto)");
        go.AddComponent<WorldRuntimeFixer>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; stamp = Time.unscaledTime; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }

    void OnScene(Scene s, LoadSceneMode m)
    {
        done.Clear();
        triedDoor = false;      // a new world gets its own door check (this component lives across scene loads)
        stamp = Time.unscaledTime;
        next = 0f;
    }

    void Update()
    {
        if (scanning || Time.unscaledTime < next) return;
        // Dense scans while the world is being built / just after, relaxed afterwards.
        next = Time.unscaledTime + (Time.unscaledTime - stamp < 90f || WorldLauncher.Launching ? 3f : 20f);
        StartCoroutine(Scan());
    }

    static bool Has(string n, string[] list)
    {
        for (int i = 0; i < list.Length; i++) if (n.Contains(list[i])) return true;
        return false;
    }

    static string Norm(string n) { return n.ToLowerInvariant().Replace(" ", "").Replace("-", "_"); }

    static bool IsTarget(Transform t)
    {
        if (Has(Norm(t.name), Exclude)) return false;
        Transform c = t;
        for (int d = 0; d < 4 && c != null; d++, c = c.parent)
            if (Has(Norm(c.name), Include)) return true;
        return false;
    }

    IEnumerator Scan()
    {
        scanning = true;
        MeshRenderer[] rs = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        int budget = 0;
        for (int i = 0; i < rs.Length; i++)
        {
            MeshRenderer r = rs[i];
            if (++budget > 300) { budget = 0; yield return null; }
            if (r == null) continue;
            int id = r.GetInstanceID();
            if (done.Contains(id)) continue;
            Transform t = r.transform;

            if (t.name == "DoorMarker" && t.parent != null && t.GetComponentInParent<InterviewDoor>() == null)
            {
                BoxCollider z = t.parent.gameObject.AddComponent<BoxCollider>();
                z.isTrigger = true;
                z.center = new Vector3(0f, 3f, 0f);
                z.size = new Vector3(8f, 6f, 10f);
                t.parent.gameObject.AddComponent<InterviewDoor>();
                doors++;
            }
            if (!IsTarget(t)) continue;
            done.Add(id);
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f == null || f.sharedMesh == null) continue;
            Collider[] cols = r.GetComponents<Collider>();
            bool solid = false;
            for (int k = 0; k < cols.Length; k++)
            {
                if (cols[k] == null) continue;
                if (cols[k].isTrigger) { cols[k].isTrigger = false; fixedTriggers++; }
                cols[k].enabled = true;
                solid = true;
            }
            if (solid) continue;
            string nm = Norm(t.name);
            bool big = nm.Contains("cityborder_") || nm.Contains("plaza_") || f.sharedMesh.bounds.size.magnitude > 15f;
            try
            {
                MeshCollider mc = r.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = f.sharedMesh;
                mc.convex = !big && f.sharedMesh.vertexCount < 2000;
            }
            catch (System.Exception)
            {
                BoxCollider bc = r.gameObject.AddComponent<BoxCollider>();
                bc.center = f.sharedMesh.bounds.center;
                bc.size = f.sharedMesh.bounds.size;
            }
            added++;
        }
        Physics.SyncTransforms();

        string layerNote = "";
        Transform pl = WorldLauncher.Player;
        if (pl == null)
        {
            GameObject pg = null;
            try { pg = GameObject.FindGameObjectWithTag("Player"); } catch (System.Exception) { }
            if (pg != null) pl = pg.transform;
        }
        if (pl != null && Physics.GetIgnoreLayerCollision(pl.gameObject.layer, 0))
            layerNote = " | WARNING: player layer ignores Default layer (Project Settings > Physics)";

        InterviewDoor[] ds = FindObjectsByType<InterviewDoor>(FindObjectsSortMode.None);
        if (ds.Length == 0 && !triedDoor && WorldLauncher.Player != null)
        {
            triedDoor = true;
            WorldGenerator gen = FindFirstObjectByType<WorldGenerator>();
            if (gen != null && gen.Context != null)
            {
                try
                {
                    InterviewDoorStage st = gen.GetComponent<InterviewDoorStage>();
                    if (st == null) st = gen.gameObject.AddComponent<InterviewDoorStage>();
                    st.Run(gen.Context);
                    Debug.Log("World fixer: no interview door existed, created one. HasChosen=" + InterviewDoorStage.HasChosen);
                }
                catch (System.Exception e) { Debug.LogError("World fixer: door fallback failed: " + e); }
                ds = FindObjectsByType<InterviewDoor>(FindObjectsSortMode.None);
            }
        }
        toast = "World fixer v3: colliders added " + added + ", triggers fixed " + fixedTriggers + ", interview doors " + ds.Length + layerNote;
        if (added + fixedTriggers + doors > 0 || ds.Length > 0 || layerNote.Length > 0)
        {
            Debug.Log(toast);
            toastUntil = Time.unscaledTime + 8f;
        }
        scanning = false;
    }

}
