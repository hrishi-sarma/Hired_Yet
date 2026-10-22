using System.Collections.Generic;
using UnityEngine;

/// <summary>Per-car temperament: profile + impairment + a few remembered events -> mood (stress 0..1).</summary>
public class DriverMind
{
    struct Mem
    {
        public MemEvent type;
        public float time;
    }

    const int Slots = 4;
    readonly Mem[] mem = new Mem[Slots];
    int count;
    int head;

    public DriverProfile profile;
    public Impairment impair;
    public float stress;
    public float aggro;
    public string last = "-";

    public void Init(DriverProfile p, Impairment imp)
    {
        profile = p;
        impair = imp;
        p.Resolve();
        aggro = Mathf.Clamp01(p.overtakeEagerness * 0.5f + p.tailgate * 0.3f + p.runRedChance);
        stress = aggro * 0.25f;
        count = 0;
        head = 0;
        last = "-";
    }

    public void Note(MemEvent e)
    {
        mem[head].type = e;
        mem[head].time = Time.time;
        head = (head + 1) % Slots;
        if (count < Slots) count++;
        last = e + " " + Time.time.ToString("F0") + "s";
    }

    static float Weight(MemEvent e)
    {
        switch (e)
        {
            case MemEvent.CutOff: return 0.35f;
            case MemEvent.NearMiss: return 0.5f;
            case MemEvent.Blocked: return 0.2f;
            case MemEvent.Honked: return 0.25f;
            default: return 0.05f;
        }
    }

    float MemoryStress()
    {
        float sum = 0f;
        for (int i = 0; i < count; i++)
            sum += Weight(mem[i].type) * Mathf.Exp(-(Time.time - mem[i].time) / 45f);
        return Mathf.Clamp01(sum);
    }

    public void Tick(float dt, float areaMood, bool blocked, bool rush)
    {
        float target = aggro * 0.25f + MemoryStress() * 0.55f + areaMood * 0.45f + (blocked ? 0.25f : 0f) + (rush ? 0.08f : 0f);
        if (impair == Impairment.Sleepy) target *= 0.5f;
        stress = Mathf.Lerp(stress, Mathf.Clamp01(target), 1f - Mathf.Exp(-0.6f * dt));
    }

    public float Patience { get { return profile.patience * Mathf.Lerp(1.15f, 0.45f, stress) * (impair == Impairment.Sleepy ? 1.5f : 1f); } }

    public float SpeedMul
    {
        get
        {
            float m = 1f + stress * 0.14f;
            if (impair == Impairment.Sleepy) m *= 0.82f;
            return m;
        }
    }

    public float HeadwayMul { get { return Mathf.Lerp(1f, 0.55f, profile.tailgate * Mathf.Clamp01(0.4f + stress)); } }

    public float HonkTempo { get { return Mathf.Lerp(1.15f, 0.55f, stress); } }

    public float RunRedChance(float scale, bool late, float quiet)
    {
        float c = profile.runRedChance * (0.5f + stress * 1.5f) * scale * Mathf.Lerp(0.7f, 1.5f, quiet);
        if (late) c *= 1.5f;
        if (impair == Impairment.Drunk) c += 0.35f;
        else if (impair == Impairment.Sleepy) c += 0.12f;
        return Mathf.Clamp01(c);
    }

    public float OvertakeAfter()
    {
        return Mathf.Lerp(14f, 2.5f, Mathf.Clamp01(profile.overtakeEagerness * (0.6f + stress)));
    }

    public string Label()
    {
        return profile.name + (impair != Impairment.None ? " (" + impair + ")" : "");
    }
}

/// <summary>Shared road mood: honks, near misses and jams raise stress in a neighbourhood; it decays over time.</summary>
public static class TrafficMood
{
    struct Cell
    {
        public float v;
        public float t;
    }

    const float CellSize = 48f;
    const float Decay = 25f;
    static readonly Dictionary<long, Cell> cells = new Dictionary<long, Cell>();
    static float nextClean;

    static long Key(int x, int z)
    {
        return ((long)x << 32) | (long)(uint)z;
    }

    static float Value(Cell c)
    {
        return c.v * Mathf.Exp(-(Time.time - c.t) / Decay);
    }

    static void AddCell(int x, int z, float amt)
    {
        long k = Key(x, z);
        Cell c;
        if (!cells.TryGetValue(k, out c)) c = new Cell();
        c.v = Mathf.Min(1.5f, Value(c) + amt);
        c.t = Time.time;
        cells[k] = c;
    }

    public static void Add(Vector2 p, float amt)
    {
        int x = Mathf.FloorToInt(p.x / CellSize);
        int z = Mathf.FloorToInt(p.y / CellSize);
        AddCell(x, z, amt);
        AddCell(x + 1, z, amt * 0.35f);
        AddCell(x - 1, z, amt * 0.35f);
        AddCell(x, z + 1, amt * 0.35f);
        AddCell(x, z - 1, amt * 0.35f);
        if (Time.time >= nextClean && cells.Count > 300) Clean();
    }

    public static float Get(Vector2 p)
    {
        Cell c;
        if (!cells.TryGetValue(Key(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.y / CellSize)), out c)) return 0f;
        return Mathf.Clamp01(Value(c));
    }

    static void Clean()
    {
        nextClean = Time.time + 30f;
        List<long> dead = new List<long>();
        foreach (KeyValuePair<long, Cell> kv in cells)
            if (Value(kv.Value) < 0.02f) dead.Add(kv.Key);
        for (int i = 0; i < dead.Count; i++) cells.Remove(dead[i]);
    }

    public static void Reset()
    {
        cells.Clear();
    }
}
