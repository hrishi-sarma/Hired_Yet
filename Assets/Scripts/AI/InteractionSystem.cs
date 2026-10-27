using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InteractionSystem : MonoBehaviour
{
    public static InteractionSystem Instance { get; private set; }

    public TMP_FontAsset font;
    public string OverridePrompt = "";

    readonly List<IInteractable> items = new List<IInteractable>();
    PromptStack stack;
    string shown = "";
    float interviewCheck;
    bool interviewOpen;

    /// <summary>True while a normal interactable is in range this frame (so other E-key systems, such as sitting, stay out of its way).</summary>
    public bool HasTarget { get; private set; }

    public bool Suppressed
    {
        get { return interviewOpen || DevConsole.Active || PhotoMode.Active || Items.InventoryUI.IsOpen || Booklet.Active || FPAnimation.FPEvents.HoldingItem; }
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (stack != null) WorldUtil.Kill(stack.gameObject);
        if (Instance == this) Instance = null;
    }

    public void Register(IInteractable item)
    {
        if (item != null && !items.Contains(item)) items.Add(item);
    }

    public void Unregister(IInteractable item)
    {
        items.Remove(item);
    }

    void Update()
    {
        NpcDirector d = NpcDirector.Instance;
        if (d == null || d.Player == null)
        {
            Show("");
            return;
        }

        interviewCheck -= Time.unscaledDeltaTime;
        if (interviewCheck <= 0f)
        {
            interviewCheck = 0.5f;
            interviewOpen = FindAnyObjectByType<InterviewUI>() != null;
        }

        string text = "";
        IInteractable best = null;
        HasTarget = false;
        bool holdingItem = FPAnimation.FPEvents.HoldingItem;

        // Keep a nearby interaction visible while carrying, but never invoke it.
        // Suppressed also blocks event minigames that consult this system before reading E.
        if (holdingItem)
        {
            best = Nearest(d.Player.position);
            if (best != null) text = "UNEQUIP THE ITEM TO INTERACT";
        }
        else if (!string.IsNullOrEmpty(OverridePrompt))
        {
            text = OverridePrompt;
        }
        else if (!Suppressed)
        {
            best = Nearest(d.Player.position);
            if (best != null) text = best.Prompt;
        }

        // Timing / sequence / choice events have their own big panel; do not repeat their text as a prompt.
        if (EventPanelUI.Visible && !string.IsNullOrEmpty(OverridePrompt)) text = "";

        HasTarget = best != null;
        Show(text);

        if (!holdingItem && best != null)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                if (!(best is AnimalAgent) && !(best is PickpocketTarget)) FPAnimation.FPEvents.RaiseInteract(best.Position);
                best.Interact();
            }
        }
    }

    IInteractable Nearest(Vector3 p)
    {
        IInteractable best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < items.Count; i++)
        {
            IInteractable it = items[i];
            if (!it.CanInteract) continue;
            Vector3 diff = it.Position - p;
            float dy = Mathf.Abs(diff.y);
            diff.y = 0f;
            float sq = diff.sqrMagnitude;
            if (dy > 3f || sq > it.Range * it.Range || sq >= bestSq) continue;
            bestSq = sq;
            best = it;
        }
        return best;
    }

    void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            if (stack != null) stack.Hide();
            shown = "";
            return;
        }
        if (stack == null) stack = PromptStack.Create("InteractionPrompt", 100, font, false, 90f);
        shown = text;
        stack.Show(text);
    }
}
