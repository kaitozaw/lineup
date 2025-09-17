namespace LineUp;

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