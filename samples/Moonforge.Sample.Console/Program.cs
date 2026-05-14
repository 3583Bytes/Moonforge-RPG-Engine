using Moonforge.Sample.ConsoleApp.GameLoop;

if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.WriteLine("Moonforge.Sample.Console requires an interactive terminal.");
    return;
}

RoguelikeGame game = new();
game.Run();
