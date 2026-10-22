using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Flat terminal button: solid black box, green text, no border, no rounded corners.
/// The matrix-style "decode" glitch plays ONCE and then stops:
/// - Hover: every time the pointer enters, the label scrambles and decodes back to its text (~0.35 s), then stays still.
/// - Reveal(): the label decodes from random glyphs when the buttons appear (one shot).
/// - Pulse(): the same one-shot glitch (used for hotkeys and clicks).
/// There are no idle glitch bursts and no continuous scramble while hovering.
/// Created at runtime by InterviewUI. Labels must be plain text (rich text is switched off).
/// </summary>
public class GlitchButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    const string Glyphs = "01#$%&@*+=/\\|?!~^:;";   // no '<' or '>' on purpose
    const float RevealDuration = 0.5f;
    const float GlitchDuration = 0.35f;

    TMP_Text label;
    Image fill;
    RectTransform labelRt;
    string finalText = "";
    Color normal;
    Color glitchColor;
    Action clicked;
    readonly StringBuilder sb = new StringBuilder();

    bool revealing;
    float revealT;
    float glitchLeft;
    float nextScramble;

    public void Setup(TMP_Text text, Image fillImage, string finalLabel,
        Color textColor, Color glitchTextColor, Color backgroundColor, Action onClick)
    {
        label = text;
        fill = fillImage;
        finalText = finalLabel;
        normal = textColor;
        glitchColor = glitchTextColor;
        clicked = onClick;

        labelRt = label.rectTransform;
        label.richText = false;
        label.text = finalText;
        label.color = normal;
        if (fill != null) fill.color = backgroundColor;
    }

    public void SetLabel(string newLabel)
    {
        finalText = newLabel;
        if (label != null) label.text = newLabel;
    }

    /// <summary>Change the label colors (e.g. red on the REJECTED screen).</summary>
    public void SetTextColors(Color textColor, Color glitchTextColor)
    {
        normal = textColor;
        glitchColor = glitchTextColor;
        if (label != null) label.color = normal;
    }

    /// <summary>Start the decode animation after an optional delay (seconds).</summary>
    public void Reveal(float delay)
    {
        revealing = true;
        revealT = -delay;
        nextScramble = 0f;
    }

    /// <summary>One short glitch, e.g. when the matching hotkey is pressed.</summary>
    public void Pulse()
    {
        StartGlitch();
    }

    void StartGlitch()
    {
        glitchLeft = GlitchDuration;
        nextScramble = 0f;
    }

    void OnEnable()
    {
        revealing = false;
        glitchLeft = 0f;
        if (label != null)
        {
            label.text = finalText;
            label.color = normal;
            labelRt.anchoredPosition = Vector2.zero;
        }
    }

    void Update()
    {
        if (label == null) return;
        float dt = Time.unscaledDeltaTime;

        if (revealing)
        {
            revealT += dt;
            if (revealT >= RevealDuration) revealing = false;
        }
        if (glitchLeft > 0f) glitchLeft -= dt;

        bool scramble = revealing || glitchLeft > 0f;
        if (scramble)
        {
            nextScramble -= dt;
            if (nextScramble <= 0f)
            {
                nextScramble = revealing ? 0.03f : 0.04f;
                label.text = Compose();
                float j = revealing ? 0f : 2f;
                labelRt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-j, j), UnityEngine.Random.Range(-j, j) * 0.5f);
            }
            label.color = revealing ? normal : glitchColor;
        }
        else if (label.text != finalText || label.color != normal)
        {
            // glitch finished: settle on the real label and stay still
            label.text = finalText;
            label.color = normal;
            labelRt.anchoredPosition = Vector2.zero;
        }
    }

    string Compose()
    {
        if (revealing && revealT < 0f) return ""; // waiting for its turn

        sb.Length = 0;
        int n = finalText.Length;
        float reveal = revealing ? Mathf.Clamp01(revealT / RevealDuration) * (n + 3) : 0f;
        float progress = 1f - Mathf.Clamp01(glitchLeft / GlitchDuration); // hover glitch decodes as it ends

        for (int i = 0; i < n; i++)
        {
            char c = finalText[i];
            if (c == ' ')
            {
                sb.Append(' ');
                continue;
            }

            bool ok;
            if (revealing) ok = i < reveal - UnityEngine.Random.Range(0f, 3f);
            else ok = UnityEngine.Random.value < 0.3f + 0.7f * progress;

            sb.Append(ok ? c : Glyphs[UnityEngine.Random.Range(0, Glyphs.Length)]);
        }
        return sb.ToString();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartGlitch(); // once per hover; leaving and re-entering plays it again
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Pulse();
        if (clicked != null) clicked();
    }
}