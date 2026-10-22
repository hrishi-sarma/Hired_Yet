# Unified Sound System

## Folder inference
Assets/Audio/<Category>/<Group>/<file>. Category = first folder, group = second folder, tags = file name split on `_ - space .` (lowercase, digits dropped) plus the full name.
Assets/Audio/Guitar is never scanned or modified.

| Folder | Meaning | Name tags used |
|---|---|---|
| Ambient/City, Forest, Suburb | zone beds | `day`, `night`, `park` (city_park for the city park). Suburb has one file for both |
| Animals/Birds, Bunnies, Cats, Deers, Dogs | species calls | `react`/`alert`/`happy` = reaction, anything else = ambient call |
| Cars/Engines | idle loops | `idle`; accel/decel generated from it |
| Cars/Horns | horn clips | any |
| NPCs | voices | `event` = event interaction, `alien` = alien event, `npc` = general voice |

Zone mapping: City -> City, Suburb -> Suburb, Forest/Mountain/Open -> Forest, Highway -> City bed at low volume.

## Stages
1. Core (done)
   - SoundCatalog: folder-inferred clip catalog, tag queries, no-repeat pick
   - SoundPool: capped voice pool with priority stealing and distance culling
   - AudioDirector: auto-boots, tracks player, zone and night factor, category volumes
   - Editor SoundCatalogBuilder: rebuilds catalog on audio import, sets import settings (ambient streaming, cars decompress, rest compressed, mono for 3D, no preload)
2. Ambient zones (done)
   - One looping 2D source per active zone bed (max 2 live), day/night pick by NightFactor
   - Gradual crossfade in/out on zone change and day/night change, source stopped at zero volume
   - City park bed fades in near the park only (park found once via the CityPlaza object, city bed ducked 60% while in it)
   - Slow volume swell, random start offset, no repeat of the last clip, silent layers are stopped and release the clip
   - Highway uses the city bed at 30%
   - Tunable fields on AmbientZones: fadeIn, fadeOut, bedVolume, parkVolume, parkFadeDistance, parkDuck, highwayCityWeight
3. Animals
   - Fill existing AnimalSoundSet from the catalog by species
   - Burst logic: 2-5 calls spaced 0.4-1.2s then cooldown, only when the animal is within range
   - Birds routed into AmbientFlyers
4. Cars
   - Runtime-generated accel and decel variants from the idle clip (resample + gain), cached
   - Engine voice only for cars within range, horns fed into HornBank.honkClips
5. NPCs and events
   - Per-NPC pitch variants from the NPC clip
   - `event` clip -> event interaction, `alien` clip -> alien_tourist event, wired into EventAudio and WorldEvent sound slots
6. Optimization pass
   - Zone and distance gating audit, voice budget tuning, memory check

## Stage 1 setup
1. Copy the Audio folder into Scripts.
2. Run Tools > Audio > Apply Import Settings once, then Tools > Audio > Rebuild Sound Catalog.
3. Assets/Resources/SoundCatalog.asset is created. Rebuilds are automatic afterwards.
4. Play: an AudioDirector object appears. Use AudioDirector.PlayAt / Play2D for sounds.

## Stage 2 setup
1. Name files so tags match: `*_day`, `*_night` in Ambient/City and Ambient/Forest, any single name in Ambient/Suburb, `city_park` in Ambient/City.
2. Run Tools > Audio > Rebuild Sound Catalog, then play. AmbientZones is added by AudioDirector automatically.

## Player, interviewer and greeting sounds (generated in code, no audio files needed)
Clips are synthesised at runtime by ProceduralSfx and cached. Any real file dropped into the matching catalog folder replaces the generated clip for that slot.

7. Player footsteps (done)
   - FootstepSounds (added by AudioDirector) listens to FPLegsAnimator.FootDown (fires per foot, intensity = walk/sprint speed)
   - Downward raycast from the player, surface from collider + 2 parent names + material name, cached per collider (Terrain -> by zone)
   - Surfaces: Concrete, Asphalt, Grass, Dirt, Wood, Indoor, Metal, Water; 4 generated variants each, no-repeat pick, pitch jitter, L/R pitch offset
   - Volume scales with speed (sprint louder, crouch quieter)
   - Override: Assets/Audio/Footsteps/<Surface>/*.wav
8. Interviewer reactions (done)
   - InterviewerSounds: formant synth (saw/noise through 2 resonators), one short vocalisation per FaceEmotion, 3 variants each, Neutral is silent
   - Friendly "mm-hm", Pleased "mmm", Delighted "ha ha ha", Suspicious low "hmmm", Doubt rising "hm?", Annoyed tut-tut + grunt, Angry growl, Bored long sigh, Sad "aww", Disappointed falling "aw-aww", Shock gasp + "ah!", Disgust "ugh", Smug "heh heh", Worried "uh-oh"
   - Hooks in InterviewerRig: Play(Act) (every gesture reaction, Idle only 25%), SetSuspicion (annoyed/angry 60%, pleased 50%)
   - One voice at a time, 0.6 s cooldown; pass/fail outcomes interrupt and always play
   - 3D at the head bone, pitch per Mood (Impatient +8%, Paranoid +15%, Bored -8%), category "interview"
   - Override: Assets/Audio/Interviewer/<Emotion>/*.wav
9. Greeting and preliminary sounds
   - Door open and chair, hello / greeting vocal on GreetAndOffer, pass and fail jingles on SetOutcome
   - Played through AudioDirector.PlayAt, category "interview"

## Stage 7 setup
1. Copy Audio/ProceduralSfx.cs, FootstepSounds.cs, SoundCatalog.cs, AudioDirector.cs over the existing ones.
2. Play and walk. Surface changes road -> sidewalk -> grass etc. automatically. Right-click the AudioDirector object for stats.
3. If a surface sounds wrong, rename the floor/ground object or material to contain one of: water, metal, wood, road, asphalt, sidewalk, concrete, grass, dirt, path, floor, carpet.
4. Tunables on FootstepSounds: volume, pitchJitter, variants. Category volume: AudioDirector.SetVolume("footsteps", x).

## Stage 8 setup
1. Copy Audio/InterviewerSounds.cs, Audio/implementation.md and interview/InterviewerRig.cs over the existing ones.
2. Play an interview: every reaction (correct, wrong, hedge, no answer, pressure, warn, pass, fail, suspicion change) now makes its emotion sound.
3. Tune: edit the Seq() table in InterviewerSounds (pitch, duration, breathiness, roughness per syllable). Volume: AudioDirector.SetVolume("interview", x).
