namespace LineUp;

// Base player with number and disc inventory operations.
public class Player
{
    public int Number { get; }
    private readonly DiscInventory discInventory;

    public Player(int number, DiscInventory discInventory)
    {
        if (number != 1 && number != 2) throw new ArgumentOutOfRangeException(nameof(number), number, "Player number must be 1 or 2.");
        this.Number = number;
        this.discInventory = discInventory;
    }

    public int GetDiscInventory(DiscType discType) => discInventory.Get(discType);

    public int GetTotalDiscInventory() => discInventory.GetTotal();

    public void AddDiscInventory(DiscType discType, int count = 1) => discInventory.Add(discType, count);

    public void ConsumeDiscInventory(DiscType discType, int count = 1) => discInventory.Consume(discType, count);

    public void SetInventoryFromSnapshot(Dictionary<DiscType, int> stock) => discInventory.FromSnapshot(stock);

    public Dictionary<DiscType, int> SetInventoryToSnapshot() => discInventory.ToSnapshot();
}

// Human-controlled player that parses text input into commands.
public class Human : Player
{
    public Human(int number, DiscInventory discInventory) : base(number, discInventory)
    {
    }

    public bool TryParseCommand(IGamePolicy gamePolicy, string? line, int maxColumns, out Command command)
    {
        command = default;

        if (string.IsNullOrWhiteSpace(line)) return false;
        if (maxColumns < 1) throw new ArgumentOutOfRangeException(nameof(maxColumns), maxColumns, "maxColumns must be >= 1.");

        line = line.Trim();
        char head = char.ToUpperInvariant(line[0]);

        if (line.Length == 1)
        {
            switch (head)
            {
                case 'S': command = new Command(CommandKind.Save); return true;
                case 'H': command = new Command(CommandKind.Help); return true;
                case 'Q': command = new Command(CommandKind.Quit); return true;
                default: return false;
            }
        }

        if (!gamePolicy.TryMapCharToDiscType(head, out DiscType discType)) return false;
        string digits = line.Substring(1).Trim();
        if (!int.TryParse(digits, out int column)) return false;
        if (column < 1 || column > maxColumns) return false;
        if (this.GetDiscInventory(discType) <= 0) return false;

        int columnZeroBased = column - 1;
        command = new Command(CommandKind.Move, DiscType: discType, Column: columnZeroBased);

        return true;
    }
}

// Computer-controlled player that decides next commands automatically.
public class Computer : Player
{
    private readonly Random random;

    public Computer(int number, DiscInventory discInventory, Random random) : base(number, discInventory)
    {
        this.random = random;
    }

    public Command DecideCommand(Grid grid)
    {
        var playableDiscTypes = Enum.GetValues<DiscType>().Where(discType => this.GetDiscInventory(discType) > 0).ToArray();
        var playableColumns = Enumerable.Range(0, grid.Columns).Where(column => !grid.IsColumnFull(column)).ToArray();

        if (playableDiscTypes.Length == 0) throw new InvalidOperationException("No discs left to play.");
        if (playableColumns.Length == 0) throw new InvalidOperationException("No valid columns available.");

        var bestMoves = grid.FindBestMoves(this, playableDiscTypes, playableColumns);
        var ordinaryPreferred = bestMoves.Where(move => move.discType == DiscType.Ordinary).ToList();
        var candidates = ordinaryPreferred.Count > 0 ? ordinaryPreferred : bestMoves;

        var (discType, column) = candidates[random.Next(candidates.Count)];
        if (this.GetDiscInventory(discType) <= 0) throw new InvalidOperationException();

        return new Command(CommandKind.Move, discType, column);
    }
}
