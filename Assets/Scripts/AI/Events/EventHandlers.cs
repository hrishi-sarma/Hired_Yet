using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum EventStatus { Running, Success, Fail }

public interface IEventHandler
{
    string Text { get; }
    float Progress { get; }
    void Begin(WorldEventConfig cfg, System.Random rng);
    EventStatus Tick(float dt, Keyboard kb);
}

public static class EventUi
{
    static readonly Key[] DigitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
    static readonly Key[] PadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4 };

    public static int PressedDigit(Keyboard kb, int max)
    {
        if (kb == null) return -1;
        for (int i = 0; i < max && i < DigitKeys.Length; i++)
            if (kb[DigitKeys[i]].wasPressedThisFrame || kb[PadKeys[i]].wasPressedThisFrame) return i;
        return -1;
    }

    public static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T t = list[i]; list[i] = list[j]; list[j] = t;
        }
    }

    public static string Bar(float f)
    {
        int n = 14;
        int filled = Mathf.Clamp(Mathf.RoundToInt(f * n), 0, n);
        return "[" + new string('#', filled) + new string('-', n - filled) + "]";
    }

    public static IEventHandler Create(EventHandlerKind kind)
    {
        switch (kind)
        {
            case EventHandlerKind.Hold: return new HoldHandler();
            case EventHandlerKind.Mash: return new MashHandler();
            case EventHandlerKind.Sequence: return new SequenceHandler();
            case EventHandlerKind.Timing: return new TimingHandler();
            case EventHandlerKind.Choice: return new ChoiceHandler();
            case EventHandlerKind.Sorting: return new SortingHandler();
            case EventHandlerKind.Object: return new OpenHandler();
        }
        return null;
    }
}

public class HoldHandler : IEventHandler
{
    WorldEventConfig cfg;
    float progress;
    public bool Pause;      // couch: no progress until the player has stood up with it

    public string Text { get; private set; }
    public float Progress { get { return cfg != null && cfg.amount > 0f ? Mathf.Clamp01(progress / cfg.amount) : 0f; } }

    public void Begin(WorldEventConfig c, System.Random rng)
    {
        cfg = c;
        progress = 0f;
        Text = "";
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        bool held = kb != null && kb.eKey.isPressed;
        if (!Pause) progress += held ? dt : -dt * 1.5f;
        progress = Mathf.Max(0f, progress);
        Text = "HOLD E  " + EventUi.Bar(progress / cfg.amount);
        return progress >= cfg.amount ? EventStatus.Success : EventStatus.Running;
    }
}

public class MashHandler : IEventHandler
{
    WorldEventConfig cfg;
    int taps;
    bool started;
    float taskStart;

    public string Text { get; private set; }
    public float Progress { get { return cfg != null && cfg.amount > 0f ? Mathf.Clamp01(taps / cfg.amount) : 0f; } }

    public void Begin(WorldEventConfig c, System.Random rng)
    {
        cfg = c;
        taps = 0;
        started = false;
        taskStart = Time.time;
        Text = "";
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        if (kb != null && kb.eKey.wasPressedThisFrame)
        {
            if (!started)
            {
                started = true;
                taskStart = Time.time;
            }
            taps++;
        }
        float left = started ? Difficulty.Limit(cfg.limit) - (Time.time - taskStart) : Difficulty.Limit(cfg.limit);
        Text = (started ? "TAP E  " : "TAP E TO START  ") + EventUi.Bar(taps / cfg.amount) + "  " + Mathf.Max(0f, left).ToString("0.0") + "s";
        if (taps >= cfg.amount) return EventStatus.Success;
        if (started && left <= 0f) return EventStatus.Fail;
        return EventStatus.Running;
    }
}

public class SequenceHandler : IEventHandler
{
    static readonly Key[] ArrowKeys = { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow };
    static readonly string[] ArrowNames = { "UP", "DOWN", "LEFT", "RIGHT" };

    WorldEventConfig cfg;
    int[] seq;
    int step;
    float taskStart;

    public string Text { get; private set; }
    public bool LastOk = true;
    public float Progress { get { return seq != null && seq.Length > 0 ? Mathf.Clamp01(step / (float)seq.Length) : 0f; } }

    public void Begin(WorldEventConfig c, System.Random rng)
    {
        cfg = c;
        step = 0;
        taskStart = Time.time;
        Text = "";
        int n = Mathf.Max(1, Mathf.RoundToInt(c.amount));
        seq = new int[n];
        for (int i = 0; i < n; i++) seq[i] = rng.Next(4);
        EventPanelUI.ShowSequence(c.prompt);
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        if (kb != null)
        {
            for (int k = 0; k < ArrowKeys.Length; k++)
            {
                if (!kb[ArrowKeys[k]].wasPressedThisFrame) continue;
                LastOk = k == seq[step];
                EventPanelUI.Pulse(LastOk);
                if (LastOk) step++;
                else step = Mathf.Max(0, step - 1);
                break;
            }
        }
        float left = Difficulty.Limit(cfg.limit) - (Time.time - taskStart);
        System.Text.StringBuilder sb = new System.Text.StringBuilder("ARROW KEYS  ");
        for (int i = step; i < seq.Length && i < step + 5; i++)
        {
            if (i == step) sb.Append("[").Append(ArrowNames[seq[i]]).Append("]");
            else sb.Append(ArrowNames[seq[i]]);
            sb.Append(" ");
        }
        sb.Append(" (").Append(step).Append("/").Append(seq.Length).Append(")  ").Append(Mathf.Max(0f, left).ToString("0.0")).Append("s");
        Text = sb.ToString();
        EventPanelUI.SetSequence(seq, step, Difficulty.Limit(cfg.limit) > 0f ? left / Difficulty.Limit(cfg.limit) : 1f, step + " / " + seq.Length);
        if (step >= seq.Length) return EventStatus.Success;
        if (left <= 0f) return EventStatus.Fail;
        return EventStatus.Running;
    }
}

/// <summary>Carry-and-deliver for grandma_groceries and delivery_boxes. Stage 1: hold E to lift the items. Stage 2: keep E held while walking
/// to the drop-off spot (the items fall and the task fails the moment E is let go outside it). Releasing E inside the zone sets them down.</summary>
/// <summary>Watering plants: hold E next to a tub until it is completely watered, then move to the next one. All tubs done = success. Progress never drains.</summary>
public class PlantsHandler : IEventHandler
{
    public Transform player;
    public Transform[] tubs = new Transform[0];
    float[] fill;
    float per = 3f;

    /// <summary>Tub being watered right now (E held next to it), or -1.</summary>
    public int Active { get; private set; }
    public string Text { get; private set; }
    public float Progress { get { float s = 0f; for (int i = 0; i < fill.Length; i++) s += Mathf.Min(fill[i], per); return fill.Length > 0 ? s / (per * fill.Length) : 0f; } }
    public float Fill01(int i) { return fill != null && i >= 0 && i < fill.Length ? Mathf.Clamp01(fill[i] / per) : 0f; }

    public void Begin(WorldEventConfig c, System.Random rng)
    {
        per = Mathf.Max(2f, c.amount * 0.55f);
        fill = new float[tubs.Length];
        Active = -1;
        Text = "";
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        bool held = kb != null && kb.eKey.isPressed;
        Active = -1;
        int near = -1, done = 0;
        float best = 1.7f;
        for (int i = 0; i < tubs.Length; i++)
        {
            if (fill[i] >= per) done++;
            if (tubs[i] == null || player == null) continue;
            Vector3 d = tubs[i].position - player.position;
            d.y = 0f;
            if (d.magnitude < best) { best = d.magnitude; near = i; }
        }
        if (done >= tubs.Length) return EventStatus.Success;
        if (near < 0) Text = "GO TO A TUB AND HOLD E  (" + done + "/" + tubs.Length + " WATERED)";
        else if (fill[near] >= per) Text = "THIS TUB IS DONE - MOVE TO THE NEXT  (" + done + "/" + tubs.Length + ")";
        else
        {
            if (held) { fill[near] += dt; Active = near; }
            Text = "HOLD E TO WATER  " + EventUi.Bar(fill[near] / per) + "  (" + done + "/" + tubs.Length + ")";
        }
        return EventStatus.Running;
    }
}

public class DeliveryHandler : IEventHandler
{
    const float PickTime = 0.9f, Grace = 0.2f;
    public Transform player;
    public Vector3 dest;
    public bool canPick = true;     // false while the player is not touching the item (log): no lift progress
    public float radius = 2.5f, startDist = 30f;
    public string liftText = "HOLD E TO LIFT  ", carryText = "KEEP HOLDING E  -  DELIVER TO THE MARKER  ";
    float pick, missed;
    bool aborted;

    public bool Carrying { get; private set; }
    public string Text { get; private set; }
    public float Progress { get { return Carrying ? 1f - Mathf.Clamp01(Dist() / Mathf.Max(1f, startDist)) : Mathf.Clamp01(pick / PickTime); } }

    public float Dist()
    {
        if (player == null) return 0f;
        Vector3 d = player.position - dest;
        d.y = 0f;
        return d.magnitude;
    }

    public void Abort() { aborted = true; }

    public void Begin(WorldEventConfig c, System.Random rng)
    {
        pick = 0f; missed = 0f; aborted = false; Carrying = false; Text = "";
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        bool held = kb != null && kb.eKey.isPressed;
        if (!Carrying)
        {
            pick = Mathf.Max(0f, pick + (held && canPick ? dt : -dt * 1.5f));
            Text = canPick ? liftText + EventUi.Bar(pick / PickTime) : "[ STEP UP TO THE LOG END TO GRAB IT ]";
            if (pick >= PickTime) { Carrying = true; missed = 0f; }
            return EventStatus.Running;
        }
        float dist = Dist();
        bool inZone = dist <= radius;
        missed = held ? 0f : missed + dt;
        Text = inZone ? "[ RELEASE E TO SET DOWN ]" : carryText + Mathf.CeilToInt(dist) + " m";
        if (aborted) return EventStatus.Fail;
        if (missed > Grace) return inZone ? EventStatus.Success : EventStatus.Fail;
        return EventStatus.Running;
    }
}

/// <summary>pigeon_king: one press of E throws breadcrumbs (the player animation tosses them, the flock eats them). Succeeds when they are eaten.</summary>
public class FeedHandler : IEventHandler
{
    public PigeonFlock flock;
    float t, idle;

    public string Text { get; private set; }
    public float Progress { get { return flock != null ? Mathf.Clamp01(1f - flock.Agitation) : 0f; } }

    public void Begin(WorldEventConfig c, System.Random rng) { t = 0f; idle = 0f; Text = ""; }

    /// <summary>The flock starts agitated. The player throws crumbs with E (as often as they like) until the flock has eaten enough to calm down.</summary>
    public EventStatus Tick(float dt, Keyboard kb)
    {
        if (flock == null) return EventStatus.Fail;
        t += dt;
        if (kb != null && kb.eKey.wasPressedThisFrame && t > 0.3f && flock.CanThrow) { flock.StartThrow(); idle = 0f; }
        string bar = Bar(Progress);
        if (flock.Throws == 0)
        {
            Text = "[ PRESS E ] THROW BREADCRUMBS\nPIGEONS CALM " + bar;
            idle += dt;
            return idle > 40f ? EventStatus.Fail : EventStatus.Running;
        }
        if (flock.Holding || !flock.Released) { Text = "SCATTERING BREADCRUMBS\nPIGEONS CALM " + bar; return EventStatus.Running; }
        if (!flock.AllEaten) { Text = "PIGEONS ARE EATING\nPIGEONS CALM " + bar; idle = 0f; return EventStatus.Running; }
        if (flock.Calmed && flock.SettledFor(0.8f)) return EventStatus.Success;
        if (flock.Calmed) { Text = "PIGEONS ARE CALMING DOWN\nPIGEONS CALM " + bar; return EventStatus.Running; }
        Text = (flock.CanThrow ? "[ PRESS E ] THROW MORE CRUMBS" : "GETTING MORE CRUMBS") + "\nPIGEONS CALM " + bar;
        idle += dt;
        return idle > 30f ? EventStatus.Fail : EventStatus.Running;
    }

    static string Bar(float v)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(v * 10f), 0, 10);
        return "[" + new string('#', n) + new string('-', 10 - n) + "]";
    }
}

/// <summary>Pointer sweeps a bar; E must be pressed while it is inside the green zone. N hits win. A miss loses a hit and costs time.
/// The clock and the first sweep start on the first E. Speeds up with every hit.</summary>
public class TimingHandler : IEventHandler
{
    WorldEventConfig cfg;
    TimingParams tp;
    int hits;
    bool started;
    float phase, zMin, zMax, deadline, total, startTime, penalty;
    System.Random rng;

    public string Text { get; private set; }
    public float Progress { get { return tp != null ? Mathf.Clamp01(hits / (float)tp.hits) : 0f; } }
    float Ptr { get { return Mathf.PingPong(phase, 1f); } }

    public void Begin(WorldEventConfig c, System.Random r)
    {
        cfg = c; tp = c.timing != null ? c.timing : new TimingParams(); rng = r;
        hits = 0; started = false; phase = 0f; penalty = 0f; Text = "";
        total = Difficulty.Limit(c.limit > 0f ? c.limit : tp.timeLimit);
        NewZone();
        EventPanelUI.ShowTiming(c.prompt, tp.hits);
    }

    void NewZone()
    {
        float w = Mathf.Clamp(tp.zoneWidth * Difficulty.ZoneMul, 0.05f, 0.5f), cur = Ptr, center = 0.5f;
        for (int i = 0; i < 6; i++)
        {
            center = w * 0.5f + 0.04f + (float)rng.NextDouble() * (1f - w - 0.08f);
            if (Mathf.Abs(center - cur) > 0.25f) break;
        }
        zMin = center - w * 0.5f; zMax = center + w * 0.5f;
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        bool press = kb != null && kb.eKey.wasPressedThisFrame;
        if (!started)
        {
            if (press) { started = true; startTime = Time.time; }
            Text = "PRESS E TO START";
            EventPanelUI.SetTiming(0f, zMin, zMax, 0, 1f, "Press E to start");
            return EventStatus.Running;
        }
        phase += dt * tp.pointerSpeed * Difficulty.PointerMul * (1f + 0.15f * hits);
        float ptr = Ptr;
        if (press)
        {
            bool ok = ptr >= zMin && ptr <= zMax;
            EventPanelUI.Pulse(ok);
            if (ok) { hits++; if (hits < tp.hits) NewZone(); }
            else { hits = Mathf.Max(0, hits - 1); penalty += tp.missPenalty; }
        }
        float left = total - (Time.time - startTime) - penalty;
        Text = "PRESS E IN THE GREEN  " + hits + "/" + tp.hits + "  " + Mathf.Max(0f, left).ToString("0.0") + "s";
        EventPanelUI.SetTiming(ptr, zMin, zMax, hits, left / total, hits + " / " + tp.hits);
        if (hits >= tp.hits) return EventStatus.Success;
        if (left <= 0f) return EventStatus.Fail;
        return EventStatus.Running;
    }
}

public class ChoiceHandler : IEventHandler
{
    const float Feedback = 0.7f;
    WorldEventConfig cfg;
    List<ChoiceQuestion> qs;
    int[] order;
    int qi, wrong, allowed;
    float taskStart, wait;
    bool answered;
    System.Random rng;

    public string Text { get; private set; }
    public float Progress { get { return qs != null && qs.Count > 0 ? Mathf.Clamp01(qi / (float)qs.Count) : 0f; } }

    public void Begin(WorldEventConfig c, System.Random r)
    {
        cfg = c;
        // Fresh randomness every session (the director RNG is seeded from the world seed, which would repeat the same questions).
        rng = new System.Random(System.Guid.NewGuid().GetHashCode());
        ChoiceParams p = c.choice;
        if (p == null || p.questions == null || p.questions.Count == 0)
        {
            WorldEventConfig d = WorldEventDefaults.Find(c.id);
            if (d != null) p = d.choice;
        }
        List<ChoiceQuestion> pool = new List<ChoiceQuestion>();
        if (p != null) foreach (ChoiceQuestion q in p.questions) if (q != null && q.options != null && q.options.Length >= 2) pool.Add(q);
        // Merge the built-in question pool so older saved libraries (which only hold one or two questions) still get the full variety.
        WorldEventConfig defCfg = WorldEventDefaults.Find(c.id);
        if (defCfg != null && defCfg.choice != null)
            foreach (ChoiceQuestion q in defCfg.choice.questions)
            {
                if (q == null || q.options == null || q.options.Length < 2) continue;
                bool dup = false;
                for (int i = 0; i < pool.Count; i++) if (pool[i].question == q.question) { dup = true; break; }
                if (!dup) pool.Add(q);
            }
        EventUi.Shuffle(pool, rng);
        // do not open with the question that was asked last time this event ran (remembered across sessions)
        string key = "toon_lastq_" + c.id;
        int last = PlayerPrefs.GetInt(key, 0);
        if (pool.Count > 1 && pool[0].question.GetHashCode() == last) { ChoiceQuestion t0 = pool[0]; pool[0] = pool[1]; pool[1] = t0; }
        if (pool.Count > 0) PlayerPrefs.SetInt(key, pool[0].question.GetHashCode());
        int n = Mathf.Clamp(p != null ? p.asked : 1, 1, Mathf.Max(1, pool.Count));
        qs = pool.GetRange(0, Mathf.Min(n, pool.Count));
        allowed = qs.Count >= 3 ? 1 : 0;
        qi = 0; wrong = 0; wait = 0f; answered = false;
        taskStart = Time.time;
        Text = "";
        EventPanelUI.ShowChoice(c.prompt, false);
        Shuffle(rng);
    }

    void Shuffle(System.Random rng)
    {
        if (qi >= qs.Count) return;
        order = new int[qs[qi].options.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        EventUi.Shuffle(order, rng);
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        if (qs == null || qs.Count == 0) return EventStatus.Fail;
        float left = Difficulty.Limit(cfg.limit) > 0f ? Difficulty.Limit(cfg.limit) - (Time.time - taskStart) : 1f;
        float frac = Difficulty.Limit(cfg.limit) > 0f ? left / Difficulty.Limit(cfg.limit) : -1f;
        if (answered)
        {
            wait -= dt;
            if (wait <= 0f)
            {
                answered = false;
                if (wrong > allowed) return EventStatus.Fail;
                if (qi >= qs.Count) return EventStatus.Success;
                Shuffle(rng);
            }
            else return EventStatus.Running;
        }
        if (Difficulty.Limit(cfg.limit) > 0f && left <= 0f) return EventStatus.Fail;
        ChoiceQuestion q = qs[qi];
        string[] opts = new string[order.Length];
        for (int i = 0; i < order.Length; i++) opts[i] = (i + 1) + ".  " + q.options[order[i]];
        string info = "PRESS OR CLICK " + (order.Length > 2 ? "1-" + order.Length : "1 OR 2") + "  (" + (qi + 1) + " / " + qs.Count + ")";
        Text = info;
        EventPanelUI.SetChoice(q.question, opts, false, Color.white, frac, info);
        int k = EventUi.PressedDigit(kb, order.Length);
        if (k < 0) k = EventPanelUI.ClickedOption(order.Length);
        if (k >= 0)
        {
            bool ok = order[k] == q.correct;
            if (!ok) wrong++;
            EventPanelUI.Pulse(ok);
            qi++;
            answered = true;
            wait = Feedback;
        }
        return EventStatus.Running;
    }
}

public class SortingHandler : IEventHandler
{
    const float Feedback = 0.45f;
    WorldEventConfig cfg;
    SortingParams sp;
    List<SortItem> items;
    int idx, wrong;
    float wait, taskStart;
    bool answered;

    public string Text { get; private set; }
    public float Progress { get { return items != null && items.Count > 0 ? Mathf.Clamp01(idx / (float)items.Count) : 0f; } }

    public void Begin(WorldEventConfig c, System.Random rng)
    {
        cfg = c;
        sp = c.sorting;
        if (sp == null || sp.items == null || sp.items.Count == 0)
        {
            WorldEventConfig d = WorldEventDefaults.Find(c.id);
            if (d != null) sp = d.sorting;
        }
        List<SortItem> pool = new List<SortItem>(sp.items);
        EventUi.Shuffle(pool, rng);
        int n = Mathf.Clamp(Mathf.RoundToInt(c.amount), 1, pool.Count);
        items = pool.GetRange(0, n);
        idx = 0; wrong = 0; wait = 0f; answered = false;
        taskStart = Time.time;
        Text = "";
        EventPanelUI.ShowChoice(c.prompt, true);
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        if (items == null || items.Count == 0) return EventStatus.Fail;
        float left = Difficulty.Limit(cfg.limit) > 0f ? Difficulty.Limit(cfg.limit) - (Time.time - taskStart) : 1f;
        float frac = Difficulty.Limit(cfg.limit) > 0f ? left / Difficulty.Limit(cfg.limit) : -1f;
        if (answered)
        {
            wait -= dt;
            if (wait > 0f) return EventStatus.Running;
            answered = false;
            if (wrong > sp.allowedMistakes) return EventStatus.Fail;
            if (idx >= items.Count) return EventStatus.Success;
        }
        if (Difficulty.Limit(cfg.limit) > 0f && left <= 0f) return EventStatus.Fail;
        SortItem it = items[idx];
        string[] opts = new string[sp.categories.Length];
        for (int i = 0; i < opts.Length; i++) opts[i] = (i + 1) + ".  " + sp.categories[i];
        string info = "PRESS OR CLICK 1 OR 2  (" + (idx + 1) + " / " + items.Count + ")  MISTAKES " + wrong + " / " + (sp.allowedMistakes + 1);
        Text = info;
        EventPanelUI.SetChoice(it.name, opts, true, it.color, frac, info);
        int k = EventUi.PressedDigit(kb, opts.Length);
        if (k < 0) k = EventPanelUI.ClickedOption(opts.Length);
        if (k >= 0)
        {
            bool ok = k == it.category;
            if (!ok) wrong++;
            EventPanelUI.Pulse(ok);
            idx++;
            answered = true;
            wait = Feedback;
        }
        return EventStatus.Running;
    }
}
