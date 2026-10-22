Stylized World Update — Cartoon Grass, House Texture Variation, Fence Contrast

Changes in this update:
- Terrain grass uses a painterly cartoon treatment with soft clumps, directional blade-like strokes, and sparse tiny cream/blush flowers inspired by the supplied references.
- Grass terrain tiling is reduced from 22m to 16m per texture tile to keep the blade pattern legible at normal play distances.
- Terrain textures blend their borders before mipmaps are generated; asphalt and sidewalk generation/mapping are unchanged.
- House plaster textures use palette-specific generated variants, so different house palettes do not share the exact same painted patch pattern.
- House UV mapping rotates in deterministic quarter-turns per house and increases plaster UV density for smaller, more visible wall texture detail.
- Fences/pickets use ivory-white material for darker facades and dark brown for lighter facades, when the fence part is named with 'fence' or 'picket'.

Install:
1. Back up Assets/Scripts/World.
2. Replace the matching source scripts, keeping existing .meta files.
3. Allow Unity to compile and regenerate the world in the Editor to refresh generated TerrainLayer assets.

Notes:
- The asphalt texture and asphalt UV mapper exclusion are unchanged.
- Per-house wall variation is deterministic from the prefab/lot seed, so the same generated world stays reproducible.
- Complex imported meshes with Read/Write disabled still skip custom UV remapping, as in the prior fix.
- This source collection has not been compiled or visually tested inside the user's Unity project.
