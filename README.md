# Unity.ProjectVania

A Unity 6 rebuild of *ProjectVania*, a 2009 XNA 3.0 (C#) 2D side-scrolling platformer prototype: a
Castlevania-flavored character (sprites named Morris, Shanoa, Leo and Richter) that runs, jumps,
kneels, slides and wall-slides across a 16-pixel tile grid in front of a painted mountain-farmhouse
backdrop, with a loose-follow camera, a slide-down typed console, a tile-painting map editor and a
main menu, all wrapped in a hand-built component "Engine".

[![Unity](https://img.shields.io/badge/Unity-6000.4.3f1-black)](ProjectSettings/ProjectVersion.txt) [![Status](https://img.shields.io/badge/status-reconstruction-orange)](docs/BIBLE.md)

There is no build yet. This repo currently holds the Unity project scaffold and the reverse-
engineering record the rebuild is built from.

## Why

The original is an engine-first sandbox from 2009 whose platforming feel is worth keeping exactly:

- **Per-frame, hand-tuned platforming.** Acceleration 14000, a 200 px/s run cap, ground and air drag
  of 0.58 and 0.65 per frame, a fixed 0.6 s jump curve that rises about 76 px and that releasing the key does not shorten, a wall slide that caps
  at 83 px/s, a kneel on a hard landing and a slide out of a run.
- **A grid you can paint while you play.** One key flips the running game into a map editor: a tile
  palette, a cursor that scrolls the camera, left click to place a solid tile, right click to erase,
  and the player can be dragged around with the mouse.
- **A console that is part of the game.** A tilde-key console slides down over the world and runs
  `say`, `yell`, `fade`, `play music`, `mute`, `console text red` and friends.
- **Everything is a swappable component.** Texture, font, effect, audio, input, camera, scene, GUI,
  script and map-editor managers behind interfaces, handed to every object in one bundle.

See [`docs/RECONSTRUCTION.md`](docs/RECONSTRUCTION.md) for the full system-by-system record read from
the original C# source and content files, and [`docs/BIBLE.md`](docs/BIBLE.md) for the rebuild's
brief. Work is tracked as stories under epic `U.VAN-A` in
[`docs/USER_STORIES.md`](docs/USER_STORIES.md), written in build order.

## Status

Reconstruction written, implementation not yet started. The stories, in build order:

- Engine-free Core, a fixed 60 Hz simulation and the command/event model
- Data-driven tuning, content and settings registry
- Project hygiene: URP, Input System, Unity.Shared, code-built scenes
- Input bindings (keyboard and Xbox pad) per game state
- The 16 px collision grid and its queries
- Player physics and action state machine
- Player animation, characters and rendering
- Map, backdrop and tile rendering
- Camera (loose, locked, cursor, free; scroll, zoom, rotate)
- Game states and the main menu
- The typed console and its script language
- HUD, message stack, fade lens and debug readout
- Audio cues, music and mute
- Map editor
- Post-process effects
- Everything the original left unwired, kept as data
- Asset parity sweep and the end-to-end scripted verification run

### Muted launches

`play.ps1` launches muted: it sets `MINDATTIC_CLI_MUTED=1` and passes `-muted`, so a CLI or agent starting
the game never blasts audio. Pass `-Sound` to launch with audio, `-Build` to rebuild first (`.\unity.ps1
build`), `-Wait` to wait for the player to exit. Either switch works on its own when you start
`ProjectVania.exe` (or the editor) yourself:

```
$env:MINDATTIC_CLI_MUTED = '1'; .\Build\ProjectVania\ProjectVania.exe
.\Build\ProjectVania\ProjectVania.exe -muted
```

The mute is the shared `com.mindattic.unity.shared` session mute: read before the first scene loads, it
makes the player profile's settings report music and SFX muted for that run only, and `SessionAudio`
silences the listener (the cue and song players follow audio.json and the console's `mute`, which still
works underneath). The player's saved `settings.json` is never written, so the next normal launch has
sound. The `-batchmode` runs in `unity.ps1` (compile, test, build, capture) are not playable launches and
are unaffected.

## Original source

`R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\ProjectVania` (the fuller copy; its `Engine`
project is the game) and `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\XNA 2D\ProjectVania`
(an earlier, smaller copy). Read-only reference, never edited by this project.
