using System.Collections.Generic;
using UnityEngine;

public class PickpocketTarget : MonoBehaviour, IInteractable
{
    static readonly List<PickpocketTarget> targets = new List<PickpocketTarget>();

    Pedestrian ped;
    Items.ItemInstance item;
    bool flagged;
    bool busy;

    public static int ActiveCount
    {
        get { return targets.Count; }
    }

    public Items.ItemInstance Item
    {
        get { return item; }
    }

    public bool Flagged
    {
        get { return flagged; }
    }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    public float Range
    {
        get
        {
            NpcDirector d = NpcDirector.Instance;
            return d != null ? d.settings.pickpocket.triggerRadius : 1.6f;
        }
    }

    public string Prompt
    {
        get { return "[ PRESS E TO PICKPOCKET ]"; }
    }

    public bool CanInteract
    {
        get { return flagged && !busy && !PlayerInventory.PickpocketLocked && ped != null && !ped.Pooled && ped.CanBePickpocketed; }
    }

    public static PickpocketTarget Flag(Pedestrian p, Items.ItemInstance def)
    {
        if (p == null || def == null) return null;
        PickpocketTarget t = p.GetComponent<PickpocketTarget>();
        if (t == null) t = p.gameObject.AddComponent<PickpocketTarget>();
        t.ped = p;
        t.item = def;
        if (!t.flagged)
        {
            t.flagged = true;
            targets.Add(t);
            if (InteractionSystem.Instance != null) InteractionSystem.Instance.Register(t);
        }
        return t;
    }

    public void Unflag()
    {
        if (flagged)
        {
            flagged = false;
            targets.Remove(this);
            if (InteractionSystem.Instance != null) InteractionSystem.Instance.Unregister(this);
        }
        item = null;
    }

    void OnDestroy()
    {
        Unflag();
    }

    public void Interact()
    {
        if (!CanInteract) return;
        NpcDirector d = NpcDirector.Instance;
        if (d == null) return;
        PickpocketSettings st = d.settings.pickpocket;
        bool success = d.Rng.NextDouble() < st.successChance * Difficulty.PickpocketMul;   // higher experience level = harder
        Items.ItemInstance got = item;
        Pedestrian p = ped;
        Vector3 player = d.Player.position;
        System.Action apply = delegate { Apply(success, got, p, player, st); };
        if (FPAnimation.FPEvents.HasListeners)
        {
            busy = true;
            FPAnimation.FPEvents.RaisePickpocketAttempt(transform, success, apply);
        }
        else apply();
    }

    void Apply(bool success, Items.ItemInstance got, Pedestrian p, Vector3 player, PickpocketSettings st)
    {
        busy = false;
        if (success)
        {
            PlayerInventory.PickpocketLocked = true;
            List<PickpocketTarget> all = new List<PickpocketTarget>(targets);
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null) all[i].Unflag();
            // Stage 9: stored + held with a toast, or dropped at the player's feet with the swap UI when full.
            Items.ItemPickups.Offer(got);
        }
        else
        {
            Unflag();
            if (p != null) { p.PlayCaughtVoice(); p.Alert(player, st.alertSeconds); }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!flagged) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Range);
    }
}
