Grass/lawn, concrete pore, terrain normal, and rock dressing update

Changes in this revision:
- Lawn/lot-pad texture tile size is 0.20 m (four times larger than the previous 0.05 m tile size, so the pattern is less extreme).
- Concrete albedo pores are smaller and less contrasty; concrete height relief and AO strength are reduced.
- Generated terrain layers now get a height-derived normal map. On the grass layer, bright flower heads and blade highlights contribute extra normal relief so they read as slightly raised. This is shading relief, not actual added geometry.
- Forest rocks use the stylized rock material with generated normal, height, and AO maps. Their primitive sphere meshes are subtly deformed for more irregular silhouettes.
- Additional roadside rocks are placed 18.5-29 m from the generated highway centerline, outside the normal road-clearance zone, subject to zone, slope, and occupancy checks. Existing forest rocks remain.
- Decorative rock meshes are still combined into material batches for WebGL draw-call efficiency.

Installation: replace the matching scripts under Assets/Scripts/World, preserve existing .meta files, let Unity compile, then regenerate the world/terrain. In the Editor, TerrainApplyStage regenerates the generated TerrainLayer assets and their normal maps.

No external images or normal maps are required. TerrainLayer uses a normal map for raised-looking shading; it does not have a universal height/parallax slot, so flower pop-out is lighting relief rather than physical geometry. Validate the result in the target Unity 6 URP project before shipping.
