using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Quartz;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class SendOrganizationAcceptedEmailJobTests
{
    private static IJobExecutionContext CreateContext(Guid organizationUserId)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(new JobDataMap
        {
            { SendOrganizationAcceptedEmailJob.OrganizationUserIdKey, organizationUserId.ToString() }
        });
        return context;
    }

    [Theory, BitAutoData]
    public async Task Execute_UserStillAccepted_SendsAcceptedEmail(
        SutProvider<SendOrganizationAcceptedEmailJob> sutProvider,
        OrganizationUser orgUser,
        Organization organization,
        User user,
        OrganizationUserUserDetails admin)
    {
        // Arrange
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.UserId = user.Id;
        orgUser.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns([admin]);

        // Act
        await sutProvider.Sut.Execute(CreateContext(orgUser.Id));

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationAcceptedEmailAsync(
                organization,
                user.Email,
                Arg.Is<IEnumerable<string>>(emails => emails.Contains(admin.Email)));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public async Task Execute_UserNoLongerAccepted_DoesNotSendAcceptedEmail(
        OrganizationUserStatusType status,
        SutProvider<SendOrganizationAcceptedEmailJob> sutProvider,
        OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Status = status;

        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);

        // Act
        await sutProvider.Sut.Execute(CreateContext(orgUser.Id));

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceiveWithAnyArgs()
            .SendOrganizationAcceptedEmailAsync(default!, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task Execute_UserRemoved_DoesNotSendAcceptedEmail(
        SutProvider<SendOrganizationAcceptedEmailJob> sutProvider,
        Guid organizationUserId)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns((OrganizationUser?)null);

        // Act
        await sutProvider.Sut.Execute(CreateContext(organizationUserId));

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceiveWithAnyArgs()
            .SendOrganizationAcceptedEmailAsync(default!, default!, default!);
    }
}
