namespace LineUp;

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