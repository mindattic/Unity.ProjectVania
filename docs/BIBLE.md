---
codex: 1
project: Unity.ProjectVania
layer: bible
status: living
updated: 2026-10-23
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
| 3 | A kneel is not left by releasing S | **Keep, confirm in play**: it reads as a deliberate crouch toggle; the verification story (A18) checks it and the owner may flip it |
| 4 | `step2` and `drag` cues restarted every frame | **Fix**: one `step2` per run-animation frame advance; `drag` restarted when its 0.26 s clip ends |
| 5 | Shift or pad A held blocks moving | **Fix**: neither blocks movement; Shift has no binding |
| 6 | Wall jump climbs before pushing away | **Keep, confirm in play** (§4.7) |
| 7 | Terminal-speed landing while moving leaves one frame of Falling on OnGround | **Keep**: A6's frame-replay tests assert it |
| 8 | Last name never chosen; `Other` gender returns null | **Fix**: every name is selectable; `Other` is not exposed |
| 9, 10 | `Tile(x, y)` mix-up draws the sheet 180 times; palette Y clamp off by one; right step unbounded; no bounds check on the edited cell | **Fix**: the palette draws each tile once at its place; clamp to the sheet in both axes; no painting or highlighting outside the grid |
| 11 | Short console commands crash | **Fix**: too few words prints a usage line; nothing throws |
| 11b | `console clear` spares `History[0]` | **Keep** (it is the echoed command line) |
| 12 | Sun cycle and ActionCross never run | **Keep inert** as data (U.VAN-US-A16) |
| 13 | `fade out` snaps clear when it finishes | **Fix**: it holds black until `fade in` or `fade off` |
| 14 | `icon_poisondart` loads the parry icon; `icon_consecrate` unloaded | **Fix** in the data: each icon is its own file |
| 15 | Unused library code (`Quad`, `LinkedList`, `TileGrid`, `Event`...) and files outside the project | **Drop**: no behaviour to keep |

**Audio.** Music never starts by itself, as in the original; only `play music` / `play battle` start it
(§11). The XACT banks become a cue table in data (name, clip, volume in dB, loop, category), and the
`music1` cue and unused footsteps are kept in the table, unplayed, as the original had them.

**Input.** The Input System with a code-built action map; bindings are data (§3.1). Wheel zoom,
`=`/`-` zoom, rotate, scroll, mode-cycle and editor-toggle keep their original steps and signs.

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
- Whether the kneel should stand up on release, and the wall jump push-off (defects 3 and 6): decided
  after the play-through in U.VAN-US-A18.
- A real window size and aspect policy beyond the inferred 800 x 600 (§18.3).

## Laws

See AGENTS.md for the non-negotiables (reconstruction is ground truth, an engine-free tested Core,
content and tuning as data, built in code, done = verified by play, finite). Artifacts follow
HOUSE-LAW-12 and the docs site HOUSE-LAW-14 in the shared standard.
