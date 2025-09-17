namespace LineUp;

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