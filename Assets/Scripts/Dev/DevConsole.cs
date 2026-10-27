using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Items;

// Developer command line. Shift + ` opens it.
//   events : menu of every world event (spawns it and teleports you next to it)
//   items  : menu of every item; click to put it in the LAST inventory slot
//   teleport office | teleport event<N> | teleport <event id>   (also: "teleport to office", "tp event2")
//   skip   : while the cognitive puzzles are running, finish them all with a perfect score
//   help   : list commands        close / Esc / the X button : close
// Every menu has a close button. While anything is open the game is paused and world interaction is blocked.
public class DevConsole : MonoBehaviour
{
    public static bool Active { get; private set; }

    enum Panel { None, Events, Items }

    static DevConsole inst;
    bool open; Panel panel; string input = "", status = "";
    float prevTimeScale = 1f; CursorLockMode prevLock; bool prevVisible;
    Vector2 scroll; List<WorldEventConfig> events; GUIStyle bold, field, small;
    bool focusNext;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("DevConsole"); DontDestroyOnLoad(go); inst = go.AddComponent<DevConsole>();
    }

    void Update()
    {
        var kb = Keyboard.current; if (kb == null) return;
        if (kb.backquoteKey.wasPressedThisFrame && kb.shiftKey.isPressed) SetOpen(!open);
        else if (open && kb.escapeKey.wasPressedThisFrame) { if (panel != Panel.None) panel = Panel.None; else SetOpen(false); }
    }

    void SetOpen(bool on, bool restoreCursor = true)
    {
        if (on == open) return;
        open = on; Active = on; panel = Panel.None; input = ""; status = ""; focusNext = on;
        if (on)
        {
            prevTimeScale = Time.timeScale; prevLock = Cursor.lockState; prevVisible = Cursor.visible;
            Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else
        {
            Time.timeScale = prevTimeScale;
            if (restoreCursor) { Cursor.lockState = prevLock; Cursor.visible = prevVisible; }
        }
    }

    void OnDisable() { if (open) SetOpen(false); }

    // ---------------------------------------------------------------- commands
    void Run(string cmd)
    {
        cmd = cmd.Trim().ToLowerInvariant();
        if (cmd == "tp" || cmd == "teleport" || cmd.StartsWith("tp ") || cmd.StartsWith("teleport ")) { Teleport(cmd); return; }
        if (cmd == "weather" || cmd.StartsWith("weather ")) { SetWeather(cmd.Substring(7).Trim()); return; }
        switch (cmd)
        {
            case "": return;
            case "events": panel = Panel.Events; status = ""; break;
            case "items": panel = Panel.Items; status = ""; break;
            case "skip": Skip(); break;
            case "devcam": case "dev cam": case "dev camera": SetOpen(false); PhotoMode.RequestDev(); break;
            case "close": case "exit": SetOpen(false); break;
            case "help": status = "Commands: events, items, skip, devcam, weather clear/rain/storm/fog/auto on|off, teleport office / event<N> / <event id>, close"; break;
            default: status = "Unknown command: " + cmd + "  (try: help)"; break;
        }
    }

    void SetWeather(string arg)
    {
        WeatherType t;
        if (WeatherController.Instance == null) { status = "Weather controller not running"; return; }
        if (arg == "auto" || arg == "auto on" || arg == "auto off")
        {
            bool on = arg != "auto off";
            WeatherController.SetAuto(on);
            status = "Automatic weather: " + (on ? "on" : "off");
            return;
        }
        if (arg == "clear" || arg == "off") t = WeatherType.Clear;
        else if (arg == "rain") t = WeatherType.LightRain;
        else if (arg == "storm" || arg == "thunder" || arg == "thunderstorm") t = WeatherType.Thunderstorm;
        else if (arg == "fog") t = WeatherType.MorningFog;
        else { status = "Usage: weather clear / rain / storm / fog / auto on / auto off"; return; }
        WeatherController.Set(t);
        status = "Weather: " + t;
    }

    // "teleport office", "teleport to event2", "tp lost_dog"
    void Teleport(string cmd)
    {
        string arg = cmd.StartsWith("teleport") ? cmd.Substring(8) : cmd.Substring(2);
        arg = arg.Trim();
        if (arg.StartsWith("to ")) arg = arg.Substring(3);
        arg = arg.Trim().Replace(' ', '_');
        var npc = FindFirstObjectByType<NpcDirector>();
        if (npc == null || npc.Player == null) { status = "No player in this scene."; return; }
        if (arg == "") { status = "Usage: teleport office | teleport event<N> | teleport <event id>"; return; }

        Vector3 pos;
        if (arg == "office" || arg == "door" || arg == "interview")
        {
            var door = FindFirstObjectByType<InterviewDoor>();
            if (door == null) { status = "No office door in this scene."; return; }
            pos = door.hasDoorPoint ? door.doorPoint : door.transform.position;
            pos.y = npc.GroundY(pos.x, pos.z, pos.y + 3f) + 0.1f;
        }
        else
        {
            var list = npc.Events != null ? npc.Events.Events : null;
            if (list == null || list.Count == 0) { status = "No events in this world yet."; return; }
            WorldEvent ev = null;
            string num = arg.StartsWith("event") ? arg.Substring(5).Trim('_') : "";
            int n;
            if (num != "" && int.TryParse(num, out n))
            {
                if (n < 1 || n > list.Count) { status = "Event " + n + " doesn't exist (1 to " + list.Count + ")."; return; }
                ev = list[n - 1];
            }
            else foreach (var e in list) if (e != null && e.cfg != null && e.cfg.id.Contains(arg)) { ev = e; break; }
            if (ev == null) { status = "No event matches '" + arg + "'. Try event1 to event" + list.Count + "."; return; }
            npc.Events.TeleportTo(ev);
            SetOpen(false);
            return;
        }
        var cc = npc.Player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        npc.Player.position = pos;
        if (cc != null) cc.enabled = true;
        SetOpen(false);
    }

    void Skip()
    {
        var c = FindFirstObjectByType<CognitiveAssessmentController>();
        if (c == null || !c.IsRunning) { status = "No cognitive puzzle is running."; return; }
        c.ForcePass();
        SetOpen(false, false);   // the puzzle controller already restored the world controls / cursor
    }

    void GiveItem(Items.ItemDef def, NameState? variant)
    {
        var it = ItemFactory.Create(def, -1, variant);
        if (it == null) return;
        Inventory.Current.SwapIn(Inventory.SlotCount - 1, it);
        status = "Slot " + Inventory.SlotCount + " <- " + def.name + (variant.HasValue ? " [" + variant.Value + "]" : "");
    }

    void SpawnEvent(WorldEventConfig c)
    {
        var npc = FindFirstObjectByType<NpcDirector>();
        if (npc == null || npc.Events == null) { status = "No event director in this scene."; return; }
        var ev = npc.Events.SpawnEvent(c);
        if (ev != null || c.id == "flying_documents") SetOpen(false);
        else status = "No valid spot for " + c.id;
    }

    // ---------------------------------------------------------------- UI
    void OnGUI()
    {
        if (!open) return;
        if (bold == null) { bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold }; small = new GUIStyle(GUI.skin.label) { fontSize = 12 }; }
        var e = Event.current;
        float w = Screen.width;

        // command bar
        var bar = new Rect(0, Screen.height - 44, w, 44);
        GUI.Box(bar, GUIContent.none);
        GUI.Label(new Rect(10, bar.y + 12, 20, 24), ">", bold);
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "devcmd")
        { string c = input; input = ""; Run(c); e.Use(); }
        GUI.SetNextControlName("devcmd");
        string t = GUI.TextField(new Rect(30, bar.y + 10, w - 190, 26), input);
        input = t.Replace("`", "").Replace("~", "");
        if (focusNext && e.type == EventType.Repaint) { GUI.FocusControl("devcmd"); focusNext = false; }
        if (GUI.Button(new Rect(w - 150, bar.y + 10, 70, 26), "Run")) { string c = input; input = ""; Run(c); }
        if (GUI.Button(new Rect(w - 74, bar.y + 10, 64, 26), "Close X")) { SetOpen(false); return; }
        if (status != "") GUI.Label(new Rect(10, bar.y - 24, w - 20, 22), status, bold);

        if (panel == Panel.Events) EventsPanel();
        else if (panel == Panel.Items) ItemsPanel();
    }

    Rect PanelRect(string title, out Rect body)
    {
        var r = new Rect(20, 20, 400, Mathf.Min(Screen.height - 100, 660));
        GUI.Box(r, "");
        GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 80, 24), title, bold);
        if (GUI.Button(new Rect(r.xMax - 70, r.y + 6, 60, 24), "X")) panel = Panel.None;
        body = new Rect(r.x + 8, r.y + 36, r.width - 16, r.height - 44);
        return r;
    }

    void EventsPanel()
    {
        Rect body; PanelRect("EVENTS", out body);
        if (events == null)
        {
            var npc = FindFirstObjectByType<NpcDirector>();
            events = new List<WorldEventConfig>();
            foreach (var z in new[] { EventZone.Suburb, EventZone.Forest, EventZone.City })
                events.AddRange(WorldEventCatalog.ForZone(z, npc != null ? npc.settings.events.library : null));
            events.Sort((a, b) => { int c = WorldEventCatalog.FamilyOf(a).CompareTo(WorldEventCatalog.FamilyOf(b)); return c != 0 ? c : string.CompareOrdinal(a.id, b.id); });
        }
        GUILayout.BeginArea(body);
        var npcD = FindFirstObjectByType<NpcDirector>();
        if (npcD != null && npcD.Events != null && GUILayout.Button("Teleport to next event")) npcD.Events.TeleportToNextEvent();
        scroll = GUILayout.BeginScrollView(scroll);
        bool first = true; EventFamily last = EventFamily.Other;
        foreach (var c in events)
        {
            var f = WorldEventCatalog.FamilyOf(c);
            if (first || f != last) { GUILayout.Label(f.ToString().ToUpper(), bold); last = f; first = false; }
            if (GUILayout.Button(c.id + "  (" + c.zone + ")")) SpawnEvent(c);
        }
        GUILayout.EndScrollView(); GUILayout.EndArea();
    }

    void ItemsPanel()
    {
        Rect body; PanelRect("ITEMS  (click = put in slot " + Inventory.SlotCount + ")", out body);
        GUILayout.BeginArea(body);
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var d in Items.ItemDatabase.All)
        {
            var v = d.nameVariants;
            if (v == null || v.Length <= 1)
            { if (GUILayout.Button(d.id + ". " + d.name + "  (" + d.rarity + ")")) GiveItem(d, null); }
            else foreach (var ns in v)
                if (GUILayout.Button(d.id + ". " + d.name + "  [" + ns + "]  (" + d.rarity + ")")) GiveItem(d, ns);
        }
        GUILayout.EndScrollView(); GUILayout.EndArea();
    }
}
