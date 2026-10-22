using System;
using System.Collections.Generic;

namespace Items
{
    // Plain C# inventory: 3 slots, slot 0 starts with the player's resume.
    // No UI, no world objects. UI (Stage 6) and world drop (Stage 5b) subscribe to events.
    public class Inventory
    {
        public const int SlotCount = 3;

        readonly ItemInstance[] slots = new ItemInstance[SlotCount];

        public event Action<int, ItemInstance> OnItemAdded;     // slot, item
        public event Action<int, ItemInstance> OnItemRemoved;   // slot, item (the item that left)
        public event Action<int, ItemInstance> OnItemChanged;   // slot, item (name edited etc.)
        public event Action<ItemInstance> OnItemDropped;        // World drop system (Stage 5b) listens

        // One inventory per run. The game returns to the start menu after the interview, so Reset() on new game.
        static Inventory current;
        public static Inventory Current { get { return current ?? (current = new Inventory()); } }

        public static Inventory NewGame()
        {
            current = new Inventory();
            current.slots[0] = ItemFactory.CreatePlayerResume();
            return current;
        }

        // ---------- queries ----------
        public ItemInstance Get(int slot) { return InRange(slot) ? slots[slot] : null; }
        public bool IsEmpty(int slot) { return InRange(slot) && slots[slot] == null; }
        public int FirstEmpty() { for (int i = 0; i < SlotCount; i++) if (slots[i] == null) return i; return -1; }
        public bool IsFull { get { return FirstEmpty() < 0; } }
        public int Count { get { int n = 0; for (int i = 0; i < SlotCount; i++) if (slots[i] != null) n++; return n; } }

        public int IndexOf(ItemInstance item) { for (int i = 0; i < SlotCount; i++) if (slots[i] == item) return i; return -1; }
        public bool HasDef(int defId) { for (int i = 0; i < SlotCount; i++) if (slots[i] != null && slots[i].def.id == defId) return true; return false; }

        // Held items of a document type (Resume etc.), in slot order. Used by the interviewer (Stage 8).
        public List<ItemInstance> Find(DocType type)
        {
            var list = new List<ItemInstance>();
            for (int i = 0; i < SlotCount; i++)
                if (slots[i] != null && slots[i].def.docType == type) list.Add(slots[i]);
            return list;
        }

        public List<ItemInstance> All()
        {
            var list = new List<ItemInstance>();
            for (int i = 0; i < SlotCount; i++) if (slots[i] != null) list.Add(slots[i]);
            return list;
        }

        // ---------- add / remove ----------
        public bool TryAdd(ItemInstance item, out int slot)
        {
            slot = -1;
            if (item == null) return false;
            slot = FirstEmpty();
            if (slot < 0) return false;
            slots[slot] = item;
            if (OnItemAdded != null) OnItemAdded(slot, item);
            return true;
        }
        public bool TryAdd(ItemInstance item) { int s; return TryAdd(item, out s); }

        // Remove without dropping (consumed / handed away).
        public ItemInstance Remove(int slot)
        {
            if (!InRange(slot) || slots[slot] == null) return null;
            var item = slots[slot];
            slots[slot] = null;
            if (OnItemRemoved != null) OnItemRemoved(slot, item);
            return item;
        }

        // Drop into the world: clears the slot and raises OnItemDropped so Stage 5b can spawn a WorldItem.
        public ItemInstance Drop(int slot)
        {
            var item = Remove(slot);
            if (item != null && OnItemDropped != null) OnItemDropped(item);
            return item;
        }

        // Inventory full + new pickup: put `incoming` into `slot`, return the old item (caller drops it).
        public ItemInstance SwapIn(int slot, ItemInstance incoming)
        {
            if (!InRange(slot) || incoming == null) return null;
            var old = slots[slot];
            slots[slot] = incoming;
            if (old != null && OnItemRemoved != null) OnItemRemoved(slot, old);
            if (OnItemAdded != null) OnItemAdded(slot, incoming);
            return old;
        }

        // Reorder two slots (either may be empty).
        public bool Swap(int a, int b)
        {
            if (!InRange(a) || !InRange(b) || a == b) return false;
            var t = slots[a]; slots[a] = slots[b]; slots[b] = t;
            if (slots[a] != null && OnItemChanged != null) OnItemChanged(a, slots[a]);
            if (slots[b] != null && OnItemChanged != null) OnItemChanged(b, slots[b]);
            return true;
        }

        // ---------- name actions (no animation, just state; UI plays its own feedback) ----------
        public bool CanChangeName(int slot) { var i = Get(slot); return i != null && i.CanChangeName; }
        public bool CanAddName(int slot)    { var i = Get(slot); return i != null && i.CanAddName; }

        // Other-name paper -> Scribbled, your name written over it.
        public bool ChangeName(int slot)
        {
            if (!CanChangeName(slot)) return false;
            var i = slots[slot];
            i.originalName = i.printedName;
            i.printedName = ItemFactory.PlayerName;
            i.nameState = NameState.Scribbled;
            if (OnItemChanged != null) OnItemChanged(slot, i);
            return true;
        }

        // Blank name -> Own.
        public bool AddName(int slot)
        {
            if (!CanAddName(slot)) return false;
            var i = slots[slot];
            i.printedName = ItemFactory.PlayerName;
            i.nameState = NameState.Own;
            if (OnItemChanged != null) OnItemChanged(slot, i);
            return true;
        }

        static bool InRange(int s) { return s >= 0 && s < SlotCount; }
    }
}
