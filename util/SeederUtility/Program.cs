using Bit.SeederUtility.Commands;
using CommandDotNet;

namespace Bit.SeederUtility;

public class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>()
            .Run(args);
    }

    [Subcommand]
    public OrganizationCommand Organization { get; set; } = null!;

    [Subcommand]
    public VaultOrganizationCommand VaultOrganization { get; set; } = null!;

    [Subcommand]
    public SeedCommand Seed { get; set; } = null!;
}
