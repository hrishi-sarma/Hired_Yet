using UnityEngine;
namespace FPAnimation
{
    public abstract class FPPatAction : FPAction
    {
        readonly Transform animal;
        readonly bool dog;
        Vector3 startPlayer, startAnimal;
        bool started;

        protected abstract float Hover { get; }
        protected abstract float Standoff { get; }
        protected abstract float Height { get; }

        protected FPPatAction(Transform animal, bool isDog, float seconds)
        {
            this.animal = animal; dog = isDog; Duration = Mathf.Max(1.2f, seconds);
        }

        public override bool ShouldInterrupt(in FPActionCtx x)
        {
            if (animal == null || !animal.gameObject.activeInHierarchy) return true;
            Vector3 pp = x.c.transform.position;
            if (!started) { started = true; startPlayer = pp; startAnimal = animal.position; }
            if ((pp - startPlayer).sqrMagnitude > 1.44f) return true;
            if ((animal.position - startAnimal).sqrMagnitude > 2.25f) return true;
            return Disturbed(x);
        }

        public override void Evaluate(in FPActionCtx x, float n, ref FPPose p, out float dip)
        {
            float D = Duration, t = n * D;
            float rEnd = D - 0.35f;
            float down = Sm(0f, 0.35f, t) * (1f - Sm(rEnd, D, t));
            float reach = Sm(0.2f, 0.45f, t) * (1f - Sm(rEnd - 0.1f, rEnd, t));
            float h = Height;
            Vector3 ap = animal != null ? animal.position : x.camBase;
            Vector3 toA = ap - x.camBase; toA.y = 0f;
            float d = toA.magnitude;
            Vector3 dir = d > 0.01f ? toA / d : Vector3.zero;
            Vector3 basePt = ap + Vector3.up * (h + Hover) - dir * (dog ? 0.15f : 0.18f);
            float lunge = Mathf.Clamp(d - Standoff, 0f, 1.6f) * down;
            CamMove = dir * lunge;

            // relative to the animal's ground level (world-y made the dip always max out), and never dip a camera that is already low (crouch/sit)
            float want = Mathf.Clamp(x.camBase.y - (ap.y + h + 0.45f), 0f, 1.05f);
            if (FPEvents.Sitting) want = 0f;
            dip = -want * down;
            float camY = x.camBase.y + dip, hz = Mathf.Max(0.15f, d - lunge);
            float look = Mathf.Atan2(camY - basePt.y, hz) * Mathf.Rad2Deg;
            CamPitch = Mathf.Clamp(look - x.rawPitch, 0f, 35f) * down * 0.6f;

            p.torsoLean = 15f * down;
            p.L = new FPArm { flex = Mathf.Lerp(p.L.flex, 70f, down), abd = 8f, elbow = Mathf.Lerp(p.L.elbow, 25f, down), curl = 0.3f, pron = 0.6f };

            Vector3 fwd = animal != null ? animal.forward : Vector3.forward;
            Vector3 right = animal != null ? animal.right : Vector3.right;
            float sp = Mathf.InverseLerp(0.45f, rEnd - 0.1f, t);
            Vector3 world;
            float roll;
            if (dog)
            {
                float ph = Mathf.Sin(sp * 2.5f * Mathf.PI * 2f);
                world = basePt + fwd * (0.20f * ph);
                roll = ph;
            }
            else
            {
                float ph = sp * 3f * Mathf.PI * 2f;
                world = basePt + fwd * 0.12f + right * (0.03f * Mathf.Sin(ph)) + Vector3.up * (0.02f * Mathf.Cos(ph));
                roll = Mathf.Sin(ph);
            }
            p.R = new FPArm
            {
                flex = 60f, abd = 10f, elbow = 40f,
                curl = Mathf.Lerp(0.3f, dog ? 0.05f : 0.45f, reach),
                pron = 0.8f + 0.15f * roll * reach,
                ikW = reach,
                ikTarget = x.ToChest(world)
            };
        }
    }
}

namespace FPAnimation
{
    public class FPPatDogAction : FPPatAction
    {
        public FPPatDogAction(Transform animal, float seconds) : base(animal, true, seconds) { }
        protected override float Hover { get { return 0.07f; } }
        protected override float Standoff { get { return 0.95f; } }
        protected override float Height { get { return 0.55f; } }
    }

    public class FPPatCatAction : FPPatAction
    {
        public FPPatCatAction(Transform animal, float seconds) : base(animal, false, seconds) { }
        protected override float Hover { get { return 0.28f; } }
        protected override float Standoff { get { return 0.8f; } }
        protected override float Height { get { return 0.28f; } }
    }
}
