using System.Text.Json;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Test.AutoFixture.OrganizationFixtures;

public class OrganizationCustomization : ICustomization
{
    public bool UseGroups { get; set; }
    public PlanType PlanType { get; set; }

    public void Customize(IFixture fixture)
    {
        var organizationId = Guid.NewGuid();
        var maxCollections = (short)new Random().Next(10, short.MaxValue);
        var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == PlanType);
        var seats = (short)new Random().Next(plan.PasswordManager.BaseSeats, plan.PasswordManager.MaxSeats ?? short.MaxValue);
        var smSeats = plan.SupportsSecretsManager
            ? (short?)new Random().Next(plan.SecretsManager.BaseSeats, plan.SecretsManager.MaxSeats ?? short.MaxValue)
            : null;

        fixture.Customize<Organization>(composer => composer
            .With(o => o.Id, organizationId)
            .With(o => o.MaxCollections, maxCollections)
            .With(o => o.UseGroups, UseGroups)
            .With(o => o.PlanType, PlanType)
            .With(o => o.Seats, seats)
            .With(o => o.SmSeats, smSeats));

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
        var validUpgradePlans = StaticStore.Plans.Where(p => p.Type != PlanType.Free && p.LegacyYear == null).OrderBy(p => p.UpgradeSortOrder).Select(p => p.Type).ToList();
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
        var selectedPlan = StaticStore.Plans.Last(p => !plansToIgnore.Contains(p.Type) && !p.Disabled);

        fixture.Customize<OrganizationUpgrade>(composer => composer
            .With(ou => ou.Plan, selectedPlan.Type)
            .With(ou => ou.PremiumAccessAddon, selectedPlan.PasswordManager.HasPremiumAccessOption));
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
        // Set Manage to false, this ensures it doesn't conflict with the other properties during validation
        fixture.Customize<CollectionAccessSelection>(composer => composer
            .With(c => c.Manage, false));
    }
}

public class SecretsManagerOrganizationCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        const PlanType planType = PlanType.EnterpriseAnnually;
        var organizationId = Guid.NewGuid();

        fixture.Customize<Organization>(composer => composer
            .With(o => o.Id, organizationId)
            .With(o => o.UseSecretsManager, true)
            .With(o => o.PlanType, planType)
            .With(o => o.Plan, StaticStore.GetPlan(planType).Name)
            .With(o => o.MaxAutoscaleSmSeats, (int?)null)
            .With(o => o.MaxAutoscaleSmServiceAccounts, (int?)null));
    }
}

internal class TeamsStarterOrganizationCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var organizationId = Guid.NewGuid();
        const PlanType planType = PlanType.TeamsStarter;

        fixture.Customize<Organization>(composer =>
            composer
                .With(organization => organization.Id, organizationId)
                .With(organization => organization.PlanType, planType)
                .With(organization => organization.Seats, 10)
                .Without(organization => organization.MaxStorageGb));
    }
}

internal class TeamsMonthlyWithAddOnsOrganizationCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var organizationId = Guid.NewGuid();
        const PlanType planType = PlanType.TeamsMonthly;

        fixture.Customize<Organization>(composer =>
            composer
                .With(organization => organization.Id, organizationId)
                .With(organization => organization.PlanType, planType)
                .With(organization => organization.Seats, 20)
                .With(organization => organization.UseSecretsManager, true)
                .With(organization => organization.SmSeats, 5)
                .With(organization => organization.SmServiceAccounts, 53));
    }
}

public class OrganizationCustomizeAttribute : BitCustomizeAttribute
{
    public bool UseGroups { get; set; }
    public PlanType PlanType { get; set; } = PlanType.EnterpriseAnnually;
    public override ICustomization GetCustomization() => new OrganizationCustomization()
    {
        UseGroups = UseGroups,
        PlanType = PlanType
    };
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

internal class TeamsStarterOrganizationCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new TeamsStarterOrganizationCustomization();
}

internal class TeamsMonthlyWithAddOnsOrganizationCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new TeamsMonthlyWithAddOnsOrganizationCustomization();
}

internal class EphemeralDataProtectionCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new EphemeralDataProtectionProviderBuilder());
    }

    private class EphemeralDataProtectionProviderBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            var type = request as Type;
            if (type == null || type != typeof(IDataProtectionProvider))
            {
                return new NoSpecimen();
            }

            return new EphemeralDataProtectionProvider();
        }
    }
}

internal class EphemeralDataProtectionAutoDataAttribute : CustomAutoDataAttribute
{
    public EphemeralDataProtectionAutoDataAttribute() : base(new SutProviderCustomization(), new EphemeralDataProtectionCustomization())
    { }
}
