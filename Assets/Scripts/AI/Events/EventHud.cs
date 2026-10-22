using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen-space event guidance: a chunky cartoon compass arrow at the top middle (up = straight ahead) with a stylized message card under it.
/// Shared by the delivery events (arrow to the drop-off) and the paper gust (arrow and prompt, near the papers only).
/// Callers pass an owner key so one event cannot clear another's HUD. The message string uses the same format as PromptStack.</summary>
public class EventHud : MonoBehaviour
{
    static EventHud inst;
    string owner = "";
    RectTransform arrow;
    PromptStack msg;

    static EventHud Get()
    {
        if (inst == null)
        {
            GameObject g = new GameObject("EventHud");
            inst = g.AddComponent<EventHud>();
            inst.Build();
        }
        return inst;
    }

    /// <summary>Show the arrow (angle = degrees to turn right to face the target) and/or a message. Empty message hides the card.</summary>
    public static void Set(string who, bool arrowOn, float angle, string text, Color msgColor)
    {
        EventHud h = Get();
        h.owner = who;
        h.arrow.gameObject.SetActive(arrowOn);
        if (arrowOn)
        {
            h.arrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
            h.arrow.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.time * 6f));
        }
        h.msg.Show(text);
    }

    public static void Clear(string who)
    {
        if (inst == null || inst.owner != who) return;
        inst.owner = "";
        inst.arrow.gameObject.SetActive(false);
        inst.msg.Hide();
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 98;
        CanvasScaler sc = gameObject.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // arrow: round sticker badge holding a chunky arrow, rotates as a whole
        arrow = UIKit.NewRect("Arrow", transform);
        UIKit.Place(arrow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(120f, 120f));
        UIKit.Boxy(UIKit.AddImage(arrow, UIKit.Rounded(60), Toon.Blue), 5f, true);
        RectTransform ring = UIKit.NewRect("Ring", arrow);
        UIKit.Stretch(ring, 9f, 9f, 9f, 9f);
        UIKit.AddImage(ring, UIKit.Ring(50, 3), Toon.Ink);
        RectTransform ic = UIKit.NewRect("Icon", arrow);
        UIKit.Place(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 78f));
        UIKit.AddImage(ic, ToonIcons.Get("arrow"), Toon.Yellow);
        arrow.gameObject.SetActive(false);

        InteractionSystem sys = InteractionSystem.Instance;
        msg = PromptStack.Create("Message", 98, sys != null ? sys.font : null, true, 170f);
    }

    void OnDestroy() { if (msg != null) WorldUtil.Kill(msg.gameObject); if (inst == this) inst = null; }
}
