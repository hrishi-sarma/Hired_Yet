Lawn / District Rails / Roof / Foliage Refinement

Changes in this revision:
- Suburban lot lawn uses a 12 m world-space tile and a 1024 px procedural texture. Its macro variation is broader and domain-warped, with reduced high-frequency contrast to suppress the small repeating patch pattern.
- City perimeter rails are no longer generated. The suburb perimeter fence remains.
- Suburb perimeter fence segments are skipped on high terrain and steep slopes (normalized height > 0.27 or sampled slope > 0.32), so the fence stays in lowland/plain areas rather than climbing mountains.
- Roof tile world size reduced to 0.72 m and the procedural ridge cadence increased to 14 rows. Roof height data now includes staggered raised ridges and shallow seams, which feed the generated normal map.
- Foliage/leaf height data includes broad leaf-cluster variation and subtle vein relief. A stronger foliage normal map is assigned for URP lighting.

Compatibility: Unity 6, URP, WebGL-friendly generated textures/materials. This is source-level validation only; compile and regenerate inside the target Unity project to confirm scene-specific behavior.
