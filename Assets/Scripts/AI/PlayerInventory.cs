using System;
using UnityEngine;
using Items;

/// <summary>
/// Compatibility API for older world scripts. Storage is exclusively Items.Inventory.
/// New code should use Items.Inventory directly.
/// </summary>
[Obsolete("PlayerInventory is a compatibility facade. Use Items.Inventory.Current.")]
public static class PlayerInventory
{
    public const int SlotCount = Items.Inventory.SlotCount;
    static Items.Inventory subscribedInventory;
    static bool pickpocketLocked;
    public static event Action Changed;

    static PlayerInventory() { Subscribe(); }
    static void Subscribe()
    {
        InterviewInventoryBridge.EnsureInitialized();
        Items.Inventory inv = Items.Inventory.Current;
        if (subscribedInventory == inv) return;
        if (subscribedInventory != null)
        {
            subscribedInventory.OnItemAdded -= OnChanged;
            subscribedInventory.OnItemRemoved -= OnChanged;
            subscribedInventory.OnItemChanged -= OnChanged;
        }
        inv.OnItemAdded += OnChanged;
        inv.OnItemRemoved += OnChanged;
        inv.OnItemChanged += OnChanged;
        subscribedInventory = inv;
    }
    static void OnChanged(int slot, ItemInstance item)
    {
        Action cb = Changed; if (cb != null) cb();
    }

    public static bool PickpocketLocked { get { return pickpocketLocked; } set { pickpocketLocked = value; } }

    public static ItemDef[] Extras
    {
        get { return new[] { Get(1), Get(2) }; }
    }
    public static bool ResumeAvailable
    {
        get { return InterviewInventoryBridge.HasResume; }
        set
        {
            Subscribe();
            Items.Inventory inv = Items.Inventory.Current;
            if (!value)
            {
                for (int i=0;i<SlotCount;i++) { var x=inv.Get(i); if (x!=null && x.def.docType==DocType.Resume) { inv.Remove(i); break; } }
            }
            else if (!InterviewInventoryBridge.HasResume)
            {
                int empty=inv.FirstEmpty();
                if (empty>=0) inv.SwapIn(empty, ItemFactory.CreatePlayerResume());
            }
        }
    }

    public static ItemDef Get(int slot)
    {
        Subscribe();
        ItemInstance item = Items.Inventory.Current.Get(slot);
        if (item == null) return null;
        ItemDef d = new ItemDef();
        d.id = item.def.id.ToString();
        d.displayName = item.def.name;
        d.description = item.NameLabel;
        d.code = item.def.name.Length > 0 ? item.def.name.Substring(0, Math.Min(3,item.def.name.Length)).ToUpperInvariant() : "ITM";
        d.rarity = (ItemRarity)(int)item.def.rarity;
        d.credit = Mathf.RoundToInt(InterviewInventoryBridge.Credit(item, ResumeSession.SelectedJob));
        d.satisfies = Tags(item);
        return d;
    }

    static string[] Tags(ItemInstance item)
    {
        var tags = new System.Collections.Generic.List<string>();
        if (item.def.docType == DocType.Resume) tags.Add("resume");
        if (item.def.docType == DocType.Certificate) tags.Add("certification");
        if (item.def.form == ItemForm.Booklet || item.def.name.Contains("Portfolio") || item.def.name.Contains("Report")) tags.Add("portfolio");
        if (item.def.name.Contains("Reference") || item.def.name.Contains("Business Card") || item.def.name.Contains("Referral")) tags.Add("reference");
        if (item.def.name.Contains("ID Card")) tags.Add("id");
        if (item.def.docType == DocType.CoverLetter || item.def.form == ItemForm.Card || item.def.form == ItemForm.Object) tags.Add("demo");
        return tags.ToArray();
    }

    public static bool IsFull { get { return Items.Inventory.Current.IsFull; } }
    public static bool Has(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        int parsed; if (int.TryParse(id, out parsed)) return Items.Inventory.Current.HasDef(parsed);
        string key = id.ToLowerInvariant();
        int defId = key.Contains("business") ? 11 : key.Contains("freelance") ? 12 :
            key.Contains("referral") ? 13 : key.Contains("company") ? 14 :
            key.Contains("college") || key == "id_card" ? 15 :
            key.Contains("certificate") || key.Contains("license") ? 9 :
            key.Contains("reference") ? 10 : key.Contains("portfolio") || key.Contains("folder") || key.Contains("sketch") ? 16 :
            key.Contains("report") || key.Contains("lesson") ? 17 :
            key.Contains("resume") || key.Contains("cv") ? 3 :
            key.Contains("cover") ? 6 : key.Contains("envelope") ? 18 : -1;
        return defId >= 0 && Items.Inventory.Current.HasDef(defId);
    }
    public static bool TryAdd(ItemDef item)
    {
        Subscribe();
        ItemInstance converted = InterviewInventoryBridge.FromLegacy(item);
        return converted != null && Items.Inventory.Current.TryAdd(converted);
    }
    public static bool Swap(int slot, ItemDef item)
    {
        Subscribe();
        ItemInstance converted = InterviewInventoryBridge.FromLegacy(item);
        if (converted == null || slot<0 || slot>=SlotCount) return false;
        Items.Inventory.Current.SwapIn(slot, converted);
        return true;
    }
    public static void Remove(int slot) { Items.Inventory.Current.Remove(slot); }
    public static ItemDef FindFor(string tag)
    {
        for (int i=0;i<SlotCount;i++) { ItemDef d=Get(i); if (d!=null && d.Satisfies(tag)) return d; }
        return null;
    }
    public static bool CanSatisfy(string tag)
    {
        if (tag=="resume" && ResumeAvailable) return true;
        return FindFor(tag)!=null;
    }
    public static void Reset()
    {
        Items.Inventory.NewGame();
        pickpocketLocked=false;
        Action cb=Changed; if(cb!=null) cb();
    }
}
