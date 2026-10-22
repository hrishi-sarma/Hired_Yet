using System;
using UnityEngine;

namespace Items
{
    // Stage 9: serialize {defId, seed, nameState, condition, printedName} per slot; round-trips items identically.
    public static class InventorySave
    {
        const string Key = "items_inventory_v1";

        [Serializable] class Rec { public int slot, defId, seed, nameState, condition; public string printedName, originalName; public bool starter; }
        [Serializable] class File { public Rec[] items = new Rec[0]; }

        public static string ToJson(Inventory inv)
        {
            var list = new System.Collections.Generic.List<Rec>();
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var it = inv.Get(i); if (it == null) continue;
                list.Add(new Rec { slot = i, defId = it.def.id, seed = it.seed, nameState = (int)it.nameState, condition = (int)it.condition,
                                   printedName = it.printedName, originalName = it.originalName, starter = it.isPlayerStarter });
            }
            return JsonUtility.ToJson(new File { items = list.ToArray() });
        }

        public static ItemInstance Restore(int defId, int seed, NameState ns, Condition cond, string printed, string original, bool starter)
        {
            ItemInstance it;
            if (starter) { it = ItemFactory.CreatePlayerResume(); it.seed = seed; }
            else
            {
                it = ItemFactory.Create(defId, seed);
                if (it == null) return null;
            }
            it.nameState = ns; it.condition = cond; it.printedName = printed ?? ""; it.originalName = original ?? "";
            return it;
        }

        // Replaces the current run's inventory contents (new Inventory via NewGame, then fills slots).
        public static bool FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            File f; try { f = JsonUtility.FromJson<File>(json); } catch (Exception) { return false; }
            if (f == null || f.items == null) return false;
            var inv = Inventory.NewGame();
            for (int i = 0; i < Inventory.SlotCount; i++) inv.Remove(i);
            foreach (var r in f.items)
            {
                if (r.slot < 0 || r.slot >= Inventory.SlotCount) continue;
                var it = Restore(r.defId, r.seed, (NameState)r.nameState, (Condition)r.condition, r.printedName, r.originalName, r.starter);
                if (it != null) inv.SwapIn(r.slot, it);
            }
            return true;
        }

        public static void Save() { PlayerPrefs.SetString(Key, ToJson(Inventory.Current)); PlayerPrefs.Save(); }
        public static bool Load() { return PlayerPrefs.HasKey(Key) && FromJson(PlayerPrefs.GetString(Key)); }
        public static void Clear() { PlayerPrefs.DeleteKey(Key); }
    }
}
