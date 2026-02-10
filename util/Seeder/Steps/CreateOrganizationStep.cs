using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates an organization from a fixture or explicit parameters.
/// </summary>
internal sealed class CreateOrganizationStep : IStep
{
    private readonly string? _fixtureName;
    private readonly string? _name;
    private readonly string? _domain;
    private readonly int _seats;

    private CreateOrganizationStep(string? fixtureName, string? name, string? domain, int seats)
    {
        if (fixtureName is null && (name is null || domain is null))
        {
            throw new ArgumentException(
                "Either fixtureName OR (name AND domain) must be provided.");
        }

        _fixtureName = fixtureName;
        _name = name;
        _domain = domain;
        _seats = seats;
    }

    internal static CreateOrganizationStep FromFixture(string fixtureName) =>
        new(fixtureName, null, null, 0);

    internal static CreateOrganizationStep FromParams(string name, string domain, int seats) =>
        new(null, name, domain, seats);

    public void Execute(SeederContext context)
    {
        string name, domain;
        int seats;

        if (_fixtureName is not null)
        {
            var fixture = context.SeedReader.Read<SeedOrganization>($"organizations.{_fixtureName}");
            name = fixture.Name;
            domain = fixture.Domain;
            seats = fixture.Seats;
        }
        else
        {
            name = _name!;
            domain = _domain!;
            seats = _seats;
        }

        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var organization = OrganizationSeeder.Create(name, domain, seats, orgKeys.PublicKey, orgKeys.PrivateKey);

        context.Organization = organization;
        context.OrgKeys = orgKeys;
        context.Domain = domain;

        context.Organizations.Add(organization);
    }
}
