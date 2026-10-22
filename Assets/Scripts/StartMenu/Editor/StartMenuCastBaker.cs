using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage 4B: records closed loops from the lane / sidewalk graphs while the WorldContext still exists (called by StartMenuBaker.Bake)
/// and places the fixed roaming cast as scene objects driven by MenuPathFollower:
/// cars on highway + suburb lanes, NPCs / dogs / cats on suburb sidewalks. City and forest stay empty.
/// </summary>
public static class StartMenuCastBaker
{
    const int Cars = 6, Npcs = 0, Dogs = 3, Cats = 3, Butterflies = 5, Birds = 10;
    const float FlyerScale = 1.6f;   // birds/butterflies are tiny at menu-camera distance
    const string RootName = "Menu Cast";

    [MenuItem("Tools/Start Menu/Clear Roaming Cast")]
    public static void Clear()
    {
        var go = GameObject.Find(RootName);
        if (go != null) { UnityEngine.Object.DestroyImmediate(go); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); }
    }

    sealed class Poly
    {
        public List<Vector3> p = new List<Vector3>(); public List<float> v = new List<float>(); public bool closed = true; public float length;
        public void Add(Vector3 q, float s) { if (p.Count > 0 && (p[p.Count - 1] - q).sqrMagnitude < 0.0025f) return; p.Add(q); v.Add(s); }
        public void Finish() { length = 0f; for (int i = 1; i < p.Count; i++) length += Vector3.Distance(p[i - 1], p[i]); if (closed && p.Count > 1) length += Vector3.Distance(p[p.Count - 1], p[0]); }
        public Poly Reversed() { var r = new Poly { closed = closed }; for (int i = p.Count - 1; i >= 0; i--) r.Add(p[i], v[i]); r.Finish(); return r; }
    }

    sealed class Cycle<T> where T : class
    {
        public Func<T, List<T>> succ; public Func<List<T>, bool> ok; public int minN, maxN, budget; public System.Random rng; T start;
        public List<T> Find(T s)
        {
            start = s; var path = new List<T> { s };
            return Dfs(path) ? path : null;
        }
        bool Dfs(List<T> path)
        {
            if (--budget < 0) return false;
            var list = succ(path[path.Count - 1]);
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = list[i]; list[i] = list[j]; list[j] = t; }
            for (int i = 0; i < list.Count; i++)
            {
                T n = list[i];
                if (n == start) { if (path.Count >= minN && ok(path)) return true; continue; }
                if (path.Count >= maxN || path.Contains(n)) continue;
                path.Add(n);
                if (Dfs(path)) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }
    }

    public static void Bake(WorldGenerator gen, MenuCameraRig rig)
    {
        WorldContext ctx = gen.Context;
        Clear();
        StripCityProps(ctx);
        Vector3 focus = rig != null && rig.Framed ? rig.FocusPoint : Vector3.zero;
        var rng = new System.Random(ctx.settings.seed * 17 + 3);

        var cross = new CrosswalkRegistry();
        PedestrianGraph pg = PedestrianGraph.Build(ctx, cross);
        LaneGraph lg = LaneGraph.Build(ctx, pg, cross);

        GameObject tmp = new GameObject("~bakeDirector") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            NpcDirector dir = tmp.AddComponent<NpcDirector>();
            if (!CopySettings(gen, dir)) Debug.LogWarning("StartMenu cast: no NpcDirector settings found, using defaults (primitive models).");
            dir.PrepareForBake(ctx);
            var pool = new AgentPool();

            Transform root = new GameObject(RootName).transform;
            Transform carsT = Group(root, "Cars"), npcT = Group(root, "NPCs"), dogT = Group(root, "Dogs"), catT = Group(root, "Cats");

            // ---------------- cars: suburb-only loops (never despawn)
            var trips = FindTrips(lg, focus, rng);
            int placed = 0;
            if (trips.Count > 0)
                for (int i = 0; i < Cars; i++)
                {
                    int li = i % trips.Count, onLoop = 0, k = 0;
                    for (int j = 0; j < Cars; j++) if (j % trips.Count == li) { if (j < i) k++; onLoop++; }
                    PlaceCar(pool, dir, carsT, trips[li], k, onLoop); placed++;
                }
            else Debug.LogWarning("StartMenu cast: no suburb-to-highway route found near the shot, cars skipped.");

            // ---------------- walkers + animals
            var walkLoops = FindWalkLoops(pg, focus, rng);
            if (walkLoops.Count == 0) Debug.LogWarning("StartMenu cast: no suburb sidewalk loop found, NPCs/animals skipped.");
            else
            {
                for (int i = 0; i < Npcs; i++)
                {
                    int li = i % walkLoops.Count; Poly lp = walkLoops[li].forward;
                    bool rev = (i / walkLoops.Count) % 2 == 1; if (rev) lp = walkLoops[li].backward;
                    PlaceNpc(pool, dir, npcT, lp, (float)rng.NextDouble() * lp.length, 1.25f + (float)rng.NextDouble() * 0.35f);
                }
                for (int i = 0; i < Dogs; i++) PlaceAnimal(AnimalSpecies.Dog, dogT, walkLoops[(i + 1) % walkLoops.Count], i, rng);
                for (int i = 0; i < Cats; i++) PlaceAnimal(AnimalSpecies.Cat, catT, walkLoops[(i + 2) % walkLoops.Count], i, rng);
            }
            BakeFlyers(ctx, pg, Group(root, "Flyers"), rng);
            Debug.Log("StartMenu cast: " + placed + " cars, " + (walkLoops.Count > 0 ? Npcs + " NPCs, " + Dogs + " dogs, " + Cats + " cats" : "no walkers") + " baked into '" + RootName + "'.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tmp);
            // LaneGraph.Build spawns runtime-only signal heads (they need the live traffic simulation and spam NullReferenceExceptions in the menu)
            GameObject tl;
            while ((tl = GameObject.Find("TrafficLights (runtime)")) != null) UnityEngine.Object.DestroyImmediate(tl);
        }
    }

    /// <summary>The city only needs ground, roads and buildings (skyline): its props and trees are removed.</summary>
    static void StripCityProps(WorldContext ctx)
    {
        int n = 0;
        foreach (var layer in new[] { PlacementLayer.Prop, PlacementLayer.Tree })
        {
            Transform root = ctx.LayerRoot(layer);
            if (root != null) n += StripCity(root, ctx);
        }
        Debug.Log("StartMenu cast: removed " + n + " city props/trees.");
    }

    static int StripCity(Transform t, WorldContext ctx)
    {
        int n = 0;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Transform c = t.GetChild(i);
            bool group = c.childCount > 0 && c.GetComponent<MeshRenderer>() == null && c.GetComponent<LODGroup>() == null;
            if (group) { n += StripCity(c, ctx); continue; }
            if ((ctx.zoneMap != null ? ctx.zoneMap.ZoneAt(c.position.x, c.position.z) : ZoneType.Open) == ZoneType.City) { UnityEngine.Object.DestroyImmediate(c.gameObject); n++; }
        }
        return n;
    }

    // ------------------------------------------------------------------ flyers (4C)
    static void BakeFlyers(WorldContext ctx, PedestrianGraph pg, Transform parent, System.Random rng)
    {
        var edges = new List<PedEdge>();
        foreach (var e in pg.Sidewalks) if (WalkOk(e)) edges.Add(e);
        if (edges.Count == 0) { Debug.LogWarning("StartMenu cast: no suburb edges for flyers."); return; }
        FlyerSpecies[] birds = { FlyerSpecies.Sparrow, FlyerSpecies.Robin, FlyerSpecies.BlueJay, FlyerSpecies.Cardinal, FlyerSpecies.Pigeon, FlyerSpecies.Crow };
        for (int i = 0; i < Butterflies + Birds; i++)
        {
            bool bf = i < Butterflies;
            PedEdge e = edges[rng.Next(edges.Count)];
            Vector2 m = (e.a + e.b) * 0.5f;
            float ground = Mathf.Max(ctx.HeightAt(m.x, m.y), e.sideY);
            FlyerSpecies sp = bf ? FlyerSpecies.Butterfly : birds[rng.Next(bf ? 1 : birds.Length)];
            var go = new GameObject((bf ? "Butterfly " : "Bird ") + (i + 1));
            go.transform.SetParent(parent, false);
            FlyerRig rig = FlyerModelFactory.Build(sp, go.transform, rng, 1f);
            rig.visual.localScale = Vector3.one * FlyerScale;
            var f = go.AddComponent<MenuFlyer>();
            f.butterfly = bf;
            f.phase = (float)rng.NextDouble() * 6.28f;
            f.direction = rng.Next(2) * 2 - 1;
            f.wings = rig.wings;
            if (bf) { f.center = new Vector3(m.x, ground + 1.8f + (float)rng.NextDouble() * 1.5f, m.y); f.extent = new Vector3(3f + (float)rng.NextDouble() * 4f, 0.7f, 3f + (float)rng.NextDouble() * 4f); }
            else { f.center = new Vector3(m.x, ground + 14f + (float)rng.NextDouble() * 14f, m.y); f.extent = new Vector3(35f + (float)rng.NextDouble() * 45f, 2.5f, 0f); f.speed = 7f + (float)rng.NextDouble() * 3f; }
            go.transform.position = f.center;
        }
    }

    static Transform Group(Transform root, string name) { var t = new GameObject(name).transform; t.SetParent(root, false); return t; }

    // ------------------------------------------------------------------ settings
    static bool CopySettings(WorldGenerator gen, NpcDirector target)
    {
        var src = gen.GetComponent<NpcDirector>();
        if (src == null) src = UnityEngine.Object.FindFirstObjectByType<NpcDirector>(FindObjectsInactive.Include);
        if (src != null) { EditorUtility.CopySerialized(src, target); return true; }
        string path = null;
        foreach (string g in AssetDatabase.FindAssets("t:Scene " + WorldLauncher.WorldSceneName))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == WorldLauncher.WorldSceneName) { path = p; break; }
        }
        if (path == null) return false;
        Scene s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        bool ok = false;
        try
        {
            foreach (var go in s.GetRootGameObjects())
            {
                var d = go.GetComponentInChildren<NpcDirector>(true);
                if (d != null) { EditorUtility.CopySerialized(d, target); ok = true; break; }
            }
        }
        finally { EditorSceneManager.CloseScene(s, true); }
        return ok;
    }

    // ------------------------------------------------------------------ cars
    static bool CarOk(RoadPath l) { return l != null && l.kind == PathKind.Lane && (l.zone == ZoneType.Suburb || l.zone == ZoneType.Highway); }

    /// <summary>Routes that start in the suburb, reach a highway lane and come back. Prefers a true graph cycle touching both zones;
    /// otherwise an out-and-back chain (return leg shifted to the opposite side of the road).</summary>
    static List<Poly> FindTrips(LaneGraph lg, Vector3 focus, System.Random rng)
    {
        var conn = new Dictionary<long, RoadPath>();
        Func<RoadPath, RoadPath, long> key = (a, b) => ((long)a.GetHashCode() << 32) ^ (uint)b.GetHashCode();
        Func<RoadPath, List<RoadPath>> succ = l =>
        {
            var r = new List<RoadPath>(4);
            for (int i = 0; i < l.outs.Count; i++)
            {
                RoadPath c = l.outs[i];
                if (c.next != null && CarOk(c.next) && c.next.zone == ZoneType.Suburb) { r.Add(c.next); conn[key(l, c.next)] = c; }
            }
            return r;
        };
        Func<List<RoadPath>, bool, Poly> build = (cyc, closed) =>
        {
            var poly = new Poly { closed = true };
            int n = cyc.Count;
            for (int i = 0; i < n; i++)
            {
                RoadPath l = cyc[i];
                AppendPath(poly, l, l.speed * 0.85f);
                if (i + 1 < n || closed)
                {
                    RoadPath nx = cyc[(i + 1) % n], c;
                    if (conn.TryGetValue(key(l, nx), out c)) AppendPath(poly, c, (c.speed > 0f ? c.speed : l.speed * 0.6f) * 0.85f);
                }
            }
            if (!closed)   // out-and-back: return along the same road, shifted onto the opposite side
            {
                var back = new List<Vector3>(poly.p); var bv = new List<float>(poly.v);
                for (int i = back.Count - 1; i >= 0; i--)
                {
                    Vector3 d = i > 0 ? back[i - 1] - back[i] : back[i] - back[i + 1];
                    d.y = 0f; if (d.sqrMagnitude < 1e-6f) d = Vector3.forward; d.Normalize();
                    poly.Add(back[i] + new Vector3(d.z, 0f, -d.x) * 3.6f, bv[i]);
                }
            }
            poly.Finish(); return poly;
        };
        Func<List<RoadPath>, bool> touchesBoth = p =>
        {
            bool sub = false, hw = false;
            for (int i = 0; i < p.Count; i++) { if (p[i].zone == ZoneType.Highway) hw = true; else if (p[i].zone == ZoneType.Suburb) sub = true; }
            return sub && hw;
        };

        var lanes = new List<RoadPath>();
        foreach (var l in lg.Lanes) if (l.zone == ZoneType.Suburb && CarOk(l)) lanes.Add(l);
        lanes.Sort((a, b) => Dist(a, focus).CompareTo(Dist(b, focus)));

        var trips = new List<Poly>();
        var used = new HashSet<RoadPath>();
        int tries = 0;
        // 1) closed loops on suburb lanes only
        foreach (var l in lanes)
        {
            if (trips.Count >= 3 || ++tries > 40) break;
            if (used.Contains(l)) continue;
            var cy = new Cycle<RoadPath> { succ = succ, minN = 4, maxN = 40, budget = 6000, rng = rng, ok = p => { for (int i = 0; i < p.Count; i++) if (used.Contains(p[i])) return false; return true; } };
            var found = cy.Find(l);
            if (found == null) continue;
            var poly = build(found, true);
            if (poly.length < 150f) continue;
            foreach (var f in found) used.Add(f);
            trips.Add(poly);
        }
        // 2) fallback: out-and-back along a suburb chain (BFS to depth 8)
        if (trips.Count == 0)
        {
            tries = 0;
            foreach (var l in lanes)
            {
                if (trips.Count >= 2 || ++tries > 30) break;
                var prev = new Dictionary<RoadPath, RoadPath> { { l, null } }; var depth = new Dictionary<RoadPath, int> { { l, 0 } };
                var q = new Queue<RoadPath>(); q.Enqueue(l);
                RoadPath goal = null;
                while (q.Count > 0 && goal == null && prev.Count < 4000)
                {
                    RoadPath c = q.Dequeue();
                    foreach (var n in succ(c)) { if (prev.ContainsKey(n)) continue; prev[n] = c; depth[n] = depth[c] + 1; if (depth[n] >= 8) { goal = n; break; } q.Enqueue(n); }
                }
                if (goal == null) continue;
                var chain = new List<RoadPath>();
                for (RoadPath c = goal; c != null; c = prev[c]) chain.Insert(0, c);
                var poly = build(chain, false);
                if (poly.length < 150f) continue;
                trips.Add(poly);
            }
        }
        return trips;
    }

    static float Dist(RoadPath l, Vector3 f) { Vector2 m = (l.min + l.max) * 0.5f; return (new Vector2(f.x, f.z) - m).sqrMagnitude; }

    static void AppendPath(Poly poly, RoadPath p, float speed)
    {
        for (int i = 0; i < p.pts.Length; i++) poly.Add(new Vector3(p.pts[i].x, p.ys[i], p.pts[i].y), Mathf.Max(1f, speed));
    }

    static void PlaceCar(AgentPool pool, NpcDirector dir, Transform parent, Poly loop, int index, int count)
    {
        int prof;
        GameObject go = pool.BuildForBake(dir, AgentKind.Car, parent, out prof);
        go.name = "Car " + parent.childCount;
        Strip(go);
        var f = go.AddComponent<MenuPathFollower>();
        f.kind = MenuPathFollower.Kind.Car;
        Fill(f, loop, loop.length * index / Mathf.Max(1, count));
        f.lookAhead = 3.5f; f.turnRate = 7f;
        go.transform.position = loop.p[0];
    }

    // ------------------------------------------------------------------ walkers
    sealed class WalkLoop { public Poly forward, backward; public List<PedEdge> edges; }

    static bool WalkOk(PedEdge e) { return e != null && e.kind == PedEdgeKind.Sidewalk && e.zone == ZoneType.Suburb; }

    static Vector3 Lat(PedEdge e, float s, float lateral) { Vector2 p = e.Point(s) + e.left * lateral; return new Vector3(p.x, e.sideY, p.y); }

    static Poly WalkPoly(List<PedEdge> edges, float lateralMul, float speed)
    {
        var poly = new Poly();
        foreach (var e in edges) { poly.Add(Lat(e, 0f, e.lane * lateralMul), speed); poly.Add(Lat(e, e.length, e.lane * lateralMul), speed); }
        poly.Finish(); return poly;
    }

    static List<WalkLoop> FindWalkLoops(PedestrianGraph pg, Vector3 focus, System.Random rng)
    {
        var edges = new List<PedEdge>();
        foreach (var e in pg.Sidewalks) if (WalkOk(e) && e.length > 3f) edges.Add(e);
        edges.Sort((a, b) => EdgeDist(a, focus).CompareTo(EdgeDist(b, focus)));
        Func<PedEdge, List<PedEdge>> succ = e =>
        {
            var r = new List<PedEdge>(4);
            if (e.to == null) return r;
            for (int i = 0; i < e.to.outgoing.Count; i++) { var n = e.to.outgoing[i]; if (WalkOk(n) && n != e.reverse) r.Add(n); }
            return r;
        };
        var loops = new List<WalkLoop>();
        var used = new HashSet<PedEdge>();
        int tries = 0;
        foreach (var e in edges)
        {
            if (loops.Count >= 4 || ++tries > 120) break;
            if (used.Contains(e)) continue;
            var cy = new Cycle<PedEdge> { succ = succ, minN = 3, maxN = 24, budget = 2000, rng = rng, ok = p => { float L = 0; for (int i = 0; i < p.Count; i++) { if (used.Contains(p[i])) return false; L += p[i].length; } return L >= 50f; } };
            var found = cy.Find(e);
            if (found == null) continue;
            foreach (var f in found) { used.Add(f); if (f.reverse != null) used.Add(f.reverse); }
            loops.Add(MakeLoop(found));
        }
        if (loops.Count == 0)   // fallback: out-and-back along a short chain (closed polyline, people simply turn around)
        {
            foreach (var e in edges)
            {
                var chain = new List<PedEdge> { e };
                for (int i = 0; i < 5; i++) { var s = succ(chain[chain.Count - 1]); if (s.Count == 0) break; chain.Add(s[rng.Next(s.Count)]); }
                bool allRev = true; foreach (var c in chain) if (c.reverse == null) allRev = false;
                if (!allRev) continue;
                var full = new List<PedEdge>(chain);
                for (int i = chain.Count - 1; i >= 0; i--) full.Add(chain[i].reverse);
                loops.Add(MakeLoop(full)); break;
            }
        }
        return loops;
    }

    static WalkLoop MakeLoop(List<PedEdge> cyc)
    {
        var l = new WalkLoop { edges = cyc, forward = WalkPoly(cyc, 1f, 1.4f) };
        bool ok = true; foreach (var e in cyc) if (e.reverse == null) ok = false;
        if (ok) { var rev = new List<PedEdge>(); for (int i = cyc.Count - 1; i >= 0; i--) rev.Add(cyc[i].reverse); l.backward = WalkPoly(rev, 1f, 1.4f); }
        else l.backward = l.forward.Reversed();
        return l;
    }

    static float EdgeDist(PedEdge e, Vector3 f) { return (new Vector2(f.x, f.z) - (e.a + e.b) * 0.5f).sqrMagnitude; }

    static void PlaceNpc(AgentPool pool, NpcDirector dir, Transform parent, Poly loop, float startDist, float speed)
    {
        int prof;
        GameObject go = pool.BuildForBake(dir, AgentKind.Pedestrian, parent, out prof);
        go.name = "NPC " + parent.childCount;
        Strip(go);
        var f = go.AddComponent<MenuPathFollower>();
        f.kind = MenuPathFollower.Kind.Walker;
        Fill(f, loop, startDist);
        f.speedMul = speed / 1.4f; f.lookAhead = 0.8f; f.turnRate = 10f;
        var list = dir.settings.npcProfiles;
        if (prof >= 0 && prof < list.Count) f.animSet = list[prof].animationSet;
        f.defaultAnimSet = dir.settings.defaultAnimations;
        go.transform.position = loop.p[0];
    }

    static void PlaceAnimal(AnimalSpecies sp, Transform parent, WalkLoop wl, int index, System.Random rng)
    {
        bool rev = index % 2 == 1;
        Poly lp = WalkPoly(rev && wl.backward != null ? ReverseEdges(wl) : wl.edges, -1f, 1f);   // opposite sidewalk lane to the people
        var go = new GameObject((sp == AnimalSpecies.Dog ? "Dog " : "Cat ") + (index + 1));
        go.transform.SetParent(parent, false);
        AnimalRig rig = AnimalModelFactory.Build(sp, go.transform, rng);
        rig.visual.localScale = Vector3.one * rig.sizeMul;
        var f = go.AddComponent<MenuPathFollower>();
        f.kind = MenuPathFollower.Kind.Animal;
        Fill(f, lp, (float)rng.NextDouble() * lp.length);
        bool dog = sp == AnimalSpecies.Dog;
        f.speedMul = dog ? 1.2f : 0.7f; f.lookAhead = 0.6f; f.turnRate = 8f; f.legSwing = dog ? 30f : 24f;
        if (!dog) { f.moveSeconds = 6f; f.restSeconds = 5f; }
        f.legs = rig.legs; f.tail = rig.tail;
        go.transform.position = lp.p[0];
    }

    static List<PedEdge> ReverseEdges(WalkLoop wl)
    {
        var r = new List<PedEdge>();
        for (int i = wl.edges.Count - 1; i >= 0; i--) r.Add(wl.edges[i].reverse != null ? wl.edges[i].reverse : wl.edges[i]);
        return r;
    }

    // ------------------------------------------------------------------ helpers
    static void Fill(MenuPathFollower f, Poly poly, float startDist)
    {
        f.pts = poly.p.ToArray(); f.speeds = poly.v.ToArray(); f.closed = poly.closed; f.startDistance = startDist;
    }

    /// <summary>Removes everything AgentPool added that only the AI needs (Car/Pedestrian, NpcAnimator, physics, audio).</summary>
    static void Strip(GameObject go)
    {
        foreach (var a in go.GetComponents<AgentBase>()) UnityEngine.Object.DestroyImmediate(a);
        foreach (var a in go.GetComponents<NpcAnimator>()) UnityEngine.Object.DestroyImmediate(a);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(r);
        foreach (var s in go.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(s);
        go.layer = 0;
    }
}
