using UnityEngine;

public sealed class CarLightSource : MonoBehaviour
{
    public bool head;
    public bool brake;
    public bool hazard;
    public bool blinkOn;
    public int indicator;
    public float length = 4.2f;

    public bool Active
    {
        get { return head || brake || hazard || indicator != 0; }
    }
}
