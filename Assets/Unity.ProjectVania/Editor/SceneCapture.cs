using System;
using System.IO;
using System.Reflection;
using Unity.ProjectVania.Core.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.ProjectVania.EditorTools
{
    /// <summary>
    /// Renders the composed game's camera to PNGs at the 800 x 600 reference, so a change can be seen working from the
    /// command line (<c>unity.ps1 capture</c>, which runs with graphics). It boots the game as the player would, lets the
    /// characters fall and land and the Loose camera settle on player one, then captures the followed view, the view
    /// zoomed in and turned by the camera keys (the original's matrix: scale cubed, about the world origin), the map's
    /// top-left corner, and that corner with the editor open. The main menu's pictures (menu, menu-video) and the console's (console-sliding, console, console-full-red) draw the UI Toolkit overlay over the camera's picture; the others are the camera alone, without the names.
    /// </summary>
    public static class SceneCapture
    {
        public const string DefaultOutput = "Logs/Capture";

        public static void Capture()
        {
            try
            {
                var output = ArgumentValue("-captureOutput") ?? DefaultOutput;
                Directory.CreateDirectory(output);
                var host = Bootstrap.Run(Startup.StreamingSource());
                if (host == null)
                {
                    throw new InvalidOperationException("The settings did not load.");
                }

                Steps(host, 300);

                Present(host);
                var camera = GameObject.Find("Camera").GetComponent<Camera>();
                Save(camera, Path.Combine(output, "warmup.png")); // the pipeline's first render in a fresh editor is incomplete
                File.Delete(Path.Combine(output, "warmup.png"));
                Save(camera, Path.Combine(output, "play.png"));

                // The main menu (U.VAN-US-A10) over the frozen world: the root, then the Video level, then closed again.
                SaveWithUi(camera, host, Path.Combine(output, "play-ui.png"));
                host.World.Submit(new ToggleMainMenu(PlayerId.One));
                host.World.Advance(1.0 / 60);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "menu.png"));
                host.World.Submit(new NavigateMenu(PlayerId.One, MenuInput.Down));
                host.World.Submit(new NavigateMenu(PlayerId.One, MenuInput.Select));
                host.World.Advance(1.0 / 60);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "menu-video.png"));
                host.World.Submit(new NavigateMenu(PlayerId.One, MenuInput.Back));
                host.World.Submit(new NavigateMenu(PlayerId.One, MenuInput.Back));
                host.World.Advance(1.0 / 60);
                Present(host);

                // The console (U.VAN-US-A11): half-way down, then down with a few lines run, then full height on red.
                host.World.Submit(new ToggleConsole(PlayerId.One));
                Steps(host, 6);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "console-sliding.png"));
                while (host.World.Console.Sliding)
                {
                    Steps(host, 1);
                }

                foreach (var line in new[] { "lee", "say hello", "console history yellow", "console text", "ver", "console text green", "type here" })
                {
                    Type(host, line, submit: line != "type here");
                }

                while (!host.World.Console.Blink)
                {
                    Steps(host, 1);
                }

                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "console.png"));
                Type(host, "console full", submit: true);
                Type(host, "console bg red", submit: true);
                Present(host);
                SaveWithUi(camera, host, Path.Combine(output, "console-full-red.png"));
                host.World.Submit(new ToggleConsole(PlayerId.One));
                Steps(host, 1);
                while (host.World.Console.Sliding)
                {
                    Steps(host, 1);
                }

                Present(host);

                // = held 20 steps (scale 1.2, magnified 1.728 about the world origin), then PageUp 20 (0.2 rad clockwise).
                Nudge(host, CameraNudge.ZoomIn, 20);
                Present(host);
                Save(camera, Path.Combine(output, "zoomed.png"));
                Nudge(host, CameraNudge.RotateRight, 20);
                Present(host);
                Save(camera, Path.Combine(output, "turned.png"));
                Nudge(host, CameraNudge.RotateLeft, 20);
                Nudge(host, CameraNudge.ZoomOut, 20);
                Present(host);

                var follow = camera.GetComponent<GameCamera>();
                follow.enabled = false;
                camera.GetComponent<PixelPerfectCamera>().Magnification = 1f;
                camera.transform.SetPositionAndRotation(new Vector3(300f / 16f, -200f / 16f, camera.transform.position.z), Quaternion.identity);
                Save(camera, Path.Combine(output, "corner.png"));

                host.World.Submit(new ToggleMapEditor(PlayerId.One));
                host.World.Advance(1.0 / 60);
                Present(host);
                Save(camera, Path.Combine(output, "editor.png"));

                UnityEngine.Object.DestroyImmediate(GameObject.Find(Bootstrap.RootName));
                Debug.Log($"[ProjectVania] Captured to {Path.GetFullPath(output)}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProjectVania] Capture failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        static void Present(GameHost host)
        {
            foreach (var view in host.GetComponentsInChildren<CharacterView>())
            {
                view.Present(0f);
            }

            host.GetComponentInChildren<MapView>().Present();
            host.GetComponentInChildren<NameTags>()?.Place();
            host.GetComponentInChildren<MainMenuView>()?.Refresh();
            host.GetComponentInChildren<ConsoleView>()?.Refresh();
            var follow = host.GetComponentInChildren<GameCamera>();
            if (follow != null && follow.enabled)
            {
                follow.Present();
            }
        }

        /// <summary>Clears the console's line, types <paramref name="line"/> and, if asked, presses Enter, in one step.</summary>
        static void Type(GameHost host, string line, bool submit)
        {
            host.World.Submit(new EditConsole(PlayerId.One, ConsoleInput.Clear));
            host.World.Submit(new EditConsole(PlayerId.One, ConsoleInput.Append, line));
            if (submit)
            {
                host.World.Submit(new EditConsole(PlayerId.One, ConsoleInput.Submit));
            }

            Steps(host, 1);
        }

        static void Steps(GameHost host, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                host.World.Advance(1.0 / 60);
            }
        }

        static void Nudge(GameHost host, CameraNudge nudge, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                host.World.Submit(new NudgeCamera(PlayerId.One, nudge));
                host.World.Advance(1.0 / 60);
            }
        }

        static void Save(Camera camera, string path) => Save(camera, path, null);

        /// <summary>
        /// The camera's picture with the UI Toolkit panel drawn over it: the panel is pointed at the same render texture
        /// (without clearing it) at a scale of 1 and repainted by UI Toolkit's own runtime loop, called directly because an
        /// -executeMethod run ticks no player loop.
        /// </summary>
        static void SaveWithUi(Camera camera, GameHost host, string path)
        {
            var document = host.GetComponentInChildren<UIDocument>();
            var panel = document.GetComponent<PixelPerfectPanel>();
            var settings = document.panelSettings;
            Save(camera, path, target =>
            {
                var previous = settings.targetTexture;
                try
                {
                    settings.clearColor = false;
                    settings.targetTexture = target;
                    panel.Apply(800, 600);
                    host.GetComponentInChildren<NameTags>()?.Place();
                    // Lay out first, then draw exactly once: the texture is not cleared, so a second repaint would draw
                    // the translucent menu over itself.
                    PumpPanels("UpdatePanels");
                    PumpPanels("UpdatePanels", "RepaintPanels", "RenderOffscreenPanels");
                }
                finally
                {
                    settings.targetTexture = previous;
                    panel.Apply(Screen.width, Screen.height);
                }
            });
        }

        static void PumpPanels(params string[] steps)
        {
            var utility = typeof(UIDocument).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            if (utility == null)
            {
                throw new InvalidOperationException("UIElementsRuntimeUtility not found; the UI cannot be captured.");
            }

            const BindingFlags any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in steps)
            {
                var method = utility.GetMethod(name, any);
                if (method == null)
                {
                    throw new InvalidOperationException($"UIElementsRuntimeUtility.{name} not found; the UI cannot be captured.");
                }

                var parameters = method.GetParameters();
                var arguments = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    // RepaintPanels(bool onlyOffscreen): the panel now draws into the capture's texture, which is offscreen.
                    arguments[i] = parameters[i].ParameterType == typeof(bool) ? (object)true : null;
                }

                method.Invoke(null, arguments);
            }
        }

        static void Save(Camera camera, string path, Action<RenderTexture> overlay)
        {
            var target = new RenderTexture(800, 600, 24, RenderTextureFormat.ARGB32);
            var previousRect = camera.rect;
            var previousActive = RenderTexture.active;
            try
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.orthographic = true;
                // 600 rows at 16 px per unit over the camera's zoom, as PixelPerfectCamera frames it
                camera.orthographicSize = 600f / 2f / 16f / camera.GetComponent<PixelPerfectCamera>().Magnification;
                camera.aspect = 800f / 600f;
                camera.targetTexture = target;
                camera.Render();
                overlay?.Invoke(target);
                RenderTexture.active = target;
                var image = new Texture2D(800, 600, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                camera.rect = previousRect;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static string ArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
