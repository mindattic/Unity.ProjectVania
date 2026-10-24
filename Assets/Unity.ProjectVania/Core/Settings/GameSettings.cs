using System;
using System.Collections.Generic;

namespace Unity.ProjectVania.Core.Settings
{
    /// <summary>
    /// The settings registry: every tuning figure, content definition and text of the game, loaded from the data
    /// files (StreamingAssets/Settings in the build) through an <see cref="ISettingsSource"/> and validated on load.
    /// The files hold the original's own figures in its own units (pixels, pixels per second, per frame); Core keeps
    /// them as they are, and <see cref="Units"/> is the one place they meet the Runtime's units.
    /// </summary>
    public sealed class GameSettings
    {
        /// <summary>The folder under StreamingAssets the data files live in.</summary>
        public const string Folder = "Settings";

        public const string PlayerFile = "player.json";
        public const string CharactersFile = "characters.json";
        public const string AnimationsFile = "animations.json";
        public const string GridFile = "grid.json";
        public const string MenuFile = "menu.json";
        public const string CameraFile = "camera.json";
        public const string ConsoleFile = "console.json";
        public const string AudioFile = "audio.json";
        public const string SegmentsFile = "segments.json";
        public const string NamesFile = "names.json";

        /// <summary>Every data file the registry reads.</summary>
        public static readonly string[] Files =
        {
            PlayerFile, CharactersFile, AnimationsFile, GridFile, MenuFile, CameraFile, ConsoleFile, AudioFile, SegmentsFile, NamesFile,
        };

        private GameSettings()
        {
        }

        public PlayerData Player { get; private set; }
        public CharacterData Characters { get; private set; }
        public AnimationData Animations { get; private set; }
        public GridData Grid { get; private set; }
        public MenuData Menu { get; private set; }
        public CameraData Camera { get; private set; }
        public ConsoleData Console { get; private set; }
        public AudioData Audio { get; private set; }
        public SegmentData Segments { get; private set; }
        public NameData Names { get; private set; }

        /// <summary>The original-to-Runtime unit mapping (grid.json units).</summary>
        public OriginalUnits Units { get; private set; }

        /// <summary>
        /// Reads and validates every data file from <paramref name="source"/>. Throws a <see cref="SettingsException"/>
        /// listing every problem (a missing file, bad JSON with its line, a missing or unknown key, a value of the wrong
        /// type or out of range, a reference to something that does not exist) if anything is wrong.
        /// </summary>
        public static GameSettings Load(ISettingsSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var errors = new List<string>();
            var settings = new GameSettings
            {
                Player = Read(source, PlayerFile, errors, PlayerData.Read),
                Characters = Read(source, CharactersFile, errors, CharacterData.Read),
                Animations = Read(source, AnimationsFile, errors, AnimationData.Read),
                Grid = Read(source, GridFile, errors, GridData.Read),
                Menu = Read(source, MenuFile, errors, MenuData.Read),
                Camera = Read(source, CameraFile, errors, CameraData.Read),
                Console = Read(source, ConsoleFile, errors, ConsoleData.Read),
                Audio = Read(source, AudioFile, errors, AudioData.Read),
                Segments = Read(source, SegmentsFile, errors, SegmentData.Read),
                Names = Read(source, NamesFile, errors, NameData.Read),
            };

            if (errors.Count == 0)
            {
                settings.CheckReferences(errors);
            }

            if (errors.Count > 0)
            {
                throw new SettingsException(errors, source.Describe);
            }

            settings.Units = new OriginalUnits(settings.Grid.PixelsPerUnit, settings.Grid.StepsPerSecond);
            return settings;
        }

        private static T Read<T>(ISettingsSource source, string file, List<string> errors, Func<SettingsReader, T> read)
            where T : class
        {
            string text;
            try
            {
                text = source.Read(file);
            }
            catch (Exception e) when (!(e is SettingsException))
            {
                errors.Add($"{file}: could not be read ({e.Message})");
                return null;
            }

            if (text == null)
            {
                errors.Add($"{file}: not found");
                return null;
            }

            JsonValue root;
            try
            {
                root = Json.Parse(text);
            }
            catch (JsonException e)
            {
                errors.Add($"{file}: is not valid JSON ({e.Message})");
                return null;
            }

            if (root.Kind != JsonKind.Object)
            {
                errors.Add($"{file}: must hold one JSON object {{ ... }}");
                return null;
            }

            var before = errors.Count;
            var reader = new SettingsReader(root, file, errors);
            var data = read(reader);
            reader.Done();
            return errors.Count == before ? data : null;
        }

        /// <summary>The checks that span files: every name one file uses must exist in the file that defines it.</summary>
        private void CheckReferences(List<string> errors)
        {
            foreach (var character in Characters.Characters)
            {
                var sheet = Animations.Find(character.Sheet);
                if (sheet == null)
                {
                    errors.Add($"{CharactersFile}: '{character.Name}' uses sheet '{character.Sheet}', which {AnimationsFile} does not define");
                    continue;
                }

                foreach (var required in new[] { "idle", "run" })
                {
                    if (sheet.Find(required) == null)
                    {
                        errors.Add($"{AnimationsFile}: sheet '{sheet.Sheet}' has no '{required}' animation");
                    }
                }
            }

            foreach (var cue in new[] { Console.LongCue, Console.ShortCue, Menu.MoveCue, Menu.SelectCue })
            {
                if (Audio.FindCue(cue) == null)
                {
                    errors.Add($"{AudioFile}: has no cue '{cue}', which {ConsoleFile} or {MenuFile} plays");
                }
            }

            var width = Grid.MapWidth;
            var height = Grid.MapHeight;
            foreach (var position in Player.SpawnPositions)
            {
                CheckInMap(errors, position, width, height, "a spawn position");
            }

            CheckInMap(errors, Player.ResetPosition, width, height, "the reset position");
        }

        private static void CheckInMap(List<string> errors, Pair position, int width, int height, string what)
        {
            if (position.X > width || position.Y > height)
            {
                errors.Add($"{PlayerFile}: {what} ({position.X}, {position.Y}) is outside the {width} x {height} map");
            }
        }
    }

    /// <summary>
    /// The one mapping from the original's units to the Runtime's, read from grid.json: pixels become units at
    /// <see cref="PixelsPerUnit"/> (one grid cell is one unit), and figures the original added once per frame become
    /// per-second at <see cref="StepsPerSecond"/>. Core itself keeps the original's pixels and frames.
    /// </summary>
    public readonly struct OriginalUnits
    {
        public OriginalUnits(float pixelsPerUnit, int stepsPerSecond)
        {
            PixelsPerUnit = pixelsPerUnit;
            StepsPerSecond = stepsPerSecond;
        }

        public float PixelsPerUnit { get; }
        public int StepsPerSecond { get; }

        /// <summary>A length in pixels as units.</summary>
        public float ToUnits(float pixels) => pixels / PixelsPerUnit;

        /// <summary>A length in units as pixels.</summary>
        public float ToPixels(float units) => units * PixelsPerUnit;

        /// <summary>A figure added once per step, as a figure per second.</summary>
        public float PerSecond(float perStep) => perStep * StepsPerSecond;
    }
}
