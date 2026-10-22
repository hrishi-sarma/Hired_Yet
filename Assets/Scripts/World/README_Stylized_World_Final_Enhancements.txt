Stylized world enhancements (Unity 6 / URP / WebGL)

Included changes:
- Improved tileable lawn texture with larger, domain-warped painted clumps and reduced obvious checker repetition.
- Smaller, lower-contrast concrete pores and shallower concrete height relief.
- More legible staggered roof shingle/ridge texture and smaller roof texture scale.
- Two diagonal cartoon reflection streaks on shared glass textures, applied to windows of every size.
- Short overlapping highway barrier segments to reduce gaps around path joins.
- Continuous sampled suburb/city perimeter rails, mesh-combined for fewer renderers; openings are left where local roads or the highway meet the district boundary.
- Per-house deterministic random scatter of small stones, gravel and bushes around residential lot sides/rear/front corners, with the central doorway kept clear. Garden decorations are mesh-combined for WebGL and have no colliders.

Install by backing up Assets/Scripts/World, extracting the archive, replacing corresponding scripts, preserving existing .meta files, then recompiling and regenerating the world.

Validation note: archive integrity and source delimiter checks were performed; the project was not compiled in Unity. Inspect perimeter alignment and house-yard clearance in the scene before shipping a WebGL build.
