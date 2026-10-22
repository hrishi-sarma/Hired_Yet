using System.Collections.Generic;

namespace Items
{
    /// <summary>Stage 7: per-form stat visibility/labels and same-DocType comparison.</summary>
    public static class ItemCompare
    {
        public struct Row { public string label; public int value; public bool hasDelta; public int delta; }
        public class Result { public List<Row> rows = new List<Row>(); public ItemInstance against; public int againstSlot = -1; }

        static readonly StatFlags[] Order = { StatFlags.Quality, StatFlags.Relevance, StatFlags.Credibility, StatFlags.Presentation };

        public static string Label(ItemDef d, StatFlags s)
        {
            if (d.docType == DocType.None)
            {
                if (s == StatFlags.Relevance && (d.id == 11 || d.id == 12)) return "NETWORKING";   // Business / Freelance card
                if (s == StatFlags.Credibility && d.id == 13) return "TRUST";                       // Referral Slip
            }
            switch (s) { case StatFlags.Quality: return "QUALITY"; case StatFlags.Relevance: return "RELEVANCE"; case StatFlags.Credibility: return "CREDIBILITY"; default: return "PRESENTATION"; }
        }
        static int Get(ItemStats st, StatFlags s)
        {
            switch (s) { case StatFlags.Quality: return st.quality; case StatFlags.Relevance: return st.relevance; case StatFlags.Credibility: return st.credibility; default: return st.presentation; }
        }

        /// <summary>First other held item with the same DocType (documents only); null if none.</summary>
        public static int FindAgainst(Inventory inv, int slot)
        {
            var it = inv.Get(slot); if (it == null || it.def.docType == DocType.None) return -1;
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                if (i == slot) continue; var o = inv.Get(i);
                if (o != null && o.def.docType == it.def.docType) return i;
            }
            return -1;
        }

        /// <summary>Rows only for stats the item uses; deltas vs a same-DocType item when one is held.</summary>
        public static Result Build(Inventory inv, int slot)
        {
            var r = new Result(); var it = inv.Get(slot); if (it == null) return r;
            int a = FindAgainst(inv, slot);
            if (a >= 0) { r.against = inv.Get(a); r.againstSlot = a; }
            var st = it.Stats;
            foreach (var f in Order)
            {
                if ((it.def.usedStats & f) == 0) continue;
                var row = new Row { label = Label(it.def, f), value = Get(st, f) };
                if (r.against != null) { row.hasDelta = true; row.delta = row.value - Get(r.against.Stats, f); }
                r.rows.Add(row);
            }
            return r;
        }
    }
}
