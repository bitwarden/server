using System.Data.Common;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class InitPendingOrganizationCommandTests
{
    [Theory, BitAutoData]
    public async Task InitPendingOrganizationAsync_NullOrgUser_ReturnsError(
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(request.OrganizationUserId)
            .Returns((OrganizationUser?)null);

        var result = await sutProvider.Sut.InitPendingOrganizationAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserNotFoundError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationAsync_NullOrg_ReturnsError(
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(request.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns((Organization?)null);

        var result = await sutProvider.Sut.InitPendingOrganizationAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotFoundError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationAsync_ValidationFails_ReturnsError(
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(request.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateAsync(Arg.Any<InitPendingOrganizationValidationRequest>())
            .Returns(callInfo =>
            {
                var req = callInfo.Arg<InitPendingOrganizationValidationRequest>();
                return new ValidationResult<InitPendingOrganizationValidationRequest>(req, new InvalidTokenError());
            });

        var result = await sutProvider.Sut.InitPendingOrganizationAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<InvalidTokenError>(result.AsError);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .InitializeOrganizationAsync(Arg.Any<Organization>(), Arg.Any<Func<DbConnection, DbTransaction, Task>>());
    }

    [Theory, BitAutoData]
    public async Task InitPendingOrganizationAsync_Success(
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        var requestWithCollection = request with { CollectionName = "My Collection" };
        SetupSuccessfulValidation(org, orgUser, requestWithCollection, sutProvider);

        var result = await sutProvider.Sut.InitPendingOrganizationAsync(requestWithCollection);

        Assert.False(result.IsError);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .InitializeOrganizationAsync(
                Arg.Is<Organization>(o =>
                    o.Enabled == true &&
                    o.Status == OrganizationStatusType.Created &&
                    o.PublicKey == requestWithCollection.OrganizationKeys.PublicKey &&
                    o.PrivateKey == requestWithCollection.OrganizationKeys.WrappedPrivateKey),
                Arg.Any<Func<DbConnection, DbTransaction, Task>>());

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .BuildConfirmOwnerAction(
                Arg.Is<OrganizationUser>(ou =>
                    ou.Status == OrganizationUserStatusType.Confirmed &&
                    ou.UserId == requestWithCollection.User.Id &&
                    ou.Key == requestWithCollection.EncryptedOrganizationSymmetricKey &&
                    ou.Email == null));

        await sutProvider.GetDependency<ICollectionRepository>().Received(1)
            .CreateAsync(
                Arg.Is<Collection>(c => c.Name == "My Collection" && c.OrganizationId == requestWithCollection.OrganizationId),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(l => l == null),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(l => l.Any(i => i.Manage)));
    }

    private static void SetupSuccessfulValidation(
        Organization org,
        OrganizationUser orgUser,
        InitPendingOrganizationRequest request,
        SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(request.OrganizationUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(request.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IInitPendingOrganizationValidator>()
            .ValidateAsync(Arg.Any<InitPendingOrganizationValidationRequest>())
            .Returns(callInfo =>
            {
                var req = callInfo.Arg<InitPendingOrganizationValidationRequest>();
                return new ValidationResult<InitPendingOrganizationValidationRequest>(req, new OneOf.Types.None());
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .BuildConfirmOwnerAction(Arg.Any<OrganizationUser>())
            .Returns((_, __) => Task.CompletedTask);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(request.User.Id)
            .Returns(new List<Device>());
    }
}
