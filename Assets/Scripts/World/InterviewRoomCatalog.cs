using UnityEngine;

/// <summary>
/// Holds pools of prefabs and materials used by InterviewRoomGenerator.
/// Can be customized in the Unity Inspector by dropping in your Asset Store prefabs.
/// Any list left empty will automatically fall back to procedural low-poly geometry.
/// </summary>
[CreateAssetMenu(fileName = "InterviewRoomCatalog", menuName = "Interview Room/Catalog", order = 10)]
public class InterviewRoomCatalog : ScriptableObject
{
    [Header("1. Main Furniture")]
    [Tooltip("Office desks / tables placed at the center of the room.")]
    public GameObject[] deskPrefabs = new GameObject[0];

    [Tooltip("Executive / swivel chairs for the interviewer behind the desk.")]
    public GameObject[] interviewerChairPrefabs = new GameObject[0];

    [Tooltip("Visitor / guest chairs for the candidate in front of the desk.")]
    public GameObject[] guestChairPrefabs = new GameObject[0];

    [Header("2. Desk Clutter & Accessories")]
    [Tooltip("Desk lamps (can have a point/spotlight attached).")]
    public GameObject[] deskLampPrefabs = new GameObject[0];

    [Tooltip("Laptops (open or closed) to place on the desk.")]
    public GameObject[] laptopPrefabs = new GameObject[0];

    [Tooltip("Small desk items: coffee mugs, water glasses, paper stacks, clipboards, pen holders, nameplates.")]
    public GameObject[] deskClutterPrefabs = new GameObject[0];

    [Header("3. Floor & Corner Props")]
    [Tooltip("Potted office plants: ficus, snake plant, palm, etc.")]
    public GameObject[] plantPrefabs = new GameObject[0];

    [Tooltip("Office corner props: water coolers, filing cabinets, coat racks, trash bins, bookshelves.")]
    public GameObject[] cornerPropPrefabs = new GameObject[0];

    [Tooltip("Rugs or carpets placed under the desk.")]
    public GameObject[] carpetPrefabs = new GameObject[0];

    [Header("4. Wall Decor")]
    [Tooltip("Wall items: whiteboards, wall clocks, framed diplomas/certificates, minimal corporate art.")]
    public GameObject[] wallDecorPrefabs = new GameObject[0];

    [Tooltip("Window frames or blinds prefabs (optional).")]
    public GameObject[] windowPrefabs = new GameObject[0];

    [Tooltip("Door frame / entrance door prefabs (optional).")]
    public GameObject[] doorPrefabs = new GameObject[0];

    [Header("5. Interviewer Avatars")]
    [Tooltip("Seated or formal humanoid character prefabs.")]
    public GameObject[] interviewerPrefabs = new GameObject[0];

    [Header("6. Room Materials & Finishes (Optional)")]
    [Tooltip("Materials for walls (painted drywall, wood slat, brick, concrete).")]
    public Material[] wallMaterials = new Material[0];

    [Tooltip("Materials for flooring (carpet tiles, hardwood parquet, polished concrete).")]
    public Material[] floorMaterials = new Material[0];

    [Tooltip("Materials for ceiling (drop ceiling tiles, dark industrial, white plaster).")]
    public Material[] ceilingMaterials = new Material[0];

    public GameObject PickRandom(GameObject[] array, System.Random rng)
    {
        if (array == null || array.Length == 0) return null;
        int validCount = 0;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] != null) validCount++;
        }
        if (validCount == 0) return null;

        int pick = rng.Next(validCount);
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == null) continue;
            if (pick == 0) return array[i];
            pick--;
        }
        return null;
    }

    public Material PickRandomMaterial(Material[] array, System.Random rng)
    {
        if (array == null || array.Length == 0) return null;
        int validCount = 0;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] != null) validCount++;
        }
        if (validCount == 0) return null;

        int pick = rng.Next(validCount);
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == null) continue;
            if (pick == 0) return array[i];
            pick--;
        }
        return null;
    }
}
