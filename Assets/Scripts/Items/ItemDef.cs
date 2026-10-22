namespace Items
{
    // Static definition of one of the 20 allowed items (see interview_game_items.md).
    public class ItemDef
    {
        public readonly int id;
        public readonly string name;
        public readonly Rarity rarity;
        public readonly ItemForm form;
        public readonly DocType docType;
        public readonly Condition defaultCondition;
        // Name states this item can spawn with. One is picked per instance.
        public readonly NameState[] nameVariants;
        public readonly StatFlags usedStats;
        // True for Legendary items: never rolled by spawn tables, only granted by events.
        public readonly bool eventOnly;

        public ItemDef(int id, string name, Rarity rarity, ItemForm form, DocType docType,
                       Condition cond, NameState[] variants, StatFlags used)
        {
            this.id = id; this.name = name; this.rarity = rarity; this.form = form;
            this.docType = docType; defaultCondition = cond; nameVariants = variants;
            usedStats = used; eventOnly = rarity == Rarity.Legendary;
        }

        public bool IsDocument { get { return docType != DocType.None; } }
        public bool HasName { get { return nameVariants.Length > 0 && nameVariants[0] != NameState.NotApplicable; } }
    }
}
