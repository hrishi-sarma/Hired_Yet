using System.Collections.Generic;
using UnityEngine;

public static class ItemCatalog
{
    struct Flavor
    {
        public string[] keys;
        public string id;
        public string name;
        public string desc;
        public string sat;
        public ItemRarity rarity;
        public string hex;
        public PrimitiveKind prim;
        public Vector3 size;
    }

    static List<ItemDef> all;
    static readonly Dictionary<string, ItemDef> byId = new Dictionary<string, ItemDef>();
    static InterviewDatabase builtFor;
    static bool fromJson;

    static readonly Flavor[] Flavors =
    {
        F("dev,developer,programmer,engineer,software,coder,it,sysadmin,admin,hacker,data", "linux_laptop", "Linux Laptop", "A beaten-up laptop covered in stickers. Boots straight to a terminal.", "demo,portfolio", ItemRarity.Uncommon, "#4A4F57", PrimitiveKind.Cube, 0.35f, 0.03f, 0.25f),
        F("dev,developer,programmer,engineer,software,coder,it,sysadmin,admin,hacker,data", "usb_stick", "USB Stick", "Holds a few projects and a suspicious amount of memes.", "portfolio", ItemRarity.Common, "#2F6FB5", PrimitiveKind.Cube, 0.06f, 0.02f, 0.02f),
        F("wizard,mage,sorcerer,witch,warlock,enchanter", "wand", "Wand", "Smells faintly of ozone. Probably real.", "demo", ItemRarity.Uncommon, "#7A4DB3", PrimitiveKind.Cylinder, 0.03f, 0.3f, 0.03f),
        F("wizard,mage,sorcerer,witch,warlock,enchanter", "spellbook", "Spellbook", "Heavy, leather bound, whispers when closed.", "portfolio", ItemRarity.Uncommon, "#5A2E2E", PrimitiveKind.Cube, 0.2f, 0.05f, 0.28f),
        F("chef,cook,baker,barista", "chef_knife", "Chef Knife", "A very sharp argument for your skills.", "demo", ItemRarity.Uncommon, "#B8BCC4", PrimitiveKind.Cube, 0.04f, 0.01f, 0.3f),
        F("chef,cook,baker,barista", "recipe_book", "Recipe Book", "Stained pages full of handwritten notes.", "portfolio", ItemRarity.Common, "#C79A5A", PrimitiveKind.Cube, 0.18f, 0.04f, 0.24f),
        F("doctor,nurse,surgeon,medic,paramedic,dentist", "stethoscope", "Stethoscope", "Cold metal and a lot of credibility.", "demo", ItemRarity.Uncommon, "#3C3C42", PrimitiveKind.Sphere, 0.1f, 0.1f, 0.1f),
        F("doctor,nurse,surgeon,medic,paramedic,dentist", "medical_license", "Medical License", "Official looking, with a stamp.", "certification,id", ItemRarity.Rare, "#E6E2D3", PrimitiveKind.Cube, 0.2f, 0.01f, 0.14f),
        F("teacher,professor,tutor,lecturer", "lesson_plan", "Lesson Plan", "A neat folder of plans for the whole term.", "portfolio,demo", ItemRarity.Common, "#D9D3B8", PrimitiveKind.Cube, 0.22f, 0.02f, 0.3f),
        F("pilot,driver,captain,sailor", "logbook", "Logbook", "Hours logged, signatures included.", "portfolio,certification", ItemRarity.Uncommon, "#2C3E50", PrimitiveKind.Cube, 0.16f, 0.03f, 0.22f),
        F("artist,designer,painter,illustrator,musician", "sketchbook", "Sketchbook", "Full of work you can show off.", "portfolio,demo", ItemRarity.Common, "#8E8E5A", PrimitiveKind.Cube, 0.2f, 0.03f, 0.27f)
    };

    static Flavor F(string keys, string id, string name, string desc, string sat, ItemRarity rarity, string hex, PrimitiveKind prim, float sx, float sy, float sz)
    {
        Flavor f = new Flavor();
        f.keys = keys.Split(',');
        f.id = id;
        f.name = name;
        f.desc = desc;
        f.sat = sat;
        f.rarity = rarity;
        f.hex = hex;
        f.prim = prim;
        f.size = new Vector3(sx, sy, sz);
        return f;
    }

    public static List<ItemDef> All
    {
        get
        {
            Ensure();
            return all;
        }
    }

    public static ItemDef ById(string id)
    {
        Ensure();
        ItemDef def;
        return id != null && byId.TryGetValue(id, out def) ? def : null;
    }

    public static void Reload()
    {
        all = null;
        Ensure();
    }

    static void Ensure()
    {
        ResumeSession.EnsureLoaded();
        if (all != null && (fromJson || builtFor == ResumeSession.Database)) return;
        Build();
    }

    static void Build()
    {
        all = new List<ItemDef>();
        byId.Clear();
        fromJson = false;
        builtFor = ResumeSession.Database;

        TextAsset asset = Resources.Load<TextAsset>("item_data");
        if (asset != null)
        {
            ItemDatabase db = JsonUtility.FromJson<ItemDatabase>(asset.text);
            if (db != null && db.items != null && db.items.Length > 0)
            {
                for (int i = 0; i < db.items.Length; i++) Add(db.items[i]);
                fromJson = true;
                return;
            }
        }

        AddGeneric();
        AddPerJob(builtFor);
    }

    static void Add(ItemDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id) || byId.ContainsKey(def.id)) return;
        if (string.IsNullOrEmpty(def.displayName)) def.displayName = def.id;
        if (string.IsNullOrEmpty(def.code)) def.code = CodeOf(def.displayName);
        if (def.size.sqrMagnitude < 0.0001f) def.size = new Vector3(0.2f, 0.1f, 0.15f);
        if (def.jobIds == null) def.jobIds = new string[0];
        if (def.categories == null) def.categories = new string[0];
        if (def.satisfies == null) def.satisfies = new string[0];
        if (def.slot == null) def.slot = new ModelSlot();
        all.Add(def);
        byId[def.id] = def;
    }

    static ItemDef Make(string id, string name, string desc, string sat, ItemRarity rarity, string hex, PrimitiveKind prim, Vector3 size, int credit)
    {
        ItemDef d = new ItemDef();
        d.id = id;
        d.displayName = name;
        d.description = desc;
        d.satisfies = sat.Split(',');
        d.rarity = rarity;
        d.colorHex = hex;
        d.primitive = prim;
        d.size = size;
        d.credit = credit;
        return d;
    }

    static void AddGeneric()
    {
        Add(Make("id_card", "ID Card", "A government ID. Not yours, but the photo is close enough.", "id", ItemRarity.Common, "#C8D6E5", PrimitiveKind.Cube, new Vector3(0.085f, 0.003f, 0.054f), 6));
        Add(Make("business_card", "Business Card", "Somebody else's job title, in embossed lettering.", "reference", ItemRarity.Common, "#EDEDED", PrimitiveKind.Cube, new Vector3(0.09f, 0.002f, 0.05f), 4));
        Add(Make("certificate", "Certificate", "A framed-looking certificate. It can stand in for a missing resume.", "certification,resume", ItemRarity.Uncommon, "#E8D9A8", PrimitiveKind.Cube, new Vector3(0.3f, 0.005f, 0.21f), 6));
        Add(Make("reference_letter", "Reference Letter", "Glowing praise from somebody who definitely exists.", "reference", ItemRarity.Uncommon, "#F0E6D2", PrimitiveKind.Cube, new Vector3(0.21f, 0.004f, 0.3f), 6));
        Add(Make("old_resume", "Crumpled Resume", "A resume copy. Slightly wet, but readable.", "resume", ItemRarity.Uncommon, "#D8D8D0", PrimitiveKind.Cube, new Vector3(0.21f, 0.004f, 0.3f), 5));
        Add(Make("portfolio_folder", "Portfolio Folder", "A thick folder of work samples.", "portfolio", ItemRarity.Uncommon, "#6B4F3A", PrimitiveKind.Cube, new Vector3(0.24f, 0.03f, 0.32f), 6));
    }

    static void AddPerJob(InterviewDatabase db)
    {
        if (db == null || db.jobs == null) return;
        for (int i = 0; i < db.jobs.Length; i++)
        {
            JobData j = db.jobs[i];
            if (j == null || string.IsNullOrEmpty(j.id) || j.proof == null || string.IsNullOrEmpty(j.proof.item)) continue;
            ItemDef d = Make("proof_" + j.id, j.proof.item, "Your proof of skill for this line of work.", "demo,portfolio", ItemRarity.Rare, HexFor(j.id), PrimitiveKind.Cube, new Vector3(0.2f, 0.1f, 0.15f), 8);
            d.jobIds = new string[] { j.id };
            Add(d);
        }

        for (int f = 0; f < Flavors.Length; f++)
        {
            List<string> ids = new List<string>();
            for (int i = 0; i < db.jobs.Length; i++)
                if (MatchesKeys(db.jobs[i], Flavors[f].keys)) ids.Add(db.jobs[i].id);
            if (ids.Count == 0) continue;
            ItemDef d = Make(Flavors[f].id, Flavors[f].name, Flavors[f].desc, Flavors[f].sat, Flavors[f].rarity, Flavors[f].hex, Flavors[f].prim, Flavors[f].size, 6);
            d.jobIds = ids.ToArray();
            Add(d);
        }
    }

    static bool MatchesKeys(JobData job, string[] keys)
    {
        if (job == null) return false;
        string[] a = Tokens(job.id);
        string[] b = Tokens(job.category);
        for (int k = 0; k < keys.Length; k++)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] == keys[k]) return true;
            for (int i = 0; i < b.Length; i++) if (b[i] == keys[k]) return true;
        }
        return false;
    }

    static string[] Tokens(string s)
    {
        if (string.IsNullOrEmpty(s)) return new string[0];
        return s.ToLowerInvariant().Split(new char[] { '_', '-', ' ', '.', '/' }, System.StringSplitOptions.RemoveEmptyEntries);
    }

    static string HexFor(string id)
    {
        int h = 17;
        for (int i = 0; i < id.Length; i++) h = h * 31 + id[i];
        Color c = Color.HSVToRGB(Mathf.Abs(h % 100) / 100f, 0.45f, 0.8f);
        return "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    public static string CodeOf(string name)
    {
        string[] words = name.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        string s;
        if (words.Length >= 2) s = words[0].Substring(0, 1) + words[1].Substring(0, 1);
        else if (words.Length == 1) s = words[0].Length >= 2 ? words[0].Substring(0, 2) : words[0];
        else s = "??";
        return s.ToUpperInvariant();
    }

    public static List<ItemDef> ForJob(JobData job)
    {
        Ensure();
        List<ItemDef> result = new List<ItemDef>();
        for (int i = 0; i < all.Count; i++)
            if (all[i].IsGeneric || all[i].FitsJob(job)) result.Add(all[i]);
        return result;
    }

    static int Weight(ItemDef d)
    {
        switch (d.rarity)
        {
            case ItemRarity.Common: return 6;
            case ItemRarity.Uncommon: return 4;
        }
        return 2;
    }

    static ItemDef PickWeighted(List<ItemDef> pool, System.Random rng)
    {
        int total = 0;
        for (int i = 0; i < pool.Count; i++) total += Weight(pool[i]);
        int roll = rng.Next(Mathf.Max(1, total));
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= Weight(pool[i]);
            if (roll < 0) return pool[i];
        }
        return pool.Count > 0 ? pool[0] : null;
    }

    public static ItemDef RandomFor(JobData job, System.Random rng, float biasToJob)
    {
        Ensure();
        List<ItemDef> jobPool = new List<ItemDef>();
        List<ItemDef> generic = new List<ItemDef>();
        for (int i = 0; i < all.Count; i++)
        {
            ItemDef d = all[i];
            if (PlayerInventory.Has(d.id)) continue;
            if (d.FitsJob(job)) jobPool.Add(d);
            else if (d.IsGeneric) generic.Add(d);
        }
        bool wantJob = jobPool.Count > 0 && (generic.Count == 0 || rng.NextDouble() < biasToJob);
        List<ItemDef> pool = wantJob ? jobPool : generic;
        if (pool.Count == 0) pool = all;
        return PickWeighted(pool, rng);
    }
}
