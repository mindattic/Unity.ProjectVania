using System;
using Unity.ProjectVania.Core.Characters;
using Unity.ProjectVania.Core.Display;
using Unity.ProjectVania.Core.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.ProjectVania
{
    /// <summary>
    /// The code-built bootstrap (RECONSTRUCTION 0): the project's one scene is empty, and this composes everything
    /// at start-up instead of Inspector wiring. It points the shared profiles at the player's data folder, loads the
    /// settings registry (stopping with every error listed if a file is bad), then builds the pixel-perfect camera,
    /// the game host, a sprite per character, and the UI Toolkit root (with the characters' names) at the 800x600 reference.
    /// </summary>
    public static class Bootstrap
    {
        public const string RootName = "ProjectVania";

        /// <summary>The composed game, or null before the bootstrap ran or after it stopped on bad settings.</summary>
        public static GameHost Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnLoad() => Run(Startup.StreamingSource());

        /// <summary>Loads the settings from <paramref name="source"/> and composes the game, or logs every error and stops.</summary>
        public static GameHost Run(ISettingsSource source)
        {
            Current = null;
            Unity.Shared.ProfilesBootstrap.Initialize();

            var result = Startup.LoadSettings(source);
            if (!result.Succeeded)
            {
                Stop(result);
                return null;
            }

            Current = Compose(result.Settings);
            return Current;
        }

        /// <summary>Builds the camera, host and UI for valid settings. Replaces a previous composition.</summary>
        public static GameHost Compose(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Remove(existing);
            }

            var root = new GameObject(RootName);
            var host = root.AddComponent<GameHost>();
            host.Initialize(settings);

            // Letterbox bars are outside every camera's viewport rect, so nothing clears them on their own; this
            // camera draws nothing (cullingMask 0) and runs before the game camera (lower depth) only to paint
            // them black, full-screen, under whatever the game camera draws over it.
            var letterboxObject = new GameObject("LetterboxCamera");
            letterboxObject.transform.SetParent(root.transform, false);
            var letterbox = letterboxObject.AddComponent<Camera>();
            letterbox.clearFlags = CameraClearFlags.SolidColor;
            letterbox.backgroundColor = Color.black;
            letterbox.cullingMask = 0;
            letterbox.depth = -1;

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(100, 149, 237, 255); // XNA CornflowerBlue, the original's clear colour
            var pixelPerfect = cameraObject.AddComponent<PixelPerfectCamera>();
            pixelPerfect.PixelsPerUnit = settings.Grid.PixelsPerUnit;
            cameraObject.AddComponent<FollowDrivenCharacter>().Initialize(host.World, host.Input.Player, settings.Grid.PixelsPerUnit);

            BuildCharacters(root.transform, host, settings);
            var ui = BuildUi(root.transform);
            ui.gameObject.AddComponent<NameTags>().Initialize(host.World, settings.Characters.NameTag, camera, settings.Grid.PixelsPerUnit, ui.rootVisualElement);
            return host;
        }

        /// <summary>One sprite per Core character, drawn from its sheet's strips (RECONSTRUCTION 4.8).</summary>
        static void BuildCharacters(Transform parent, GameHost host, GameSettings settings)
        {
            var group = new GameObject("Characters");
            group.transform.SetParent(parent, false);
            var art = new CharacterArt(settings.Animations, settings.Grid.PixelsPerUnit);
            var characters = host.World.Characters;
            for (var i = 0; i < characters.Count; i++)
            {
                var definition = characters[i].Definition ?? settings.Characters.Find(settings.Characters.DefaultCharacter);
                var view = new GameObject($"Character {i} ({characters[i].Name})", typeof(SpriteRenderer)).AddComponent<CharacterView>();
                view.transform.SetParent(group.transform, false);
                var animator = new CharacterAnimator(settings.Animations.Find(definition.Sheet));
                view.Initialize(host.World, i, animator, art, settings.Grid.PixelsPerUnit);
            }
        }

        static UIDocument BuildUi(Transform parent)
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "ProjectVaniaPanel";
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");

            var uiObject = new GameObject("UI");
            uiObject.transform.SetParent(parent, false);
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = panel;

            // Fits the panel to the same viewport PixelPerfectCamera gave the game camera (both read the current
            // Screen size through PixelPerfect.Fit), applied immediately rather than waiting for the first
            // LateUpdate so the UI is never shown unscaled for a frame, and in edit-mode tests that run no frames.
            var pixelPerfectUi = uiObject.AddComponent<PixelPerfectPanel>();
            pixelPerfectUi.Apply(Screen.width, Screen.height);
            return document;
        }

        /// <summary>Logs every error of the failed load and stops the game (quits a build, leaves Play mode in the Editor).</summary>
        static void Stop(StartupResult result)
        {
            Debug.LogError($"[ProjectVania] The settings in {result.Source} are invalid ({result.Errors.Count} problem(s)); stopping.");
            foreach (var error in result.Errors)
            {
                Debug.LogError($"[ProjectVania] {error}");
            }

            if (!Application.isPlaying)
            {
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(1);
#endif
        }

        static void Remove(GameObject target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
