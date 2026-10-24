using System.Collections.Generic;
using Unity.ProjectVania.Core.Commands;
using Unity.ProjectVania.Core.Display;
using Unity.ProjectVania.Core.Settings;
using Unity.ProjectVania.Core.Simulation;
using Unity.ProjectVania.Core.World;
using UnityEngine;

namespace Unity.ProjectVania
{
    /// <summary>
    /// Draws the map as <c>Map.Draw</c> did (RECONSTRUCTION 5, 6): the 1 px outline, then each layer whole and unscaled
    /// (the painted mountain-farmhouse backdrop, area_1, 1920 x 1200 at (0, 0)), then the grid's cells, all under the
    /// characters. The cells are painted by Core (<see cref="MapDrawing.PaintCells"/>) from the tileset's pixels into one
    /// point-filtered texture, repainted only when the map changes or the game enters or leaves the editor (where every
    /// cell shows). It only reads the world. Art is <c>Resources/World/&lt;asset&gt;.png</c>, named as the original's
    /// TextureManager keys.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class MapView : MonoBehaviour
    {
        public const string ResourceFolder = "World";

        /// <summary>The prefix of a tileset's file name (tileset_default); the importer makes those readable.</summary>
        public const string TilesetPrefix = "tileset_";

        /// <summary>The map draws under the characters (whose sorting orders start at 0), in the original's order.</summary>
        public const int OutlineOrder = -300;
        public const int FirstLayerOrder = -200;
        public const int CellsOrder = -100;

        GameWorld _world;
        float _pixelsPerUnit;
        PixelImage _tileset;
        PixelImage _canvas;
        Texture2D _cellTexture;
        Color32[] _upload;
        int _paintedRevision = -1;
        GameState? _paintedState;
        readonly List<SpriteRenderer> _outline = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _layers = new List<SpriteRenderer>();

        public IReadOnlyList<SpriteRenderer> Outline => _outline;
        public IReadOnlyList<SpriteRenderer> Layers => _layers;
        public SpriteRenderer Cells { get; private set; }

        /// <summary>The cell texture's pixels as Core painted them (row 0 at the top), or null without a tileset.</summary>
        public PixelImage CellCanvas => _canvas;

        /// <summary>How many times the cells have been painted (once at start, then once per change).</summary>
        public int Repaints { get; private set; }

        /// <summary>The Resources path of one texture.</summary>
        public static string PathOf(string asset) => $"{ResourceFolder}/{asset}";

        public void Initialize(GameWorld world, GridData grid)
        {
            _world = world;
            _pixelsPerUnit = grid.PixelsPerUnit;
            var draw = grid.Draw;

            BuildOutline(draw, grid.MapWidth, grid.MapHeight);
            if (draw.ShowLayers)
            {
                for (var i = 0; i < draw.Layers.Count; i++)
                {
                    BuildLayer(draw.Layers[i], FirstLayerOrder + i);
                }
            }

            BuildCells(grid.TilesetAsset);
            Present();
        }

        void LateUpdate()
        {
            Present();
        }

        /// <summary>Repaints the cells if the grid changed or the game entered or left the editor since the last paint.</summary>
        public void Present()
        {
            if (_world == null || _canvas == null)
            {
                return;
            }

            var state = _world.State;
            var drawsAll = state == GameState.MapEditor;
            var paintedAll = _paintedState == GameState.MapEditor;
            if (_paintedRevision == _world.Map.Revision && _paintedState.HasValue && drawsAll == paintedAll)
            {
                return;
            }

            MapDrawing.PaintCells(_world.Map, state, _tileset, _canvas);
            Upload(_canvas, _cellTexture, _upload);
            _paintedRevision = _world.Map.Revision;
            _paintedState = state;
            Repaints++;
        }

        void BuildOutline(MapDrawData draw, int width, int height)
        {
            var group = Child("Outline");
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "OutlinePixel", filterMode = FilterMode.Point };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            var sprite = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(0f, 1f), _pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "OutlinePixel";
            var strips = MapDrawing.Outline(width, height, draw.OutlineThickness);
            for (var i = 0; i < strips.Count; i++)
            {
                var strip = strips[i];
                var renderer = Renderer(group, $"Strip {i}", sprite, OutlineOrder);
                renderer.color = ToColor(draw.OutlineColour);
                renderer.transform.localPosition = TopLeft(strip.X, strip.Y);
                renderer.transform.localScale = new Vector3(strip.Width, strip.Height, 1f); // the 1 px sprite stretched, as Shape did
                _outline.Add(renderer);
            }
        }

        void BuildLayer(MapLayerData layer, int order)
        {
            var path = PathOf(layer.Asset);
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError($"[ProjectVania] The layer texture {path} is missing.");
                return;
            }

            // The whole texture, unscaled, its top-left at the layer's position (SpriteBatch.Draw(texture, position, colour)).
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0f, 1f), _pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = layer.Asset;
            var renderer = Renderer(Child($"Layer {layer.Asset}"), null, sprite, order);
            renderer.color = ToColor(layer.Colour);
            renderer.transform.localPosition = TopLeft(layer.Position.X, layer.Position.Y);
            _layers.Add(renderer);
        }

        void BuildCells(string tilesetAsset)
        {
            var path = PathOf(tilesetAsset);
            var tileset = Resources.Load<Texture2D>(path);
            if (tileset == null)
            {
                Debug.LogError($"[ProjectVania] The tileset {path} is missing.");
                return;
            }

            if (!tileset.isReadable)
            {
                Debug.LogError($"[ProjectVania] The tileset {path} is not readable; the cells are painted from its pixels.");
                return;
            }

            _tileset = Download(tileset);
            _canvas = MapDrawing.CellCanvas(_world.Map);
            _upload = new Color32[_canvas.Width * _canvas.Height];
            _cellTexture = new Texture2D(_canvas.Width, _canvas.Height, TextureFormat.RGBA32, false)
            {
                name = "Cells",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var sprite = Sprite.Create(_cellTexture, new Rect(0, 0, _canvas.Width, _canvas.Height), new Vector2(0f, 1f), _pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "Cells";
            Cells = Renderer(Child("Cells"), null, sprite, CellsOrder);
            Cells.transform.localPosition = TopLeft(0f, 0f);
        }

        /// <summary>A texture's pixels as Core reads them (row 0 at the top; Unity's rows run bottom-up).</summary>
        public static PixelImage Download(Texture2D texture)
        {
            var image = new PixelImage(texture.width, texture.height);
            var pixels = texture.GetPixels32();
            for (var y = 0; y < texture.height; y++)
            {
                var row = (texture.height - 1 - y) * texture.width;
                for (var x = 0; x < texture.width; x++)
                {
                    var c = pixels[row + x];
                    image[x, y] = new Rgba(c.r, c.g, c.b, c.a);
                }
            }

            return image;
        }

        static void Upload(PixelImage image, Texture2D texture, Color32[] buffer)
        {
            var source = image.Pixels;
            for (var y = 0; y < image.Height; y++)
            {
                var from = y * image.Width;
                var to = (image.Height - 1 - y) * image.Width;
                for (var x = 0; x < image.Width; x++)
                {
                    var c = source[from + x];
                    buffer[to + x] = new Color32(c.R, c.G, c.B, c.A);
                }
            }

            texture.SetPixels32(buffer);
            texture.Apply(false);
        }

        Transform Child(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(transform, false);
            return child;
        }

        static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, int order)
        {
            SpriteRenderer renderer;
            if (name == null)
            {
                renderer = parent.gameObject.AddComponent<SpriteRenderer>();
            }
            else
            {
                renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                renderer.transform.SetParent(parent, false);
            }

            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>A top-left corner in the map's pixels (y down) as a Unity position (y up).</summary>
        Vector3 TopLeft(float x, float y) => new Vector3(x / _pixelsPerUnit, -y / _pixelsPerUnit, 0f);

        public static Color32 ToColor(Rgba c) => new Color32(c.R, c.G, c.B, c.A);
    }
}
