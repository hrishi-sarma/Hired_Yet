STYLIZED WORLD + DOOR-FACING FIX
Unity 6 / URP / WebGL

WHAT CHANGED
1. ModelFitter.cs
   - Finds a child named `door` and prioritizes it over door_frame.
   - Uses the door's actual Renderer bounds, not its Transform pivot, to infer which facade contains the entrance.
   - Corrects the model's local yaw so the door-bearing facade faces local +Z. The building lot yaw then turns that facade toward the sidewalk on either side of the street.
   - Models without a recognized door retain the prior random-rotation fallback.

2. StylizedMaterialFactory.cs
   - Materials are deliberately matte with almost no specular shine.
   - Generated textures have stronger, stepped, hand-painted patterns for plaster, roofs, wood, asphalt, concrete, ground, rock, snow and glass.
   - Existing 20 architectural palette setup is retained.

3. TerrainApplyStage.cs
   - Terrain textures use stronger color breakup and quantized tonal variation.
   - Editor regeneration refreshes the generated terrain-layer textures so the previous generated flat/mild textures are not reused.

4. StylizedWorldSetup.cs (new)
   - Added automatically by WorldGenerator when a generation run starts.
   - Sets a stylized ambient-light palette, warm directional lighting, soft medium-resolution shadows with a limited shadow distance, subtle distance fog, and lightweight URP color grading/neutral tonemapping.
   - Uses a subtle vignette and very low bloom. No outline effect or expensive screen-space effects are required.
   - A Context Menu action, Apply Stylized World Look Now, is also available on the component.

INSTALL
- Back up Assets/Scripts/World.
- Copy the updated .cs files into your existing world scripts folder, preserving existing .meta files.
- Add StylizedWorldSetup.cs if copying files individually. WorldGenerator adds it automatically when generating.
- Regenerate the world in the Editor to refresh generated terrain layers and building materials.
- Test the WebGL build on the target browser; quality/performance can vary by device.

NOTES
- Door orientation is inferred from the visible renderer bounds. If the door mesh is centered inside the overall building bounds (not located on an exterior face), automatic detection cannot infer a reliable front and will default to the prefab's +Z side.
- Stylization is intentionally lightweight for WebGL. This does not create custom normal maps or true toon outlines; it uses procedural diffuse textures, matte URP Lit materials, lighting, fog and color grading.
- URP post-processing must be enabled for the active URP renderer/camera for the volume color grading to show.

COMPILATION FIX (Unity 6 URP compatibility)
- Fully qualifies UnityEngine.ShadowQuality and UnityEngine.ShadowResolution to avoid namespace ambiguity with URP types.
- Removes the Vignette.roundness assignment because that property is not exposed by the URP package version used by this project. Vignette intensity and smoothness remain configured.

UV / TEXTURE SCALE FIX
----------------------
StylizedUVMapper.cs is included and is called automatically whenever stylized materials are assigned to generated mesh renderers. It creates a private mesh copy and remaps UVs with dominant-normal planar projection measured in approximate world metres. This keeps primitive road/sidewalk cubes and scaled prefab meshes from stretching a full texture across large surfaces. Material tiling is set per stylized material role (asphalt, concrete, plaster, roof, wood, rock, etc.) to keep texture features at a consistent physical size. Original imported meshes are not edited in-place. Terrain uses TerrainLayer.tileSize and remains handled by TerrainApplyStage.

For arbitrary organic/curved models, dominant-axis projection is a practical procedural fallback rather than a replacement for hand-authored UV unwraps; it may create seams on complex meshes. Verify representative prefabs and the WebGL build after regeneration.


UV MAPPING READ/WRITE SAFETY FIX
- StylizedUVMapper now checks Mesh.isReadable before accessing vertices/normals and skips UV remapping for imported meshes whose Read/Write option is disabled.
- Automatic roof/wall geometry splitting also skips non-readable meshes instead of calling GetTriangles/GetVertices/GetNormals and throwing errors. Existing materials remain assigned.
- If you need custom UV remapping or automatic roof/wall splitting on a particular imported model, select its model asset in Unity and enable Model Import Settings > Read/Write, then Apply. This increases runtime mesh memory, so enable it only for meshes that need it.


Latest texture continuity polish (Oct 2026):
- StylizedUVMapper now projects UVs from world-space positions, so adjacent sidewalk/curb/ground-base primitives share a continuous texture phase instead of restarting at every object pivot.
- Asphalt texture generation and asphalt physical tile size are unchanged.
- Concrete no longer has a regular square grid that reads as repeated wallpaper; it uses irregular fine aggregate instead.
- Non-asphalt material textures use 256px tiles, while terrain layer textures are generated at 512px. Terrain tiling is enlarged to reduce obvious repetition.
- House materials are double-sided (Cull Off); light walls use darker frames.
- Blue window glass has stylized diagonal cartoon reflection patches. Procedural foliage uses a mottled leaf-like material.

- Procedural brown tree trunks/fallen timber now receive a restrained wood/bark grain; green tree crowns and shrubs use a mottled leaf-like foliage texture.
- House glass gets diagonal stylized reflection patches, and light wall colors always receive darker frames.
- House material variants set URP culling off to keep both sides visible.
- Asphalt renderers are excluded from UV remapping to preserve the road texture exactly as it looked before this update.
