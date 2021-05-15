using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AutoFixture;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Utilities;

namespace Bit.Core.Test.AutoFixture.OrganizationFixtures
{
    public class Organization : ICustomization
    {
        public bool UseGroups { get; set; }
        public Guid CollectionId { get; set; }

        public void Customize(IFixture fixture)
        {
            var organizationId = Guid.NewGuid();

            fixture.Customize<Core.Models.Table.Organization>(composer => composer
                .With(o => o.Id, organizationId)
                .With(o => o.UseGroups, UseGroups));

            fixture.Customize<Core.Models.Table.Collection>(composer =>
                composer
                    .With(c => c.Id, CollectionId)
                    .With(c => c.OrganizationId, organizationId));

            fixture.Customize<Group>(composer => composer.With(g => g.OrganizationId, organizationId));
        }
    }

    internal class PaidOrganization : ICustomization
    {
        public PlanType CheckedPlanType { get; set; }
        public void Customize(IFixture fixture)
        {
            var validUpgradePlans = StaticStore.Plans.Where(p => p.Type != Enums.PlanType.Free && !p.Disabled).Select(p => p.Type).ToList();
            var lowestActivePaidPlan = validUpgradePlans.First();
            CheckedPlanType = CheckedPlanType.Equals(Enums.PlanType.Free) ? lowestActivePaidPlan : CheckedPlanType;
            validUpgradePlans.Remove(lowestActivePaidPlan);
            fixture.Customize<Core.Models.Table.Organization>(composer => composer
                .With(o => o.PlanType, CheckedPlanType));
            fixture.Customize<OrganizationUpgrade>(composer => composer
                .With(ou => ou.Plan, validUpgradePlans.First()));
        }
    }

    internal class FreeOrganizationUpgrade : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Core.Models.Table.Organization>(composer => composer
                .With(o => o.PlanType, PlanType.Free));

            var plansToIgnore = new List<PlanType> { PlanType.Free, PlanType.Custom };
            var selectedPlan = StaticStore.Plans.Last(p => !plansToIgnore.Contains(p.Type) && !p.Disabled);

            fixture.Customize<OrganizationUpgrade>(composer => composer
                .With(ou => ou.Plan, selectedPlan.Type)
                .With(ou => ou.PremiumAccessAddon, selectedPlan.HasPremiumAccessOption));
            fixture.Customize<Core.Models.Table.Organization>(composer => composer
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
            fixture.Customize<Core.Models.Table.Organization>(composer => composer
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

    internal class PaidOrganizationAutoDataAttribute : CustomAutoDataAttribute
    {
        public PaidOrganizationAutoDataAttribute(int planType = 0) : base(new SutProviderCustomization(),
            new PaidOrganization { CheckedPlanType = (PlanType)planType })
        { }
    }

    internal class InlinePaidOrganizationAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlinePaidOrganizationAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(PaidOrganization) }, values)
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
}
