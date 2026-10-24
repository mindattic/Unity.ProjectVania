using System;
using System.IO;
using Unity.ProjectVania.Core.Commands;
using UnityEditor;
using UnityEngine;

namespace Unity.ProjectVania.EditorTools
{
    /// <summary>
    /// Renders the composed game's camera to PNGs at the 800 x 600 reference, so a change can be seen working from the
    /// command line (<c>unity.ps1 capture</c>, which runs with graphics). It boots the game as the player would, lets the
    /// characters fall and land, then captures the followed view, the map's top-left corner, and that corner with the
    /// editor open. The UI Toolkit overlay (the names) is not in a camera render and so not in the pictures.
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

                for (var i = 0; i < 120; i++)
                {
                    host.World.Advance(1.0 / 60);
                }

                Present(host);
                var camera = GameObject.Find("Camera").GetComponent<Camera>();
                Save(camera, Path.Combine(output, "warmup.png")); // the pipeline's first render in a fresh editor is incomplete
                File.Delete(Path.Combine(output, "warmup.png"));
                Save(camera, Path.Combine(output, "play.png"));

                var follow = camera.GetComponent<FollowDrivenCharacter>();
                follow.enabled = false;
                camera.transform.position = new Vector3(300f / 16f, -200f / 16f, camera.transform.position.z);
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
            host.GetComponentInChildren<FollowDrivenCharacter>()?.Follow();
        }

        static void Save(Camera camera, string path)
        {
            var target = new RenderTexture(800, 600, 24, RenderTextureFormat.ARGB32);
            var previousRect = camera.rect;
            var previousActive = RenderTexture.active;
            try
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = 600f / 2f / 16f; // 600 rows at 16 px per unit, as PixelPerfectCamera frames it
                camera.aspect = 800f / 600f;
                camera.targetTexture = target;
                camera.Render();
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
