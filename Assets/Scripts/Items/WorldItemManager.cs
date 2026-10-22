using System;
using System.Collections.Generic;
using UnityEngine;

namespace Items
{
    // Spawns, drops, rests, picks up and cleans WorldItems. Auto-creates itself on first use.
    public class WorldItemManager : MonoBehaviour
    {
        public static int MaxItems = 30;
        public static float DropDistance = 1f, Skin = 0.08f, KillY = -100f;

        static WorldItemManager inst;
        static readonly List<WorldItem> items = new List<WorldItem>();
        static Vector3 lastValid; static bool hasValid;
        static WorldItem newest;
        Inventory bound; float tick;

        // Stage 6 listens: pickup blocked because the inventory is full (open swap UI with this item).
        public static event Action<WorldItem> OnPickupBlocked;
        public static event Action<WorldItem, int> OnPickedUp;   // item, slot

        public static int Count { get { Prune(); return items.Count; } }
        public static IList<WorldItem> All { get { Prune(); return items; } }

        public static WorldItemManager Ensure()
        {
            if (inst == null)
            {
                inst = FindAnyObjectByType<WorldItemManager>();
                if (inst == null) inst = new GameObject("WorldItemManager").AddComponent<WorldItemManager>();
            }
            return inst;
        }

        void Awake() { inst = this; }
        void OnDestroy() { Unbind(); if (inst == this) inst = null; }

        void Update()
        {
            if (bound != Inventory.Current) { Unbind(); bound = Inventory.Current; bound.OnItemDropped += OnDropped; }
            tick -= Time.deltaTime;
            if (tick <= 0f) { tick = 0.5f; TrackPlayerGround(); }
        }

        void Unbind() { if (bound != null) bound.OnItemDropped -= OnDropped; bound = null; }

        void OnDropped(ItemInstance it)
        {
            var cam = PlayerCam();
            if (cam == null) { Spawn(it, PlayerPos() + Vector3.up * 0.5f, 0f); return; }
            DropFrom(cam.transform.position, cam.transform.forward, it);
        }

        // ---------- public API ----------
        public static WorldItem Spawn(ItemInstance it, Vector3 pos, float yaw)
        {
            Ensure(); Prune();
            var wi = WorldItem.Create(it, pos, Quaternion.Euler(0f, yaw, 0f));
            items.Add(wi); newest = wi;
            Enforce();
            return wi;
        }

        // Drop in front of origin along dir: ~1 m, shortened at walls, then down to the ground.
        public static WorldItem DropFrom(Vector3 origin, Vector3 dir, ItemInstance it)
        {
            Ensure();
            var probe = new Vector3(0.3f, 0.03f, 0.3f);
            Vector3 pos = FindDropPosition(origin, dir, probe);
            var wi = Spawn(it, pos, Quaternion.LookRotation(Flat(dir)).eulerAngles.y);
            wi.Launch(Flat(dir) * 0.4f, UnityEngine.Random.onUnitSphere * 1.5f);
            return wi;
        }

        public static Vector3 FindDropPosition(Vector3 origin, Vector3 dir, Vector3 probeSize)
        {
            dir = dir.sqrMagnitude < 1e-6f ? Vector3.forward : dir.normalized;
            float dist = DropDistance; RaycastHit hit;
            if (RayGround(origin, dir, DropDistance, out hit)) dist = Mathf.Max(0.15f, hit.distance - Skin);
            Vector3 p = origin + dir * dist;
            if (RayGround(p, Vector3.down, 4f, out hit)) p = hit.point + hit.normal * 0.08f;
            // reject spots inside geometry: nudge back toward the origin, then up
            Vector3 back = (origin - p); back.y = 0f; back = back.sqrMagnitude > 1e-4f ? back.normalized : -Flat(dir);
            for (int i = 0; i < 8 && Overlaps(p, probeSize * 0.5f); i++) p += back * 0.1f + Vector3.up * 0.02f;
            return p;
        }

        public static bool Pickup(WorldItem w)
        {
            if (w == null || w.Removed || w.Item == null) return false;
            int slot;
            if (!Inventory.Current.TryAdd(w.Item, out slot))
            {
                if (OnPickupBlocked != null) OnPickupBlocked(w);
                return false;
            }
            Remove(w);
            ItemSfx.PlayFor(w.Item); ItemSfx.Haptic(0.1f, 0.2f, 0.06f);
            HoldSlot(slot);                       // picked-up items go straight into the player's hand
            if (OnPickedUp != null) OnPickedUp(w, slot);
            return true;
        }

        // Full inventory: put the world item into `slot`, drop the displaced item where the world item was.
        public static bool SwapPickup(WorldItem w, int slot)
        {
            if (w == null || w.Removed) return false;
            Vector3 pos = w.transform.position; float yaw = w.transform.eulerAngles.y;
            var old = Inventory.Current.SwapIn(slot, w.Item);
            Remove(w);
            if (old != null) Spawn(old, pos + Vector3.up * 0.05f, yaw);
            HoldSlot(slot);
            return true;
        }

        // Stage 9: lets non-world sources (events, NPC rewards) open the swap UI for an item they just dropped.
        public static void RaiseBlocked(WorldItem w) { if (w != null && OnPickupBlocked != null) OnPickupBlocked(w); }

        public static void HoldSlot(int slot)
        {
            var hold = FindAnyObjectByType<ItemHold>();
            if (hold != null) hold.Equip(slot);   // retries by itself if the hand rig isn't ready yet
        }

        public static void Remove(WorldItem w)
        {
            if (w == null) return;
            w.MarkRemoved(); items.Remove(w);
            if (newest == w) newest = null;
            WorldUtil.Kill(w.gameObject);
        }

        public static void ClearAll() { for (int i = items.Count - 1; i >= 0; i--) if (items[i] != null) Remove(items[i]); items.Clear(); }

        // ---------- safety net ----------
        public static bool IsOutOfBounds(Vector3 p)
        {
            return float.IsNaN(p.y) || p.y < KillY || (hasValid && p.y < lastValid.y - 40f);
        }

        public static void RecordValid(Vector3 p) { lastValid = p; hasValid = true; }

        public static void Rescue(WorldItem w)
        {
            if (w == null || w.Removed) return;
            Vector3 basePos = hasValid ? lastValid : PlayerPos();
            Vector3 p; RaycastHit hit;
            if (RayGround(basePos + Vector3.up * 1f, Vector3.down, 6f, out hit)) p = hit.point + hit.normal * 0.08f; else p = basePos + Vector3.up * 0.3f;
            w.ResetPhysics();
            w.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, w.transform.eulerAngles.y, 0f));
        }

        // ---------- helpers ----------
        public static bool IsIgnored(Collider c)
        {
            if (c == null) return true;
            if (c.GetComponentInParent<WorldItem>() != null) return true;
            var pl = PlayerTf();
            return pl != null && c.transform.IsChildOf(pl.root);
        }

        // Nearest non-trigger hit that isn't a WorldItem or the player.
        public static bool RayGround(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default(RaycastHit); float bd = float.MaxValue; bool found = false;
            var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsIgnored(hits[i].collider) || hits[i].distance <= 0f || hits[i].distance >= bd) continue;
                bd = hits[i].distance; best = hits[i]; found = true;
            }
            return found;
        }

        static bool Overlaps(Vector3 center, Vector3 half)
        {
            var cols = Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++) if (!IsIgnored(cols[i])) return true;
            return false;
        }

        static Vector3 Flat(Vector3 d) { d.y = 0f; return d.sqrMagnitude < 1e-6f ? Vector3.forward : d.normalized; }

        static Transform PlayerTf() { var d = NpcDirector.Instance; return d != null ? d.Player : null; }
        static Vector3 PlayerPos() { var t = PlayerTf(); return t != null ? t.position : Vector3.zero; }
        static Camera PlayerCam() { var d = NpcDirector.Instance; var c = d != null ? d.PlayerCamera : null; return c != null ? c : Camera.main; }

        void TrackPlayerGround()
        {
            var t = PlayerTf(); if (t == null) return;
            RaycastHit hit;
            if (RayGround(t.position + Vector3.up * 0.5f, Vector3.down, 2f, out hit)) RecordValid(hit.point);
        }

        static void Prune() { items.RemoveAll(w => w == null); }
        public static void NotifyDestroyed(WorldItem w) { items.Remove(w); }

        // Oldest despawn first; never the newest drop, nor anything near / in view of the player.
        static void Enforce()
        {
            var cam = PlayerCam();
            while (items.Count > MaxItems)
            {
                WorldItem victim = null;
                for (int i = 0; i < items.Count && victim == null; i++)
                {
                    var w = items[i];
                    if (w == null) { victim = w; continue; }
                    if (w == newest || InView(cam, w.transform.position)) continue;
                    victim = w;
                }
                if (victim == null) break;   // everything protected: allow temporary overflow
                Remove(victim);
            }
        }

        static bool InView(Camera cam, Vector3 p)
        {
            if (cam == null) return false;
            if ((cam.transform.position - p).sqrMagnitude < 9f) return true;
            Vector3 v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f && (cam.transform.position - p).sqrMagnitude < 900f;
        }
    }
}
