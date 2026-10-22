using System.Collections;
using UnityEngine;

// Stage 2: pre-interview small talk (up to 3 turns). Reuses the short-answer row and the voice row.
public partial class InterviewUI
{
    const int ChatTurns = 3, ChatMaxExchanges = 8;
    bool chatMode;
    bool chatSkip;
    float rapport = 0.5f;
    string lastSaid = "";
    GlitchButton chatSkipBtn;
    QuestionData chatQ;

    IEnumerator SmallTalkStage()
    {
        chatMode = true;
        chatSkip = false;
        rapport = 0.5f;
        SmallTalk.Reset();
        if (chatQ == null) chatQ = new QuestionData { id = "chat", type = "short", bucket = "chat", difficulty = "easy", text = "" };
        BuildChatSkip();

        yield return SaySmall(Persona.Opener(mood, ResumeSession.CandidateName));
        int turn = 0;
        for (int ex = 0; ex < ChatMaxExchanges && turn < ChatTurns && !chatSkip; ex++)
        {
            yield return AskChat("SMALL TALK " + (turn + 1) + "/" + ChatTurns + "  -  say something, ask me something, or type 'ready'");
            if (chatSkip) break;
            string said = ansRaw;
            string nm = ResumeSession.CandidateName;

            if (Profanity.Is(said))                                     // offensive language: one big warning, then rejection before the cognitive test
            {
                conductStrikes++;
                Feel(MoodEvent.Rude); Feel(MoodEvent.Rude);
                Pop(Cartoon.Steam);
                if (rig != null) rig.Trigger(RigEvent.Warn);
                rapport = 0f;
                if (conductStrikes >= 2) { misconduct = true; yield return SaySmall(Persona.Offended(2, nm), false); break; }
                yield return SaySmall(Persona.Offended(1, nm), false);
                continue;
            }

            float conf;
            Intent it = IntentMatcher.Match(said, out conf);
            if (Persona.IsHuh(said)) it = Intent.Repeat;               // "what" / "huh": repeat the last line
            Persona.NoProbe = turn + 1 >= ChatTurns;                   // last turn: no question the player cannot answer
            string soc = (it != Intent.ReadyStart && it != Intent.Rude) ? Persona.Social(said, mood, nm) : null;
            if (soc != null)                                           // compliment, flirt, "are you ok", "what if you were...", "what were we doing"
            {
                turn++;
                switch (Persona.LastSocial)
                {
                    case "flirt": rapport -= Persona.FlirtCount >= 2 ? 0.3f : 0.15f; Feel(MoodEvent.Rude); break;
                    case "slight": rapport -= 0.2f; Feel(MoodEvent.Rude); break;
                    case "praise":
                    case "looks": rapport += 0.1f; Feel(MoodEvent.Polite); Pop(Cartoon.Heart); break;
                }
                rapport = Mathf.Clamp01(rapport);
                yield return SaySmall(soc);
                if (turn >= ChatTurns) yield return SaySmall(Persona.ReadyLine(nm) ?? SmallTalk.Reply(Intent.ReadyStart, mood, nm));
                continue;
            }
            if (it != Intent.ReadyStart && it != Intent.Rude)
            {
                string pa = Persona.Answer(said, mood, nm);        // a question about the interviewer: answer in persona
                if (pa != null)
                {
                    turn++;                                           // answering the player's question uses a turn, so the 1/3 counter is honest
                    yield return SaySmall(pa);
                    if (turn >= ChatTurns) yield return SaySmall(Persona.ReadyLine(nm) ?? SmallTalk.Reply(Intent.ReadyStart, mood, nm));
                    continue;
                }
            }
            turn++;
            switch (it)
            {
                case Intent.Rude: rapport -= 0.3f; Feel(MoodEvent.Rude); Pop(Cartoon.Steam); break;
                case Intent.Thanks:
                case Intent.Compliment: rapport += 0.15f; Feel(MoodEvent.Polite); Pop(Cartoon.Heart); break;
                case Intent.Greet:
                case Intent.Sorry: rapport += 0.1f; Feel(MoodEvent.Polite); break;
            }
            rapport = Mathf.Clamp01(rapport);

            string prev = lastSaid;                              // the interviewer's last line, kept for repeat/clarify
            string reply = it == Intent.Unknown ? Persona.React(said, mood, nm) : null;
            if (reply == null)
            {
                reply = it == Intent.ReadyStart ? Persona.ReadyLine(nm) : null;
                if (reply == null) reply = Persona.Voice(SmallTalk.Reply(it, mood, nm), mood, nm, true);
                if (it == Intent.Rude) { string sc = Persona.ScoldLine(); if (sc != null) reply += " " + sc; }
            }
            bool again = it == Intent.Repeat || it == Intent.Clarify;
            yield return SaySmall(reply, !again);
            if (again)
            {
                string rs = Persona.Restate(it == Intent.Clarify, prev);   // rephrase the open question instead of repeating our own reply
                if (!string.IsNullOrEmpty(rs)) yield return Speak(rs);
                lastSaid = prev;
            }
            if (it == Intent.ReadyStart) break;
            if (turn >= ChatTurns) yield return SaySmall(Persona.ReadyLine(nm) ?? SmallTalk.Reply(Intent.ReadyStart, mood, nm));
        }

        Persona.NoProbe = false;
        if (!chatSkip)
        {
            // let the player read the interviewer's last line before the next stage starts; any key continues
            HideControls();
            if (chatSkipBtn != null) chatSkipBtn.gameObject.SetActive(false);
            Commit("<color=" + HexDim + ">[ press any key to continue ]</color>");
            yield return WaitOrKey(6f);
        }

        chatMode = false;
        if (chatSkipBtn != null) { WorldUtil.Kill(chatSkipBtn.gameObject); chatSkipBtn = null; }
        suspicion = Mathf.Clamp(suspicion + Mathf.Lerp(4f, -4f, rapport), 0f, MaxSuspicion());
        HideControls();
        committed.Length = 0;
        tail = "";
        Refresh();
    }

    IEnumerator SaySmall(string text, bool note = true)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        if (note) Persona.Note(text);
        string keep = lastSaid;
        lastSaid = text;
        yield return Speak(text);
        if (text == keep) lastSaid = text;
    }

    IEnumerator AskChat(string hint)
    {
        state = State.Typing;
        current = chatQ;
        HideControls();
        Commit("<color=" + HexDim + ">" + Clean(hint) + "</color>");
        ShowControls(false, null, null);
        while (state == State.Asking) yield return null;
    }

    void BuildChatSkip()
    {
        if (controlsRt == null || chatSkipBtn != null) return;
        chatSkipBtn = MakeButton(controlsRt, "ChatSkipBtn", "SKIP SMALL TALK", Vector2.zero, new Vector2(S(250f), S(38f)),
            delegate { chatSkip = true; if (state == State.Asking) Submit("skip", false); });
        RectTransform rt = chatSkipBtn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(0f, S(6f));
        chatSkipBtn.Reveal(0f);
    }
}
