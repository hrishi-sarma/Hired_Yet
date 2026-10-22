using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stylized flat-colour painter for NPC models. Reads renderer, mesh and material names (e.g. "face.001", "gloves.001", "hair", "hood.002")
/// and paints skin, hair and clothes with random colours. One skin tone per NPC; garments of the same group share a colour.
/// Uses MaterialPropertyBlocks, so shared materials are not instanced. Unrecognised names (eyes, teeth, ...) are left alone.
/// </summary>
public class NpcStylizedPainter : MonoBehaviour
{
    public static bool Enabled = true;
    [Tooltip("Replace the albedo texture with white so the colour is flat and stylized. Off keeps the texture and tints it.")]
    public bool flatten = true;

    enum Part { None, Skin, Hair, Top, Bottom, Shoes, Accent, Bag, Gloves }

    static readonly string[] Skip = { "eye", "teeth", "tooth", "tongue", "mouth", "lash", "iris", "pupil", "cornea", "glass", "nail" };
    static readonly string[] SkinKeys = { "skin", "face", "head", "neck", "hand", "arm", "ear", "nose", "body", "leg", "foot", "torso" };
    static readonly string[] HairKeys = { "hair", "beard", "brow", "mustache", "moustache", "stubble", "fringe", "ponytail" };
    static readonly string[] TopKeys = { "shirt", "top", "tee", "jacket", "coat", "hoodie", "hood", "sweater", "jumper", "vest", "dress", "blouse", "cloth", "outfit", "uniform", "sleeve", "cardigan", "poncho", "robe", "suit" };
    static readonly string[] BottomKeys = { "pant", "trouser", "jean", "short", "skirt", "legging" };
    static readonly string[] ShoeKeys = { "shoe", "boot", "sneaker", "sock", "sandal", "heel", "slipper" };
    static readonly string[] AccentKeys = { "hat", "cap", "beanie", "scarf", "belt", "tie", "mask", "helmet", "bandana", "apron" };
    static readonly string[] GloveKeys = { "glove", "mitten" };
    static readonly string[] BagKeys = { "bag", "backpack", "satchel", "purse", "pack" };

    static readonly string[] SkinHex = { "#F6D9C2", "#EBC3A2", "#DDAA82", "#C68B5E", "#A8693F", "#85502F", "#62391F", "#4A2A17" };
    static readonly string[] HairHex = { "#0F0C0A", "#2A1A10", "#4A2E1A", "#6B4423", "#8C6239", "#B8873F", "#D9B36A", "#A83A1E", "#8A8A8A", "#D8D8D8" };
    static readonly string[] ShoeHex = { "#1A1A1A", "#2B2B2B", "#3B2A1E", "#4A3A2A", "#E8E8E8", "#7A2020" };

    [Tooltip("Share of NPCs whose gloves are painted in their skin tone, i.e. bare hands.")]
    public static float BareHandsChance = 0.85f;

    public struct Palette { public Color top, bottom, shoes, accent, bag; }

    public const int PaletteCount = 40;
    static Palette[] palettes;

    /// <summary>The 40 fixed palettes (same every run). Muted, wearable colours: no neon. Index = NPC outfit.</summary>
    public static Palette[] Palettes
    {
        get
        {
            if (palettes != null) return palettes;
            palettes = new Palette[PaletteCount];
            System.Random r = new System.Random(4040);
            for (int i = 0; i < PaletteCount; i++)
            {
                float h = (i + (float)r.NextDouble() * 0.5f) / PaletteCount;
                float bh;
                switch (i % 4)
                {
                    case 0: bh = h + 0.08f; break;            // analogous
                    case 1: bh = h + 0.5f; break;             // complementary
                    case 2: bh = h; break;                    // neutral bottom
                    default: bh = h + 0.33f; break;           // triad
                }
                Palette p = new Palette();
                p.top = Color.HSVToRGB(Mathf.Repeat(h, 1f), Rf(r, 0.25f, 0.55f), Rf(r, 0.5f, 0.85f));
                p.bottom = i % 4 == 2
                    ? Color.HSVToRGB(Mathf.Repeat(h, 1f), Rf(r, 0.04f, 0.15f), Rf(r, 0.2f, 0.55f))
                    : Color.HSVToRGB(Mathf.Repeat(bh, 1f), Rf(r, 0.12f, 0.4f), Rf(r, 0.25f, 0.6f));
                p.shoes = Hex(ShoeHex[r.Next(ShoeHex.Length)]);
                p.accent = Color.HSVToRGB(Mathf.Repeat(h + Rf(r, 0.08f, 0.17f), 1f), Rf(r, 0.3f, 0.55f), Rf(r, 0.5f, 0.8f));
                p.bag = Color.HSVToRGB(Mathf.Repeat(h + 0.5f, 1f), Rf(r, 0.1f, 0.35f), Rf(r, 0.25f, 0.55f));
                palettes[i] = p;
            }
            return palettes;
        }
    }

    static float Rf(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

    public static void Apply(GameObject model, System.Random rng, NpcLook look = null)
    {
        if (!Enabled || model == null) return;
        NpcStylizedPainter p = model.GetComponent<NpcStylizedPainter>();
        if (p == null) p = model.AddComponent<NpcStylizedPainter>();
        p.Paint(rng, look);
    }

    public void Paint(System.Random rng, NpcLook look = null)
    {
        Color skin = Hex(SkinHex[rng.Next(SkinHex.Length)]);
        skin = Jitter(skin, rng, 0.01f, 0.04f, 0.04f);
        Color hair = Hex(HairHex[rng.Next(HairHex.Length)]);
        Palette pal = Palettes[rng.Next(PaletteCount)];
        Color top = Jitter(pal.top, rng, 0.01f, 0.04f, 0.05f);
        Color bottom = Jitter(pal.bottom, rng, 0.01f, 0.04f, 0.05f);
        Color shoes = pal.shoes;
        Color accent = pal.accent;
        Color bag = pal.bag;
        Color gloves = rng.NextDouble() < BareHandsChance ? skin : accent;   // most NPCs: glove meshes read as bare hands
        if (look != null)
        {
            if (look.hair.HasValue) hair = look.hair.Value;
            if (look.top.HasValue) top = look.top.Value;
            if (look.bottom.HasValue) bottom = look.bottom.Value;
            if (look.shoes.HasValue) shoes = look.shoes.Value;
            if (look.accent.HasValue) accent = look.accent.Value;
            if (look.gloves.HasValue) gloves = look.gloves.Value;
        }

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        Renderer[] rs = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Material[] mats = r.sharedMaterials;
            bool swapped = false;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;
                mats[m] = DoubleSided(mats[m]);
                swapped = true;
            }
            if (swapped) r.sharedMaterials = mats;
            string meshName = "";
            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null) meshName = smr.sharedMesh.name;
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) meshName = mf.sharedMesh.name;
            }
            for (int m = 0; m < mats.Length; m++)
            {
                string matName = mats[m] != null ? mats[m].name : "";
                Part part = Classify(matName, r.name, meshName);
                if (part == Part.None) continue;
                Color c;
                switch (part)
                {
                    case Part.Skin: c = skin; break;
                    case Part.Hair: c = hair; break;
                    case Part.Top: c = top; break;
                    case Part.Bottom: c = bottom; break;
                    case Part.Shoes: c = shoes; break;
                    case Part.Bag: c = bag; break;
                    case Part.Gloves: c = gloves; break;
                    default: c = accent; break;
                }
                r.GetPropertyBlock(mpb, m);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                if (flatten)
                {
                    mpb.SetTexture("_BaseMap", Texture2D.whiteTexture);
                    mpb.SetTexture("_MainTex", Texture2D.whiteTexture);
                }
                r.SetPropertyBlock(mpb, m);
            }
        }
    }

    static readonly System.Collections.Generic.Dictionary<Material, Material> doubleSided = new System.Collections.Generic.Dictionary<Material, Material>();

    static Material DoubleSided(Material src)
    {
        Material inst;
        if (doubleSided.TryGetValue(src, out inst) && inst != null) return inst;
        inst = new Material(src);
        inst.name = src.name;
        if (inst.HasProperty("_Cull")) inst.SetFloat("_Cull", 0f);
        if (inst.HasProperty("_CullMode")) inst.SetFloat("_CullMode", 0f);
        if (inst.HasProperty("_DoubleSidedEnable")) inst.SetFloat("_DoubleSidedEnable", 1f);
        inst.doubleSidedGI = true;
        doubleSided[src] = inst;
        return inst;
    }

    // Material name wins over renderer name, which wins over mesh name; a clothing word beats a body word ("arm" in "armor", "hand" in "gloves").
    static Part Classify(string material, string renderer, string mesh)
    {
        string[] names = { material.ToLowerInvariant(), renderer.ToLowerInvariant(), mesh.ToLowerInvariant() };
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            if (n.Length == 0) continue;
            if (Has(n, Skip)) return Part.None;
            if (Has(n, HairKeys)) return Part.Hair;
            if (Has(n, BagKeys)) return Part.Bag;
            if (Has(n, ShoeKeys)) return Part.Shoes;
            if (Has(n, BottomKeys)) return Part.Bottom;
            if (Has(n, GloveKeys)) return Part.Gloves;
            if (Has(n, AccentKeys)) return Part.Accent;
            if (Has(n, TopKeys)) return Part.Top;
            if (Has(n, SkinKeys)) return Part.Skin;
        }
        return Part.None;
    }

    static bool Has(string n, string[] keys)
    {
        for (int i = 0; i < keys.Length; i++) if (n.Contains(keys[i])) return true;
        return false;
    }

    static Color Jitter(Color c, System.Random r, float h, float s, float v)
    {
        float hh, ss, vv;
        Color.RGBToHSV(c, out hh, out ss, out vv);
        hh = Mathf.Repeat(hh + ((float)r.NextDouble() * 2f - 1f) * h, 1f);
        ss = Mathf.Clamp01(ss + ((float)r.NextDouble() * 2f - 1f) * s);
        vv = Mathf.Clamp01(vv + ((float)r.NextDouble() * 2f - 1f) * v);
        return Color.HSVToRGB(hh, ss, vv);
    }

    static Color Hex(string h)
    {
        Color c;
        ColorUtility.TryParseHtmlString(h, out c);
        return c;
    }
}
