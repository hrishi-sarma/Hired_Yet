using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TerminalController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_Text logText;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] TMP_InputField inputField;
    [SerializeField] TMP_Text counterText;
    [SerializeField] LogClickHandler logClickHandler;

    [Header("Resume Rules")]
    [SerializeField] int nameMaxWords = 4;
    [SerializeField] int nameMaxChars = 40;
    [SerializeField] int hobbyMaxWords = 4;
    [SerializeField] int hobbyMaxChars = 30;
    [SerializeField] int educationMin = 0;
    [SerializeField] int educationMax = 100;
    [SerializeField] int embellishMax = 30;

    [Header("Flow")]
    [SerializeField] string gameSceneName = "InterviewWorld";
    [SerializeField] float charsPerSecond = 90f;
    [SerializeField] float linePause = 0.12f;

    [Header("Colors")]
    [SerializeField] string colNormal = "#33FF66";
    [SerializeField] string colDim = "#1E9945";
    [SerializeField] string colBright = "#D8FFE2";
    [SerializeField] string colError = "#FF5555";
    [SerializeField] string colWarn = "#FFD34D";

    enum Mode { None, Text, Select, Review }
    enum ReviewAction { None, Print, Edit, Shred }
    enum EditTarget { Name, Experience, Hobby, Education, All }

    struct Slot
    {
        public int minWords;
        public int maxWords;
        public int maxChars;
        public bool numeric;
        public int minValue;
        public int maxValue;
        public bool optional;
    }

    readonly StringBuilder committed = new StringBuilder();
    string tail = "";
    Mode mode = Mode.None;
    Slot slot;
    string submitted;
    int chosen = -1;
    int highlight;
    string[] selectLines = new string[0];
    bool inputEnabled;

    ReviewAction reviewAction;
    EditTarget editTarget;
    bool awaitingField;

    void Start()
    {
        inputField.richText = false;
        inputField.onFocusSelectAll = false;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.onValidateInput = ValidateChar;
        inputField.onValueChanged.AddListener(UpdateCounter);
        inputField.onSubmit.AddListener(OnSubmit);
        logClickHandler.LinkClicked += OnLinkClicked;
        logText.richText = true;
        counterText.richText = true;
        counterText.text = "";
        StartCoroutine(Run());
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && mode == Mode.Select && inputEnabled)
        {
            int count = selectLines.Length;
            if (kb.downArrowKey.wasPressedThisFrame)
            {
                highlight = (highlight + 1) % count;
                RenderOptions();
            }
            else if (kb.upArrowKey.wasPressedThisFrame)
            {
                highlight = (highlight + count - 1) % count;
                RenderOptions();
            }
        }
        if (inputEnabled && !inputField.isFocused) inputField.ActivateInputField();
    }

    // Replacement for the old Input.anyKeyDown (keyboard, mouse buttons, touch).
    static bool AnyInputPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;

        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

        return false;
    }

    // =====================================================================
    // MAIN FLOW
    // =====================================================================
    IEnumerator Run()
    {
        SetInputEnabled(false);
        ResumeSession.EnsureLoaded();
        if (!ResumeSession.Ready)
        {
            Commit("<color=" + colError + ">FATAL: interview_data.json missing from a Resources folder.</color>");
            yield break;
        }
        ResumeSession.NewSession();

        yield return Say("JOB APPLICATION TERMINAL v1.0", colBright);
        yield return Say("Booting resume module...", colDim);
        yield return ProgressBar("Loading modules", 1.2f);
        yield return Say("Connection established.", colDim);
        yield return ProgressBar("Syncing interviewer database", 1f);
        yield return Say("Press any key to skip text animations.", colDim);
        Commit("");

        ResumeSession.ClearAnswers();
        yield return AskName();
        yield return AskExperience();
        yield return AskHobby();
        yield return AskEducation();

        while (true)
        {
            yield return Review();

            if (reviewAction == ReviewAction.Print) break;

            if (reviewAction == ReviewAction.Shred)
            {
                yield return ShredAndQuit();
                yield break;
            }

            yield return EditSection(editTarget);
        }

        yield return AskPerk();

        yield return Say("Printing resume...", colNormal);
        yield return Say("Done. Good luck out there.", colBright);
        yield return new WaitForSecondsRealtime(0.6f);
        LoadGameScene();
    }

    IEnumerator AskName()
    {
        yield return Say("SECTION 1/4 - NAME", colBright);
        yield return Say("Enter the name that will appear on your resume.", colNormal);
        slot = new Slot { minWords = 1, maxWords = nameMaxWords, maxChars = nameMaxChars };
        yield return AskText();
        ResumeSession.CandidateName = submitted;
        yield return Comment(NameRemark(submitted));
        Commit("");
    }

    IEnumerator AskExperience()
    {
        yield return Say("SECTION 2/4 - EXPERIENCE / SKILLS", colBright);
        yield return Say("Choose ONE claim. Harder jobs pay more, but the interviewer tests them harder.", colNormal);
        yield return Say("Type 1-3 + ENTER, use UP/DOWN + ENTER, or click an option.", colNormal);
        selectLines = new string[ResumeSession.OfferedTexts.Length];
        for (int i = 0; i < selectLines.Length; i++)
        {
            JobData j = ResumeSession.OfferedJobs[i];
            selectLines[i] = ResumeSession.OfferedTexts[i] + "  <color=" + colDim + ">[RISK " + RiskInfo.Risk(j) + " | PAY " + RiskInfo.Pay(j) + "]</color>";
        }
        yield return RunSelect();
        ResumeSession.SelectOffer(chosen);
        yield return Comment(ExperienceRemark());
        Commit("");
    }

    IEnumerator AskPerk()
    {
        yield return Say("PREP STEP - choose ONE perk to take with you.", colBright);
        Perk[] perks = PerkInfo.Draw(3);
        selectLines = new string[perks.Length];
        for (int i = 0; i < perks.Length; i++) selectLines[i] = PerkInfo.Name(perks[i]) + " - " + PerkInfo.Description(perks[i]);
        yield return RunSelect();
        ResumeSession.SelectedPerk = perks[chosen];
        yield return Comment(PerkInfo.Name(perks[chosen]) + " equipped. Do not waste it.");
        Commit("");
    }

    IEnumerator RunSelect()
    {
        slot = new Slot { minWords = 1, maxWords = 1, maxChars = 1 };
        chosen = -1;
        highlight = 0;
        mode = Mode.Select;
        RenderOptions();
        SetInputEnabled(true);
        while (chosen < 0) yield return null;
        SetInputEnabled(false);
        mode = Mode.None;
        tail = "";
        Commit(BuildOptions(chosen, true));
        Commit("<color=" + colBright + ">> selected [" + (chosen + 1) + "]</color>");
        yield return new WaitForSecondsRealtime(linePause);
    }

    IEnumerator AskHobby()
    {
        yield return Say("SECTION 3/4 - HOBBY", colBright);
        yield return Say("Enter one hobby.", colNormal);
        slot = new Slot { minWords = 1, maxWords = hobbyMaxWords, maxChars = hobbyMaxChars };
        yield return AskText();
        ResumeSession.Hobby = submitted;
        yield return Comment(HobbyRemark(submitted));
        Commit("");
    }

    IEnumerator AskEducation()
    {
        yield return Say("SECTION 4/4 - EDUCATION", colBright);
        yield return Say("Enter your real final education score as a percentage (" + educationMin + "-" + educationMax + "%).", colNormal);
        slot = new Slot
        {
            minWords = 1,
            maxWords = 1,
            maxChars = educationMax.ToString().Length,
            numeric = true,
            minValue = educationMin,
            maxValue = educationMax
        };
        yield return AskText();
        int real = int.Parse(submitted);
        ResumeSession.RealEducation = real;
        ResumeSession.EducationScore = real;

        int cap = Mathf.Min(educationMax, real + embellishMax);
        if (cap > real)
        {
            yield return Say("EMBELLISH? Type a higher score to put on your resume, or press ENTER to keep " + real + "%.", colNormal);
            yield return Say("Every extra point makes the interviewer's grade checks harsher.", colDim);
            slot = new Slot
            {
                minWords = 0,
                maxWords = 1,
                maxChars = educationMax.ToString().Length,
                numeric = true,
                optional = true,
                minValue = real + 1,
                maxValue = cap
            };
            yield return AskText();
            if (submitted.Length > 0) ResumeSession.EducationScore = int.Parse(submitted);
        }

        yield return Comment(ScoreRemark());
        int emb = ResumeSession.Embellishment;
        if (emb > 0) yield return Comment("Transcript padded by +" + emb + ". Brave. Grade checks will be harsher.");
        else yield return Comment("No padding. Refreshing.");
        Commit("");
    }

    // =====================================================================
    // TERMINAL PERSONALITY
    // =====================================================================
    IEnumerator Comment(string line)
    {
        yield return Say("TERMINAL> " + line, colDim);
    }

    IEnumerator ProgressBar(string label, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (AnyInputPressed()) break;
            t += Time.unscaledDeltaTime;
            tail = BarLine(label, t / seconds);
            Refresh();
            yield return null;
        }
        tail = "";
        Commit(BarLine(label, 1f));
        yield return new WaitForSecondsRealtime(linePause);
    }

    string BarLine(string label, float p)
    {
        int width = 20;
        int filled = Mathf.Clamp(Mathf.RoundToInt(p * width), 0, width);
        int pct = Mathf.RoundToInt(Mathf.Clamp01(p) * 100f);
        return "<color=" + colDim + ">" + label + " [</color><color=" + colNormal + ">" + new string('#', filled) + "</color><color=" + colDim + ">" + new string('-', width - filled) + "] " + pct + "%</color>";
    }

    static string PickOne(string[] options)
    {
        return options[UnityEngine.Random.Range(0, options.Length)];
    }

    static string NameRemark(string name)
    {
        string[] w = name.Split(' ');
        if (w.Length == 1)
            return PickOne(new string[] { "A single name. Mysterious.", "Just " + w[0] + "? Bold branding." });
        return PickOne(new string[]
        {
            "Name logged. Welcome, " + w[0] + ".",
            w[0] + ". Noted. The interviewer will remember it.",
            "Name saved. Try not to forget it."
        });
    }

    static string ExperienceRemark()
    {
        JobData j = ResumeSession.SelectedJob;
        if (j == null) return "Claim registered.";
        string line;
        if (j.strength >= 3) line = PickOne(new string[] { "High risk, high pay. The interviewer will dig deep.", "Ambitious. Expect hard questions." });
        else if (j.strength == 2) line = PickOne(new string[] { "Respectable claim. A fair test awaits.", "Medium risk. Medium pay. Medium sweat." });
        else line = PickOne(new string[] { "Low risk, low pay. Safe, but dull.", "Modest claim. Easy to defend." });
        if (j.tone == "satirical") line += " That one sounds made up.";
        return line;
    }

    static string HobbyRemark(string hobby)
    {
        string h = hobby.ToLowerInvariant();
        if (h.Contains("sleep")) return "Hobby: sleeping. Bold.";
        if (h.Contains("gam")) return "Gaming. Employers pretend to be impressed.";
        if (h.Contains("read")) return "Reading. You may get quizzed on that.";
        if (h.Contains("chess")) return "Chess. Thinking three moves ahead? We will see.";
        return PickOne(new string[]
        {
            "Hobby noted. Do not forget what you wrote.",
            "Interesting. The interviewer may ask about it.",
            "Hobby saved. Let's hope you actually do that."
        });
    }

    static string ScoreRemark()
    {
        int s = ResumeSession.EducationScore;
        if (s < 30) return "Education: " + s + "%. Noted.";
        if (s < 60) return "Education: " + s + "%. Room for growth.";
        if (s < 85) return "Education: " + s + "%. Respectable.";
        if (s < 95) return "Education: " + s + "%. Impressive. Suspiciously so?";
        return "Education: " + s + "%. Perfection. Nobody believes that.";
    }

    // =====================================================================
    // REVIEW / PRINT / EDIT / SHRED  (all typed, no buttons)
    // =====================================================================
    IEnumerator Review()
    {
        Commit("<color=" + colBright + ">==================== RESUME ====================</color>");
        Commit("<color=" + colNormal + ">NAME       : " + ResumeSession.CandidateName + "</color>");
        Commit("<color=" + colNormal + ">EXPERIENCE : " + ResumeSession.SelectedText + "</color>");
        Commit("<color=" + colNormal + ">HOBBY      : " + ResumeSession.Hobby + "</color>");
        Commit("<color=" + colNormal + ">EDUCATION  : " + ResumeSession.EducationScore + "%</color>");
        Commit("<color=" + colBright + ">================================================</color>");
        yield return Say("PRINT  - pick a prep perk, print your resume and head out", colNormal);
        yield return Say("EDIT   - change a field (NAME, EXPERIENCE, HOBBY, EDUCATION or ALL)", colNormal);
        yield return Say("SHRED  - shred your resume and quit", colNormal);

        reviewAction = ReviewAction.None;
        awaitingField = false;
        slot = new Slot { minWords = 1, maxWords = 2, maxChars = 24 };
        mode = Mode.Review;
        SetInputEnabled(true);
        while (reviewAction == ReviewAction.None) yield return null;
        SetInputEnabled(false);
        mode = Mode.None;
        Commit("");
    }

    IEnumerator EditSection(EditTarget target)
    {
        yield return Say("Reopening " + TargetLabel(target) + "...", colWarn);
        Commit("");

        switch (target)
        {
            case EditTarget.Name:
                yield return AskName();
                break;
            case EditTarget.Experience:
                yield return AskExperience();
                break;
            case EditTarget.Hobby:
                yield return AskHobby();
                break;
            case EditTarget.Education:
                yield return AskEducation();
                break;
            case EditTarget.All:
                ResumeSession.ClearAnswers();
                yield return AskName();
                yield return AskExperience();
                yield return AskHobby();
                yield return AskEducation();
                break;
        }
    }

    IEnumerator ShredAndQuit()
    {
        yield return Say("Feeding resume into the shredder...", colWarn);
        ResumeSession.ClearAnswers();
        yield return Say("Resume destroyed. Goodbye.", colError);
        yield return new WaitForSecondsRealtime(0.8f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SubmitReview(string clean)
    {
        string lower = clean.ToLowerInvariant();
        string[] parts = lower.Split(' ');
        string cmd = parts[0];
        string arg = parts.Length > 1 ? parts[1] : null;

        // Second step of "EDIT": waiting for the field name.
        if (awaitingField)
        {
            EditTarget t;
            if (cmd == "cancel" || cmd == "back")
            {
                Commit("<color=" + colBright + ">> " + clean + "</color>");
                Commit("<color=" + colWarn + ">Edit cancelled. Type PRINT, EDIT or SHRED.</color>");
                awaitingField = false;
            }
            else if (TryParseTarget(cmd, out t))
            {
                Commit("<color=" + colBright + ">> " + clean + "</color>");
                editTarget = t;
                awaitingField = false;
                reviewAction = ReviewAction.Edit;
            }
            else
            {
                Error("Type NAME, EXPERIENCE, HOBBY, EDUCATION or ALL (or CANCEL).");
            }
            return;
        }

        if (cmd == "print")
        {
            Commit("<color=" + colBright + ">> " + clean + "</color>");
            reviewAction = ReviewAction.Print;
        }
        else if (cmd == "shred")
        {
            Commit("<color=" + colBright + ">> " + clean + "</color>");
            reviewAction = ReviewAction.Shred;
        }
        else if (cmd == "edit")
        {
            Commit("<color=" + colBright + ">> " + clean + "</color>");
            if (arg == null)
            {
                Commit("<color=" + colWarn + ">Which field? NAME, EXPERIENCE, HOBBY, EDUCATION or ALL (CANCEL to go back).</color>");
                awaitingField = true;
                return;
            }
            EditTarget t;
            if (TryParseTarget(arg, out t))
            {
                editTarget = t;
                reviewAction = ReviewAction.Edit;
            }
            else
            {
                Error("Unknown field. Use NAME, EXPERIENCE, HOBBY, EDUCATION or ALL.");
            }
        }
        else
        {
            Error("Unknown command. Type PRINT, EDIT or SHRED.");
        }
    }

    static bool TryParseTarget(string word, out EditTarget target)
    {
        switch (word)
        {
            case "name": target = EditTarget.Name; return true;
            case "experience":
            case "exp":
            case "skills":
            case "claim": target = EditTarget.Experience; return true;
            case "hobby": target = EditTarget.Hobby; return true;
            case "education":
            case "edu":
            case "score": target = EditTarget.Education; return true;
            case "all":
            case "reset": target = EditTarget.All; return true;
        }
        target = EditTarget.All;
        return false;
    }

    static string TargetLabel(EditTarget t)
    {
        switch (t)
        {
            case EditTarget.Name: return "NAME";
            case EditTarget.Experience: return "EXPERIENCE";
            case EditTarget.Hobby: return "HOBBY";
            case EditTarget.Education: return "EDUCATION";
        }
        return "the whole resume";
    }

    // =====================================================================
    // INPUT PLUMBING
    // =====================================================================
    IEnumerator AskText()
    {
        submitted = null;
        mode = Mode.Text;
        SetInputEnabled(true);
        while (submitted == null) yield return null;
        SetInputEnabled(false);
        mode = Mode.None;
        yield return new WaitForSecondsRealtime(linePause);
    }

    IEnumerator Say(string line, string color)
    {
        float t = 0f;
        int shown = 0;
        while (shown < line.Length)
        {
            if (AnyInputPressed())
            {
                shown = line.Length;
            }
            else
            {
                t += Time.unscaledDeltaTime * charsPerSecond;
                shown = Mathf.Min(line.Length, Mathf.FloorToInt(t));
            }
            tail = "<color=" + color + ">" + line.Substring(0, shown) + "</color>";
            Refresh();
            if (shown < line.Length) yield return null;
        }
        tail = "";
        Commit("<color=" + color + ">" + line + "</color>");
        yield return new WaitForSecondsRealtime(linePause);
    }

    void OnSubmit(string raw)
    {
        if (!inputEnabled) return;
        string clean = Collapse(raw);
        switch (mode)
        {
            case Mode.Text: SubmitText(clean); break;
            case Mode.Select: SubmitSelect(clean); break;
            case Mode.Review: SubmitReview(clean); break;
        }
        inputField.text = "";
        if (inputEnabled) inputField.ActivateInputField();
    }

    void SubmitText(string clean)
    {
        if (slot.optional && clean.Length == 0)
        {
            Commit("<color=" + colDim + ">> (kept)</color>");
            submitted = "";
            return;
        }
        int words = CountWords(clean);
        if (words < slot.minWords) { Error("This field cannot be empty."); return; }
        if (words > slot.maxWords) { Error("Maximum " + slot.maxWords + " word(s) allowed."); return; }
        if (clean.Length > slot.maxChars) { Error("Maximum " + slot.maxChars + " characters allowed."); return; }

        if (slot.numeric)
        {
            int value;
            if (!int.TryParse(clean, out value) || value < slot.minValue || value > slot.maxValue)
            {
                Error("Enter a number from " + slot.minValue + " to " + slot.maxValue + ".");
                return;
            }
            clean = value.ToString();
            Commit("<color=" + colBright + ">> " + clean + "%</color>");
            submitted = clean;
            return;
        }
        else if (!HasLetter(clean))
        {
            Error("Use at least one letter.");
            return;
        }

        Commit("<color=" + colBright + ">> " + clean + "</color>");
        submitted = clean;
    }

    void SubmitSelect(string clean)
    {
        if (clean.Length == 0)
        {
            chosen = highlight;
            return;
        }
        int n;
        if (int.TryParse(clean, out n) && n >= 1 && n <= selectLines.Length)
        {
            chosen = n - 1;
            return;
        }
        Error("Type a number from 1 to " + selectLines.Length + ".");
    }

    void OnLinkClicked(int index)
    {
        if (mode != Mode.Select || !inputEnabled) return;
        if (index < 0 || index >= selectLines.Length) return;
        chosen = index;
    }

    char ValidateChar(string text, int charIndex, char c)
    {
        if (mode == Mode.Select || slot.numeric) return char.IsDigit(c) ? c : '\0';
        if (c == ' ')
        {
            if (charIndex == 0) return '\0';
            if (text[charIndex - 1] == ' ') return '\0';
            if (charIndex < text.Length && text[charIndex] == ' ') return '\0';
            return CountWords(text) >= slot.maxWords ? '\0' : c;
        }
        return char.IsLetterOrDigit(c) || c == '-' || c == '\'' || c == '.' ? c : '\0';
    }

    void UpdateCounter(string text)
    {
        if (!inputEnabled)
        {
            counterText.text = "";
            return;
        }
        if (mode == Mode.Select)
        {
            counterText.text = "<color=" + colDim + ">type 1-3, UP/DOWN + ENTER, or click</color>";
            return;
        }
        if (mode == Mode.Review)
        {
            string hint = awaitingField ? "NAME / EXP / HOBBY / EDU / ALL / CANCEL" : "type PRINT, EDIT or SHRED";
            counterText.text = "<color=" + colDim + ">" + hint + "</color>";
            return;
        }
        int words = CountWords(text);
        bool over = words > slot.maxWords || text.Length > slot.maxChars;
        string color = over ? colError : colDim;
        string range = slot.numeric ? "  range " + slot.minValue + "-" + slot.maxValue + "%" + (slot.optional ? "  ENTER = keep" : "") : "";
        counterText.text = "<color=" + color + ">words " + words + "/" + slot.maxWords + "  chars " + text.Length + "/" + slot.maxChars + range + "</color>";
    }

    void SetInputEnabled(bool on)
    {
        inputEnabled = on;
        inputField.interactable = on;
        inputField.characterLimit = on ? slot.maxChars : 0;
        inputField.text = "";
        UpdateCounter("");
        if (on) inputField.ActivateInputField();
    }

    // =====================================================================
    // RENDERING
    // =====================================================================
    void RenderOptions()
    {
        tail = BuildOptions(highlight, false);
        Refresh();
    }

    string BuildOptions(int selected, bool final)
    {
        StringBuilder sb = new StringBuilder();
        int count = selectLines.Length;
        for (int i = 0; i < count; i++)
        {
            bool on = i == selected;
            string color = on ? colBright : (final ? colDim : colNormal);
            string marker = on ? ">" : " ";
            string line = "<color=" + color + ">" + marker + " [" + (i + 1) + "] " + selectLines[i] + "</color>";
            if (final) sb.Append(line);
            else sb.Append("<link=\"" + i + "\">" + line + "</link>");
            if (i < count - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    void Commit(string richLine)
    {
        committed.Append(richLine).Append('\n');
        Refresh();
    }

    void Refresh()
    {
        logText.text = committed.ToString() + tail;
        LayoutRebuilder.ForceRebuildLayoutImmediate(logText.rectTransform);
        if (logText.preferredHeight > scrollRect.viewport.rect.height) scrollRect.verticalNormalizedPosition = 0f;
    }

    void Error(string message)
    {
        Commit("<color=" + colError + ">! " + message + "</color>");
    }

    void LoadGameScene()
    {
        if (Application.CanStreamedLevelBeLoaded(WorldLauncher.WorldSceneName))
        {
            WorldLauncher.Begin(gameSceneName);
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Error("Scene '" + gameSceneName + "' is not in Build Settings.");
            return;
        }
        SceneManager.LoadScene(gameSceneName);
    }

    static string Collapse(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        raw = raw.Replace("<", "").Replace(">", "");
        return string.Join(" ", raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    static int CountWords(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    static bool HasLetter(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsLetter(s[i])) return true;
        }
        return false;
    }
}