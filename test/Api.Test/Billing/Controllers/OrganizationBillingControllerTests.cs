using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(OrganizationBillingController))]
[SutProviderCustomize]
public class OrganizationBillingControllerTests
{
    [Theory, BitAutoData]
    public async Task GetMetadataAsync_MetadataNull_NotFound(
        Guid organizationId,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        var result = await sutProvider.Sut.GetMetadataAsync(organizationId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetMetadataAsync_OK(
        Guid organizationId,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationBillingService>().GetMetadata(organizationId)
            .Returns(new OrganizationMetadataDTO(true));

        var result = await sutProvider.Sut.GetMetadataAsync(organizationId);

        Assert.IsType<Ok<OrganizationMetadataResponse>>(result);

        var organizationMetadataResponse = ((Ok<OrganizationMetadataResponse>)result).Value;

        Assert.True(organizationMetadataResponse.IsOnSecretsManagerStandalone);
    }
}
