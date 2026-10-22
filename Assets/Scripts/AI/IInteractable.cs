using UnityEngine;

public interface IInteractable
{
    Vector3 Position { get; }
    float Range { get; }
    string Prompt { get; }
    bool CanInteract { get; }
    void Interact();
}
