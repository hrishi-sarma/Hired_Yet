using UnityEngine;
using UnityEngine.InputSystem;

namespace Items
{
    [System.Serializable]
    public struct HoldPose
    {
        public Vector3 pos, euler;
        public float scale;
        public HoldPose(Vector3 p, Vector3 e, float s) { pos = p; euler = e; scale = s; }
    }

    public static class HoldPoses
    {
        public static HoldPose Paper = new HoldPose(new Vector3(-0.045f, 0.025f, 0.16f), new Vector3(-75f, 0f, 8f), 0.8f);
        public static HoldPose SmallPaper = new HoldPose(new Vector3(-0.045f, 0.015f, 0.14f), new Vector3(-70f, 0f, 8f), 1.1f);
        public static HoldPose Card = new HoldPose(new Vector3(-0.045f, 0.005f, 0.12f), new Vector3(-70f, 0f, 8f), 1.7f);
        public static HoldPose Booklet = new HoldPose(new Vector3(-0.045f, 0.025f, 0.15f), new Vector3(-80f, 0f, 8f), 0.9f);
        public static HoldPose Envelope = new HoldPose(new Vector3(-0.045f, 0.015f, 0.14f), new Vector3(-75f, 0f, 8f), 0.9f);
        public static HoldPose Guitar = new HoldPose(new Vector3(0.05f, 0.055f, 0.05f), new Vector3(-20f, 90f, 0f), 0.55f);
        public static HoldPose Gun = new HoldPose(new Vector3(0f, -0.055f, 0.17f), new Vector3(0f, 0f, 0f), 1f);

        public static HoldPose Get(ItemDef d)
        {
            switch (d.form)
            {
                case ItemForm.Paper: return Paper;
                case ItemForm.SmallPaper: return SmallPaper;
                case ItemForm.Card: return Card;
                case ItemForm.Booklet: return Booklet;
                case ItemForm.Envelope: return Envelope;
                default: return d.id == 19 ? Guitar : Gun;
            }
        }
    }

    [DefaultExecutionOrder(1000)]
    public class ItemHold : MonoBehaviour
    {
        public Transform socket;
        public float raiseTime = 0.35f;
        public Vector3 raiseOffset = new Vector3(0f, -0.12f, -0.05f);
        public float raiseTilt = 35f;
        // Flat items (paper/card/booklet/envelope) are aligned to the camera so they face the viewer upright.
        public float readPitch = 12f;   // top of the sheet leans away from the viewer
        public float readYaw = 0f;      // small turn about vertical if desired
        public Vector3 readOffset = new Vector3(-0.14f, -0.045f, 0f); // camera-space shift: -x = left, +y = up
        bool faceCam;

        public ItemInstance Current { get; private set; }
        public GameObject HeldObject { get; private set; }
        public int HeldSlot { get; private set; } = -1;

        Inventory inv;
        HoldPose pose;
        float t;
        int pendingSlot = -1;
        ItemInstance lastItem;   // last thing held, for the F toggle

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }
        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) { Ensure(); }
        static void Ensure()
        {
            if (FindAnyObjectByType<ItemHold>() == null)
                new GameObject("ItemHold").AddComponent<ItemHold>();
        }

        Transform ResolveSocket()
        {
            if (socket != null) return socket;
            return FPAnimation.FPEvents.HandR;
        }

        public bool ToggleSlot(int slot)
        {
            if (slot < 0 || slot >= Inventory.SlotCount) return false;
            if (HeldSlot == slot && Current != null) { Unequip(); return true; }
            return Equip(slot);
        }

        public bool Equip(int slot)
        {
            if (slot < 0 || slot >= Inventory.SlotCount) return false;
            var item = Inventory.Current.Get(slot);
            if (item == null) { pendingSlot = -1; Unequip(); return false; }
            if (ResolveSocket() == null) { pendingSlot = slot; return false; }
            pendingSlot = -1;
            if (!Equip(item)) return false;
            HeldSlot = slot;
            return true;
        }

        public bool Equip(ItemInstance item)
        {
            var s = ResolveSocket();
            if (s == null || item == null) return false;
            pendingSlot = -1;
            Unequip();
            Bind();
            Current = item; lastItem = item; HeldSlot = inv != null ? inv.IndexOf(item) : -1;
            HeldObject = item.def.id == GuitarPlayer.GuitarId ? GuitarModel.Create(item, "Held_Guitar") : ItemMeshes.CreateObject(item, "Held_" + item.def.name);
            foreach (var tr0 in HeldObject.GetComponentsInChildren<Transform>(true)) tr0.gameObject.layer = s.gameObject.layer;
            foreach (var r in HeldObject.GetComponentsInChildren<Renderer>(true))
            { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            HeldObject.transform.SetParent(s, false);
            pose = HoldPoses.Get(item.def);
            faceCam = item.def.form != ItemForm.Object;
            HeldObject.transform.localScale = Vector3.one * pose.scale;
            FPAnimation.FPEvents.HoldingItem = true;
            t = 0f; Apply(0f);
            return true;
        }

        // F: put away what's in hand, or bring back the last held item (falls back to the first item you own)
        public void ToggleHold()
        {
            if (Current != null) { pendingSlot = -1; Unequip(); return; }
            var inv0 = Inventory.Current;
            int slot = lastItem != null ? inv0.IndexOf(lastItem) : -1;
            if (slot < 0) for (int i = 0; i < Inventory.SlotCount; i++) if (!inv0.IsEmpty(i)) { slot = i; break; }
            if (slot >= 0) Equip(slot);
        }

        public void Unequip()
        {
            if (HeldObject != null) WorldUtil.Kill(HeldObject);
            HeldObject = null; Current = null; HeldSlot = -1;
            FPAnimation.FPEvents.HoldingItem = false;
        }

        public void Refresh()
        {
            if (HeldObject != null && Current != null && Current.def.id != GuitarPlayer.GuitarId) ItemMeshes.RefreshTexture(HeldObject, Current);
        }

        void Bind()
        {
            var cur = Inventory.Current;
            if (inv == cur) return;
            Unbind(); inv = cur;
            inv.OnItemChanged += Changed; inv.OnItemRemoved += Removed;
        }

        void Unbind()
        {
            if (inv == null) return;
            inv.OnItemChanged -= Changed; inv.OnItemRemoved -= Removed; inv = null;
        }

        void Changed(int slot, ItemInstance it)
        {
            if (it == Current)
            {
                HeldSlot = slot;
                Refresh();
            }
            else if (Current != null && inv != null)
            {
                int currentSlot = inv.IndexOf(Current);
                if (currentSlot >= 0) HeldSlot = currentSlot;
            }
        }
        void Removed(int slot, ItemInstance it) { if (it == Current) Unequip(); }

        void OnDestroy() { Unbind(); Unequip(); }

        static float OutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        void Apply(float k)
        {
            if (HeldObject == null) return;
            float e = OutBack(Mathf.Clamp01(k)), inv1 = 1f - e;
            var tr = HeldObject.transform;
            tr.localPosition = pose.pos + raiseOffset * inv1;
            if (faceCam && Camera.main != null)
            {
                // local +Y (text face) -> toward camera, local +Z (page top) -> camera up, local +X -> camera right
                var cam = Camera.main.transform;
                var face = Quaternion.LookRotation(Vector3.up, Vector3.back);
                var tilt = Quaternion.Euler(readPitch + raiseTilt * inv1, readYaw, 0f);
                tr.rotation = cam.rotation * tilt * face;
                tr.position += cam.rotation * (readOffset);
            }
            else
                tr.localRotation = Quaternion.Euler(pose.euler + new Vector3(raiseTilt * inv1, 0f, 0f));
        }

        void LateUpdate()
        {
            // re-apply after the arm rig is solved so wrist roll / sway never tilts the sheet
            if (HeldObject != null && faceCam && t >= 1f) Apply(1f);
        }

        void Update()
        {
            // The first-person rig is created at runtime. Retry a requested equip once its hand exists.
            if (pendingSlot >= 0 && ResolveSocket() != null)
            {
                int slot = pendingSlot;
                pendingSlot = -1;
                Equip(slot);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && !InventoryUI.IsOpen && !InventoryUI.Locked && !DevConsole.Active && !EventPanelUI.InputLock && !Booklet.Active)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Equip(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) Equip(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) Equip(2);
                else if (keyboard.fKey.wasPressedThisFrame) ToggleHold();
                else if (keyboard.gKey.wasPressedThisFrame && HeldSlot >= 0 && !InventoryUI.NoDrop)
                {
                    int dropSlot = HeldSlot;
                    pendingSlot = -1;
                    Inventory.Current.Drop(dropSlot); // WorldItemManager turns this into a pickup-able world item.
                    Unequip();
                }
            }

            if (HeldObject != null && t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, raiseTime);
                Apply(t);
            }
        }
    }
}
