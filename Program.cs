using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading;

public enum MainMenu { New, Load, Test, Quit }

public enum GameCategory { Classic, Basic, Spin }

public enum PlayMode { HumanVsHuman, HumanVsComputer };

public readonly record struct GridSize(int Columns, int Rows);

public enum CommandKind { Move, Save, Help, Quit };

public enum DiscType { Ordinary, Boring, Magnetic, Exploding };

public readonly record struct Command(CommandKind CommandKind, DiscType? DiscType = null, int? Column = null);

public readonly record struct MoveResult(int Column, int Row, Disc? ChangeFrom, Disc? ChangeTo);

public readonly record struct MoveResults(IReadOnlyList<MoveResult> Items);

// Interface for game policy with defaults for play mode, grid size, disc mapping and grid rotation
public interface IGamePolicy
{
    PlayMode ConfigurePlayMode(IConfigurator<PlayMode> playModeConfigurator);

    GridSize ConfigureGridSize(IConfigurator<GridSize> gridConfigurator);

    IReadOnlyCollection<DiscType> AllowedDiscTypes { get; }

    DiscInventory CreateDiscInventory(GridSize gridSize);

    bool TryMapCharToDiscType(char head, out DiscType type);

    int? RotateEveryNRounds => null;
}

// Base implementation of game policy.
public abstract class GamePolicyBase : IGamePolicy
{
    public virtual PlayMode ConfigurePlayMode(IConfigurator<PlayMode> playModeConfigurator) => playModeConfigurator.Get();

    public virtual GridSize ConfigureGridSize(IConfigurator<GridSize> gridConfigurator) => new GridSize(9, 8);

    public abstract IReadOnlyCollection<DiscType> AllowedDiscTypes { get; }

    public virtual DiscInventory CreateDiscInventory(GridSize gridSize)
    {
        int budget = (gridSize.Columns * gridSize.Rows) / 2;
        const int perSpecial = 2;
        var initialStock = AllowedDiscTypes.Where(t => t != DiscType.Ordinary).ToDictionary(t => t, _ => perSpecial);
        int specialTotal = initialStock.Values.Sum();
        if (AllowedDiscTypes.Contains(DiscType.Ordinary))
        {
            int ordinary = Math.Max(0, budget - specialTotal);
            initialStock[DiscType.Ordinary] = ordinary;
        }
        return new DiscInventory(initialStock);
    }

    public virtual bool TryMapCharToDiscType(char head, out DiscType discType)
    {
        char key = char.ToUpperInvariant(head);
        if (DiscTypeCode.Map.TryGetValue(key, out var t) && AllowedDiscTypes.Contains(t))
        {
            discType = t;
            return true;
        }
        discType = default;
        return false;
    }

    public virtual int? RotateEveryNRounds => null;
}

// Game policy enabling classic rules.
public sealed class ClassicGamePolicy : GamePolicyBase
{
    public override GridSize ConfigureGridSize(IConfigurator<GridSize> gridConfigurator) => gridConfigurator.Get();
    public override IReadOnlyCollection<DiscType> AllowedDiscTypes { get; } = new[] { DiscType.Ordinary, DiscType.Boring, DiscType.Magnetic };
}

// Game policy enabling basic rules.
public sealed class BasicGamePolicy : GamePolicyBase
{
    public override IReadOnlyCollection<DiscType> AllowedDiscTypes { get; } = new[] { DiscType.Ordinary };
}

// Game policy enabling spin rules.
public sealed class SpinGamePolicy : GamePolicyBase
{
    public override IReadOnlyCollection<DiscType> AllowedDiscTypes { get; } = new[] { DiscType.Ordinary };
    public override int? RotateEveryNRounds => 5;
}

// Creates a game policy instance based on the game category.
public static class GamePolicyFactory
{
    public static IGamePolicy Create(GameCategory gameCategory) => gameCategory switch
    {
        GameCategory.Classic => new ClassicGamePolicy(),
        GameCategory.Basic => new BasicGamePolicy(),
        GameCategory.Spin => new SpinGamePolicy(),
        _ => throw new ArgumentOutOfRangeException(nameof(gameCategory), gameCategory, "Game category does not exist.")
    };
}

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

// Serializable snapshot of a player
public sealed class PlayerSnapshot
{
    public int Number { get; set; }
    public Dictionary<DiscType, int> DiscInventory { get; set; } = new();
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

// // Interface for providing a configuration value of type T.
public interface IConfigurator<T>
{
    T Get();
}

// Reads the main menu from the console.
public sealed class ConsoleMainMenuConfigurator : IConfigurator<MainMenu>
{
    public MainMenu Get()
    {
        return InputHelper.ReadMainMenu();
    }
}

// Reads the game category from the console.
public sealed class ConsoleGameCategoryConfigurator : IConfigurator<GameCategory>
{
    public GameCategory Get()
    {
        return InputHelper.ReadGameCategory();
    }
}

// Provides a fixed game category for testing.
public sealed class TestGameCategoryConfigurator : IConfigurator<GameCategory>
{
    public GameCategory Get()
    {
        return GameCategory.Classic;
    }
}

// Reads the play mode from the console.
public sealed class ConsolePlayModeConfigurator : IConfigurator<PlayMode>
{
    public PlayMode Get()
    {
        return InputHelper.ReadPlayMode();
    }
}

// Provides a fixed play mode for testing.
public sealed class TestPlayModeConfigurator : IConfigurator<PlayMode>
{
    public PlayMode Get()
    {
        return PlayMode.HumanVsHuman;
    }
}

// Reads the grid size from the console.
public sealed class ConsoleGridSizeConfigurator : IConfigurator<GridSize>
{
    public GridSize Get()
    {
        var (columns, rows) = InputHelper.ReadGridSize();
        return new GridSize(columns, rows);
    }
}

// Provides a fixed grid size for testing.
public sealed class TestGridSizeConfigurator : IConfigurator<GridSize>
{
    public GridSize Get()
    {
        return new GridSize(7, 6);
    }
}

// Console input helpers for menus and settings.
public static class InputHelper
{
    public static MainMenu ReadMainMenu()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("==== LineUp ====");
            Console.WriteLine("[N] New Game");
            Console.WriteLine("[L] Load Game");
            Console.WriteLine("[T] Test Game");
            Console.WriteLine("[Q] Quit");
            Console.Write("Menu: ");

            string? s = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(s))
            {
                char c = char.ToUpperInvariant(s.Trim()[0]);
                switch (c)
                {
                    case 'N': return MainMenu.New;
                    case 'L': return MainMenu.Load;
                    case 'T': return MainMenu.Test;
                    case 'Q': return MainMenu.Quit;
                }
            }

            Console.WriteLine("Invalid input. Press any key to continue...");
            Console.ReadKey(true);
        }
    }

    public static GameCategory ReadGameCategory()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("==== LineUp ====");
            Console.WriteLine("[0] Classic");
            Console.WriteLine("[1] Basic");
            Console.WriteLine("[2] Spin");
            Console.Write("Category: ");
            string? m = Console.ReadLine();

            if (int.TryParse(m, out int category) && (category == 0 || category == 1 || category == 2))
            {
                return (GameCategory)category;
            }

            Console.WriteLine("Invalid input. Press any key to continue...");
            Console.ReadKey();
        }
    }

    public static PlayMode ReadPlayMode()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("==== LineUp ====");
            Console.WriteLine("[0] Human vs Human");
            Console.WriteLine("[1] Human vs Computer");
            Console.Write("Mode: ");
            string? m = Console.ReadLine();

            if (int.TryParse(m, out int playMode) && (playMode == 0 || playMode == 1))
            {
                return (PlayMode)playMode;
            }

            Console.WriteLine("Invalid input. Press any key to continue...");
            Console.ReadKey();
        }
    }

    public static (int Columns, int Rows) ReadGridSize()
    {
        int columns;
        int rows;
        while (true)
        {
            Console.Clear();
            Console.WriteLine("==== LineUp ====");
            Console.Write("Number of columns: ");
            string? c = Console.ReadLine();
            Console.Write("Number of rows: ");
            string? r = Console.ReadLine();

            if (int.TryParse(c, out columns) && int.TryParse(r, out rows) && columns >= 7 && rows >= 6 && columns >= rows)
            {
                return (columns, rows);
            }

            Console.WriteLine("Invalid Input. Column must be larger than 7. Row must be larger than 6. Column must be equal to or larger than Row. Press any key to continue...");
            Console.ReadKey();
        }
    }
}

// Lookup map from character codes to DiscType.
public static class DiscTypeCode
{
    public static readonly IReadOnlyDictionary<char, DiscType> Map = new Dictionary<char, DiscType>
    {
        ['O'] = DiscType.Ordinary,
        ['B'] = DiscType.Boring,
        ['M'] = DiscType.Magnetic,
        ['E'] = DiscType.Exploding,
    };
}

// Disc inventory that tracks counts of available discs per type for a player.
public class DiscInventory
{
    private readonly Dictionary<DiscType, int> stock;

    public DiscInventory(Dictionary<DiscType, int> initialStock)
    {
        stock = new Dictionary<DiscType, int>(initialStock);
    }

    public int Get(DiscType discType) => stock.TryGetValue(discType, out var count) ? count : 0;

    public int GetTotal() => stock.Values.Sum();

    public void Add(DiscType discType, int count = 1)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "count must be > 0.");

        if (stock.ContainsKey(discType))
            stock[discType] += count;
        else
            stock[discType] = count;
    }

    public void Consume(DiscType discType, int count = 1)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "count must be > 0.");
        if (!stock.ContainsKey(discType) || stock[discType] < count) throw new ArgumentOutOfRangeException(nameof(count), count, "count must be less than stock.");
        stock[discType] -= count;
    }

    public void FromSnapshot(Dictionary<DiscType, int> savedStock)
    {
        stock.Clear();
        foreach (var (k, v) in savedStock)
        {
            if (v < 0) throw new ArgumentOutOfRangeException(nameof(savedStock), "count must be >= 0.");
            if (v > 0) stock[k] = v;
        }
    }

    public Dictionary<DiscType, int> ToSnapshot()
    {
        return Enum.GetValues<DiscType>().ToDictionary(dt => dt, dt => Get(dt));
    }
}

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

// A disc with player number, disc type and character.
public class Disc
{
    public int PlayerNumber { get; }
    public DiscType DiscType { get; }
    public char Character { get; }

    public Disc(int playerNumber, DiscType discType)
    {
        this.PlayerNumber = playerNumber;
        this.DiscType = discType;
        this.Character = (playerNumber, discType) switch
        {
            (1, DiscType.Ordinary) => '@',
            (1, DiscType.Boring) => 'B',
            (1, DiscType.Magnetic) => 'M',
            (1, DiscType.Exploding) => 'E',
            (2, DiscType.Ordinary) => '#',
            (2, DiscType.Boring) => 'b',
            (2, DiscType.Magnetic) => 'm',
            (2, DiscType.Exploding) => 'e',
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

// 2D board storing discs.
public class Grid
{
    public int Columns { get; set; }
    public int Rows { get; set; }
    private readonly Disc?[,] discs;

    public Grid(int columns, int rows)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns), columns, "Number of columns must be > 0.");
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), rows, "Number of rows must be > 0.");
        this.Columns = columns;
        this.Rows = rows;
        this.discs = new Disc[columns, rows];
    }

    private bool IsValidColumn(int column) => 0 <= column && column < Columns;

    public bool IsColumnFull(int column)
    {
        if (!IsValidColumn(column)) throw new ArgumentOutOfRangeException(nameof(column), column, "Column is not valid.");
        return discs[column, 0] is not null;
    }

    public MoveResult PlaceDisc(Player player, Disc disc, int column)
    {
        if (IsColumnFull(column)) throw new InvalidOperationException($"Column {column} is full.");

        Disc? changeTo = disc;

        for (int row = this.Rows - 1; row >= 0; row--)
        {
            if (this.discs[column, row] == null)
            {
                Disc? changeFrom = this.discs[column, row];
                this.discs[column, row] = changeTo;
                return new MoveResult(column, row, changeFrom, changeTo);
            }
        }

        throw new InvalidOperationException();
    }

    public MoveResults BoreDisc(MoveResult moveResult)
    {
        var list = new List<MoveResult>();

        if (moveResult.ChangeTo is not Disc boreDisc) return new MoveResults(new List<MoveResult>());

        if (moveResult.Row == this.Rows - 1 || (moveResult.Row < this.Rows - 1 && this.discs[moveResult.Column, moveResult.Row + 1] is Disc { DiscType: DiscType.Boring })) return new MoveResults(new List<MoveResult>());

        int column = moveResult.Column;
        for (int row = moveResult.Row + 1; row <= this.Rows - 1; row++)
        {
            if (this.discs[column, row] is Disc { DiscType: not DiscType.Boring })
            {
                Disc? changeFromFirst = this.discs[column, row - 1];
                Disc? changeToFirst = null;
                Disc? changeFromSecond = this.discs[column, row];
                Disc? changeToSecond = boreDisc;

                this.discs[column, row - 1] = changeToFirst;
                this.discs[column, row] = changeToSecond;
                list.Add(new MoveResult(column, row - 1, changeFromFirst, changeToFirst));
                list.Add(new MoveResult(column, row, changeFromSecond, changeToSecond));
            }
        }

        MoveResults moveResults = new MoveResults(list);
        return moveResults;
    }

    public MoveResults MagnetDisc(MoveResult moveResult)
    {
        var list = new List<MoveResult>();

        if (moveResult.ChangeTo is not Disc magnetDisc) return new MoveResults(new List<MoveResult>());

        if (moveResult.Row > this.Rows - 3) return new MoveResults(new List<MoveResult>());

        bool found = false;
        int targetRow = 0;
        int column = moveResult.Column;
        for (int row = moveResult.Row + 2; row <= this.Rows - 1; row++)
        {
            if (this.discs[column, row] is Disc { DiscType: not DiscType.Boring } disc && disc.PlayerNumber == magnetDisc.PlayerNumber)
            {
                targetRow = row;
                found = true;
                break;
            }
        }

        if (found)
        {
            for (int row = targetRow; row >= moveResult.Row + 2; row--)
            {
                Disc? changeFromFirst = this.discs[column, row];
                Disc? changeToFirst = this.discs[column, row - 1];
                Disc? changeFromSecond = this.discs[column, row - 1];
                Disc? changeToSecond = this.discs[column, row];

                this.discs[column, row] = changeToFirst;
                this.discs[column, row - 1] = changeToSecond;
                list.Add(new MoveResult(column, row, changeFromFirst, changeToFirst));
                list.Add(new MoveResult(column, row - 1, changeFromSecond, changeToSecond));
            }
        }
        else
        {
            return new MoveResults(new List<MoveResult>());
        }

        MoveResults moveResults = new MoveResults(list);
        return moveResults;
    }

    public MoveResults ExplodeDisc(MoveResult moveResult)
    {
        return new MoveResults(new List<MoveResult>());
    }

    public Grid Clone()
    {
        Grid grid = new Grid(this.Columns, this.Rows);
        for (int column = 0; column < Columns; column++)
            for (int row = 0; row < Rows; row++)
                grid.discs[column, row] = this.discs[column, row];
        return grid;
    }

    public bool TryDrop(Player player, Disc disc, int column)
    {
        try
        {
            MoveResult moveResult = this.PlaceDisc(player, disc, column);

            if (disc.DiscType == DiscType.Boring)
            {
                this.BoreDisc(moveResult);
            }
            else if (disc.DiscType == DiscType.Magnetic)
            {
                this.MagnetDisc(moveResult);
            }
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public int CountMaxDiscsInLine(Player player)
    {
        int max = 0;

        void ScanRay(int column, int row, int dc, int dr)
        {
            int streak = 0;
            while (0 <= column && column < this.Columns && 0 <= row && row < this.Rows)
            {
                if (this.discs[column, row] is Disc disc && disc.PlayerNumber == player.Number)
                    streak++;
                else
                {
                    if (streak > max) max = streak;
                    streak = 0;
                }
                column += dc; row += dr;
            }
            if (streak > max) max = streak;
        }

        for (int column = 0; column < this.Columns; column++) ScanRay(column, 0, 0, 1);
        for (int row = 0; row < this.Rows; row++) ScanRay(0, row, 1, 0);
        for (int column = 0; column < this.Columns; column++) ScanRay(column, 0, 1, 1);
        for (int row = 1; row < this.Rows; row++) ScanRay(0, row, 1, 1);
        for (int column = 0; column < this.Columns; column++) ScanRay(column, this.Rows - 1, 1, -1);
        for (int row = this.Rows - 2; row >= 0; row--) ScanRay(0, row, 1, -1);

        return max;
    }

    public List<(DiscType discType, int column)> FindBestMoves(Player player, IEnumerable<DiscType> discTypes, IEnumerable<int> columns)
    {
        var result = new List<(DiscType discType, int column)>();
        int bestScore = int.MinValue;

        foreach (var discType in discTypes)
        {
            foreach (int column in columns)
            {
                if (this.IsColumnFull(column)) continue;

                Grid gridClone = this.Clone();
                Disc disc = new Disc(player.Number, discType);
                if (!gridClone.TryDrop(player, disc, column))
                    continue;

                int score = gridClone.CountMaxDiscsInLine(player);

                if (score > bestScore)
                {
                    bestScore = score;
                    result.Clear();
                    result.Add((discType, column));
                }
                else if (score == bestScore)
                {
                    result.Add((discType, column));
                }
            }
        }

        return result;
    }

    public void Rotate()
    {
    }

    public Disc? GetDiscAt(int column, int row) => discs[column, row];

    internal void SetDiscAt(int column, int row, Disc? disc) => discs[column, row] = disc;
}

// Console renderer for the grid, prompts, and status messages.
public class Ui
{
    private readonly int columns;
    private readonly int rows;
    private readonly int originLeft;
    private readonly int originTop;
    private readonly int cellWidth;
    private int gridWidth => columns * (cellWidth + 1) + 1;
    private int gridHeight => rows;
    private int commandLeft => originLeft + gridWidth + 2;
    private int commandTop => originTop;
    private int msgLeft => originLeft;
    private int msgTop => originTop + gridHeight + 1;
    private int statusLeft => originLeft;
    private int statusTop => originTop + gridHeight + 2;

    public Ui(int columns, int rows, int originLeft = 0, int originTop = 0, int cellWidth = 3)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns), columns, "columns must be > 0.");
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), rows, "rows must be > 0.");
        if (cellWidth <= 0) throw new ArgumentOutOfRangeException(nameof(cellWidth), cellWidth, "cellWidth must be > 0.");

        this.columns = columns;
        this.rows = rows;
        this.originLeft = originLeft;
        this.originTop = originTop;
        this.cellWidth = cellWidth;
    }

    public void DrawGrid(Grid grid)
    {
        Console.Clear();
        for (int row = 0; row < grid.Rows; row++)
        {
            Console.SetCursorPosition(originLeft, originTop + row);
            Console.Write("|");
            for (int column = 0; column < grid.Columns; column++)
            {
                Console.Write(new string(' ', cellWidth) + "|");
            }
        }
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                Console.SetCursorPosition(originLeft + column * (this.cellWidth + 1) + 2, originTop + row);
                if (grid.GetDiscAt(column, row) is Disc disc)
                {
                    Console.Write(disc.Character);
                }
                else
                {
                    Console.Write(' ');
                }
            }
        }
    }

    public void RenderResult(MoveResult moveResult)
    {
        Console.SetCursorPosition(originLeft + moveResult.Column * (this.cellWidth + 1) + 2, originTop + moveResult.Row);
        if (moveResult.ChangeTo is Disc disc)
        {
            Console.Write(disc.Character);
        }
        else
        {
            Console.Write(' ');
        }
        Thread.Sleep(500);
    }

    public void RenderResults(MoveResults moveResults)
    {
        for (int i = 0; i < moveResults.Items.Count; i++)
        {
            this.RenderResult(moveResults.Items[i]);
        }
    }

    public void ShowCommand()
    {
        Console.SetCursorPosition(commandLeft, commandTop);
        Console.Write("[S]ave [H]elp [Q]uit");
    }

    public void ShowMsg()
    {
        Console.SetCursorPosition(msgLeft, msgTop);
        Console.Write(new string(' ', Console.WindowWidth - msgLeft));
        Console.SetCursorPosition(statusLeft, statusTop);
        Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - statusLeft)));

        Console.SetCursorPosition(msgLeft, msgTop);
        Console.Write("Select: ");
    }

    public void ShowStatus(string message)
    {
        Console.SetCursorPosition(statusLeft, statusTop);
        Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - statusLeft)));

        Console.SetCursorPosition(statusLeft, statusTop);
        Console.Write(message);
    }
}

// Top-level game loop.
public class Game
{
    private GameState? gameState;

    public Game()
    {
    }

    public void Initiate()
    {
        GameCategory gameCategory = GameCategory.Classic;
        IGamePolicy gamePolicy = GamePolicyFactory.Create(gameCategory);
        PlayMode playMode = gamePolicy.ConfigurePlayMode(new ConsolePlayModeConfigurator());
        GridSize gridSize = gamePolicy.ConfigureGridSize(new ConsoleGridSizeConfigurator());
        gameState = new GameState(gameCategory, gamePolicy, playMode, gridSize);
    }

    public void Load()
    {
        try
        {
            string jsonStringRead = File.ReadAllText("GameState.json");
            var snapshot = JsonSerializer.Deserialize<GameStateSnapshot>(jsonStringRead);
            if (snapshot == null)
            {
                Console.WriteLine("Error: Could not deserialize GameState.");
                return;
            }
            gameState = GameStateMapper.FromSnapshot(snapshot);
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("Error: The file 'GameState.json' was not found.");
            return;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON parse error: {ex.Message}");
            return;
        }
        catch (NotSupportedException ex)
        {
            Console.WriteLine($"Serialization type error: {ex.Message}");
            return;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"An I/O error occurred: {ex.Message}");
            return;
        }
    }

    public void Play()
    {
        if (gameState != null)
        {
            gameState.Ui.DrawGrid(gameState.Grid);
            gameState.Ui.ShowCommand();
            string? endMessage = null;
            while (!this.CheckGameOver(gameState.Grid, out endMessage))
            {
                Player currentPlayer = gameState.Players[gameState.Turn];

                if (currentPlayer is Human human)
                {
                    gameState.Ui.ShowMsg();
                    string? line = Console.ReadLine();

                    if (!human.TryParseCommand(gameState.GamePolicy, line, gameState.Grid.Columns, out Command command))
                    {
                        gameState.Ui.ShowStatus("Invalid input. Press any key to continue...");
                        Console.ReadKey(true);
                        continue;
                    }
                    switch (command.CommandKind)
                    {
                        case CommandKind.Move:
                            if (command.DiscType is DiscType discType && command.Column is int column)
                            {
                                if (this.TryExecuteMove(human, discType, column))
                                {
                                    human.ConsumeDiscInventory(discType);
                                    gameState.Turn = 1 - gameState.Turn;
                                    if (gameState.Turn == 0) gameState.Round++;
                                }
                                else
                                {
                                    gameState.Ui.ShowStatus("Invalid move. Column is full or out of range. Press any key to continue...");
                                    Console.ReadKey(true);
                                }
                            }
                            break;
                        case CommandKind.Save:
                            this.Save(gameState);
                            break;
                        case CommandKind.Help:
                            this.Help();
                            break;
                        case CommandKind.Quit:
                            return;
                    }
                }
                else if (currentPlayer is Computer computer)
                {
                    Command command = computer.DecideCommand(gameState.Grid);
                    if (command.DiscType is DiscType discType && command.Column is int column)
                    {
                        if (this.TryExecuteMove(computer, discType, column))
                        {
                            computer.ConsumeDiscInventory(discType);
                            gameState.Turn = 1 - gameState.Turn;
                            if (gameState.Turn == 0) gameState.Round++;
                        }
                        else
                        {
                            gameState.Ui.ShowStatus("Computer attempted an invalid move. Press any key to continue...");
                            Console.ReadKey(true);
                        }
                    }
                }

                if (this.CheckRotate(gameState.GamePolicy, gameState.Round))
                {
                    gameState.Grid.Rotate();
                    gameState.Ui.DrawGrid(gameState.Grid);
                    gameState.Ui.ShowCommand();
                }
            }
            gameState.Ui.ShowStatus(endMessage + " Press any key to exit...");
            Console.ReadKey(true);
        }
    }

    public void Test()
    {
        GameCategory gameCategory = new TestGameCategoryConfigurator().Get();
        IGamePolicy gamePolicy = GamePolicyFactory.Create(gameCategory);
        PlayMode playMode = new TestPlayModeConfigurator().Get();
        GridSize gridSize = new TestGridSizeConfigurator().Get();
        gameState = new GameState(gameCategory, gamePolicy, playMode, gridSize);

        Console.Clear();
        Console.Write("Input: ");
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) return;
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

        gameState.Ui.DrawGrid(gameState.Grid);
        string? endMessage = null;
        foreach (var part in parts)
        {
            Player currentPlayer = gameState.Players[gameState.Turn];
            if (currentPlayer is Human human)
            {
                if (!human.TryParseCommand(gameState.GamePolicy, part, gameState.Grid.Columns, out Command command))
                {
                    endMessage = "Invalid command.";
                    break;
                }
                if (command.DiscType is not DiscType discType || command.Column is not int column)
                {
                    endMessage = "Invalid command.";
                    break;
                }
                if (!this.TryExecuteMove(human, discType, column))
                {
                    endMessage = "Invalid command.";
                    break;
                }
                human.ConsumeDiscInventory(discType);
                gameState.Turn = 1 - gameState.Turn;
                if (gameState.Turn == 0) gameState.Round++;
                if (this.CheckRotate(gameState.GamePolicy, gameState.Round)) gameState.Grid.Rotate();
                if (this.CheckGameOver(gameState.Grid, out endMessage)) break;
            }
        }
        if (endMessage is "") endMessage = "No winners.";

        gameState.Ui.ShowStatus(endMessage + " Press any key to exit...");
        Console.ReadKey(true);
    }

    private bool CheckGameOver(Grid grid, out string message)
    {
        if (CheckWinner(grid, out message)) return true;
        if (CheckFinish(grid, out message)) return true;
        return false;
    }

    private bool CheckWinner(Grid grid, out string message)
    {
        message = string.Empty;
        if (grid is null || gameState is null) return false;

        int maxPlayer1 = grid.CountMaxDiscsInLine(gameState.Players[0]);
        int maxPlayer2 = grid.CountMaxDiscsInLine(gameState.Players[1]);

        if (maxPlayer1 >= gameState.WinCondition && maxPlayer2 >= gameState.WinCondition)
        {
            message = "Draw!";
            return true;
        }
        else if (maxPlayer1 >= gameState.WinCondition)
        {
            message = "Player1 win!";
            return true;
        }
        else if (maxPlayer2 >= gameState.WinCondition)
        {
            message = "Player2 win!";
            return true;
        }

        return false;
    }

    private bool CheckFinish(Grid grid, out string message)
    {
        message = string.Empty;
        if (gameState == null) return false;

        foreach (Player player in gameState.Players)
        {
            if (player.GetTotalDiscInventory() == 0)
            {
                message = "Draw!";
                return true;
            }
        }
        return false;
    }

    private bool CheckRotate(IGamePolicy policy, int round)
    {
        if (round <= 0) return false;
        if (policy.RotateEveryNRounds is not int r || r <= 0) return false;
        return (round % r) == 0;
    }

    private bool TryExecuteMove(Player player, DiscType discType, int column)
    {
        if (gameState != null)
        {
            try
            {
                Disc disc = new Disc(player.Number, discType);
                MoveResult moveResult = gameState.Grid.PlaceDisc(player, disc, column);
                gameState.Ui.RenderResult(moveResult);

                if (discType == DiscType.Boring)
                {
                    MoveResults moveResults = gameState.Grid.BoreDisc(moveResult);
                    gameState.Ui.RenderResults(moveResults);
                    this.ReturnDiscs(moveResults);
                }
                else if (discType == DiscType.Magnetic)
                {
                    MoveResults moveResults = gameState.Grid.MagnetDisc(moveResult);
                    gameState.Ui.RenderResults(moveResults);
                }
                else if (discType == DiscType.Exploding)
                {
                    MoveResults moveResults = gameState.Grid.ExplodeDisc(moveResult);
                    gameState.Ui.RenderResults(moveResults);
                }

                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        return false;
    }

    private void ReturnDiscs(MoveResults moveResults)
    {
        if (gameState != null)
        {
            for (int i = 0; i < moveResults.Items.Count; i++)
            {
                MoveResult moveResult = moveResults.Items[i];
                if (moveResult.ChangeTo is Disc { DiscType: DiscType.Boring } && moveResult.ChangeFrom is Disc { DiscType: not DiscType.Boring })
                {
                    if (Array.Find(gameState.Players, player => player.Number == 1) is Player player1 && moveResult.ChangeFrom is Disc disc1 && disc1.PlayerNumber == 1)
                    {
                        player1.AddDiscInventory(DiscType.Ordinary);
                    }
                    else if (Array.Find(gameState.Players, player => player.Number == 2) is Player player2 && moveResult.ChangeFrom is Disc disc2 && disc2.PlayerNumber == 2)
                    {
                        player2.AddDiscInventory(DiscType.Ordinary);
                    }
                }
            }
        }
    }

    private void Save(GameState gameState)
    {
        try
        {
            GameStateSnapshot snapshot = GameStateMapper.ToSnapshot(gameState);
            string jsonString = JsonSerializer.Serialize(snapshot);
            File.WriteAllText("GameState.json", jsonString);
        }
        catch (FileNotFoundException)
        {
            gameState.Ui.ShowStatus("Error: The file 'GameState.json' was not found. Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        catch (JsonException ex)
        {
            gameState.Ui.ShowStatus($"JSON parse error: {ex.Message}. Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        catch (NotSupportedException ex)
        {
            gameState.Ui.ShowStatus($"Serialization type error: {ex.Message}. Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        catch (IOException ex)
        {
            gameState.Ui.ShowStatus($"An I/O error occurred: {ex.Message}. Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
    }

    private void Help()
    {
        if (gameState != null)
        {
            if (gameState.GameCategory == GameCategory.Classic)
                gameState.Ui.ShowStatus($"Available Command: [Disc Type: O, B, M][Column Number: 1-{gameState.Grid.Columns}]. Press any key to continue...");
            else if (gameState.GameCategory == GameCategory.Basic)
                gameState.Ui.ShowStatus($"Available Command: [Disc Type: O][Column Number: 1-{gameState.Grid.Columns}]. Press any key to continue...");
            else if (gameState.GameCategory == GameCategory.Spin)
                gameState.Ui.ShowStatus($"Available Command: [Disc Type: O][Column Number: 1-{gameState.Grid.Columns}]. Press any key to continue...");
            Console.ReadKey(true);
        }
    }
}

// Application entry point.
public static class Program
{
    public static void Main(string[] args)
    {
        while (true)
        {
            try
            {
                ConsoleMainMenuConfigurator consoleMainMenuConfigurator = new ConsoleMainMenuConfigurator();
                MainMenu mainMenu = consoleMainMenuConfigurator.Get();

                switch (mainMenu)
                {
                    case MainMenu.New:
                        {
                            Game game = new Game();
                            game.Initiate();
                            game.Play();
                            break;
                        }
                    case MainMenu.Load:
                        {
                            Game game = new Game();
                            game.Load();
                            game.Play();
                            break;
                        }
                    case MainMenu.Test:
                        {
                            Game game = new Game();
                            game.Test();
                            break;
                        }
                    case MainMenu.Quit:
                        return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] {e.Message}. Press any key to continue...");
                Console.ReadKey(true);
            }
        }
    }
}