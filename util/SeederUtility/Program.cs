using Bit.SeederUtility.Commands;
using CommandDotNet;

namespace Bit.SeederUtility;

public class Program
{
    private static int Main(string[] args)
    {
        PrintBanner();

        return new AppRunner<Program>()
            .Run(args);
    }

    private static void PrintBanner()
    {
        var brightGreen = "\x1b[92m";
        var green = "\x1b[32m";
        var brown = "\x1b[38;2;128;60;30m";
        var cyan = "\x1b[36m";
        var bold = "\x1b[1m";
        var reset = "\x1b[0m";

        // Art sections: leaves (bright green), stem (green), seed (brown)
        (string Color, string Art)[] sections =
        [
            (brightGreen, """
                                            ...........
                                         ..::------==:.
                                       ..-----=====+-.
                                     ..----======+=-.
                                     .--==+=======-.
                                    .-===========...
                .------:::...      ..=+=======-...
                .-=====-----:.    .:=:.........
                 .-+=======----...:=..
                  .-=======+==--..=:....
                   ..===========:=-.....
                     .:========+=+:..
                      ....:----:-=:..
                """),
            (green, """
                         ..:=:.
                           .+:
                           .=-..
                           .-=.....
                           .:=:....
                           ..=:.
                             --.
                """),
            (brown, """
                     ...-======:..:-...=#####*-..
                  ..:====--=======-=+##*******###-.
                  .==------=======**************###:
                 .==----=======+***************#####.
                .:+========++*****************######-
                ..##************************########.
                  :*###*****************###########:.
                  ..-#########***################=..
                     .:*######################*-..
                        ..-=+*###########*=-.....
                           .................
                """),
        ];

        var allLines = sections
            .SelectMany(s => s.Art.Split('\n'))
            .Where(l => !string.IsNullOrWhiteSpace(l));
        var minIndent = allLines.Min(l => l.Length - l.TrimStart().Length);

        Console.WriteLine();
        foreach (var (color, art) in sections)
        {
            foreach (var line in art.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                Console.WriteLine($"{color}{line[minIndent..]}{reset}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  {bold}{cyan}╔══════════════════════════════╗{reset}");
        Console.WriteLine($"  {bold}{cyan}║      SEEDER    UTILITY       ║{reset}");
        Console.WriteLine($"  {bold}{cyan}╚══════════════════════════════╝{reset}");
        Console.WriteLine();
    }

    [Subcommand]
    public OrganizationCommand Organization { get; set; } = null!;

    [Subcommand]
    public SeedCommand Seed { get; set; } = null!;
}
