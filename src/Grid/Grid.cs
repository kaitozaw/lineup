namespace LineUp;

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
