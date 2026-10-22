Unity 6 URP / WebGL road and district enhancement update (marking/barrier fixes)

Fixes in this revision:
- Local lane dashes are restricted to mid-block road sections, leaving a clearance near intersections.
- Zebra crossings are disabled entirely. Only dashed lane-split markings are generated on local roads and highways.
- Highway guardrails are generated as connected rail segments between consecutive highway samples, with consistent offsets and endpoint posts. This replaces independently sized rails that could leave large gaps or protrude past their intended segment.
- Highway lane dashes remain within the road surface.
- Existing traffic lights, street signs, district/building generation and pond-free setup are preserved.

Install:
1. Back up Assets/Scripts/World.
2. Replace the corresponding C# files from this archive, preserving your Unity .meta files and scene component assignments.
3. Let Unity compile, regenerate the world, then inspect intersections and highway curves.
4. Test in the target WebGL browser.

This is a source-level fix and has not been compiled or profiled inside the user's Unity project. Visual placement still depends on the configured road widths and highway sampling spacing.


BUILDING DOOR ORIENTATION
-------------------------
Building prefabs can contain a child GameObject tagged exactly "Door". The generator detects that child and rotates the prefab so the face containing the door points toward the lot frontage/sidewalk. Door-tagged models do not use random 90-degree rotation. Ensure the Door child is positioned on the exterior face of the prefab; its dominant local X/Z offset from the prefab root identifies the face. Prefabs without a tagged Door retain their existing rotation behavior.


STYLIZED URP / WEBGL MATERIALS
------------------------------
This collection includes StylizedMaterialFactory.cs and StylizedMeshCleanup.cs.
It generates and shares small procedural grayscale textures at runtime, then colors
them per material role. No external textures or normal maps are required.

House prefab parts are recognized by child names such as:
  house, wall, roof, door, door_frame, window, window_frame, platform, facade.
Small houses use one of eight muted palettes and always use a charcoal-black roof.
Larger houses use additional muted palettes. Window glass is blue-tinted, doors are
dark wood, and frames alternate between off-white and charcoal. Palette selection
uses the prefab name plus placement coordinates for deterministic variation.

When a house/building mesh combines roof and wall geometry into a single mesh called
"house", the system attempts to split triangles into wall/roof submeshes based on
triangle height and normals. This is a heuristic; unusual roof meshes may need the
roof and wall as separate named children for perfect control.

Generated local roads and sidewalks use stylized asphalt and concrete materials;
highway meshes use stylized asphalt. Terrain grass, dirt, rock, and snow use generated
noise/pattern textures with matte TerrainLayer settings. Shared textures/materials
keep memory use low for URP WebGL. No generated normal maps are required.

After replacing scripts, regenerate the world. Existing generated objects retain their
old materials until regenerated. Test in a WebGL build, since browser GPU cost varies.
