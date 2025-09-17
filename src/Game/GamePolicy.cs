namespace LineUp;

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
