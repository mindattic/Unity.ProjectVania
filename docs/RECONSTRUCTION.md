---
codex: 1
project: Unity.ProjectVania
layer: research
status: living
updated: 2026-10-23
---

# ProjectVania — reverse-engineering record

Source: `R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\ProjectVania` (the fuller copy), a
Visual Studio 2008 / XNA Game Studio 3.0 solution (`Engine.sln`, one `Engine` project, .NET 3.5,
x86, built with a nested `Content.contentproj`). Its earlier, smaller sibling is
`R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\XNA 2D\ProjectVania` (section 17). Both are
read-only reference, never edited. Everything below is read directly from the C# files and the
`Engine\Content` tree (487 files in the fuller copy, 240 of them source and content once build output and
the ClickOnce publish are set aside, 57 of them `.cs`, about 18,900 lines of which about 5,500 are name
tables and another 1,500 an unused linked-list library); file paths are
relative to `Engine\` and are given so a later session can re-check the original.

**How this was produced.** The original was read statically: every `.cs` file, every `.fx`,
`.spritefont`, `audio.xap`, `maps.zdx`, `maps.txt` and `Content.contentproj`, plus the dimensions of
every PNG and the length of every WAV. XNA 3.0 is not runnable on this machine, so nothing here was
observed running. Behaviour that was traced through several interacting code paths (the player
state machine, the slide, the wall jump, the message stack) is marked **traced**: it follows from the
code as written and must be confirmed by play when it is rebuilt (the rebuild's verification story,
U.VAN-US-A18, does that). Anything the code does not decide is under "Open questions" (section 18).

The project is named `ProjectVania` (the folder) but is a single namespace, `Engine`, and an
engine-first sandbox: no enemies, items, health, levels, save files, story or win condition exist.
The only game is one player (two are spawned and switchable) moving in one 1900x1200 room.

## 0. Runtime facts

- Framework: XNA 3.0, `Microsoft.Xna.Framework.Game` subclass `Game1` (`Game1.cs`), entry
  `Program.Main` (`using (Game1 game = new Game1()) game.Run();`).
- Timestep: the XNA default, fixed 60 Hz (`IsFixedTimeStep` never changed), so `ElapsedGameTime` is
  always 1/60 s. Much of the physics is deliberately **per frame, not per second** (gravity adds 10
  every frame; drag multiplies every frame), so the rebuild's Core must step at exactly 60 Hz.
- Back buffer: never set, so the XNA default 800x600 window. Evidence: `Sun` and `Lenses` hard-code or
  read 800x600, `Gui\Hud\demo_gui.png`/`gui_0.png` are 800x600, every HUD rectangle is placed from
  `Camera.Screen` (the viewport read once in `CameraManager`'s constructor). Windowed by default;
  F12 or a menu item calls `GraphicsDeviceManager.ToggleFullScreen()` (same back-buffer size).
- Clear colour: `Color.CornflowerBlue` (`SceneManager.Draw`). The OS cursor is hidden
  (`IsMouseVisible = false`); the game draws its own cursors where it wants one (section 9).
- Version string: `g.Version = "Version 0.0.1"` (shown by the console `version` command).
- Packaging: `Engine\publish\` is a ClickOnce publish (`setup.exe`, `Engine.application`, manifest);
  `Content\Application\GameThumbnail.png` (64x64) and `Game.ico` are its icons. Not game content.

## 1. Architecture: one bundle of swappable components

`Game1.Initialize` builds ten `GameComponent`s and hands every one the same `ComponentCollection`
(`Core\Datatypes\ComponentCollection.cs`): a plain object with ten interface-typed fields
(`ISceneManager`, `IScriptManager`, `IInputManager`, `ICameraManager`, `IAudioManager`,
`ITextureManager`, `IEffectManager`, `IFontManager`, `IGuiManager`, `IMapEditor`). Every other object
(`Player`, `Map`, `Console`, `Cursor`, `Tileset`) keeps that bundle and reaches the others through it
(`Scene.Map`, `Camera.Position`, `Audio.PlaySfx(...)`). There is no event system (`Events\*` is an
empty `EventManager`, an `Event` with a name and a rectangle and an empty `Zone`, all unused).

Components are added to `Game.Components` in this order, which is both their `Update` order and
(for the drawable ones) their `Draw` order, because every `UpdateOrder`/`DrawOrder` is left at 0:

| # | Component | Updates | Draws |
|---|---|---|---|
| 1 | `TextureManager` | nothing | no |
| 2 | `EffectManager` | nothing | no |
| 3 | `FontManager` | nothing | no |
| 4 | `AudioManager` | nothing (called from Scene) | no |
| 5 | `InputManager` | reads keyboard, pad, mouse; dispatches by game state | no |
| 6 | `SceneManager` | audio tick, map, players (only in `Play`) | map, players, names |
| 7 | `CameraManager` | follows per its mode, eases, builds the matrix | no |
| 8 | `GuiManager` | HUD pieces, console, menu, messages, fps | HUD, menu, console, debug text |
| 9 | `ScriptManager` | nothing | no |
| 10 | `MapEditor` | cursor, palette, tile painting (only in `MapEditor` state) | via Scene and Gui |

So one frame is: **input -> scene (players move) -> camera (follows the already-moved player) -> gui
-> script -> map editor**, then Scene draws, then Gui draws. Content is loaded by name through
dictionaries filled in each manager's `Initialize` (`TextureManager.Textures["morris_run"]`,
`FontManager.Fonts["Arial"]`, `EffectManager.Effects["filter"]`), with the asset folder chosen by
the `g.ContentDirectory` enum and `g.Directory()` (a switch returning `Content\Areas\`,
`Content\Gui\Icons\`, and so on).

`g` (`Core\Global.cs`, the "global" class) holds the shared enums and helpers:
`FadeState {Off, In, Out}`, `DrawingMode`, `EditingMode`, `CameraMode {Free, LockPlayer, LoosePlayer,
LooseCursor}`, `Gender {Male, Female, Other}`, `DirectionX {Left=-1, Right=1}`, `DirectionY {Up=-1,
Down=1}`, `GameState {Play, MainMenu, Console, MapEditor}`, `CharacterSheet {Shanoa, Morris, Leo}`,
`SpriteSheetDefinition` (unused struct of seven strings), spacing constants (2, 4, 6, 8; unused),
`terminalVelocity = 1.0f` (unused), `A_near_B`, `A_between_BC` (two overloads, the Vector2 one has a
typo comparing `A.Y >= B.X + Width`; unused), `YesNo`, `OnOff`, `GetIntersectY` (line y at x; unused),
`RandomName`, `NewLine`, `Alpha(int percent)` and `Alpha(float)` (white with that alpha, returning
opaque white if out of 0..1 / 0..100), `IsArithmetic`, `IsEven`, `IsOdd`.

`Rand` (`Rand.cs`) wraps one shared `System.Random` (`GetRandomInt(min,max)` is
`Next(max-min)+min`, so **max is exclusive**; floats, doubles and a Vector2 form exist).

## 2. Game states and how they change

`g.GameState {Play, MainMenu, Console, MapEditor}`; `SceneManager.GameState`'s setter also records
`PreviousGameState` (never read). The game starts in `Play` (`SceneManager.Initialize`).

| From | Trigger | To | Notes |
|---|---|---|---|
| Play, MapEditor, Console | Esc, or pad Start | MainMenu | plays `menu_open` |
| MainMenu | Esc, or pad Start, or Left/B at the root level | Play | plays `menu_close`; always to `Play`, even if the menu was opened from the editor |
| Play, MapEditor | `~` (OemTilde), or pad Back | (console slides) | state stays until the slide ends |
| Console | `~` or pad Back | Play | slide-up sets `Play` on every frame of the slide |
| Play | M, or middle mouse button | MapEditor | camera -> `Free`, editor cursor placed on the player |
| MapEditor | M, or middle mouse | Play | camera -> `LoosePlayer` |
| any | R | Play | also camera -> `LoosePlayer`, resets every player (section 4.9) |

While the state is not `Play`, `SceneManager.Update` skips every player update (the world freezes) but
still updates the map layers; the camera, GUI and audio keep running.

## 3. Input (`Managers\InputManager.cs`, 1,235 lines)

Each frame `InputManager.Update` copies current to previous for pad and keyboard, reads the new
states, then calls one of four handlers by game state; afterwards `UpdateMouse` runs in every state.
Edge tests are `IsKeyDown(k) && WasKeyUp(k)` (pressed this frame). Bindings are set in
`SetupKeys`/`SetupButtons` (a TODO says they should load from XML; they never do).

### 3.1 Bindings

| Action | Keyboard | Xbox pad |
|---|---|---|
| Move left / right | A / D (held) | left stick left / right |
| Kneel (slide if moving) | S (held, and neither A nor D) | left stick down |
| Jump | Space (on press) | A (any frame it is held) |
| Cycle player left / right | `[` / `]` (on press) | L1 / R1 (on press) |
| Main menu | Esc | Start |
| Console | `~` (OemTilde) | Back |
| Reset | R | none |
| Toggle fullscreen | F12 (Play and MainMenu only) | none |
| Toggle quick bar | U | none |
| Camera mode cycle | C | none |
| Map editor toggle | M, middle mouse | none |
| Camera scroll left/right/up/down | Delete / PageDown / Home / End (held, `+1`/frame of velocity) | none |
| Zoom in / out | `=` / `-` (held, `+-0.01` scale/frame), mouse wheel up / down (`+-0.01` per notch) | none |
| Rotate left / right | Insert / PageUp (held, `-+0.01` rad/frame) | none |
| Unused action keys | W (up), Q, E (shoulders), Enter (confirm), Backspace (cancel), J, K, L, 1-5 (with and without Shift) | D-pad, L3, X/Y/B stubs |

Notes that matter for parity:

- `Home/Delete/PageDown/End` are also bound as the pad-button stand-ins `ButtonY/X/B/A`, and
  `Insert/PageUp` as `ButtonL1/R1`; those stand-ins call empty stubs (`Command_ButtonY`,
  `Action_ButtonX/B/A`, the ActionCross code being commented out), so only the camera effect is real.
- `Command_Move` does nothing while **either Shift is held or pad A is held**; because the
  left/right/down/idle choice is an `if / else if` chain, the player keeps whatever `movement` it had
  (and so keeps accelerating) while Shift is held. **Traced.**
- When none of left, right, down is held the handler calls `Command_Stand` every frame, which sets
  `movement = 0`. When down is held (alone) it calls `Command_Kneel`, which does **not** reset
  `movement`, so a runner who switches from D to S keeps `movement = +1` (this is what makes a slide).
- `IsButtonDown` is correct but `WasButtonDown(button)` ignores its argument and tests pad B
  (`InputManager.cs` line 1222); the jump-release branch it feeds is empty anyway.
- Each frame the pad's left trigger value drives both vibration motors
  (`GamePad.SetVibration(One, Triggers.Left, Triggers.Left)`).
- A pad connecting this frame sets message index 0 to "XBox 360 Gamepad Connected" (Peru colour).
- Pad keyboard-equivalents are used only for player 1 (`PlayerIndex.One`).

### 3.2 Per state

- **Play** (`CheckInput_Play`): everything in 3.1 except the map-editor-only items; Esc, `~` and the
  pad menu/console buttons first, then movement, jump, R, cycle, the stubs, F12, U, C, M, camera keys.
- **MapEditor** (`CheckInput_MapEditor`): Space held switches the editor cursor to `Screen` mode
  (pick a palette tile), otherwise `World` mode; A/D/W/S (and the D-pad) on press select the previous
  or next palette tile (unless Ctrl) **and** scroll the camera by 4 (unless Shift) - see section 12;
  Esc, `~`, R, F12, U, C, M and the camera keys behave as in Play. Player movement and jump are not
  read.
- **Console** (`CheckInput_Console`): see section 10.2.
- **MainMenu** (`CheckInput_MainMenu`): section 9.3.
- **Mouse** (`UpdateMouse`, every state): wheel up zooms in, wheel down zooms out (each notch is a
  single `+-0.01` to `scale`), a middle-button press toggles the map editor.

## 4. The player (`Player\Player.cs`, 940 lines; `Player\Animation\*`)

The only entity. Two are created in `SceneManager.LoadContent`: `new Player((500,100), Male)` and
`new Player((550,100), Male)`, both of the `Morris` sheet (the field default). `SceneManager.PlayerIndex`
selects which one `Input` drives and the camera follows (`CurrentPlayer`); **both** are updated and
drawn every frame, so the idle one falls to the floor and stands still while the other runs.

### 4.1 Identity

- `Name = g.RandomName(gender)`: a random pick from a hard-coded table in `Core\Global.cs`: about
  1,220 male names (lines 331-1550, "Aaron" ... "Zane"), about 4,070 female names (lines 1559-5634),
  15 "beast" names (BoGrath, Reigra, Zalzef, Regaa, Grunwelp, Zogra, Gatrick, Calidai, Moroba,
  Ghanstro, Borgridge, Polark, Zambo, Ritpht, Zebtweird). `Gender.Other` is never passed; it falls
  through to a one-element table holding `null`, and the beast names are chosen by an `else` that is
  only reached by a third gender value. The pick is `names[GetRandomInt(0, Length-1)]`, which with
  the exclusive max means **the last name in each table can never be chosen**.
- `Facing` (`DirectionX`, default 0 = neither; `Draw` then does not flip, and the wall test treats it as Right), `Position`
  (`Vector2`, the **bottom centre of the feet**), `Velocity`, `UnitSize`, `HasAbility`, `ShowBounds`.
- `CharacterSheet` picks the art and `UnitSize` (collision height in cells):
  `Shanoa` (1x3), `Morris` (1x3, the default), `Leo` (1x2). Nothing ever changes it after
  construction; Shanoa and Leo are reachable only by editing the field's initial value.
- `AbilityList` (`AbilityList.cs`): `Run, Jump, Kneel, WallSlide, WallJump, Slide = true`;
  `SinkInWater, BulletTime = false`. Read in play: `Jump`, `Kneel`, `WallSlide`, `WallJump`.
  `Run`, `Slide`, `SinkInWater`, `BulletTime` are never read.

### 4.2 Constants (Morris/Leo/Shanoa share them)

| Name | Value | Used for |
|---|---|---|
| `MoveAcceleration` | 14000 | `velocity.X += movement * 14000 * dt` (233.3 per frame) |
| `MaxMoveSpeed` | 200 | clamp of `velocity.X` before drag |
| `GroundDragFactor` | 0.58 | `velocity.X *= 0.58` each frame when `terrainState == OnGround` |
| `AirDragFactor` | 0.65 | the same when not on the ground (InAir, OnWall) |
| `MaxJumpTime` | 0.6 s | length of the rising phase |
| `JumpLaunchVelocity` | -1100 | scale of the jump curve |
| `JumpControlPower` | 0.14 | exponent of the jump curve |
| `GravityAcceleration` | 10 | added to `velocity.Y` **per frame** while falling |
| `MaxFallSpeed` | 500 | clamp of `velocity.Y` while falling; also the "hard landing" test |
| wall-slide speed | `MaxFallSpeed / 6` = 83.33 | slide cap (section 4.7) |
| wall-slide decay | 0.97 per frame | while faster than the cap |
| `MaxWallSlideTime` | 1.0 s | idle-on-wall timer before dropping |
| `fallDamageTime` | 3.0 s | `fallTime` threshold whose body is commented out (no damage exists) |
| `MaxWalljumpDelayTime`, `MaxLockOutTime` | 0.5 s | declared, only referenced in comments |
| slide launch | `facing * 14000 * dt` = +-233.3 | `velocity.X` on a kneeling jump |
| slide friction | 0.7 per frame | `velocity.X *= 0.7` each frame in `Action_Slide` |

Derived figures (computed by simulating the per-frame arithmetic): steady ground run speed
**116 px/s** (`0.58 * 200`), steady air speed **130 px/s** (`0.65 * 200`); a full jump rises about
**76 px** over 36 frames (the continuous curve integrates to 81 px) and then falls under gravity,
reaching `MaxFallSpeed` after 50 frames (0.83 s).

### 4.3 Frame order (`Player.Update`)

```
elapsed = dt (1/60)
CheckCollisions(elapsed)   // sample the grid, apply horizontal accel, drive the terrain state machine
ApplyDrag()                // velocity.X *= 0.58 (OnGround) or 0.65 (anything else)
CheckBounds()              // clamp position to the map rectangle, zeroing the clamped velocity axis
position += velocity * elapsed
CheckActionState()         // run the current action: animation choice, jump curve, gravity, sounds
```

`CheckActionState` therefore runs **after** the position update, so a jump's velocity is applied on
the following frame, and gravity added in `Action_Fall` moves the player the frame after it was added.

### 4.4 Grid sampling (`World\Map\Map.cs`, section 5)

Each frame `CheckCollisions` samples three things at the player's feet position:

- `wallCollision = Map.CheckHorizontalCollision(copy of position, Facing, UnitSize.Height)`: a count
  0..`UnitSize.Height` of how many of the (up to three) cells stacked above the foot row, in the
  **facing** direction, are `Hard`. It works on a *copy* of the position, so its snap is lost; only
  the count matters. If the count is above 0 and the player is not `isJumping`, `velocity.X` is
  zeroed here.
- `floorCollision = Map.CheckFloorCollision(ref position)`: if the cell containing the feet is `Hard`,
  snaps `position.Y` to that cell's top (`y * 16`) and returns true. This one **does** move the player.
- `ceilingCollision = Map.CheckCeilingCollision(ref position, 3)`: if the cell three cells above the
  feet is `Hard`, snaps `position.Y` to `(y + 4) * 16` and returns true.

Horizontal acceleration then depends on `wallCollision`:

- `0` (no wall): `velocity.X += movement * 233.3`, clamp to +-200, and **if `isWallJumping`, negate it**
  (so a wall jump pushes away from the wall).
- `== UnitSize.Height` (a full-height wall): `velocity.X = 0` (with a no-effect `+= 5 * -movement`
  first when wall jumping).
- any partial count (1 or 2 of 3): neither happens; the player is neither accelerated nor stopped
  (except by the zeroing inside `CheckWallCollision`), so the drag decays it. This is the step and
  ledge behaviour: a one-cell step is not a wall to a three-cell-tall player, but it does not
  accelerate him either.

### 4.5 Terrain state machine (`TerrainState {InAir, OnGround, OnWall, Hanging}`)

`Hanging` is declared and never used. The rest, as traced from `CheckCollisions`:

- **OnGround, not jumping**: no floor -> `Initiate_Fall`; floor and `movement == 0` and the action is
  not `Kneeling` -> `Initiate_Stand` (only does anything if the action is not already Standing).
- **OnGround, jumping** (the whole rise of a ground jump, because a jump does not change the terrain
  state): only the ceiling is tested; a hit zeroes `velocity.Y` and falls.
- **InAir**: touching a wall while `movement != 0` -> `Initiate_WallSlide`; else no floor ->
  `Initiate_Fall` (re-run every frame); else floor: if `velocity.Y == 500` exactly (a terminal-speed
  landing) -> terrain `OnGround` + `Initiate_Kneel`, otherwise `Initiate_Stand`.
- **OnWall**: floor -> `Initiate_Stand`; wall gone -> **flip `Facing`** and `Initiate_Fall`; idle
  (`movement == 0`) on the wall -> `wallslideTime += dt` until 1.0 s, then `Initiate_Fall`.

### 4.6 Action state (`ActionState {Standing, Running, Kneeling, Sliding, Jumping, WallSliding, Hanging, Falling}`)

`Reset` and construction start the player `Falling` / `InAir`. Each action has an `Initiate_` (a
transition, called from input or collision) and an `Action_` (run every frame from
`CheckActionState`):

- **Stand** (`Initiate_Stand`, only if not already Standing): action Standing, terrain OnGround,
  `jumpTime = 0`, `isJumping = isWallJumping = false`, `fallTime = 0`, `velocity.Y = 0`.
  `Action_Stand`: `movement = 0`, play `idle`.
- **Run** (`Initiate_Move(direction)`): `Facing = direction`, `movement = +-1`; action becomes Running
  only if on the ground and not jumping. `Action_Run`: play `run` and **`Audio.PlaySfx("step2")`
  every frame** it runs (60 cue starts per second of a 0.43 s sound; the cue has no instance cap
  below 255). `Cancel_Move`: `movement = 0`.
- **Jump** (`Initiate_Jump`, only when `isJumping` is false and action is not Jumping and
  `jumpTime == 0`): `isJumping = true` if the Jump ability is held (the `if` guards only that one
  statement; `wasJumping = false` always runs); cancelled when on a wall without WallJump; if the
  action was **Kneeling** it becomes **Sliding** with `velocity.X = Facing * 233.3` (the "slide jump",
  and `isJumping` stays true); otherwise action = Jumping. `Action_Jump` (while `isJumping`): play
  `jump`; `jumpTime += dt`; while `jumpTime <= 0.6`,
  `velocity.Y = -1100 * (1 - (jumpTime / 0.6)^0.14)`, and from the wall, set `isWallJumping`; past
  0.6 s -> `Initiate_Fall`. If `isJumping` is cleared while `wasJumping` was set, `jumpTime = 0` and
  fall. **Releasing the key does not shorten the jump**: `Cancel_Jump` exists, but its only call
  site is commented out, so every jump is full height (about 76 px). Re-jump requires leaving the
  Jumping action and `jumpTime` being exactly 0, which `Initiate_Stand`, `Initiate_WallSlide` and
  `Cancel_Jump` restore.
- **Fall** (`Initiate_Fall`): `isJumping = isWallJumping = false`, `wallslideTime = 0`, action
  Falling, terrain InAir (it does not reset `jumpTime` or `fallTime`). `Action_Fall`: play `fall`;
  `velocity.Y = clamp(velocity.Y + 10, -500, 500)`; `fallTime += dt`.
- **Wall slide** (`Initiate_WallSlide`, only if the WallSlide ability, the action is not already
  WallSliding and `movement != 0`): `isJumping = false`, `jumpTime = 0`, action WallSliding, terrain
  OnWall. `Action_Wallslide`: if `velocity.Y > 83.33` then `velocity.Y *= 0.97` else
  `velocity.Y = 83.33`; play `wallslide`; **`Audio.PlaySfx("drag")` every frame**.
- **Kneel** (`Initiate_Kneel`, only if the Kneel ability, not already Kneeling, terrain OnGround, not
  jumping): if `movement == 0` -> Kneeling; else if `velocity.Y != 500` -> Sliding; then
  `velocity.Y = 0`. `Action_Kneel`: play `kneel`. A Kneeling player is **never returned to Standing by
  the idle test** (it is excluded there); he leaves by moving (`Initiate_Move` sets Running) or by
  jumping (slide jump). Releasing S alone does not stand him up. **Traced.**
- **Slide** (`Action_Slide`): `UnitSize = (3,1)` (the collision box becomes three wide and one tall,
  **and is never restored** - see section 15), `velocity.X *= 0.7`, play `slide`.

### 4.7 Wall slide, wall jump (traced behaviour)

A wall slide starts only in the air, holding toward a wall (a full or partial `wallCollision`, in the
`Facing` direction, with `movement != 0`). The slide caps the fall at 83 px/s. A wall jump is a normal
jump started from `OnWall`: `isWallJumping` flips on, which (a) negates the horizontal acceleration
each frame while there is no wall contact and (b) flips the sprite. Because the jump starts while
still touching the wall, the full-height-wall branch holds `velocity.X` at 0 for as long as contact
lasts, so in practice the first part of a wall jump is a vertical climb along the wall and the push
away only happens once contact is lost. After the apex the player falls, re-attaches if still holding
toward the wall, and so on. The sprite is flipped: `Facing == Right` flips the (left-facing) art
horizontally, and `isWallJumping` inverts that.

### 4.8 Bounds, art, animation

- Frame size is square: `FrameWidth = FrameHeight = texture.Height` (64), frame count =
  `texture.Width / 64`. Origin is the **bottom centre** of the frame (32, 64), so `Position` is at the
  feet.
- Collision box for tests (`Bounds()`): `(X - 12, Y - 51, 25, 51)` (`localBounds` 25 wide x 51 tall
  from `0.4 * 64` and `0.8 * 64` truncated; used by the map editor's drag-the-player test and the
  `Intersects` helper).
- `AnimationPlayer` (a struct) advances time in **Draw**, not Update: `time += dt; while (time >
  frameTime) { time -= frameTime; frame = looping ? (frame + 1) % count : min(frame + 1, count - 1);
  }`. `PlayAnimation(a)` restarts only if `a` is a different animation object.
- Animations per sheet (frame time in seconds, `L` loops):

| Sheet | Animation | PNG (width x 64) | Frames | Time | Loop |
|---|---|---|---|---|---|
| Morris | idle | 256 | 4 | 1.0 | L |
| Morris | run | 960 | 15 | 0.05 | L |
| Morris | jump | 192 | 3 | 0.2 | no |
| Morris | fall | 64 | 1 | 1.0 | L |
| Morris | wallslide | 64 | 1 | 0.07 | L |
| Morris | kneel | 256 | 4 | 0.07 | no |
| Morris | slide | 256 | 4 | 0.07 | no |
| Shanoa | idle, run | 640 | 10 | 0.1 | L |
| Leo | idle | 256 | 4 | 0.1 | L |
| Leo | run | 257 | 4 (257/64) | 0.1 | L |

  (Richter has `idle.png` and `run.png`, 640 wide, loaded nowhere.) A sheet that lacks an animation
  (Shanoa and Leo have only idle and run) would throw `KeyNotFoundException` the first time the
  player jumps, falls, kneels or wall slides; only Morris is playable in practice.
- `Draw`: `swap = Facing == Right`, inverted while `isWallJumping`; draws at `Position` with
  `SpriteEffects.FlipHorizontally` when `swap`. An optional debug box (`ShowBounds`, off) draws
  `cell.png` scaled. `InRenderArea()` skips drawing a player outside half a screen of the camera
  (using the camera's negative-position convention, section 7).
- Sounds from the player: `step2` (every Running frame), `drag` (every WallSliding frame).
  `jump1` exists in the sound bank but its call is commented out.

### 4.9 Reset

`R` (any of Play or the editor) sets state `Play`, camera `LoosePlayer`, and for **every** player:
`Position = (500,100)`, `Velocity = 0`, action Falling, terrain InAir. It does not clear `isJumping`,
`isWallJumping`, `movement` or `UnitSize`.

## 5. The map (`World\Map\Map.cs`, `MapEditor\GridCell.cs`)

- `new Map(new Size(1900, 1200), components)`. `Bounds() = (0,0,1900,1200)`. The player is clamped
  to it each frame (`CheckBounds`: X to [0,1900], Y to [0,1200], zeroing velocity on the clamped axis;
  there is also a stray `position.X < 0`/`Y < 0` clamp).
- **Grid**: `cellSize = 16`; `cells[1900/16 = 118, 1200/16 = 75]` of `GridCell`. A cell has
  `Position`, size, `Color`, `Asset = "tileset_default"`, `assetBounds` (the source rectangle in the
  tileset, default `(0,0,16,16)`), and `State {Hard, Soft, Empty}`. `Soft` is never assigned and never
  tested. A fresh cell is `Empty`, white at alpha 64. `GridCell.Bounds` has a setter writing
  `Size.Width` through a temporary (no effect); unused.
- **The starting level** is carved by `SpawnBridge()` in the constructor, the only authored level
  data in the game: every cell of row 30 (`y = 480..495`) is `Hard`, all columns (a 118-cell floor
  at screen y 480); every cell of **column 20** (`x = 320..335`) and **column 40** (`x = 640..655`)
  is `Hard` for all 75 rows (two full-height walls); plus `[39,30]` and `[38,30]` (already floor).
  Each of those cells is white (opaque) with `assetBounds = (16, 0, 16, 16)` (the second tile of the
  tileset's top row). The players spawn at x = 500 and 550, between the two walls, fall 380 px and land
  on the row-30 floor.
- **Queries** (all take pixel positions, divide by 16, truncate toward zero):
  `CellInRange`, `CellMatchesState`, `GetGridCoordinates(Vector2) -> Point`,
  `CheckFloorCollision(ref)`, `CheckCeilingCollision(ref, heightInCells)`,
  `CheckHorizontalCollision(ref, direction, heightInCells)` (below), `CheckIntersection` (a no-op
  X-only test; unused), `Intersects(Rectangle)` (unused).
- **`CheckHorizontalCollision`**: `facing = +-1`; `buffer = facing == 1 ? -8 : 8`;
  `x = (location.X + buffer) / 16`; `y = (location.Y - 8) / 16` (the row of the cell **one pixel row
  inside the feet**, so the floor underfoot is not a wall). It then tests the cell at `(x + facing, y)`,
  and, if the height allows, `(x + facing, y - 1)` and `(x + facing, y - 2)`; each `Hard` hit adds 1
  and (to the copy) sets `X = x * 16`. Effect: an 16-px wide player whose contact cell is the one
  8 px ahead of his centre.
- **Layers**: a `Layer` is a texture, position and colour. The map holds one, `area_1` at (0,0)
  (1920x1200, drawn white). `Layer.Update` runs a 500 ms timer that does nothing. The other four
  area images (`area_1_b1.png`, `area_1_b2.png`, `area_1_f1.png`, `area_1_f2.png`, all 1920x1200,
  evidently back and front parallax layers) are in the content project and are never loaded.
- **Outline**: `Shape box` (`Shape.cs`, a polyline made of stretched 1x1 white textures) draws the
  map rectangle as a 1 px white outline, first thing in `Map.Draw`.
- **Draw order**: outline -> layers (if `ShowLayers`, default true) -> grid cells. A cell is drawn if
  its state is `Hard` or `Soft`, or **always while the state is `MapEditor`** (then empty cells show as
  a translucent first-tile square). `ShowGrid` is stored and never read.
- **Dead data kept in memory**: `SegmentDefinition[512]`, `MapSegment[3,64]`, `Ledge[16]` (each 16
  `Vector2` nodes, a node count and flags), filled by `ReadSegmentDefinitions` and `AddSeg` and never
  drawn, collided or saved. `Tileset` (section 12) is the only list element in `TileSet`.
- There is **no persistence**: no save, load or export of the grid exists, so every editor change is
  lost on exit.

### 5.1 Segment definitions (`Content\maps.zdx`, copy in `Content\MapEditor\maps.txt`)

A text file: a header line `Map Defs`, `#src N` lines selecting the source image (1 = `maps1.png`,
1024x1024), and per definition three lines: name, `x0 y0 x1 y1` (the rectangle corners; width and
height are differences), an integer flag. Eleven definitions: `grass1` (0 26 410 178), `grass2` (0 178
404 369), `grass3` (0 369 411 564), `rock1` (423 28 511 93), `rock2` (438 92 491 137), `grave1`
(432 140 503 220), `grave2` (430 220 499 292), `block1` (512 0 611 93), `block2` (514 93 676 187),
`blackbox` (16 570 378 796), `torch` (437 300 479 409, flag 1; the others 0). Parsed into
`segDef[0..10]`; the original editor UI that would have placed them (`Managers\MapEditor.cs`, an
unreferenced draft, section 12.3) was replaced by the tile editor before the game was finished.

## 6. Rendering of the world (`Managers\SceneManager.cs`)

Three `SpriteBatch`es (`sbWorld`, `sbPlayer`, `sbText`), each begun with
`SpriteBlendMode.AlphaBlend`, `SpriteSortMode.Immediate`, `SaveStateMode.SaveState` and the
**camera's `TransformationMatrix`**, with an `Effect` pass wrapped around the drawing:

1. Clear to CornflowerBlue.
2. `DrawMap`: effect `filter`, then `map.Draw` (outline, layers, cells), then, in the editor state,
   `MapEditor.DrawToWorld` (the world-space cursor).
3. `DrawPlayers`: for each player inside the render area, `player.Draw`.
4. `DrawText`: for each player in the render area, the name is written twice with Arial 10, centred
   horizontally and `Top`-aligned: red at `(X - 1, Y - 100)` and yellow at `(X, Y)`. The `Top`
   alignment shifts the text up by `stringHeight * fontSize(10) * 0.4`, about 60 px for Arial 10, so the
   yellow name sits above the head and the red one about 100 px above that.

The effect used by all three is `effect = "filter"` (a string field; the `"negative"` line is
commented out). Its parameters are left at their shader defaults (the `Parameters[...]` assignments are
commented out), but `refractSampler` is never bound.

### 6.1 Shaders (`Content\Effects\*.fx`, all `ps_2_0`)

All five are built and loaded into `EffectManager`; only `filter` is ever used.

- `filter.fx` (used): parameters `burn 0.01`, `saturation 1.0`, `r g b 1.0`, `brite 0.0`. It samples
  `refractSampler` (unbound) to get a small texture-coordinate displacement `GetDif * 0.1`, darkens by
  `distance(tex, centre) * burn` (a vignette), applies saturation as
  `col = (col * sat + avg * (1 - sat)) * channelGain`, and adds `brite`. With the defaults it is a mild
  vignette per sprite, texture-coordinate-local (distance is measured within each sprite's 0..1 UV).
- `negative.fx`: `col.rgb = 1 - col.rgb`. Selected by commented-out code only.
- `bloom.fx`: 13-tap blur (12 Poisson offsets scaled by `v = 0.01`, averaged over 13), `col.a = 0.2`.
- `pause.fx`: 12-tap blur at `0.005` followed by desaturation to the average.
- `water.fx`: a rippled reflection (`horizon 0.5`, `delta`, `theta` time parameters) darkened to 0.35,
  with an alpha ramp over `y` 0.2..0.25.

## 7. The camera (`Managers\CameraManager.cs`)

`ICameraManager` exposes `Position`, `Velocity`, `Speed`, `Rotation`, `Scale`, `AspectRatio`,
`TransformationMatrix`, `Mode`, `RenderArea`, `Screen`, and the scroll/zoom/rotate methods.

- **Convention**: `Position` is the **negative** of the world point at the screen centre. The
  matrix is `Scale(s^3, s^3, 0) * RotationZ(rotation) * Translation(W/2 + Position.X, H/2 +
  Position.Y, 0)` (the cube makes `+0.01` per tick an accelerating zoom).
- Initial: `Position = (1,1)`, `Rotation = 0`, `Scale = 1`, mode **`LoosePlayer`**.
- Each `Update`, by mode: `Free` does nothing; `LockPlayer` sets `Position = -player.Position`;
  `LoosePlayer` calls `MoveToLocation(player.Position)`; `LooseCursor` does the same toward
  `MapEditor.Cursor.Position`.
- `MoveToLocation(L)`: `run = -(|L.X| - |Position.X|)`, `rise` likewise, then `velocity += 0.02 *
  (run, rise)`. Then `|velocity| < 0.01` snaps to 0, `Position += velocity`, `velocity *= 0.7`, then
  `CheckBounds`. The absolute values make this correct only while both world coordinates are
  non-negative, which they always are (the map is at the positive quadrant). The result is a damped
  ease toward the target, a second-order lag rather than a snap.
- `CheckBounds`: `-Position.X` is clamped to [0, `Map.Width`], `-Position.Y` to [0, `Map.Height`]
  (zeroing the velocity on a clamped axis). The camera **centre** never leaves the map, so half a
  screen of clear colour can show past an edge.
- Manual scroll keys add +-1 (or the amount passed, the editor uses 2 and 4) to `velocity`, so scrolling
  is acceleration that the 0.7 damping settles; `ZoomIn/Out` add +-0.01 to `Scale` (no clamp - a scale
  of 0 or below inverts or collapses the view); `RotateLeft/Right` add -+0.01 rad.
- `CycleCameraMode` (C): Free -> LockPlayer -> LoosePlayer -> LooseCursor -> Free.
- `RenderArea` is computed with mixed corner/size arithmetic and is read nowhere;
  `Speed`, `Target`, `AspectRatio` are unused.

## 8. Cursors (`MapEditor\Cursor.cs`, `Cursor2D.cs`)

`Cursor` is a sprite (`cursor_hand_topleft.png`, 13x18, loaded as `"cursor"`) with two modes:
`World` (position = camera centre, and the mouse's offset from the screen centre scrolls the
camera by 2 per frame beyond a 2 px dead zone, left/right/up/down) and `Screen` (position = the OS
mouse, clamped inside the window). Colour: left button yellow, right red, else white. Helpers
`Bounds`, `FingerTip` (a quarter-size box), `IsOver`, `Intersects`, `CenterOnCell`,
`MoveCellLeft/Right/Up/Down` (scroll the camera by 4, or by `amount * 16`), an unused `ScrollMode`
enum and a `ChangeMode` that cannot change anything (three identical `else if` branches). Two
instances exist: the GUI cursor (Screen mode, drawn only over the main menu) and the editor's
(World mode). `Cursor2D.cs` is an earlier cursor, present in the folder but not in the project file.

## 9. GUI (`Gui\*`, drawn by `GuiManager.Draw` with a plain `SpriteBatch.Begin(AlphaBlend)`)

Draw order inside `GuiManager.Draw`: portrait, quick bar, lenses (fade), sun, the (empty) `Hud`
dictionary, then **either** the main menu and its cursor **or** the editor's GUI (palette and
cursor), then the console, then the debug text, then the message stack.

### 9.1 Always-on HUD

- **Portrait** (`Portrait`): at `(0, H - 110)`, 99x110, three layers in order glass
  (`portrait_glass`, alpha 0.4), photo (`portrait_1`, 84x94, alpha 1), frame (`portrait_frame`).
  Static: the photo never changes with the selected player. The other six portraits
  (`portrait_archer/paladin/priest/rogue/soldier/wizard`) are loaded and unused.
- **Quick bar** (`QuickBar`): glass (alpha 0.4) and frame (`quick_bar_glass/frame`, 261x105) at
  `(103, H - 105)`. `U` toggles it: it slides 3 px per frame down off the screen (to `y = H`) or up
  (to `H - 105`); toggling is ignored mid-slide. It holds nothing.
- **Action cross** (`ActionCross`): a 145x145 glass/frame with four 48x48 slots (top, left, right,
  bottom icons: `icon_selectsquad` then `blank`s) and red/white activate-deactivate methods. Its
  construction, update and drawing are commented out in `GuiManager`; it never exists at runtime.
  `SetActionIcons` is an empty shell of commented lines.
- **Debug text** (Arial 10, white, drawn every frame in every state):
  `Name\nX, Y\ncellX, cellY` of the current player at (100,100); `Mode: <CameraMode>\ncamX, camY` at
  (300,100); the GUI cursor position at (600,100) (written twice in the same place); `Coords:` + the
  editor's selected cell at (700,100); the frame rate `1 / dt` at `(W - 100, 10)`. (With a fixed
  1/60 step it always reads 60.)
- **Sun / daylight** (`World\Sun.cs`): intended day-night tint cycle: `PeriodOfDay {Day, Dusk, Night,
  Dawn}` with colours Day `(255,255,255,0)`, Dusk `(255,0,0,64)`, Night `(128,0,255,64)`, Dawn
  `(128,128,128,0)`, a 10 ms tick that ramps two alphas by 1 and advances the period, drawn as two
  800x600 white rectangles tinted by `lastPeriod` and `currentPeriod` over the whole screen. **It never
  runs**: the tick's `Timer` is only advanced by `UpdateTimer`, which nothing calls, so the cycle
  stays frozen and both tints stay `(0,0,0,0)`: invisible. `FadeToColor` is a half-written helper
  (it reads the red channel for all four). `Gui\Daylight\` is an empty folder in the project.
- **Messages** (`MessageHandler`): a list of `(text, position, colour)`. `AddMessage` appends at
  `(W/2, H/2 - 16)`. Every 50 ms of accumulated time, for each message `m` in order, **if** `m == 0`
  or its `Y + 16 < messages[last].Y`: `Y += 1` and its alpha `-= 1`, removing it at alpha 0 (the
  "last" index is whichever moved last). Effect: messages drift down and fade, each starting only
  once the one before it is 16 px ahead, so they queue as a falling, fading stack. A message is
  **drawn only while `Y > H/2 + 16`**, i.e. after the first 33 ticks (1.65 s), and lives 255 ticks
  (12.75 s). The constructor queues six test messages "test1".."test6" (Wheat), so they play out
  shortly after launch. `InvokeMessage` overwrites message 0's text and colour (used for the gamepad
  connected notice). Messages are drawn with Arial 10 at their position.
- **Lenses / fade** (`Lenses`): one full-screen lens `fade`, a 1x1 white texture stretched to W x H,
  tinted `(0,0,0,0)`. Every `Update` where the accumulated time exceeds 10 ms (every frame at 60 Hz),
  `CheckFade`: `Out` adds 1 to the alpha until 255 then sets state `Off`; `In` subtracts 1 until 0 then
  `Off`; `Off` **resets the tint to `(0,0,0,0)`**. So a `fade out` takes 255 frames (4.25 s) to
  reach black and then, the next frame, snaps back to clear; `fade in` from a clear start does nothing
  visible. Driven only by the console commands `fade in|out|off`.

### 9.2 Main menu (`Gui\MainMenu\*`, section 3 for input)

Background `menu1.png` (543x742) stretched over the whole screen at alpha 0.8; an `arrow1` indicator
(16x16) at `(item.x - 18, item.y)`; items in Lucida Console 14 pt, base colour Yellow, selected Red, at
`x = 50`, `y = 100 + 50 * index`; a title at (10, 15) and a description line at `(10, H - 20)`, both
white. Three levels:

| Level | Parent | Items (index order) |
|---|---|---|
| 0 (root, title "Main Menu") | none | Game, Video, Audio, Controls, Quit |
| 1 ("Main Menu : Game") | 0 | "1-0", "1-1" (placeholders) |
| 2 ("Main Menu : Video") | 0 | "Toggle Fullscreen", "2-1" (placeholder) |

Descriptions (by item text): "Game : Change various game settings", "Video : Change video settings",
"Audio : Change audio settings", "Controls : Change game controls", "Quit : Quit game", "Toggle
Fullscreen : Switch between Fullscreen and Windowed mode"; everything else is empty. Titles for levels 3
("Controls : Change game controls") and 4 ("Quit : Quit game") exist and are unreachable.
Behaviour: Up/Down move with wrap and play `menu_move`; Right/Enter/D/pad-right/stick-right/A selects
(plays `menu_select`): root Game -> level 1, Video -> level 2, Audio and Controls do nothing, **Quit
exits**; "Toggle Fullscreen" toggles; every select then resets the highlight to item 0 of the current
level. Left/A-key/pad-left/B/stick-left goes to the parent level, or **closes the menu from the
root**. F12 toggles fullscreen inside the menu. The mouse is not used by the menu.

## 10. The console

### 10.1 Window (`Gui\Console\Console.cs`)

A 200 px tall panel (`console1.png`, 608x203, stretched to W x 200, alpha 0.75) that starts hidden at
`y = -200`. Toggling (`~` / pad Back) while not already sliding starts a slide and flips `Visible`;
`Command_ToggleConsole` also plays the ambience cue `machine_1` if the panel is taller than 200 (only
true after `console full`) and `machine_1_short` otherwise. Sliding **down** while `Y < 0` advances
`Y += (int)(1 + 1 * (1 - Y/16))` using integer division (14 px at the top, easing to 2 px), then snaps
to 0, stops sliding and sets the game state to `Console`. Sliding **up** moves 12 px a frame (and
sets `Play` every frame), then hides at `-height`. While the state is `Console` it draws, in Arial 10:
the prompt `>` in yellow at `(6 or 4, height - 20)` and the typed text (colour `Colors["ConsoleText"]`,
default white) at `(16, height - 20)` with a `|` caret, alternating every 500 ms (the prompt shifts
2 px with the blink); then the history, 100 lines, line `h` at `(2, height - 34 - 14 * h)` in
`Colors["ConsoleHistory"]` (default light grey). Submitting pushes the typed text to `History[0]`
(shifting the rest down) before the script runs, so command output assigned to `History[0]` replaces
the echoed command.

### 10.2 Typing (`CheckInput_Console`)

Esc and `~`/Back toggle menu and console as in Play. Enter submits (add to history, run the script,
clear the text, `HistoryIndex = 0`); Delete clears the line; Backspace removes the last character on
press, or every frame while Shift is held; Up recalls `History[HistoryIndex]` and increments the index
(bounded by 100); Space appends a space; digits, `-`, `=`, `[ ] \ ; ' , . /` append their characters, and
with Shift the shifted symbols (`! @ # $ % ^ & * ( ) _ + { } | : '' < > ?`, note the **double**
apostrophe for Shift+`'`). Anything else newly pressed (and not Left Shift) appends
`key.ToString().ToLower()`: letters give a lowercase letter, but Right Shift, arrows, Tab, F-keys and
the like append their names ("rightshift", "left", "f5"). There is no uppercase and no cursor
movement.

### 10.3 Script language (`Managers\ScriptManager.cs`)

`ExecuteScript(string)` lowercases the line, then: exact matches `play music` and `play battle`
(`MediaPlayer.Play` of the two songs), `fade in` / `fade out` / `fade off` (sets `Lenses.Fade`), and
`msg hi` (queues "Hello!" in Wheat). Then it splits on spaces and runs, in order:

- `say <words>`: a message `''<words>''` (two apostrophes each side) in Yellow. `yell <words>`:
  `''<WORDS>!''` upper-cased in Red.
- By word count, a switch on word 0:
  - 1 word: `exit`/`quit` (exits the game); `version`/`ver` (`History[0] = "Version 0.0.1"`);
    `pause`/`unpause` (toggles `MediaPlayer`, message "Music Paused"/"Music Unpaused",
    LightSteelBlue); `mute`/`unmute` (toggles `Audio.Muted`, sets `MediaPlayer.Volume` to 0 or the
    audio volume, message "Mute: On|Off"); `lee`/`buzzer` (writes a nine-line "LEE" heart banner into
    `History[0..8]`).
  - 2 words: `run`/`execute` (do nothing); `console clear|full|text|history|bg`; `commander ...`.
  - 3 words: `console text|history|bg <colour>` and `console clear|full ...`.
  - 4 words: `commander <x|y> ...` (empty handlers). 5 words: `unit text|history|bg|full ...` (the same
    parameter handlers).
- Parameters: `console text <colour>` and `console history <colour>` accept `red`, `white`, `blue`,
  `yellow`, `green`, `black` (the matching XNA `Color`); `console bg red` sets the panel texture to
  `red.png` (1x1) - `red` is the only colour handled; `console full` sets the panel height to the viewport
  height (600), which also makes the next toggle play the long ambience cue; `console clear` blanks
  `History[1..99]` (not index 0).
- Anything unrecognised does nothing and gives no error. `console text` or `commander x` with **too few
  words** reads `currentScript[2]` past the end and throws `IndexOutOfRangeException`, which crashes
  the game (a defect, section 15).

## 11. Audio (`Managers\ResourceManagers\AudioManager.cs`, `Content\Audio\*`)

Two systems side by side:

- **XACT**: `audio.xap` (project), built to `Content\Audio\Win\audio.xgs`, `audio.xwb` (a 12.4 MB wave
  bank), and three sound banks `music.xsb`, `sfx.xsb`, `ambience.xsb`. Wave bank `audio` holds 13 waves
  (`music1`, `footstep_1`, `footstep_2`, `machine_1`, `machine_1_short`, `menu_close`, `menu_open`,
  `menu_move`, `menu_select`, `drag`, `step1`, `jump1`, `step2`). Cues, per bank (volumes in
  hundredths of a dB):

| Bank | Cue | Wave | Volume | Played by |
|---|---|---|---|---|
| `music` | `music1` | music1 (64.5 s) | -1200, loop 255 | never |
| `sfx` | `footstep_1`, `footstep_2` | likewise | -2000 | never |
| `sfx` | `drag` | drag (0.26 s) | -1200 | wall slide, every frame |
| `sfx` | `step1` | step1 (1.0 s) | -1200 | never |
| `sfx` | `jump1` | jump1 (0.67 s) | -1200 | commented out |
| `sfx` | `step2` | step2 (0.43 s) | -1200 | running, every frame |
| `ambience` | `machine_1` (1.2 s), `machine_1_short` (0.6 s) | likewise | 0 | console toggle |
| `ambience` | `menu_open` (0.84), `menu_close` (0.73), `menu_move` (0.22), `menu_select` (0.87) | likewise | 0 | menu |

  Categories `Global`, `Default`, `Music` (background music), `Sfx`, `Ambience`, each with up to 255
  instances. `AudioEngine`, `WaveBank`, `sfx` and `ambience` `SoundBank`s are created in
  `Initialize`; the `music` bank is never loaded. `bonga.wav` (9 KB, an unsupported WAV format) is in
  the folder and not in the bank.
- **Songs** via the content pipeline: `Content\Audio\Music\music.mp3` (7.1 MB) and `battle.mp3`
  (5.0 MB), loaded as `Song`s named `music` and `battle` (built as `.wma`), played only by the console
  commands (`MediaPlayer.Play`, no loop, no auto-start; the "restart when stopped" line is commented
  out).
- `Volume = 1.0`, `Muted = false`. `PlayMusic/PlaySfx/PlayAmbience` do nothing while muted; `mute`
  changes `MediaPlayer.Volume` but cues already started are unaffected. `RandomizeSongPosition`
  reads the duration and does nothing. `AudioManager.Update` is empty. `g.ContentDirectory.Audio_Sfx`
  and `Audio_Ambient` point at folders (`Content\Audio\Sfx\`, `\Ambient\`) that do not exist.

## 12. The map editor (`MapEditor\*`, `Managers\MapEditor.cs`)

### 12.1 The working editor (`MapEditor\MapEditor.cs`)

Entered with M / middle mouse; the camera goes to `Free`. The editor owns a `Cursor` (World mode) and a
`TilePalette`. While the state is `MapEditor`, every frame:

- **World mode** (Space not held): the cursor follows the camera centre, and the mouse's offset from
  the screen centre scrolls the camera (section 8); then **the OS mouse is warped back to the screen
  centre** (`Mouse.SetPosition`) if the window is active, so the mouse acts as a joystick. The selected
  cell is `GetGridCoordinates(cursor.Position)`. Left button down over a player's `Bounds()` sets
  that player's position to the cursor (drag a player; the player keeps falling when released).
  Left button down also **paints** the cell under the cursor: white, `Hard`, source rectangle =
  `selectedTile * 16` (16x16). Right button erases it: alpha-64 white, `Empty`, rectangle (0,0,16,16).
  With no button, the cell under the cursor is tinted `Yellow` as a highlight, and the previously
  highlighted cell (if still yellow) reverts to white (solid) or alpha-64 (empty). There is no bounds
  check on the selected cell, so scrolling to the 1900 px edge (column 118 is out of range) can
  throw. **Traced.**
- **Screen mode** (Space held): the cursor follows the OS mouse; the palette's `SelectedTile` is set from
  the cursor position: `round((cursor - Offset) / 16)` clamped to X `[0, tilesX - 1]` and Y
  `[0, tilesY]` (an off-by-one: Y can reach the row one past the last).
- A/D/W/S (or D-pad) press: step `SelectedTile` by one (left clamps at 0, right is unbounded) unless
  Ctrl, and scroll the camera by 4 unless Shift.

### 12.2 Palette (`TilePalette`, `Tileset`, `Tile`)

`TilePalette`: a translucent 300x295 panel (`tilepalette_background`, alpha 0.4) at `(5, H - 305)`,
the tileset drawn at `Offset = (16, 300)`, and a 16x16 `square_selector` at `Offset + SelectedTile * 16`.
`Tileset("tileset_default")` is `Tileset\default.png`, 303x173, so 18 x 10 tiles of 16 px (the image is a
Mario-style tile sheet: question and brick blocks, grass, dirt and water squares, logs, a spiked ball,
ice, a wooden crate). `Tile` stores `X, Y, Width, Height, Color (255,255,255,128)`; but
`Tileset.LoadTileSet` calls the two-argument constructor `Tile(x * 16, y * 16)` which sets **width and
height** instead of position, leaving every tile's `(X, Y)` at (0,0): the palette therefore draws the
**whole sheet 180 times at the same spot** at half alpha rather than tile by tile, which still looks
like the sheet. `Tile.Bounds/IsOver/Position/Coordinates` exist for the intended layout and are unused.

### 12.3 The dead draft (`Managers\MapEditor.cs`, not in the project file)

An earlier editor for the segment system (layer back/mid/fore switch, `SegmentSelection | CollisionMap |
Ledges` draw types, a `Ledges` node editor, a segment list of 9 rows with drag-to-place and layer
parallax scalars 0.375 / 0.5 / 0.625). It references members that do not exist (`Scene.Gui.TextWriter`,
`Map.Grid`) so it does not compile and is excluded. It documents the intended design of the unused
segment and ledge data.

## 13. Content inventory (`Engine\Content`)

"Loaded" = referenced by a texture/font/effect/audio manager. "Used" = actually drawn or played in a
running game. Dimensions in pixels.

| Folder | Files | Loaded as | Used |
|---|---|---|---|
| `Areas` | `area_1.png` 1920x1200; `area_1_b1`, `_b2`, `_f1`, `_f2` (1920x1200) | `area_1` only | `area_1` is the backdrop: mountains and a farmhouse on an olive-grove hill under cloud |
| `Characters\Playable\Morris` | `idle` 256, `run` 960, `jump` 192, `fall` 64, `kneel` 256, `slide` 256, `wallslide` 64 (all x 64) | `morris_*` | the player |
| `...\Shanoa`, `...\Leo` | `idle`, `run` (640 / 256, 640 / 257 wide) | `shanoa_*`, `leo_*` | only if the sheet field is edited |
| `...\Richter`, `Playable\Untitled.png` (768x64), `Characters\frame_10.png` (640x64) | idle/run strips | no | no |
| `Gui\Hud` | `portrait_frame`, `portrait_glass` (99x110), `quick_bar_frame`, `quick_bar_glass` (261x105), `action_cross_frame`, `_glass` (145x145), `arrow1` (16x16), `bar_health` (135x17), `demo_gui`, `demo_gui_0`, `gui_0`, `gui_1` (800x600 mock-ups) | all but the 800x600 mock-ups | portrait, quick bar, arrow; the rest unused |
| `Gui\Portraits` | 7 portraits, 84x94 (`portrait_1`, archer, paladin, priest, rogue, soldier, wizard) | all | only `portrait_1` |
| `Gui\Icons` | 4 action buttons 48x48 (`action_A/B/X/Y`); 29 skill icons 64x64 or 48x48 (assassinate, attack, aura, battlecry, bow, consecrate, crossbow, falcon, fireball, flashheal, icewall, lifewell, lightning, mace, might, parry, phalanx, poisondart, pray, redemption, regen, repair, selectsquad, shield, shoot, stab, trap, vanish, wand) | all but `icon_consecrate` (both `icon_poisondart.png` and `icon_consecrate.png` exist as files); `icon_poisondart` is loaded from `icon_parry.png` (a typo) | none (ActionCross is off) |
| `Textures` | `menu1` 543x742; `console1` 608x203; `cursor_hand_topleft` 13x18; `white`, `red`, `blank` 1x1; `brick` 512x512; `block` 76x76; `console2` 608x203; `foggy_woods` 500x347; `menu2` 600x742; `metal1` 800x600 | `menu1, console1, cursor, white, red, blank, brick, console2, foggy_woods, metal1` (not `block`, `menu2`) | menu1, console1, cursor, white, red |
| `MapEditor` | `1x1.bmp` (loaded as `null`), `cell.png` 16x16, `block.png` 16x16, `icons.dds`, `maps1.png` 1024x1024 (the segment atlas), `maps.txt` | `null, cell, block, icons, maps1` | `null` (the outline and fills), `cell` (debug box) |
| `TilePalette` | `square_selector` 16x16, `tilepalette_background` 300x300 | both | editor |
| `Tileset` | `default.png` 303x173 | `tileset_default` | the tiles of every grid cell |
| `Particles` | `particle_bug` 9x9, `_leaf` 18x20, `_rain` 4x4, `_sand` 3x3, `_snow` 9x9 | no | no (the `Particles\` folder in the project is empty) |
| `Selectors` | `selector`, `_S`, `_M`, `_L`, `_XL` (64x64) | no | no |
| `SpriteSheets` | 16 character sheets: hunter, knight, leo (256x513), magitek_brown, magitek_yellow, paladin, priest, rogue, soldier plus blue/brown/gray/halo/red/yellow variants, wizard (256x256 each) | no (`RandomSkin()` names six soldier variants but is never called) | no |
| `Banners` | `banner_1` ... `banner_15` (64x64) | no | no |
| `Fonts` | `Arial_10.spritefont` (Arial 10), `Lucida.spritefont` (Lucida Console 14), `Webdings_14.spritefont` (Webdings 14); region 32-126 | Arial, Lucida, Webdings | Arial (all game text), Lucida (menu items); Webdings unused |
| `Effects` | `bloom`, `filter`, `negative`, `pause`, `water` | all five | `filter` |
| `Audio` | section 11 | | |
| `Application` | `GameThumbnail.png` 64x64 | ClickOnce | no |
| `maps.zdx` | section 5.1 | read as text | parsed, unused |

Counts: 132 PNGs, 14 WAVs, 2 MP3s, 5 effects, 3 fonts in the source `Content`; 140 compiled `.xnb`
files in the Debug output.

## 14. Name tables

`g.RandomName` (section 4.1) holds the one real "data file" besides `maps.zdx`: about 5,300 first
names hard-coded in `Core\Global.cs`. The rebuild keeps them as a data asset (a text file) and keeps
the `Random.Next(Length - 1)` selection only if parity of "never the last name" is wanted (decision in
the BIBLE).

## 15. Defects and dead ends found while reading (traced)

Each is decided in the BIBLE: keep (it is the game's feel), fix, or drop.

1. `Initiate_Jump` while Kneeling enters Sliding with `isJumping = true`, and the OnGround+jumping
   branch tests only the ceiling, so the player **cannot fall off a ledge, run, or stand while sliding
   from a kneel jump** until a `R` reset or leaving the ground another way.
2. `Action_Slide` sets `UnitSize` to (3,1) and nothing restores it, so after the first slide every
   wall test uses a one-cell height (`wallCollision == 1`).
3. A kneel is never left by releasing S (the idle test excludes `Kneeling`).
4. `step2` and `drag` start a cue **every frame** while running or wall sliding.
5. Shift, or pad A held, freezes the move branch without stopping the player.
6. A wall jump's horizontal push is cancelled while still touching the wall (section 4.7).
7. `Player.Initiate_Move` only starts Running on the ground and not jumping; at terminal-speed landing
   while moving the action stays `Falling` with terrain `OnGround` for a frame (traced).
8. `g.RandomName` never returns the last name; a `Gender.Other` returns null.
9. `Tile(x, y)` constructor mix-up (section 12.2); the palette Y clamp is off by one.
10. Editor cell index has no bounds check at the right and bottom edges, and the palette's right step is
    unbounded (stepping past the last tile selects a tile off the sheet).
11. Console commands with too few words crash the game; `console clear` spares `History[0]`.
12. The Sun cycle and the ActionCross never run, and the portrait and quick bar are static pictures.
13. `Lenses.Off` resets the tint, so a finished `fade out` flashes clear.
14. `icon_poisondart` loads the parry icon; `icon_consecrate` is never loaded.
15. Unused code that is in the project: `Quad.cs` (a quadrant manager from "BlaqBox"), `Core\LinkedList`
    (1,461 lines), `SpriteSheet` and `MapEditor\TileGrid` (the XNA "TiledSprites" sample), the `Shape`
    ellipse and circle helpers, `Event`/`Zone`/`EventManager`, `Time` (an empty class), the `Layer` timer.
    Not in the project file at all: `Gamepad.cs` (a DirectInput `MyGamePad` wrapper that needs a
    `Soopah.Xna.Input` library which is not here), `Cursor2D.cs`, `Managers\MapEditor.cs`.
16. `CyclePlayerLeft/Right` only change `playerIndex`; the character switched away from keeps its `movement`, so one
    left mid-run keeps running (and wall sliding at a wall) with no one driving it (traced).

## 16. What the original does not have (so the rebuild must not invent it)

Enemies, NPCs, items, health or damage (a `bar_health.png` and a `fallDamageTime` stub only), multiple
rooms or levels, doors, any scripted event, saving or loading of anything (maps included), a title
screen, a win or lose state, a pause (the menu stops the world), settings that persist, a working
Audio/Controls/Game menu page, gamepad rebinding, or any dialogue. The Castlevania flavour is in the
sprite names (Shanoa and Richter from the *Castlevania* series, Morris, Leo) and nothing else.

## 17. The earlier copy (`XNA 2D\ProjectVania`)

An earlier, smaller snapshot of the same project: 262 files, about 12,960 lines, the same component
architecture under different names (`Entity\Player.cs` for `Player\Player.cs`, `Core\Function.cs` for
`Core\Global.cs`, `Core\Constant.cs` and `Core\Datatype.cs`, `Gui\Daylight\Daylight.cs` for `World\Sun.cs`).
It is closer to the XNA platformer starter kit it grew from: **one** player (spawned at (100,100)), no
`Map` or grid at all (the "floor" is a hard-coded `Position.Y > 300` test), no map editor, no
`maps.zdx`. Differences that matter:

- **Tuning**: `MaxMoveSpeed = 2000` (vs 200), `MaxJumpTime = 0.35` (vs 0.6), `JumpLaunchVelocity =
  -4000` (vs -1100), `GravityAcceleration = 3500` per **second** scaled by dt (vs 10 per frame),
  `MaxFallSpeed = 600` (vs 500); positions are rounded to whole pixels each frame.
- **Absent** from the earlier copy: `AbilityList`, wall slide and wall jump, kneel and slide, Leo and
  Richter art, Morris's fall/jump/kneel/slide/wallslide sheets (it has `idle` and `run` only), the
  `cursor_hand_topleft` texture, the sounds `drag`, `jump1`, `step1`, `step2` and the `Xbox` sound-bank
  folder, and the five shaders `bloom`, `filter`, `negative`, `pause`, `water`.
- **Present only in the earlier copy**: five effects `basic`, `pointsprite`, `rain`, `snow`, `riemer` (plus
  `Effectsackups`), loaded into `EffectManager` by name and **never used by any code in either copy**
  (no rain or snow system exists in the code; only the particle PNGs name the idea), and a `New Folder`
  placeholder under `Characters\Playable`.

The rebuild follows the fuller copy. The earlier copy is recorded so that nothing in it is mistaken for
a lost feature; the five dropped effects are the only content that exists in neither the fuller copy
nor any code, and the rebuild treats them as out of scope (the BIBLE records the decision).

## 18. Open questions (need an owner decision or play to settle)

1. **Fix or keep the traced defects of section 15** - the BIBLE proposes a default for each; the owner
   may overrule.
2. Whether the second player should be a controllable second character (the original is one active
   player plus an idle twin) or stay a spectator mannequin.
3. The window size: 800x600 is inferred; the back buffer was never set in code.
4. Whether music should start by itself (the original never played it unless a console command asked).
5. The refract sampler of `filter.fx` is unbound in the original; the rebuild decides the look
   (the BIBLE chooses the plain vignette).
6. The Sun, ActionCross, portrait swap and quick bar contents are scaffolding with no designed content;
   the BIBLE keeps them as inert, data-described pieces.

## 19. Build order (the stories)

The stories below are filed in `docs/USER_STORIES.md` as epic `U.VAN-A`, in this order, so each builds on
the last and full parity is reached by building them in sequence. The section is the one that defines the
behaviour each story must reproduce.

| Story | Builds | Reconstruction |
|---|---|---|
| A1 | Core assembly, 60 Hz tick, command and event model, player-id seam | 0, 1, 2 |
| A2 | Data-driven tuning, content and settings registry | 4.2, 5, 9, 13 |
| A3 | URP, Input System, Unity.Shared, code-built scenes, project hygiene | 0 |
| A4 | Input bindings per game state, keyboard and Xbox pad | 3 |
| A5 | The 16 px grid, the starting bridge level and collision queries | 5 |
| A6 | Player physics and action state machine | 4.2-4.7, 4.9 |
| A7 | Player characters, animation and rendering, names, two players | 4.1, 4.8 |
| A8 | Backdrop, tile and outline rendering | 5, 6 |
| A9 | Camera modes, ease, bounds, scroll, zoom, rotate | 7 |
| A10 | Game states, main menu and fullscreen | 2, 9.2 |
| A11 | The console window, typing, script language | 10 |
| A12 | HUD, message stack, fade lens, debug readout | 9.1 |
| A13 | Audio banks, cues, songs, mute and pause | 11 |
| A14 | The map editor | 8, 12 |
| A15 | Post-process effects | 6.1 |
| A16 | Everything unwired in the original, kept as data | 5.1, 9.1, 15 |
| A17 | Asset parity sweep and the live-asset diff test | 13 |
| A18 | End-to-end scripted verification | all |
