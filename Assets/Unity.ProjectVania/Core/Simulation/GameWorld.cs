using System;
using System.Collections.Generic;
using Unity.ProjectVania.Core.Commands;

namespace Unity.ProjectVania.Core.Simulation
{
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

        /// <summary>Feeds frame time in, runs every fixed step now due and returns how many ran.</summary>
        public int Advance(double deltaSeconds)
        {
            _events.Clear();
            int steps = _clock.Advance(deltaSeconds);
            for (int i = 0; i < steps; i++) Step();
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
                    if (set.State != State)
                    {
                        var from = State;
                        State = set.State;
                        _events.Add(new GameStateChanged(Tick, set.Player, from, State));
                    }
                    break;
            }
        }
    }
}
