using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.OrganizationFixtures
{
    public class OrganizationCustomization : ICustomization
    {
        public bool UseGroups { get; set; }

        public void Customize(IFixture fixture)
        {
            var organizationId = Guid.NewGuid();
            var maxConnections = (short)new Random().Next(10, short.MaxValue);

            fixture.Customize<Organization>(composer => composer
                .With(o => o.Id, organizationId)
                .With(o => o.MaxCollections, maxConnections)
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
            var validUpgradePlans = StaticStore.Plans.Where(p => p.Type != PlanType.Free && !p.Disabled).Select(p => p.Type).ToList();
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
            var organizationId = new Guid();
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

    internal class EfOrganization : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new OrganizationBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        }
    }

    internal class PaidOrganizationAutoDataAttribute : CustomAutoDataAttribute
    {
        public PaidOrganizationAutoDataAttribute(PlanType planType) : base(new SutProviderCustomization(),
            new PaidOrganization { CheckedPlanType = planType })
        { }
        public PaidOrganizationAutoDataAttribute(int planType = 0) : this((PlanType)planType) { }
    }

    internal class InlinePaidOrganizationAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlinePaidOrganizationAutoDataAttribute(PlanType planType, object[] values) : base(
            new ICustomization[] { new SutProviderCustomization(), new PaidOrganization { CheckedPlanType = planType } }, values)
        { }

        public InlinePaidOrganizationAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(PaidOrganization) }, values)
        { }
    }

    internal class InlineFreeOrganizationAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineFreeOrganizationAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(FreeOrganization) }, values)
        { }
    }

    internal class FreeOrganizationUpgradeAutoDataAttribute : CustomAutoDataAttribute
    {
        public FreeOrganizationUpgradeAutoDataAttribute() : base(new SutProviderCustomization(), new FreeOrganizationUpgrade())
        { }
    }

    internal class InlineFreeOrganizationUpgradeAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineFreeOrganizationUpgradeAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(FreeOrganizationUpgrade) }, values)
        { }
    }

    internal class OrganizationInviteAutoDataAttribute : CustomAutoDataAttribute
    {
        public OrganizationInviteAutoDataAttribute(int inviteeUserType = 0, int invitorUserType = 0, string permissionsBlob = null) : base(new SutProviderCustomization(),
            new OrganizationInvite
            {
                InviteeUserType = (OrganizationUserType)inviteeUserType,
                InvitorUserType = (OrganizationUserType)invitorUserType,
                PermissionsBlob = permissionsBlob,
            })
        { }
    }

    internal class InlineOrganizationInviteAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineOrganizationInviteAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(OrganizationInvite) }, values)
        { }
    }

    internal class EfOrganizationAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfOrganizationAutoDataAttribute() : base(new SutProviderCustomization(), new EfOrganization())
        { }
    }

    internal class InlineEfOrganizationAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfOrganizationAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfOrganization) }, values)
        { }
    }
}
