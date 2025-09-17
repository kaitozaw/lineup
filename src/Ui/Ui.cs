namespace LineUp;

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
