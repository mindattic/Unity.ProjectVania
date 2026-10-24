using System;
using System.Collections.Generic;
using Unity.ProjectVania.Core.Characters;
using Unity.ProjectVania.Core.Commands;
using Unity.ProjectVania.Core.Settings;
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
        /// (<c>SceneManager.LoadContent</c>: two, at (500, 100) and (550, 100)), each <paramref name="unitSize"/> cells.
        /// </summary>
        public GameWorld(Map map, PlayerData player, Pair unitSize)
            : this(map)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            foreach (var spawn in player.SpawnPositions)
            {
                _characters.Add(new PlayerCharacter(player, unitSize, new PixelVector(spawn.X, spawn.Y)));
            }
        }

        readonly List<PlayerCharacter> _characters = new List<PlayerCharacter>();
        readonly PlayerData _player;
        ActionKind[] _actionsBefore = Array.Empty<ActionKind>();

        /// <summary>The collision grid (RECONSTRUCTION 5), or null for a world built without one.</summary>
        public Map Map { get; }

        /// <summary>Every character, all simulated each Play step whether driven or not (RECONSTRUCTION 4).</summary>
        public IReadOnlyList<PlayerCharacter> Characters => _characters;

        /// <summary>
        /// The character <paramref name="player"/>'s commands drive: player n drives character n (the original's
        /// <c>Scene.CurrentPlayer</c>, PlayerIndex 0), or null if there is none.
        /// </summary>
        public PlayerCharacter Driven(PlayerId player) =>
            player.IsValid && player.Value < _characters.Count ? _characters[player.Value] : null;

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
        /// InputManager), then, only in Play, update every character (the SceneManager), then advance the tick.
        /// </summary>
        void Step()
        {
            if (_actionsBefore.Length != _characters.Count) _actionsBefore = new ActionKind[_characters.Count];
            for (int i = 0; i < _characters.Count; i++) _actionsBefore[i] = _characters[i].Action;

            while (_pending.Count > 0) Apply(_pending.Dequeue());

            if (State == GameState.Play)
            {
                foreach (var character in _characters)
                {
                    character.Step(Map, FixedStepper.OriginalElapsedSeconds);
                }
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

                // ToggleMapEditor: from the editor to Play, from anything else (even the menu) into the editor.
                case ToggleMapEditor toggle:
                    ChangeState(toggle.Player, State == GameState.MapEditor ? GameState.Play : GameState.MapEditor);
                    break;

                // Reset: always back to Play and every character to the reset position, Falling (RECONSTRUCTION 4.9;
                // the camera resets with its story, A9).
                case ResetGame reset:
                    ChangeState(reset.Player, GameState.Play);
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

                case ToggleFullscreen _:
                    _events.Add(new FullscreenToggled(Tick));
                    break;
            }
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
