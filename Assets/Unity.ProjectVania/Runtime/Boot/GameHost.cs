using Unity.ProjectVania.Core.Commands;
using Unity.ProjectVania.Core.Input;
using Unity.ProjectVania.Core.Settings;
using Unity.ProjectVania.Core.Simulation;
using Unity.ProjectVania.Core.World;
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

        /// <summary>Plays the cues the world names (the menu's sounds).</summary>
        public CuePlayer Cues { get; private set; }

        /// <summary>
        /// What the menu's Quit does (<c>Game.Exit()</c>): quits a build, or leaves Play mode in the Editor. Replaceable so
        /// a test can see it called without ending the test run.
        /// </summary>
        public System.Action Exit { get; set; } = ExitProgram;

        public void Initialize(GameSettings settings)
        {
            Settings = settings;
            // Both spawned characters are the default one, each with a fresh random name, as the original's two Morrises.
            var character = settings.Characters.Find(settings.Characters.DefaultCharacter);
            World = new GameWorld(Map.FromSettings(settings.Grid), settings.Player, character, settings.Names, new System.Random(), settings.Camera, settings.Menu);
            Device?.Dispose();
            Device = new InputSystemDevice(settings.Input.StickPressPoint);
            Input = new PlayerInput(PlayerId.One, settings.Input, Device);
            Cues = GetComponent<CuePlayer>();
            if (Cues == null)
            {
                Cues = gameObject.AddComponent<CuePlayer>();
            }

            Cues.Initialize(settings.Audio);
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
            Present(World.Events);
        }

        /// <summary>Plays what the world's steps produced: the window mode, the cues, the exit.</summary>
        public void Present(System.Collections.Generic.IReadOnlyList<Core.Commands.IGameEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case FullscreenToggled _:
                        Screen.fullScreen = !Screen.fullScreen;
                        break;
                    case CueRequested cue:
                        Cues.Play(cue.Cue);
                        break;
                    case QuitRequested _:
                        Exit?.Invoke();
                        break;
                }
            }
        }

        static void ExitProgram()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
#else
            Application.Quit();
#endif
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
