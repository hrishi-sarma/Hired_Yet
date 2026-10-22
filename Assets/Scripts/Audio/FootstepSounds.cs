using System.Collections.Generic;
using FPAnimation;
using UnityEngine;

/// <summary>Plays a footstep on every FPLegsAnimator.FootDown. Surface comes from a downward raycast (collider/parent names and material name), cached per collider.
/// Clips: Assets/Audio/Footsteps/&lt;Surface&gt;/*.wav override the generated ones when present.</summary>
public class FootstepSounds : MonoBehaviour
{
    public int variants = 4;
    [Range(0f, 1f)] public float volume = 0.8f;
    public float pitchJitter = 0.03f;

    readonly Dictionary<Collider, Surface> cache = new Dictionary<Collider, Surface>();
    readonly AudioClip[][] gen = new AudioClip[8][];
    readonly int[] last = new int[8];
    Surface current = Surface.Concrete;
    public Surface Current { get { return current; } }

    void OnEnable() { FPLegsAnimator.FootDown += OnFoot; for (int i = 0; i < last.Length; i++) last[i] = -1; }
    void OnDisable() { FPLegsAnimator.FootDown -= OnFoot; }

    void OnFoot(bool left, float intensity)
    {
        Transform p = AudioDirector.Player;
        if (p == null) return;
        current = Detect(p.position);
        AudioClip clip = Pick(current);
        if (clip == null) return;
        float v = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(intensity)) * volume;
        float pitch = (left ? 0.985f : 1.015f) * (1f + Random.Range(-pitchJitter, pitchJitter));
        AudioDirector.PlayAt(clip, p.position, "footsteps", v, pitch, 2, 2f, 20f);
    }

    AudioClip Pick(Surface s)
    {
        SoundCatalog c = SoundCatalog.Instance;
        if (c != null)
        {
            List<SoundEntry> l = c.Get("footsteps", s.ToString());
            if (l.Count > 0) return SoundCatalog.Pick(l, ref last[(int)s]);
        }
        AudioClip[] g = gen[(int)s];
        if (g == null) g = gen[(int)s] = ProceduralSfx.Footsteps(s, Mathf.Max(1, variants));
        return SoundCatalog.PickClip(g, ref last[(int)s]);
    }

    Surface Detect(Vector3 pos)
    {
        RaycastHit h;
        if (!Physics.Raycast(pos + Vector3.up, Vector3.down, out h, 3f, ~0, QueryTriggerInteraction.Ignore)) return ZoneDefault();
        Surface s;
        if (cache.TryGetValue(h.collider, out s) && !(h.collider is TerrainCollider)) return s;
        s = Classify(h.collider);
        if (!(h.collider is TerrainCollider)) cache[h.collider] = s;
        return s;
    }

    static Surface ZoneDefault()
    {
        ZoneType z = AudioDirector.Zone;
        return z == ZoneType.Forest || z == ZoneType.Mountain ? Surface.Dirt : (z == ZoneType.Open ? Surface.Grass : Surface.Concrete);
    }

    static bool Has(string s, params string[] k)
    {
        for (int i = 0; i < k.Length; i++) if (s.Contains(k[i])) return true;
        return false;
    }

    static Surface Classify(Collider col)
    {
        if (col is TerrainCollider) return ZoneDefault() == Surface.Concrete ? Surface.Grass : ZoneDefault();
        string n = "";
        Transform t = col.transform;
        for (int i = 0; i < 3 && t != null; i++, t = t.parent) n += t.name.ToLowerInvariant() + "|";
        string own = col.name.ToLowerInvariant();
        Renderer r = col.GetComponent<Renderer>();
        if (r != null && r.sharedMaterial != null) { string m = r.sharedMaterial.name.ToLowerInvariant(); n += m; own += "|" + m; }
        // grass areas (e.g. lawns inside the water park) always win, judged by the object's own name/material
        if (Has(own, "grass", "lawn", "turf", "meadow")) return Surface.Grass;
        // "WaterPark" in a parent name must not turn every surface in the park into water
        string w = n.Replace("waterpark", "").Replace("water_park", "").Replace("water park", "").Replace("water-park", "");
        if (Has(w, "water", "river", "pond", "puddle")) return Surface.Water;
        if (Has(n, "metal", "steel", "grate", "manhole")) return Surface.Metal;
        if (Has(n, "wood", "plank", "deck", "porch", "bridge")) return Surface.Wood;
        if (Has(n, "road", "asphalt", "highway", "parking", "crosswalk")) return Surface.Asphalt;
        if (Has(n, "sidewalk", "concrete", "plaza", "pavement", "curb", "stair")) return Surface.Concrete;
        if (Has(n, "grass", "lots", "lawn", "park", "meadow", "ground")) return Surface.Grass;
        if (Has(n, "dirt", "path", "trail", "gravel", "sand", "mud")) return Surface.Dirt;
        if (Has(n, "floor", "carpet", "office", "interview", "room", "rug")) return Surface.Indoor;
        return ZoneDefault();
    }
}
