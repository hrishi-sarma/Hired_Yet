using System;
using System.Collections.Generic;
using UnityEngine;

namespace Items
{
    // Stage 3: form-whitelisted, rarity-weighted item rolls.
    // Designers can override any table with Resources/SpawnTables.json (see SpawnTableFile).
    public static class ItemSpawner
    {
        [Serializable] class SpawnTableFile { public SpawnTable[] tables; }

        static Dictionary<SpawnSource, SpawnTable> tables;

        static readonly ItemForm P = ItemForm.Paper, SP = ItemForm.SmallPaper, C = ItemForm.Card,
                                 B = ItemForm.Booklet, E = ItemForm.Envelope, O = ItemForm.Object;

        static Dictionary<SpawnSource, SpawnTable> Tables
        {
            get { if (tables == null) { tables = Defaults(); LoadOverrides(); } return tables; }
        }

        static Dictionary<SpawnSource, SpawnTable> Defaults()
        {
            var list = new[]
            {
                new SpawnTable(SpawnSource.FlyingPaperTrail, new[] { P }),                 // no Referral Slip (SmallPaper)
                new SpawnTable(SpawnSource.LostBackpack,     new[] { P, SP, C, B, E }),    // Legendary excluded automatically
                new SpawnTable(SpawnSource.LooseGroundPapers,new[] { P, E }),
                new SpawnTable(SpawnSource.NpcRecruiter,     new[] { C, SP }),             // Business Card, Referral Slip, IDs
                new SpawnTable(SpawnSource.NpcFreelancer,    new[] { C }, new[] { 11, 14, 15 }), // Freelance Card only
                new SpawnTable(SpawnSource.NpcStudent,       new[] { C, P, B }),
                new SpawnTable(SpawnSource.NpcMentor,        new[] { P }),
                new SpawnTable(SpawnSource.Event,            new[] { O }),                 // pool empty: use GrantItem
            };
            var d = new Dictionary<SpawnSource, SpawnTable>();
            foreach (var t in list) d[t.source] = t;
            return d;
        }

        // Optional: Resources/SpawnTables.json = { "tables": [ { "source": 1, "allowed": [0,2], "weights": {...}, "extraAllowlist": [], "blocklist": [] } ] }
        public static void LoadOverrides()
        {
            if (tables == null) tables = Defaults();
            var ta = Resources.Load<TextAsset>("SpawnTables");
            if (ta == null) return;
            try
            {
                var f = JsonUtility.FromJson<SpawnTableFile>(ta.text);
                if (f != null && f.tables != null) foreach (var t in f.tables) { t.weights.legendary = 0f; tables[t.source] = t; }
            }
            catch (Exception e) { Debug.LogWarning("SpawnTables.json invalid, using defaults: " + e.Message); }
        }

        public static void ResetToDefaults() { tables = null; }
        public static SpawnTable GetTable(SpawnSource s) { return Tables[s]; }
        public static void SetTable(SpawnTable t) { Tables[t.source] = t; }

        // Rolls a def. Skips defs already held in 'held' (duplicate guard) when provided.
        // Returns null if the pool is empty after filtering.
        public static ItemDef Roll(SpawnTable t, System.Random r, Inventory held = null)
        {
            if (Difficulty.LegendaryChance > 0f && r.NextDouble() < Difficulty.LegendaryChance)   // high difficulty: small chance of a legendary drop
            {
                var legs = new List<ItemDef>();
                foreach (var d in ItemDatabase.All) if (d.rarity == Rarity.Legendary && (held == null || !held.HasDef(d.id))) legs.Add(d);
                if (legs.Count > 0) return legs[r.Next(legs.Count)];
            }
            var pool = new List<ItemDef>(); var w = new List<float>(); float total = 0f;
            foreach (var d in t.Pool())
            {
                if (held != null && held.HasDef(d.id)) continue;
                float x = t.weights.Get(d.rarity) * Difficulty.RarityMul(d.rarity);
                if (x <= 0f) continue;
                pool.Add(d); w.Add(x); total += x;
            }
            if (pool.Count == 0) return null;
            double pick = r.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++) { pick -= w[i]; if (pick < 0) return pool[i]; }
            return pool[pool.Count - 1];
        }

        public static ItemDef Roll(SpawnSource s, System.Random r, Inventory held = null)
        { return Roll(Tables[s], r, held); }

        // Rolls a def and builds the instance (seed drawn from r). Null if nothing can roll.
        public static ItemInstance RollInstance(SpawnSource s, System.Random r, Inventory held = null)
        {
            var d = Roll(s, r, held);
            return d == null ? null : ItemFactory.Create(d.id, r.Next());
        }

        public static ItemInstance RollInstance(SpawnSource s, int seed, Inventory held = null)
        { return RollInstance(s, new System.Random(seed), held); }

        // Scripted grants (the only way Legendary items appear). Does not touch the inventory.
        public static ItemInstance GrantItem(int defId, int seed)
        { return ItemDatabase.Get(defId) == null ? null : ItemFactory.Create(defId, seed); }

        public static ItemInstance GrantItem(int defId)
        { return GrantItem(defId, Environment.TickCount ^ (defId * 7919)); }
    }
}
