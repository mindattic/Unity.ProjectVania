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

        /// <summary>Plays the cues the world names (the menu's, the console's and the characters' sounds).</summary>
        public CuePlayer Cues { get; private set; }

        /// <summary>Plays the songs the console starts, pauses and mutes (the original's MediaPlayer).</summary>
        public SongPlayer Songs { get; private set; }

        /// <summary>
        /// What the menu's Quit does (<c>Game.Exit()</c>): quits a build, or leaves Play mode in the Editor. Replaceable so
        /// a test can see it called without ending the test run.
        /// </summary>
        public System.Action Exit { get; set; } = ExitProgram;

        public void Initialize(GameSettings settings)
        {
            Settings = settings;
            // characters.json's roster, each with a fresh random name (owner decision, U.VAN-T25: Morris, Shanoa and Leo,
            // where the original spawned two Morrises).
            World = new GameWorld(Map.FromSettings(settings.Grid), settings.Player, settings.Characters.Roster, settings.Names, new System.Random(), settings.Camera, settings.Menu,
                settings.Console, settings.Audio, settings.Hud, settings.Animations, settings.Editor);
            Device?.Dispose();
            Device = new InputSystemDevice(settings.Input.StickPressPoint);
            Input = new PlayerInput(PlayerId.One, settings.Input, Device);
            Cues = GetComponent<CuePlayer>();
            if (Cues == null)
            {
                Cues = gameObject.AddComponent<CuePlayer>();
            }

            Cues.Initialize(settings.Audio);
            Songs = GetComponent<SongPlayer>();
            if (Songs == null)
            {
                Songs = gameObject.AddComponent<SongPlayer>();
            }

            Songs.Initialize(settings.Audio);
        }

        /// <summary>One rendered frame: latch this frame's input, run every step now due, then present its events.</summary>
        public void Tick(double deltaSeconds)
        {
            if (World == null)
            {
                return;
            }

            Device.Poll();
            // A song that ran to its end is reported before the step, as the original's MediaPlayer.State read Stopped then.
            if (Songs.Ended())
            {
                World.Submit(new EndSong(Input.Player));
            }

            World.Advance(deltaSeconds, Input);
            Present(World.Events);
            HoldTheMouse();
        }

        /// <summary>
        /// The editor's World mode reads the mouse as a joystick and warped it back to the screen centre every frame
        /// (<c>Mouse.SetPosition</c>, RECONSTRUCTION 12.1): the device reads its travel, and while playing the OS cursor is
        /// locked there. The original never showed the OS cursor (<c>IsMouseVisible = false</c>); it drew its own hands.
        /// </summary>
        void HoldTheMouse()
        {
            var editor = World.Editor(Input.Player);
            Device.Joystick = World.State == GameState.MapEditor && editor != null && editor.Mode == EditorCursorMode.World;
            if (!Application.isPlaying)
            {
                return;
            }

            var lockState = Device.Joystick ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != lockState)
            {
                Cursor.lockState = lockState;
            }

            if (Cursor.visible)
            {
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Plays what the world's steps produced: the window mode, the cues, the songs, pause and mute, the exit. The
        /// script's messages and fade are kept by the world's HUD and drawn from its state (A12).
        /// </summary>
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
                    case SongRequested song:
                        Songs.Play(song.Song);
                        break;
                    case MusicPauseChanged pause:
                        Songs.SetPaused(pause.Paused);
                        break;
                    case MuteChanged mute:
                        Songs.SetMuted(mute.Muted);
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
