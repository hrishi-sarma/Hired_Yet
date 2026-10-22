ASPHALT UV, POROUS WALLS, HEIGHT/NORMAL/AO AND MICRO-GRASS UPDATE

Changes in this update:
- Asphalt renderers are no longer excluded from StylizedUVMapper. Primitive and readable road meshes now use continuous world-metre planar UVs, so the asphalt texture no longer stretches over each whole road segment.
- Asphalt material scale is now 0.85 metres per texture repeat, with a 512px procedural aggregate/binder texture, fine mineral aggregate, small pores and darker binder variation.
- Procedural height, normal and ambient-occlusion maps are generated per material role. Asphalt uses stronger, finer aggregate/pore relief; plaster/concrete use smaller porous cavities and softened concrete relief. Maps are attached when the active shader exposes the corresponding URP properties.
- Height maps are generated at runtime and kept readable until normal/AO generation completes. Generated textures are shared and cleared with the material factory.
- Terrain grass layer tiling was increased by 1000x in frequency (tile size 12m -> 0.012m) and its blade/clump frequencies were raised to produce a fine grass-like micro-pattern. This is intentionally extremely fine and can be mip-filtered at distance.

Notes:
- URP/Lit supports normal and occlusion maps, but does not expose a parallax height property in every configuration. The height map is used to derive the normal and AO maps and is assigned as a parallax map only when the selected shader exposes that property.
- Asphalt mapping requires a readable mesh. Generated primitive road meshes are readable; imported road meshes with Read/Write disabled are safely skipped by the mapper.
- This source collection has not been compiled in the target Unity project. Check the Console and inspect road joints, walls, and terrain after regeneration.
