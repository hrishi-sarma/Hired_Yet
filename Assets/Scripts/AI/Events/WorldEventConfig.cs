using UnityEngine;

[CreateAssetMenu(fileName = "WorldEventConfig", menuName = "World/Event Config")]
public class WorldEventConfig : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public EventZone zone;
    public bool enabled = true;
    [Min(0f)] public float spawnWeight = 1f;

    [Header("Text")]
    public string npcLabel = "NPC";
    public string prompt;
    [TextArea] public string ask;
    [TextArea] public string success;
    [TextArea] public string fail;

    [Header("NPC (leave blank for a random NPC)")]
    public NpcProfile npcOverride;
    public AnimationClip npcBefore;
    public AnimationClip npcDuring;
    public AnimationClip npcAfter;

    [Header("Sounds")]
    public AudioClip soundBefore;
    public AudioClip soundDuring;
    public AudioClip soundAfter;
    [Range(0f, 1f)] public float soundVolume = 1f;

    [Header("Prop (leave prefab blank to use the primitive)")]
    public GameObject propPrefab;
    public Vector3 propScale = Vector3.one;
    public Vector3 propOffset;
    public PrimitiveKind propKind = PrimitiveKind.Cube;
    public Color propColor = Color.gray;
    [Tooltip("Footprint of the prop in metres (x width, y height, z length). Also used for placement checks when a prefab is assigned, so set it to the model's size.")]
    public Vector3 propSize = Vector3.one;
    [Tooltip("Metres the prop moves during the event. 0 uses the built-in default for this event id.")]
    [Min(0f)] public float travelDistance;
    public Color shirt = Color.gray;

    [Header("Mechanic")]
    public EventHandlerKind handler;
    public float amount = 5f;
    public float limit;
    [Range(0f, 1f)] public float reward = 0.35f;

    [Header("Player animation")]
    public PlayerEventAnim playerBefore = PlayerEventAnim.Greet;
    public PlayerEventAnim playerDuring;
    public PlayerEventAnim playerAfter = PlayerEventAnim.Goodbye;

    [Header("Handler parameters")]
    public TimingParams timing = new TimingParams();
    public ChoiceParams choice = new ChoiceParams();
    public SortingParams sorting = new SortingParams();
    public ObjectParams objects = new ObjectParams();

    public bool Implemented
    {
        get { return handler == EventHandlerKind.Hold || handler == EventHandlerKind.Mash || handler == EventHandlerKind.Sequence || handler == EventHandlerKind.Timing || handler == EventHandlerKind.Choice || handler == EventHandlerKind.Sorting || handler == EventHandlerKind.Object; }
    }
}
