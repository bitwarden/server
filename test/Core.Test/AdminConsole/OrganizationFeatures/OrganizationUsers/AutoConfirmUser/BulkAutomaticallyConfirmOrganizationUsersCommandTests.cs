using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
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

        var requests = BuildRequests(organization.Id, performingUserId,
            (orgUser1.Id, key1),
            (orgUser2.Id, key2));

        SetupCommonMocks(sutProvider, organization, [orgUser1, orgUser2]);
        SetupValidatorAllValid(sutProvider, requests, [orgUser1, orgUser2], organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser1.Id, orgUser2.Id]);

        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync(requests);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Null(r.Error));
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

        var requests = BuildRequests(organization.Id, performingUserId,
            (orgUser1.Id, key1),
            (orgUser2.Id, key2));

        SetupCommonMocks(sutProvider, organization, [orgUser1, orgUser2]);

        // orgUser1 passes validation; orgUser2 fails
        sutProvider.GetDependency<IBulkAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateManyAsync(Arg.Any<IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest>>())
            .Returns([
                ValidationResultHelpers.Valid(BuildValidationRequest(requests[0], orgUser1, organization)),
                ValidationResultHelpers.Invalid(BuildValidationRequest(requests[1], orgUser2, organization), new UserIsNotAccepted())
            ]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser1.Id]);

        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync(requests);

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

        await sutProvider.GetDependency<IBulkAutomaticallyConfirmOrganizationUsersValidator>()
            .DidNotReceive()
            .ValidateManyAsync(Arg.Any<IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest>>());
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

        var requests = BuildRequests(organization.Id, performingUserId, (orgUser.Id, key));

        SetupCommonMocks(sutProvider, organization, [orgUser]);
        SetupValidatorAllValid(sutProvider, requests, [orgUser], organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser.Id]);

        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync(requests);

        // Assert
        Assert.Single(results);
        Assert.Equal(orgUser.Id, results[0].OrganizationUserId);
    }

    private static List<AutomaticallyConfirmOrganizationUserRequest> BuildRequests(
        Guid organizationId,
        Guid performingUserId,
        params (Guid OrgUserId, string Key)[] userKeys) =>
        userKeys.Select(u => new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = u.OrgUserId,
            OrganizationId = organizationId,
            Key = u.Key,
            DefaultUserCollectionName = string.Empty,
            PerformedBy = new StandardUser(performingUserId, false)
        }).ToList();

    private static AutomaticallyConfirmOrganizationUserValidationRequest BuildValidationRequest(
        AutomaticallyConfirmOrganizationUserRequest request,
        OrganizationUser orgUser,
        Organization organization) =>
        new()
        {
            OrganizationUserId = request.OrganizationUserId,
            OrganizationId = request.OrganizationId,
            Key = request.Key,
            DefaultUserCollectionName = request.DefaultUserCollectionName,
            PerformedBy = request.PerformedBy,
            OrganizationUser = orgUser,
            Organization = organization
        };

    private static void SetupCommonMocks(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider,
        Organization organization,
        ICollection<OrganizationUser> orgUsers)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(orgUsers);
    }

    private static void SetupValidatorAllValid(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider,
        IEnumerable<AutomaticallyConfirmOrganizationUserRequest> requests,
        ICollection<OrganizationUser> orgUsers,
        Organization organization)
    {
        var orgUserById = orgUsers.ToDictionary(ou => ou.Id);
        var validationResults = requests
            .Select(r => ValidationResultHelpers.Valid(BuildValidationRequest(r, orgUserById[r.OrganizationUserId], organization)))
            .ToList<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>>();

        sutProvider.GetDependency<IBulkAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateManyAsync(Arg.Any<IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest>>())
            .Returns(validationResults);
    }
}
