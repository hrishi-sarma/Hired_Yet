using System.Collections.Generic;

public enum EventZone { Suburb, Forest, City }

public enum EventFamily { Carry, HoldHelp, Mash, Timing, Sequence, Other, Object, Lost }

public enum EventStyle { Hold, Mash, Sequence }

public static class WorldEventCatalog
{
    public static List<WorldEventConfig> ForZone(EventZone zone, WorldEventLibrary library)
    {
        List<WorldEventConfig> src = library != null && library.events.Count > 0 ? library.events : WorldEventDefaults.All;
        List<WorldEventConfig> list = new List<WorldEventConfig>();
        for (int i = 0; i < src.Count; i++)
        {
            WorldEventConfig c = src[i];
            if (c != null && c.enabled && ZoneMatch(c, zone) && c.spawnWeight > 0f && c.Implemented) list.Add(c);
        }
        return list;
    }

    /// <summary>Where an event may spawn. flying_documents is not a slot event (PaperGustSpawner runs it, city only); the backpack is always
    /// forest; the wallet is city or suburb. Overrides the asset zone so older saved assets behave too.</summary>
    public static bool ZoneMatch(WorldEventConfig c, EventZone zone)
    {
        if (c.id == "flying_documents") return false;
        if (c.id == "lost_backpack" || c.id == "lost_wallet") return false;   // standalone: scattered by WorldEventDirector.SpawnStandalone
        return c.zone == zone;
    }

    public static EventFamily FamilyOf(WorldEventConfig c)
    {
        if (c.id == "fallen_log") return EventFamily.Carry;
        if (c.id == "lost_wallet") return EventFamily.Lost;
        if (c.handler == EventHandlerKind.Object) return EventFamily.Object;
        switch (c.handler)
        {
            case EventHandlerKind.Hold:
                return c.id == "grandma_groceries" || c.id == "garage_sale_couch" || c.id == "delivery_boxes" ? EventFamily.Carry : EventFamily.HoldHelp;
            case EventHandlerKind.Mash: return c.id == "fallen_log" ? EventFamily.Carry : EventFamily.Mash;
            case EventHandlerKind.Timing: return EventFamily.Timing;
            case EventHandlerKind.Sequence: return EventFamily.Sequence;
        }
        return EventFamily.Other;
    }
}
