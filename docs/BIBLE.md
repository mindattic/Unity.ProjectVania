---
codex: 1
project: Unity.ProjectVania
layer: bible
status: living
updated: 2026-10-24
---

# ProjectVania — rebuild bible

The brief for the Unity rebuild. Every mechanic here is grounded in docs/RECONSTRUCTION.md: read
that first for "what the original actually did"; this file is "what we are building", free to make the
XNA-era implementation (a bundle of manager objects, per-frame constants, a hard-coded level) cleaner
without changing the feel. Section numbers below (§) refer to RECONSTRUCTION.

## Pitch

A 2D side-scrolling platformer sandbox in a Castlevania mould: one acrobatic character who runs,
jumps, kneels, slides and clings to walls on a 16-pixel tile grid in front of a painted mountain
farmhouse, with a hand-built toolbox around the game (a console that slides down and runs commands, a
map editor you can drop into at any moment, a main menu). It is an engine demo more than a game; the
rebuild's job is to be a faithful, clean, tested version of exactly that.

## Pillars

1. **The platforming feel is the product.** The numbers in §4.2 (acceleration 14000, run cap 200,
   drag 0.58 / 0.65 per frame, the fixed 0.6 s jump curve, the 83 px/s wall slide, gravity 10 per
   frame, terminal speed 500) are kept verbatim and stepped at a fixed 60 Hz, because the original
   tuned them per frame, not per second. See §4.
2. **The world is a grid you can repaint live.** A 118 x 75 grid of 16 px cells, hard cells block,
   and the editor changes the grid while the game runs. See §5 and §12.
3. **The toolbox is part of the game.** Console, editor, menu and camera keys are all reachable in the
   running game exactly as they were. See §2, §3, §9, §10.
4. **Components are swappable.** The original's best idea was a bundle of interfaces; the rebuild
   keeps the idea as an engine-free Core with services behind interfaces, composed in code.

## Systems (status tracked per-story in docs/USER_STORIES.md)

| System | Reconstruction ref | Story |
|---|---|---|
| Engine-free Core, 60 Hz tick, command/event model, player-id seam | §0, §1, §2 | U.VAN-US-A1 |
| Data-driven tuning, content and settings registry | §4.2, §5, §9, §13 | U.VAN-US-A2 |
| URP, Input System, Unity.Shared, code-built scenes, hygiene | §0 | U.VAN-US-A3 |
| Input bindings per game state (keyboard and Xbox pad) | §3 | U.VAN-US-A4 |
| The 16 px grid, the bridge level, collision queries | §5 | U.VAN-US-A5 |
| Player physics and action state machine | §4.2-4.7, §4.9 | U.VAN-US-A6 |
| Characters, animation, rendering, names, two players | §4.1, §4.8 | U.VAN-US-A7 |
| Backdrop, tile and outline rendering | §5, §6 | U.VAN-US-A8 |
| Camera modes, ease, bounds, scroll, zoom, rotate | §7 | U.VAN-US-A9 |
| Game states, main menu, fullscreen | §2, §9.2 | U.VAN-US-A10 |
| Console window, typing, script language | §10 | U.VAN-US-A11 |
| HUD, message stack, fade lens, debug readout | §9.1 | U.VAN-US-A12 |
| Audio banks, cues, songs, mute and pause | §11 | U.VAN-US-A13 |
| Map editor | §8, §12 | U.VAN-US-A14 |
| Post-process effects | §6.1 | U.VAN-US-A15 |
| Everything unwired in the original, kept as data | §5.1, §9.1, §15 | U.VAN-US-A16 |
| Asset parity sweep and live-asset diff test | §13 | U.VAN-US-A17 |
| End-to-end scripted verification | all | U.VAN-US-A18 |

## Decisions made (defaults; the owner may overrule any)

**Coordinates and scale.** Core keeps the original's pixels, y-down, positions at the feet, so the
original's arithmetic is copied rather than translated. Runtime converts at 16 px per Unity unit (one
grid cell is one unit), flips y, pivots sprites at the bottom centre, and renders a pixel-perfect 800 x 600
reference (the back buffer was never set in the original, §0; windowed, F12 and the menu toggle
fullscreen, the reference letterboxed on other aspects).

**Simulation.** Core steps at exactly 1/60 s with an accumulator; rendering interpolates. Every
frame-based constant (gravity, drag, the 1 alpha/frame fade) stays per step. Frame order is the
original's: input -> players -> camera -> GUI -> editor (§1). The animation clock advances when drawn,
as the original's did (§4.8).

**Two players.** Both are spawned at (500,100) and (550,100), both simulate, `[` / `]` / L1 / R1 cycle
who is driven and followed (§4). The idle twin is kept (the open question in §18.2 defaults to "as
original"); commands carry a player id so a second driver later is a one-line change, per the rebuild
standard.

**Defects of RECONSTRUCTION §15, decided.**

| # | Defect | Decision |
|---|---|---|
| 1 | Slide-jump leaves `isJumping` set: trapped, cannot fall | **Fix**: the slide jump starts a slide with `isJumping` clear, so ordinary ground checks apply |
| 2 | `UnitSize` stuck at (3,1) after a slide | **Fix**: restored to the character's size on leaving the slide |
| 3 | A kneel is not left by releasing S | **Fix** (owner decision 2026-10-23, built by U.VAN-T23): releasing S now stands the character up on the next idle step; a held S, or a jump taken from the kneel, still reads as a kneel that frame, so a slide jump is unaffected. `player.json`'s `kneel.releaseStandsUp` is the data slot |
| 4 | `step2` and `drag` cues restarted every frame | **Fix**: one `step2` per run-animation frame advance; `drag` restarted when its 0.26 s clip ends |
| 5 | Shift or pad A held blocks moving | **Fix**: neither blocks movement; Shift has no binding |
| 6 | Wall jump climbs before pushing away | **Fix** (owner decision 2026-10-23, built by U.VAN-T24): an active wall jump now kicks off a full-height wall every frame contact lasts, at `player.json`'s `wallJump.pushSpeed` (200, the run cap), instead of pinning the horizontal speed at 0 until contact happened to break on its own, which against a wall this tall it never did |
| 7 | Terminal-speed landing while moving leaves one frame of Falling on OnGround | **Keep**: A6's frame-replay tests assert it |
| 8 | Last name never chosen; `Other` gender returns null | **Fix**: every name is selectable; `Other` is not exposed |
| 9, 10 | `Tile(x, y)` mix-up draws the sheet 180 times; palette Y clamp off by one; right step unbounded; no bounds check on the edited cell | **Fix**: the palette draws each tile once at its place; clamp to the sheet in both axes; no painting or highlighting outside the grid |
| 11 | Short console commands crash | **Fix**: too few words prints a usage line; nothing throws |
| 11b | `console clear` spares `History[0]` | **Keep** (it is the echoed command line) |
| 12 | Sun cycle and ActionCross never run | **Keep inert** as data (U.VAN-US-A16) |
| 13 | `fade out` snaps clear when it finishes | **Fix**: it holds black until `fade in` or `fade off` |
| 14 | `icon_poisondart` loads the parry icon; `icon_consecrate` unloaded | **Fix** in the data: each icon is its own file |
| 15 | Unused library code (`Quad`, `LinkedList`, `TileGrid`, `Event`...) and files outside the project | **Drop**: no behaviour to keep |
| 16 | `[` / `]` leave the character switched away from with its `movement` held, so one switched away from mid-run runs on, undriven, for ever | **Fix** (U.VAN-T10): the character left behind lets go of its direction (`Cancel_Move`) as the switch happens, so the twin stands still as it was meant to |

**Audio.** Music never starts by itself, as in the original; only `play music` / `play battle` start it
(§11). The XACT banks become a cue table in data (name, clip, volume in dB, loop, category), and the
`music1` cue and unused footsteps are kept in the table, unplayed, as the original had them.

**Input.** The Input System supplies the devices; the action map is Core's (U.VAN-US-A4). Bindings are data
(`input.json`: every action's XNA-named keys and pad buttons, the console's typing chain), validated on load.
`InputRouter` is the original's `InputManager` dispatch: one handler per game state chosen by the state at the start
of the step, the original's `if` / `else if` chains and order, and its edge test **per control, not per action**
(holding S, a press of Down still moves the menu down), which is why the routing is Core code over raw control state
rather than Input System actions, whose press is per action. Input is read once per 60 Hz step, as the original read
it once per frame; the Runtime device latches a press that starts and ends between two steps, and queues wheel
notches one per step. Commands carry the driver's player id and are applied in issue order within the step, so Esc
and M in one frame open the menu and then toggle the editor from it, as the original's handler did. Wheel zoom,
`=`/`-` zoom, rotate, scroll, mode-cycle and editor-toggle keep their original steps and signs. Kept as the
original: pad A jumps on every step it is held (Space only on press); the middle mouse button toggles the editor in
every state, the menu and console included; the console types a key with no character as its lower-case name
("rightshift", "f12"); the pad's left trigger drives both rumble motors. Fixed: defect 5 (Shift or pad A no longer
block moving). The original's empty stubs (Q, W, J, K, L, 1-5, pad X/Y/B, the jump release) issue nothing. Until the
console's slide is built (A11), opening it enters the Console state at once rather than when the slide ends.

**Character physics (U.VAN-US-A6).** Core's `PlayerCharacter` is `Player.cs` frame for frame: commands
(`Initiate_Move`, `Cancel_Move`, `Initiate_Kneel`, `Command_Jump`) are applied before the step's update, then
collisions and the terrain machine, drag, bounds, the move, and the action, exactly in the original's order. The
per-frame arithmetic is single precision with the frame XNA actually reported, `TimeSpan.FromTicks(166667)` =
0.0166667 s (`FixedStepper.OriginalElapsedSeconds`), not 1/60: so the jump curve runs 35 frames and falls on the 36th,
and the idle-wall timer drops on the 61st frame. Every figure is player.json's, including the ceiling test's 3 cells.
Both characters simulate every Play step; player n starts on character n and `[` / `]` move it (A7, below).
Defect 1's fix leaves `isJumping` clear on a kneeling jump, and with the ordinary ground checks back the idle test stands
the character the same frame, so the kneel-jump "slide" is a one-frame nudge of about 2 px (the original's was barely
3 px before it trapped him); A18's play-through should say whether it wants a longer slide. Defect 2 restores the 1 x 3
size the moment the action leaves Sliding (including on Reset). Defect 3 is fixed (U.VAN-T23): `player.json`'s
`kneel.releaseStandsUp` stands the character up the step after S stops being held, read by `PlayerCharacter`'s idle
check; it only triggers on a step where `Kneel()` is not also called, so a held kneel (or a jump taken from it, which
still reads the action as Kneeling before this step's physics run) is unaffected. Defect 6 is fixed (U.VAN-T24):
`player.json`'s `wallJump.pushOff`/`pushSpeed` push an active wall jump away from a full-height wall every frame
contact lasts (through the same air drag as any other horizontal speed, so it settles at the same 130 px/s a run
settles to in the air), while the vertical jump curve and the passive wall slide are untouched; with `pushOff` off,
the original's climb-then-pin behaviour is unchanged. Defect 7 is kept as the original.

**Characters on screen (U.VAN-US-A7).** Core's `CharacterAnimator` is the original's `AnimationPlayer` and the
`PlayAnimation` calls of `Player.cs`'s `Action_*` methods: the action picks the strip (Standing idle, Running run,
Jumping jump only while the jump is live, Falling fall, WallSliding wallslide, Kneeling kneel, Sliding slide), a strip
restarts only when it changes, and the clock advances by the drawn frame's time with the original's `while (time >
frameTime)` step, looping or holding the last frame, at animations.json's frame times. The strips are the original
PNGs under `Resources/Characters/<sheet>/`, imported unscaled and point-filtered by `CharacterArtImporter` (code, not
Inspector), cut into 64 px frames pivoted at the bottom centre, so the sprite stands on the feet; Runtime draws
characters in list order (a later one over an earlier one) at the position interpolated by the world's `Alpha`. The
art faces left: facing right flips it and a wall jump inverts that; facing neither way (before the first move) does
not flip. A sheet without a strip (Shanoa and Leo have only idle and run) plays idle instead of the original's
`KeyNotFoundException` crash; U.VAN-T25 gives them their own moves. Both spawned characters are the default (Morris),
as the original's two were. Each is named at spawn from names.json's table for its gender (defect 8's fix: every name
can be picked), and `SceneManager.DrawText` is kept exactly: the name written twice in an Arial 10 equivalent
(Unity's built-in Arial, 13 px for 10 pt at XNA's 96 dpi), centred, red at (X - 1, Y - 100) and then yellow at
(X, Y), each lifted by TextWriter's Top alignment (16 px measured height x 10 x 0.4 = 64 px), so the yellow name sits
on the frame's top edge and the red one 100 px above it; characters.json's `nameTag` holds every figure. The names are
UI Toolkit labels in the pixel-perfect panel, placed each frame where the game camera shows the character, scaled by
its magnification and turned with it about the lifted text's top centre, as the original drew them through the same
matrix (A9). `[` / `]` (and L1 / R1) give each player id
its own driven index, wrapping both ways as `CyclePlayerLeft/Right`, with an event for the camera; defect 16 is fixed.
The camera follows the newly driven character by its mode (A9, below).

**The map on screen (U.VAN-US-A8).** `MapView` draws `Map.Draw` in its order under the characters (sorting orders
-300, -200 + layer, -100; characters from 0), over the game camera's clear colour, CornflowerBlue (100, 149, 237).
grid.json's `draw` section holds the clear colour, the outline's colour and thickness, `showLayers` and the layer list
(only `area_1` at (0, 0) in white: the four parallax images were never loaded and are not listed). The outline is
Core's `MapDrawing.Outline`, worked from `Shape.CreateBox` and its 1x1 pixel rotated about its top-left and stretched
to (length, 1): four 1 px strips just outside the map (x = -1, y = 1200, x = 1900, y = -1), each the edge's length, so
the four corner pixels stay uncovered; the right strip lies under the 1920 px backdrop, which is drawn after it, so it is
hidden as it was in the original. The backdrop is `Resources/World/area_1.png` whole and unscaled, 1920 x 1200 at 16 px
per unit with its top-left at the map's origin. The cells are painted in Core (`MapDrawing.PaintCells`): each drawn
cell's source rectangle of `tileset_default` (the original `Tileset/default.png`, 303 x 173, imported readable) at the
cell's position, white-tinted at its alpha, composited straight-alpha in the original's `foreach` order (column-major)
into one point-filtered 1888 x 1200 texture, so drawing it over the backdrop is the same as drawing every cell over it.
A cell is drawn if it is Hard or Soft, or whatever its state while the game is in the map editor (empty cells then show
as the first tile at alpha 64). The texture is repainted only when `Map.Revision` changes or the game enters or leaves
the editor; a whole repaint is a few tens of milliseconds, so A14's painting may want a one-cell repaint. Art is imported
in code (`WorldArtImporter`), point-filtered and unscaled. `unity.ps1 capture` renders the camera to
`Logs/Capture/*.png` (the followed view, that view zoomed and then turned by the camera keys, the top-left corner,
and the editor) to see it.

**The camera (U.VAN-US-A9).** Core's `WorldCamera` is `CameraManager` line for line, one per player (the world
gives each character's player one; `GameWorld.Camera(player)`), stepped after the characters in every state, so it
keeps easing while the menu or console freezes the world. The original's negative position convention, its start at
(1, 1) in LoosePlayer, its order (follow by mode, snap a velocity under 0.01 per axis, move, damp by 0.7, clamp the
centre to the 1900 x 1200 map zeroing the clamped axis' velocity), `MoveToLocation`'s absolute-value gap times 0.02,
LockPlayer's set-then-still-apply-velocity, the scroll keys adding 1 to the velocity, `=` / `-` and the wheel adding
0.01 to the unclamped scale, Insert / PageUp turning by 0.01 rad, and C cycling Free, LockPlayer, LoosePlayer,
LooseCursor are all camera.json's figures, replayed frame for frame in the Core tests against a literal copy of the
original's arithmetic (including the snap's double-precision 0.01). M into the editor sets the camera Free and puts the
editor cursor on the driven character; M out of it and R set LoosePlayer (R every player's camera, as it resets every
character). LooseCursor follows `GameWorld.EditorCursor(player)`, (0, 0) until the editor first opens; the cursor's own
movement in the editor, and the mouse joystick's scroll by 2, are the editor's (A14). An editor direction press scrolls
the camera by 4 unless Shift (`MoveCell*`). The view is the original's matrix, `Scale(s^3) * RotationZ(r) *
Translation(400 + X, 300 + Y)`: it magnifies and turns about the **world origin**, not the screen centre, so zooming or
turning swings the followed character off the centre until the ease brings it back, and a scale at or below zero turns
the view half round (scale cubed keeps its sign). This is kept exactly, as the story asks; it is the one camera
behaviour that reads like an accident, and the owner may want it zoomed about the centre instead. Runtime's
`GameCamera` frames the orthographic game camera on the world point the matrix puts at the screen centre, turned by the
rotation (plus a half turn for a negative magnification) with its orthographic size over the magnification, which is the
same picture; the EditMode tests check the Unity camera puts world points exactly where the original's matrix did.
The camera is interpolated by the world's `Alpha`, as the characters are, zoom and turn included.

**Effects.** The one effect used, `filter`, is a 1 % radial vignette (its `refractSampler` was never
bound, so it never refracted); it becomes a URP full-screen pass over the world camera with its
parameters as data. This is a **deliberate deviation** from RECONSTRUCTION 6.1: the original darkened by
distance within each sprite's own UV (every tile, player and name vignetted individually); a screen-space
pass vignettes the screen corners instead. At `burn = 0.01` the difference is near invisible, and the
per-sprite look can be restored with a sprite material if the owner wants it. `bloom`, `negative`, `pause` and `water` are ported as selectable post-process
presets that nothing enables, mirroring the original; the five effects that exist only in the earlier
copy (`basic`, `pointsprite`, `rain`, `snow`, `riemer`) are out of scope (§17).

**HUD and text.** Everything the original always drew is always drawn: the portrait, the quick bar,
the debug readout (§9.1) and the six start-up test messages, kept as data so the opening looks the
same; the readout's frame-rate shows the real frame rate. Text is UI Toolkit in Arial 10 and Lucida
Console 14 equivalents.

**Persistence.** None in the original and none added: the editor's changes are lost on exit. Adding a
save is a new feature and needs the owner's say-so.

**Scope.** No enemies, items, health, rooms, dialogue or saving are added (§16). The finished repo is a
proof of concept of the original's engine and toolbox and is then done.

## Decisions beyond the original (not yet made, flag when a story hits these)

- Whether the idle twin should become a controllable second character (§18.2).
- A real window size and aspect policy beyond the inferred 800 x 600 (§18.3).

## Laws

See AGENTS.md for the non-negotiables (reconstruction is ground truth, an engine-free tested Core,
content and tuning as data, built in code, done = verified by play, finite). Artifacts follow
HOUSE-LAW-12 and the docs site HOUSE-LAW-14 in the shared standard.
