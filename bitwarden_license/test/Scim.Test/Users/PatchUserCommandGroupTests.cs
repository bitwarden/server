using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Users;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Users;

[SutProviderCustomize]
public class PatchUserCommandGroupTests
{
    // RFC 7643 section 4.1.2: User.groups is readOnly, so membership may only change via the Group resource.
    [Theory]
    [BitAutoData("add")]
    [BitAutoData("remove")]
    [BitAutoData("replace")]
    public async Task PatchUser_GroupsPath_IsRejectedAsReadOnly(
        string op,
        SutProvider<PatchUserCommand> sutProvider,
        OrganizationUser organizationUser,
        Guid groupId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var scimPatchModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>
            {
                new()
                {
                    Op = op,
                    Path = "groups",
                    Value = JsonDocument.Parse($"[{{\"value\":\"{groupId}\"}}]").RootElement
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PatchUserAsync(organizationUser.OrganizationId, organizationUser.Id, scimPatchModel));

        Assert.Contains("read-only", exception.Message);
    }
}
