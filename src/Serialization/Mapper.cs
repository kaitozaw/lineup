namespace LineUp;

// Maps between GameState and GameStateSnapshot.
public static class GameStateMapper
{
    public static GameStateSnapshot ToSnapshot(GameState gameState)
    {
        return new GameStateSnapshot
        {
            GameCategory = gameState.GameCategory,
            PlayMode = gameState.PlayMode,
            PlayerSnapshots = gameState.Players.Select(PlayerMapper.ToSnapshot).ToArray(),
            Gridsize = gameState.GridSize,
            GridSnapshot = GridMapper.ToSnapshot(gameState.Grid),
            Turn = gameState.Turn,
            Round = gameState.Round
        };
    }

    public static GameState FromSnapshot(GameStateSnapshot snapshot)
    {
        IGamePolicy gamePolicy = GamePolicyFactory.Create(snapshot.GameCategory);
        GameState gameState = new GameState(snapshot.GameCategory, gamePolicy, snapshot.PlayMode, snapshot.Gridsize);
        PlayerMapper.FromSnapshot(gameState.Players, snapshot.PlayerSnapshots);
        gameState.Grid = GridMapper.FromSnapshot(snapshot.GridSnapshot);
        gameState.Turn = snapshot.Turn;
        gameState.Round = snapshot.Round;

        return gameState;
    }
}

// Maps between Player and PlayerSnapshot.
public static class PlayerMapper
{
    public static PlayerSnapshot ToSnapshot(Player player)
    {
        return new PlayerSnapshot
        {
            Number = player.Number,
            DiscInventory = player.SetInventoryToSnapshot()
        };
    }

    public static void FromSnapshot(Player[] players, PlayerSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var player = Array.Find(players, p => p.Number == snapshot.Number);
            if (player == null)
                throw new InvalidOperationException($"Player #{snapshot.Number} not found in current game state.");
            player.SetInventoryFromSnapshot(snapshot.DiscInventory);
        }
    }
}

// Maps between Grid and GridSnapshot.
public static class GridMapper
{
    public static GridSnapshot ToSnapshot(Grid grid)
    {
        var cells = new CellSnapshot[grid.Columns][];
        for (int column = 0; column < grid.Columns; column++)
        {
            cells[column] = new CellSnapshot[grid.Rows];
            for (int row = 0; row < grid.Rows; row++)
            {
                var disc = grid.GetDiscAt(column, row);
                cells[column][row] = disc is null ? new CellSnapshot() : new CellSnapshot { PlayerNumber = disc.PlayerNumber, DiscType = disc.DiscType };
            }
        }
        return new GridSnapshot
        {
            Columns = grid.Columns,
            Rows = grid.Rows,
            Cells = cells
        };
    }

    public static Grid FromSnapshot(GridSnapshot snapshot)
    {
        Grid grid = new Grid(snapshot.Columns, snapshot.Rows);
        for (int column = 0; column < snapshot.Columns; column++)
        {
            for (int row = 0; row < snapshot.Rows; row++)
            {
                var cell = snapshot.Cells[column][row];
                if (cell.PlayerNumber is int pn && cell.DiscType is DiscType dt)
                {
                    grid.SetDiscAt(column, row, new Disc(pn, dt));
                }
                else
                {
                    grid.SetDiscAt(column, row, null);
                }
            }
        }
        return grid;
    }
}