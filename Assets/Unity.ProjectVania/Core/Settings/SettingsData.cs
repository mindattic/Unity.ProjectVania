using System.Collections.Generic;

namespace Unity.ProjectVania.Core.Settings
{
    public enum Gender
    {
        Male,
        Female,
    }

    /// <summary>The player's actions (RECONSTRUCTION 4.6); a character's move set lists the ones it can do.</summary>
    public enum ActionKind
    {
        Standing,
        Running,
        Kneeling,
        Sliding,
        Jumping,
        WallSliding,
        Hanging,
        Falling,
    }

    public enum CameraMode
    {
        Free,
        LockPlayer,
        LoosePlayer,
        LooseCursor,
    }

    public enum MenuAction
    {
        None,
        Open,
        Quit,
        ToggleFullscreen,
    }

    /// <summary>player.json: the constants of RECONSTRUCTION 4.2 and the owner's later decisions, as data slots.</summary>
    public sealed class PlayerData
    {
        public float MoveAcceleration { get; private set; }
        public float MaxMoveSpeed { get; private set; }
        public float GroundDragFactor { get; private set; }
        public float AirDragFactor { get; private set; }
        public float MaxJumpTime { get; private set; }
        public float JumpLaunchVelocity { get; private set; }
        public float JumpControlPower { get; private set; }
        public float GravityAcceleration { get; private set; }
        public float MaxFallSpeed { get; private set; }
        public float WallSlideSpeedDivisor { get; private set; }
        public float WallSlideDecay { get; private set; }
        public float MaxWallSlideTime { get; private set; }
        public float FallDamageTime { get; private set; }
        public float MaxWalljumpDelayTime { get; private set; }
        public float MaxLockOutTime { get; private set; }
        public float SlideLaunchAcceleration { get; private set; }
        public float SlideFriction { get; private set; }
        public Pair SlideUnitSize { get; private set; }
        public IReadOnlyList<Pair> SpawnPositions { get; private set; }
        public Pair ResetPosition { get; private set; }
        public Abilities Ability { get; private set; }

        /// <summary>Owner decision: releasing a kneel stands the character up (U.VAN-T23).</summary>
        public bool KneelReleaseStandsUp { get; private set; }

        /// <summary>Owner decision: a wall jump pushes off the wall (U.VAN-T24).</summary>
        public bool WallJumpPushOff { get; private set; }

        /// <summary>The push-off speed in pixels per second, a slot U.VAN-T24 tunes.</summary>
        public float WallJumpPushSpeed { get; private set; }

        /// <summary>The wall-slide speed cap: <see cref="MaxFallSpeed"/> over <see cref="WallSlideSpeedDivisor"/> (83.33).</summary>
        public float WallSlideSpeed => MaxFallSpeed / WallSlideSpeedDivisor;

        internal static PlayerData Read(SettingsReader r)
        {
            var data = new PlayerData
            {
                MoveAcceleration = r.Positive("moveAcceleration"),
                MaxMoveSpeed = r.Positive("maxMoveSpeed"),
                GroundDragFactor = r.Number("groundDragFactor", 0.0001f, 1f),
                AirDragFactor = r.Number("airDragFactor", 0.0001f, 1f),
                MaxJumpTime = r.Positive("maxJumpTime"),
                JumpLaunchVelocity = r.Number("jumpLaunchVelocity", float.MinValue, -0.0001f),
                JumpControlPower = r.Positive("jumpControlPower"),
                GravityAcceleration = r.Positive("gravityAcceleration"),
                MaxFallSpeed = r.Positive("maxFallSpeed"),
                WallSlideSpeedDivisor = r.Positive("wallSlideSpeedDivisor"),
                WallSlideDecay = r.Number("wallSlideDecay", 0.0001f, 1f),
                MaxWallSlideTime = r.Positive("maxWallSlideTime"),
                FallDamageTime = r.Positive("fallDamageTime"),
                MaxWalljumpDelayTime = r.Number("maxWalljumpDelayTime", 0f),
                MaxLockOutTime = r.Number("maxLockOutTime", 0f),
                SlideLaunchAcceleration = r.Positive("slideLaunchAcceleration"),
                SlideFriction = r.Number("slideFriction", 0.0001f, 1f),
                SlideUnitSize = r.Pair("slideUnitSize", 1f, 100f),
            };

            var spawn = r.Section("spawn", s => new
            {
                Positions = s.Pairs("positions", 0f),
                Reset = s.Pair("resetPosition", 0f),
            });
            data.SpawnPositions = spawn.Positions;
            data.ResetPosition = spawn.Reset;
            r.Check(spawn.Positions.Count >= 1, "spawn", "must list at least one spawn position");
            data.Ability = r.Section("abilities", Abilities.Read);
            data.KneelReleaseStandsUp = r.Section("kneel", s => s.Flag("releaseStandsUp"));
            var wall = r.Section("wallJump", s => new { Push = s.Flag("pushOff"), Speed = s.Number("pushSpeed", 0f) });
            data.WallJumpPushOff = wall.Push;
            data.WallJumpPushSpeed = wall.Speed;
            return data;
        }
    }

    /// <summary>AbilityList (RECONSTRUCTION 4.1).</summary>
    public sealed class Abilities
    {
        public bool Run { get; private set; }
        public bool Jump { get; private set; }
        public bool Kneel { get; private set; }
        public bool WallSlide { get; private set; }
        public bool WallJump { get; private set; }
        public bool Slide { get; private set; }
        public bool SinkInWater { get; private set; }
        public bool BulletTime { get; private set; }

        internal static Abilities Read(SettingsReader r) => new Abilities
        {
            Run = r.Flag("run"),
            Jump = r.Flag("jump"),
            Kneel = r.Flag("kneel"),
            WallSlide = r.Flag("wallSlide"),
            WallJump = r.Flag("wallJump"),
            Slide = r.Flag("slide"),
            SinkInWater = r.Flag("sinkInWater"),
            BulletTime = r.Flag("bulletTime"),
        };
    }

    /// <summary>characters.json: who can be played, with a move set each (owner: Shanoa and Leo playable too, U.VAN-T25).</summary>
    public sealed class CharacterData
    {
        public string DefaultCharacter { get; private set; }
        public IReadOnlyList<Character> Characters { get; private set; }

        public Character Find(string name)
        {
            foreach (var character in Characters)
            {
                if (character.Name == name)
                {
                    return character;
                }
            }

            return null;
        }

        internal static CharacterData Read(SettingsReader r)
        {
            var data = new CharacterData
            {
                DefaultCharacter = r.Text("defaultCharacter"),
                Characters = r.Each("characters", Character.Read),
            };
            r.Check(data.Find(data.DefaultCharacter) != null, "defaultCharacter", "must name one of the characters");
            var seen = new HashSet<string>();
            foreach (var character in data.Characters)
            {
                r.Check(seen.Add(character.Name), "characters", $"lists '{character.Name}' twice");
            }

            return data;
        }
    }

    public sealed class Character
    {
        public string Name { get; private set; }
        public string Sheet { get; private set; }

        /// <summary>The collision size in cells (width, height).</summary>
        public Pair UnitSize { get; private set; }

        public bool Playable { get; private set; }
        public Gender Gender { get; private set; }
        public IReadOnlyList<ActionKind> MoveSet { get; private set; }

        internal static Character Read(SettingsReader r) => new Character
        {
            Name = r.Text("name"),
            Sheet = r.Text("sheet"),
            UnitSize = r.Pair("unitSize", 1f, 100f),
            Playable = r.Flag("playable"),
            Gender = r.Choice<Gender>("gender"),
            MoveSet = r.Choices<ActionKind>("moveSet"),
        };
    }

    /// <summary>animations.json: the animation table of RECONSTRUCTION 4.8.</summary>
    public sealed class AnimationData
    {
        public int FrameSize { get; private set; }
        public IReadOnlyList<AnimationSheet> Sheets { get; private set; }

        public AnimationSheet Find(string sheet)
        {
            foreach (var s in Sheets)
            {
                if (s.Sheet == sheet)
                {
                    return s;
                }
            }

            return null;
        }

        internal static AnimationData Read(SettingsReader r)
        {
            var data = new AnimationData
            {
                FrameSize = r.Integer("frameSize", 1, 4096),
            };
            data.Sheets = r.Each("sheets", s => AnimationSheet.Read(s, data.FrameSize));
            var seen = new HashSet<string>();
            foreach (var sheet in data.Sheets)
            {
                r.Check(seen.Add(sheet.Sheet), "sheets", $"lists '{sheet.Sheet}' twice");
            }

            return data;
        }
    }

    public sealed class AnimationSheet
    {
        public string Sheet { get; private set; }
        public IReadOnlyList<AnimationClip> Animations { get; private set; }

        public AnimationClip Find(string name)
        {
            foreach (var a in Animations)
            {
                if (a.Name == name)
                {
                    return a;
                }
            }

            return null;
        }

        internal static AnimationSheet Read(SettingsReader r, int frameSize)
        {
            var sheet = new AnimationSheet
            {
                Sheet = r.Text("sheet"),
                Animations = r.Each("animations", a => AnimationClip.Read(a, frameSize)),
            };
            var seen = new HashSet<string>();
            foreach (var a in sheet.Animations)
            {
                r.Check(seen.Add(a.Name), "animations", $"lists '{a.Name}' twice in {sheet.Sheet}");
            }

            return sheet;
        }
    }

    public sealed class AnimationClip
    {
        public string Name { get; private set; }
        public int Width { get; private set; }
        public int Frames { get; private set; }
        public float FrameTime { get; private set; }
        public bool Loop { get; private set; }

        internal static AnimationClip Read(SettingsReader r, int frameSize)
        {
            var clip = new AnimationClip
            {
                Name = r.Text("name"),
                Width = r.Integer("width", 1, 100000),
                Frames = r.Integer("frames", 1, 1000),
                FrameTime = r.Positive("frameTime"),
                Loop = r.Flag("loop"),
            };
            r.Check(frameSize <= 0 || clip.Frames == clip.Width / frameSize, "frames",
                $"must be width / frameSize truncated ({clip.Width} / {frameSize} = {(frameSize > 0 ? clip.Width / frameSize : 0)}) for '{clip.Name}'");
            return clip;
        }
    }

    /// <summary>grid.json: the 16 px grid, the bridge level and the pixel-to-unit mapping.</summary>
    public sealed class GridData
    {
        public int CellSize { get; private set; }
        public int MapWidth { get; private set; }
        public int MapHeight { get; private set; }
        public int FreshCellAlpha { get; private set; }
        public float[] FreshCellAssetBounds { get; private set; }
        public string TilesetAsset { get; private set; }
        public int FloorRow { get; private set; }
        public IReadOnlyList<int> WallColumns { get; private set; }
        public float[] BridgeTileBounds { get; private set; }
        public float PixelsPerUnit { get; private set; }
        public int StepsPerSecond { get; private set; }
        public Pair ReferenceScreen { get; private set; }

        /// <summary>Columns of cells (118 for 1900 / 16, truncated).</summary>
        public int Columns => MapWidth / CellSize;

        /// <summary>Rows of cells (75 for 1200 / 16, truncated).</summary>
        public int Rows => MapHeight / CellSize;

        internal static GridData Read(SettingsReader r)
        {
            var data = new GridData
            {
                CellSize = r.Integer("cellSize", 1, 1024),
                MapWidth = r.Integer("mapWidth", 1, 100000),
                MapHeight = r.Integer("mapHeight", 1, 100000),
                FreshCellAlpha = r.Integer("freshCellAlpha", 0, 255),
                FreshCellAssetBounds = r.Quad("freshCellAssetBounds", 0f),
                TilesetAsset = r.Text("tilesetAsset"),
            };
            var bridge = r.Section("bridge", b => new
            {
                Floor = b.Integer("floorRow", 0, 100000),
                Walls = b.Integers("wallColumns", 0, 100000),
                Tile = b.Quad("tileBounds", 0f),
            });
            data.FloorRow = bridge.Floor;
            data.WallColumns = bridge.Walls;
            data.BridgeTileBounds = bridge.Tile;
            var units = r.Section("units", u => new { Ppu = u.Positive("pixelsPerUnit"), Steps = u.Integer("stepsPerSecond", 1, 1000) });
            data.PixelsPerUnit = units.Ppu;
            data.StepsPerSecond = units.Steps;
            data.ReferenceScreen = r.Pair("referenceScreen", 1f);
            r.Check(data.MapWidth >= data.CellSize && data.MapHeight >= data.CellSize, "cellSize", "must fit inside the map");
            r.Check(data.CellSize <= 0 || data.FloorRow < data.MapHeight / data.CellSize, "bridge", "floorRow is outside the grid");
            foreach (var column in data.WallColumns)
            {
                r.Check(data.CellSize <= 0 || column < data.MapWidth / data.CellSize, "bridge", $"wall column {column} is outside the grid");
            }

            return data;
        }
    }

    /// <summary>camera.json: CameraManager's constants (RECONSTRUCTION 7).</summary>
    public sealed class CameraData
    {
        public Pair InitialPosition { get; private set; }
        public float InitialRotation { get; private set; }
        public float InitialScale { get; private set; }
        public CameraMode InitialMode { get; private set; }
        public List<CameraMode> ModeCycle { get; private set; }
        public float FollowGain { get; private set; }
        public float SnapBelow { get; private set; }
        public float Damping { get; private set; }
        public float ScrollStep { get; private set; }
        public Pair EditorScrollSteps { get; private set; }
        public float ZoomStep { get; private set; }
        public float RotateStep { get; private set; }
        public float ScaleExponent { get; private set; }

        internal static CameraData Read(SettingsReader r)
        {
            var data = new CameraData
            {
                InitialPosition = r.Pair("initialPosition"),
                InitialRotation = r.Number("initialRotation"),
                InitialScale = r.Positive("initialScale"),
                InitialMode = r.Choice<CameraMode>("initialMode"),
                ModeCycle = r.Choices<CameraMode>("modeCycle"),
                FollowGain = r.Positive("followGain"),
                SnapBelow = r.Number("snapBelow", 0f),
                Damping = r.Number("damping", 0.0001f, 0.9999f),
                ScrollStep = r.Positive("scrollStep"),
                EditorScrollSteps = r.Pair("editorScrollSteps", 0f),
                ZoomStep = r.Positive("zoomStep"),
                RotateStep = r.Positive("rotateStep"),
                ScaleExponent = r.Positive("scaleExponent"),
            };
            r.Check(data.ModeCycle.Contains(data.InitialMode), "initialMode", "must be in modeCycle");
            return data;
        }
    }

    /// <summary>console.json: the console window, typing and script constants (RECONSTRUCTION 10).</summary>
    public sealed class ConsoleData
    {
        public int Height { get; private set; }
        public int FullHeight { get; private set; }
        public float TextureAlpha { get; private set; }
        public int StartY { get; private set; }
        public int SlideDownStep { get; private set; }
        public int SlideDownEase { get; private set; }
        public int SlideEaseDivisor { get; private set; }
        public int SlideUpStep { get; private set; }
        public int HistoryLines { get; private set; }
        public int HistoryOffset { get; private set; }
        public int HistoryLineHeight { get; private set; }
        public int PromptX { get; private set; }
        public int PromptBlinkX { get; private set; }
        public int PromptFromBottom { get; private set; }
        public int TextX { get; private set; }
        public int TextFromBottom { get; private set; }
        public int HistoryX { get; private set; }
        public int BlinkMilliseconds { get; private set; }
        public string Prompt { get; private set; }
        public string Caret { get; private set; }
        public string Version { get; private set; }
        public string TextColour { get; private set; }
        public string HistoryColour { get; private set; }
        public string PromptColour { get; private set; }
        public string LongCue { get; private set; }
        public string ShortCue { get; private set; }

        internal static ConsoleData Read(SettingsReader r)
        {
            var data = new ConsoleData
            {
                Height = r.Integer("height", 1, 10000),
                FullHeight = r.Integer("fullHeight", 1, 10000),
                TextureAlpha = r.Number("textureAlpha", 0f, 1f),
                StartY = r.Integer("startY", -10000, 0),
                SlideDownStep = r.Integer("slideDownStep", 1, 1000),
                SlideDownEase = r.Integer("slideDownEase", 0, 1000),
                SlideEaseDivisor = r.Integer("slideEaseDivisor", 1, 1000),
                SlideUpStep = r.Integer("slideUpStep", 1, 1000),
                HistoryLines = r.Integer("historyLines", 1, 10000),
                HistoryOffset = r.Integer("historyOffset", 0, 10000),
                HistoryLineHeight = r.Integer("historyLineHeight", 1, 1000),
                PromptX = r.Integer("promptX", 0, 10000),
                PromptBlinkX = r.Integer("promptBlinkX", 0, 10000),
                PromptFromBottom = r.Integer("promptFromBottom", 0, 10000),
                TextX = r.Integer("textX", 0, 10000),
                TextFromBottom = r.Integer("textFromBottom", 0, 10000),
                HistoryX = r.Integer("historyX", 0, 10000),
                BlinkMilliseconds = r.Integer("blinkMilliseconds", 1, 100000),
                Prompt = r.Text("prompt"),
                Caret = r.Text("caret"),
                Version = r.Text("version"),
                TextColour = r.Text("textColour"),
                HistoryColour = r.Text("historyColour"),
                PromptColour = r.Text("promptColour"),
                LongCue = r.Text("longCue"),
                ShortCue = r.Text("shortCue"),
            };
            r.Check(data.FullHeight > data.Height, "fullHeight", "must be taller than the console's height");
            return data;
        }
    }

    /// <summary>audio.json: the cue table, categories and songs (RECONSTRUCTION 11).</summary>
    public sealed class AudioData
    {
        public float Volume { get; private set; }
        public bool Muted { get; private set; }
        public IReadOnlyList<AudioCategory> Categories { get; private set; }
        public IReadOnlyList<AudioCue> Cues { get; private set; }
        public IReadOnlyList<Song> Songs { get; private set; }

        public AudioCue FindCue(string cue)
        {
            foreach (var c in Cues)
            {
                if (c.Cue == cue)
                {
                    return c;
                }
            }

            return null;
        }

        internal static AudioData Read(SettingsReader r)
        {
            var data = new AudioData
            {
                Volume = r.Number("volume", 0f, 1f),
                Muted = r.Flag("muted"),
                Categories = r.Each("categories", c => new AudioCategory
                {
                    Name = c.Text("name"),
                    MaxInstances = c.Integer("maxInstances", 1, 255),
                }),
                Cues = r.Each("cues", AudioCue.Read),
                Songs = r.Each("songs", s => new Song
                {
                    Name = s.Text("name"),
                    File = s.Text("file"),
                    PlayedBy = s.Text("playedBy", true),
                }),
            };
            var seen = new HashSet<string>();
            foreach (var cue in data.Cues)
            {
                r.Check(seen.Add(cue.Cue), "cues", $"lists '{cue.Cue}' twice");
            }

            return data;
        }
    }

    public sealed class AudioCategory
    {
        public string Name { get; internal set; }
        public int MaxInstances { get; internal set; }
    }

    public sealed class Song
    {
        public string Name { get; internal set; }
        public string File { get; internal set; }
        public string PlayedBy { get; internal set; }
    }

    public sealed class AudioCue
    {
        public string Bank { get; private set; }
        public string Cue { get; private set; }
        public string Wave { get; private set; }
        public float Seconds { get; private set; }

        /// <summary>XACT volume in hundredths of a dB.</summary>
        public int Volume { get; private set; }

        /// <summary>XACT loop count (255 is infinite, 0 plays once).</summary>
        public int Loop { get; private set; }

        public string PlayedBy { get; private set; }

        internal static AudioCue Read(SettingsReader r) => new AudioCue
        {
            Bank = r.Text("bank"),
            Cue = r.Text("cue"),
            Wave = r.Text("wave"),
            Seconds = r.Number("seconds", 0f),
            Volume = r.Integer("volume", -10000, 0),
            Loop = r.Integer("loop", 0, 255),
            PlayedBy = r.Text("playedBy", true),
        };
    }

    /// <summary>menu.json: the main menu tree and its look (RECONSTRUCTION 9.2).</summary>
    public sealed class MenuData
    {
        public string Background { get; private set; }
        public float BackgroundAlpha { get; private set; }
        public string Arrow { get; private set; }
        public int ArrowOffset { get; private set; }
        public int ItemX { get; private set; }
        public int ItemY { get; private set; }
        public int ItemStep { get; private set; }
        public Pair TitlePosition { get; private set; }
        public int DescriptionFromBottom { get; private set; }
        public string ItemColour { get; private set; }
        public string SelectedColour { get; private set; }
        public string TextColour { get; private set; }
        public string MoveCue { get; private set; }
        public string SelectCue { get; private set; }
        public IReadOnlyList<MenuLevel> Levels { get; private set; }

        internal static MenuData Read(SettingsReader r)
        {
            var data = new MenuData
            {
                Background = r.Text("background"),
                BackgroundAlpha = r.Number("backgroundAlpha", 0f, 1f),
                Arrow = r.Text("arrow"),
                ArrowOffset = r.Integer("arrowOffset", -1000, 1000),
                ItemX = r.Integer("itemX", 0, 10000),
                ItemY = r.Integer("itemY", 0, 10000),
                ItemStep = r.Integer("itemStep", 1, 1000),
                TitlePosition = r.Pair("titlePosition", 0f),
                DescriptionFromBottom = r.Integer("descriptionFromBottom", 0, 10000),
                ItemColour = r.Text("itemColour"),
                SelectedColour = r.Text("selectedColour"),
                TextColour = r.Text("textColour"),
                MoveCue = r.Text("moveCue"),
                SelectCue = r.Text("selectCue"),
                Levels = r.Each("levels", MenuLevel.Read),
            };
            if (data.Levels.Count > 0)
            {
                r.Check(data.Levels[0].Parent == -1, "levels", "level 0 is the root and its parent must be -1");
            }

            for (var i = 0; i < data.Levels.Count; i++)
            {
                var level = data.Levels[i];
                r.Check(i == 0 || (level.Parent >= 0 && level.Parent < data.Levels.Count && level.Parent != i), "levels",
                    $"level {i} ('{level.Title}') has parent {level.Parent}, which is not another level");
                foreach (var item in level.Items)
                {
                    r.Check(item.Action != MenuAction.Open || (item.Target >= 0 && item.Target < data.Levels.Count), "levels",
                        $"item '{item.Text}' of level {i} opens level {item.Target}, which does not exist");
                }
            }

            return data;
        }
    }

    public sealed class MenuLevel
    {
        public string Title { get; private set; }
        public int Parent { get; private set; }
        public IReadOnlyList<MenuItem> Items { get; private set; }

        internal static MenuLevel Read(SettingsReader r) => new MenuLevel
        {
            Title = r.Text("title"),
            Parent = r.Integer("parent", -1, 1000),
            Items = r.Each("items", MenuItem.Read, allowEmpty: true),
        };
    }

    public sealed class MenuItem
    {
        public string Text { get; private set; }
        public string Description { get; private set; }
        public MenuAction Action { get; private set; }
        public int Target { get; private set; }

        internal static MenuItem Read(SettingsReader r) => new MenuItem
        {
            Text = r.Text("text"),
            Description = r.Text("description", true),
            Action = r.Choice<MenuAction>("action"),
            Target = r.Integer("target", -1, 1000),
        };
    }

    /// <summary>segments.json: the map segment definitions of maps.zdx (RECONSTRUCTION 5.1).</summary>
    public sealed class SegmentData
    {
        public IReadOnlyList<SegmentSource> Sources { get; private set; }
        public IReadOnlyList<SegmentDefinition> Definitions { get; private set; }

        internal static SegmentData Read(SettingsReader r)
        {
            var data = new SegmentData
            {
                Sources = r.Each("sources", s => new SegmentSource
                {
                    Id = s.Integer("id", 1, 1000),
                    Image = s.Text("image"),
                    Size = s.Pair("size", 1f),
                }),
                Definitions = r.Each("definitions", SegmentDefinition.Read),
            };
            var names = new HashSet<string>();
            foreach (var d in data.Definitions)
            {
                r.Check(names.Add(d.Name), "definitions", $"lists '{d.Name}' twice");
                SegmentSource source = null;
                foreach (var s in data.Sources)
                {
                    if (s.Id == d.Source)
                    {
                        source = s;
                    }
                }

                r.Check(source != null, "definitions", $"'{d.Name}' uses source {d.Source}, which is not listed");
                r.Check(d.X1 > d.X0 && d.Y1 > d.Y0, "definitions", $"'{d.Name}' must have x1 > x0 and y1 > y0");
                r.Check(source == null || (d.X1 <= source.Size.X && d.Y1 <= source.Size.Y), "definitions",
                    $"'{d.Name}' reaches outside its source image");
            }

            return data;
        }
    }

    public sealed class SegmentSource
    {
        public int Id { get; internal set; }
        public string Image { get; internal set; }
        public Pair Size { get; internal set; }
    }

    public sealed class SegmentDefinition
    {
        public string Name { get; private set; }
        public int Source { get; private set; }
        public int X0 { get; private set; }
        public int Y0 { get; private set; }
        public int X1 { get; private set; }
        public int Y1 { get; private set; }
        public int Flag { get; private set; }
        public int Width => X1 - X0;
        public int Height => Y1 - Y0;

        internal static SegmentDefinition Read(SettingsReader r)
        {
            var d = new SegmentDefinition
            {
                Name = r.Text("name"),
                Source = r.Integer("source", 1, 1000),
                Flag = r.Integer("flag", 0, 1000),
            };
            var rect = r.Quad("rect", 0f);
            d.X0 = (int)rect[0];
            d.Y0 = (int)rect[1];
            d.X1 = (int)rect[2];
            d.Y1 = (int)rect[3];
            return d;
        }
    }

    /// <summary>names.json: the name tables of RandomName (RECONSTRUCTION 4.1, 14).</summary>
    public sealed class NameData
    {
        public IReadOnlyList<string> Male { get; private set; }
        public IReadOnlyList<string> Female { get; private set; }
        public IReadOnlyList<string> Beast { get; private set; }

        public IReadOnlyList<string> For(Gender gender) => gender == Gender.Male ? Male : Female;

        internal static NameData Read(SettingsReader r) => new NameData
        {
            Male = r.Texts("male"),
            Female = r.Texts("female"),
            Beast = r.Texts("beast"),
        };
    }
}
