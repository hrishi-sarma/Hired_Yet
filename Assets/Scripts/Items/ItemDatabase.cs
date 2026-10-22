using System.Collections.Generic;

namespace Items
{
    public static class ItemDatabase
    {
        static readonly NameState[] None_  = { NameState.NotApplicable };
        static readonly NameState[] Other_ = { NameState.Other };
        static readonly NameState[] Illeg_ = { NameState.Illegible };
        static readonly NameState[] Blank_ = { NameState.Blank };
        static readonly NameState[] OwnV   = { NameState.Own };
        static readonly NameState[] BlankOrOther = { NameState.Blank, NameState.Other };

        const StatFlags Doc = StatFlags.All;
        const StatFlags Card_ = StatFlags.Relevance | StatFlags.Credibility | StatFlags.Presentation;
        const StatFlags Misc = StatFlags.Quality | StatFlags.Relevance | StatFlags.Presentation;

        static readonly ItemDef[] defs =
        {
            new ItemDef( 1, "Crumpled Resume",               Rarity.Common,   ItemForm.Paper, DocType.Resume,      Condition.Crumpled, Illeg_, Doc),
            new ItemDef( 2, "Resume with Someone Else's Name", Rarity.Uncommon, ItemForm.Paper, DocType.Resume,    Condition.Clean,    Other_, Doc),
            new ItemDef( 3, "Blank-Name Resume",             Rarity.Rare,     ItemForm.Paper, DocType.Resume,      Condition.Clean,    Blank_, Doc),
            new ItemDef( 4, "Crumpled Cover Letter",         Rarity.Common,   ItemForm.Paper, DocType.CoverLetter, Condition.Crumpled, Illeg_, Doc),
            new ItemDef( 5, "Cover Letter with Someone Else's Name", Rarity.Uncommon, ItemForm.Paper, DocType.CoverLetter, Condition.Clean, Other_, Doc),
            new ItemDef( 6, "Blank-Name Cover Letter",       Rarity.Rare,     ItemForm.Paper, DocType.CoverLetter, Condition.Clean,    Blank_, Doc),
            new ItemDef( 7, "Crumpled Certificate",          Rarity.Common,   ItemForm.Paper, DocType.Certificate, Condition.Crumpled, Illeg_, Doc),
            new ItemDef( 8, "Certificate with Someone Else's Name", Rarity.Uncommon, ItemForm.Paper, DocType.Certificate, Condition.Clean, Other_, Doc),
            new ItemDef( 9, "Blank-Name Certificate",        Rarity.Rare,     ItemForm.Paper, DocType.Certificate, Condition.Clean,    Blank_, Doc),
            new ItemDef(10, "Reference Letter",              Rarity.Rare,     ItemForm.Paper, DocType.None,        Condition.Clean,    OwnV,   Doc),
            new ItemDef(11, "Business Card",                 Rarity.Uncommon, ItemForm.Card,  DocType.None,        Condition.Clean,    BlankOrOther, Card_),
            new ItemDef(12, "Freelance Service Card",        Rarity.Uncommon, ItemForm.Card,  DocType.None,        Condition.Clean,    None_,  Card_),
            new ItemDef(13, "Employee Referral Slip",        Rarity.Epic,     ItemForm.SmallPaper, DocType.None,   Condition.Clean,    None_,  Card_),
            new ItemDef(14, "Company ID Card",               Rarity.Rare,     ItemForm.Card,  DocType.None,        Condition.Clean,    BlankOrOther, Card_),
            new ItemDef(15, "College ID Card",               Rarity.Common,   ItemForm.Card,  DocType.None,        Condition.Clean,    BlankOrOther, Card_),
            new ItemDef(16, "Portfolio Booklet",             Rarity.Rare,     ItemForm.Booklet,  DocType.None,     Condition.Clean,    None_,  Misc),
            new ItemDef(17, "Project Report",                Rarity.Uncommon, ItemForm.Paper, DocType.None,        Condition.Clean,    None_,  Misc),
            new ItemDef(18, "Sealed Envelope",               Rarity.Uncommon, ItemForm.Envelope, DocType.None,     Condition.Clean,    None_,  StatFlags.None),
            new ItemDef(19, "Guitar",                        Rarity.Legendary, ItemForm.Object, DocType.None,      Condition.Clean,    None_,  StatFlags.None),
            new ItemDef(20, "Gun Prop",                      Rarity.Legendary, ItemForm.Object, DocType.None,      Condition.Clean,    None_,  StatFlags.None),
        };

        static readonly Dictionary<int, ItemDef> byId = Build();
        static Dictionary<int, ItemDef> Build()
        {
            var d = new Dictionary<int, ItemDef>();
            foreach (var x in defs) d[x.id] = x;
            return d;
        }

        // System item (not one of the 20 spawnable items, not counted in All).
        public static readonly ItemDef PlayerResumeDef = new ItemDef(0, "Your Resume", Rarity.Common,
            ItemForm.Paper, DocType.Resume, Condition.Clean, OwnV, Doc);
        public static IReadOnlyList<ItemDef> All { get { return defs; } }
        public static ItemDef Get(int id) { if (id == 0) return PlayerResumeDef; ItemDef d; return byId.TryGetValue(id, out d) ? d : null; }
    }
}
