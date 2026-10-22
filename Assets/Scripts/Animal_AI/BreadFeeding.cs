using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class BreadCrumb
{
    public Transform t;
    public Vector3 from, to;
    public float amount = 1f, age, flightT, flightLen, arc;
    public float Size;
    public int claims;   // birds currently assigned to eat this crumb

    public bool Landed { get { return flightT >= 1f; } }
    public bool Gone { get { return amount <= 0.02f; } }
    public Vector3 Position { get { return t != null ? t.position : to; } }

    public void Eat(float a)
    {
        amount -= a;
        if (t != null) t.localScale = new Vector3(Size, Size * 0.7f, Size) * Mathf.Clamp(0.25f + amount * 0.75f, 0.2f, 1f);
    }
}

/// <summary>Throw breadcrumbs with Q: birds of any species fly in and eat them. No E prompt; a small hint shows only while the feeding is
/// active, the player is near where the crumbs landed and the throw cooldown is over.</summary>
public sealed class BreadFeeding : MonoBehaviour
{
    static BreadFeeding inst;
    static readonly string[] Tints = { "#D9B77A", "#E8CC93", "#C79B5A", "#F0DCAB" };

    FlyerWorldController ctl;
    NpcDirector director;
    Transform root;
    readonly List<BreadCrumb> crumbs = new List<BreadCrumb>();
    readonly List<FlyerAgent> scan = new List<FlyerAgent>(32);
    Vector3 throwSpot;
    PromptStack hintStack;
    string hintShown = "";
    const float HintRadius = 12f, EndRadius = 30f;
    Vector3 flockCenter;
    int flockCount;
    bool active, agitated;
    int throwsThisEvent;
    float scanTimer, nextThrow, idleTime, agitatedTime, startleTimer, startledTotal;
    System.Random rng = new System.Random(4417);

    public static bool Active { get { return inst != null && inst.active; } }
    public static bool HasCrumbs { get { return inst != null && inst.crumbs.Count > 0; } }
    public static bool Calm { get { return inst != null && inst.active && !inst.agitated; } }

    public static BreadFeeding Create(FlyerWorldController controller, NpcDirector owner, Transform parent)
    {
        GameObject go = new GameObject("Bread Feeding");
        go.transform.SetParent(parent, false);
        BreadFeeding b = go.AddComponent<BreadFeeding>();
        b.ctl = controller;
        b.director = owner;
        b.root = go.transform;
        inst = b;
        return b;
    }

    public static float AlertFor(float alert, bool sprinting)
    {
        return Calm ? Mathf.Min(alert, sprinting ? 3.5f : 1.4f) : alert;
    }

    public static void Scared()
    {
        if (inst != null && inst.active && !inst.agitated) inst.agitated = true;
    }

    public const int MaxEatersPerCrumb = 1;      // preferred: one bird per crumb
    public const int MaxSharedEaters = 2;        // fallback when every crumb is already taken
    public const int MaxSummonedBirds = 12;   // hard cap on birds flown in for feeding at once

    public static BreadCrumb Nearest(Vector3 p, float maxDist, BreadCrumb ignore = null, BreadCrumb keep = null)
    {
        if (inst == null || !inst.active) return null;
        // Pass 1: free crumbs only. Pass 2: share a crumb so no bird is left standing while food remains.
        for (int pass = 0; pass < 2; pass++)
        {
            int limit = pass == 0 ? MaxEatersPerCrumb : MaxSharedEaters;
            BreadCrumb best = null;
            float bd = maxDist * maxDist;
            for (int i = 0; i < inst.crumbs.Count; i++)
            {
                BreadCrumb c = inst.crumbs[i];
                if (!c.Landed || c.Gone || c == ignore) continue;
                if (c.claims >= limit && c != keep) continue;
                Vector3 d = c.Position - p; d.y = 0f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = c; }
            }
            if (best != null) return best;
        }
        return null;
    }

    public static bool TryAttract(Vector3 p, out Vector3 spot)
    {
        spot = Vector3.zero;
        if (inst == null || !inst.active || inst.agitated) return false;
        BreadCrumb c = Nearest(p, 32f);
        if (c == null) return false;
        Vector3 o = new Vector3((float)inst.rng.NextDouble() - 0.5f, 0f, (float)inst.rng.NextDouble() - 0.5f) * 1.6f;
        spot = c.Position + o;
        return true;
    }

    void TryThrow()
    {
        InteractionSystem sys = InteractionSystem.Instance;
        if (sys != null && (sys.Suppressed || !string.IsNullOrEmpty(sys.OverridePrompt))) return;
        if (Time.time < nextThrow) return;
        nextThrow = Time.time + 3.4f;
        Vector3 pp = director.Player.position;
        Camera cam = Camera.main;
        Vector3 dir = cam != null ? cam.transform.forward : director.Player.forward;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : director.Player.forward;
        Vector3 target = pp + dir * 3.8f;
        bool calming = agitated;
        FPAnimation.FPEvents.RaiseFeedStarted(target, delegate { Release(target, calming); });
    }

    void Release(Vector3 target, bool calming)
    {
        if (director == null || director.Player == null) return;
        Vector3 hand = FPAnimation.FPEvents.HandR != null ? FPAnimation.FPEvents.HandR.position : director.Player.position + Vector3.up * 1.2f;
        if (active) throwsThisEvent++; else throwsThisEvent = 0;
        int n = calming ? 8 : 10;
        for (int i = 0; i < n; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble()) * 1.3f;
            float x = target.x + Mathf.Cos(a) * r, z = target.z + Mathf.Sin(a) * r;
            float y = director.GroundY(x, z, director.Player.position.y + 2f);
            AddCrumb(hand, new Vector3(x, y + 0.02f, z), i * 0.04f);
        }
        active = true;
        throwSpot = target;
        agitated = false;
        agitatedTime = 0f;
        idleTime = 0f;
        // more food thrown during the same feeding -> more birds drop in
        ctl.SummonBirds(target, (flockCount < 4 ? 3 : 2) + Mathf.Min(throwsThisEvent * 2, 6));
        startleTimer = 7f + (float)rng.NextDouble() * 6f;
    }

    void AddCrumb(Vector3 from, Vector3 to, float delay)
    {
        GameObject g = PrimitiveFactory.Create((rng.Next(2) == 0) ? PrimitiveKind.Sphere : PrimitiveKind.Cube, root);
        g.name = "Crumb";
        Collider c = g.GetComponent<Collider>();
        if (c != null) WorldUtil.Kill(c);
        g.layer = 2;
        Renderer rd = g.GetComponent<Renderer>();
        Color col;
        ColorUtility.TryParseHtmlString(Tints[rng.Next(Tints.Length)], out col);
        if (rd != null) rd.sharedMaterial = MaterialFactory.Get(col);
        BreadCrumb b = new BreadCrumb { t = g.transform, from = from, to = to, flightLen = 0.45f + (to - from).magnitude * 0.05f, arc = 0.6f };
        b.Size = 0.035f + (float)rng.NextDouble() * 0.03f;
        b.flightT = -delay / b.flightLen;
        g.transform.position = from;
        g.transform.localScale = new Vector3(b.Size, b.Size * 0.7f, b.Size);
        g.transform.rotation = Random.rotation;
        crumbs.Add(b);
    }

    void Update()
    {
        if (director == null || !director.Running || director.Player == null) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.qKey.wasPressedThisFrame) TryThrow();
        UpdateHint();
        for (int i = crumbs.Count - 1; i >= 0; i--)
        {
            BreadCrumb c = crumbs[i];
            if (c.t == null) { crumbs.RemoveAt(i); continue; }
            if (!c.Landed)
            {
                c.flightT += Time.deltaTime / c.flightLen;
                float k = Mathf.Clamp01(c.flightT);
                Vector3 pos = Vector3.Lerp(c.from, c.to, k);
                pos.y += Mathf.Sin(k * Mathf.PI) * c.arc;
                c.t.position = pos;
                c.t.Rotate(300f * Time.deltaTime, 200f * Time.deltaTime, 0f);
                continue;
            }
            c.age += Time.deltaTime;
            if (c.age > 50f) c.Eat(Time.deltaTime * 0.5f);
            if (c.Gone) { WorldUtil.Kill(c.t.gameObject); crumbs.RemoveAt(i); }
        }

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.4f;
            ctl.GroundBirds(director.Player.position, 20f, scan);
            flockCount = scan.Count;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < scan.Count; i++) sum += scan[i].transform.position;
            if (scan.Count > 0) flockCenter = sum / scan.Count;
        }

        if (!active) return;
        bool anyCrumb = false;
        for (int i = 0; i < crumbs.Count; i++) if (!crumbs[i].Gone) { anyCrumb = true; break; }
        idleTime = anyCrumb ? 0f : idleTime + Time.deltaTime;

        if (agitated) agitatedTime += Time.deltaTime;
        else if (anyCrumb) startleTimer = Mathf.Max(startleTimer, 4f);   // no random startles while there is food
        else if (flockCount >= 3 && startledTotal < 2f)
        {
            startleTimer -= Time.deltaTime;
            if (startleTimer <= 0f)
            {
                startleTimer = 8f + (float)rng.NextDouble() * 6f;
                if (rng.NextDouble() < 0.45)
                {
                    startledTotal += 1f;
                    ctl.Startle(flockCenter, 12f, director.Player.position);
                    agitated = true;
                }
            }
        }

        Vector3 away = director.Player.position - throwSpot; away.y = 0f;
        if (idleTime > 6f || agitatedTime > 22f || (flockCount == 0 && !anyCrumb && idleTime > 2f) || away.magnitude > EndRadius) EndEvent();
    }

    void EndEvent()
    {
        active = false;
        agitated = false;
        agitatedTime = 0f;
        idleTime = 0f;
        startledTotal = 0f;
        throwsThisEvent = 0;
        nextThrow = Mathf.Max(nextThrow, Time.time + 10f);
    }

    // hint: only while the event is running, near the spot where crumbs were thrown, and not on cooldown
    void UpdateHint()
    {
        string text = "";
        if (active && Time.time >= nextThrow)
        {
            InteractionSystem sys = InteractionSystem.Instance;
            Vector3 d = director.Player.position - throwSpot; d.y = 0f;
            if ((sys == null || !sys.Suppressed) && d.magnitude <= HintRadius)
                text = agitated ? "[ Q ] Throw crumbs to calm the flock" : "[ Q ] Throw more crumbs";
        }
        if (text == hintShown) return;
        hintShown = text;
        if (hintStack == null)
        {
            if (string.IsNullOrEmpty(text)) return;
            InteractionSystem sys = InteractionSystem.Instance;
            hintStack = PromptStack.Create("BreadHint", 99, sys != null ? sys.font : null, false, 150f);
        }
        hintStack.Show(text);
    }

    void OnDestroy()
    {
        if (hintStack != null) WorldUtil.Kill(hintStack.gameObject);
        if (inst == this) inst = null;
    }
}
