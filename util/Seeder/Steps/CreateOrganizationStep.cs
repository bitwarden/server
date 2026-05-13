using Bit.Core.Billing.Enums;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Options;
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
    private readonly int? _seats;
    private readonly PlanType _planType;
    private readonly OrganizationOverrides? _overrides;

    private CreateOrganizationStep(
        string? fixtureName,
        string? name,
        string? domain,
        int? seats,
        PlanType planType,
        OrganizationOverrides? overrides)
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
        _planType = planType;
        _overrides = overrides;
    }

    internal static CreateOrganizationStep FromFixture(
        string fixtureName,
        string? planType = null,
        int? seats = null,
        OrganizationOverrides? overrides = null) =>
        new(fixtureName, null, null, seats, PlanFeatures.Parse(planType), overrides);

    internal static CreateOrganizationStep FromParams(
        string name,
        string domain,
        int? seats = null,
        PlanType planType = PlanType.EnterpriseAnnually,
        OrganizationOverrides? overrides = null) =>
        new(null, name, domain, seats, planType, overrides);

    public void Execute(SeederContext context)
    {
        string name, domain;

        if (_fixtureName is not null)
        {
            var fixture = context.GetSeedReader().Read<SeedOrganization>($"organizations.{_fixtureName}");
            name = fixture.Name;
            domain = fixture.Domain;
        }
        else
        {
            name = _name!;
            domain = _domain!;
        }

        var seats = _seats ?? PlanFeatures.GenerateRealisticSeatCount(_planType, domain);
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var organization = OrganizationSeeder.Create(name, domain, seats, context.GetMangler(), orgKeys.PublicKey, orgKeys.PrivateKey, _planType);

        PlanFeatures.ApplyOrganizationOverrides(organization, _overrides);

        context.Organization = organization;
        context.OrgKeys = orgKeys;
        context.Domain = domain;

        context.Organizations.Add(organization);
    }

}
