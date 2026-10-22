using System;
using System.Collections.Generic;

namespace Items
{
    public enum SpawnSource
    {
        FlyingPaperTrail, LostBackpack, LooseGroundPapers,
        NpcRecruiter, NpcFreelancer, NpcStudent, NpcMentor, Event
    }

    // Per-rarity weights. Legendary is always 0 for random rolls (events call GrantItem).
    [Serializable]
    public struct RarityWeights
    {
        public float common, uncommon, rare, epic, legendary;
        public RarityWeights(float c, float u, float r, float e) { common = c; uncommon = u; rare = r; epic = e; legendary = 0f; }
        public static RarityWeights Default { get { return new RarityWeights(55f, 28f, 13f, 3f); } }
        public float Get(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return common;
                case Rarity.Uncommon: return uncommon;
                case Rarity.Rare: return rare;
                case Rarity.Epic: return epic;
                default: return 0f;   // Legendary never rolls
            }
        }
    }

    // One spawner's rules. Pool = (form in allowed AND id not in blocklist) OR id in extraAllowlist.
    // Event-only (Legendary) items never enter the pool.
    [Serializable]
    public class SpawnTable
    {
        public SpawnSource source;
        public ItemForm[] allowed = new ItemForm[0];
        public RarityWeights weights = RarityWeights.Default;
        public int[] extraAllowlist = new int[0];
        public int[] blocklist = new int[0];

        public SpawnTable() { }
        public SpawnTable(SpawnSource s, ItemForm[] forms, int[] block = null, int[] extra = null)
        {
            source = s; allowed = forms;
            if (block != null) blocklist = block;
            if (extra != null) extraAllowlist = extra;
        }

        public bool Allows(ItemDef d)
        {
            if (d == null || d.eventOnly) return false;
            if (Array.IndexOf(extraAllowlist, d.id) >= 0) return true;
            return Array.IndexOf(allowed, d.form) >= 0 && Array.IndexOf(blocklist, d.id) < 0;
        }

        public List<ItemDef> Pool()
        {
            var l = new List<ItemDef>();
            foreach (var d in ItemDatabase.All) if (Allows(d)) l.Add(d);
            return l;
        }
    }
}
