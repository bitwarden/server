using Xunit;
using Bit.Test.Common.AutoFixture.Attributes;
using System.Threading.Tasks;
using System;
using Bit.Core.Enums;
using System.Linq;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using Bit.Test.Common.AutoFixture;
using Bit.Api.Controllers;
using Bit.Core.Context;
using NSubstitute;
using Bit.Core.Exceptions;
using Bit.Api.Test.AutoFixture.Attributes;

namespace Bit.Api.Test.Controllers
{
    [ControllerCustomize(typeof(OrganizationSponsorshipsController))]
    [SutProviderCustomize]
    public class OrganizationSponsorshipsControllerTests
    {
        public static IEnumerable<object[]> EnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => PlanTypeHelper.IsEnterprise(p)).Select(p => new object[] { p });
        public static IEnumerable<object[]> NonEnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => !PlanTypeHelper.IsEnterprise(p)).Select(p => new object[] { p });

        [Theory]
        [MemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task CreateSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan, Organization org,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            org.PlanType = sponsoringOrgPlan;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorship(org.Id.ToString(), null));

            Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
        }
    }
}
