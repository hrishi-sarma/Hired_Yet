using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small helper used by InterviewUI to build uGUI at runtime.
/// All sprites (rounded panels, rings, scanlines) are generated procedurally, so no art assets are needed.
/// </summary>
public static class UIKit
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // ---------- paper theme (matches ResumeFormUI) ----------
    public static readonly Color Paper = new Color(1f, 0.98f, 0.9f, 0.97f);
    public static readonly Color Cream = new Color(1f, 1f, 0.95f, 1f);
    public static readonly Color Dark = new Color(0.15f, 0.12f, 0.2f, 1f);

    /// <summary>Boxy cartoon look: thick dark outline + hard drop shadow.</summary>
    public static Image Boxy(Image img, float outline = 4f, bool shadow = true)
    {
        Outline o = img.gameObject.AddComponent<Outline>();
        o.effectColor = Dark; o.effectDistance = new Vector2(outline, -outline);
        if (shadow)
        {
            Shadow s = img.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.45f); s.effectDistance = new Vector2(8f, -8f);
        }
        return img;
    }

    /// <summary>Strip of orange tape stuck on the top edge of a "paper" panel.</summary>
    public static void Tape(RectTransform parent, float x, float rot)
    {
        RectTransform t = NewRect("Tape", parent);
        Place(t, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, 2f), new Vector2(120f, 30f));
        t.localRotation = Quaternion.Euler(0f, 0f, rot);
        Boxy(AddImage(t, null, new Color(1f, 0.62f, 0.2f, 0.95f)), 2f, false);
    }

    // ---------- colors ----------
    public static Color Hex(string h)
    {
        Color c;
        ColorUtility.TryParseHtmlString(h, out c);
        return c;
    }

    public static Color A(Color c, float a)
    {
        c.a = a;
        return c;
    }

    // ---------- procedural sprites ----------
    /// <summary>9-sliced rounded rectangle (filled).</summary>
    public static Sprite Rounded(int radius)
    {
        return MakeRounded(radius, false, 0);
    }

    /// <summary>9-sliced rounded rectangle outline.</summary>
    public static Sprite Ring(int radius, int stroke)
    {
        return MakeRounded(radius, true, stroke);
    }

    static Sprite MakeRounded(int radius, bool ring, int stroke)
    {
        string key = (ring ? "ring" : "fill") + radius + "_" + stroke;
        Sprite cached;
        if (cache.TryGetValue(key, out cached) && cached != null) return cached;

        int size = radius * 2 + 4;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = size * 0.5f;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius; // signed distance to the edge

                float a = Mathf.Clamp01(0.5f - d);
                if (ring)
                {
                    float inner = Mathf.Clamp01(0.5f - (d + stroke));
                    a = Mathf.Clamp01(a - inner);
                }
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);

        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        cache[key] = s;
        return s;
    }

    /// <summary>
    /// 256x4 tile with one darker row = CRT scanlines (hard-edged stripes, no gradient).
    /// Use with Image.Type.Tiled.
    /// </summary>
    public static Sprite Scanlines()
    {
        Sprite cached;
        if (cache.TryGetValue("scanlines", out cached) && cached != null) return cached;

        Texture2D tex = new Texture2D(256, 4, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                tex.SetPixel(x, y, y == 3 ? new Color(0f, 0f, 0f, 0.35f) : new Color(0f, 0f, 0f, 0f));
            }
        }
        tex.Apply(false, true);
        Sprite s = Sprite.Create(tex, new Rect(0, 0, 256, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        cache["scanlines"] = s;
        return s;
    }

    // ---------- rect helpers ----------
    public static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    /// <summary>Point-anchored placement (anchorMin == anchorMax).</summary>
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // ---------- graphics ----------
    public static Image AddImage(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = Image.Type.Sliced;
        return img;
    }

    public static TextMeshProUGUI AddText(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align, TMP_FontAsset font, FontStyles style = FontStyles.Normal, float spacing = 0f)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.characterSpacing = spacing;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Rounded translucent panel with a crisp outline child.</summary>
    public static RectTransform Panel(Transform parent, string name, int radius, Color fill, Color ringColor,
        bool raycast, out Image fillImg, out Image ringImg)
    {
        RectTransform rt = NewRect(name, parent);
        fillImg = AddImage(rt, Rounded(radius), fill, raycast);
        RectTransform ringRt = NewRect("Ring", rt);
        Stretch(ringRt);
        ringImg = AddImage(ringRt, Ring(radius, 2), ringColor, false);
        return rt;
    }

    public static RectTransform Panel(Transform parent, string name, int radius, Color fill, Color ringColor)
    {
        Image a;
        Image b;
        return Panel(parent, name, radius, fill, ringColor, false, out a, out b);
    }
}