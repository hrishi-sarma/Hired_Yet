using UnityEngine;

public class InterviewRequest
{
    public string tag;
    public string item;
    public string label;
    public string shortName;
}

public struct RequestOutcome
{
    public bool good;
    public float delta;
    public string result;
    public string tag;
    public Remark reply;
}

public static class RequestRules
{
    public static string[] PickLabels;                       // non-null: buttons show these labels instead of inventory slots
    public static string PickHint;                           // optional hint line under the buttons
    public static readonly System.Collections.Generic.HashSet<int> Shown = new System.Collections.Generic.HashSet<int>();
    public static Items.ItemInstance Handed;                 // document the player is handing over (held in hand)
    public static bool GaveAny;                              // at least one document was handed over this request
    public static Items.ItemInstance PendingBluff;           // set by Judge/JudgeExtra when the item carries someone else's name

    public const float WrongItem = 18f;
    public const float Missing = 14f;
    public const float ResumeCredit = 8f;
    public const float JobBonus = 1.5f;

    public static InterviewRequest ForProof(JobData job)
    {
        string item = job != null && job.proof != null && !string.IsNullOrEmpty(job.proof.item) ? job.proof.item : "credentials";
        InterviewRequest r = new InterviewRequest();
        r.item = item;
        r.tag = TagFor(item);
        r.label = "PROOF";
        r.shortName = Debrief.Short(item.ToUpperInvariant(), 12);
        return r;
    }

    public static InterviewRequest ForResume()
    {
        InterviewRequest r = new InterviewRequest();
        r.item = "resume";
        r.tag = "resume";
        r.label = "RESUME";
        r.shortName = "RESUME";
        return r;
    }

    static bool Has(string s, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
            if (s.Contains(keys[i])) return true;
        return false;
    }

    static string TagFor(string item)
    {
        string s = " " + item.ToLowerInvariant();
        if (Has(s, "certif", "licen", "diploma", "degree")) return "certification";
        if (Has(s, "passport", "badge", " id ")) return "id";
        if (Has(s, "reference", "letter")) return "reference";
        if (Has(s, "portfolio", "sample", "book", "folder", "sketch")) return "portfolio";
        return "demo";
    }

    static RequestOutcome Make(bool good, float delta, string result, string tag, Remark reply)
    {
        RequestOutcome o = new RequestOutcome();
        o.good = good;
        o.delta = delta;
        o.result = result;
        o.tag = tag;
        o.reply = reply;
        return o;
    }

    public static RequestOutcome Judge(InterviewRequest r, int option, JobData job)
    {
        RequestOutcome missing = Make(false, Missing, "NOTHING TO SHOW", " ! NOTHING TO SHOW ", Remark.ProofBad);
        Items.ItemInstance item = option >= 1 && option <= Items.Inventory.SlotCount
            ? InterviewInventoryBridge.Get(option - 1) : null;
        return JudgeItem(r, item, job);
    }

    /// <summary>Judges one handed-over document against what the interviewer asked for.</summary>
    public static RequestOutcome JudgeItem(InterviewRequest r, Items.ItemInstance item, JobData job)
    {
        RequestOutcome missing = Make(false, Missing, "NOTHING TO SHOW", " ! NOTHING TO SHOW ", Remark.ProofBad);
        if (item == null) return missing;

        InterviewInventoryBridge.NotifyShown(item);
        string name = item.def.name.ToUpperInvariant();
        bool satisfies = InterviewInventoryBridge.Satisfies(item, r.tag);
        int suspicion = InterviewInventoryBridge.Suspicion(item);
        int crossCheck = InterviewInventoryBridge.RegisterPresented(item);
        float credit = InterviewInventoryBridge.Credit(item, job);
        suspicion = InterviewInventoryBridge.Cap(suspicion + crossCheck);
        PendingBluff = InterviewInventoryBridge.IsBluffable(item) ? item : null;
        Debug.Log("[Interview] " + item.def.name + " | name=" + item.nameState + " base=" + Items.SuspicionConfig.For(item) + " cross=" + crossCheck + " capped=" + suspicion + " credit=" + credit.ToString("0.0") + " satisfies=" + satisfies);
        if (satisfies)
        {
            // A useful document earns credit, while questionable names add suspicion.
            float delta = -credit + suspicion;
            return Make(true, delta, name + " SHOWN", "proof accepted: " + item.def.name.ToLowerInvariant(), Remark.ProofOk);
        }

        // Showing the wrong item is worse when it also looks suspicious.
        return Make(false, WrongItem + suspicion, "WRONG ITEM", " ! WRONG ITEM: " + name + " ", Remark.ProofBad);
    }

    public static RequestOutcome JudgeExtra(Items.ItemInstance item, JobData job)
    {
        InterviewInventoryBridge.NotifyShown(item);
        string name = item.def.name.ToUpperInvariant();
        int baseS = InterviewInventoryBridge.Suspicion(item);
        int cross = InterviewInventoryBridge.RegisterPresented(item);
        int suspicion = InterviewInventoryBridge.Cap(baseS + cross);
        PendingBluff = InterviewInventoryBridge.IsBluffable(item) ? item : null;
        float credit = InterviewInventoryBridge.Credit(item, job);
        Debug.Log("[Interview] EXTRA " + item.def.name + " | name=" + item.nameState + " base=" + baseS + " cross=" + cross + " capped=" + suspicion + " credit=" + credit.ToString("0.0"));
        if (item.def.id == 19) return Make(true, -1f + suspicion, "GUITAR SHOWN", "guitar: placeholder reaction", Remark.ProofOk);       // 8d stub
        if (item.def.id == 20) return Make(false, 25f, "WEAPON SHOWN", " ! WEAPON ", Remark.ProofBad);                                      // 8d stub
        if (item.def.id == 18)                                                                                                               // 8d mini-event
        {
            if (Random.value < 0.5f) return Make(true, -2f, "ENVELOPE: NOTE", "envelope: glowing note inside", Remark.ProofOk);
            return Make(false, 4f, "ENVELOPE: EMPTY", " ! ENVELOPE EMPTY ", Remark.ProofBad);
        }
        float delta = -credit * 0.5f + suspicion;
        bool good = delta <= 0f;
        return Make(good, delta, name + " SHOWN", "extra: " + item.def.name.ToLowerInvariant(), good ? Remark.ProofOk : Remark.ProofBad);
    }

    public static string ShownName(string raw)
    {
        if (PickLabels != null)
        {
            int p;
            if (raw == "none") return "NONE";
            if (int.TryParse(raw, out p) && p >= 1 && p <= PickLabels.Length) return PickLabels[p - 1];
        }
        if (raw == "give") return Handed != null ? Handed.def.name.ToUpperInvariant() : "DOCUMENT";
        if (raw == "none") return "NOTHING / AT HOME";
        int slot;
        if (int.TryParse(raw, out slot) && slot >= 1 && slot <= Items.Inventory.SlotCount)
        {
            Items.ItemInstance item = InterviewInventoryBridge.Get(slot - 1);
            return item != null ? item.def.name.ToUpperInvariant() : "EMPTY SLOT";
        }
        return raw.ToUpperInvariant();
    }
}
