namespace LineUp;

public enum MainMenu { New, Load, Test, Quit }

public enum GameCategory { Classic, Basic, Spin }

public enum PlayMode { HumanVsHuman, HumanVsComputer };

public readonly record struct GridSize(int Columns, int Rows);

public enum CommandKind { Move, Save, Help, Quit };

public enum DiscType { Ordinary, Boring, Magnetic, Exploding };

public readonly record struct Command(CommandKind CommandKind, DiscType? DiscType = null, int? Column = null);

public readonly record struct MoveResult(int Column, int Row, Disc? ChangeFrom, Disc? ChangeTo);

public readonly record struct MoveResults(IReadOnlyList<MoveResult> Items);