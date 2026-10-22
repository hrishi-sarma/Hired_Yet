using UnityEngine;
using UnityEngine.UI;

namespace Items
{
    // Stage 9: single entry point for every pickup source (events, NPC rewards, pickpocket, papers).
    // Roll from a SpawnTable -> Offer(): stores + holds it and shows a toast, or (inventory full)
    // drops it at the player's feet and opens the swap UI. The instance (seed included) is never altered.
    public static class ItemPickups
    {
        public enum Result { None, Stored, SwapPrompt }

        public static string Label(ItemInstance it) { return it == null ? "" : it.def.name; }

        public static ItemInstance Roll(SpawnSource s, System.Random r) { return ItemSpawner.RollInstance(s, r, Inventory.Current); }

        // Random NPC table (recruiter / freelancer / student / mentor).
        public static SpawnSource RandomNpcSource(System.Random r)
        {
            switch (r.Next(4)) { case 0: return SpawnSource.NpcRecruiter; case 1: return SpawnSource.NpcFreelancer; case 2: return SpawnSource.NpcStudent; default: return SpawnSource.NpcMentor; }
        }

        // World-event id -> spawn table. Unknown events use a random NPC table.
        public static SpawnSource SourceForEvent(string id, System.Random r)
        {
            switch (id)
            {
                case "flying_documents": return SpawnSource.FlyingPaperTrail;
                case "lost_backpack": return SpawnSource.LostBackpack;
                case "lost_wallet": case "vending_machine": case "runaway_cart": case "stalled_car": return SpawnSource.NpcRecruiter;
                case "street_magician": case "food_truck": case "delivery_boxes": case "beekeeper": return SpawnSource.NpcFreelancer;
                case "lemonade_stand": case "tourist_map": case "mushroom_sorting": case "sprinkler_valve": return SpawnSource.NpcStudent;
                case "hermit_riddle": case "grandma_groceries": case "druid_dance": case "witch_cauldron": return SpawnSource.NpcMentor;
                default: return RandomNpcSource(r);
            }
        }

        public static ItemInstance RollForEvent(string id, System.Random r) { return Roll(SourceForEvent(id, r), r); }

        // Dialogue-node actions.
        public static Result GiveItem(int defId) { return Offer(ItemSpawner.GrantItem(defId)); }
        public static Result GiveRandom(SpawnSource table, System.Random r = null) { return Offer(Roll(table, r ?? new System.Random())); }

        // Loose papers on the ground: scatters pickup-able WorldItems around a point.
        public static void ScatterLoose(Vector3 center, int count, System.Random r, float radius = 1.2f)
        {
            for (int i = 0; i < count; i++)
            {
                var it = Roll(SpawnSource.LooseGroundPapers, r); if (it == null) return;
                float a = (float)(r.NextDouble() * Mathf.PI * 2), d = (float)r.NextDouble() * radius;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * d, 0.5f, Mathf.Sin(a) * d);
                WorldItemManager.Spawn(it, p, (float)r.NextDouble() * 360f);
            }
        }

        public static Result Offer(ItemInstance it, bool toast = true)
        {
            if (it == null) return Result.None;
            int slot;
            if (Inventory.Current.TryAdd(it, out slot))
            {
                WorldItemManager.HoldSlot(slot);
                ItemSfx.PlayFor(it); ItemSfx.Haptic(0.15f, 0.25f, 0.08f);
                if (toast) ItemToast.Show("Found: " + Label(it), it);
                return Result.Stored;
            }
            // Full: it lands at the player's feet as a normal WorldItem and the swap UI opens.
            var d = NpcDirector.Instance;
            var cam = d != null && d.PlayerCamera != null ? d.PlayerCamera : Camera.main;
            WorldItem w = cam != null ? WorldItemManager.DropFrom(cam.transform.position, cam.transform.forward, it)
                                      : WorldItemManager.Spawn(it, (d != null && d.Player != null ? d.Player.position : Vector3.zero) + Vector3.up * 0.5f, 0f);
            ItemSfx.Play(ItemSfx.Kind.Pop);
            if (toast) ItemToast.Show("Inventory full: " + Label(it), it);
            WorldItemManager.RaiseBlocked(w);
            return Result.SwapPrompt;
        }
    }

    // Pickup toast with a small texture preview. Unscaled time so it works while the game is paused.
    public class ItemToast : MonoBehaviour
    {
        static ItemToast inst;
        GameObject box; Text label; RawImage thumb; float hideAt;

        public static void Show(string text, ItemInstance it)
        {
            if (inst == null) { var g = new GameObject("ItemToast"); DontDestroyOnLoad(g); inst = g.AddComponent<ItemToast>(); inst.Build(); }
            inst.Display(text, it);
        }

        void Build()
        {
            var cg = new GameObject("ToastCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cg.transform.SetParent(transform, false);
            var canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 125;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = .5f;

            box = new GameObject("Box", typeof(RectTransform), typeof(Image)); box.transform.SetParent(cg.transform, false);
            var rt = (RectTransform)box.transform; rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f); rt.pivot = new Vector2(.5f, 0f); rt.anchoredPosition = new Vector2(0, 90); rt.sizeDelta = new Vector2(420, 64);
            var img = box.GetComponent<Image>(); img.color = UIKit.Cream; img.raycastTarget = false; UIKit.Boxy(img, 3f, true);

            var tg = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage)); tg.transform.SetParent(box.transform, false);
            var tr = (RectTransform)tg.transform; tr.anchorMin = tr.anchorMax = new Vector2(0, .5f); tr.pivot = new Vector2(0, .5f); tr.anchoredPosition = new Vector2(8, 0); tr.sizeDelta = new Vector2(40, 48);
            thumb = tg.GetComponent<RawImage>(); thumb.raycastTarget = false;

            var lg = new GameObject("Label", typeof(RectTransform), typeof(Text)); lg.transform.SetParent(box.transform, false);
            var lr = (RectTransform)lg.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(58, 4); lr.offsetMax = new Vector2(-10, -4);
            label = lg.GetComponent<Text>(); label.color = UIKit.Dark; label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 10; label.resizeTextMaxSize = 20;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            box.SetActive(false);
        }

        void Display(string text, ItemInstance it)
        {
            label.text = text;
            Texture2D t = null;
            if (it != null && it.def.form != ItemForm.Object) { try { t = ItemTextures.Get(it); } catch (System.Exception) { } }
            thumb.texture = t; thumb.gameObject.SetActive(t != null);
            box.SetActive(true); hideAt = Time.unscaledTime + 2.6f;
        }

        void Update()
        {
            if (box != null && box.activeSelf && Time.unscaledTime > hideAt) box.SetActive(false);
        }
    }
}
