using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class InterviewUI
{
    const float VoiceTimeBonus = 5f;
    const float CircleSize = 56f;
    const float CircleGap = 8f;
    const float CirclesW = CircleSize * 3f + CircleGap * 2f;
    const float GroupGap = 18f;
    const float BtnGap = 12f;
    const string HexAlert = "#C62828";

    VoiceInput voice;
    System.Action<string> voiceFailedHandler;
    bool voiceReady;
    RectTransform controlsRt, circlesRt;
    VoiceCircle cPrimary, cStop, cConfirm;
    string voiceLine = "";
    string voiceHint = "";
    bool voiceHintAlert;
    bool voiceBonusGiven;
    bool meterShown;

    float ExtraVoiceH() { return 0f; }

    void BuildVoiceRow(RectTransform controls)
    {
        controlsRt = controls;
        if (!voiceReady) return;
        voice = VoiceInput.Create();
        VoiceDetach();                                   // never stack handlers on the persistent VoiceInput singleton
        voiceFailedHandler = delegate (string msg) { if (this != null) SetVoiceHint(msg, true); };
        voice.Changed += OnVoiceChanged;
        voice.Failed += voiceFailedHandler;

        circlesRt = UIKit.NewRect("VoiceCircles", controls);
        circlesRt.anchorMin = new Vector2(0f, 0.5f);
        circlesRt.anchorMax = new Vector2(0f, 0.5f);
        circlesRt.pivot = new Vector2(0f, 0.5f);
        circlesRt.sizeDelta = new Vector2(S(CirclesW), S(CircleSize));

        cPrimary = MakeCircle("VPrimary", 0, VoiceIcons.Kind.Mic, -2.5f, delegate { VoicePrimary(); });
        cStop = MakeCircle("VStop", 1, VoiceIcons.Kind.Stop, 1.5f, delegate { if (voice.State == VoiceState.Listening || voice.State == VoiceState.Paused) voice.Stop(); });
        cConfirm = MakeCircle("VConfirm", 2, VoiceIcons.Kind.Check, -1.2f, delegate { VoiceConfirm(); });

        RectTransform sr = shortRow.GetComponent<RectTransform>();
        sr.offsetMax = new Vector2(-S(CirclesW + GroupGap), 0f);
        circlesRt.gameObject.SetActive(false);
    }

    VoiceCircle MakeCircle(string name, int slot, VoiceIcons.Kind kind, float tilt, System.Action onClick)
    {
        RectTransform rt = UIKit.NewRect(name, circlesRt);
        float full = S(CircleSize * 1.34f);
        UIKit.Place(rt, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(S(CircleSize * 0.5f + slot * (CircleSize + CircleGap)), 0f), new Vector2(full, full));
        Image im = UIKit.AddImage(rt, VoiceIcons.Get(kind), Color.white, true);
        VoiceCircle vc = rt.gameObject.AddComponent<VoiceCircle>();
        vc.Init(im, tilt);
        vc.OnClick = onClick;
        return vc;
    }

    void FitLabel(GlitchButton b)
    {
        TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t == null || t.enableAutoSizing) return;
        t.enableAutoSizing = true;
        t.fontSizeMax = S(27f);
        t.fontSizeMin = S(15f);
        t.enableWordWrapping = false;
    }

    void LayoutAnswerRow()
    {
        if (controlsRt == null || !controlsRt.gameObject.activeInHierarchy || yesNoRow == null) return;
        float w = controlsRt.rect.width;
        if (w <= 1f) return;
        bool hasVoice = voiceReady && circlesRt != null && circlesRt.gameObject.activeSelf;
        float cw = hasVoice ? S(CirclesW) : 0f;
        float gg = hasVoice ? S(GroupGap) : 0f;
        if (yesNoRow.activeSelf)
        {
            GlitchButton[] bs = { yesBtn, noBtn, maybeBtn };
            int n = 0;
            for (int i = 0; i < bs.Length; i++) if (bs[i].gameObject.activeSelf) n++;
            if (n == 0)
            {
                if (hasVoice) circlesRt.anchoredPosition = new Vector2(Mathf.Max(0f, (w - cw) * 0.5f), 0f);
                return;
            }
            float gap = S(BtnGap);
            float bw = Mathf.Min(S(230f), (w - cw - gg - gap * (n - 1)) / n);
            float groupW = n * bw + (n - 1) * gap;
            float x0 = Mathf.Max(0f, (w - groupW - gg - cw) * 0.5f);
            int k = 0;
            for (int i = 0; i < bs.Length; i++)
            {
                if (!bs[i].gameObject.activeSelf) continue;
                RectTransform rt = bs[i].GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(bw, S(ControlsHeight));
                rt.anchoredPosition = new Vector2(x0 + k * (bw + gap), 0f);
                FitLabel(bs[i]);
                k++;
            }
            if (hasVoice) circlesRt.anchoredPosition = new Vector2(x0 + groupW + gg, 0f);
        }
        else if (hasVoice) circlesRt.anchoredPosition = new Vector2(w - cw, 0f);
    }

    void VoiceOnQuestion()
    {
        if (!voiceReady || circlesRt == null) return;
        voice.ResetAll();
        voiceBonusGiven = false;
        voiceLine = "";
        voiceHint = "";
        meterShown = false;
        circlesRt.gameObject.SetActive(true);
        voice.Arm();
        RefreshVoiceCircles();
    }

    void VoiceUnload()
    {
        if (voice == null) return;
        voice.ResetAll();
        voice.Unload();
    }

    void VoiceOnHide()
    {
        if (!voiceReady || circlesRt == null) return;
        voice.ResetAll();
        voiceLine = "";
        voiceHint = "";
        meterShown = false;
        circlesRt.gameObject.SetActive(false);
    }

    void VoiceTick()
    {
        LayoutAnswerRow();
        if (!voiceReady || voice == null || circlesRt == null || !circlesRt.gameObject.activeSelf) return;
        bool live = voice.State == VoiceState.Listening;
        cPrimary.Pulsing = live;
        if (live && voice.MeterActive)
        {
            voiceHint = "MIC " + new string('|', Mathf.Clamp(Mathf.RoundToInt(voice.MicLevel * 30f), 0, 12)) + (voice.MicStatus.Length > 0 ? "  (" + voice.MicStatus + ")" : "");
            voiceHintAlert = voice.MicStatus.Length > 0;
            meterShown = true;
        }
        else if (meterShown)
        {
            meterShown = false;
            voiceHint = "";
            voiceHintAlert = false;
            if (state == State.Asking) RefreshVoiceLine();
        }
        if (live && state == State.Asking) RefreshVoiceLine();
    }

    void VoicePrimary()
    {
        if (state != State.Asking || current == null) return;
        switch (voice.State)
        {
            case VoiceState.Idle:
            case VoiceState.Paused:
                VoiceMicPressed();
                break;
            case VoiceState.Listening:
                voice.Pause();
                break;
            case VoiceState.Review:
                SetVoiceHint("", false);
                voice.Begin();
                RefreshVoiceCircles();
                break;
        }
    }

    void VoiceMicPressed()
    {
        if (state != State.Asking || current == null) return;
        SetVoiceHint("", false);
        if (voice.State == VoiceState.Paused) voice.Resume(); else voice.Begin();
        if (voice.State == VoiceState.Listening && !voiceBonusGiven) { voiceBonusGiven = true; timeLeft += VoiceTimeBonus; }
        RefreshVoiceCircles();
    }

    /// <summary>VoiceInput is DontDestroyOnLoad: handlers of a destroyed InterviewUI would keep firing into its dead RectTransforms.</summary>
    void VoiceDetach()
    {
        if (voice == null) return;
        voice.Changed -= OnVoiceChanged;
        if (voiceFailedHandler != null) voice.Failed -= voiceFailedHandler;
        voiceFailedHandler = null;
    }

    void VoiceRelease()
    {
        if (voice == null) return;
        VoiceDetach();
        voice.ResetAll();
    }

    void OnVoiceChanged()
    {
        if (this == null || logText == null) return;
        if (state != State.Asking || current == null) return;
        if (current.type != "yesno" && voice.State != VoiceState.Idle && input != null)
            input.text = voice.Transcript;
        if (voice.State == VoiceState.Review && current.type == "yesno") VoiceHeardHint();
        RefreshVoiceLine();
        RefreshVoiceCircles();
    }

    string ItemName(int slot)
    {
        Items.ItemInstance it = InterviewInventoryBridge.Get(slot);
        return it != null ? it.def.name : null;
    }

    void VoiceHeardHint()
    {
        if (requestMode)
        {
            string r = VoiceIntent.ToRequest(voice.Transcript, ItemName(1), ItemName(2));
            if (r == null) SetVoiceHint("Didn't catch that. Say one, two, three or nothing.", true);
            else SetVoiceHint("Heard: " + RequestRules.ShownName(r), false);
            return;
        }
        string c = VoiceIntent.ToYesNo(voice.Transcript, false);
        if (c == null) SetVoiceHint("Didn't catch a yes or no. Re-record or press a button.", true);
        else SetVoiceHint("Heard: " + c.ToUpperInvariant(), false);
    }

    void RefreshVoiceLine()
    {
        if (voice == null) return;
        string caret = (voice.State == VoiceState.Listening && ((int)(Time.unscaledTime * 2f) % 2 == 0)) ? "_" : "";
        string t = "";
        if (voice.State == VoiceState.Listening && !voice.HasText)
            t = "\n<color=" + HexDim + ">YOU> listening...</color>" + caret;
        else if (voice.HasText || voice.State != VoiceState.Idle)
            t = "\n<color=" + HexBright + ">YOU> </color>" + Clean(voice.Committed) + "<color=" + HexDim + "> " + Clean(voice.Interim) + "</color>" + caret;
        string h = voiceHint.Length > 0 ? "\n<color=" + (voiceHintAlert ? HexAlert : HexBright) + "><b>" + Clean(voiceHint) + "</b></color>" : "";
        voiceLine = t + h;
        Refresh();
    }

    void RefreshVoiceCircles()
    {
        if (voice == null || cPrimary == null) return;
        VoiceState s = voice.State;
        cPrimary.SetSprite(VoiceIcons.Get(s == VoiceState.Listening ? VoiceIcons.Kind.Pause : (s == VoiceState.Review ? VoiceIcons.Kind.Redo : VoiceIcons.Kind.Mic)));
        cPrimary.SetInteractable(s != VoiceState.Finishing);
        cStop.SetInteractable(s == VoiceState.Listening || s == VoiceState.Paused);
        cConfirm.SetInteractable(s == VoiceState.Review);
    }

    void SetVoiceHint(string msg, bool alert)
    {
        voiceHint = msg ?? "";
        voiceHintAlert = alert;
        meterShown = false;
        RefreshVoiceLine();
    }

    void VoiceConfirm()
    {
        if (state != State.Asking || current == null || voice.State != VoiceState.Review) return;
        if (current.type == "yesno")
        {
            if (requestMode)
            {
                string r = VoiceIntent.ToRequest(voice.Transcript, ItemName(1), ItemName(2));
                if (r == null) { SetVoiceHint("Didn't catch that. Say one, two, three or nothing.", true); return; }
                if (r != "none" && !RequestSlotUsable(r[0] - '1')) { SetVoiceHint("You don't have that item.", true); return; }
                voice.ResetAll();
                AnswerYesNo(r);
                return;
            }
            string c = VoiceIntent.ToYesNo(voice.Transcript, false);
            if (c == null) { SetVoiceHint("Didn't catch a yes or no. Re-record or press a button.", true); return; }
            if (c == "maybe" && !maybeAllowed) { SetVoiceHint("\"Maybe\" isn't an option here.", true); return; }
            voice.ResetAll();
            AnswerYesNo(c);
        }
        else
        {
            string typed = input != null ? input.text : voice.Transcript;
            if (AnswerEvaluator.Normalize(typed).Length == 0) { SetVoiceHint("Nothing to submit.", true); return; }
            voice.ResetAll();
            Submit(typed, false);
        }
    }
}
