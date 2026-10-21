using Bit.Api.KeyManagement.Controllers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Controllers;

[ControllerCustomize(typeof(OrganizationKeyManagementController))]
[SutProviderCustomize]
public class OrganizationKeyManagementControllerTests
{
    private const string _organizationKeyId = "0123456789abcdef0123456789abcdef";

    [Theory, BitAutoData]
    public async Task PostOrganizationKeyIdAsync_CallsCommandWithParsedKeyId(
        Guid orgId,
        SutProvider<OrganizationKeyManagementController> sutProvider)
    {
        var request = new SetOrganizationKeyIdRequestModel { OrganizationKeyId = _organizationKeyId };

        await sutProvider.Sut.PostOrganizationKeyIdAsync(orgId, request);

        await sutProvider.GetDependency<ISetOrganizationKeyIdCommand>().Received(1)
            .SetOrganizationKeyIdAsync(orgId, KeyId.FromHexEncodedString(_organizationKeyId)!);
    }
}
