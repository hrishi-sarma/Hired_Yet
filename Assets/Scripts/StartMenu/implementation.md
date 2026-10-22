# Start Menu Overhaul — Staged Implementation

## Decisions (final)
- **Separate scene `StartMenu`**, later loaded additively by the resume scene and unloaded on START (zero cost during resume writing; desk room untouched).
- **World is generated at runtime with a fixed seed** (same view every launch). Reason: cars/NPCs need `WorldContext` (lane graph, zones, heights) which only exists after `WorldGenerator` runs — a baked, hand-generated scene can't drive `NpcDirector`. Generation happens behind the existing `LoadingScreen` once per launch.
- **Everything is auto-built** by `Tools > Start Menu > Build StartMenu Scene`: it clones the generator rig (all stages, director, lights, sky, volumes, RenderSettings) from the game's world scene (`WorldLauncher.WorldSceneName`), swaps `WorldBootstrap` → `MenuWorldBootstrap`, and adds the camera rig.
- Traffic focus point = ground point between suburb and city (not the camera), so the initial population spawns **inside the shot**. All menu agents are simulated every frame (large `farSimRadius`) → smooth.

## Stages
| # | Stage | Status |
|---|---|---|
| 1 | Backdrop scene tooling, camera rig (auto-frame + sway + parallax), smooth cars | **DONE (this delivery)** |
| 2 | Menu UI restyle (`PostItMenu` → text-only list, same font, outline/shadow, hover) | **DONE** |
| 3 | Integration: START flow (fade → resume scene → eye-blink once → slots), Discard returns | **DONE** |
| 4A | **Bake the world into the StartMenu scene** (edit-mode generation, saved scene, no runtime generation, strip clutter) | **DONE** |
| 4B | Pre-placed roaming agents: 6 cars (highway + suburb loops), 10 NPCs, 3 dogs, 3 cats in the suburbs; never despawn; city empty | **DONE** |
| 4C | Flyers: 5 butterflies, 10 birds; night fireflies kept | **DONE** |
| 4D | Audio: only day bird sounds + night forest sounds; everything else muted | **DONE** |
| 4E | Performance + polish (static flags, shadows/LOD/culling, pre-applied shadow pass, text gradient, shorter load) | |

## Stage 1 — what was done

### Files (copy into `Assets/Scripts/`, keep folder structure)
| File | Purpose |
|---|---|
| `StartMenu/MenuCameraRig.cs` | Auto-frames camera, ambient sway, mouse parallax (new Input System aware) |
| `StartMenu/MenuWorldBootstrap.cs` | Fixed-seed generation, director start, camera framing, menu traffic tuning, `Ready` event |
| `StartMenu/Editor/StartMenuSceneBuilder.cs` | Menu tool that builds the scene; also "Snap Rig To Scene View" |
| `AI/Car.cs` (replaces yours; diff in `AI/Car.patch`) | Smooth far-LOD motion |

### Smooth cars (Car.cs, 2 small edits)
Cause: cars beyond `farSimRadius` only run logic every `farTick` (0.2 s) and `Pose(far…)` **snapped** position/yaw at each tick → visible stepping.
Fix: between ticks `FarGlide()` slides the car along its path at current speed (cheap `path.Sample`), and yaw is always rotated smoothly (`Pose(false, Time.deltaTime)`). AI cost is unchanged (logic still ticks at `farTick`); only the visual pose is per-frame. Menu also sets `farSimRadius = 450` so everything in shot is fully simulated.
> Pedestrians have a similar far tick if you see stepping there; same pattern applies (say so and I'll patch).

### Camera framing (automatic)
1. Samples the zone map: **mountain** cells near the suburb, **forest** cells around the suburb–city midpoint.
2. Camera sits **behind the suburb toward the mountains** (`behindSuburbRadii`), `heightAboveGround` above terrain (ridge-checked so it never clips), looks at a point `lookAtCityBlend` of the way from suburb → city.
3. **FOV auto-fits** suburb edges, city far edge/sides (+building height) and the forest centroid, with `fitMargin`; refits on aspect change.
4. Inspector tweaks on `Menu Camera Rig`: height, behind distance, look blend, margin, FOV limits, `manualOffset / manualEuler / fovOffset`, parallax/sway strengths.
5. Want full manual? Move Scene view where you like → `Tools > Start Menu > Snap Rig To Scene View` (turns `autoFrame` off).

### How to use
1. Create empty scene `StartMenu` (File > New Scene), save it, add it to Build Settings.
2. `Tools > Start Menu > Build StartMenu Scene`.
3. Press Play: loading screen → world → traffic runs, camera sways, mouse parallax.
4. Tune the rig / seed (`MenuWorldBootstrap.fixedSeed`) until the framing is right.

### Notes / assumptions
- Source scene is found by `WorldLauncher.WorldSceneName` (`worldtest1`). Roots with Camera/AudioListener/EventSystem/Player tag are not copied; the clone's pre-generated "World" child is dropped (regenerated at play).
- Added a Menu Camera tagged `MainCamera` with the `AudioListener` (NpcDirector falls back to `Camera.main`). In Stage 3 the desk camera is disabled while this scene is loaded, so only one listener is active.
- Uses URP/`WorldShadowFix` exactly like `WorldBootstrap`. Untested in Unity here — if the builder reports "No WorldGenerator found", check the scene name.

## Stage 2 — UI (done)
Only `resumegen/ResumeMenu.cs` changed: `PostItMenu.Init` now builds `MenuItemAnim` text entries (new class, same file); `MakeItem()` added; `notes` list type changed. Heading text: `PostItMenu.Title` ("" hides it). Layout constants `ItemX/FirstY/Spacing/TitleY` at the top of the layout block. Panels keep the post-it look.

### Original plan
`PostItMenu.Init` keeps its signature. Left-aligned text list (START, HISTORY, SETTINGS, CREDITS, QUIT), the **same `handwritingFont` reference**, `Outline`+`Shadow` borders, invisible hit areas (no rectangles), hover = red + slide, fade in/out. Panels unchanged.

## Stage 3 — integration
`ResumeFormUI`: stop creating `DrowsyView` at load; desk hidden + desk cam off while `StartMenu` is additive-loaded; START → fade black → unload StartMenu → create `DrowsyView` and `Wake(2.4)` once → `BuildSlots`. `Discard()` reloads the backdrop.

## Stage 4 — performance
Mute car/NPC audio in menu, shadow distance ≈ view depth, `lodBias` 0.6 while in menu (restored on START), optional 30 fps cap, cull distances for small props, readable-text gradient on the left.

## Stage 3 — integration (done)
**Design change vs. the first plan:** instead of loading StartMenu additively inside the resume scene, **StartMenu is the entry scene** and START loads the resume scene (single load). Reason: StartMenu already owns its menu UI, camera, listener and director; a single-scene load unloads the whole world automatically (zero cost while writing), and nothing in the desk room needs hiding/toggling. Trade-off: returning to the menu (Discard, or quitting from a game) reloads StartMenu, which regenerates the fixed-seed world behind the loading screen.

Flow: START → menu fades out, screen fades to black (0.7 s) → resume scene loads → `ResumeFormUI.Start` sees `StartMenuUI.EnteredFromMenu`, skips the post-it menu, runs `StartGame()` immediately → eyes open once (2.4 s) as the slots appear. Discard → `SceneManager.LoadScene(SaveSystem.MenuScene)` (= StartMenu).
Changes: `ResumeFormUI.cs` (3 small edits: fromMenu flag, early `StartGame()`, Discard return), `StartMenuUI.cs` (sets SaveSystem state, fade + load), builder adds StartMenu (index 0) and the resume scene to Build Settings. If the resume scene is opened directly, the old menu still works (legacy path).

## Stage 4 — pre-baked menu world (plan)
**Why split:** the world generator already supports edit-mode generation (it saves terrain data/textures as assets), so geometry can live in the saved scene. But cars/NPCs/animals are driven by `NpcDirector`, which needs the runtime-only `WorldContext` (lane graph, zones). A scene with a fixed, never-despawning cast therefore needs a different driver: **paths recorded at bake time** (from the lane/pedestrian graphs while the context still exists) and **lightweight followers** saved as scene objects. That is 4B; 4A only bakes the geometry.

### Stage 4A (done) — files
- `StartMenu/Editor/StartMenuBaker.cs` — `Tools > Start Menu > Bake World (generate in scene)` and `Clear Baked World`.
- `StartMenu/MenuWorldBootstrap.cs` — new `useBakedWorld` flag: at Play it skips generation and the director; only the short shadow pass runs.

**What Bake does:** removes unused gameplay roots copied from the game scene (inventory/item objects, stray Main Camera), removes `NpcDirector`, disables `InterviewDoorStage` (no office/door indicator), generates the world at the fixed seed in edit mode (progress bar), freezes the auto-framed camera pose + FOV into `Menu Camera Rig` (auto-frame off, you can move the rig freely), sets `useBakedWorld`, saves the scene.
**Use:** open StartMenu → (run `Build StartMenu Scene` once if not done) → `Bake World` → look at the Scene view → Play. No generation at Play, no traffic yet (arrives in 4B).
**Caveats:** (1) scene file gets large (generated meshes are embedded) — expected; (2) systems that relied on the runtime context (street lamps at night, window lights, traffic lights) may need follow-up — report anything missing after the bake and I'll patch it in 4E; (3) re-bake any time (it regenerates cleanly); the seed is `MenuWorldBootstrap.fixedSeed`.

### 4B plan — roaming cast (baked)
Bake step records closed loops: cars on highway + suburb lanes only, pedestrians on suburb sidewalks, dogs/cats on suburb ground; city and forest stay empty. Spawns the fixed counts (6 cars, 10 NPCs, 3 dogs, 3 cats) as scene objects using your existing models/painters, each with a tiny `MenuPathFollower` (smooth per-frame motion, no AI, no LOD ticks, never destroyed).
### 4C plan — flyers
5 butterflies + 10 birds (reuse `FlyerAgent`/`AmbientLifeController` with fixed counts and a suburb habitat box); keep the night fireflies.
### 4D plan — audio
Keep bird sounds (day) and night forest ambience; strip car/footstep/animal/NPC/horn sources from the menu scene.
### 4E plan — performance/polish
Static flags + batching, shadow distance ≈ view depth, `lodBias`, cull distances, optional occlusion bake, move the shadow pass into the bake, left-side text gradient.

## Stage 4B (done) — roaming cast
**Use:** just run `Tools > Start Menu > Bake World (generate in scene)` again (it now also bakes the cast; the world regenerates identically from the seed). `Clear Roaming Cast` removes the `Menu Cast` root. Counts are constants at the top of `StartMenuCastBaker` (`Cars, HighwayCars, Npcs, Dogs, Cats`).
**How:** while the live `WorldContext` exists, the baker builds the lane/sidewalk graphs, finds closed loops (DFS cycles) nearest the shot focus: cars on suburb lanes (distinct loops) + 2 on the highway, NPCs on suburb sidewalks (half walk the reverse direction), dogs/cats on the opposite sidewalk lane. Models come from the game's own `AgentPool.Build` (your NPC profiles, car models, painter), then AI components/physics/audio are stripped and a `MenuPathFollower` is added (smooth per-frame motion, NpcAnimator clips for NPCs, procedural leg swing for animals, cats stop & go). Nothing despawns, no director runs.
**Notes:** NPC/car settings are copied from the `NpcDirector` (disabled, kept on the generator; if missing it is read from the game world scene). If no highway cycle exists the highway cars follow an open chain and wrap to its start (keep it off-screen). Animal leg swing assumes hip pivots rotate about local X; tell me if legs look wrong. Car lights/horns not baked (4D/4E).
**Edited existing files:** `AI/AgentPool.cs` (`BuildForBake`, edit-mode-safe destroy), `AI/NpcDirector.cs` (`PrepareForBake`), `Animal_AI/AnimalModelFactory.cs` (edit-mode-safe destroy), `StartMenu/Editor/StartMenuBaker.cs` (keeps director disabled, calls cast bake).

## Stage 4C (done) — flyers
Baked together with the cast (`Bake World`). `StartMenuCastBaker.BakeFlyers` builds 5 butterflies + 10 birds with the game's `FlyerModelFactory` (scaled x1.6 via `FlyerScale`) under `Menu Cast/Flyers`, each driven by `StartMenu/MenuFlyer.cs`: butterflies wander on looping Lissajous paths 2-3 m above suburb sidewalks; birds soar in circles (35-80 m radius, 14-28 m up) with flap bursts/glides and banking. No AI/director. Night fireflies (AmbientLifeController) untouched. Wing flap assumes shoulder pivots roll about local Z; report if wings look wrong.

## Night fix (after 4C)
Cause: window/lamp glow materials and street-lamp positions live in static runtime registries (`StylizedMaterialFactory.glowEntries`, `StreetLampLights.lampPos`) that a baked scene loses; shadow distance was 110 m (menu camera is far from the town); night fog was dense. Fix: `Bake World` now snapshots both registries into `Menu Night Data` (`MenuNightData.cs`), which re-registers them at Play, sets shadow distance 900 m / 4 cascades / 4096 map (restored on unload) and caps fog density (`maxFogDensity`). New small exports in `StylizedMaterialFactory` and `StreetLampLights`.

## Fog fix
The white mountains were the valley-fog mist layers: `ValleyFogDriver` kept its layers/materials in non-serialized `readonly` lists, so a baked scene lost them and the mist stayed frozen in its daytime (white) colour. The lists are now serialized (`World/ValleyFogStage.cs`); re-bake. `MenuNightData.maxFogDensity` now defaults to 0 (off) so the fog matches the game.

## Status summary (current)
| Stage | Status |
|---|---|
| 1, 2, 3, 4A, 4B, 4C | DONE |
| Night fix (glow/lamps/shadows) | DONE (re-bake required) |
| Fog fix (valley mist) | DONE (re-bake required) |
| 4D Audio | DONE |
| 4E Performance/polish | next |

**Workflow now:** copy files into `Assets/Scripts/` (keep folders) > open StartMenu > `Tools > Start Menu > Bake World` (bakes world, cast, flyers, night data) > Play.
**Menu rules:** no colour grading in the menu (`MenuNoGrade` disables all Volumes; lens flare stays). City has no props/trees (buildings, roads, ground remain). Runtime-only traffic-light objects are deleted at bake.

## Stage 4D (done) — audio
`StartMenu/MenuAudio.cs` (added automatically by `MenuWorldBootstrap`, no re-bake needed). Two looping 2D sources from the `SoundCatalog`: day = `Ambient/Forest` clip tagged `day` (falls back to `Animals/Birds` clips), night = `Ambient/Forest` clip tagged `night`; crossfaded by `StylizedMaterialFactory.CurrentNight`, fades in after loading, fades out on START (`MenuAudio.FadeOut`, called from `StartMenuUI.Leave`). Every other AudioSource in the StartMenu scene is muted (UI roots excepted); persistent game audio (AudioDirector pool, DontDestroyOnLoad) is never touched. The game's AudioDirector/AmbientZones stay idle in the menu because no NpcDirector/world context exists. Volumes: `dayVolume`, `nightVolume` on `MenuAudio`. Needs the sound catalog built (`Tools > Audio > Rebuild Sound Catalog`) and files named `*_day` / `*_night` in `Ambient/Forest`.
