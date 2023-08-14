using System.Text.Json;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Braintree;

namespace Bit.Core.Test.AutoFixture.OrganizationFixtures;

public class OrganizationCustomization : ICustomization
{
    public bool UseGroups { get; set; }

    public void Customize(IFixture fixture)
    {
        var organizationId = Guid.NewGuid();
        var maxCollections = (short)new Random().Next(10, short.MaxValue);

        fixture.Customize<Organization>(composer => composer
            .With(o => o.Id, organizationId)
            .With(o => o.MaxCollections, maxCollections)
            .With(o => o.UseGroups, UseGroups));

        fixture.Customize<Collection>(composer =>
            composer
                .With(c => c.OrganizationId, organizationId)
                .Without(o => o.CreationDate)
                .Without(o => o.RevisionDate));

        fixture.Customize<Group>(composer => composer.With(g => g.OrganizationId, organizationId));
    }
}

internal class OrganizationBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(Organization))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        var providers = fixture.Create<Dictionary<TwoFactorProviderType, TwoFactorProvider>>();
        var organization = new Fixture().WithAutoNSubstitutions().Create<Organization>();
        organization.SetTwoFactorProviders(providers);
        return organization;
    }
}

internal class PaidOrganization : ICustomization
{
    public PlanType CheckedPlanType { get; set; }
    public void Customize(IFixture fixture)
    {
        var validUpgradePlans = StaticStore.PasswordManagerPlans.Where(p => p.Type != PlanType.Free && p.LegacyYear == null).OrderBy(p => p.UpgradeSortOrder).Select(p => p.Type).ToList();
        var lowestActivePaidPlan = validUpgradePlans.First();
        CheckedPlanType = CheckedPlanType.Equals(PlanType.Free) ? lowestActivePaidPlan : CheckedPlanType;
        validUpgradePlans.Remove(lowestActivePaidPlan);
        fixture.Customize<Organization>(composer => composer
            .With(o => o.PlanType, CheckedPlanType));
        fixture.Customize<OrganizationUpgrade>(composer => composer
            .With(ou => ou.Plan, validUpgradePlans.First()));
    }
}

internal class FreeOrganization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<Organization>(composer => composer
            .With(o => o.PlanType, PlanType.Free));
    }
}

internal class FreeOrganizationUpgrade : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<Organization>(composer => composer
            .With(o => o.PlanType, PlanType.Free));

        var plansToIgnore = new List<PlanType> { PlanType.Free, PlanType.Custom };
        var selectedPlan = StaticStore.PasswordManagerPlans.Last(p => !plansToIgnore.Contains(p.Type) && !p.Disabled);

        fixture.Customize<OrganizationUpgrade>(composer => composer
            .With(ou => ou.Plan, selectedPlan.Type)
            .With(ou => ou.PremiumAccessAddon, selectedPlan.HasPremiumAccessOption));
        fixture.Customize<Organization>(composer => composer
            .Without(o => o.GatewaySubscriptionId));
    }
}

internal class OrganizationInvite : ICustomization
{
    public OrganizationUserType InviteeUserType { get; set; }
    public OrganizationUserType InvitorUserType { get; set; }
    public string PermissionsBlob { get; set; }
    public void Customize(IFixture fixture)
    {
        var organizationId = Guid.NewGuid();
        PermissionsBlob = PermissionsBlob ?? JsonSerializer.Serialize(new Permissions(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        fixture.Customize<Organization>(composer => composer
            .With(o => o.Id, organizationId)
            .With(o => o.Seats, (short)100));
        fixture.Customize<OrganizationUser>(composer => composer
            .With(ou => ou.OrganizationId, organizationId)
            .With(ou => ou.Type, InvitorUserType)
            .With(ou => ou.Permissions, PermissionsBlob));
        fixture.Customize<OrganizationUserInvite>(composer => composer
            .With(oi => oi.Type, InviteeUserType));
    }
}

public class SecretsManagerOrganizationCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var organizationId = Guid.NewGuid();
        var planType = PlanType.EnterpriseAnnually;

        fixture.Customize<Organization>(composer => composer
            .With(o => o.Id, organizationId)
            .With(o => o.UseSecretsManager, true)
            .With(o => o.PlanType, planType)
            .With(o => o.Plan, StaticStore.GetPasswordManagerPlan(planType).Name)
            .With(o => o.MaxAutoscaleSmSeats, (int?)null)
            .With(o => o.MaxAutoscaleSmServiceAccounts, (int?)null)
        );
    }
}

internal class OrganizationCustomizeAttribute : BitCustomizeAttribute
{
    public bool UseGroups { get; set; }
    public override ICustomization GetCustomization() => new OrganizationCustomization() { UseGroups = UseGroups };
}

internal class PaidOrganizationCustomizeAttribute : BitCustomizeAttribute
{
    public PlanType CheckedPlanType { get; set; } = PlanType.FamiliesAnnually;
    public override ICustomization GetCustomization() => new PaidOrganization() { CheckedPlanType = CheckedPlanType };
}

internal class FreeOrganizationCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new FreeOrganization();
}

internal class FreeOrganizationUpgradeCustomize : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new FreeOrganizationUpgrade();
}

internal class OrganizationInviteCustomizeAttribute : BitCustomizeAttribute
{
    public OrganizationUserType InviteeUserType { get; set; } = OrganizationUserType.Owner;
    public OrganizationUserType InvitorUserType { get; set; } = OrganizationUserType.Owner;
    public string PermissionsBlob { get; set; }

    public override ICustomization GetCustomization() => new OrganizationInvite
    {
        InviteeUserType = InviteeUserType,
        InvitorUserType = InvitorUserType,
        PermissionsBlob = PermissionsBlob,
    };
}

internal class SecretsManagerOrganizationCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() =>
        new SecretsManagerOrganizationCustomization();
}
