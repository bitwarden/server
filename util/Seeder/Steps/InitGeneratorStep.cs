using Bit.Seeder.Data;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Initializes the deterministic random data engine on <see cref="SeederContext.Generator"/>.
/// </summary>
/// <remarks>
/// Produces no entities itself. Derives a repeatable seed from the domain string (same domain
/// always yields the same generated data). Downstream steps like <see cref="GenerateCiphersStep"/>
/// consume the generator for realistic usernames, cards, identities, and notes.
/// </remarks>
/// <seealso cref="GeneratorContext"/>
internal sealed class InitGeneratorStep : IStep
{
    private readonly OrganizationVaultOptions _options;

    private InitGeneratorStep(OrganizationVaultOptions options)
    {
        _options = options;
    }

    internal static InitGeneratorStep FromDomain(string domain, int? seed = null)
    {
        var options = new OrganizationVaultOptions
        {
            Name = domain,
            Domain = domain,
            Users = 0,
            Seed = seed
        };
        return new InitGeneratorStep(options);
    }

    public void Execute(SeederContext context)
    {
        context.Generator = GeneratorContext.FromOptions(_options);
    }
}
