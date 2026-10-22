using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WorldGenerator))]
[AddComponentMenu("Procedural World/World Bootstrap")]
public class WorldBootstrap : MonoBehaviour
{
    [Header("Player")]
    public GameObject playerPrefab;
    public string playerTag = "Player";
    public float spawnLift = 1.2f;
    public bool removeScenePlayers = true;

    [Header("Launch")]
    public bool randomSeed = true;
    public bool runWhenOpenedDirectly = false;

    IEnumerator Start()
    {
        bool launched = WorldLauncher.Launching;
        if (!launched && !runWhenOpenedDirectly) yield break;

        LoadingScreen.Kind = LoadingScreen.LoadKind.World;
        LoadingScreen screen = LoadingScreen.Show();
        screen.SetProgress(0.1f, "preparing");
        yield return null;

        WorldGenerator gen = GetComponent<WorldGenerator>();
        SaveData sv = SaveSystem.Pending;
        if (sv != null && sv.stage == 0 && sv.worldSeed != 0) gen.settings.seed = sv.worldSeed;   // same world as the saved game
        else if (randomSeed) gen.NewSeed();
        SaveSystem.NoteWorldSeed(gen.settings.seed);

        IEnumerator routine = gen.GenerateRoutine();
        while (routine.MoveNext())
        {
            screen.SetProgress(0.1f + 0.85f * gen.Progress, gen.CurrentStage);
            yield return null;
        }
        if (!gen.LastGenerateOk || gen.Context == null)
        {
            WorldLauncher.Fail("a generation stage failed, see the Console");
            yield break;
        }

        screen.SetProgress(0.97f, "spawning player");
        yield return null;

        if (playerPrefab == null)
        {
            WorldLauncher.Fail("WorldBootstrap has no player prefab assigned");
            yield break;
        }

        GameObject player = null;
        yield return SpawnPlayer(gen.Context, p => player = p);
        WorldLauncher.Player = player.transform;

        NpcDirector director = GetComponent<NpcDirector>();
        if (director == null) director = gameObject.AddComponent<NpcDirector>();
        if (director.enabled) director.Begin(gen.Context, player.transform);

        // shadows: URP/sun settings + every solid mesh casts (time-sliced, ~cheap); a light follow-up catches late spawns
        screen.SetProgress(0.985f, "casting shadows");
        yield return WorldShadowFix.Apply(k => screen.SetProgress(0.985f + 0.012f * k, "casting shadows"));
        StartCoroutine(WorldShadowFix.Followup());

        screen.SetProgress(1f, "ready");
        yield return null;
        WorldLauncher.Launching = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SaveSystem.WorldReady();
        screen.Finish();
    }

    IEnumerator SpawnPlayer(WorldContext c, System.Action<GameObject> done)
    {
        if (removeScenePlayers)
        {
            GameObject[] old = GameObject.FindGameObjectsWithTag(playerTag);
            for (int i = 0; i < old.Length; i++) WorldUtil.Kill(old[i]);
        }

        Vector3 pos;
        Quaternion rot;
        FindSpawn(c, out pos, out rot);
        SaveData sv = SaveSystem.Pending;
        if (sv != null && sv.stage == 0 && sv.hasPos)
        {
            float gy = c.HeightAt(sv.pos.x, sv.pos.z);
            pos = new Vector3(sv.pos.x, Mathf.Max(sv.pos.y + 0.15f, gy + 0.2f), sv.pos.z);
            rot = Quaternion.Euler(0f, sv.yaw, 0f);
        }

        GameObject holder = new GameObject("PlayerSpawnHolder");
        holder.SetActive(false);
        GameObject player = Instantiate(playerPrefab, holder.transform);
        player.transform.SetPositionAndRotation(pos, rot);
        player.SetActive(false);
        player.transform.SetParent(null, true);
        WorldUtil.Kill(holder);
        player.tag = playerTag;

        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        player.SetActive(true);

        Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
            if (!cams[i].transform.IsChildOf(player.transform) && cams[i].targetTexture == null) cams[i].enabled = false;
        AudioListener[] ls = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 0; i < ls.Length; i++)
            if (!ls[i].transform.IsChildOf(player.transform)) ls[i].enabled = false;

        done(player);
    }

    void FindSpawn(WorldContext c, out Vector3 pos, out Quaternion rot)
    {
        Vector2 look = c.zoneMap != null ? c.zoneMap.cityCenter : Vector2.zero;
        pos = new Vector3(0f, c.HeightAt(0f, 0f) + spawnLift + 1f, 0f);

        if (c.roads != null && c.zoneMap != null)
        {
            List<BlockInfo> blocks = new List<BlockInfo>();
            for (int i = 0; i < c.roads.blocks.Count; i++)
                if (c.roads.blocks[i].zone == ZoneType.Suburb) blocks.Add(c.roads.blocks[i]);
            Vector2 sc = c.zoneMap.suburbCenter;
            blocks.Sort((a, b) => (a.center - sc).sqrMagnitude.CompareTo((b.center - sc).sqrMagnitude));
            bool found = false;
            for (int k = 0; k < blocks.Count && k < 8 && !found; k++)
            {
                BlockInfo b = blocks[k];
                float o = b.lotRect.width * 0.5f + b.sidewalkWidth * 0.5f;
                for (int side = 0; side < 4 && !found; side++)
                {
                    for (int s = -3; s <= 3 && !found; s++)
                    {
                        float t = s * 4f;
                        Vector2 p;
                        if (side == 0) p = new Vector2(b.center.x + t, b.center.y - o);
                        else if (side == 1) p = new Vector2(b.center.x + t, b.center.y + o);
                        else if (side == 2) p = new Vector2(b.center.x - o, b.center.y + t);
                        else p = new Vector2(b.center.x + o, b.center.y + t);
                        Vector3 baseCenter = new Vector3(p.x, b.sidewalkTop, p.y);
                        OrientedBox box = OrientedBox.FromBase(baseCenter, new Vector3(1.4f, 2f, 1.4f), 0f);
                        if (!c.occupancy.IsFree(box, PlacementLayer.Prop, 0f)) continue;
                        pos = baseCenter + Vector3.up * spawnLift;
                        found = true;
                    }
                }
            }
        }

        Vector3 dir = new Vector3(look.x - pos.x, 0f, look.y - pos.z);
        rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
    }
}
