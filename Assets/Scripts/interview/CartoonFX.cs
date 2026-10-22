using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Cartoon { Question, Exclaim, Anger, Heart, Sparkle, Sweat, Zzz, Dots, Steam }

/// <summary>
/// Comic-style 2D stickers that pop over the interviewer's head (question mark, !, !!, !!!, anger mark,
/// hearts, sweat drops, Zzz...). All art is generated at runtime (white fill + black outline, tinted per
/// use), so no assets are needed. Elements are UI images placed by projecting the head into screen space.
/// </summary>
public class CartoonFX : MonoBehaviour
{
    class Item
    {
        public RectTransform rt; public Image img;
        public float t0, life, size, rot0, wob, wobFreq, vy, driftX, bounceAmt;
        public Vector2 off;      // offset in head-size units
    }

    RectTransform layer; InterviewerRig rig; System.Func<Camera> camGet;
    readonly List<Item> items = new List<Item>();
    readonly List<Item> pending = new List<Item>();
    static Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    const float LayerRef = 1f;

    public void Init(RectTransform parent, InterviewerRig r, System.Func<Camera> cam)
    {
        layer = parent; rig = r; camGet = cam;
    }

    public void Clear()
    {
        for (int i = 0; i < items.Count; i++) if (items[i] != null && items[i].rt != null) WorldUtil.Kill(items[i].rt.gameObject);
        items.Clear(); pending.Clear(); lastT = -99f; lastPrio = 0;
    }

    // ------------------------------------------------------------------ public API
    float lastT = -99f; int lastPrio;
    const float Gap = 2.0f;           // minimum time between two reactions (anti-spam)

    static int Prio(Cartoon k, int level)
    {
        switch (k)
        {
            case Cartoon.Exclaim: return level >= 3 ? 4 : 3;
            case Cartoon.Anger: return 3;
            case Cartoon.Heart: case Cartoon.Sparkle: case Cartoon.Question: case Cartoon.Steam: return 2;
        }
        return 1;
    }

    void FadeAll()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            if (it == null || it.rt == null) continue;
            if (it.t0 > now) { it.t0 = now - it.life; continue; }     // not shown yet -> drop
            it.t0 = Mathf.Min(it.t0, now - it.life * 0.74f);           // jump into the fade-out
        }
    }

    /// <summary>One reaction at a time: a bigger reaction replaces a smaller one, small ones during the cooldown are dropped.</summary>
    public void Pop(Cartoon kind, int level = 1, bool force = false)
    {
        if (layer == null) return;
        int prio = Prio(kind, level);
        float now = Time.unscaledTime;
        bool busy = now - lastT < Gap;
        if (!force && busy && prio <= lastPrio) return;
        if (!force && items.Count >= 5) return;
        if (busy || force) FadeAll();
        lastT = now; lastPrio = prio;
        switch (kind)
        {
            case Cartoon.Question:
                Add("q", new Color(1f, 0.86f, 0.2f), 1.0f, 0.1f, 1.05f, 0f, 0f, -8f, 4f, 7f, 0f, 0f, 1.7f);
                break;
            case Cartoon.Exclaim:
            {
                level = Mathf.Clamp(level, 1, 3);
                Color c = level == 1 ? new Color(1f, 0.86f, 0.2f) : (level == 2 ? new Color(1f, 0.6f, 0.15f) : new Color(1f, 0.28f, 0.22f));
                float s = 0.8f + 0.14f * level;
                for (int i = 0; i < level; i++)
                {
                    float k = i - (level - 1) * 0.5f;
                    Add("e", c, s, k * 0.55f, 1.0f, 0f, i * 0.09f, k * -12f, level * 2f, 13f + level * 3f, 0f, 0f, 1.3f + level * 0.15f);
                }
                break;
            }
            case Cartoon.Anger:
                Add("a", new Color(1f, 0.18f, 0.14f), 0.85f, 0.78f, 0.9f, 0f, 0f, 8f, 9f, 15f, 0f, 0f, 1.5f);
                break;
            case Cartoon.Heart:
                for (int i = 0; i < Mathf.Clamp(level, 1, 2); i++)
                    Add("h", i == 0 ? new Color(1f, 0.38f, 0.52f) : new Color(1f, 0.62f, 0.74f), i == 0 ? 0.75f : 0.5f, i == 0 ? -0.1f : 0.6f, i == 0 ? 0.85f : 0.7f,
                        0.3f, i * 0.22f, i == 0 ? -8f : 12f, 6f, 5f, 0.05f, 0.5f, 1.7f);
                break;
            case Cartoon.Sparkle:
                for (int i = 0; i < Mathf.Clamp(level, 1, 2); i++)
                    Add("s", i == 0 ? new Color(1f, 0.92f, 0.35f) : Color.white, i == 0 ? 0.7f : 0.45f, i == 0 ? -0.5f : 0.55f, i == 0 ? 1.0f : 0.8f,
                        0f, i * 0.15f, i == 0 ? -10f : 15f, 20f, 8f, 0f, 0f, 1.1f);
                break;
            case Cartoon.Sweat:
                Add("d", new Color(0.5f, 0.8f, 1f), 0.58f, 0.8f, 0.75f, -0.22f, 0f, 8f, 4f, 6f, 0f, 0f, 1.5f);
                break;
            case Cartoon.Zzz:
                for (int i = 0; i < 3; i++)
                    Add("z", new Color(0.72f, 0.86f, 1f), 0.38f + i * 0.17f, 0.55f + i * 0.33f, 0.85f + i * 0.28f, 0.28f, i * 0.35f, 8f, 6f, 4f, 0f, 0.2f, 1.9f);
                break;
            case Cartoon.Dots:
                Add("t", Color.white, 1.05f, 0.75f, 1.0f, 0f, 0f, 0f, 3f, 4f, 0f, 0.15f, 1.7f);
                break;
            case Cartoon.Steam:
                Add("c", new Color(0.96f, 0.96f, 0.96f), 0.62f, 0.95f, 0.4f, 0.4f, 0f, 0f, 8f, 5f, 0.05f, 0f, 1.3f);
                Add("c", new Color(0.96f, 0.96f, 0.96f), 0.62f, -0.95f, 0.4f, 0.4f, 0.16f, 0f, 8f, 5f, -0.05f, 0f, 1.3f);
                break;
        }
    }

    // sprite, tint, size(units of head), x, y (head units), rise speed, delay, rotation, wobble deg, wobble hz, drift, bounce, life
    void Add(string spr, Color tint, float size, float x, float y, float vy, float delay, float rot, float wob, float wobFreq, float driftX, float bounce, float life)
    {
        if (layer == null) return;
        GameObject go = new GameObject("Cartoon_" + spr, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(layer, false);
        go.SetActive(false);
        Image im = go.GetComponent<Image>();
        im.sprite = GetSprite(spr); im.color = tint; im.raycastTarget = false;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100f, 100f);
        items.Add(new Item
        {
            rt = rt, img = im, t0 = Time.unscaledTime + delay, life = life, size = size, rot0 = rot, wob = wob, wobFreq = wobFreq,
            vy = vy, driftX = driftX, bounceAmt = bounce, off = new Vector2(x, y)
        });
    }

    // ------------------------------------------------------------------ animation
    static float EaseOutBack(float t)
    {
        const float c1 = 2.2f, c3 = c1 + 1f;
        t = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    void LateUpdate()
    {
        if (layer == null || items.Count == 0) return;
        Camera cam = camGet != null ? camGet() : Camera.main;
        if (cam == null || rig == null) return;
        Canvas cv = layer.GetComponentInParent<Canvas>();
        float sf = cv != null ? Mathf.Max(0.01f, cv.scaleFactor) : 1f;
        Camera ccam = (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) ? cv.worldCamera : null;

        Vector3 anchor = rig.HeadPosition + Vector3.up * 0.32f;
        Vector3 sp = cam.WorldToScreenPoint(anchor);
        bool behind = sp.z <= 0.05f;
        Vector3 sp2 = cam.WorldToScreenPoint(anchor + cam.transform.right * 0.5f);
        float ppm = Mathf.Abs(sp2.x - sp.x) * 2f;            // screen pixels per world meter at the head
        float unit = Mathf.Clamp(ppm * 0.42f, 70f, 260f);     // pixels of one "head unit"

        float now = Time.unscaledTime;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            Item it = items[i];
            if (it == null || it.rt == null || it.img == null) { items.RemoveAt(i); continue; }
            float age = now - it.t0;
            if (age < 0f || behind) { it.rt.gameObject.SetActive(false); continue; }
            if (age > it.life) { WorldUtil.Kill(it.rt.gameObject); items.RemoveAt(i); continue; }
            if (!it.rt.gameObject.activeSelf) it.rt.gameObject.SetActive(true);

            float u = age / it.life;
            float popT = Mathf.Clamp01(age / 0.28f);
            float sc = EaseOutBack(popT);
            float outT = Mathf.Clamp01((u - 0.72f) / 0.28f);
            sc *= 1f - outT * outT;
            // little "boing" settle after the pop
            if (age > 0.28f && age < 0.7f) sc *= 1f + Mathf.Sin((age - 0.28f) * 22f) * 0.07f * (1f - (age - 0.28f) / 0.42f);
            float alpha = 1f - Mathf.Clamp01((u - 0.8f) / 0.2f);

            float yRise = it.vy * age;
            float bounce = it.bounceAmt > 0f ? Mathf.Abs(Mathf.Sin(age * 5f)) * it.bounceAmt * 0.4f : 0f;
            Vector2 pxOff = new Vector2((it.off.x + it.driftX * age) * unit, (it.off.y - 0.55f + yRise + bounce) * unit);
            Vector2 scr = new Vector2(sp.x, sp.y) + pxOff;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, scr, ccam, out local);
            it.rt.anchoredPosition = local;
            float px = unit * it.size / sf;
            it.rt.sizeDelta = new Vector2(px, px);
            it.rt.localScale = new Vector3(sc, sc, 1f);
            it.rt.localRotation = Quaternion.Euler(0f, 0f, it.rot0 + Mathf.Sin(age * it.wobFreq) * it.wob);
            Color c = it.img.color; c.a = alpha; it.img.color = c;
        }
    }

    // ------------------------------------------------------------------ procedural art
    delegate float Sdf(float x, float y);

    static Sprite GetSprite(string key)
    {
        Sprite s;
        if (cache.TryGetValue(key, out s) && s != null) return s;
        Sdf f, ink = null, hi = null;
        switch (key)
        {
            case "q": f = SdfQuestion; hi = HiQuestion; break;
            case "e": f = SdfExclaim; hi = (x, y) => Capsule(x, y, -0.1f, 0.52f, -0.08f, 0.2f, 0.045f); break;
            case "a": f = SdfAnger; break;
            case "h": f = SdfHeart; hi = (x, y) => Mathf.Min(Capsule(x, y, -0.46f, 0.3f, -0.4f, 0.44f, 0.055f), Circle(x, y, -0.26f, 0.52f, 0.05f)); break;
            case "s": f = SdfSparkle; break;
            case "d": f = SdfDrop; hi = (x, y) => Capsule(x, y, -0.2f, -0.12f, -0.2f, -0.34f, 0.055f); break;
            case "z": f = SdfZ; break;
            case "c": f = SdfCloud; hi = (x, y) => Circle(x, y, -0.45f, 0.08f, 0.09f); break;
            case "b": f = SdfBurst; break;
            case "t":
                f = SdfBubble;
                ink = (x, y) => Mathf.Min(Mathf.Min(Circle(x, y, -0.4f, 0.08f, 0.13f), Circle(x, y, 0f, 0.08f, 0.13f)), Circle(x, y, 0.4f, 0.08f, 0.13f));
                break;
            default: f = (x, y) => Circle(x, y, 0f, 0f, 0.8f); break;
        }
        s = Bake(f, ink, hi, 160);
        cache[key] = s;
        return s;
    }

    // Toon sticker: thick black outline, three-tone cel shading (light / base / shade), optional gloss + ink details.
    static Sprite Bake(Sdf sdf, Sdf ink, Sdf hi, int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[n * n];
        const float ow = 0.11f;
        float aa = 2.4f / n;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = ((i + 0.5f) / n * 2f - 1f) * 1.2f;
                float y = ((j + 0.5f) / n * 2f - 1f) * 1.2f;
                float d = sdf(x, y);
                float outer = Mathf.Clamp01(0.5f - (d - ow) / aa);
                float inner = Mathf.Clamp01(0.5f - d / aa);
                float g = 0.95f;
                if (sdf(x + 0.13f, y - 0.13f) > 0f) g = 0.72f;           // shade on the lower-right rim
                if (hi != null && hi(x, y) < 0f) g = 1f;                  // gloss
                float bodyG = 255f * g;
                float inkA = 0f;
                if (ink != null) inkA = Mathf.Clamp01(0.5f - ink(x, y) / aa);
                float v = Mathf.Lerp(18f, bodyG, inner);
                v = Mathf.Lerp(v, 18f, inkA * inner);
                byte b = (byte)Mathf.Clamp(v, 0f, 255f);
                px[j * n + i] = new Color32(b, b, b, (byte)(255f * outer));
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---- shape helpers
    static float Circle(float x, float y, float cx, float cy, float r) { return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r; }
    static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r) { return RoundCone(x, y, ax, ay, r, bx, by, r); }
    // capsule whose radius changes from ra (at a) to rb (at b): tapered strokes
    static float RoundCone(float x, float y, float ax, float ay, float ra, float bx, float by, float rb)
    {
        float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
        float h = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy + 1e-6f));
        float ex = px - dx * h, ey = py - dy * h;
        return Mathf.Sqrt(ex * ex + ey * ey) - Mathf.Lerp(ra, rb, h);
    }

    // fat tapered "!" : wide top, narrow bottom, round dot
    static float SdfExclaim(float x, float y)
    {
        float bar = RoundCone(x, y, 0f, 0.6f, 0.25f, 0f, -0.02f, 0.12f);
        return Mathf.Min(bar, Circle(x, y, 0f, -0.56f, 0.18f));
    }

    static float ArcQ(float x, float y, float r, float w, float aMin)
    {
        float cy = 0.3f;
        float ang = Mathf.Atan2(y - cy, x) * Mathf.Rad2Deg;
        if (ang < aMin && ang > -179.9f) return 1f;
        return Mathf.Abs(Mathf.Sqrt(x * x + (y - cy) * (y - cy)) - r) - w;
    }

    static float SdfQuestion(float x, float y)
    {
        float arc = ArcQ(x, y, 0.34f, 0.12f, -75f);
        float cap = Circle(x, y, -0.34f, 0.3f, 0.12f);
        float stem = RoundCone(x, y, 0.1f, 0.04f, 0.12f, 0f, -0.2f, 0.12f);
        return Mathf.Min(Mathf.Min(arc, cap), Mathf.Min(stem, Circle(x, y, 0f, -0.58f, 0.16f)));
    }
    static float HiQuestion(float x, float y) { return ArcQ(x, y + 0.0f, 0.34f, 0.035f, 105f) + (y > 0.45f && x < 0.0f ? 0f : 1f); }

    static float SdfHeart(float x, float y)
    {
        y += 0.05f;
        float a = Circle(x, y, -0.28f, 0.22f, 0.36f);
        float b = Circle(x, y, 0.28f, 0.22f, 0.36f);
        float qx = (x - y) * 0.7071f, qy = (x + y) * 0.7071f;
        float qd = Mathf.Max(Mathf.Abs(qx) - 0.4f, Mathf.Abs(qy + 0.05f) - 0.4f);
        float box = Mathf.Max(qd, -(y - 0.22f));
        return Mathf.Min(Mathf.Min(a, b), box);
    }

    // four-point twinkle with concave sides
    static float SdfSparkle(float x, float y)
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        return (Mathf.Pow(ax, 0.6f) + Mathf.Pow(ay, 0.6f) - 0.95f) * 0.3f;
    }

    static float SdfDrop(float x, float y)
    {
        float c = Circle(x, y, 0f, -0.25f, 0.42f);
        float h = 0.42f * (0.75f - y) / 0.95f;
        float cone = Mathf.Max(Mathf.Max(Mathf.Abs(x) - h, -(y + 0.25f)), y - 0.75f) * 0.8f;
        return Mathf.Min(c, cone);
    }

    static float SdfZ(float x, float y)
    {
        float r = 0.15f;
        return Mathf.Min(Mathf.Min(Capsule(x, y, -0.42f, 0.5f, 0.42f, 0.5f, r), Capsule(x, y, 0.42f, 0.5f, -0.42f, -0.5f, r)),
                         Capsule(x, y, -0.42f, -0.5f, 0.42f, -0.5f, r));
    }

    static float SdfCloud(float x, float y)
    {
        return Mathf.Min(Mathf.Min(Circle(x, y, -0.42f, -0.02f, 0.34f), Circle(x, y, 0.0f, 0.14f, 0.42f)),
                         Mathf.Min(Circle(x, y, 0.42f, -0.06f, 0.32f), Circle(x, y, 0.0f, -0.18f, 0.4f)));
    }

    // comic anger mark: four tapered, curved strokes around an empty centre
    static float SdfAnger(float x, float y)
    {
        float best = 1f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                float cx = sx * 0.8f, cy = sy * 0.8f, r = 0.6f;
                float phi = Mathf.Atan2(-sy, -sx) * Mathf.Rad2Deg;
                float ang = Mathf.DeltaAngle(phi, Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg);
                if (Mathf.Abs(ang) > 44f) continue;
                float t = (ang + 44f) / 88f;
                float w = 0.03f + 0.14f * Mathf.Sin(t * Mathf.PI);
                float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) - w;
                if (d < best) best = d;
            }
        return best;
    }

    // radiating "surprise" rays over the top half
    static float SdfBurst(float x, float y)
    {
        float best = 1f;
        for (int i = 0; i < 7; i++)
        {
            float a = (20f + i * 23.3f) * Mathf.Deg2Rad;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            float d = RoundCone(x, y, cs * 0.62f, sn * 0.62f - 0.1f, 0.075f, cs * 1.05f, sn * 1.05f - 0.1f, 0.02f);
            if (d < best) best = d;
        }
        return best;
    }

    // ellipsis thought bubble with a little tail
    static float SdfBubble(float x, float y)
    {
        float ex = x / 0.82f, ey = (y - 0.08f) / 0.5f;
        float ell = (Mathf.Sqrt(ex * ex + ey * ey) - 1f) * 0.42f;
        return Mathf.Min(Mathf.Min(ell, Circle(x, y, -0.42f, -0.6f, 0.14f)), Circle(x, y, -0.58f, -0.82f, 0.08f));
    }
}
