using System;
using System.Collections.Generic;
using Unity.ProjectVania.Core.Commands;

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

        /// <summary>One 1/60 s step: apply every queued command in order, then advance the tick.</summary>
        void Step()
        {
            while (_pending.Count > 0) Apply(_pending.Dequeue());
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

                // Reset: always back to Play (the camera and characters reset with their stories, A6 and A9).
                case ResetGame reset:
                    ChangeState(reset.Player, GameState.Play);
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
