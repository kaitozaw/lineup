namespace LineUp;

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
