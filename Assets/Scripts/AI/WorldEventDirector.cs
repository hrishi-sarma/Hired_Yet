using System.Collections.Generic;
using UnityEngine;

public class WorldEventDirector
{
    readonly NpcDirector director;
    readonly List<WorldEvent> events = new List<WorldEvent>();
    readonly List<Vector3> placed = new List<Vector3>();
    readonly EventPlacement placement;
    GameObject root;
    int tourIndex;

    public List<WorldEvent> Events
    {
        get { return events; }
    }

    public WorldEventDirector(NpcDirector owner)
    {
        director = owner;
        placement = new EventPlacement(owner);
    }

    public EventPlacement Placement
    {
        get { return placement; }
    }

    struct Pick { public WorldEventConfig cfg; public EventZone zone; public Vector3 pos; public float yaw; public List<OrientedBox> boxes; }

    public void Build()
    {
        Clear();
        if (!director.settings.events.enabled || director.Ctx == null || director.Player == null) return;
        EnsureRoot();
        PaperGustSpawner.Attach(root, director);
        WorldRng rng = new WorldRng(director.Ctx.settings.seed, "events");
        EventSettings st = director.settings.events;
        // 3-4 randomized events per zone; distinct configs, families are only preferred to differ
        List<EventZone> slots = new List<EventZone>();
        EventZone[] zs = { EventZone.Suburb, EventZone.Forest, EventZone.City };
        for (int z = 0; z < zs.Length; z++)
        {
            int n = rng.Range(3, 5);
            for (int i = 0; i < n; i++) slots.Add(zs[z]);
        }
        Dictionary<EventZone, List<WorldEventConfig>> pools = new Dictionary<EventZone, List<WorldEventConfig>>();
        for (int i = 0; i < slots.Count; i++)
            if (!pools.ContainsKey(slots[i])) pools[slots[i]] = WorldEventCatalog.ForZone(slots[i], st.library);

        List<Pick> best = new List<Pick>();
        HashSet<WorldEventConfig> usedCfg = new HashSet<WorldEventConfig>();
        HashSet<EventFamily> usedFam = new HashSet<EventFamily>();
        int baseline = placed.Count;
        for (int i = 0; i < slots.Count; i++)
        {
            List<WorldEventConfig> cands = new List<WorldEventConfig>();
            List<WorldEventConfig> fresh = new List<WorldEventConfig>();
            List<WorldEventConfig> pool = pools[slots[i]];
            for (int k = 0; k < pool.Count; k++)
            {
                if (usedCfg.Contains(pool[k])) continue;
                cands.Add(pool[k]);
                if (!usedFam.Contains(WorldEventCatalog.FamilyOf(pool[k]))) fresh.Add(pool[k]);
            }
            List<WorldEventConfig> src = fresh.Count > 0 ? fresh : cands;
            for (int t = 0; t < st.configTries * 2 && src.Count > 0; t++)
            {
                WorldEventConfig def = PickWeighted(src, rng);
                Pick pk = new Pick { cfg = def, zone = slots[i] };
                if (TryFindSpot(def, slots[i], rng, true, out pk.pos, out pk.yaw, out pk.boxes))
                {
                    best.Add(pk);
                    placed.Add(pk.pos);
                    usedCfg.Add(def);
                    usedFam.Add(WorldEventCatalog.FamilyOf(def));
                    break;
                }
                src.Remove(def);
                if (src.Count == 0 && src == fresh) { src = cands; cands.RemoveAll(c => fresh.Contains(c)); }
            }
        }
        placed.RemoveRange(baseline, placed.Count - baseline);
        if (best.Count < slots.Count) Debug.LogWarning("WorldEventDirector: only placed " + best.Count + " of " + slots.Count + " events.");
        for (int i = 0; i < best.Count; i++)
        {
            Spawn(best[i].cfg, best[i].pos, best[i].yaw, best[i].boxes);
            placed.Add(best[i].pos);
        }
        SpawnStandalone(rng, st);
        SpawnRainAndCrossing(rng);
        if (director.settings.logEvents)
            for (int i = 0; i < events.Count; i++) Debug.Log("World event: " + events[i].cfg.id + " (" + events[i].cfg.zone + ", " + WorldEventCatalog.FamilyOf(events[i].cfg) + ") at " + events[i].transform.position);
    }

    void EnsureRoot()
    {
        if (root != null) return;
        root = new GameObject("World Events");
        root.transform.SetParent(director.transform, false);
    }

    static void CollectPlans(List<EventZone> slots, Dictionary<EventZone, List<WorldEventConfig>> pools, int i, EventFamily[] cur, bool[] used, List<EventFamily[]> outPlans, WorldRng rng)
    {
        if (outPlans.Count >= 60) return;
        if (i == slots.Count)
        {
            outPlans.Add((EventFamily[])cur.Clone());
            return;
        }
        List<WorldEventConfig> pool = pools[slots[i]];
        List<int> fams = new List<int>();
        for (int f = 0; f < used.Length; f++)
        {
            if (used[f]) continue;
            for (int k = 0; k < pool.Count; k++)
                if ((int)WorldEventCatalog.FamilyOf(pool[k]) == f) { fams.Add(f); break; }
        }
        for (int n = fams.Count - 1; n > 0; n--)
        {
            int j = rng.Range(0, n + 1);
            int tmp = fams[n]; fams[n] = fams[j]; fams[j] = tmp;
        }
        for (int n = 0; n < fams.Count; n++)
        {
            used[fams[n]] = true;
            cur[i] = (EventFamily)fams[n];
            CollectPlans(slots, pools, i + 1, cur, used, outPlans, rng);
            used[fams[n]] = false;
        }
    }

    static void Shuffle<T>(List<T> list, WorldRng rng)
    {
        for (int n = list.Count - 1; n > 0; n--)
        {
            int j = rng.Range(0, n + 1);
            T tmp = list[n]; list[n] = list[j]; list[j] = tmp;
        }
    }

    public void Clear()
    {
        for (int i = 0; i < events.Count; i++)
            if (events[i] != null) WorldUtil.Kill(events[i].gameObject);
        events.Clear();
        placed.Clear();
        if (root != null) WorldUtil.Kill(root);
        root = null;
    }

    static WorldEventConfig PickWeighted(List<WorldEventConfig> pool, WorldRng rng)
    {
        float total = 0f;
        for (int i = 0; i < pool.Count; i++) total += pool[i].spawnWeight;
        float roll = rng.Range(0f, total);
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= pool[i].spawnWeight;
            if (roll <= 0f) return pool[i];
        }
        return pool[pool.Count - 1];
    }

    Transform BuildNpc(WorldEventConfig cfg, System.Random rng, Transform parent, float yaw, out EventNpc ctl)
    {
        if (cfg.id == "alien_tourist")
        {
            GameObject alien = AlienBuilder.Build(parent);
            alien.name = "NPC";
            alien.transform.localPosition = Vector3.zero;
            alien.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            ctl = alien.AddComponent<EventNpc>();
            return alien.transform;
        }
        NpcProfile profile = cfg.npcOverride;
        if (profile == null || !profile.IsValid)
        {
            profile = EventNpcStyler.Pick(director.settings.npcProfiles, rng, EventNpcStyler.GenderFor(cfg.id));
        }

        GameObject root;
        Animator anim = null;
        AnimationClip idle = null;
        float fade = 0.2f;
        AnimationClip walk = null;
        if (profile != null)
        {
            float height;
            root = NpcModelBuilder.Build(profile, parent, 0.3f, profile.RollScale((float)rng.NextDouble()), out height, out anim, EventNpcStyler.LookFor(cfg.id));
            ResolvedAnimations ra = NpcAnimationSet.Resolve(profile.animationSet, director.settings.defaultAnimations);
            idle = ra.idle;
            walk = ra.walk;
            fade = ra.crossfade;
            EventNpcStyler.Decorate(cfg.id, root.transform.Find("Model") != null ? root.transform.Find("Model").gameObject : root, anim, root.transform);
        }
        else
        {
            root = AiPrimitives.BuildPedestrian(cfg.shirt, AiPrimitives.RandomSkin(rng), 1.7f, parent);
            EventNpcStyler.Decorate(cfg.id, root, null, root.transform);
        }

        root.name = "NPC";
        root.transform.localPosition = Vector3.zero;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        ctl = root.AddComponent<EventNpc>();
        ctl.Setup(anim, idle, cfg.npcBefore, cfg.npcDuring, cfg.npcAfter, fade, walk);
        return root.transform;
    }

    bool Spaced(Vector3 p)
    {
        EventSettings st = director.settings.events;
        Vector3 pp = director.Player.position;
        float dx = p.x - pp.x;
        float dz = p.z - pp.z;
        if (dx * dx + dz * dz < st.minDistanceFromPlayer * st.minDistanceFromPlayer) return false;
        for (int i = 0; i < placed.Count; i++)
        {
            float ex = p.x - placed[i].x;
            float ez = p.z - placed[i].z;
            if (ex * ex + ez * ez < st.minDistanceBetween * st.minDistanceBetween) return false;
        }
        return true;
    }

    public bool TryFindSpot(WorldEventConfig cfg, WorldRng rng, bool checkSpacing, out Vector3 pos, out float yaw, out List<OrientedBox> boxes)
    {
        EventZone z = cfg.zone;
        if (cfg.id == "lost_backpack") z = EventZone.Forest;
        else if (cfg.id == "lost_wallet") z = rng.Range(0, 2) == 0 ? EventZone.City : EventZone.Suburb;
        return TryFindSpot(cfg, z, rng, checkSpacing, out pos, out yaw, out boxes);
    }

    public bool TryFindSpot(WorldEventConfig cfg, EventZone zone, WorldRng rng, bool checkSpacing, out Vector3 pos, out float yaw, out List<OrientedBox> boxes)
    {
        boxes = null;
        if (zone == EventZone.Forest) return FindForest(cfg, rng, checkSpacing, out pos, out yaw, out boxes);
        return FindStreet(cfg, rng, zone == EventZone.City ? ZoneType.City : ZoneType.Suburb, checkSpacing, out pos, out yaw, out boxes);
    }

    bool FindStreet(WorldEventConfig cfg, WorldRng rng, ZoneType zone, bool checkSpacing, out Vector3 pos, out float yaw, out List<OrientedBox> boxes)
    {
        pos = Vector3.zero;
        yaw = 0f;
        boxes = null;
        PedestrianGraph g = director.PedGraph;
        if (g == null || g.Sidewalks.Count == 0) return false;
        List<PedEdge> list = new List<PedEdge>();
        for (int i = 0; i < g.Sidewalks.Count; i++)
            if (g.Sidewalks[i].zone == zone && g.Sidewalks[i].length > 8f) list.Add(g.Sidewalks[i]);
        if (list.Count == 0) return false;
        float[] lateral = { 1.7f, 2.5f };
        for (int attempt = 0; attempt < 400; attempt++)
        {
            PedEdge e = list[rng.Range(0, list.Count)];
            float s = rng.Range(2.5f, e.length - 2.5f);
            for (int k = 0; k < lateral.Length; k++)
            {
                Vector2 p = e.Point(s) + e.left * (e.innerSign * lateral[k]);
                Vector3 w = new Vector3(p.x, director.GroundY(p.x, p.y, e.sideY + 3f), p.y);
                if (checkSpacing && !Spaced(w)) continue;
                Vector2 outward = -e.left * e.innerSign;
                float yw = Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg;
                List<OrientedBox> bx;
                if (!placement.Validate(cfg, w, yw, e, out bx)) continue;
                pos = w;
                yaw = yw;
                boxes = bx;
                return true;
            }
        }
        return false;
    }

    bool FindForest(WorldEventConfig cfg, WorldRng rng, bool checkSpacing, out Vector3 pos, out float yaw, out List<OrientedBox> boxes)
    {
        pos = Vector3.zero;
        yaw = rng.Range(0f, 360f);
        boxes = null;
        WorldContext ctx = director.Ctx;
        float half = ctx.Size * 0.5f;
        for (int attempt = 0; attempt < 2000; attempt++)
        {
            float x = rng.Range(-half, half);
            float z = rng.Range(-half, half);
            if (ctx.zoneMap.ZoneAt(x, z) != ZoneType.Forest) continue;
            if (ctx.NormalAt(x, z).y < 0.94f) continue;
            float y = ctx.HeightAt(x, z);
            Vector3 w = new Vector3(x, director.GroundY(x, z, y + 20f), z);
            if (checkSpacing && !Spaced(w)) continue;
            float yw = rng.Range(0f, 360f);
            List<OrientedBox> bx;
            if (!placement.Validate(cfg, w, yw, null, out bx)) continue;
            pos = w;
            yaw = yw;
            boxes = bx;
            return true;
        }
        return false;
    }

    WorldEvent Spawn(WorldEventConfig def, Vector3 pos, float yaw, List<OrientedBox> boxes)
    {
        GameObject go = new GameObject("Event " + def.id);
        go.transform.SetParent(root.transform, false);
        go.transform.position = pos;
        placement.Reserve(boxes, go);
        System.Random rng = director.Rng;
        EventNpc npcCtl;
        bool loose = def.id == "lost_backpack" || def.id == "lost_wallet";   // standalone objects have no NPC
        Transform npc = null;
        npcCtl = null;
        if (!loose) npc = BuildNpc(def, rng, go.transform, yaw, out npcCtl);

        Vector3 side = EventPlacement.PropOffset(def, yaw);
        float propYaw = EventPlacement.PropYaw(def, yaw);
        GameObject prop = PropBuilder.Build(def, go.transform, rng, director);
        prop.transform.position = pos + side;
        prop.transform.rotation = Quaternion.Euler(0f, propYaw, 0f);

        WorldEvent ev = go.AddComponent<WorldEvent>();
        ev.npc = npc;
        ev.npcCtl = npcCtl;
        ev.Init(def, director.settings.events.attempts);
        events.Add(ev);
        return ev;
    }

    WorldEventConfig CfgById(string id)
    {
        WorldEventConfig c = WorldEventDefaults.Find(id);
        WorldEventLibrary lib = director.settings.events.library;
        if (lib != null && lib.events != null) foreach (WorldEventConfig x in lib.events) if (x != null && x.id == id && x.enabled) c = x;
        return c;
    }

    /// <summary>Scatters loose backpacks (forest) and wallets (city/suburb): random count each, no NPC, spaced apart, any number may exist.</summary>
    void SpawnStandalone(WorldRng rng, EventSettings st)
    {
        string[] ids = { "lost_backpack", "lost_wallet" };
        int[] lo = { st.backpackMin, st.walletMin }, hi = { st.backpackMax, st.walletMax };
        for (int k = 0; k < ids.Length; k++)
        {
            WorldEventConfig def = CfgById(ids[k]);
            if (def == null) continue;
            int n = rng.Range(lo[k], Mathf.Max(lo[k], hi[k]) + 1);
            for (int i = 0; i < n; i++)
                for (int t = 0; t < 8; t++)
                {
                    Vector3 pos; float yaw; List<OrientedBox> boxes;
                    if (!TryFindSpot(def, rng, false, out pos, out yaw, out boxes)) continue;
                    bool far = (pos - director.Player.position).sqrMagnitude > 15f * 15f;
                    for (int e = 0; far && e < events.Count; e++)
                        if ((events[e].transform.position - pos).sqrMagnitude < st.standaloneSpacing * st.standaloneSpacing) far = false;
                    if (!far) continue;
                    Spawn(def, pos, yaw, boxes);
                    break;
                }
        }
    }

    bool FarFromEvents(Vector3 pos)
    {
        if ((pos - director.Player.position).sqrMagnitude < 15f * 15f) return false;
        for (int e = 0; e < events.Count; e++)
            if ((events[e].transform.position - pos).sqrMagnitude < 25f * 25f) return false;
        return true;
    }

    /// <summary>Two forest campers (only visible while it rains) and two old ladies waiting beside crosswalks.</summary>
    void SpawnRainAndCrossing(WorldRng rng)
    {
        WorldEventConfig fire = CfgById("campfire_rain");
        for (int n = 0; fire != null && n < 2; n++)
            for (int t = 0; t < 8; t++)
            {
                Vector3 pos; float yaw; List<OrientedBox> boxes;
                if (!TryFindSpot(fire, rng, false, out pos, out yaw, out boxes) || !FarFromEvents(pos)) continue;
                Spawn(fire, pos, yaw, boxes);
                break;
            }
        WorldEventConfig elder = CfgById("elderly_crosswalk");
        for (int n = 0; elder != null && n < 2; n++) SpawnCrossing(elder, rng, true);
    }

    WorldEvent SpawnCrossing(WorldEventConfig elder, WorldRng rng, bool spaced)
    {
        PedestrianGraph g = director.PedGraph;
        if (g == null || g.Crosswalks.Count == 0) return null;
        for (int t = 0; t < 16; t++)
        {
            PedEdge e = g.Crosswalks[rng.Range(0, g.Crosswalks.Count)];
            if (e.length < 4f) continue;
            float s = Mathf.Max(0.3f, Mathf.Min(e.curb, e.length * 0.3f) - 0.8f);
            Vector2 h2 = e.Point(s) + e.left * 1.6f, d2 = e.Point(e.length - s) + e.left * 1.6f;
            Vector3 home = new Vector3(h2.x, director.GroundY(h2.x, h2.y, e.sideY + 3f), h2.y);
            if (spaced && !FarFromEvents(home)) continue;
            Vector3 dest = new Vector3(d2.x, director.GroundY(d2.x, d2.y, e.sideY + 3f), d2.y);
            WorldEvent ev = Spawn(elder, home, Mathf.Atan2(e.dir.x, e.dir.y) * Mathf.Rad2Deg, new List<OrientedBox>());
            ev.SetCross(dest);
            return ev;
        }
        return null;
    }

    static readonly string[] CandidateNames = Items.PopCultureNames.Full;

    /// <summary>Three bystanders standing along the pavement beside the finder, each with a floating name plate. One of them owns the wallet.</summary>
    WalletSetup BuildWallet(WorldEventConfig def, Transform parent, Vector3 center, float yaw, System.Random rng)
    {
        WalletSetup ws = new WalletSetup();
        List<string> names = new List<string>(CandidateNames);
        EventUi.Shuffle(names, rng);
        Vector3 tang = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;
        float[] offs = { 3.2f, -3.2f, 5.6f, -5.6f, 8f, -8f, 10.4f, -10.4f };
        for (int i = 0; i < offs.Length && ws.npcs.Count < 3; i++)
        {
            Vector3 p = center + tang * offs[i];
            p.y = director.GroundY(p.x, p.z, center.y + 2f);
            if (!SpotFree(p, parent)) continue;
            EventNpc ctl;
            Transform t = BuildNpc(def, rng, parent, yaw, out ctl);
            t.position = p;
            Vector3 look = center - p; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(look);
            string nm = names[ws.npcs.Count];
            FloatingLabel pl = FloatingLabel.Create("Name", t, 2.4f);
            pl.offset = new Vector3(0f, 2.15f, 0f);
            pl.follow = t;
            pl.maxRange = 26f;
            pl.baseScale = 0.07f;
            pl.Set(nm.ToUpperInvariant(), Color.white);
            pl.gameObject.SetActive(false);
            ws.npcs.Add(t); ws.names.Add(nm); ws.plates.Add(pl);
        }
        return ws;
    }

    static bool SpotFree(Vector3 p, Transform own)
    {
        Collider[] cs = Physics.OverlapBox(p + Vector3.up * 1f, new Vector3(0.4f, 0.9f, 0.4f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cs.Length; i++)
        {
            Collider c = cs[i];
            if (c is TerrainCollider || c.bounds.size.y < 0.5f || c.transform.IsChildOf(own) || c.GetComponent<CharacterController>() != null) continue;
            return false;
        }
        return true;
    }

    WorldEvent spawnedEvent;
    int spawnSeq;

    /// <summary>Spawns one specific event at a valid spot anywhere in its zone and teleports the player to it (DevConsole "events").</summary>
    public WorldEvent SpawnEvent(WorldEventConfig cfg)
    {
        if (cfg == null || director.Ctx == null || director.Player == null) return null;
        EnsureRoot();
        if (cfg.id == "flying_documents")
        {
            PaperGustSpawner ps = root.GetComponent<PaperGustSpawner>();
            if (ps != null && !ps.SpawnNow()) Debug.LogWarning("SpawnEvent: no city sidewalk for the paper gust");
            return null;
        }
        if (spawnedEvent != null)
        {
            events.Remove(spawnedEvent);
            WorldUtil.Kill(spawnedEvent.gameObject);
            spawnedEvent = null;
        }
        WorldRng rng = new WorldRng(director.Ctx.settings.seed, "dev" + (spawnSeq++) + Time.frameCount);
        if (cfg.id == "elderly_crosswalk")
        {
            spawnedEvent = SpawnCrossing(cfg, rng, false);
            if (spawnedEvent == null) { Debug.LogWarning("SpawnEvent: no crosswalk found"); return null; }
            TeleportTo(spawnedEvent);
            return spawnedEvent;
        }
        Vector3 pos;
        float yaw;
        List<OrientedBox> boxes;
        if (!TryFindSpot(cfg, rng, false, out pos, out yaw, out boxes))
        {
            Debug.LogWarning("SpawnEvent: no valid spot for " + cfg.id);
            return null;
        }
        spawnedEvent = Spawn(cfg, pos, yaw, boxes);
        spawnedEvent.alwaysOn = true;
        TeleportTo(spawnedEvent);
        return spawnedEvent;
    }

    public void TeleportTo(WorldEvent ev)
    {
        if (ev == null || director.Player == null) return;
        Vector3 target = ev.transform.position + ev.npc.forward * 2f + Vector3.up * 0.5f;
        CharacterController cc = director.Player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        director.Player.position = target;
        Vector3 look = ev.npc.position - target;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) director.Player.rotation = Quaternion.LookRotation(look);
        if (cc != null) cc.enabled = true;
    }

    public void TeleportToNextEvent()
    {
        if (events.Count == 0 || director.Player == null) return;
        WorldEvent ev = events[tourIndex % events.Count];
        tourIndex++;
        TeleportTo(ev);
        Debug.Log("Teleported to event " + ev.cfg.id + " (" + ev.cfg.zone + ")");
    }
}
