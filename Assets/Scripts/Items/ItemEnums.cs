using System;

namespace Items
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    // Drives spawn rules (Stage 3), mesh (Stage 5) and hold pose.
    public enum ItemForm { Paper, SmallPaper, Card, Booklet, Envelope, Object }

    public enum DocType { None, Resume, CoverLetter, Certificate }

    // Illegible = someone else's name, smudged/unreadable. Not editable.
    public enum NameState { NotApplicable, Own, Other, Illegible, Scribbled, Blank }

    public enum Condition { Clean, Crumpled }

    [Flags]
    public enum StatFlags
    {
        None = 0, Quality = 1, Relevance = 2, Credibility = 4, Presentation = 8,
        All = Quality | Relevance | Credibility | Presentation
    }
}
