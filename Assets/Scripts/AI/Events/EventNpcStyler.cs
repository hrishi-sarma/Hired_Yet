using System.Collections.Generic;
using UnityEngine;

public enum NpcGender { Any, Male, Female }

/// <summary>Per-event colour overrides handed to NpcStylizedPainter (null = keep the random pick).</summary>
public class NpcLook
{
    public Color? hair, top, bottom, shoes, accent, gloves;
}

/// <summary>
/// Event-specific NPC casting and styling: gender-matched model choice (profile/prefab name contains "man" or "girl"),
/// fixed looks (white-haired grandma, monochrome magician) and accessories (witch hat, top hat, druid staff with a glowing orb).
/// Edit the tables below to change which event uses which gender / look.
/// </summary>
public static class EventNpcStyler
{
    static readonly Dictionary<string, NpcGender> Genders = new Dictionary<string, NpcGender>
    {
        { "sprinkler_valve", NpcGender.Male },     // soggy dad
        { "injured_deer", NpcGender.Male },        // ranger
        { "fallen_log", NpcGender.Male },          // lumberjack
        { "street_magician", NpcGender.Male },
        { "hermit_riddle", NpcGender.Male },
        { "lawnmower", NpcGender.Male },
        { "grandma_groceries", NpcGender.Female }, // old woman
        { "garage_sale_couch", NpcGender.Female }, // garage sale lady
        { "witch_cauldron", NpcGender.Female },
    };

    public static NpcGender GenderFor(string eventId)
    {
        NpcGender g;
        return eventId != null && Genders.TryGetValue(eventId, out g) ? g : NpcGender.Any;
    }

    public static NpcGender GenderOf(NpcProfile p)
    {
        string n = ((p.prefab != null ? p.prefab.name : "") + "|" + (p.name ?? "")).ToLowerInvariant();
        if (n.Contains("girl") || n.Contains("woman") || n.Contains("lady") || n.Contains("female")) return NpcGender.Female;
        if (n.Contains("man") || n.Contains("boy") || n.Contains("male")) return NpcGender.Male;
        return NpcGender.Any;
    }

    /// <summary>Weighted pick among valid profiles of the wanted gender; falls back to any profile only if none match.</summary>
    public static NpcProfile Pick(List<NpcProfile> all, System.Random rng, NpcGender want)
    {
        if (all == null) return null;
        List<NpcProfile> list = new List<NpcProfile>();
        if (want != NpcGender.Any)
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].IsValid && GenderOf(all[i]) == want) list.Add(all[i]);
        if (list.Count == 0)
        {
            if (want != NpcGender.Any) Debug.LogWarning("EventNpcStyler: no " + want + " NPC profile found (name needs 'man' / 'girl'); using any NPC.");
            list = all;
        }
        int pick = NpcProfile.Pick(list, rng);
        return pick >= 0 ? list[pick] : null;
    }

    public static NpcLook LookFor(string id)
    {
        switch (id)
        {
            case "grandma_groceries":
                return new NpcLook { hair = Hex("#F4F4F2") };
            case "street_magician":
                return new NpcLook
                {
                    hair = Hex("#E8E8E8"), top = Hex("#15151A"), bottom = Hex("#0E0E12"),
                    shoes = Hex("#0A0A0A"), accent = Hex("#F2F2F2"), gloves = Hex("#F2F2F2")
                };
            case "witch_cauldron":
                return new NpcLook { hair = Hex("#1B1424"), top = Hex("#2B1B45"), bottom = Hex("#1D1230"), accent = Hex("#6B3FA0") };
        }
        return null;
    }

    static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

    // ------------------------------------------------------------------ accessories
    public static void Decorate(string id, GameObject model, Animator anim, Transform npcRoot)
    {
        if (npcRoot == null) return;
        if (id == "witch_cauldron") AddHat(model, anim, npcRoot, true);
        else if (id == "street_magician") AddHat(model, anim, npcRoot, false);
        else if (id == "druid_dance") AddStaff(npcRoot);
    }

    static void AddHat(GameObject model, Animator anim, Transform npcRoot, bool witch)
    {
        Transform bone = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        if (bone == null) bone = FindByName(model.transform, "head");
        if (bone == null) return;

        // measure the real head from the head/hair/face renderers
        bool any = false;
        Bounds hb = new Bounds();
        Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            string n = (rs[i].name + "|" + (rs[i].sharedMaterial != null ? rs[i].sharedMaterial.name : "")).ToLowerInvariant();
            if (!(n.Contains("head") || n.Contains("hair") || n.Contains("face"))) continue;
            if (n.Contains("hat") || n.Contains("hood")) continue;
            if (rs[i].bounds.center.y < bone.position.y - 0.05f) continue;
            if (!any) { hb = rs[i].bounds; any = true; } else hb.Encapsulate(rs[i].bounds);
        }
        float k = Mathf.Max(0.01f, npcRoot.lossyScale.y);
        Vector3 c = any ? new Vector3(hb.center.x, hb.max.y, hb.center.z) : bone.position + Vector3.up * 0.16f * k;
        float headW = any ? Mathf.Clamp(Mathf.Max(hb.size.x, hb.size.z), 0.12f * k, 0.3f * k) : 0.19f * k;

        GameObject hat = new GameObject(witch ? "WitchHat" : "MagicianHat");
        hat.transform.position = c - Vector3.up * headW * 0.22f * 4f;     // sink into the hair so it sits on the head, not above it
        hat.transform.rotation = npcRoot.rotation * Quaternion.Euler(witch ? -7f : 0f, 0f, 0f);
        if (witch) BuildWitchHat(hat.transform, headW);
        else BuildTopHat(hat.transform, headW);
        hat.transform.localScale = Vector3.one * 4f;   // hat props are 4x bigger
        hat.transform.SetParent(bone, true);
    }

    static Transform FindByName(Transform root, string key)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) if (all[i].name.ToLowerInvariant().Contains(key)) return all[i];
        return null;
    }

    static void BuildTopHat(Transform p, float w)
    {
        Prim(p, "Crown", PrimitiveKind.Cylinder, new Vector3(0f, w * 0.78f, 0f), new Vector3(w * 1.12f, w * 0.78f, w * 1.12f), "#0C0C10");
        Prim(p, "Brim", PrimitiveKind.Cylinder, new Vector3(0f, w * 0.02f, 0f), new Vector3(w * 1.95f, 0.006f, w * 1.95f), "#0C0C10");
        Prim(p, "Band", PrimitiveKind.Cylinder, new Vector3(0f, w * 0.2f, 0f), new Vector3(w * 1.16f, w * 0.17f, w * 1.16f), "#F2F2F2");
        Prim(p, "Top", PrimitiveKind.Cylinder, new Vector3(0f, w * 1.56f, 0f), new Vector3(w * 1.08f, 0.004f, w * 1.08f), "#18181E");
    }

    static void Prim(Transform p, string n, PrimitiveKind k, Vector3 pos, Vector3 scale, string hex)
    {
        GameObject g = PrimitiveFactory.Create(k, p);
        g.name = n;
        Collider col = g.GetComponent<Collider>();
        if (col != null) WorldUtil.Kill(col);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.identity;
        g.transform.localScale = scale;                 // cylinder primitive: y scale is half height
        Renderer r = g.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = MaterialFactory.Get(Hex(hex));
    }

    // ---- witch hat: bent cone + wide brim, one mesh, procedural stylized texture (purple, stars, moons, gold-buckle band)
    static Material witchMat;

    static void BuildWitchHat(Transform p, float w)
    {
        GameObject g = new GameObject("Hat");
        g.transform.SetParent(p, false);
        g.AddComponent<MeshFilter>().sharedMesh = WitchMesh(w * 0.66f, w * 2.5f, w * 1.45f, w * 0.7f);
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        if (witchMat == null)
        {
            witchMat = new Material(MaterialFactory.Get(Color.white));
            Texture2D t = WitchTexture();
            witchMat.mainTexture = t;
            if (witchMat.HasProperty("_BaseMap")) witchMat.SetTexture("_BaseMap", t);
            if (witchMat.HasProperty("_BaseColor")) witchMat.SetColor("_BaseColor", Color.white);
            witchMat.color = Color.white;
            if (witchMat.HasProperty("_Cull")) witchMat.SetFloat("_Cull", 0f);
            if (witchMat.HasProperty("_Smoothness")) witchMat.SetFloat("_Smoothness", 0.1f);
        }
        mr.sharedMaterial = witchMat;
    }

    static Mesh WitchMesh(float crownR, float height, float brimR, float bend)
    {
        const int seg = 28, rings = 12;
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();
        // cone, tip bent backwards
        for (int r = 0; r <= rings; r++)
        {
            float f = r / (float)rings;
            float rad = crownR * Mathf.Pow(1f - f, 0.85f) + 0.002f;
            float y = f * height * (1f - 0.08f * f);
            float z = -bend * f * f;
            for (int s = 0; s <= seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad + z));
                uv.Add(new Vector2(s / (float)seg, 0.1f + 0.9f * f));
            }
        }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < seg; s++)
            {
                int i = r * (seg + 1) + s, j = i + seg + 1;
                t.Add(i); t.Add(j); t.Add(i + 1);
                t.Add(i + 1); t.Add(j); t.Add(j + 1);
            }
        // brim: ring from crown to brim radius, drooping slightly at the edge
        int b0 = v.Count;
        for (int s = 0; s <= seg; s++)
        {
            float a = s / (float)seg * Mathf.PI * 2f;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(d * (crownR * 0.92f)); uv.Add(new Vector2(s / (float)seg, 0.05f));
            v.Add(d * brimR + Vector3.down * brimR * 0.07f); uv.Add(new Vector2(s / (float)seg, 0.03f));
        }
        for (int s = 0; s < seg; s++)
        {
            int i = b0 + s * 2;
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i + 2); t.Add(i + 1); t.Add(i + 3);
            t.Add(i); t.Add(i + 2); t.Add(i + 1);     // underside
            t.Add(i + 2); t.Add(i + 3); t.Add(i + 1);
        }
        Mesh m = new Mesh();
        m.name = "WitchHat";
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Texture2D WitchTexture()
    {
        const int S = 256;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapModeU = TextureWrapMode.Repeat; tex.wrapModeV = TextureWrapMode.Clamp;
        Color brim = Hex("#2A1745"), band = Hex("#1A0E2B"), gold = Hex("#F2C14E"), deep = Hex("#3C2066"), light = Hex("#7B4DB8");
        Color[] px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float v = y / (float)S;
                Color c;
                if (v < 0.1f) c = brim;
                else if (v < 0.3f) c = band;
                else c = Color.Lerp(deep, light, Mathf.InverseLerp(0.3f, 1f, v) * 0.85f);
                px[y * S + x] = c;
            }
        // buckle on the band (square frame, gold)
        for (int y = (int)(S * 0.13f); y < (int)(S * 0.27f); y++)
            for (int x = (int)(S * 0.44f); x < (int)(S * 0.56f); x++)
            {
                bool edge = y < S * 0.15f || y >= S * 0.25f || x < S * 0.46f || x >= S * 0.54f;
                if (edge) px[y * S + x] = gold;
            }
        System.Random r = new System.Random(77);
        for (int i = 0; i < 70; i++)    // stars (plus shapes)
        {
            int cx = r.Next(4, S - 4), cy = r.Next((int)(S * 0.34f), S - 4);
            Color sc = Color.Lerp(gold, Color.white, (float)r.NextDouble() * 0.5f);
            for (int d = -2; d <= 2; d++) { px[cy * S + cx + d] = sc; px[(cy + d) * S + cx] = sc; }
        }
        for (int i = 0; i < 5; i++)     // crescent moons
        {
            int cx = r.Next(20, S - 20), cy = r.Next((int)(S * 0.4f), S - 24);
            for (int y = -9; y <= 9; y++)
                for (int x = -9; x <= 9; x++)
                {
                    bool outer = x * x + y * y <= 81, inner = (x - 4) * (x - 4) + y * y <= 64;
                    if (outer && !inner) px[(cy + y) * S + cx + x] = gold;
                }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ---- druid staff: planted beside the druid, glowing orb on top
    static void AddStaff(Transform npcRoot)
    {
        float k = Mathf.Max(0.01f, npcRoot.lossyScale.y);
        GameObject staff = new GameObject("DruidStaff");
        staff.transform.SetParent(npcRoot, false);
        staff.transform.localPosition = new Vector3(0.62f / k, 0f, 0.12f / k);
        staff.transform.localRotation = Quaternion.Euler(0f, 0f, 3f);
        staff.transform.localScale = Vector3.one / k;          // children below are in metres

        Prim(staff.transform, "Shaft", PrimitiveKind.Cylinder, new Vector3(0f, 0.95f, 0f), new Vector3(0.06f, 0.95f, 0.06f), "#5E4025");
        Prim(staff.transform, "Foot", PrimitiveKind.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.09f, 0.04f, 0.09f), "#3E2A18");
        for (int i = 0; i < 3; i++)    // wooden claws cradling the orb
        {
            float a = i * 120f;
            Transform c = new GameObject("Claw").transform;
            c.SetParent(staff.transform, false);
            c.localPosition = new Vector3(0f, 1.9f, 0f);
            c.localRotation = Quaternion.Euler(0f, a, 0f);
            Prim(c, "Prong", PrimitiveKind.Cylinder, new Vector3(0.085f, 0.1f, 0f), new Vector3(0.035f, 0.11f, 0.035f), "#5E4025");
            c.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, -22f);
        }
        GameObject orb = PrimitiveFactory.Create(PrimitiveKind.Sphere, staff.transform);
        orb.name = "GlowOrb";
        Collider col = orb.GetComponent<Collider>();
        if (col != null) WorldUtil.Kill(col);
        orb.transform.localPosition = new Vector3(0f, 2.02f, 0f);
        orb.transform.localScale = Vector3.one * 0.2f;
        Color glow = Hex("#7DFFC4");
        Material m = new Material(MaterialFactory.Get(glow));
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", glow * 3f);
        orb.GetComponent<Renderer>().sharedMaterial = m;
        Light l = orb.AddComponent<Light>();
        l.type = LightType.Point; l.color = glow; l.range = 4.5f; l.intensity = 1.6f; l.shadows = LightShadows.None;
        orb.AddComponent<StaffGlow>().Init(m, l, glow);
    }
}

/// <summary>Gentle pulse of the staff orb's emission and light.</summary>
public class StaffGlow : MonoBehaviour
{
    Material mat; Light lt; Color col; float ph;
    public void Init(Material m, Light l, Color c) { mat = m; lt = l; col = c; ph = Random.value * 6.28f; }
    void Update()
    {
        float k = 0.8f + 0.2f * Mathf.Sin(Time.time * 2.2f + ph);
        if (mat != null && mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", col * (3f * k));
        if (lt != null) lt.intensity = 1.6f * k;
    }
}
