using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Items;

/// <summary>One save file = one resume slot. Stored in persistentDataPath/saves/slotN.json (+ slotN_resume.png).</summary>
[Serializable]
public class InterviewHistoryEntry
{
    public string id = "";
    public string runId = "";      // dedupe key: one history record per run
    public string completedAt = "";
    public int sourceSlot = -1;
    public string candidateName = "";
    public string jobName = "";
    public string outcome = "";
    public string grade = "";
    public float score;
    public float suspicion;
    public int asked;
    public int correct;
    public string jobId = "";
    public string tier = "";
    public string mood = "";
    public int followUps;
    public int contradictions;
    public string proofResult = "";
    public string finalizedReport = "";
}

[Serializable]
class InterviewHistoryFile
{
    public List<InterviewHistoryEntry> entries = new List<InterviewHistoryEntry>();
}

[Serializable]
public class SaveData
{
    public int version = 2;
    public string runId = "";          // durable identity of this run (menu -> world -> interview -> verdict)
    public bool completed;             // terminal state: a finalized run is never resumable
    public string name = "", hobby = "", experience = "None";
    public int perk, education, realEducation, character = -1, jobPick = -1;
    public string[] offeredIds = new string[0], offeredTexts = new string[0];
    public int stage;                  // 0 = open world, 1 = interview room
    public string sceneName = "";
    public int worldSeed, roomSeed;     // roomSeed: interview room layout, fixed per run so resuming does not rebuild a different room
    public bool hasPos; public Vector3 pos; public float yaw;
    public string inventory = "";            // live inventory (InventorySave json)
    public string interviewInventory = "";   // snapshot taken when an interview run starts (restored on rejection / mid-interview quit)
    public bool interviewRunning;
    public int attempts;
    public float playTime;
    public bool bookletPending;              // brand-new save: the tutorial booklet opens once after the first spawn
    public float worldElapsed;               // seconds on the punctuality timer (HARD / EXPERT only; 0 on older saves)
    public string savedAt = "";
    public List<string> doneEvents = new List<string>();   // world events already finished: "id@x,z=state" (the world regenerates from its seed, so these stay finished)
}

public static class SaveSystem
{
    public const int Slots = 5;
    public const float AutoSaveInterval = 300f;    // seconds between "anything changed?" checks (every 5 minutes; quitting also saves)

    public static int Active = -1;
    public static SaveData Data;
    public static SaveData Pending;                // consumed once the target scene is ready (world bootstrap / interview arrival)
    public static string MenuScene = "";
    public static Font UiFont;
    public static event Action Saved;

    static string lastSig = "";

    static string Dir { get { string d = Path.Combine(Application.persistentDataPath, "saves"); Directory.CreateDirectory(d); return d; } }
    static string JsonPath(int i) { return Path.Combine(Dir, "slot" + i + ".json"); }
    static string PngPath(int i) { return Path.Combine(Dir, "slot" + i + "_resume.png"); }
    static string HistoryPath { get { return Path.Combine(Application.persistentDataPath, "interview_history.json"); } }
    static List<InterviewHistoryEntry> historyCache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = -1; Data = null; Pending = null; lastSig = ""; MenuScene = ""; historyCache = null; }

    // ------------------------------------------------------------ queries
    public static bool Exists(int i) { return ValidSlot(i) && File.Exists(JsonPath(i)); }

    /// <summary>Completed runs live outside save slots, so deleting a slot never deletes its history.</summary>
    public static List<InterviewHistoryEntry> GetHistory()
    {
        if (historyCache != null) return new List<InterviewHistoryEntry>(historyCache);
        try
        {
            if (File.Exists(HistoryPath))
            {
                InterviewHistoryFile file = JsonUtility.FromJson<InterviewHistoryFile>(File.ReadAllText(HistoryPath));
                historyCache = file != null && file.entries != null ? file.entries : new List<InterviewHistoryEntry>();
            }
            else historyCache = new List<InterviewHistoryEntry>();
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveSystem: history could not be read: " + e.Message);
            try { File.Copy(HistoryPath, HistoryPath + ".corrupt", true); } catch (Exception) { }   // keep the bad file instead of overwriting it silently
            historyCache = new List<InterviewHistoryEntry>();
        }
        return new List<InterviewHistoryEntry>(historyCache);
    }

    static bool HistoryHas(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return false;
        List<InterviewHistoryEntry> h = GetHistory();
        for (int i = 0; i < h.Count; i++) if (h[i] != null && h[i].runId == runId) return true;
        return false;
    }

    /// <summary>Persists one finished run. Idempotent per runId. Returns true only when the record is safely on disk.</summary>
    static bool RecordCompletedRun(string outcome, string grade, float score, string finalizedReport)
    {
        string runId = Data != null ? Data.runId : "";
        if (HistoryHas(runId)) return true;
        List<InterviewHistoryEntry> previous = GetHistory();
        List<InterviewHistoryEntry> entries = new List<InterviewHistoryEntry>(previous);
        string jobName = "";
        if (Data != null && Data.jobPick >= 0 && Data.offeredTexts != null && Data.jobPick < Data.offeredTexts.Length)
            jobName = Data.offeredTexts[Data.jobPick];

        InterviewHistoryEntry entry = new InterviewHistoryEntry();
        entry.runId = runId ?? "";
        entry.id = string.IsNullOrEmpty(runId) ? DateTime.UtcNow.Ticks.ToString() : runId;
        entry.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        entry.sourceSlot = Active;
        entry.candidateName = Data != null && !string.IsNullOrEmpty(Data.name) ? Data.name : ResumeSession.CandidateName;
        entry.jobName = jobName;
        entry.outcome = outcome ?? "";
        entry.grade = grade ?? "";
        entry.score = Mathf.Clamp(score, 0f, 100f);
        entry.suspicion = InterviewResult.FinalSuspicion;
        entry.asked = InterviewResult.Asked;
        entry.correct = InterviewResult.Correct;
        entry.jobId = InterviewResult.JobId ?? "";
        entry.tier = InterviewResult.Tier ?? "";
        entry.mood = InterviewResult.MoodName ?? "";
        entry.followUps = InterviewResult.Counters;
        entry.contradictions = InterviewResult.Contradictions;
        entry.proofResult = InterviewResult.ProofResult ?? "";
        entry.finalizedReport = finalizedReport ?? "";
        entries.Insert(0, entry);
        try
        {
            InterviewHistoryFile file = new InterviewHistoryFile();
            file.entries = entries;
            AtomicWrite(HistoryPath, JsonUtility.ToJson(file, true));
            historyCache = entries;
            return true;
        }
        catch (Exception e)
        {
            historyCache = previous;
            Debug.LogError("SaveSystem: history write failed (" + HistoryPath + "): " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// Ends the active run exactly once (safe to call repeatedly). Order matters: history is persisted first;
    /// only then is the run marked completed and its resumable slot deleted. False = history write failed, slot kept.
    /// </summary>
    public static bool FinalizeRun()
    {
        if (Data == null) return false;
        if (Data.completed) return true;
        if (string.IsNullOrEmpty(Data.runId)) Data.runId = NewRunId();
        string outcome = InterviewResult.Passed ? "HIRED" : "REJECTED";
        if (!RecordCompletedRun(outcome, InterviewResult.Rating, InterviewResult.Score, InterviewResult.Report)) return false;
        Data.completed = true;
        if (ValidSlot(Active)) DeleteFiles(Active);
        return true;
    }

    public static bool RunCompleted { get { return Data != null && Data.completed; } }

    static string NewRunId() { return Guid.NewGuid().ToString("N"); }
    static bool ValidSlot(int i) { return i >= 0 && i < Slots; }

    /// <summary>Write to slotN.tmp then replace, so a crash never leaves a half-written file.</summary>
    static void AtomicWrite(string path, string text)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        if (File.Exists(path))
        {
            try { File.Replace(tmp, path, null); }
            catch (Exception) { File.Delete(path); File.Move(tmp, path); }
        }
        else File.Move(tmp, path);
    }

    public static SaveData Peek(int i)
    {
        if (!Exists(i)) return null;
        try
        {
            SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(JsonPath(i)));
            if (d == null) { Debug.LogWarning("SaveSystem: slot " + i + " is empty or malformed."); return null; }
            if (d.completed || HistoryHas(d.runId))    // finished run whose slot was never cleaned up (crash between history write and delete)
            {
                Debug.Log("SaveSystem: slot " + i + " belongs to a completed run; removing the stale save.");
                DeleteFiles(i); return null;
            }
            if (d.offeredIds == null) d.offeredIds = new string[0];
            if (d.offeredTexts == null) d.offeredTexts = new string[0];
            if (d.doneEvents == null) d.doneEvents = new List<string>();
            if (d.inventory == null) d.inventory = "";
            if (d.interviewInventory == null) d.interviewInventory = "";
            return d;
        }
        catch (Exception e) { Debug.LogWarning("SaveSystem: slot " + i + " unreadable: " + e.Message); return null; }
    }

    public static bool InGame { get { return Active >= 0 && Data != null && SceneManager.GetActiveScene().name != MenuScene; } }

    // ------------------------------------------------------------ create / load / delete
    /// <summary>Called by the resume desk right after SUBMIT (ResumeSession already holds the form, ResumePixels the snapshot).</summary>
    public static void StartNew(int slot, int pickIdx)
    {
        Active = slot; Pending = null; lastSig = "";
        Data = new SaveData { stage = 0, sceneName = WorldLauncher.WorldSceneName, jobPick = pickIdx, runId = NewRunId(), bookletPending = true };
        var jobs = ResumeSession.OfferedJobs; var texts = ResumeSession.OfferedTexts;
        if (jobs != null)
        {
            Data.offeredIds = new string[jobs.Length]; Data.offeredTexts = new string[jobs.Length];
            for (int i = 0; i < jobs.Length; i++) { Data.offeredIds[i] = jobs[i].id; Data.offeredTexts[i] = texts != null && i < texts.Length ? texts[i] : ""; }
        }
        WriteResumePng(slot);
        Data.inventory = InventorySave.ToJson(Inventory.Current);
        Write(slot);
    }

    public static bool Continue(int slot)
    {
        var d = Peek(slot);
        if (d == null) return false;
        if (string.IsNullOrEmpty(d.runId)) d.runId = NewRunId();     // save from before run ids existed
        Active = slot; Data = d; Pending = d; lastSig = "";
        ResumeSession.EnsureLoaded();
        ResumeSession.CandidateName = d.name; ResumeSession.Hobby = d.hobby; ResumeSession.Experience = d.experience;
        ResumeSession.EducationScore = d.education; ResumeSession.RealEducation = d.realEducation;
        ResumeSession.SelectedPerk = (Perk)d.perk;
        ResumeSession.SetCharacter(d.character);
        var jobs = new JobData[d.offeredIds.Length]; var texts = new string[jobs.Length];
        for (int i = 0; i < jobs.Length; i++)
        {
            jobs[i] = Array.Find(ResumeSession.Database.jobs, j => j.id == d.offeredIds[i]);
            texts[i] = d.offeredTexts[i];
        }
        ResumeSession.OfferedJobs = jobs; ResumeSession.OfferedTexts = texts;
        if (d.jobPick >= 0 && d.jobPick < jobs.Length && jobs[d.jobPick] != null) ResumeSession.SelectOffer(d.jobPick);
        ReadResumePng(slot);
        RestoreInventory(d.inventory);
        Write(slot);                                                 // persists a freshly assigned runId

        if (d.stage == 0) WorldLauncher.Begin(MenuScene);
        else
        {
            string target = Application.CanStreamedLevelBeLoaded(d.sceneName) ? d.sceneName : FindInterviewScene();
            if (target == null) { Debug.LogError("SaveSystem: interview scene not in Build Settings."); Active = -1; Data = null; Pending = null; return false; }
            LoadingScreen.TransitionToScene(target);
        }
        return true;
    }

    public static void Delete(int slot)
    {
        DeleteFiles(slot);
        if (Active == slot) { Active = -1; Data = null; Pending = null; }
    }

    static void DeleteFiles(int slot)
    {
        if (!ValidSlot(slot)) return;
        try { if (File.Exists(JsonPath(slot))) File.Delete(JsonPath(slot)); if (File.Exists(PngPath(slot))) File.Delete(PngPath(slot)); } catch (Exception e) { Debug.LogError("SaveSystem: could not delete slot " + slot + ": " + e.Message); }
        string k = "rg_slot" + slot;      // also wipe the unfinished form draft so the page comes back blank
        foreach (var s in new[] { "n", "h", "g" }) PlayerPrefs.DeleteKey(k + s);
        foreach (var s in new[] { "c", "e", "x" }) PlayerPrefs.DeleteKey(k + s);
        PlayerPrefs.Save();
    }

    static string FindInterviewScene()
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string nm = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            if (nm.ToLowerInvariant().Contains("interview")) return nm;
        }
        return null;
    }

    // ------------------------------------------------------------ inventory
    public static void RestoreInventory(string json)
    {
        InterviewInventoryBridge.EnsureInitialized();
        ItemTextures.ClearCache();                       // starter resume texture is regenerated from the restored ResumePixels
        if (string.IsNullOrEmpty(json) || !InventorySave.FromJson(json)) Inventory.NewGame();
    }

    // ------------------------------------------------------------ finished world events
    /// <summary>null = this event has not been finished in the current save; otherwise the state string stored by NoteEventDone.</summary>
    public static string EventState(string key)
    {
        if (Data == null || Data.doneEvents == null) return null;
        string p = key + "=";
        for (int i = 0; i < Data.doneEvents.Count; i++) if (Data.doneEvents[i].StartsWith(p, StringComparison.Ordinal)) return Data.doneEvents[i].Substring(p.Length);
        return null;
    }

    public static void NoteEventDone(string key, string state)
    {
        if (Data == null) return;
        if (Data.doneEvents == null) Data.doneEvents = new List<string>();
        string p = key + "=", entry = p + state;
        for (int i = 0; i < Data.doneEvents.Count; i++) if (Data.doneEvents[i].StartsWith(p, StringComparison.Ordinal)) { Data.doneEvents[i] = entry; return; }
        Data.doneEvents.Add(entry);
    }

    // ------------------------------------------------------------ world / interview transitions
    public static void NoteWorldSeed(int seed) { if (Data != null) Data.worldSeed = seed; }

    /// <summary>Interview room seed: reuse the one stored for this run, otherwise store the freshly rolled one.</summary>
    public static int RoomSeed(int rolled)
    {
        if (Data == null || Data.completed || Data.stage != 1) return rolled;
        if (Data.roomSeed != 0) return Data.roomSeed;
        Data.roomSeed = rolled; return rolled;
    }

    public static void WorldReady()
    {
        bool intro = false;
        if (Data != null) { Pending = null; intro = Data.bookletPending; Data.bookletPending = false; SaveNow(true); }
        WorldTimer.StartWorld();
        if (intro) Booklet.ShowIntro();
    }

    /// <summary>World door -> interview room: from now on this slot resumes in the interview room.</summary>
    public static void EnterInterview(string scene)
    {
        if (Data == null) return;
        WorldTimer.Stop();
        Data.stage = 1; Data.sceneName = scene; Data.hasPos = false; Data.interviewRunning = false; Data.interviewInventory = "";
        SaveNow(false);
    }

    /// <summary>Interview actually starts: snapshot everything the interviewer may take away.</summary>
    public static void InterviewStarted()
    {
        if (Data == null) return;
        Vector3 p; float yaw;
        if (FindPlayerPose(out p, out yaw)) { Data.pos = p; Data.yaw = yaw; Data.hasPos = true; }
        Data.interviewInventory = InventorySave.ToJson(Inventory.Current);
        Data.interviewRunning = true;
        SaveNow(false);
    }

    /// <summary>Hired or Rejected: finalize (idempotent; history first, then slot removal) and go to the main menu. Never reloads the room.</summary>
    public static void CompleteGame()
    {
        if (Data != null && !Data.completed && !FinalizeRun())
            Debug.LogError("SaveSystem: run could not be finalized; the save was kept so progress is not lost.");
        QuitToMenu(false);
    }

    public static void QuitToMenu(bool save)
    {
        if (save) SaveNow(false);
        Time.timeScale = 1f; AudioListener.pause = false;
        Items.InventoryUI.Locked = false; Items.InventoryUI.NoDrop = false;
        WorldLauncher.Launching = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Active = -1; Data = null; Pending = null; lastSig = "";
        if (!string.IsNullOrEmpty(MenuScene)) SceneManager.LoadScene(MenuScene);
    }

    // ------------------------------------------------------------ saving
    public static bool FindPlayerPose(out Vector3 pos, out float yaw)
    {
        pos = Vector3.zero; yaw = 0f;
        Transform t = FindPlayer();
        if (t == null) return false;
        pos = t.position; yaw = t.eulerAngles.y; return true;
    }

    public static Transform FindPlayer()
    {
        if (WorldLauncher.Player != null) return WorldLauncher.Player;
        try { var g = GameObject.FindGameObjectWithTag("Player"); if (g != null) return g.transform; } catch (Exception) { }
        var cc = UnityEngine.Object.FindFirstObjectByType<CharacterController>();
        return cc != null ? cc.transform : null;
    }

    static void Capture()
    {
        var d = Data;
        d.name = ResumeSession.CandidateName; d.hobby = ResumeSession.Hobby; d.experience = ResumeSession.Experience;
        d.education = ResumeSession.EducationScore; d.realEducation = ResumeSession.RealEducation;
        d.perk = (int)ResumeSession.SelectedPerk; d.character = ResumeSession.CharacterIndex;
        if (WorldTimer.Active && (WorldTimer.Running || d.stage == 1)) d.worldElapsed = WorldTimer.Elapsed;
        d.sceneName = d.stage == 0 ? WorldLauncher.WorldSceneName : SceneManager.GetActiveScene().name;
        if (d.interviewRunning) return;      // mid-interview: keep the pre-interview inventory + position (the interview restarts from its start)
        d.inventory = InventorySave.ToJson(Inventory.Current);
        bool ready = d.stage == 1 || (!WorldLauncher.Launching && WorldLauncher.Player != null);
        Vector3 p; float yaw;
        if (ready && Pending == null && FindPlayerPose(out p, out yaw)) { d.pos = p; d.yaw = yaw; d.hasPos = true; }
    }

    static string Sig(SaveData d)
    {
        return d.inventory + "|" + d.interviewInventory + "|" + d.stage + d.interviewRunning + d.attempts + "|" + d.education + d.realEducation + d.perk +
               "|" + Mathf.RoundToInt(d.pos.x) + "," + Mathf.RoundToInt(d.pos.y) + "," + Mathf.RoundToInt(d.pos.z) + "|" + (int)(d.yaw / 15f) + d.hasPos + "|" + (d.doneEvents != null ? d.doneEvents.Count : 0);
    }

    /// <summary>Called every AutoSaveInterval: only writes when something actually changed.</summary>
    public static void AutoSave()
    {
        if (!InGame || Data.completed) return;
        Capture();
        if (Sig(Data) == lastSig) return;
        Write(Active); Toast();
    }

    public static void SaveNow(bool toast)
    {
        if (Active < 0 || Data == null || Data.completed) return;
        if (SceneManager.GetActiveScene().name != MenuScene) Capture();
        Write(Active);
        if (toast) Toast();
    }

    static void Toast() { var h = Saved; if (h != null) h(); }

    static void Write(int slot)
    {
        if (Data == null || Data.completed || !ValidSlot(slot)) return;   // a finalized run must never be written back
        try
        {
            Data.savedAt = DateTime.Now.ToString("MMM d, HH:mm");
            Data.version = 2;
            AtomicWrite(JsonPath(slot), JsonUtility.ToJson(Data));
            lastSig = Sig(Data);
        }
        catch (Exception e) { Debug.LogError("SaveSystem: write failed (slot " + slot + "): " + e.Message); }
    }

    // ------------------------------------------------------------ resume paper texture
    static void WriteResumePng(int slot)
    {
        if (ResumeSession.ResumePixels == null) return;
        var t = new Texture2D(ResumeSession.ResumeW, ResumeSession.ResumeH, TextureFormat.RGBA32, false);
        t.SetPixels32(ResumeSession.ResumePixels); t.Apply();
        try { File.WriteAllBytes(PngPath(slot), t.EncodeToPNG()); } catch (Exception e) { Debug.LogWarning(e.Message); }
        WorldUtil.Kill(t);
    }

    static void ReadResumePng(int slot)
    {
        ResumeSession.ResumePixels = null;
        if (!File.Exists(PngPath(slot))) return;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (t.LoadImage(File.ReadAllBytes(PngPath(slot)))) { ResumeSession.ResumePixels = t.GetPixels32(); ResumeSession.ResumeW = t.width; ResumeSession.ResumeH = t.height; }
        WorldUtil.Kill(t);
    }
}

/// <summary>Hidden host (DontDestroyOnLoad): autosave ticks, quit/pause saves, interview-room position restore.</summary>
public class SaveRunner : MonoBehaviour
{
    float timer;
    InterviewDoor door; float doorCheck; bool doorLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var g = new GameObject("SaveSystem Host"); DontDestroyOnLoad(g);
        g.AddComponent<SaveRunner>(); g.AddComponent<PauseMenu>();
    }

    void OnEnable() { SceneManager.sceneLoaded += OnLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }

    void OnLoaded(Scene s, LoadSceneMode m)
    {
        timer = 0f; door = null; doorCheck = 0f; doorLogged = false;
        var p = SaveSystem.Pending;
        if (p != null && p.stage == 1 && s.name != SaveSystem.MenuScene && s.name != WorldLauncher.WorldSceneName) StartCoroutine(RestoreInterviewPose(p));
    }

    IEnumerator RestoreInterviewPose(SaveData p)
    {
        yield return new WaitForSecondsRealtime(0.6f);   // let the room generator / player controller finish their own setup
        if (p.hasPos)
        {
            Transform t = SaveSystem.FindPlayer();
            if (t != null)
            {
                var cc = t.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                t.position = p.pos; t.rotation = Quaternion.Euler(0f, p.yaw, 0f);
                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();
            }
        }
        if (SaveSystem.Pending == p) SaveSystem.Pending = null;
    }

    // Safety net for the open-world interview door (it has been seen dead after quit-to-menu + continue in the same session):
    // once the world is ready, make sure the door exists, is active and enabled, and log its state once so a failure leaves a trace in the Console.
    void DoorWatch()
    {
        if (SaveSystem.Data == null || SaveSystem.Data.stage != 0 || WorldLauncher.Launching || WorldLauncher.Player == null) return;
        if (Time.unscaledTime < doorCheck) return;
        doorCheck = Time.unscaledTime + 1f;
        if (door == null) door = UnityEngine.Object.FindFirstObjectByType<InterviewDoor>(FindObjectsInactive.Include);
        if (door == null) { if (!doorLogged) { doorLogged = true; Debug.LogWarning("[DoorWatch] no InterviewDoor in this world"); } return; }
        bool healed = false;
        for (Transform t = door.transform; t != null; t = t.parent) if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); healed = true; }
        if (!door.enabled) { door.enabled = true; healed = true; }
        if (!doorLogged || healed)
        {
            doorLogged = true;
            Vector3 d = door.doorPoint - WorldLauncher.Player.position; d.y = 0f;
            Debug.Log("[DoorWatch] door at " + door.doorPoint + ", player " + WorldLauncher.Player.position + ", flat distance " + d.magnitude.ToString("F1") + " m, healed=" + healed + ", scene " + SceneManager.GetActiveScene().name);
        }
    }

    void Update()
    {
        DoorWatch();
        if (!SaveSystem.InGame) return;
        if (SaveSystem.Data != null && !PauseMenu.Paused) SaveSystem.Data.playTime += Time.unscaledDeltaTime;
        if (!PauseMenu.Paused) timer += Time.unscaledDeltaTime;
        if (timer >= SaveSystem.AutoSaveInterval) { timer = 0f; SaveSystem.AutoSave(); }
    }

    void OnApplicationPause(bool paused) { if (paused && SaveSystem.InGame) SaveSystem.SaveNow(false); }
    void OnApplicationQuit() { if (SaveSystem.InGame) SaveSystem.SaveNow(false); }
}
