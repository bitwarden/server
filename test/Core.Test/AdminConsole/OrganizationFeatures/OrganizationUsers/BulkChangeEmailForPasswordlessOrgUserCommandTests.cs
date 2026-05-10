using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class BulkChangeEmailForPasswordlessOrgUserCommandTests
{
    [Theory, BitAutoData]
    public async Task BulkChangeOrganizationUserEmailAsync_AllSucceed_ReturnsEmptyErrors(
        Guid organizationId,
        OrganizationUser orgUser1,
        OrganizationUser orgUser2,
        SutProvider<BulkChangeEmailForPasswordlessOrgUserCommand> sutProvider)
    {
        orgUser1.OrganizationId = organizationId;
        orgUser2.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser1.Id).Returns(orgUser1);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser2.Id).Returns(orgUser2);

        var requests = new[]
        {
            (orgUser1.Id, "user1@example.com"),
            (orgUser2.Id, "user2@example.com"),
        };

        var results = (await sutProvider.Sut.BulkChangeOrganizationUserEmailAsync(organizationId, requests)).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(string.Empty, r.ErrorMessage));
        await sutProvider.GetDependency<IChangeEmailForPasswordlessOrgUserCommand>()
            .Received(2)
            .ChangeOrganizationUserEmailAsync(organizationId, Arg.Any<OrganizationUser>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task BulkChangeOrganizationUserEmailAsync_PartialFailure_ReturnsPerItemErrors(
        Guid organizationId,
        OrganizationUser orgUser1,
        OrganizationUser orgUser2,
        SutProvider<BulkChangeEmailForPasswordlessOrgUserCommand> sutProvider)
    {
        orgUser1.OrganizationId = organizationId;
        orgUser2.OrganizationId = organizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser1.Id).Returns(orgUser1);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser2.Id).Returns(orgUser2);

        sutProvider.GetDependency<IChangeEmailForPasswordlessOrgUserCommand>()
            .ChangeOrganizationUserEmailAsync(organizationId, orgUser1, Arg.Any<string>())
            .ThrowsAsync(new BadRequestException("User has a master password."));

        var requests = new[]
        {
            (orgUser1.Id, "user1@example.com"),
            (orgUser2.Id, "user2@example.com"),
        };

        var results = (await sutProvider.Sut.BulkChangeOrganizationUserEmailAsync(organizationId, requests)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("User has a master password.", results[0].ErrorMessage);
        Assert.Equal(string.Empty, results[1].ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task BulkChangeOrganizationUserEmailAsync_OrgUserNotFound_ReturnsError(
        Guid organizationId,
        Guid unknownOrgUserId,
        SutProvider<BulkChangeEmailForPasswordlessOrgUserCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(unknownOrgUserId).Returns((OrganizationUser)null);

        var requests = new[] { (unknownOrgUserId, "user@example.com") };

        var results = (await sutProvider.Sut.BulkChangeOrganizationUserEmailAsync(organizationId, requests)).ToList();

        Assert.Single(results);
        Assert.NotEmpty(results[0].ErrorMessage);
        await sutProvider.GetDependency<IChangeEmailForPasswordlessOrgUserCommand>()
            .DidNotReceive()
            .ChangeOrganizationUserEmailAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUser>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task BulkChangeOrganizationUserEmailAsync_OrgUserBelongsToDifferentOrg_ReturnsError(
        Guid organizationId,
        OrganizationUser orgUserFromOtherOrg,
        SutProvider<BulkChangeEmailForPasswordlessOrgUserCommand> sutProvider)
    {
        // OrganizationId on the fetched user does not match the requested org.
        orgUserFromOtherOrg.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUserFromOtherOrg.Id).Returns(orgUserFromOtherOrg);

        var requests = new[] { (orgUserFromOtherOrg.Id, "user@example.com") };

        var results = (await sutProvider.Sut.BulkChangeOrganizationUserEmailAsync(organizationId, requests)).ToList();

        Assert.Single(results);
        Assert.NotEmpty(results[0].ErrorMessage);
        await sutProvider.GetDependency<IChangeEmailForPasswordlessOrgUserCommand>()
            .DidNotReceive()
            .ChangeOrganizationUserEmailAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUser>(), Arg.Any<string>());
    }
}
