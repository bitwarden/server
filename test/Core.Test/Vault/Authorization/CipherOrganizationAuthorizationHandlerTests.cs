using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Vault.Authorization.Ciphers;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Authorization;

[SutProviderCustomize]
public class CipherOrganizationAuthorizationHandlerTests
{
    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.Owner), BitAutoData]
    public async Task MissingUserId_Failure(
        CurrentContextOrganization organization,
        SutProvider<CipherOrganizationAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(null as Guid?);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = ReadAnyAsAdminContext(organization.Id);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task ReadAnyAsAdmin_OwnerOrAdmin_Success(
        OrganizationUserType userType,
        CurrentContextOrganization organization,
        SutProvider<CipherOrganizationAuthorizationHandler> sutProvider)
    {
        organization.Type = userType;
        ArrangeMember(sutProvider, organization);

        var context = ReadAnyAsAdminContext(organization.Id);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.Custom), BitAutoData]
    public async Task ReadAnyAsAdmin_CustomWithEditAnyCollection_Success(
        CurrentContextOrganization organization,
        SutProvider<CipherOrganizationAuthorizationHandler> sutProvider)
    {
        organization.Permissions = new Core.Models.Data.Permissions { EditAnyCollection = true };
        ArrangeMember(sutProvider, organization);

        var context = ReadAnyAsAdminContext(organization.Id);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.Custom)]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, false, true)]
    public async Task ReadAnyAsAdmin_CustomWithoutEditAnyCollection_Failure(
        bool deleteAnyCollection,
        bool accessImportExport,
        bool accessReports,
        CurrentContextOrganization organization,
        SutProvider<CipherOrganizationAuthorizationHandler> sutProvider)
    {
        organization.Permissions = new Core.Models.Data.Permissions
        {
            DeleteAnyCollection = deleteAnyCollection,
            AccessImportExport = accessImportExport,
            AccessReports = accessReports,
        };
        ArrangeMember(sutProvider, organization);

        var context = ReadAnyAsAdminContext(organization.Id);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task ReadAnyAsAdmin_ProviderUser_Success(
        Guid userId,
        Guid organizationId,
        SutProvider<CipherOrganizationAuthorizationHandler> sutProvider)
    {
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.GetOrganization(organizationId).Returns((CurrentContextOrganization)null);
        currentContext.ProviderUserForOrgAsync(organizationId).Returns(true);

        var context = ReadAnyAsAdminContext(organizationId);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task ReadAnyAsAdmin_NotMemberOrProvider_Failure(
        Guid userId,
        Guid organizationId,
        SutProvider<CipherOrganizationAuthorizationHandler> sutProvider)
    {
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.GetOrganization(organizationId).Returns((CurrentContextOrganization)null);
        currentContext.ProviderUserForOrgAsync(organizationId).Returns(false);

        var context = ReadAnyAsAdminContext(organizationId);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static void ArrangeMember(
        SutProvider<CipherOrganizationAuthorizationHandler> sutProvider, CurrentContextOrganization organization)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
    }

    private static AuthorizationHandlerContext ReadAnyAsAdminContext(Guid organizationId) =>
        new(
            [CipherOrganizationOperations.ReadAnyAsAdmin],
            new ClaimsPrincipal(),
            new OrganizationScope(organizationId));
}
