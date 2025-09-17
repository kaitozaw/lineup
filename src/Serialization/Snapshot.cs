namespace LineUp;

// Serializable snapshot of a game state
public sealed class GameStateSnapshot
{
    public GameCategory GameCategory { get; set; }
    public PlayMode PlayMode { get; set; }
    public PlayerSnapshot[] PlayerSnapshots { get; set; } = Array.Empty<PlayerSnapshot>();
    public GridSize Gridsize { get; set; }
    public GridSnapshot GridSnapshot { get; set; } = new GridSnapshot();
    public int Turn { get; set; }
    public int Round { get; set; }
}

// Serializable snapshot of a player
public sealed class PlayerSnapshot
{
    public int Number { get; set; }
    public Dictionary<DiscType, int> DiscInventory { get; set; } = new();
}

// Serializable snapshot of a grid
public sealed class GridSnapshot
{
    public int Columns { get; set; }
    public int Rows { get; set; }
    public CellSnapshot[][] Cells { get; set; } = default!;
}

// Serializable snapshot of a cell
public sealed class CellSnapshot
{
    public int? PlayerNumber { get; set; }
    public DiscType? DiscType { get; set; }
}