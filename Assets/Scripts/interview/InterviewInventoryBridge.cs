using System;
using System.Collections.Generic;
using UnityEngine;
using Items;

/// <summary>
/// Shared interview-facing access to the single Items.Inventory. The old PlayerInventory
/// class is only a compatibility facade; it no longer owns item storage.
/// </summary>
public static class InterviewInventoryBridge
{
    static bool initialized;
    static readonly List<string> presentedNames = new List<string>();
    public static event Action<int> OnSpecialItemShown;

    static int roundSuspicion;
    static readonly Dictionary<ItemInstance, bool> bluffRolls = new Dictionary<ItemInstance, bool>();
    public static void BeginInterview() { presentedNames.Clear(); roundSuspicion = 0; bluffRolls.Clear(); }

    /// <summary>Applies the per-round cap to newly added document suspicion.</summary>
    public static int Cap(int add)
    {
        int room = Mathf.Max(0, Items.SuspicionConfig.RoundCap - roundSuspicion);
        int a = Mathf.Min(add, room);
        roundSuspicion += Mathf.Max(0, a);
        return a;
    }
    public static void AdjustRound(int d) { roundSuspicion = Mathf.Max(0, roundSuspicion + d); }
    // any document whose name is wrong / missing / scratched / unreadable makes the interviewer ask about it
    public static bool IsBluffable(ItemInstance i)
    {
        return i != null && !i.isPlayerStarter &&
               (i.nameState == NameState.Other || i.nameState == NameState.Blank || i.nameState == NameState.Scribbled || i.nameState == NameState.Illegible);
    }
    public static string QuestionFor(ItemInstance i)
    {
        switch (i.nameState)
        {
            case NameState.Blank: return "There's no name on this. Whose document is it?";
            case NameState.Scribbled: return "Why is there a name scratched out on this?";
            case NameState.Illegible: return "This is crumpled and I can't read the name. Is it yours?";
            default: return "This name isn't yours?";
        }
    }
    // successful bluff drops the item's suspicion by one tier (Other/Scribbled -> Blank, Blank -> Illegible, Illegible -> 0)
    public static int BluffReduction(ItemInstance i)
    {
        int cur = Items.SuspicionConfig.For(i), to;
        switch (i.nameState)
        {
            case NameState.Other: case NameState.Scribbled: to = Items.SuspicionConfig.Blank; break;
            case NameState.Blank: to = Items.SuspicionConfig.Illegible; break;
            default: to = 0; break;
        }
        return Mathf.Min(to, cur) - cur;
    }
    public static bool HasBluffRoll(ItemInstance i) { return i != null && bluffRolls.ContainsKey(i); }
    public static void SetBluffRoll(ItemInstance i, bool ok) { if (i != null) bluffRolls[i] = ok; }
    public static int RegisterPresented(ItemInstance item)
    {
        if (item == null || item.nameState == NameState.Illegible || string.IsNullOrEmpty(item.printedName)) return 0;
        string name = item.printedName.Trim();
        int penalty = 0;
        for (int i = 0; i < presentedNames.Count; i++)
            if (!string.Equals(presentedNames[i], name, StringComparison.OrdinalIgnoreCase))
            { penalty = Items.SuspicionConfig.CrossCheckMismatch; break; }
        if (!presentedNames.Contains(name)) presentedNames.Add(name);
        return penalty;
    }

    public static Inventory Current
    {
        get { EnsureInitialized(); return Inventory.Current; }
    }

    public static void EnsureInitialized()
    {
        if (initialized) return;
        // Preserve anything already placed in the new inventory by another system.
        if (Inventory.Current.Count == 0) Inventory.NewGame();
        initialized = true;
    }


    /// <summary>Stage 9 API for event/NPC/world reward sources. Never silently replaces a held item.</summary>
    public static bool GrantItem(int defId, int seed = -1)
    {
        EnsureInitialized();
        Items.ItemInstance instance = Items.ItemFactory.Create(defId, seed);
        int slot;
        bool added = Current.TryAdd(instance, out slot);
        if (!added)
            Debug.LogWarning("Inventory full: could not grant " + (instance != null ? instance.def.name : ("item " + defId)) + ". Route this reward through the inventory swap prompt.");
        return added;
    }

    /// <summary>Grant a generated instance while preserving its seed, name state and rolled stats.</summary>
    public static bool GrantInstance(Items.ItemInstance instance)
    {
        if (instance == null) return false;
        EnsureInitialized();
        int slot;
        bool added = Current.TryAdd(instance, out slot);
        if (!added) Debug.LogWarning("Inventory full: reward requires a swap: " + instance.def.name);
        return added;
    }

    public static ItemInstance Get(int slot) { return Current.Get(slot); }

    public static bool HasResume
    {
        get
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                ItemInstance item = Get(i);
                if (item != null && item.def.docType == DocType.Resume) return true;
            }
            return false;
        }
    }

    public static bool Satisfies(ItemInstance item, string tag)
    {
        if (item == null || string.IsNullOrEmpty(tag)) return false;
        string t = tag.Trim().ToLowerInvariant();
        string n = item.def.name.ToLowerInvariant();
        switch (t)
        {
            case "resume": return item.def.docType == DocType.Resume;
            case "certification": return item.def.docType == DocType.Certificate || n.Contains("certificate");
            case "reference": return n.Contains("reference") || n.Contains("business card") || n.Contains("referral");
            case "id": return n.Contains("id card");
            case "portfolio": return item.def.form == ItemForm.Booklet || n.Contains("portfolio") || n.Contains("project report");
            case "demo": return item.def.form == ItemForm.Object || item.def.form == ItemForm.Card ||
                                item.def.docType == DocType.CoverLetter || item.def.docType == DocType.Certificate ||
                                item.def.form == ItemForm.Booklet || item.def.name.Contains("Report");
            default: return n.Contains(t);
        }
    }

    public static float Credit(ItemInstance item, JobData job)
    {
        if (item == null) return 0f;
        var s = item.Stats;
        float value = (s.quality * .035f + s.relevance * .045f + s.credibility * .035f + s.presentation * .02f);
        if (item.def.docType == DocType.Resume) value += 2f;
        if (item.def.docType == DocType.Certificate || item.def.form == ItemForm.Booklet) value += 1f;
        return Mathf.Clamp(value, 1f, 10f);
    }

    public static int Suspicion(ItemInstance item) { return Items.SuspicionConfig.For(item); }

    public static void NotifyShown(ItemInstance item)
    {
        if (item == null) return;
        if (item.def.id == 19 || item.def.id == 20)
        {
            Action<int> callback = OnSpecialItemShown;
            if (callback != null) callback(item.def.id);
            Debug.Log("Stage 8 special item shown: " + item.def.name);
        }
    }

    public static ItemInstance FromLegacy(global::ItemDef legacy)
    {
        if (legacy == null) return null;
        int id = MatchLegacy(legacy);
        if (id < 0) return null;
        return ItemFactory.Create(id);
    }

    static int MatchLegacy(global::ItemDef legacy)
    {
        string name = (legacy.displayName ?? legacy.id ?? "").ToLowerInvariant();
        if (name.Contains("business card")) return 11;
        if (name.Contains("freelance")) return 12;
        if (name.Contains("referral")) return 13;
        if (name.Contains("company id")) return 14;
        if (name.Contains("college id") || name == "id card") return 15;
        if (name.Contains("certificate") || name.Contains("license")) return 9;
        if (name.Contains("reference letter")) return 10;
        if (name.Contains("portfolio") || name.Contains("folder") || name.Contains("sketchbook")) return 16;
        if (name.Contains("project report") || name.Contains("lesson plan")) return 17;
        if (name.Contains("resume") || name.Contains("cv")) return 3;
        if (name.Contains("cover letter")) return 6;
        if (name.Contains("envelope")) return 18;
        if (name.Contains("guitar")) return 19;
        if (name.Contains("gun")) return 20;
        // Fall back to tags from the legacy catalog.
        if (legacy.Satisfies("certification")) return 9;
        if (legacy.Satisfies("portfolio")) return 16;
        if (legacy.Satisfies("reference")) return 10;
        if (legacy.Satisfies("id")) return 15;
        if (legacy.Satisfies("resume")) return 3;
        return -1;
    }
}
