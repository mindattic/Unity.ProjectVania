using System.Collections.Generic;
using System.Linq;
using Unity.ProjectVania.Core.Display;

namespace Unity.ProjectVania.Core.Settings
{
    /// <summary>Where one of the <see cref="ActionCrossData"/>'s four slots sits (<c>ActionCross.cs</c>).</summary>
    public enum ActionCrossPosition
    {
        Top,
        Left,
        Right,
        Bottom,
    }

    /// <summary>
    /// content.json: everything the original built but never wired into play (RECONSTRUCTION 5.1, 9.1, 13, 15),
    /// kept as data only so the rebuild loses none of it (U.VAN-US-A16). Nothing in Core or Runtime reads this data
    /// for behaviour.
    /// </summary>
    public sealed class ContentData
    {
        public SegmentAtlasData SegmentAtlas { get; private set; }
        public LedgeShapeData Ledges { get; private set; }
        public SunData Sun { get; private set; }
        public ActionCrossData ActionCross { get; private set; }
        public PortraitInventory Portraits { get; private set; }
        public IconInventory Icons { get; private set; }
        public AreaLayerInventory AreaLayers { get; private set; }
        public AssetInventory Particles { get; private set; }
        public AssetInventory Selectors { get; private set; }
        public AssetInventory Banners { get; private set; }
        public AssetInventory SpriteSheets { get; private set; }
        public UnwiredCharacterArt UnwiredCharacterArt { get; private set; }

        internal static ContentData Read(SettingsReader r)
        {
            var data = new ContentData
            {
                SegmentAtlas = r.Section("segmentAtlas", SegmentAtlasData.Read),
                Ledges = r.Section("ledges", LedgeShapeData.Read),
                Sun = r.Section("sun", SunData.Read),
                ActionCross = r.Section("actionCross", ActionCrossData.Read),
                Portraits = r.Section("portraits", PortraitInventory.Read),
                Icons = r.Section("icons", IconInventory.Read),
                AreaLayers = r.Section("areaLayers", AreaLayerInventory.Read),
                Particles = r.Section("particles", AssetInventory.Read),
                Selectors = r.Section("selectors", AssetInventory.Read),
                Banners = r.Section("banners", AssetInventory.Read),
                SpriteSheets = r.Section("spriteSheets", AssetInventory.Read),
                UnwiredCharacterArt = r.Section("unwiredCharacterArt", UnwiredCharacterArt.Read),
            };
            r.Check(data.Particles.Assets.Count == 5, "particles", "must list the original's five particle pictures");
            r.Check(data.Selectors.Assets.Count == 5, "selectors", "must list the original's five selector sizes");
            r.Check(data.Banners.Assets.Count == 15, "banners", "must list the original's fifteen banners");
            r.Check(data.SpriteSheets.Assets.Count == 16, "spriteSheets", "must list the original's sixteen character sheets");
            return data;
        }
    }

    /// <summary><c>MapEditor\maps1.png</c> (RECONSTRUCTION 5.1): the source image segments.json's definitions cut from.</summary>
    public sealed class SegmentAtlasData
    {
        public string Asset { get; private set; }
        public Pair Size { get; private set; }

        internal static SegmentAtlasData Read(SettingsReader r) => new SegmentAtlasData
        {
            Asset = r.Text("asset"),
            Size = r.Pair("size", 1f),
        };
    }

    /// <summary><c>Map.cs</c>'s <c>Ledge[16]</c> (RECONSTRUCTION 5): the shape of the dead allocation, not content.</summary>
    public sealed class LedgeShapeData
    {
        public int Slots { get; private set; }
        public int NodesPerLedge { get; private set; }

        internal static LedgeShapeData Read(SettingsReader r)
        {
            var data = new LedgeShapeData
            {
                Slots = r.Integer("slots", 1, 1000),
                NodesPerLedge = r.Integer("nodesPerLedge", 1, 1000),
            };
            r.Check(data.Slots == 16, "slots", "must be 16, as Map.cs allocated");
            r.Check(data.NodesPerLedge == 16, "nodesPerLedge", "must be 16, as Ledge.cs allocated");
            return data;
        }
    }

    /// <summary><c>World\Sun.cs</c> (RECONSTRUCTION 9.1, defect 12): the day-night cycle that never ticks.</summary>
    public sealed class SunData
    {
        public float TickMilliseconds { get; private set; }
        public int AlphaStep { get; private set; }
        public IReadOnlyList<SunPeriod> Periods { get; private set; }

        internal static SunData Read(SettingsReader r)
        {
            var data = new SunData
            {
                TickMilliseconds = r.Positive("tickMilliseconds"),
                AlphaStep = r.Integer("alphaStep", 1, 255),
                Periods = r.Each("periods", SunPeriod.Read),
            };
            r.Check(data.Periods.Count == 4, "periods", "must list all four of Day, Dusk, Night and Dawn");
            var expected = new[] { "Day", "Dusk", "Night", "Dawn" };
            for (var i = 0; i < data.Periods.Count && i < expected.Length; i++)
            {
                r.Check(data.Periods[i].Name == expected[i], "periods", $"entry {i} must be '{expected[i]}' (PeriodOfDay's own order)");
            }

            return data;
        }
    }

    public sealed class SunPeriod
    {
        public string Name { get; private set; }
        public Rgba Colour { get; private set; }

        internal static SunPeriod Read(SettingsReader r) => new SunPeriod
        {
            Name = r.Text("name"),
            Colour = Rgba.FromQuad(r.Quad("colour", 0f, 255f)),
        };
    }

    /// <summary><c>Gui\ActionCross.cs</c> (RECONSTRUCTION 9.1, defect 12): commented out in GuiManager, never built.</summary>
    public sealed class ActionCrossData
    {
        public string Glass { get; private set; }
        public string Frame { get; private set; }
        public Pair Size { get; private set; }
        public Pair SlotSize { get; private set; }
        public IReadOnlyList<ActionCrossSlot> Slots { get; private set; }

        internal static ActionCrossData Read(SettingsReader r)
        {
            var data = new ActionCrossData
            {
                Glass = r.Text("glass"),
                Frame = r.Text("frame"),
                Size = r.Pair("size", 1f),
                SlotSize = r.Pair("slotSize", 1f),
                Slots = r.Each("slots", ActionCrossSlot.Read),
            };
            r.Check(data.Slots.Count == 4, "slots", "must list all four of the cross's slots");
            var seen = new HashSet<ActionCrossPosition>();
            foreach (var slot in data.Slots)
            {
                r.Check(seen.Add(slot.Position), "slots", $"lists '{slot.Position}' twice");
            }

            return data;
        }
    }

    public sealed class ActionCrossSlot
    {
        public ActionCrossPosition Position { get; private set; }
        public string Icon { get; private set; }

        internal static ActionCrossSlot Read(SettingsReader r) => new ActionCrossSlot
        {
            Position = r.Choice<ActionCrossPosition>("position"),
            Icon = r.Text("icon"),
        };
    }

    /// <summary><c>Gui\Portraits</c> (RECONSTRUCTION 9.1, 13): the one portrait drawn and the six that never are.</summary>
    public sealed class PortraitInventory
    {
        public string Used { get; private set; }
        public IReadOnlyList<string> Unused { get; private set; }

        internal static PortraitInventory Read(SettingsReader r)
        {
            var data = new PortraitInventory
            {
                Used = r.Text("used"),
                Unused = r.Texts("unused"),
            };
            r.Check(data.Unused.Count == 6, "unused", "must list the other six portraits");
            r.Check(!data.Unused.Contains(data.Used), "unused", $"must not list '{data.Used}' again; it is already 'used'");
            return data;
        }
    }

    /// <summary><c>Gui\Icons</c> (RECONSTRUCTION 13, defect 14): the action buttons and skill icons, all unused.</summary>
    public sealed class IconInventory
    {
        public IReadOnlyList<string> ActionButtons { get; private set; }
        public IReadOnlyList<string> Skills { get; private set; }

        internal static IconInventory Read(SettingsReader r)
        {
            var data = new IconInventory
            {
                ActionButtons = r.Texts("actionButtons"),
                Skills = r.Texts("skills"),
            };
            r.Check(data.ActionButtons.Count == 4, "actionButtons", "must list all four action buttons");
            r.Check(data.Skills.Count == 29, "skills", "must list all twenty-nine skill icons");
            var seen = new HashSet<string>();
            foreach (var skill in data.Skills)
            {
                r.Check(seen.Add(skill), "skills", $"lists '{skill}' twice");
            }

            return data;
        }
    }

    /// <summary>Areas (RECONSTRUCTION 5, 13): the one layer drawn and the four parallax layers never loaded.</summary>
    public sealed class AreaLayerInventory
    {
        public string Drawn { get; private set; }
        public IReadOnlyList<string> Unused { get; private set; }

        internal static AreaLayerInventory Read(SettingsReader r)
        {
            var data = new AreaLayerInventory
            {
                Drawn = r.Text("drawn"),
                Unused = r.Texts("unused"),
            };
            r.Check(data.Unused.Count == 4, "unused", "must list the four parallax layers");
            r.Check(!data.Unused.Contains(data.Drawn), "unused", $"must not list '{data.Drawn}' again; it is already 'drawn'");
            return data;
        }
    }

    /// <summary>A flat list of asset names kept as content only (particles, selectors, banners, sprite sheets).</summary>
    public sealed class AssetInventory
    {
        public IReadOnlyList<string> Assets { get; private set; }

        internal static AssetInventory Read(SettingsReader r)
        {
            var data = new AssetInventory
            {
                Assets = r.Texts("assets"),
            };
            var seen = new HashSet<string>();
            foreach (var asset in data.Assets)
            {
                r.Check(seen.Add(asset), "assets", $"lists '{asset}' twice");
            }

            return data;
        }
    }

    /// <summary>
    /// <c>Characters\Playable\Richter</c> (RECONSTRUCTION 13): idle and run art the original never loaded and the
    /// rebuild never made playable (unlike Shanoa and Leo, U.VAN-T25).
    /// </summary>
    public sealed class UnwiredCharacterArt
    {
        public string Sheet { get; private set; }
        public Pair Size { get; private set; }

        internal static UnwiredCharacterArt Read(SettingsReader r) => new UnwiredCharacterArt
        {
            Sheet = r.Text("sheet"),
            Size = r.Pair("size", 1f),
        };
    }
}
