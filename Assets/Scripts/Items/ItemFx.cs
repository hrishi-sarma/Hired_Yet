using UnityEngine;
using UnityEngine.Rendering;

namespace Items
{
    // Stage 10: small sparkle on Rare+ world items, gold glow on Legendary.
    public static class ItemFx
    {
        static Material mat;

        static Material Mat()
        {
            if (mat != null) return mat;
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultParticleMaterial != null) mat = new Material(rp.defaultParticleMaterial);
            else { var sh = Shader.Find("Sprites/Default"); if (sh != null) mat = new Material(sh); }
            return mat;
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(.55f, .85f, 1f);
                case Rarity.Epic: return new Color(.78f, .65f, 1f);
                case Rarity.Legendary: return new Color(1f, .82f, .25f);
                default: return Color.white;
            }
        }

        public static void Attach(GameObject go, ItemInstance it)
        {
            if (go == null || it == null || it.def.rarity < Rarity.Rare) return;
            var m = Mat(); if (m == null) return;
            var fx = new GameObject("RarityFx"); fx.transform.SetParent(go.transform, false);
            var ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Color c = RarityColor(it.def.rarity); bool leg = it.def.rarity == Rarity.Legendary;
            var main = ps.main; main.loop = true; main.startLifetime = 1.1f; main.startSpeed = 0.06f; main.startSize = leg ? 0.035f : 0.022f;
            main.startColor = c; main.maxParticles = leg ? 24 : 10; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -0.02f;
            var em = ps.emission; em.rateOverTime = leg ? 14f : 4f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(.2f, .02f, .2f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .3f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = fx.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            if (leg)
            {
                var l = fx.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = 1.6f; l.intensity = 0.8f; l.shadows = LightShadows.None;
            }
        }
    }
}
