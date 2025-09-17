namespace LineUp;
using System;

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