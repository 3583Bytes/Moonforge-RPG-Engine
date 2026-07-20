using Moonforge.Sample.Roguelike.GameLoop;

if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.WriteLine("Moonforge.Sample.Roguelike.Console requires an interactive terminal.");
    return;
}

RoguelikeGame game = new();
game.Run();
