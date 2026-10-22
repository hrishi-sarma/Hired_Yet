using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>A piece of text on the paper that is "written" by the pen, one character at a time.</summary>
public class WriteField
{
    public Text display, hint;
    public Func<string> get;
    public bool centered;
    public string written = "";
    internal string lastW, lastT;
}

/// <summary>The player's right hand holding a pencil, top-down over the paper. Follows the mouse; whenever a field gets text it
/// travels to the field, scribbles over the paper and finalizes each letter.</summary>
public class ResumePenHand : MonoBehaviour
{
    Material skinMat, sleeveMat;

    /// <summary>Recolour both hands + sleeves (photo-ID character choice).</summary>
    public void SetColors(Color skin, Color sleeve)
    {
        if (skinMat != null) { skinMat.color = skin; skinMat.mainTexture = DeskKit.Soft(skin, 0.05f); }
        if (sleeveMat != null) { sleeveMat.color = Color.white; sleeveMat.mainTexture = DeskKit.Cloth(sleeve); }
    }

    Camera cam; Canvas canvas; float paperY;
    readonly List<WriteField> fields = new List<WriteField>();
    WriteField active; float charTimer, idleHold, poke, tickS; bool tickMode; Action tickCb; Vector3 pokePos;
    Transform tipRoot, pivot, shadow, shadowL; TrailRenderer trail; Material shadowMat;
    HandRig rightHand, leftHand; Vector3 lampPos = new Vector3(-0.17f, 0.22f, 0.10f);
    Vector3 pos, vel, want; float lift = 0.02f, liftVel, pitch = 44f;
    // direction from the pencil tip towards the hand (screen: right + down, so the arm comes in from the bottom like the player's POV)
    readonly Vector3 yawDir = new Vector3(0.28f, 0f, -0.96f).normalized;
    bool started;
    public bool locked;

    public void Init(Camera c, Canvas cv, float paperTopY, Transform parent, Vector3 lamp)
    {
        cam = c; canvas = cv; paperY = paperTopY; lampPos = lamp;
        Build(parent);
        pos = want = new Vector3(0.0f, paperY, 0.03f);
    }

    public void SetCanvas(Canvas c) { canvas = c; }

    public void Add(WriteField f) { fields.Add(f); }

    /// <summary>Instantly finish all pending handwriting (used right before the form is photographed).</summary>
    public void FlushAll()
    {
        for (int i = 0; i < fields.Count; i++) { fields[i].written = fields[i].get() ?? ""; fields[i].lastW = null; Refresh(fields[i]); }
        active = null;
    }

    public void Poke(RectTransform r)
    {
        if (r == null) return;
        pokePos = r.TransformPoint(r.rect.center); poke = 0.5f; tickMode = false; tickCb = null;
    }

    public void PokeTick(RectTransform r, Action done)
    {
        if (r == null) return;
        pokePos = r.TransformPoint(r.rect.center); tickS = r.rect.width * r.lossyScale.x * 0.5f;
        tickMode = true; tickCb = done; poke = 1.3f;
    }

    // ---------- build ----------
    void Build(Transform parent)
    {
        tipRoot = new GameObject("Pencil Tip").transform; tipRoot.SetParent(parent, false);
        pivot = new GameObject("Pencil Pivot").transform; pivot.SetParent(tipRoot, false);

        // pencil along +Z (tip at origin)
        Material wood = DeskKit.Mat(new Color(0.96f, 0.80f, 0.55f), null, 0.1f);
        Material lead = DeskKit.Mat(new Color(0.18f, 0.18f, 0.22f), null, 0.3f);
        Material body = DeskKit.Mat(Color.white, DeskKit.Stripes(new Color(1f, 0.80f, 0.14f), 6f, 0.2f), 0.35f);
        Material band = DeskKit.Mat(new Color(0.82f, 0.84f, 0.9f), null, 0.6f);
        Material eraser = DeskKit.Mat(new Color(1f, 0.55f, 0.68f), null, 0.15f);
        DeskKit.ConePart(pivot, lead, 0f, 0.004f, 0.0011f);
        DeskKit.ConePart(pivot, wood, 0f, 0.016f, 0.0039f);
        DeskKit.Rod(pivot, body, 0.016f, 0.155f, 0.0039f);
        DeskKit.Rod(pivot, band, 0.155f, 0.163f, 0.0042f);
        DeskKit.Rod(pivot, eraser, 0.163f, 0.173f, 0.0037f);

        // two anatomical hands (posed every frame): right grips the pencil, left rests on the table
        Color Skin = ResumeSession.PlayerSkin, Sleeve = ResumeSession.PlayerOutfit;
        Material skin = skinMat = DeskKit.Mat(Skin, DeskKit.Soft(Skin, 0.05f), 0.15f);
        Material sleeve = sleeveMat = DeskKit.Mat(Color.white, DeskKit.Cloth(Sleeve), 0.08f);
        rightHand = new HandRig(parent, skin, sleeve);
        leftHand = new HandRig(parent, skin, sleeve);

        // scribble trail (graphite)
        var tg = new GameObject("Scribble"); tg.transform.SetParent(tipRoot, false); tg.transform.localPosition = new Vector3(0, 0.0003f, 0);
        trail = tg.AddComponent<TrailRenderer>();
        trail.time = 0.55f; trail.minVertexDistance = 0.0006f; trail.widthMultiplier = 0.0013f; trail.numCapVertices = 2; trail.numCornerVertices = 2;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
        trail.sharedMaterial = DeskKit.Sprite(null, Color.white, 3200);
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(new Color(0.16f, 0.16f, 0.2f), 0f), new GradientColorKey(new Color(0.22f, 0.22f, 0.28f), 1f) },
                     new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad; trail.emitting = false;

        // soft contact shadow on the paper (the UI sheet can't receive real shadows)
        shadowMat = DeskKit.Sprite(DeskKit.Blob(), new Color(0f, 0f, 0f, 0.28f), 3100);
        shadow = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        WorldUtil.Kill(shadow.GetComponent<Collider>());
        shadow.name = "Hand Shadow"; shadow.SetParent(parent, false);
        var sr = shadow.GetComponent<Renderer>(); sr.sharedMaterial = shadowMat; sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
        shadow.rotation = Quaternion.Euler(90f, 0f, 0f);
        shadowL = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        WorldUtil.Kill(shadowL.GetComponent<Collider>());
        shadowL.name = "Left Hand Shadow"; shadowL.SetParent(parent, false);
        var sl = shadowL.GetComponent<Renderer>(); sl.sharedMaterial = shadowMat; sl.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sl.receiveShadows = false;
    }

    // ---------- text helpers ----------
    static string Common(string a, string b)
    {
        int n = Mathf.Min(a.Length, b.Length), i = 0;
        while (i < n && a[i] == b[i]) i++;
        return a.Substring(0, i);
    }

    float Width(WriteField f, string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        var set = f.display.GetGenerationSettings(Vector2.zero);
        return f.display.cachedTextGenerator.GetPreferredWidth(s, set) / f.display.pixelsPerUnit;
    }

    void Refresh(WriteField f)
    {
        string t = f.get() ?? "";
        if (!t.StartsWith(f.written, StringComparison.Ordinal)) f.written = Common(t, f.written);
        if (f.lastW == f.written && f.lastT == t) return;
        f.lastW = f.written; f.lastT = t;
        string rest = t.Substring(f.written.Length);
        f.display.text = f.written + (rest.Length > 0 ? "<color=#00000000>" + rest + "</color>" : "");
        if (f.hint != null) f.hint.enabled = t.Length == 0;
    }

    bool Pending(WriteField f) { string t = f.get() ?? ""; return f.written.Length < t.Length && t.StartsWith(f.written, StringComparison.Ordinal); }

    Vector3 Caret(WriteField f)
    {
        var c = new Vector3[4]; f.display.rectTransform.GetWorldCorners(c);
        Vector3 left = (c[0] + c[1]) * 0.5f, right = (c[2] + c[3]) * 0.5f, center = (left + right) * 0.5f;
        float s = canvas.transform.lossyScale.x;
        string t = f.get() ?? "";
        float part = Width(f, f.written), full = Width(f, t);
        Vector3 origin = f.centered ? center : left;
        float off = (f.centered ? -full * 0.5f : 0f) + part;
        Vector3 p = origin + canvas.transform.right * off * s - canvas.transform.up * (4f * s);
        p.y = paperY; return p;
    }

    void AppendChar(WriteField f)
    {
        string t = f.get() ?? "";
        if (f.written.Length < t.Length) { f.written += t[f.written.Length]; Refresh(f); }
    }

    /// <summary>Moves the real mouse cursor to where the right hand rests (locked pose), so the hand spawns un-moved on the right.</summary>
    public void WarpToRest()
    {
#if ENABLE_INPUT_SYSTEM
        if (cam == null || Mouse.current == null) return;
        Vector3 sp = cam.WorldToScreenPoint(new Vector3(0.06f, paperY, -0.1f));
        Vector2 target = new Vector2(Mathf.Clamp(sp.x, 0f, Screen.width - 1f), Mathf.Clamp(sp.y, 0f, Screen.height - 1f));
        Mouse.current.WarpCursorPosition(target);
        UnityEngine.InputSystem.LowLevel.InputState.Change(Mouse.current.position, target);
#endif
    }

    // ---------- input ----------
    static Vector2 MousePos()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#else
        return Input.mousePosition;
#endif
    }

    static bool Pressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    Vector3 MousePoint()
    {
        Ray r = cam.ScreenPointToRay(MousePos());
        float e;
        if (new Plane(Vector3.up, new Vector3(0f, paperY, 0f)).Raycast(r, out e)) return r.GetPoint(e);
        return want;
    }

    // ---------- per frame ----------
    void Update()
    {
        if (cam == null || canvas == null) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        for (int i = 0; i < fields.Count; i++) Refresh(fields[i]);

        if (active != null && !Pending(active)) { active = null; idleHold = 0.35f; }
        if (active == null)
            for (int i = 0; i < fields.Count; i++)
                if (Pending(fields[i])) { active = fields[i]; charTimer = 0f; break; }

        bool writing = false; float amp = 0f, wantLift;
        bool pressed = Pressed();
        if (active != null)
        {
            want = Caret(active); writing = true; amp = 0.0024f;
            int backlog = (active.get() ?? "").Length - active.written.Length;
            charTimer += dt;
            float per = backlog > 8 ? 0.03f : (backlog > 3 ? 0.05f : 0.075f);
            if (charTimer >= per) { charTimer = 0f; AppendChar(active); }
            wantLift = (pos - want).magnitude > 0.012f ? 0.014f : 0.0006f;
        }
        else if (poke > 0f)
        {
            poke -= dt; writing = true;
            if (tickMode)
            {
                float u = Mathf.Clamp01((1.3f - poke - 0.35f) / 0.7f);
                Vector2 pa = new Vector2(-0.7f, 0.05f), pb = new Vector2(-0.2f, -0.7f), pc = new Vector2(0.8f, 0.8f);
                Vector2 q = u < 0.3f ? Vector2.Lerp(pa, pb, u / 0.3f) : Vector2.Lerp(pb, pc, (u - 0.3f) / 0.7f);
                want = pokePos + (canvas.transform.right * q.x + canvas.transform.up * q.y) * tickS;
                amp = 0.0004f;
                if (tickCb != null && u >= 0.5f) { var cb = tickCb; tickCb = null; cb(); }
            }
            else { want = pokePos; amp = 0.003f; }
            wantLift = (pos - want).magnitude > 0.012f ? 0.014f : 0.0006f;
        }
        else
        {
            if (locked) { want = new Vector3(0.06f, paperY, -0.1f); wantLift = 0.016f; }
            else
            {
                if (idleHold > 0f) idleHold -= dt; else want = MousePoint();
                wantLift = pressed ? 0.002f : 0.016f;
            }
        }

        Vector3 flatWant = new Vector3(want.x, paperY, want.z);
        pos = Vector3.SmoothDamp(pos, flatWant, ref vel, writing ? 0.045f : 0.06f, 100f, dt);
        lift = Mathf.SmoothDamp(lift, wantLift, ref liftVel, 0.06f, 10f, dt);

        Vector3 tip = pos;
        if (writing && lift < 0.003f)
        {
            float t = Time.unscaledTime;
            tip += canvas.transform.right * Mathf.Sin(t * 58f) * amp + canvas.transform.up * (Mathf.PerlinNoise(t * 30f, 1.3f) - 0.5f) * 2f * amp * 0.8f;
        }
        tip.y = paperY + 0.0008f + lift;
        tipRoot.position = tip; tipRoot.rotation = Quaternion.LookRotation(yawDir, Vector3.up);
        pitch = Mathf.Lerp(pitch, writing ? 34f : (pressed ? 36f : 44f), dt * 8f);
        pivot.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
        trail.emitting = writing && lift < 0.003f;
        started = true;
    }

    void LateUpdate()
    {
        if (!started) return;
        Vector3 up = Vector3.up;
        Vector3 T = tipRoot.position, p = pivot.forward;

        Vector3 hf = (-p + up * 0.3f).normalized;
        Vector3 hr = Vector3.Cross(up, hf).normalized;
        Vector3 hu = Vector3.Cross(hf, hr).normalized;
        Vector3 palmR = T + p * 0.095f + hr * 0.008f + hu * 0.03f;
        Vector3 armR = (new Vector3(0.12f, 0f, -0.99f).normalized + up * 0.18f).normalized;
        rightHand.Pose(palmR - hf * 0.042f, hf, -1f, armR);

        Vector3 fwdL = new Vector3(0.22f, 0f, 0.97f).normalized;
        float breathe = Mathf.Sin(Time.unscaledTime * 1.4f) * 0.0004f;
        Vector3 palmL = new Vector3(-0.168f, paperY + 0.022f + breathe, -0.065f);
        Vector3 cL = palmL + fwdL * 0.04f; cL.y = paperY + 0.025f + breathe;
        Vector3 armL = (new Vector3(-0.20f, 0f, -0.97f).normalized + up * 0.12f).normalized;
        leftHand.Pose(cL - fwdL * 0.042f, fwdL, 1f, armL);

        // ---- soft contact shadows (light comes from the lamp) ----
        float h = Mathf.Max(0f, T.y - paperY);
        float spread = 1f + h * 10f;
        Vector3 away = new Vector3(T.x - lampPos.x, 0f, T.z - lampPos.z); away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.right;
        Vector3 c0 = (T + palmR) * 0.5f;
        shadow.position = new Vector3(c0.x, paperY + 0.0009f, c0.z) + away * 0.018f * spread;
        shadow.rotation = Quaternion.Euler(90f, Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg - 90f, 0f);
        shadow.localScale = new Vector3(0.17f * spread, 0.075f * spread, 1f);
        Vector3 awayL = new Vector3(palmL.x - lampPos.x, 0f, palmL.z - lampPos.z).normalized;
        shadowL.position = new Vector3(palmL.x, paperY + 0.0009f, palmL.z) + awayL * 0.016f + new Vector3(fwdL.x, 0f, fwdL.z) * 0.05f;
        shadowL.rotation = Quaternion.Euler(90f, Mathf.Atan2(fwdL.x, fwdL.z) * Mathf.Rad2Deg - 90f, 0f);
        shadowL.localScale = new Vector3(0.20f, 0.10f, 1f);
        var c = shadowMat.color; c.a = Mathf.Clamp01(0.30f - h * 4f); shadowMat.color = c;
    }
}

public class HandRig
{
    readonly Transform root, body, thumb, skinArm, sleeveArm;

    public HandRig(Transform parent, Material skin, Material sleeve)
    {
        root = new GameObject("hand").transform; root.SetParent(parent, false);
        body = DeskKit.P(PrimitiveType.Sphere, "mitten", root, skin, new Vector3(0f, 0f, 0.042f), Vector3.zero, new Vector3(0.063f, 0.048f, 0.078f), false);
        thumb = new GameObject("thumb").transform; thumb.SetParent(root, false);
        DeskKit.P(PrimitiveType.Capsule, "thumbBlob", thumb, skin, new Vector3(0f, 0f, 0.018f), new Vector3(90f, 0f, 0f), new Vector3(0.025f, 0.019f, 0.025f), false);
        skinArm = DeskKit.P(PrimitiveType.Capsule, "wrist", parent, skin, Vector3.zero, Vector3.zero, Vector3.one, false);
        sleeveArm = DeskKit.P(PrimitiveType.Capsule, "sleeve", parent, sleeve, Vector3.zero, Vector3.zero, Vector3.one, false);
    }

    public void Pose(Vector3 wrist, Vector3 fwd, float side, Vector3 armDir)
    {
        root.position = wrist; root.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        thumb.localPosition = new Vector3(side * 0.022f, -0.016f * (side < 0f ? 1f : 0f) + 0.002f, 0.024f);
        thumb.localRotation = Quaternion.Euler(side < 0f ? 25f : 0f, side * 22f, 0f);
        Vector3 fa = armDir.normalized;
        DeskKit.Seg(skinArm, wrist, wrist + fa * 0.04f, 0.014f);
        DeskKit.Seg(sleeveArm, wrist + fa * 0.036f, wrist + fa * 0.35f, 0.018f);
    }
}
