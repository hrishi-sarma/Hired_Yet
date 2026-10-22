using UnityEngine;

namespace Items
{
    // One small bird that flies in and sits on the guitar (neck/body join, between the hands) while the guitar is raised,
    // watches the player, bobs to the music, and flies off when the guitar is lowered. Lives on the held guitar object.
    [DefaultExecutionOrder(1200)]
    public class GuitarPerchBird : MonoBehaviour
    {
        public FlyerSpecies species = FlyerSpecies.Robin;
        public Vector3 perch = new Vector3(0f, 0.043f, 0.14f);   // guitar-local, middle of the instrument
        public float scale = 1.1f;
        public float arriveDelay = 5f;   // seconds of continuous playing before the bird lands
        public float leaveDelay = 3f;    // seconds after playing stops before the bird flies off

        GuitarPoseDriver drv;
        Transform root;
        FlyerRig rig;
        float land, playT, quietT, yaw, headT, nextTilt, tilt, tiltAmt, seed;
        bool present;
        int lastLayer = -1;
        bool yawInit;
        Quaternion rot = Quaternion.identity;

        void Awake() { drv = GetComponent<GuitarPoseDriver>(); seed = Random.value * 50f; nextTilt = 2f; }

        void Build()
        {
            var go = new GameObject("PerchBird");
            root = go.transform; root.SetParent(transform, false);
            rig = FlyerModelFactory.Build(species, root, new System.Random(7), 1f);
            rig.visual.localScale = Vector3.one * scale * 0.62f;
            lastLayer = -1;
        }

        static float Ease(float a, float b, float k) { return a + (b - a) * k; }

        void LateUpdate()
        {
            if (drv == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            var pl = GuitarPlayer.Instance;
            bool playing = pl != null && pl.Playing;
            bool raised = drv.Target > 0f && drv.Blend > 0.9f;
            // The bird only comes while the guitar is actually being played, and stays for leaveDelay seconds after playing stops.
            if (drv.Target <= 0f) { playT = 0f; quietT = 0f; }
            else if (playing) { quietT = 0f; if (raised) playT += dt; }
            else { quietT += dt; if (!present) playT = 0f; }
            bool want;
            if (drv.Target <= 0f) want = false;                   // guitar lowered: leave straight away
            else if (present) want = quietT < leaveDelay;         // already perched: wait out the quiet spell
            else want = playT > arriveDelay;                      // not here yet: needs continuous playing
            present = want;
            land = Mathf.MoveTowards(land, want ? 1f : 0f, dt / (want ? 0.8f : 0.3f));   // leaves fast when the guitar is lowered or the player runs
            if (land <= 0.001f) { if (root != null) root.gameObject.SetActive(false); return; }
            if (root == null) Build();
            root.gameObject.SetActive(true);
            if (lastLayer != gameObject.layer)
            {
                lastLayer = gameObject.layer;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = lastLayer;
            }

            float e = land * land * (3f - 2f * land);
            bool flying = land < 0.999f;
            var cam = Camera.main;
            // Stand the bird upright on SCREEN (camera up), on the part of the body that is highest along that axis.
            // Using camera up (not world up) keeps it from leaning toward the lens when the player looks down.
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
            Vector3 camR = cam != null ? cam.transform.right : Vector3.right;
            Vector3 d = transform.InverseTransformDirection(camUp);
            float hw = GuitarModel.HalfW(perch.z);
            Vector3 lp = new Vector3(hw * Mathf.Clamp(d.x * 4f, -1f, 1f), d.y > 0f ? GuitarModel.TopY(perch.z) : -0.045f, perch.z);
            Vector3 rest = transform.TransformPoint(lp) + camUp * 0.004f;
            Vector3 away = rest + camR * 0.3f + camUp * 0.45f;
            Vector3 p = Vector3.Lerp(away, rest, e);
            p += camUp * (Mathf.Sin(e * Mathf.PI) * 0.06f);
            root.position = p;

            // face the player (three-quarter view), so it reads as watching
            Vector3 toCam = cam != null ? cam.transform.position - p : -camR;
            toCam -= camUp * Vector3.Dot(toCam, camUp);
            if (toCam.sqrMagnitude < 0.0001f) toCam = camR;
            Quaternion target = Quaternion.LookRotation(Quaternion.AngleAxis(35f, camUp) * toCam.normalized, camUp);
            rot = yawInit ? Quaternion.Slerp(rot, target, 1f - Mathf.Exp(-10f * dt)) : target;
            yawInit = true;
            root.rotation = rot * Quaternion.Euler(flying ? -20f * (1f - e) : 0f, 0f, 0f);

            // pose
            float time = Time.unscaledTime + seed;
            float flap = 18f + Mathf.Sin(time * 55f) * 50f;
            for (int k = 0; k < 2; k++)
            {
                float s = k == 0 ? -1f : 1f;
                Quaternion fold = Quaternion.LookRotation(Vector3.up, Vector3.right * -s);
                Quaternion open = Quaternion.Euler(0f, 0f, s * flap);
                rig.wings[k].localRotation = Quaternion.Slerp(fold, open, flying ? 1f : 0f);
                rig.wings[k].localScale = new Vector3(flying ? 1f : 0.62f, 1f, 1f);
            }
            float lvl = playing ? pl.ClipLevel : 0f;
            float bob = playing ? Mathf.Abs(Mathf.Sin(time * 5.2f)) * (4f + lvl * 8f) : 0f;
            // occasional curious head tilt
            headT += dt;
            if (headT > nextTilt) { headT = 0f; nextTilt = Random.Range(1.5f, 4f); tilt = Random.Range(-1f, 1f) * 25f; }
            tiltAmt = Ease(tiltAmt, tilt, 1f - Mathf.Exp(-8f * dt));
            float hp = flying ? 0f : (Mathf.Sin(time * 1.3f) * 5f + (playing ? bob * 0.8f : 0f));
            rig.body.localRotation = Quaternion.Euler(flying ? 12f : (playing ? bob * 0.5f : 0f), 0f, 0f);
            rig.body.localPosition = new Vector3(0f, 0.095f, 0f);
            rig.head.localRotation = Quaternion.Euler(hp, flying ? 0f : tiltAmt * 0.6f, flying ? 0f : tiltAmt * 0.3f);
            rig.head.localPosition = new Vector3(0f, 0.062f, 0.095f);
            rig.tail.localRotation = Quaternion.Euler((flying ? -6f : 0f) + (playing ? bob * 1.5f : Mathf.Sin(time * 2f) * 2f), 0f, 0f);
            rig.legRoot.gameObject.SetActive(!flying || land > 0.85f);
            for (int i = 0; i < 2; i++) if (rig.eyes[i] != null) rig.eyes[i].localScale = Vector3.one * 0.017f;
        }
    }
}
