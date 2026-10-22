using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VoiceCircle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public Action OnClick;
    public bool Pulsing;

    Image img;
    RectTransform rt;
    float tilt;
    bool hover, down, interactable = true;

    public void Init(Image image, float tiltDegrees)
    {
        img = image;
        rt = GetComponent<RectTransform>();
        tilt = tiltDegrees;
        rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    public void SetSprite(Sprite s) { if (img != null && img.sprite != s) img.sprite = s; }
    public void SetInteractable(bool v) { interactable = v; }

    void Update()
    {
        if (img == null) return;
        float dt = Time.unscaledDeltaTime;
        float target = !interactable ? 0.92f : (down ? 0.9f : (hover ? 1.1f : 1f));
        if (Pulsing && interactable) target += 0.06f * Mathf.Sin(Time.unscaledTime * 7f);
        float s = Mathf.Lerp(rt.localScale.x, target, 1f - Mathf.Exp(-18f * dt));
        rt.localScale = new Vector3(s, s, 1f);
        float rz = tilt + ((hover && interactable) ? 4f * Mathf.Sin(Time.unscaledTime * 12f) : 0f);
        rt.localRotation = Quaternion.Euler(0f, 0f, rz);
        Color c = img.color;
        c.a = Mathf.Lerp(c.a, interactable ? 1f : 0.38f, 1f - Mathf.Exp(-14f * dt));
        img.color = c;
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; }
    public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
    public void OnPointerDown(PointerEventData e) { down = true; }
    public void OnPointerUp(PointerEventData e) { down = false; }
    public void OnPointerClick(PointerEventData e) { if (interactable && OnClick != null) OnClick(); }
}
