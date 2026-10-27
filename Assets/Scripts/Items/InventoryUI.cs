using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Items
{
    /// <summary>
    /// Runtime-built inventory styled like the interview HUD: flat bright blocks, thick dark outline,
    /// hard drop shadow, taped paper panel. Add to any scene object; it creates its own canvas.
    /// All layout uses centre-anchored coordinates relative to the parent (no mixed pivots).
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public ItemHold itemHold;
        public bool pauseWhileOpen = false;   // the bag is opened in real time: the world keeps running behind it

        static readonly Color Ink = UIKit.Dark;
        static readonly Color Cream = UIKit.Cream;
        static readonly Color PaperC = new Color(1f, .96f, .84f, 1f);
        static readonly Color Muted = new Color(.15f, .12f, .2f, .6f);
        static readonly Color Line = new Color(.15f, .12f, .2f, .22f);
        const float CardW = 250f, CardH = 290f, CardY = -5f, CardGap = 270f;

        Canvas canvas; GameObject root, context, confirm, tooltip;
        Button[] slotButtons = new Button[3]; Text[] slotLabels = new Text[3], slotDetail = new Text[3], slotGlyph = new Text[3];
        GameObject[] slotLines = new GameObject[3]; Image[] slotStripe = new Image[3];
        Text message, tipTitle, tipSub, tipState, tipCompare, tipNone, tipRisk; Image tipRiskBox; Image[] tipFill = new Image[4]; Text[] tipName = new Text[4], tipDelta = new Text[4]; GameObject[] tipRow = new GameObject[4];
        int selected = -1; WorldItem pendingPickup; bool open; float previousTimeScale = 1f;
        Inventory bound; Font uiFont; bool fontApplied;
        public static bool IsOpen { get; private set; }
        public static bool Locked;   // interview: inventory can't be opened (and quick-equip keys are off) until the resume is requested
        public static bool NoDrop;   // interview: nothing may be dropped on the floor
        static InventoryUI instance;
        CursorLockMode prevLock; bool prevVisible;
        public static void RequestOpen() { if (instance != null && !Locked && !instance.open) instance.SetOpen(true); }
        public static void RequestClose() { if (instance != null && instance.open) instance.SetOpen(false); }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            // AfterSceneLoad only fires once at game start, so also install on every later scene load
            // (resume scene -> world scene used to leave the world without an inventory).
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInstalled(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) { EnsureInstalled(scene); }
        static void EnsureInstalled(UnityEngine.SceneManagement.Scene scene)
        {
            var desk = FindAnyObjectByType<ResumeFormUI>();
            if (desk != null && desk.gameObject.scene == scene) return;   // resume desk has no inventory
            if (FindAnyObjectByType<InventoryUI>() == null) new GameObject("InventoryUI").AddComponent<InventoryUI>();
        }
        void Awake() { instance = this; Build(); Bind(); SetOpen(false); }
        void OnDestroy() { if (instance == this) instance = null; }
        void OnEnable() { WorldItemManager.OnPickupBlocked += PickupBlocked; WorldItemManager.OnPickedUp += PickedUp; Bind(); }
        void OnDisable() { IsOpen = false; WorldItemManager.OnPickupBlocked -= PickupBlocked; WorldItemManager.OnPickedUp -= PickedUp; Unbind(); }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (Booklet.Active) return;   // the tutorial booklet owns the keyboard while it is open
            if (Locked || DevConsole.Active || PhotoMode.Active) { if (open) SetOpen(false); return; }
            if (open && !Booklet.Active) { if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None; if (!Cursor.visible) Cursor.visible = true; }
            if (keyboard.tabKey.wasPressedThisFrame) SetOpen(!open);
            if (open && keyboard.escapeKey.wasPressedThisFrame) { if (confirm != null) { WorldUtil.Kill(confirm); confirm = null; } else if (context != null) DestroyContext(); else SetOpen(false); }
            if (open && keyboard.leftArrowKey.wasPressedThisFrame) Select(Mathf.Max(0, selected - 1));
            if (open && keyboard.rightArrowKey.wasPressedThisFrame) Select(Mathf.Min(2, selected + 1));
        }
        void Bind()
        {
            var now = Inventory.Current; if (bound == now) { Refresh(); return; }
            Unbind(); bound = now; bound.OnItemAdded += ItemChanged; bound.OnItemRemoved += ItemChanged; bound.OnItemChanged += ItemChanged; Refresh();
        }
        void Unbind() { if (bound == null) return; bound.OnItemAdded -= ItemChanged; bound.OnItemRemoved -= ItemChanged; bound.OnItemChanged -= ItemChanged; bound = null; }
        void ItemChanged(int slot, ItemInstance item) { Refresh(); }
        void PickupBlocked(WorldItem w) { pendingPickup = w; SetOpen(true); message.text = "Inventory full — pick a slot to swap."; Select(0); }
        void PickedUp(WorldItem w, int slot) { pendingPickup = null; message.text = "Picked up: " + w.Item.def.name; Refresh(); }

        // ------------------------------------------------------------------ build
        void Build()
        {
            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null) eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null) WorldUtil.Kill(legacyModule);
            var uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null) uiModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();
            ResolveFont();

            var cg = new GameObject("InventoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 120;
            var scaler = cg.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;

            // dim backdrop (click-through blocker)
            var dim = Box(cg.transform, "Dim", new Color(.08f, .06f, .14f, .55f), Vector2.zero, new Vector2(4000, 4000), true);

            root = Box(cg.transform, "InventoryPanel", PaperC, Vector2.zero, new Vector2(900, 520), true);
            UIKit.Boxy(root.GetComponent<Image>(), 4f, true);
            root.AddComponent<InventoryPopAnimation>();
            UIKit.Tape(root.GetComponent<RectTransform>(), -330f, -4f);
            UIKit.Tape(root.GetComponent<RectTransform>(), 330f, 3f);

            // header: title block on the left, "CV" badge on the right
            var head = Box(root.transform, "Header", UIKit.Hex("#FFE27A"), new Vector2(-150, 205), new Vector2(520, 64));
            UIKit.Boxy(head.GetComponent<Image>(), 3f, false);
            Label(head.transform, "Title", "THE INTERVIEW FOLIO", 28, Ink, new Vector2(0, 11), new Vector2(500, 36)).fontStyle = FontStyle.Bold;
            Label(head.transform, "Subtitle", "Papers, contacts & useful finds", 13, Muted, new Vector2(0, -16), new Vector2(500, 22));
            var cv = Box(root.transform, "CVBadge", UIKit.Hex("#FF8A80"), new Vector2(370, 205), new Vector2(76, 64));
            UIKit.Boxy(cv.GetComponent<Image>(), 3f, false);
            Label(cv.transform, "CV", "CV", 30, Ink, Vector2.zero, new Vector2(70, 56)).fontStyle = FontStyle.Bold;

            Color[] slotColors = { UIKit.Hex("#FFB3D1"), UIKit.Hex("#FFB347"), UIKit.Hex("#9EE0FF") };
            string[] slotKinds = { "STARTER FILE", "PAPERWORK", "FOUND ITEMS" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                // card (slightly tilted, like loose folders)
                var card = Box(root.transform, "Card" + i, Cream, new Vector2((i - 1) * CardGap, CardY), new Vector2(CardW, CardH), true);
                UIKit.Boxy(card.GetComponent<Image>(), 4f, true);
                card.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, (1 - i) * 1.2f);
                slotButtons[i] = card.AddComponent<Button>();
                var cb = slotButtons[i].colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, .97f, .85f, 1); cb.pressedColor = new Color(.85f, .85f, .85f, 1); cb.fadeDuration = .05f; slotButtons[i].colors = cb;
                var hover = card.AddComponent<InventorySlotHover>(); hover.owner = this; hover.slot = idx;

                // coloured header strip with number badge + category (all centre-anchored inside the card)
                var strip = Box(card.transform, "Strip", slotColors[i], new Vector2(0, 118), new Vector2(CardW - 24, 40));
                UIKit.Boxy(strip.GetComponent<Image>(), 3f, false);
                var num = Box(strip.transform, "Number", Ink, new Vector2(-(CardW - 24) * .5f + 24, 0), new Vector2(34, 28));
                Label(num.transform, "SlotNumber", "0" + (i + 1), 15, UIKit.Hex("#FFE27A"), Vector2.zero, new Vector2(34, 28)).fontStyle = FontStyle.Bold;
                Label(strip.transform, "Category", slotKinds[i], 14, Ink, new Vector2(20, 0), new Vector2(170, 30)).fontStyle = FontStyle.Bold;

                // paper face
                var face = Box(card.transform, "ItemFace", PaperC, new Vector2(0, 18), new Vector2(200, 150));
                UIKit.Boxy(face.GetComponent<Image>(), 3f, false);
                slotStripe[i] = Box(face.transform, "RarityStripe", slotColors[i], new Vector2(0, 64), new Vector2(188, 14)).GetComponent<Image>();
                var lines = Box(face.transform, "Lines", Color.clear, Vector2.zero, new Vector2(200, 150)); slotLines[i] = lines;
                float[] w = { 150, 150, 110, 130 };
                for (int l = 0; l < 4; l++) Box(lines.transform, "Line" + l, Line, new Vector2((w[l] - 150) * .5f, 26 - l * 20), new Vector2(w[l], 4));
                slotGlyph[i] = Label(face.transform, "Glyph", "", 40, Ink, new Vector2(0, -6), new Vector2(190, 90)); slotGlyph[i].fontStyle = FontStyle.Bold;

                slotLabels[i] = Label(card.transform, "Name", "", 15, Ink, new Vector2(0, -82), new Vector2(226, 40)); slotLabels[i].fontStyle = FontStyle.Bold;
                slotDetail[i] = Label(card.transform, "Detail", "", 11, Muted, new Vector2(0, -116), new Vector2(226, 24));
            }

            message = Label(root.transform, "Message", "", 14, Ink, new Vector2(-135, -218), new Vector2(560, 30));
            message.alignment = TextAnchor.MiddleLeft; message.fontStyle = FontStyle.Bold;
            var close = Button(root.transform, "Close", "CLOSE", new Vector2(320, -218), new Vector2(210, 44), UIKit.Hex("#FF8A80"));
            close.onClick.AddListener(() => SetOpen(false));

            BuildTooltip();
            SetOpen(false);
        }

        void BuildTooltip()
        {
            tooltip = Box(root.transform, "HoverStats", UIKit.Hex("#FFFDF2"), Vector2.zero, new Vector2(236, 232));
            UIKit.Boxy(tooltip.GetComponent<Image>(), 4f, true);
            tipTitle = Label(tooltip.transform, "Title", "", 16, Ink, new Vector2(0, 96), new Vector2(220, 26)); tipTitle.fontStyle = FontStyle.Bold;
            tipSub = Label(tooltip.transform, "Sub", "", 11, Muted, new Vector2(0, 77), new Vector2(220, 18));
            tipState = Label(tooltip.transform, "State", "", 11, Ink, new Vector2(0, 60), new Vector2(220, 18));
            tipCompare = Label(tooltip.transform, "Compare", "", 11, Ink, new Vector2(0, 41), new Vector2(220, 18)); tipCompare.fontStyle = FontStyle.Bold;
            for (int s = 0; s < 4; s++)
            {
                var row = Box(tooltip.transform, "StatRow" + s, Color.clear, new Vector2(0, 16 - s * 22), new Vector2(230, 20));
                tipRow[s] = row;
                var l = Label(row.transform, "Name", "", 10, Ink, new Vector2(-58, 0), new Vector2(100, 16)); l.alignment = TextAnchor.MiddleLeft; l.fontStyle = FontStyle.Bold; tipName[s] = l;
                var bg = Box(row.transform, "BarBg", new Color(.15f, .12f, .2f, .18f), new Vector2(42, 0), new Vector2(76, 12));
                UIKit.Boxy(bg.GetComponent<Image>(), 2f, false);
                var fill = Box(bg.transform, "Fill", UIKit.Hex("#7EE87A"), Vector2.zero, new Vector2(72, 8));
                var fr = fill.GetComponent<RectTransform>(); fr.anchorMin = fr.anchorMax = new Vector2(0, .5f); fr.pivot = new Vector2(0, .5f); fr.anchoredPosition = new Vector2(2, 0);
                tipFill[s] = fill.GetComponent<Image>();
                var d = Label(row.transform, "Delta", "", 11, Ink, new Vector2(100, 0), new Vector2(36, 16)); d.fontStyle = FontStyle.Bold; tipDelta[s] = d;
            }
            tipNone = Label(tooltip.transform, "NoStats", "No stats — special item", 11, Muted, new Vector2(0, 16), new Vector2(220, 20));
            var risk = Box(tooltip.transform, "RiskBox", UIKit.Hex("#FFE27A"), new Vector2(0, -96), new Vector2(200, 30));
            UIKit.Boxy(risk.GetComponent<Image>(), 3f, false); tipRiskBox = risk.GetComponent<Image>();
            tipRisk = Label(risk.transform, "Risk", "", 12, Ink, Vector2.zero, new Vector2(196, 26)); tipRisk.fontStyle = FontStyle.Bold;
            tooltip.SetActive(false);
        }

        // ------------------------------------------------------------------ helpers
        void ResolveFont()
        {
            if (uiFont != null) return;
            try
            {
                var ui = FindAnyObjectByType<InterviewUI>(FindObjectsInactive.Include);
                if (ui != null)
                {
                    var f = typeof(InterviewUI).GetField("uiFont", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    uiFont = f != null ? f.GetValue(ui) as Font : null;
                }
            }
            catch (Exception) { }
        }
        Font DefaultFont()
        {
            if (uiFont != null) return uiFont;
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf"); return f;
        }
        void ApplyFont()
        {
            ResolveFont(); if (uiFont == null || fontApplied || root == null) return;
            foreach (var t in root.transform.parent.GetComponentsInChildren<Text>(true)) t.font = uiFont;
            fontApplied = true;
        }
        /// <summary>Centre-anchored rectangle; pos is the centre relative to the parent's centre.</summary>
        GameObject Box(Transform parent, string name, Color color, Vector2 pos, Vector2 size, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size;
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = raycast; return go;
        }
        Text Label(Transform parent, string name, string value, int size, Color color, Vector2 pos, Vector2 dimensions)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = dimensions;
            var t = go.GetComponent<Text>(); t.text = value; t.font = DefaultFont(); t.fontSize = size; t.color = color; t.raycastTarget = false;
            t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.Max(8, size - 4); t.resizeTextMaxSize = size; return t;
        }
        Button Button(Transform parent, string name, string value, Vector2 pos, Vector2 size, Color color)
        {
            var go = Box(parent, name, color, pos, size, true); UIKit.Boxy(go.GetComponent<Image>(), 3f, true);
            var b = go.AddComponent<Button>(); b.onClick.AddListener(UiClick.Play); var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1); cb.pressedColor = new Color(.8f, .8f, .8f, 1); cb.fadeDuration = .05f; b.colors = cb;
            Label(go.transform, "Text", value, 16, Ink, Vector2.zero, size - new Vector2(12, 8)).fontStyle = FontStyle.Bold; return b;
        }
        // world keeps running while the bag is open, but the player is frozen: movement + look scripts off, only the cursor is free
        readonly System.Collections.Generic.List<Behaviour> lookOff = new System.Collections.Generic.List<Behaviour>();
        void LockLook(bool on)
        {
            if (on)
            {
                if (lookOff.Count > 0) return;
                Transform p = SaveSystem.FindPlayer(); if (p == null) return;
                foreach (var b in p.GetComponents<MonoBehaviour>()) if (b != null && b.enabled) { b.enabled = false; lookOff.Add(b); }
                foreach (var pi in p.GetComponentsInChildren<UnityEngine.InputSystem.PlayerInput>()) if (pi != null && pi.enabled) { pi.enabled = false; lookOff.Add(pi); }
                foreach (var b in p.GetComponentsInChildren<MonoBehaviour>())
                {
                    if (b == null || !b.enabled) continue;
                    string n = b.GetType().Name;
                    if (n.IndexOf("Look", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("MouseOrbit", System.StringComparison.OrdinalIgnoreCase) >= 0) { b.enabled = false; lookOff.Add(b); }
                }
            }
            else { foreach (var b in lookOff) if (b != null) b.enabled = true; lookOff.Clear(); }
        }
        void SetOpen(bool value)
        {
            bool was = open;
            open = value; IsOpen = value; if (value != was) ItemSfx.Play(ItemSfx.Kind.Pop, 0.6f); if (root != null) root.transform.parent.gameObject.SetActive(value);
            if (value) { ApplyFont(); Bind(); Refresh(); if (!was) { previousTimeScale = Time.timeScale; prevLock = Cursor.lockState; prevVisible = Cursor.visible; } if (pauseWhileOpen) Time.timeScale = 0f; LockLook(true); Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
            else { LockLook(false); if (pauseWhileOpen && was) Time.timeScale = previousTimeScale; if (was) { Cursor.visible = prevVisible; Cursor.lockState = prevLock; } else { Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked; } DestroyContext(); if (confirm != null) { WorldUtil.Kill(confirm); confirm = null; } }
        }

        // ------------------------------------------------------------------ hover / refresh
        public void ShowHover(int slot, bool visible)
        {
            if (tooltip == null) return;
            var item = Inventory.Current != null ? Inventory.Current.Get(slot) : null;
            if (!visible || item == null || context != null || confirm != null) { tooltip.SetActive(false); return; }
            tooltip.SetActive(true); tooltip.transform.SetAsLastSibling();
            tooltip.GetComponent<RectTransform>().anchoredPosition = new Vector2((slot - 1) * CardGap, CardY - 10);
            tipTitle.text = item.def.name; tipSub.text = item.def.rarity + "  ·  " + item.def.form; tipState.text = item.NameLabel != "" ? item.NameLabel : StateText(item);
            if (item.condition == Condition.Crumpled) tipState.text = "Crumpled · " + (item.NameLabel != "" ? item.NameLabel : "name unreadable");
            var cmp = ItemCompare.Build(Inventory.Current, slot);
            tipCompare.text = cmp.against != null ? "vs " + cmp.against.def.name + " (slot " + (cmp.againstSlot + 1) + ")" : "";
            tipNone.gameObject.SetActive(cmp.rows.Count == 0);
            for (int s = 0; s < 4; s++)
            {
                bool on = s < cmp.rows.Count; tipRow[s].SetActive(on); if (!on) continue;
                var row = cmp.rows[s]; tipName[s].text = row.label;
                tipFill[s].rectTransform.sizeDelta = new Vector2(72f * Mathf.Clamp01(row.value / 100f), 8f);
                tipFill[s].color = row.value >= 66 ? UIKit.Hex("#7EE87A") : row.value >= 40 ? UIKit.Hex("#FFE27A") : UIKit.Hex("#FF8A80");
                var d = tipDelta[s];
                if (!row.hasDelta) d.text = "";
                else { d.text = row.delta > 0 ? "▲+" + row.delta : row.delta < 0 ? "▼" + row.delta : "="; d.color = row.delta > 0 ? new Color(.12f, .55f, .2f) : row.delta < 0 ? new Color(.78f, .2f, .2f) : Muted; }
            }
            int pts = SuspicionConfig.For(item); string risk = SuspicionConfig.Level(pts);
            tipRisk.text = "SUSPICION: " + risk + (pts > 0 ? "  +" + pts : "");
            tipRiskBox.color = risk == "HIGH" ? UIKit.Hex("#FF8A80") : risk == "MEDIUM" ? UIKit.Hex("#FFB347") : risk == "LOW" ? UIKit.Hex("#FFE27A") : UIKit.Hex("#7EE87A");
        }
        void Refresh()
        {
            if (slotLabels == null || slotLabels[0] == null) return; BindSafe();
            for (int i = 0; i < 3; i++)
            {
                var it = Inventory.Current.Get(i); bool has = it != null;
                slotLabels[i].text = has ? it.def.name : (i == 0 ? "STARTER RESUME · EMPTY" : "EMPTY SLOT · FIND ITEM");
                slotDetail[i].text = has ? it.def.rarity.ToString().ToUpper() + "  ·  " + it.def.form + (it.condition == Condition.Crumpled ? "  ·  CRUMPLED" : "") : "";
                slotLines[i].SetActive(!has); slotGlyph[i].text = has ? Glyph(it) : "";
                if (has) slotStripe[i].color = RarityColor(it.def.rarity);
            }
        }
        static Color RarityColor(Rarity r)
        {
            switch (r) { case Rarity.Uncommon: return UIKit.Hex("#7EE87A"); case Rarity.Rare: return UIKit.Hex("#9EE0FF"); case Rarity.Epic: return UIKit.Hex("#C8A8FF"); case Rarity.Legendary: return UIKit.Hex("#FFD23F"); default: return UIKit.Hex("#C9D6C3"); }
        }
        static string Glyph(ItemInstance i)
        {
            switch (i.def.docType) { case DocType.Resume: return "CV"; case DocType.CoverLetter: return "CL"; case DocType.Certificate: return "CERT"; }
            switch (i.def.form) { case ItemForm.Card: return "ID"; case ItemForm.Booklet: return "BOOK"; case ItemForm.Envelope: return "MAIL"; case ItemForm.Object: return "★"; case ItemForm.SmallPaper: return "SLIP"; default: return "DOC"; }
        }
        void BindSafe() { if (bound != Inventory.Current) Bind(); }
        string StateText(ItemInstance i) { return i.condition == Condition.Crumpled ? "Crumpled · unreadable name" : i.nameState == NameState.Other ? "Other name" : i.nameState == NameState.Blank ? "Blank name" : i.nameState == NameState.Scribbled ? "Name scribbled out" : i.nameState == NameState.Own ? "Name: " + i.printedName : ""; }

        // Slot input: left double-click toggles holding; right-click opens the existing action menu.
        public void HandleSlotClick(int slot, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Select(slot);
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Left) return;
            selected = Mathf.Clamp(slot, 0, 2);
            if (eventData.clickCount >= 2)
            {
                DestroyContext();
                if (tooltip != null) tooltip.SetActive(false);
                if (pendingPickup != null) { Select(selected); return; }
                var item = Inventory.Current != null ? Inventory.Current.Get(selected) : null;
                if (item == null) { message.text = "That slot is empty."; return; }
                if (itemHold == null) itemHold = FindAnyObjectByType<ItemHold>();
                bool ok = itemHold != null && itemHold.ToggleSlot(selected);
                message.text = ok ? (itemHold.HeldSlot == selected ? "Holding: " + item.def.name : "Unequipped: " + item.def.name) : "Waiting for the player's hand rig…";
                return;
            }
            DestroyContext();
            if (tooltip != null) tooltip.SetActive(false);
            message.text = "";
        }

        // ------------------------------------------------------------------ actions
        void Select(int slot)
        {
            selected = Mathf.Clamp(slot, 0, 2); DestroyContext(); if (tooltip != null) tooltip.SetActive(false);
            var it = Inventory.Current.Get(selected);
            var acts = new System.Collections.Generic.List<(string, Color, Action)>();
            if (pendingPickup != null) acts.Add(("Swap pickup", UIKit.Hex("#7EE87A"), () => { if (pendingPickup != null && WorldItemManager.SwapPickup(pendingPickup, selected)) { pendingPickup = null; message.text = "Item swapped."; DestroyContext(); Refresh(); } }));
            else if (it != null)
            {
                if (!NoDrop) acts.Add(("Drop", UIKit.Hex("#FFB347"), () => { if (itemHold != null && itemHold.HeldSlot == selected) itemHold.Unequip(); Inventory.Current.Drop(selected); DestroyContext(); }));
                if (Inventory.Current.CanChangeName(selected)) acts.Add(("Change Name", UIKit.Hex("#FFE27A"), () => ConfirmName(true)));
                if (Inventory.Current.CanAddName(selected)) acts.Add(("Add Name", UIKit.Hex("#FFE27A"), () => ConfirmName(false)));
            }
            acts.Add(("Back", Cream, () => DestroyContext()));
            float h = 76 + acts.Count * 50;
            context = Box(root.transform, "ItemActions", PaperC, new Vector2(0, CardY), new Vector2(290, h), true); UIKit.Boxy(context.GetComponent<Image>(), 4f, true);
            Label(context.transform, "ActionTitle", it == null ? (pendingPickup != null ? "Swap into slot " + (selected + 1) : "Empty slot") : it.def.name, 18, Ink, new Vector2(0, h * .5f - 30), new Vector2(260, 34)).fontStyle = FontStyle.Bold;
            for (int a = 0; a < acts.Count; a++)
            {
                var act = acts[a]; var act2 = act.Item3;
                Button(context.transform, "Action_" + act.Item1, act.Item1, new Vector2(0, h * .5f - 76 - a * 50), new Vector2(240, 40), act.Item2).onClick.AddListener(() => act2());
            }
        }
        void ConfirmName(bool change)
        {
            if (confirm != null) WorldUtil.Kill(confirm);
            confirm = Box(root.transform, "NameConfirm", PaperC, new Vector2(0, CardY), new Vector2(380, 170), true); UIKit.Boxy(confirm.GetComponent<Image>(), 4f, true);
            confirm.transform.SetAsLastSibling();
            Label(confirm.transform, "Question", change ? "Write over the existing name?" : "Add your name to this item?", 17, Ink, new Vector2(0, 40), new Vector2(340, 50)).fontStyle = FontStyle.Bold;
            Button(confirm.transform, "Yes", "CONFIRM", new Vector2(-88, -38), new Vector2(150, 44), UIKit.Hex("#7EE87A")).onClick.AddListener(() => { if (change) Inventory.Current.ChangeName(selected); else Inventory.Current.AddName(selected); ItemSfx.Play(ItemSfx.Kind.Scribble); ItemSfx.Haptic(0.05f, 0.15f, 0.12f); WorldUtil.Kill(confirm); confirm = null; Refresh(); Select(selected); });
            Button(confirm.transform, "No", "CANCEL", new Vector2(88, -38), new Vector2(150, 44), UIKit.Hex("#FF8A80")).onClick.AddListener(() => { WorldUtil.Kill(confirm); confirm = null; });
        }
        void DestroyContext() { if (context != null) WorldUtil.Kill(context); context = null; }

    }

    public sealed class InventorySlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public InventoryUI owner; public int slot;
        public void OnPointerEnter(PointerEventData eventData) { if (owner != null) owner.ShowHover(slot, true); }
        public void OnPointerExit(PointerEventData eventData) { if (owner != null) owner.ShowHover(slot, false); }
        public void OnPointerClick(PointerEventData eventData) { if (owner != null) owner.HandleSlotClick(slot, eventData); }
    }
    public sealed class InventoryPopAnimation : MonoBehaviour
    {
        void OnEnable() { StopAllCoroutines(); StartCoroutine(Pop()); }
        System.Collections.IEnumerator Pop() { Vector3 from = Vector3.one * .94f; float t = 0; transform.localScale = from; while (t < .18f) { t += Time.unscaledDeltaTime; float x = Mathf.Clamp01(t / .18f); x = 1 - Mathf.Pow(1 - x, 3); transform.localScale = Vector3.LerpUnclamped(from, Vector3.one, x); yield return null; } transform.localScale = Vector3.one; }
    }
}
