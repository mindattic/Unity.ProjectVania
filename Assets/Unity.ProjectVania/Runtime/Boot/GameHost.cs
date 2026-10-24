using Unity.ProjectVania.Core.Commands;
using Unity.ProjectVania.Core.Input;
using Unity.ProjectVania.Core.Settings;
using Unity.ProjectVania.Core.Simulation;
using UnityEngine;

namespace Unity.ProjectVania
{
    /// <summary>
    /// Owns the running game: the validated settings, the Core world and player one's controls, stepped from real
    /// time at the fixed rate. Created by <see cref="Bootstrap"/>, never placed in a scene by hand.
    /// </summary>
    public sealed class GameHost : MonoBehaviour
    {
        public GameSettings Settings { get; private set; }
        public GameWorld World { get; private set; }

        /// <summary>The keyboard, first pad and mouse (the original's PlayerIndex.One).</summary>
        public InputSystemDevice Device { get; private set; }

        /// <summary>Player one's controls, routed per game state into commands before every step.</summary>
        public PlayerInput Input { get; private set; }

        public void Initialize(GameSettings settings)
        {
            Settings = settings;
            World = new GameWorld();
            Device?.Dispose();
            Device = new InputSystemDevice(settings.Input.StickPressPoint);
            Input = new PlayerInput(PlayerId.One, settings.Input, Device);
        }

        /// <summary>One rendered frame: latch this frame's input, run every step now due, then present its events.</summary>
        public void Tick(double deltaSeconds)
        {
            if (World == null)
            {
                return;
            }

            Device.Poll();
            World.Advance(deltaSeconds, Input);
            foreach (var e in World.Events)
            {
                if (e is FullscreenToggled)
                {
                    Screen.fullScreen = !Screen.fullScreen;
                }
            }
        }

        void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            Device?.Dispose();
        }
    }
}
