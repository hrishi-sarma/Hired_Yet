using System.Collections.Generic;
using UnityEngine;

public static class MaterialFactory
{
    static Material baseMaterial;
    static readonly Dictionary<int, Material> cache = new Dictionary<int, Material>();

    public static void Reset(Material defaultMaterial)
    {
        cache.Clear();
        StylizedMaterialFactory.Reset();
        baseMaterial = defaultMaterial;
    }

    public static Material Get(Color tint)
    {
        Color32 c32 = tint;
        int key = c32.r | (c32.g << 8) | (c32.b << 16) | (c32.a << 24);
        Material m;
        if (cache.TryGetValue(key, out m) && m != null) return m;
        // Green procedural primitives are commonly tree crowns or foliage. Give them
        // a leaf-like mottled texture instead of the generic surface pattern.
        float hue, saturation, value;
        Color.RGBToHSV(tint, out hue, out saturation, out value);
        string role;
        if (hue >= 0.18f && hue <= 0.43f && saturation >= 0.22f && value <= 0.9f)
            role = "foliage";
        else if (hue >= 0.035f && hue <= 0.13f && saturation >= 0.24f && value <= 0.62f)
            role = "wood"; // tree trunks and fallen timber receive subtle bark/wood grain
        else
            role = "surface";
        m = StylizedMaterialFactory.Get(role, tint, key);
        m.name = "World_Stylized_" + key;
        cache[key] = m;
        return m;
    }
}