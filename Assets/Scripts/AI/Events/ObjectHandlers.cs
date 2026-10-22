using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>lost_backpack: hold E to open the backpack (seconds from config amount, default Objects.holdSeconds).</summary>
public class OpenHandler : IEventHandler
{
    float need = 2f, progress;
    public string label = "OPEN THE BACKPACK";
    public string Text { get; private set; }
    public float Progress { get { return Mathf.Clamp01(progress / need); } }

    public void Begin(WorldEventConfig c, System.Random rng)
    {
        need = c.amount > 0f ? c.amount : (c.objects != null ? c.objects.holdSeconds : 2f);
        progress = 0f; Text = "";
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        bool held = kb != null && kb.eKey.isPressed;
        progress = Mathf.Max(0f, progress + (held ? dt : -dt * 1.5f));
        Text = "HOLD E TO " + label + "  " + EventUi.Bar(progress / need);
        return progress >= need ? EventStatus.Success : EventStatus.Running;
    }
}

/// <summary>Three bystanders (built by WorldEventDirector) with name plates; one owns the wallet.</summary>
public class WalletSetup
{
    public List<Transform> npcs = new List<Transform>();
    public List<string> names = new List<string>();
    public List<FloatingLabel> plates = new List<FloatingLabel>();
    public void ShowPlates(bool on) { foreach (FloatingLabel p in plates) if (p != null) p.gameObject.SetActive(on); }
}

/// <summary>lost_wallet: hold E to read the ID, then pick the owner (keys 1-3) among the bystanders, whose names float above them.</summary>
public class WalletHandler : IEventHandler
{
    const float Inspect = 2f, Feedback = 0.8f;
    public WalletSetup setup;
    WorldEventConfig cfg;
    float hold, wait;
    int owner, stage;
    bool answered, correct;

    public string Text { get; private set; }
    public float Progress { get { return stage == 0 ? Mathf.Clamp01(hold / Inspect) * 0.5f : 0.5f; } }

    public void Begin(WorldEventConfig c, System.Random rng)
    {
        cfg = c; hold = 0f; stage = 0; answered = false; Text = "";
        owner = setup != null && setup.names.Count > 0 ? rng.Next(setup.names.Count) : 0;
        if (setup != null) setup.ShowPlates(false);
    }

    public EventStatus Tick(float dt, Keyboard kb)
    {
        if (setup == null || setup.names.Count < 2) return EventStatus.Fail;
        if (stage == 0)
        {
            bool held = kb != null && kb.eKey.isPressed;
            hold = Mathf.Max(0f, hold + (held ? dt : -dt * 1.5f));
            Text = "HOLD E TO CHECK THE WALLET'S ID  " + EventUi.Bar(hold / Inspect);
            if (hold >= Inspect)
            {
                stage = 1;
                setup.ShowPlates(true);
                EventPanelUI.ShowChoice(cfg.prompt, false);
            }
            return EventStatus.Running;
        }
        if (answered)
        {
            wait -= dt;
            return wait > 0f ? EventStatus.Running : (correct ? EventStatus.Success : EventStatus.Fail);
        }
        int n = setup.names.Count;
        string[] opts = new string[n];
        for (int i = 0; i < n; i++) opts[i] = (i + 1) + ".  " + setup.names[i].ToUpperInvariant();
        string info = "PRESS OR CLICK 1-" + n;
        Text = info;
        EventPanelUI.SetChoice("The ID inside reads \"" + setup.names[owner].ToUpperInvariant() + "\".  Who owns this wallet?", opts, false, Color.white, -1f, info);
        int k = EventUi.PressedDigit(kb, n);
        if (k < 0) k = EventPanelUI.ClickedOption(n);
        if (k >= 0)
        {
            correct = k == owner;
            EventPanelUI.Pulse(correct);
            answered = true; wait = Feedback;
        }
        return EventStatus.Running;
    }
}
