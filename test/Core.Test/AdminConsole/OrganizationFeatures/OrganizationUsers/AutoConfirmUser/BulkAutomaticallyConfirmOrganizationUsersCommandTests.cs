using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using OneOf.Types;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

[SutProviderCustomize]
public class BulkAutomaticallyConfirmOrganizationUsersCommandTests
{
    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_AllSucceed_ReturnsNullErrorsForAll(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        Guid performingUserId,
        string key1,
        string key2,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        orgUser1.OrganizationId = organization.Id;
        orgUser2.OrganizationId = organization.Id;

        var requests = new[]
        {
            new AutomaticallyConfirmOrganizationUserRequest
            {
                OrganizationUserId = orgUser1.Id,
                OrganizationId = organization.Id,
                Key = key1,
                DefaultUserCollectionName = string.Empty,
                PerformedBy = new StandardUser(performingUserId, false)
            },
            new AutomaticallyConfirmOrganizationUserRequest
            {
                OrganizationUserId = orgUser2.Id,
                OrganizationId = organization.Id,
                Key = key2,
                DefaultUserCollectionName = string.Empty,
                PerformedBy = new StandardUser(performingUserId, false)
            }
        };

        sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUserCommand>()
            .AutomaticallyConfirmOrganizationUserAsync(Arg.Any<AutomaticallyConfirmOrganizationUserRequest>())
            .Returns(new CommandResult(new None()));

        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync(requests);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Null(r.Error));

        await sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUserCommand>()
            .Received(2)
            .AutomaticallyConfirmOrganizationUserAsync(Arg.Any<AutomaticallyConfirmOrganizationUserRequest>());
    }

    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_OneFailsOneSucceeds_ReturnsCorrectResults(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        Guid performingUserId,
        string key1,
        string key2,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        orgUser1.OrganizationId = organization.Id;
        orgUser2.OrganizationId = organization.Id;

        var request1 = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = orgUser1.Id,
            OrganizationId = organization.Id,
            Key = key1,
            DefaultUserCollectionName = string.Empty,
            PerformedBy = new StandardUser(performingUserId, false)
        };
        var request2 = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = orgUser2.Id,
            OrganizationId = organization.Id,
            Key = key2,
            DefaultUserCollectionName = string.Empty,
            PerformedBy = new StandardUser(performingUserId, false)
        };

        sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUserCommand>()
            .AutomaticallyConfirmOrganizationUserAsync(Arg.Is<AutomaticallyConfirmOrganizationUserRequest>(r =>
                r.OrganizationUserId == orgUser1.Id))
            .Returns(new CommandResult(new None()));

        sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUserCommand>()
            .AutomaticallyConfirmOrganizationUserAsync(Arg.Is<AutomaticallyConfirmOrganizationUserRequest>(r =>
                r.OrganizationUserId == orgUser2.Id))
            .Returns(new CommandResult(new UserIsNotAccepted()));

        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync([request1, request2]);

        // Assert
        Assert.Equal(2, results.Count);

        var successResult = results.Single(r => r.OrganizationUserId == orgUser1.Id);
        Assert.Null(successResult.Error);

        var errorResult = results.Single(r => r.OrganizationUserId == orgUser2.Id);
        Assert.NotNull(errorResult.Error);
    }

    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_EmptyList_ReturnsEmptyResults(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync([]);

        // Assert
        Assert.Empty(results);

        await sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUserCommand>()
            .DidNotReceive()
            .AutomaticallyConfirmOrganizationUserAsync(Arg.Any<AutomaticallyConfirmOrganizationUserRequest>());
    }

    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_PreservesOrganizationUserIdInResults(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        Guid performingUserId,
        string key,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        orgUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = string.Empty,
            PerformedBy = new StandardUser(performingUserId, false)
        };

        sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUserCommand>()
            .AutomaticallyConfirmOrganizationUserAsync(Arg.Any<AutomaticallyConfirmOrganizationUserRequest>())
            .Returns(new CommandResult(new None()));

        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync([request]);

        // Assert
        Assert.Single(results);
        Assert.Equal(orgUser.Id, results[0].OrganizationUserId);
    }
}
