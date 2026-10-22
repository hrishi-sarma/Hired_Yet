LAWN AND CONCRETE SURFACE UPDATE

- Lawn/lot-pad material tile size changed from 0.20 m to 0.80 m. This makes the visible grass clumps four times larger and less repetitive. The world Terrain grass layer and its tile size are not changed.
- Lawn procedural texture uses broad warped clumps, subdued mid/fine noise, and low-contrast blade-like strokes to avoid the checker/dot appearance.
- Concrete pores are smaller and shallower in both albedo and height generation. Concrete normal strength is reduced to 1.0 and AO strength to 0.34 to avoid the porous foam look.
- Asphalt and sidewalk geometry/mapping are not intentionally changed.

Install by replacing the corresponding scripts in Assets/Scripts/World and preserving existing .meta files. Regenerate the world/materials in Unity to see runtime-generated material changes. Validate in the Unity Editor and WebGL build.
