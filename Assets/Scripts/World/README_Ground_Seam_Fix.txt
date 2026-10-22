GROUND TILE SEAM FIX
====================

Changes in this revision:

1. Lot pads are explicitly assigned the shared "ground" role. Previously suburb lots could use a foliage texture while city lots could use a grey-tinted ground material, creating visible rectangular boundaries even where world-space UVs aligned.
2. All lot/grass-base pads use one canonical muted grass tint and the same cached material. Their UVs are projected from world coordinates so adjacent pads continue the same texture phase.
3. The procedural ground texture has a wider seamless edge blend, reducing visible repeat seams.
4. Terrain layer textures are edge-blended before mipmaps are generated, and their physical tile size is slightly larger to reduce obvious repetition.
5. Asphalt and concrete/sidewalk material generation and mapping are unchanged by this fix.

Installation:
- Back up Assets/Scripts/World.
- Replace the corresponding source files, keeping existing .meta files.
- Regenerate the world in the Unity Editor so generated TerrainLayer assets refresh.
- If old WorldTerrain/Layer assets are retained by your project, regenerate using TerrainApplyStage in the Editor; existing serialized TerrainLayer assets are refreshed by the current code path.

Note: this removes material-tint and UV-phase seams between adjacent generated lot pads. Large lighting/shadow differences from geometry or overlapping surfaces can still create a visible boundary; inspect any remaining line with scene lighting temporarily disabled to distinguish material seams from shadows.
