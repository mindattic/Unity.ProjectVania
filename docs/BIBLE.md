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

**Three playable characters (owner decision 2026-10-23, built by U.VAN-T25).** The original spawned two Morrises at
(500,100) and (550,100), the second only standing idle while `[` / `]` / L1 / R1 cycled control (§4); Shanoa and Leo
were reachable only by editing a field, and would have crashed on their first jump. The rebuild spawns characters.json's
`roster`, Morris, Shanoa and Leo, at player.json's three spawn positions (the original's two, and Leo at (450,100)),
each named from its own gender's table; all three simulate every Play step, and `[` / `]` / L1 / R1 cycle who is driven
and followed through all three, wrapping both ways, the one left behind letting go (defect 16). A character's
`moveSet` (characters.json) decides what it can do, read by `PlayerCharacter.Can`: Morris and Shanoa have the whole
set (run, jump, fall, wall slide and wall jump, kneel, slide) on the same player.json figures and Shanoa's 1 x 3 size;
Leo (1 x 2, an FFVI field sprite) stands, runs, jumps and falls only, so down neither kneels nor slides him and a wall
does not catch him (in the air the terrain machine goes straight on to its floor tests, so he lands standing against a
wall). Every move set must hold Standing, Running and Falling, and every move needs its strip in the sheet (checked on
load). **The art.** The original ships only `idle` and `run` strips for Shanoa and Leo (no other art for either exists
in any copy of the project), so animations.json's clips for their other moves *borrow* frames of their own run strips
(`{ name, strip, frameIndices, frameTime, loop }`; `CharacterArt` cuts the listed frames): Shanoa's jump is run frames
2-3, fall 3, wall slide 9, kneel 4 (her lowest pose), slide 5, at Morris's frame times; Leo's jump is his run's crouch
then stride (0, 2: the same poses as cells (0, 3) and (2, 3) of the original's unused `SpriteSheets/leo.png`, which has
no jump pose either) and his fall holds the stride. These are stand-ins: a real strip dropped into
`Resources/Characters/<sheet>/` replaces one by changing its entry to the `width` / `frames` form. **The structures.**
grid.json's `structures` add six blocks of Hard cells between the bridge's walls, in the bridge's tile, for Leo to climb
and jump across (anyone can use them): a 3-high and a 6-high step against the right wall, then two-cell ledges each 3
rows higher zigzagging across 1- to 3-column gaps, the top one 18 cells above the floor; a Core test climbs them with
Leo from the floor to the top using only runs and jumps. Columns 28, 31 and 34 (the spawn and reset columns) stay clear
so everyone still falls straight to the floor, columns 21-26 stay clear so the left wall is the original's for wall
slides and wall jumps, and rows 27-29 are clear left of the steps so the floor is walkable. Commands carry a player id,
so a second driver later is a one-line change, per the rebuild standard.

**Defects of RECONSTRUCTION §15, decided.**

| # | Defect | Decision |
|---|---|---|
| 1 | Slide-jump leaves `isJumping` set: trapped, cannot fall | **Fix**: the slide jump starts a slide with `isJumping` clear, so ordinary ground checks apply |
| 2 | `UnitSize` stuck at (3,1) after a slide | **Fix**: restored to the character's size on leaving the slide |
| 3 | A kneel is not left by releasing S | **Fix** (owner decision 2026-10-23, built by U.VAN-T23): releasing S now stands the character up on the next idle step; a held S, or a jump taken from the kneel, still reads as a kneel that frame, so a slide jump is unaffected. `player.json`'s `kneel.releaseStandsUp` is the data slot |
| 4 | `step2` and `drag` cues restarted every frame | **Fix** (built by U.VAN-T16): one `step2` for every frame the run strip shows (as it starts and at each frame advance, on Core's own copy of the strip's clock); `drag` started again only when the last one's 0.26 s clip has ended (16 steps); audio.json's `runCue` / `wallSlideCue` name them |
| 5 | Shift or pad A held blocks moving | **Fix**: neither blocks movement; Shift has no binding |
| 6 | Wall jump climbs before pushing away | **Fix** (owner decision 2026-10-23, built by U.VAN-T24): an active wall jump now kicks off a full-height wall every frame contact lasts, at `player.json`'s `wallJump.pushSpeed` (200, the run cap), instead of pinning the horizontal speed at 0 until contact happened to break on its own, which against a wall this tall it never did |
| 7 | Terminal-speed landing while moving leaves one frame of Falling on OnGround | **Keep**: A6's frame-replay tests assert it |
| 8 | Last name never chosen; `Other` gender returns null | **Fix**: every name is selectable; `Other` is not exposed |
| 9, 10 | `Tile(x, y)` mix-up draws the sheet 180 times; palette Y clamp off by one; right step unbounded; no bounds check on the edited cell | **Fix** (built by U.VAN-T17): the palette draws each tile once at its place (so its tiles show at `Tile.Color`'s half alpha, where the 180 stacked draws had looked opaque); the Screen pick and the arrow steps stay on the sheet in both axes; no painting, highlighting or unhighlighting outside the grid |
| 11 | Short console commands crash | **Fix**: too few words prints a usage line; nothing throws |
| 11b | `console clear` spares `History[0]` | **Keep** (it is the echoed command line) |
| 12 | Sun cycle and ActionCross never run | **Keep inert** as data (U.VAN-US-A16) |
| 13 | `fade out` snaps clear when it finishes | **Fix**: it holds black until `fade in` or `fade off` |
| 14 | `icon_poisondart` loads the parry icon; `icon_consecrate` unloaded | **Fix** in the data: each icon is its own file |
| 15 | Unused library code (`Quad`, `LinkedList`, `TileGrid`, `Event`...) and files outside the project | **Drop**: no behaviour to keep |
| 16 | `[` / `]` leave the character switched away from with its `movement` held, so one switched away from mid-run runs on, undriven, for ever | **Fix** (U.VAN-T10): the character left behind lets go of its direction (`Cancel_Move`) as the switch happens, so the twin stands still as it was meant to |
| 17 | The message stack keeps the index of the message that last moved across ticks; once a pad's notice has made the front message opaque again it outlives the rest, and when the last of them is removed the stale index points past the list and the game throws (U.VAN-T15, a test replays the original's crash) | **Fix**: the index is kept inside the list; nothing else about the stack changes |
| 18 | `InvokeMessage` does nothing with no message queued (its "show the first message" branch is commented out), so the gamepad notice is lost once the start-up messages are gone | **Fix**: with nothing queued the notice is queued as a new message; hud.json's `messages.noticeWhenEmpty` is the slot |
| 19 | The five effects read no vertex colour, so every tint drawn through `filter` was dropped (white names, an editor hand that never changes colour, opaque empty cells in the editor) | **Fix** (built by U.VAN-T18): each sample is tinted before the effect runs, which is how every view already drew; effects.json records it |

**Audio (U.VAN-US-A13).** Music never starts by itself, as in the original; only `play music` / `play battle` start it
(§11). The XACT banks become a cue table in data (audio.json: name, wave, length, volume in hundredths of a dB, loop,
bank), and the `music1` cue, the two footsteps, `step1` and `jump1` are kept in the table, unplayed, as the original had
them. The world names every cue as a `CueRequested` event, none while muted. Each character's sounds are Core's
`CharacterCues`, stepped after that character's update in Play only (so the menu, console and editor freeze them with the
world): `step2` while Running, once per run-strip frame on a copy of the drawn strip's clock stepped at the fixed rate,
restarted with every run; `drag` while WallSliding, once per clip (defect 4 above). A drag the mute dropped started
nothing, so it does not hold back the next. Runtime's `CuePlayer` plays a cue as a one-shot from
`Resources/Audio/<wave>.wav` (16-bit PCM, decompressed and preloaded) at the cue's XACT volume times the table's volume;
a cue already started plays on through a mute, as XACT's did. Every wave the table names is imported but `music1`'s
(64.5 s of PCM no code ever started; the asset sweep, A17, brings it in). Runtime's `SongPlayer` is the original's
`MediaPlayer`: the console's `SongRequested` starts the song from audio.json's `songs` (`Resources/Music/music.mp3` and
`battle.mp3`, Vorbis, streamed) from its start, once, not looping, replacing any song playing; `MusicPauseChanged` pauses
or resumes it; `MuteChanged` sets its volume to 0 or the table's volume and the song plays on unheard, as
`MediaPlayer.Volume` did. When a song heard playing has run to its end, `GameHost` submits `EndSong` before the next
step, and the world's `Music` reads Stopped again (so `pause` resumes nothing, as `MediaPlayer.State` read). audio.json's
two cues and the two songs the console names are checked on load.

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
block moving). The original's empty stubs (Q, W, J, K, L, 1-5, pad X/Y/B, the jump release) issue nothing. The console's slide
sets the state (A11, below); a world built without console.json (the routing tests) flips the Console state at once.

**Character physics (U.VAN-US-A6).** Core's `PlayerCharacter` is `Player.cs` frame for frame: commands
(`Initiate_Move`, `Cancel_Move`, `Initiate_Kneel`, `Command_Jump`) are applied before the step's update, then
collisions and the terrain machine, drag, bounds, the move, and the action, exactly in the original's order. The
per-frame arithmetic is single precision with the frame XNA actually reported, `TimeSpan.FromTicks(166667)` =
0.0166667 s (`FixedStepper.OriginalElapsedSeconds`), not 1/60: so the jump curve runs 35 frames and falls on the 36th,
and the idle-wall timer drops on the 61st frame. Every figure is player.json's, including the ceiling test's 3 cells.
Every character simulates every Play step; player n starts on character n and `[` / `]` move it (A7, below).
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
not flip. A sheet without a strip plays idle instead of the original's `KeyNotFoundException` crash; the shipped
sheets have every strip their character's move set plays (Shanoa's and Leo's borrowed, **Three playable characters**
above). The spawned characters are characters.json's roster (Morris, Shanoa, Leo; the original's two were both Morris).
Each is named at spawn from names.json's table for its gender (defect 8's fix: every name
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
the editor; a whole repaint is a few tens of milliseconds, so the editor's changes (A14) repaint and upload only their
cells (`Map.ChangesSince`, `MapDrawing.RepaintCell`), the same picture as a whole repaint. Art is imported
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

**Game states and the main menu (U.VAN-US-A10).** The states change exactly as RECONSTRUCTION 2's table: Esc or Start
opens the menu from Play, the editor or the console, and closes it to Play whatever it was opened from (the console
stays down under it, as before); only Play steps the characters, so the menu, console and editor freeze the world while
the camera keeps easing. Core's `MainMenu` is the original's `MainMenu` plus `InputManager`'s menu methods: menu.json's
levels, the selected level and item, `Up` / `Down` wrapping, `Back` to the parent's first item (silently; at the root it
closes the menu), and `Select`, which opens an `Open` item's level, returns `Quit` or `ToggleFullscreen` for the world
to carry out, and then highlights the first item of the level shown, whatever the item did. The selection is never
reset on open or close, so the menu reopens where it was left, as the original's did. The world names every cue as a
`CueRequested` event at the original's moments: `menu_open` and `menu_close` on every toggle (including Back at the
root), `menu_move` on every Up or Down, `menu_select` on every select (Quit included), nothing on going up a level; Quit
is a `QuitRequested` event, which `GameHost` turns into `Application.Quit` (leaving Play mode in the Editor). menu.json
holds the cue names and the font. Runtime's `MainMenuView` draws it, only in the MainMenu state, as UI Toolkit elements in
the pixel-perfect panel after the names: `menu1` (543 x 742, bilinear as XNA's SpriteBatch sampled a stretched picture)
over the whole 800 x 600 at alpha 0.8, which greys the frozen world out; `arrow1` at (item x - 18, item y); the items
in Lucida Console 14 (19 px at 96 dpi) at (50, 100 + 50 * index), yellow, the highlighted one red; the title at (10, 15)
and the description at (10, H - 20) in white. Lucida Console is taken from the installed system fonts (it ships with
Windows and is not ours to redistribute), with Unity's built-in font standing in where it is missing. Runtime's
`CuePlayer` plays a cue from audio.json's table, its wave at `Resources/Audio/<wave>.wav` imported as the original's
PCM, a new one-shot each time at the cue's XACT volume (hundredths of a dB) times the table's volume, nothing while
muted; the rest of the table, and mute, pause and the songs, are U.VAN-US-A13's (**Audio**, above). The GUI's hand cursor, which the original drew over the menu at the mouse
(`Cursor` in Screen mode, RECONSTRUCTION 8), is drawn with the editor's cursors (A14, below); the console's slide and its cues
are A11's (below). `unity.ps1 capture` now also writes `menu.png` and `menu-video.png` (and `play-ui.png`), the camera's
picture with the UI Toolkit panel drawn over it.

**The console (U.VAN-US-A11).** Core's `GameConsole` is `Console.cs` and `ConsoleScript` is `ScriptManager.ExecuteScript`,
every figure, word and text console.json's. `~` or pad Back plays `machine_1_short` (`machine_1` once `console full` made
the panel taller than 200), even when a slide under way ignores the toggle, then starts the slide; the world steps the
console after the cameras, in every state, as the GuiManager did. Down is the original's integer ease, `Y += 1 + (1 - Y
/ 16)` (14 px at the top, 2 near the bottom), and the frame after `Y` stops being negative it snaps to 0 and only then
enters the Console state, so the world keeps moving under the panel as it slides in; up is 12 px a frame, setting Play on
every frame of it, until it hides at minus its height. Those state writes are the original's, quirks included: Esc while
it slides down opens the menu, and the slide's end puts the console back in charge. The prompt blinks on the original's
float timer (16.6667 ms a frame, flipping above 500, so every 30 frames); with the blink on the prompt sits at x 6 and the
caret follows the line, off at x 4 without it. Typing is A4's chain into the world's line: Enter pushes the line onto the
100-line history (newest first, the oldest dropped), runs it, then clears it and restarts recall; Up recalls the next
older line up to the 100th; Delete clears, Backspace removes one (every frame with Shift). A key with no character types
its name, and that includes Esc: leaving the console for the menu leaves "escape" on the line, as the original's did. The
script lower-cases the line; `play music|battle`, `fade in|out|off` and `msg hi` are exact lines; `say` / `yell` rejoin
the words after the first with single spaces exactly as the original's loop did (so a doubled space keeps an empty word,
and `say  x` quotes `'' x''`), `yell` upper-cased with `!`; then by word count: `exit`/`quit` (a `QuitRequested`, as the
menu's Quit), `version`/`ver` (History[0], replacing the echo), `pause` (pauses a playing song, else resumes, with its
LightSteelBlue message either way), `mute` (flips the world's `Muted`, "Mute: On|Off"), `lee`/`buzzer` (the banner into
History[0..8]), `console clear` (all but History[0], defect 11b kept), `console full` (600 high), `console text|history
<colour>` (the six XNA colours), `console bg red` (the 1 x 1 red texture; the only one), and `unit text|history|bg|full`
with five words reaching the same parameters; `run`, `execute` and `commander` do nothing. Defect 11 is fixed: the two
crashes (`console text|history|bg` or `commander x` with two words) write console.json's usage line under the echo
instead, and a test runs every combination of the language's words without a throw. Mute is the world's now: audio.json's
`muted` is its start, the world names no cue (menu or console) and starts no song while muted, and `CuePlayer` plays
whatever it is told. The script's reach outside the console is events the world names and state it keeps: messages
(`MessageRequested`, text and XNA colour) for A12's message stack, the fade (`Fade`, `FadeRequested`) for A12's lens, and
songs (`SongRequested`, `MusicPauseChanged`, `MuteChanged`, `Music`; `EndSong` for the player to report a finished song)
for A13, which plays them (**Audio**, above); the messages and the fade are shown by A12's HUD (below). Runtime's
`ConsoleView` draws it in the pixel-perfect panel after the menu, every frame: `console1` (608 x 203, bilinear, as it is
stretched) over (0, Y, 800, height) at 0.75, then in the Console state only, in the Arial 10 equivalent (13 px), the
yellow `>`, the line and the 100 history lines at the original's screen positions (not the panel's), in the console's two
colours. The two `machine_1` waves and `console1` / `red` are imported. `unity.ps1 capture` writes `console-sliding.png`,
`console.png` and `console-full-red.png`.

**The map editor (U.VAN-US-A14).** Core's `MapEditor` (namespace `Editing`) is `MapEditor\MapEditor.cs`, `Cursor.cs` and
`TilePalette.cs`, one per player (the world's `Editor(player)`), every figure and colour editor.json's (the hand's asset
and 13 x 18 size, the dead zone 2, the button colours, the paint and highlight colours, the 16 px tile, the drag box's 0.4
and 0.8 of the frame, the palette's background, offset, tileset size and half-alpha tiles); the mouse scroll 2 and cell
scroll 4 are camera.json's `editorScrollSteps`, a cleared cell is grid.json's fresh cell, and the settings check a tile is
exactly a cell. M or the middle button opens it as before (A4, A9: camera Free, cursor on the driven character). The world
steps it last, after the HUD and only in the MapEditor state, as the original's component ran last. The router sends the
mouse every editor step (`PointEditor`: the reference-screen position, left and right held, and whether the window has the
focus) after Space's `SetEditorCursor`. **World mode** (Space not held): the mouse beyond half the dead zone either side of
the screen centre (x > 401, x < 399, and so on) adds the scroll to the camera's velocity, felt next step; the cursor is
`-Camera.Position` plus half the hand in whole pixels (6, 9), so it ignores zoom and turn as the original's did; then, left
held, every character whose 25 x 51 box (`Player.Bounds`, standing on the feet) touches the hand's 13 x 18 box (XNA's
`Contains` or `Intersects`) is put at the cursor, keeping its velocity and action; then the selected cell is the cell under
the cursor: left paints it Hard, opaque white, with the selected tile's 16 px square; right clears it to the fresh cell;
neither tints it yellow (the colour only); and a cell just left that is still yellow goes back to the fresh tint if empty,
else opaque white. Then the mouse is warped back to the centre if the window has the focus. Because left both drags and
paints, a dragged character stands on the cell painted under its feet, as in the original. **Screen mode** (Space held): the
cursor is put at (0, 0) (`PreviousGuiPosition`, never set) and then at the mouse kept so the hand stays on screen, and the
palette's tile is `round((cursor - (16, 300)) / 16)` to even, kept on the sheet; nothing is painted, highlighted or
scrolled, and the selected cell is kept. The arrow presses step the tile (unless Ctrl) and scroll the camera by 4 (unless
Shift), as A4 routed them. LooseCursor follows the editor's cursor in either mode, as the original's camera did. Defects 9 and
10 are fixed (table above). Kept as the original: the highlight is the cell's colour, so the cell under the cursor when the
editor closes keeps its yellow (a Hard one shows yellow in Play until the editor highlights and leaves it again); the editor
writes a cell only when it changes, so the map's revision counts real changes. The readout's "Coords:" is the editor's
selected cell, set by its World steps only (A12). Runtime: `EditorCursorView` draws the World hand in the world (sorting
-50, after the cells, before the characters) at the interpolated camera's centre plus (6, 9), tinted yellow with left, red
with right, white otherwise; `EditorGuiView` draws after the menu and before the console, as `GuiManager.Draw`'s state
branch: in the editor the palette (`tilepalette_background` at 0.4 stretched from 300 x 300 to (5, H - 305, 300, 295),
bilinear; the tileset's 18 x 10 whole tiles at (16, 300) at alpha 128; `square_selector` over the selected tile) and the
Screen hand while Space is held; in the menu the GUI's hand at the mouse kept on screen. The three pictures are the original
PNGs under `Resources/Gui`. The device reports the mouse on the letterboxed reference; while the editor is in World mode
the OS cursor is locked and the mouse's travel since the last step, in reference pixels, is reported as an offset from the
centre, which is the original's warp-and-read; an unfocused window reports the mouse at rest (the original's inactive
window kept reading wherever the mouse was). The OS cursor is hidden while playing, as `IsMouseVisible = false` hid it.
`unity.ps1 capture` writes `editor-paint.png` (a painted row, the yellow-highlighted cell under the hand, the palette) and
`editor-palette.png` (Space held: the Screen hand over the palette).

**Effects (U.VAN-US-A15).** The world is drawn through the original's effects exactly as `SceneManager` drew it: per
sprite, never as a full-screen pass (the earlier full-screen deviation is withdrawn by the owner's rebuild standard).
effects.json names the preset the world uses (`world: Filter`, the original's `effect = "filter"`) and holds each
shader's declared parameters at their .fx defaults (filter `burn 0.01`, `saturation 1`, `r g b 1`, `brite 0`; bloom
`v 0.01`, `a 0.2`; water `horizon 0.5`, `delta`/`theta 0`); figures the shaders wrote as literals (pause's 0.005, water's
0.35 and ripple, the 12 Poisson taps) stay in code as the shaders kept them. Core's `SpriteEffect` is the five pixel
shaders' arithmetic on floats, in XNA's texture coordinates (v down), nearest texel clamped at the edge, output clamped to
8 bits; `Resources/Shaders/WorldEffect.shader` (`ProjectVania/WorldEffect`, a URP sprite shader with one keyword per preset)
is the same arithmetic on the GPU, reading the texel with `Load` so the sampler's settings cannot change it.
`WorldEffectMaterial` builds the one shared material from the data. The outline (its 1 x 1 pixel stretched, so vignetted
along each strip), the backdrop, the characters' frames (in their strip's coordinates, as the original's source rectangles
were) and the editor's world hand draw with it. The cells are one painted texture, so drawing it through the shader would
vignette the whole canvas; instead Core paints each cell through `SpriteEffect` texel by texel in the tileset's
coordinates (a cell is its tile's sprite) and the canvas draws with the plain sprite material. The refract sampler was
never bound, so its displacement is zero and the filter is the plain vignette (RECONSTRUCTION 18.5 settled). Defect 19 is
fixed: each sample is tinted before the effect. The names are UI Toolkit text in Unity's font, not glyphs of the original's
sprite-font atlas, so a glyph has no place on that atlas to be vignetted by: each name's colour is its write's colour
shaded as one white texel at the texture's middle, which applies every colour term of the preset (at the defaults the
colour is unchanged; the atlas vignette was at most 0.7 % of a glyph's brightness). `bloom`, `negative`, `pause` and
`water` are presets the data can select and nothing does, as in the original; each draws the world per sprite, as the
commented-out `effect = "negative"` would have. The GUI is drawn after the world and through none of them, as
`GuiManager` drew it. `unity.ps1 capture` draws the tileset through each preset (`effect-*.png`) and fails unless every
pixel is within one level of Core's `SpriteEffect` (`effects.txt`); the five effects that exist only in the earlier copy
(`basic`, `pointsprite`, `rain`, `snow`, `riemer`) are out of scope (§17).

**HUD and text (U.VAN-US-A12).** Everything the original always drew is always drawn, every figure, text and colour
hud.json's. Core's `Hud` holds what changes: `QuickBar` is `QuickBar.cs` (starts shown at (103, H - 105); U, in Play
and the editor, starts a slide unless one is under way; 3 px a frame down until its top reaches H, or up to H - 105,
the original's two separate `if`s), `FadeLens` is `Lenses.cs` (its float timer passes 10 ms every frame, so `CheckFade`
runs every frame: Out darkens the black tint by 1 to 255, In clears it by 1 to 0 and then goes Off, Off keeps it
clear) and `MessageStack` is `MessageHandler.cs` (a message queues at (400, 284); while one is queued the float timer
passes 50 ms every third frame, and then each message in order falls 1 px and loses 1 alpha if it is the first or 16 px
behind the one that last moved, and is removed at alpha 0; it is written only below y 316, so a message shows after
1.65 s and is gone after 12.75 s), replayed frame for frame in the Core tests against a literal copy of the original's
`Update`. The six start-up messages ("test1" .. "test6", Wheat) are queued when the world is built, the script's
messages (`say`, `yell`, `msg hi`, `pause`, `mute`) join the stack, `fade in|out|off` set the lens, and a pad connecting
(Play and the editor) rewrites the front message as "XBox 360 Gamepad Connected" in Peru, opaque again, as
`InvokeMessage` did. The world steps the HUD after the console, in every state, so messages fall and fades run while
the menu or console freezes the world. `HudReadout` builds the debug text as `GuiManager.Draw` concatenated it, numbers
written as .NET 3.5 wrote a float (seven significant digits, no negative zero): the driven character's name, position
and cell at (100, 100); "Mode: " + the camera's mode and its (negative) position at (300, 100); the GUI cursor at
(600, 100), written twice as the original wrote it twice; "Coords:" + the editor's selected cell (the cell under the
editor's cursor, (0, 0) until the editor first opens) at (700, 100); the frame rate at (W - 100, 10). Runtime draws it in
`GuiManager.Draw`'s order as UI Toolkit elements in the pixel-perfect panel: `HudView` after the names and before the
menu (the portrait's glass at 0.4, `portrait_1` stretched from 84 x 94 to the 99 x 110 frame, bilinear, and the frame,
at (0, H - 110); the quick bar's glass at 0.4 and frame where its slide has it; then the lens, the whole screen in the
lens' tint), so a fade darkens the world, the names and the HUD but not the menu, the console or the text; and
`HudTextView` last, after the console (the readout in white, then each shown message in its colour and fading alpha),
in the Arial 10 equivalent (13 px). The five HUD pictures are the original PNGs under `Resources/Gui`. The GUI cursor
the readout writes is the mouse read on the letterboxed 800 x 600 reference in whole pixels and kept inside (0, 0) ..
(787, 582) as the original's Screen-mode cursor kept its 13 x 18 picture on screen; the cursor itself is drawn with the
editor's cursors (A14). Decisions and fixes: the readout shows the real frame rate (the original's `1 / ElapsedGameTime`
always read 59.99988 under XNA's fixed step; `realFrameRate: false` shows that); defect 13 is fixed
(`holdBlackAfterFadeOut`); defects 17 and 18 below are fixed. The Sun's tint never ran and is invisible, so nothing
draws it here; it and the ActionCross are A16's inert data. `unity.ps1 capture` writes `play-ui.png` (the HUD with the
start-up messages falling), `hud-quickbar-away.png` and `hud-fade-half.png`.

Text is UI Toolkit in Arial 10 and Lucida Console 14 equivalents.

**Persistence.** None in the original and none added: the editor's changes are lost on exit. Adding a
save is a new feature and needs the owner's say-so.

**Scope.** No enemies, items, health, rooms, dialogue or saving are added (§16). The finished repo is a
proof of concept of the original's engine and toolbox and is then done.

## Decisions beyond the original (not yet made, flag when a story hits these)

- ~~Whether the idle twin should become a controllable second character (§18.2).~~ Decided by the owner 2026-10-23:
  Morris, Shanoa and Leo are all playable, one driver cycling between them (U.VAN-T25, **Three playable characters**).
- Real art for Shanoa's jump, fall, wall slide, kneel and slide and Leo's jump and fall (they borrow run frames now).
- A real window size and aspect policy beyond the inferred 800 x 600 (§18.3).

## Laws

See AGENTS.md for the non-negotiables (reconstruction is ground truth, an engine-free tested Core,
content and tuning as data, built in code, done = verified by play, finite). Artifacts follow
HOUSE-LAW-12 and the docs site HOUSE-LAW-14 in the shared standard.
