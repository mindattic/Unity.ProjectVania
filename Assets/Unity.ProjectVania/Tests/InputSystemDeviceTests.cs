using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.ProjectVania.Core.Commands;
using Unity.ProjectVania.Core.Input;
using Unity.ProjectVania.Core.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using CoreKey = Unity.ProjectVania.Core.Input.Key;

namespace Unity.ProjectVania.Tests
{
    /// <summary>
    /// U.VAN-US-A4 through the real Input System: fake keyboard, Xbox pad and mouse drive the composed game host, and
    /// the original's controls reach the Core world as commands (the routing itself is covered by the Core tests).
    /// </summary>
    public class InputSystemDeviceTests : InputTestFixture
    {
        const double Step = 1.0 / 60;

        Keyboard _keyboard;
        Gamepad _pad;
        Mouse _mouse;
        GameHost _host;

        static GameSettings Settings => GameSettings.Load(
            new DirectorySettingsSource(Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder)));

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _pad = InputSystem.AddDevice<Gamepad>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _host = Bootstrap.Compose(Settings);
        }

        public override void TearDown()
        {
            var root = GameObject.Find(Bootstrap.RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            base.TearDown();
        }

        [Test]
        public void TheHostDrivesPlayerOneFromTheKeyboard()
        {
            Press(_keyboard.dKey);
            _host.Tick(Step);
            var move = _host.Input.LastCommands.OfType<MoveCharacter>().Single();
            Assert.That(move.Direction, Is.EqualTo(1));
            Assert.That(move.Player, Is.EqualTo(PlayerId.One));
        }

        [Test]
        public void EscOpensTheMenuAndStartClosesIt()
        {
            Press(_keyboard.escapeKey);
            _host.Tick(Step);
            Assert.That(_host.World.State, Is.EqualTo(GameState.MainMenu));
            Release(_keyboard.escapeKey);
            Press(_pad.startButton);
            _host.Tick(Step);
            Assert.That(_host.World.State, Is.EqualTo(GameState.Play));
        }

        [Test]
        public void ThePadsBackButtonOpensTheConsoleAndTildeClosesIt()
        {
            Press(_pad.selectButton);
            _host.Tick(Step);
            Assert.That(_host.World.State, Is.EqualTo(GameState.Console));
            Release(_pad.selectButton);
            Press(_keyboard.backquoteKey);
            _host.Tick(Step);
            Assert.That(_host.World.State, Is.EqualTo(GameState.Play));
        }

        [Test]
        public void ThePadsLeftStickAndShouldersReachTheWorld()
        {
            Set(_pad.leftStick, new Vector2(-1f, 0f));
            Press(_pad.rightShoulder);
            _host.Tick(Step);
            var commands = _host.Input.LastCommands;
            Assert.That(commands.OfType<MoveCharacter>().Single().Direction, Is.EqualTo(-1));
            Assert.That(commands.OfType<CycleCharacter>().Single().Direction, Is.EqualTo(1));
        }

        [Test]
        public void ATapBetweenTwoStepsIsNotLost()
        {
            // Pressed and released inside one Input System update: down to the next step, then up.
            Press(_keyboard.mKey, queueEventOnly: true);
            Release(_keyboard.mKey, queueEventOnly: true);
            InputSystem.Update();
            _host.Tick(Step);
            Assert.That(_host.World.State, Is.EqualTo(GameState.MapEditor));
            InputSystem.Update();
            _host.Tick(Step);
            Assert.That(_host.Input.State.Current.IsDown(CoreKey.M), Is.False);
        }

        [Test]
        public void ARenderedFrameWithNoStepKeepsThePressForTheNextStep()
        {
            Press(_keyboard.mKey, queueEventOnly: true);
            Release(_keyboard.mKey, queueEventOnly: true);
            InputSystem.Update();
            _host.Tick(Step / 4); // a fast frame: no step is due yet
            Assert.That(_host.World.State, Is.EqualTo(GameState.Play));
            InputSystem.Update();
            _host.Tick(Step);
            Assert.That(_host.World.State, Is.EqualTo(GameState.MapEditor));
        }

        [Test]
        public void TheMiddleButtonTogglesTheEditorAndTheWheelZooms()
        {
            Press(_mouse.middleButton);
            Set(_mouse.scroll, new Vector2(0f, 120f));
            _host.Tick(Step);
            Assert.That(_host.World.State, Is.EqualTo(GameState.MapEditor));
            Assert.That(_host.Input.LastCommands.OfType<NudgeCamera>().Single().Nudge, Is.EqualTo(CameraNudge.ZoomIn));
        }

        [Test]
        public void F12AsksTheWindowToToggleFullscreen()
        {
            Press(_keyboard.f12Key);
            _host.Device.Poll();
            _host.World.Advance(Step, _host.Input);
            Assert.That(_host.World.Events.OfType<FullscreenToggled>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheKeysReachCoreUnderTheOriginalsNames()
        {
            Press(_keyboard.equalsKey);
            Press(_keyboard.leftBracketKey);
            Press(_keyboard.backslashKey);
            var frame = new InputFrame();
            _host.Device.Poll();
            _host.Device.Sample(frame);
            Assert.That(frame.IsDown(CoreKey.OemPlus), Is.True);
            Assert.That(frame.IsDown(CoreKey.OemOpenBrackets), Is.True);
            Assert.That(frame.IsDown(CoreKey.OemPipe), Is.True);
            Assert.That(frame.PadConnected, Is.True);
        }
    }
}
