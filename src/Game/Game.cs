namespace LineUp;
using System.Text.Json;

// Top-level game loop.
public class Game
{
    private GameState? gameState;

    public Game()
    {
    }

    public void Initiate()
    {
        GameCategory gameCategory = new ConsoleGameCategoryConfigurator().Get();
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
        catch (JsonException e)
        {
            Console.WriteLine($"JSON parse error: {e.Message}");
            return;
        }
        catch (NotSupportedException e)
        {
            Console.WriteLine($"Serialization type error: {e.Message}");
            return;
        }
        catch (IOException e)
        {
            Console.WriteLine($"An I/O error occurred: {e.Message}");
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
        PlayMode playMode = gamePolicy.ConfigurePlayMode(new TestPlayModeConfigurator());
        GridSize gridSize = gamePolicy.ConfigureGridSize(new TestGridSizeConfigurator());
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
                    endMessage = "Invalid move.";
                    break;
                }
                if (command.DiscType is not DiscType discType || command.Column is not int column)
                {
                    endMessage = "Invalid move.";
                    break;
                }
                if (!this.TryExecuteMove(human, discType, column))
                {
                    endMessage = "Invalid move.";
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
        catch (JsonException e)
        {
            gameState.Ui.ShowStatus($"JSON parse error: {e.Message}. Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        catch (NotSupportedException e)
        {
            gameState.Ui.ShowStatus($"Serialization type error: {e.Message}. Press any key to continue...");
            Console.ReadKey(true);
            return;
        }
        catch (IOException e)
        {
            gameState.Ui.ShowStatus($"An I/O error occurred: {e.Message}. Press any key to continue...");
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