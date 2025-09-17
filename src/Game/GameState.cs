namespace LineUp;

// Holds the runtime state of the current game session.
public sealed class GameState
{
    public GameCategory GameCategory { get; }
    public IGamePolicy GamePolicy { get; }
    public PlayMode PlayMode { get; }
    public Player[] Players { get; }
    public GridSize GridSize { get; }
    public Grid Grid { get; set; }
    public Ui Ui { get; set; }
    public int WinCondition { get; }
    public int Turn { get; set; }
    public int Round { get; set; }

    public GameState(GameCategory gameCategory, IGamePolicy gamePolicy, PlayMode playMode, GridSize gridSize)
    {
        Player player1 = new Human(1, gamePolicy.CreateDiscInventory(gridSize));
        Player player2;
        Random random = new Random();
        if (playMode == PlayMode.HumanVsHuman)
            player2 = new Human(2, gamePolicy.CreateDiscInventory(gridSize));
        else
            player2 = new Computer(2, gamePolicy.CreateDiscInventory(gridSize), random);

        this.GameCategory = gameCategory;
        this.GamePolicy = gamePolicy;
        this.PlayMode = playMode;
        this.Players = new Player[] { player1, player2 };
        this.GridSize = gridSize;
        this.Grid = new Grid(gridSize.Columns, gridSize.Rows);
        this.Ui = new Ui(gridSize.Columns, gridSize.Rows);
        this.WinCondition = (gridSize.Columns * gridSize.Rows) / 10;
        this.Turn = 0;
        this.Round = 0;
    }
}