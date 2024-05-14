using Bit.Api.AdminConsole.Public.Controllers;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Public.Controllers;

[ControllerCustomize(typeof(PoliciesController))]
[SutProviderCustomize]
public class PoliciesControllerTests
{
    [Theory]
    [BitAutoData]
    [BitAutoData(PolicyType.SendOptions)]
    public async Task Put_NewPolicy_AppliesCorrectType(PolicyType type, Organization organization, PolicyUpdateRequestModel model, SutProvider<PoliciesController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);
        sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(organization.Id, type).Returns((Policy)null);

        var response = await sutProvider.Sut.Put(type, model) as JsonResult;
        var responseValue = response.Value as PolicyResponseModel;

        Assert.Equal(type, responseValue.Type);
    }
}
