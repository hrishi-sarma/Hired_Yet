using UnityEngine;

public static class NpcModelBuilder
{
    public static GameObject Build(NpcProfile profile, Transform parent, float radius, float scale, out float height, out Animator animator, NpcLook look = null)
    {
        GameObject go = new GameObject("NPC");
        go.transform.SetParent(parent, false);

        GameObject model = Object.Instantiate(profile.prefab, go.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        NpcStylizedPainter.Apply(model, new System.Random(Random.Range(0, int.MaxValue)), look);

        if (profile.stripPrefabPhysics)
        {
            Collider[] cols = model.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) WorldUtil.Kill(cols[i]);
            Rigidbody[] rbs = model.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++) WorldUtil.Kill(rbs[i]);
        }

        float native = MeasureHeight(model);
        float fit = profile.fitToHeight > 0.1f && native > 0.05f ? profile.fitToHeight / native : 1f;
        if (fit != 1f) model.transform.localScale *= fit;
        float localHeight = native > 0.05f ? native * fit : 1.75f;
        float k = Mathf.Max(0.01f, scale);
        float worldHeight = localHeight * k;
        if (worldHeight > 2.6f || worldHeight < 0.9f) k *= 1.75f / worldHeight;
        go.transform.localScale = new Vector3(k, k, k);
        height = localHeight * k;

        CapsuleCollider col = go.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.radius = radius / k;
        col.height = localHeight;
        col.center = new Vector3(0f, localHeight * 0.5f, 0f);

        animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = model.AddComponent<Animator>();
        return go;
    }

    static float MeasureHeight(GameObject model)
    {
        Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        Bounds b = new Bounds();
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] is ParticleSystemRenderer) continue;
            if (!any)
            {
                b = rs[i].bounds;
                any = true;
            }
            else b.Encapsulate(rs[i].bounds);
        }
        return any ? b.size.y : 0f;
    }
}
