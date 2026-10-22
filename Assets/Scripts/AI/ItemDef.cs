using UnityEngine;

[System.Serializable]
public class ItemDef
{
    public string id;
    public string displayName;
    public string description;
    public string code;
    public ItemRarity rarity = ItemRarity.Common;
    public string[] jobIds = new string[0];
    public string[] categories = new string[0];
    public string[] satisfies = new string[0];
    public int credit = 6;
    public ModelSlot slot = new ModelSlot();
    public PrimitiveKind primitive = PrimitiveKind.Cube;
    public Color color = Color.white;
    public string colorHex;
    public Vector3 size = new Vector3(0.2f, 0.1f, 0.15f);

    public bool IsGeneric
    {
        get { return Count(jobIds) == 0 && Count(categories) == 0; }
    }

    public Color DisplayColor
    {
        get
        {
            if (!string.IsNullOrEmpty(colorHex)) return UIKit.Hex(colorHex);
            return color;
        }
    }

    static int Count(string[] list)
    {
        return list == null ? 0 : list.Length;
    }

    static bool Has(string[] list, string value)
    {
        if (list == null || string.IsNullOrEmpty(value)) return false;
        for (int i = 0; i < list.Length; i++)
            if (string.Equals(list[i], value, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public bool Satisfies(string tag)
    {
        return Has(satisfies, tag);
    }

    public bool FitsJob(JobData job)
    {
        if (job == null) return false;
        return Has(jobIds, job.id) || Has(categories, job.category);
    }

    public string SatisfiesText()
    {
        return satisfies == null || satisfies.Length == 0 ? "nothing" : string.Join(", ", satisfies);
    }
}

[System.Serializable]
public class ItemDatabase
{
    public ItemDef[] items;
}
