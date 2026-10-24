using System;
using System.Collections.Generic;
using Unity.ProjectVania.Core.Characters;
using Unity.ProjectVania.Core.Commands;
using Unity.ProjectVania.Core.Settings;
using Unity.ProjectVania.Core.Viewing;
using Unity.ProjectVania.Core.World;

namespace Unity.ProjectVania.Core.Simulation
{
    /// <summary>Something that issues commands at the start of every step (a driver's controls, a replay).</summary>
    public interface IStepInput
    {
        /// <summary>Called before each step, with the world as the step finds it; submits that step's commands.</summary>
        void BeforeStep(GameWorld world);
    }

    /// <summary>
    /// The rules and state of the game. State is read through get-only properties and changes only inside
    /// Step, by applying queued commands, each producing events. Frame order within a step
    /// follows the original (RECONSTRUCTION 1): input commands first, then the systems in order.
    /// </summary>
    public sealed class GameWorld
    {
        readonly Queue<ICommand> _pending = new Queue<ICommand>();
        readonly List<IGameEvent> _events = new List<IGameEvent>();
        readonly FixedStepper _clock = new FixedStepper();

        /// <summary>A world with no map (the state and command tests that need no grid).</summary>
        public GameWorld()
        {
        }

        /// <summary>A world on <paramref name="map"/> (the bridge level, <see cref="Map.FromSettings"/>) with no characters.</summary>
        public GameWorld(Map map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
        }

        /// <summary>
        /// A world on <paramref name="map"/> with one character at each of player.json's spawn positions
        /// (<c>SceneManager.LoadContent</c>: two, at (500, 100) and (550, 100)), each <paramref name="unitSize"/> cells;
        /// with <paramref name="camera"/>, a camera for each character's player (<see cref="Camera"/>).
        /// </summary>
        public GameWorld(Map map, PlayerData player, Pair unitSize, CameraData camera = null)
            : this(map)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            foreach (var spawn in player.SpawnPositions)
            {
                _characters.Add(new PlayerCharacter(player, unitSize, new PixelVector(spawn.X, spawn.Y)));
            }

            AddCameras(camera);
        }

        /// <summary>
        /// The shipped world: one <paramref name="character"/> at each of player.json's spawn positions, each given a
        /// random name from <paramref name="names"/>' table for its gender drawn with <paramref name="random"/>
        /// (<c>new Player(position, gender)</c> and <c>g.RandomName</c>, RECONSTRUCTION 4.1); with <paramref name="camera"/>,
        /// a camera for each character's player (<see cref="Camera"/>).
        /// </summary>
        public GameWorld(Map map, PlayerData player, Character character, NameData names, Random random, CameraData camera = null)
            : this(map)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (random == null) throw new ArgumentNullException(nameof(random));
            foreach (var spawn in player.SpawnPositions)
            {
                var name = RandomName.Pick(names.For(character.Gender), random);
                _characters.Add(new PlayerCharacter(player, character, new PixelVector(spawn.X, spawn.Y), name));
            }

            AddCameras(camera);
        }

        // Player n starts on character n (DrivenIndex), so each character's player gets a camera; a world built without
        // camera data has none, and camera commands then do nothing.
        void AddCameras(CameraData camera)
        {
            if (camera == null) return;
            for (var i = 0; i < _characters.Count; i++)
            {
                _cameras.Add(i, new WorldCamera(camera));
            }
        }

        readonly List<PlayerCharacter> _characters = new List<PlayerCharacter>();
        readonly PlayerData _player;
        ActionKind[] _actionsBefore = Array.Empty<ActionKind>();

        // Which character each player drives, once [ or ] has moved them off their own (the original's
        // SceneManager.playerIndex, one per player instead of one global).
        readonly Dictionary<int, int> _driven = new Dictionary<int, int>();

        // Each player's camera (the original's one CameraManager, one per player instead of one global), stepped in
        // player order, and where each player's map-editor cursor is (MapEditor.Cursor.Position, which LooseCursor follows).
        readonly SortedDictionary<int, WorldCamera> _cameras = new SortedDictionary<int, WorldCamera>();
        readonly Dictionary<int, PixelVector> _editorCursors = new Dictionary<int, PixelVector>();

        /// <summary>The collision grid (RECONSTRUCTION 5), or null for a world built without one.</summary>
        public Map Map { get; }

        /// <summary>Every character, all simulated each Play step whether driven or not (RECONSTRUCTION 4).</summary>
        public IReadOnlyList<PlayerCharacter> Characters => _characters;

        /// <summary>
        /// The index into <see cref="Characters"/> of the character <paramref name="player"/>'s commands drive and the
        /// camera follows (the original's <c>Scene.PlayerIndex</c>): player n starts on character n and [ / ] move it
        /// (<see cref="CycleCharacter"/>). -1 if there is none.
        /// </summary>
        public int DrivenIndex(PlayerId player)
        {
            if (!player.IsValid) return -1;
            if (_driven.TryGetValue(player.Value, out var index)) return index;
            return player.Value < _characters.Count ? player.Value : -1;
        }

        /// <summary>The character <paramref name="player"/> drives (<c>Scene.CurrentPlayer</c>), or null if there is none.</summary>
        public PlayerCharacter Driven(PlayerId player)
        {
            var index = DrivenIndex(player);
            return index >= 0 ? _characters[index] : null;
        }

        /// <summary>
        /// <paramref name="player"/>'s camera (<c>CameraManager</c>, RECONSTRUCTION 7), or null if the world was built
        /// without camera data or the player has none. It changes only as the world applies that player's commands.
        /// </summary>
        public WorldCamera Camera(PlayerId player) =>
            player.IsValid && _cameras.TryGetValue(player.Value, out var camera) ? camera : null;

        /// <summary>
        /// Where <paramref name="player"/>'s map-editor cursor is (<c>MapEditor.Cursor.Position</c>, the original's (0, 0)
        /// until the editor first opens), which the LooseCursor camera mode follows. Entering the editor puts it on the
        /// driven character, as <c>ToggleMapEditor</c> did; the editor's own cursor movement is the editor's story (A14).
        /// </summary>
        public PixelVector EditorCursor(PlayerId player) =>
            player.IsValid && _editorCursors.TryGetValue(player.Value, out var at) ? at : new PixelVector(0f, 0f);

        /// <summary>Steps taken so far.</summary>
        public long Tick { get; private set; }

        /// <summary>The original starts in Play (RECONSTRUCTION 2).</summary>
        public GameState State { get; private set; } = GameState.Play;

        /// <summary>
        /// Whether the console is down (the original's <c>Console.Visible</c>). It is separate from the state: Esc from
        /// the console opens the menu with the console still down, and closing the menu returns to Play under it.
        /// </summary>
        public bool ConsoleOpen { get; private set; }

        /// <summary>Events produced by the last <see cref="Advance"/> call, oldest first. Cleared on the next call.</summary>
        public IReadOnlyList<IGameEvent> Events => _events;

        /// <summary>Interpolation factor between the previous and the next state, for rendering.</summary>
        public double Alpha => _clock.Alpha;

        public int PendingCommands => _pending.Count;

        /// <summary>Queues a command for the next step. Rejects a null command.</summary>
        public void Submit(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!command.Player.IsValid) throw new ArgumentException("A command must name its player.", nameof(command));
            _pending.Enqueue(command);
        }

        /// <summary>
        /// Feeds frame time in, runs every fixed step now due and returns how many ran. <paramref name="input"/>, if
        /// given, issues its commands before each step, reading the state that step starts from.
        /// </summary>
        public int Advance(double deltaSeconds, IStepInput input = null)
        {
            _events.Clear();
            int steps = _clock.Advance(deltaSeconds);
            for (int i = 0; i < steps; i++)
            {
                input?.BeforeStep(this);
                Step();
            }

            return steps;
        }

        /// <summary>
        /// One 1/60 s step in the original's frame order (RECONSTRUCTION 1): apply every queued command in order (the
        /// InputManager), then, only in Play, update every character (the SceneManager), then, in every state, every
        /// camera, following the character already moved (the CameraManager), then advance the tick.
        /// </summary>
        void Step()
        {
            if (_actionsBefore.Length != _characters.Count) _actionsBefore = new ActionKind[_characters.Count];
            for (int i = 0; i < _characters.Count; i++)
            {
                _actionsBefore[i] = _characters[i].Action;
                _characters[i].BeginStep();
            }

            foreach (var camera in _cameras.Values) camera.BeginStep();

            while (_pending.Count > 0) Apply(_pending.Dequeue());

            if (State == GameState.Play)
            {
                foreach (var character in _characters)
                {
                    character.Step(Map, FixedStepper.OriginalElapsedSeconds);
                }
            }

            foreach (var entry in _cameras)
            {
                entry.Value.Step(CameraTarget(new PlayerId(entry.Key), entry.Value.Mode), Map);
            }

            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Action != _actionsBefore[i])
                {
                    _events.Add(new CharacterActionChanged(Tick, i, _actionsBefore[i], _characters[i].Action));
                }
            }

            Tick++;
        }

        void Apply(ICommand command)
        {
            switch (command)
            {
                case SetGameState set:
                    ChangeState(set.Player, set.State);
                    break;

                // Action_ToggleMainMenu: from the menu always back to Play, even if it was opened from the editor.
                case ToggleMainMenu toggle:
                    ChangeState(toggle.Player, State == GameState.MainMenu ? GameState.Play : GameState.MainMenu);
                    break;

                // ToggleMapEditor: from the editor to Play with the camera back on LoosePlayer; from anything else (even
                // the menu) into the editor with the camera Free and the editor cursor on the driven character.
                case ToggleMapEditor toggle:
                    if (State == GameState.MapEditor)
                    {
                        ChangeState(toggle.Player, GameState.Play);
                        Camera(toggle.Player)?.SetMode(CameraMode.LoosePlayer);
                    }
                    else
                    {
                        ChangeState(toggle.Player, GameState.MapEditor);
                        Camera(toggle.Player)?.SetMode(CameraMode.Free);
                        var driven = Driven(toggle.Player);
                        if (driven != null) _editorCursors[toggle.Player.Value] = driven.Position;
                    }

                    break;

                // Reset: always back to Play, the camera back on LoosePlayer (every player's, as every character resets)
                // and every character to the reset position, Falling (RECONSTRUCTION 4.9).
                case ResetGame reset:
                    ChangeState(reset.Player, GameState.Play);
                    foreach (var camera in _cameras.Values) camera.SetMode(CameraMode.LoosePlayer);
                    foreach (var character in _characters)
                    {
                        character.Reset(new PixelVector(_player.ResetPosition.X, _player.ResetPosition.Y));
                    }

                    break;

                // The driven character's controls (Command_Move, Command_Stand, Command_Kneel, Command_Jump).
                case MoveCharacter move:
                    Driven(move.Player)?.Move(move.Direction);
                    break;

                case StandCharacter stand:
                    Driven(stand.Player)?.StopMoving();
                    break;

                case KneelCharacter kneel:
                    Driven(kneel.Player)?.Kneel();
                    break;

                case JumpCharacter jump:
                    Driven(jump.Player)?.Jump();
                    break;

                case CycleCharacter cycle:
                    Cycle(cycle.Player, cycle.Direction);
                    break;

                // Command_ToggleConsole flips the console down or up. The original changed the state only when the
                // slide ended (opening) or on every frame of it (closing); until the console's slide is built (A11)
                // the state changes at once.
                case ToggleConsole toggle:
                    ConsoleOpen = !ConsoleOpen;
                    _events.Add(new ConsoleToggled(Tick, ConsoleOpen));
                    ChangeState(toggle.Player, ConsoleOpen ? GameState.Console : GameState.Play);
                    break;

                // Left / B at the menu's root closes it (Action_ToggleMainMenu); the menu's other levels come with A10.
                case NavigateMenu navigate when navigate.Input == MenuInput.Back && State == GameState.MainMenu:
                    ChangeState(navigate.Player, GameState.Play);
                    break;

                case CycleCameraMode cycle:
                    Camera(cycle.Player)?.CycleMode();
                    break;

                case NudgeCamera nudge:
                    Camera(nudge.Player)?.Nudge(nudge.Nudge);
                    break;

                // An editor direction press scrolls the camera a cell step (Cursor.MoveCellLeft ...: ScrollLeft(4))
                // unless Shift; its palette step is the editor's (A14).
                case StepEditor step when step.MoveCell:
                    Camera(step.Player)?.ScrollCell(step.Dx, step.Dy);
                    break;

                case ToggleFullscreen _:
                    _events.Add(new FullscreenToggled(Tick));
                    break;
            }
        }

        /// <summary>What a camera in <paramref name="mode"/> follows: the editor cursor for LooseCursor, else the driven character.</summary>
        PixelVector? CameraTarget(PlayerId player, CameraMode mode)
        {
            if (mode == CameraMode.LooseCursor) return EditorCursor(player);
            var driven = Driven(player);
            return driven != null ? driven.Position : (PixelVector?)null;
        }

        /// <summary>
        /// <c>CyclePlayerLeft</c> / <c>CyclePlayerRight</c>: drive the previous or next character, wrapping at either end.
        /// The character left behind lets go of its direction (BIBLE defect 16 fixed): in the original it kept the
        /// <c>movement</c> it last had, so a character switched away from mid-run ran on, undriven, forever.
        /// </summary>
        void Cycle(PlayerId player, int direction)
        {
            var from = DrivenIndex(player);
            if (from < 0 || direction == 0) return;

            var last = _characters.Count - 1;
            var to = direction < 0
                ? (from > 0 ? from - 1 : last)
                : (from < last ? from + 1 : 0);
            if (to == from) return;

            _characters[from].StopMoving();
            _driven[player.Value] = to;
            _events.Add(new DrivenCharacterChanged(Tick, player, from, to));
        }

        void ChangeState(PlayerId player, GameState to)
        {
            if (to == State)
            {
                return;
            }

            var from = State;
            State = to;
            _events.Add(new GameStateChanged(Tick, player, from, to));
        }
    }
}
