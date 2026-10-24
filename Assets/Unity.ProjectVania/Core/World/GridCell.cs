namespace Unity.ProjectVania.Core.World
{
    /// <summary>
    /// A cell's state (<c>GridCell.CellState</c>, RECONSTRUCTION 5). Only <see cref="Hard"/> collides; <see cref="Soft"/>
    /// is declared, drawn like Hard, and never assigned or collided by the original, so it is kept and inert.
    /// </summary>
    public enum CellState
    {
        Hard,
        Soft,
        Empty,
    }

    /// <summary>A cell index on the grid (the original's <c>Point</c> from <c>GetGridCoordinates</c>).</summary>
    public readonly struct CellIndex
    {
        public CellIndex(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public override string ToString() => $"[{X}, {Y}]";
    }

    /// <summary>A pixel rectangle in the tileset (a cell's <c>assetBounds</c>).</summary>
    public readonly struct TileRect
    {
        public TileRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>A settings quad [x, y, width, height], truncated to whole pixels.</summary>
        public static TileRect FromQuad(float[] quad) => new TileRect((int)quad[0], (int)quad[1], (int)quad[2], (int)quad[3]);

        public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
    }

    /// <summary>
    /// One 16 px cell (<c>MapEditor\GridCell.cs</c>): its state, the alpha of its white tint (64 for a fresh cell, 255
    /// for a placed one) and its source rectangle in the tileset. Its position is its index times the cell size.
    /// </summary>
    public readonly struct GridCell
    {
        public GridCell(CellState state, byte alpha, TileRect tile)
        {
            State = state;
            Alpha = alpha;
            Tile = tile;
        }

        public CellState State { get; }
        public byte Alpha { get; }
        public TileRect Tile { get; }

        /// <summary>Hard or Soft: the cells <c>Map.DrawGrid</c> draws outside the editor.</summary>
        public bool IsSolidLooking => State == CellState.Hard || State == CellState.Soft;
    }

    /// <summary>
    /// How a player treats a <see cref="Map.CheckHorizontalCollision"/> count (RECONSTRUCTION 4.4, the partial-wall rule).
    /// </summary>
    public enum WallContact
    {
        /// <summary>No hard cell ahead: horizontal acceleration applies.</summary>
        None,

        /// <summary>Some but not all of the player's height is blocked (a step or ledge): neither accelerated nor stopped, so drag decays him.</summary>
        Partial,

        /// <summary>Every cell of the player's height is blocked: horizontal velocity is held at zero.</summary>
        Full,
    }
}
