using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TMP_Text))]
public class LogClickHandler : MonoBehaviour, IPointerClickHandler
{
    public event Action<int> LinkClicked;

    TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
        if (index < 0) return;
        string id = text.textInfo.linkInfo[index].GetLinkID();
        int value;
        if (int.TryParse(id, out value) && LinkClicked != null) LinkClicked(value);
    }
}
