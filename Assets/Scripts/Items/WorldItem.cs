using UnityEngine;

namespace Items
{
    // An ItemInstance living in the world: mesh + thin physical proxy + generous pickup trigger.
    // Never uses the (possibly crumpled) render mesh as a collider.
    public class WorldItem : MonoBehaviour, IInteractable
    {
        public ItemInstance Item { get; private set; }
        public bool Resting { get; private set; }
        public bool Removed { get; private set; }
        public float SpawnTime { get; private set; }

        Rigidbody rb;
        BoxCollider proxy, trigger;
        Vector3 localCenter, size;
        float stillTime, age;
        bool stuckChecked, registered;

        public Vector3 ProxySize { get { return size; } }
        public Vector3 ProxyCenterLocal { get { return localCenter; } }
        public Bounds WorldBounds { get { return proxy != null ? proxy.bounds : new Bounds(transform.position, Vector3.one * 0.1f); } }

        public static WorldItem Create(ItemInstance item, Vector3 pos, Quaternion rot)
        {
            var go = ItemMeshes.CreateObject(item, "Item_" + item.def.id);
            go.transform.SetPositionAndRotation(pos, rot);
            var wi = go.AddComponent<WorldItem>();
            wi.Init(item);
            ItemFx.Attach(go, item);   // Stage 10: sparkle on Rare+, glow on Legendary
            return wi;
        }

        void Init(ItemInstance item)
        {
            Item = item; SpawnTime = Time.time;
            var b = ItemMeshes.Build(item).bounds;
            float minY = item.condition == Condition.Crumpled ? 0.015f : (item.def.form == ItemForm.Object ? 0.04f : 0.006f);
            size = new Vector3(Mathf.Max(b.size.x, 0.02f), Mathf.Max(b.size.y, minY), Mathf.Max(b.size.z, 0.02f));
            localCenter = b.center; localCenter.y = b.center.y + (size.y - b.size.y) * 0.5f;

            proxy = gameObject.AddComponent<BoxCollider>();
            proxy.size = size; proxy.center = localCenter;

            // Generous trigger so thin items are easy to target.
            trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size + new Vector3(0.2f, 0.2f, 0.2f); trigger.center = localCenter;

            rb = gameObject.AddComponent<Rigidbody>();
            bool paper = item.def.form != ItemForm.Object && item.def.form != ItemForm.Booklet;
            rb.mass = paper ? 0.02f : (item.def.form == ItemForm.Booklet ? 0.15f : 0.8f);
            rb.linearDamping = paper ? 4f : 0.5f;
            rb.angularDamping = paper ? 3f : 0.8f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.sleepThreshold = 0.01f;
        }

        public void Launch(Vector3 velocity, Vector3 torque)
        {
            if (rb == null || rb.isKinematic) return;
            rb.linearVelocity = velocity; rb.AddTorque(torque, ForceMode.VelocityChange);
        }

        public bool IsFlat { get { return Item.def.form != ItemForm.Object; } }

        void OnEnable() { TryRegister(); }
        void OnDisable() { Unregister(); }
        void OnDestroy() { Unregister(); WorldItemManager.NotifyDestroyed(this); }

        void TryRegister()
        {
            if (registered || InteractionSystem.Instance == null) return;
            InteractionSystem.Instance.Register(this); registered = true;
        }
        void Unregister()
        {
            if (!registered) return;
            if (InteractionSystem.Instance != null) InteractionSystem.Instance.Unregister(this);
            registered = false;
        }

        void Update() { if (!registered) TryRegister(); }

        void FixedUpdate()
        {
            if (Removed || rb == null) return;
            GuardColliders();
            age += Time.fixedDeltaTime;

            if (!Resting)
            {
                if (WorldItemManager.IsOutOfBounds(transform.position)) { WorldItemManager.Rescue(this); return; }
                bool slow = rb.linearVelocity.sqrMagnitude < 0.0004f && rb.angularVelocity.sqrMagnitude < 0.01f;
                stillTime = (slow || rb.IsSleeping()) ? stillTime + Time.fixedDeltaTime : 0f;
                if (stillTime > 0.5f || age > 8f) Settle();
            }
            if (!stuckChecked && age > 2f) { stuckChecked = true; if (IsStuck()) WorldItemManager.Rescue(this); }
        }

        // PropMeshColliderEnforcer (World) swaps colliders on anything whose name looks like a prop. Keep ours intact.
        void GuardColliders()
        {
            var mc = GetComponent<MeshCollider>();
            if (mc != null) WorldUtil.Kill(mc);
            if (proxy == null) { proxy = gameObject.AddComponent<BoxCollider>(); proxy.size = size; proxy.center = localCenter; }
            if (trigger == null) { trigger = gameObject.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = size + new Vector3(0.2f, 0.2f, 0.2f); trigger.center = localCenter; }
        }

        // Snap flush to the surface below, lock rotation to the surface normal (flat forms).
        public void Settle()
        {
            RaycastHit hit;
            Vector3 c = transform.TransformPoint(localCenter);
            if (WorldItemManager.RayGround(c + Vector3.up * 0.05f, Vector3.down, 0.6f, out hit))
            {
                Quaternion rot = transform.rotation;
                if (IsFlat)
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, hit.normal);
                    if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(transform.up, hit.normal);
                    if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
                    rot = Quaternion.LookRotation(fwd.normalized, hit.normal);
                }
                float half = size.y * 0.5f;
                Vector3 centerWorld = hit.point + hit.normal * (half + 0.002f);
                transform.SetPositionAndRotation(centerWorld - rot * localCenter, rot);
                if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody != rb)
                    transform.SetParent(hit.collider.attachedRigidbody.transform, true);   // ride vehicles / platforms
            }
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            Resting = true;
            WorldItemManager.RecordValid(transform.position);
        }

        bool IsStuck()
        {
            GuardColliders();
            var cols = Physics.OverlapBox(proxy.bounds.center, size * 0.5f * 0.9f, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var o = cols[i];
                if (o == proxy || o.transform.IsChildOf(transform) || WorldItemManager.IsIgnored(o)) continue;
                Vector3 dir; float dist;
                if (Physics.ComputePenetration(proxy, transform.position, transform.rotation, o, o.transform.position, o.transform.rotation, out dir, out dist) && dist > 0.02f)
                    return true;
            }
            return false;
        }

        public void ResetPhysics()
        {
            transform.SetParent(null, true);
            rb.isKinematic = false; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            Resting = false; stillTime = 0f; age = 0f; stuckChecked = false; rb.WakeUp();
        }

        public void MarkRemoved() { Removed = true; Unregister(); }

        // ---- IInteractable ----
        public Vector3 Position { get { return transform.position; } }
        public float Range { get { return 2.2f; } }
        public bool CanInteract { get { return !Removed && Item != null; } }
        public string Prompt
        {
            get
            {
                string n = Item.def.name;
                if (Item.condition == Condition.Crumpled) n = "Crumpled " + n;
                return "E: Pick up " + n;
            }
        }
        public void Interact() { WorldItemManager.Pickup(this); }
    }
}
