using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldEventLibrary", menuName = "World/Event Library")]
public class WorldEventLibrary : ScriptableObject
{
    public List<WorldEventConfig> events = new List<WorldEventConfig>();
}
