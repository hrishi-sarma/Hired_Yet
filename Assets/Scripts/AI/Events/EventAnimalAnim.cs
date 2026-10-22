using UnityEngine;

/// <summary>Procedural animation for the event dog and deer, driven by the WorldEvent state.
/// Dog: angry (low, shaking, snapping, ears pinned) that eases to calm (wagging, panting) as the player succeeds, then sits.
/// Deer: lying in pain (laboured breathing, weak kicks, failed attempts to rise) that eases off with the healing; on success it
/// gets up and is handed over to the normal AnimalAgent so it wanders and grazes like the other deer.</summary>
public class EventAnimalAnim : MonoBehaviour
{
    const int PITCH = 0, ROLL = 1, NECK = 2, HEAD = 3, HYAW = 4, HROLL = 5, TYAW = 6, TPITCH = 7, EARL = 8, EARR = 9, TONGUE = 10, EYE = 11, L0 = 12;
    AnimalRig rig;
    AnimalSpecies sp;
    Transform groundRef, holder;
    WorldEvent ev;
    readonly float[] cur = new float[16], tgt = new float[16];
    float anger = 1f, sit, getUp, seed, lunge;
    bool handed, tried, graze;

    public void Setup(AnimalRig r, AnimalSpecies s, Transform ground)
    {
        rig = r; sp = s; groundRef = ground; holder = r.visual.parent;
        seed = Random.value * 10f;
        cur[EYE] = 1f;
        if (s == AnimalSpecies.Deer) cur[ROLL] = 90f;
    }

    static float Pulse(float x, float p) { return Mathf.Pow(Mathf.Max(0f, Mathf.Sin(x)), p); }
    static float Back(float u) { float c = 1.4f; u -= 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

    void LateUpdate()
    {
        if (rig == null || handed) return;
        if (ev == null) { ev = GetComponentInParent<WorldEvent>(); if (ev == null) return; }
        float dt = Time.deltaTime, t = Time.time + seed;
        bool won = ev.Finished && ev.Succeeded;
        float want = won ? 0f : (ev.Working ? 1f - ev.Progress : 1f);
        anger = Mathf.MoveTowards(anger, want, dt * (won ? 0.8f : 0.5f));
        System.Array.Clear(tgt, 0, tgt.Length);
        tgt[EYE] = 1f;
        lunge = 0f;
        if (sp == AnimalSpecies.Dog) Dog(t, dt, won); else Deer(t, dt, won);
        float k = 1f - Mathf.Exp(-dt * (sp == AnimalSpecies.Deer ? 5f : 8f));
        for (int i = 0; i < cur.Length; i++) cur[i] = Mathf.Lerp(cur[i], tgt[i], k);
        Apply();
        if (sp == AnimalSpecies.Dog) Face(dt);
    }

    void Dog(float t, float dt, bool won)
    {
        float a = anger, c = 1f - a;
        sit = Mathf.MoveTowards(sit, won ? 1f : 0f, dt * 0.7f);
        float shake = (Mathf.PerlinNoise(t * 18f, 1f) - 0.5f) * 2f;
        float snap = Pulse(t * 2.4f, 8f);
        lunge = snap * 0.1f * a * (1f - sit);
        tgt[PITCH] = Mathf.Lerp(8f * a + shake * 1.5f * a, -26f, sit);
        tgt[HEAD] = Mathf.Lerp(16f * a + snap * 14f * a - 3f * c, 14f, sit);
        tgt[HYAW] = Mathf.Lerp(Mathf.Sin(t * 0.9f) * 20f * c + shake * 6f * a, Mathf.Sin(t * 1.1f) * 25f, sit);
        tgt[HROLL] = shake * 4f * a;
        tgt[TPITCH] = Mathf.Lerp(35f * a + 8f * c, 0f, sit);
        tgt[TYAW] = shake * 5f * a + Mathf.Sin(t * 10f) * Mathf.Clamp01((c - 0.45f) * 2f) * 28f;
        tgt[EARL] = tgt[EARR] = 28f * a;
        tgt[TONGUE] = Mathf.Clamp01(c - 0.3f) * (0.7f + 0.2f * Mathf.Sin(t * 9f));
        tgt[EYE] = Mathf.Lerp(1f, 0.85f, c);
        tgt[L0] = tgt[L0 + 1] = -6f * a;
        tgt[L0 + 2] = tgt[L0 + 3] = -55f * sit;
    }

    void Deer(float t, float dt, bool won)
    {
        float a = anger, c = 1f - a;
        float breathe = Mathf.Sin(t * (2.2f + 2.5f * a));
        float spasm = Pulse(t * 0.8f + 1f, 3f) * a, strug = Pulse(t * 0.35f, 6f) * a;
        float noise = Mathf.PerlinNoise(t * 3f, 2f) - 0.5f;
        if (!won)
        {
            tgt[ROLL] = 90f - 28f * strug + breathe * 2.2f * (0.4f + a);
            tgt[PITCH] = noise * 6f * spasm;
            tgt[NECK] = Mathf.Lerp(8f, -18f, strug * 0.7f + c * 0.3f) + breathe * 2f;
            tgt[HEAD] = 10f * a;
            tgt[HROLL] = noise * 50f * spasm;
            tgt[HYAW] = Mathf.Sin(t * 5f) * 18f * spasm + Mathf.Sin(t * 0.6f) * 10f * a;
            for (int i = 0; i < 4; i++) tgt[L0 + i] = Mathf.Sin(t * (7f + i) + i * 1.7f) * 28f * spasm + breathe * 3f;
            tgt[EARL] = tgt[EARR] = 24f * a;
            tgt[EYE] = Mathf.Lerp(1f, 0.25f, a);
            tgt[TPITCH] = -10f * a;
            tgt[TYAW] = noise * 40f * spasm;
            return;
        }
        getUp += dt;
        float u = Mathf.Clamp01((getUp - 0.8f) / 2.2f);
        tgt[ROLL] = 90f * (1f - Back(u));
        tgt[NECK] = Mathf.Lerp(-12f, 0f, u);
        for (int i = 0; i < 4; i++) tgt[L0 + i] = getUp > 0.8f ? Mathf.Sin(getUp * 9f + i * 1.9f) * 20f * (1f - u) : 0f;
        if (getUp > 3.4f && !tried) Handover();
        if (graze) tgt[NECK] = 35f * Mathf.Clamp01(Mathf.Sin(t * 0.4f) * 2f);
    }

    // the healed deer becomes a normal wandering deer; without an animal controller it stays and grazes
    void Handover()
    {
        tried = true;
        AnimalWorldController wc = FindObjectOfType<AnimalWorldController>();
        Vector3 pos = holder.position;
        pos.y = groundRef.position.y;
        if (wc != null && wc.Adopt(AnimalSpecies.Deer, pos, holder.eulerAngles.y) != null)
        {
            handed = true;
            WorldUtil.Kill(holder.gameObject);
            return;
        }
        graze = true;
    }

    void Face(float dt)
    {
        NpcDirector d = NpcDirector.Instance;
        if (d == null || d.Player == null) return;
        Vector3 to = d.Player.position - holder.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.04f || to.sqrMagnitude > 64f) return;
        holder.rotation = Quaternion.RotateTowards(holder.rotation, Quaternion.LookRotation(to), 140f * dt);
    }

    void Apply()
    {
        Transform v = rig.visual;
        v.localPosition = new Vector3(0f, 0f, lunge);
        v.localRotation = Quaternion.Euler(cur[PITCH], 0f, cur[ROLL]);
        for (int i = 0; i < 4 && i < rig.legs.Length; i++) rig.legs[i].localRotation = Quaternion.Euler(cur[L0 + i] - cur[PITCH], 0f, 0f);
        if (rig.neck != null) rig.neck.localRotation = rig.neckHome * Quaternion.Euler(cur[NECK], 0f, 0f);
        rig.head.localRotation = rig.headHome * Quaternion.Euler(cur[HEAD], cur[HYAW], cur[HROLL]);
        rig.tail.localRotation = Quaternion.Euler(0f, cur[TYAW], 0f) * rig.tailHome * Quaternion.Euler(cur[TPITCH], 0f, 0f);
        for (int i = 0; i < rig.tailSegments.Length; i++)
            rig.tailSegments[i].localRotation = rig.tailSegmentHomeRotations[i] * Quaternion.Euler(Mathf.Sin(Time.time * 2.6f - i * 0.9f) * 6f, 0f, 0f);
        if (rig.ears.Length > 1 && rig.ears[0] != null && rig.ears[1] != null)
        {
            rig.ears[0].localRotation = rig.earHome[0] * Quaternion.Euler(cur[EARL], 0f, 0f);
            rig.ears[1].localRotation = rig.earHome[1] * Quaternion.Euler(cur[EARR], 0f, 0f);
        }
        for (int i = 0; i < rig.eyes.Length; i++) if (rig.eyes[i] != null) rig.eyes[i].localScale = new Vector3(1f, Mathf.Max(0.08f, cur[EYE]), 1f);
        if (rig.tongue != null) rig.tongue.localScale = Vector3.one * Mathf.Max(0.001f, cur[TONGUE]);
        float low = float.MaxValue;
        for (int i = 0; i < rig.feet.Length; i++) if (rig.feet[i] != null) low = Mathf.Min(low, rig.feet[i].position.y);
        for (int i = 0; i < rig.contacts.Length; i++) if (rig.contacts[i] != null) low = Mathf.Min(low, rig.contacts[i].position.y);
        if (low != float.MaxValue) v.position += Vector3.up * (groundRef.position.y - low);
    }
}
