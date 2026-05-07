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

        var request = BuildRequest(organization.Id, performingUserId,
            (orgUser1.Id, key1),
            (orgUser2.Id, key2));

        SetupCommonMocks(sutProvider, organization, [orgUser1, orgUser2]);
        SetupValidatorAllValid(sutProvider, request, [orgUser1, orgUser2], organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser1.Id, orgUser2.Id]);

        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync(request);

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

        var request = BuildRequest(organization.Id, performingUserId,
            (orgUser1.Id, key1),
            (orgUser2.Id, key2));

        SetupCommonMocks(sutProvider, organization, [orgUser1, orgUser2]);

        // orgUser1 passes validation; orgUser2 fails
        sutProvider.GetDependency<IBulkAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateManyAsync(Arg.Any<IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest>>())
            .Returns([
                ValidationResultHelpers.Valid(BuildValidationRequest(request.UsersToConfirm[0], request, orgUser1, organization)),
                ValidationResultHelpers.Invalid(BuildValidationRequest(request.UsersToConfirm[1], request, orgUser2, organization), new UserIsNotAccepted())
            ]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser1.Id]);

        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync(request);

        // Assert
        Assert.Equal(2, results.Count);

        var successResult = results.Single(r => r.OrganizationUserId == orgUser1.Id);
        Assert.Null(successResult.Error);

        var errorResult = results.Single(r => r.OrganizationUserId == orgUser2.Id);
        Assert.NotNull(errorResult.Error);
    }

    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_EmptyList_ReturnsEmptyResults(
        Guid organizationId,
        Guid performingUserId,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync(
            BuildRequest(organizationId, performingUserId));

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

        var request = BuildRequest(organization.Id, performingUserId, (orgUser.Id, key));

        SetupCommonMocks(sutProvider, organization, [orgUser]);
        SetupValidatorAllValid(sutProvider, request, [orgUser], organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser.Id]);

        // Act
        var results = await sutProvider.Sut.BulkAutomaticallyConfirmOrganizationUsersAsync(request);

        // Assert
        Assert.Single(results);
        Assert.Equal(orgUser.Id, results[0].OrganizationUserId);
    }

    private static BulkAutomaticallyConfirmOrganizationUsersRequest BuildRequest(
        Guid organizationId,
        Guid performingUserId,
        params (Guid OrgUserId, string Key)[] userKeys) =>
        new()
        {
            OrganizationId = organizationId,
            DefaultUserCollectionName = string.Empty,
            PerformedBy = new StandardUser(performingUserId, false),
            UsersToConfirm = userKeys
                .Select(u => new BulkAutoConfirmUserEntry { OrganizationUserId = u.OrgUserId, Key = u.Key })
                .ToList(),
        };

    private static AutomaticallyConfirmOrganizationUserValidationRequest BuildValidationRequest(
        BulkAutoConfirmUserEntry entry,
        BulkAutomaticallyConfirmOrganizationUsersRequest request,
        OrganizationUser orgUser,
        Organization organization) =>
        new()
        {
            Key = entry.Key,
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
        BulkAutomaticallyConfirmOrganizationUsersRequest request,
        ICollection<OrganizationUser> orgUsers,
        Organization organization)
    {
        var orgUserById = orgUsers.ToDictionary(ou => ou.Id);
        var validationResults = request.UsersToConfirm
            .Select(u => ValidationResultHelpers.Valid(BuildValidationRequest(u, request, orgUserById[u.OrganizationUserId], organization)))
            .ToList<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>>();

        sutProvider.GetDependency<IBulkAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateManyAsync(Arg.Any<IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest>>())
            .Returns(validationResults);
    }
}
