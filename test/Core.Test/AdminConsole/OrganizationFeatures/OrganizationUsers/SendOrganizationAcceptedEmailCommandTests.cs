using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class SendOrganizationAcceptedEmailCommandTests
{
    [Theory, BitAutoData]
    public async Task SendAsync_SendsEmailToDistinctAdminEmails(
        SutProvider<SendOrganizationAcceptedEmailCommand> sutProvider,
        Organization organization,
        string acceptedUserEmail)
    {
        // Arrange
        var admins = new List<OrganizationUserUserDetails>
        {
            new() { Email = "admin@example.com" },
            new() { Email = "admin@example.com" },
            new() { Email = "owner@example.com" },
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns(admins);

        // Act
        await sutProvider.Sut.SendAsync(organization, acceptedUserEmail);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationAcceptedEmailAsync(
                organization,
                acceptedUserEmail,
                Arg.Is<IEnumerable<string>>(emails =>
                    emails.SequenceEqual(new[] { "admin@example.com", "owner@example.com" })));
    }

    [Theory, BitAutoData]
    public async Task SendAsync_NoAdmins_DoesNotSendEmail(
        SutProvider<SendOrganizationAcceptedEmailCommand> sutProvider,
        Organization organization,
        string acceptedUserEmail)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails>());

        // Act
        await sutProvider.Sut.SendAsync(organization, acceptedUserEmail);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceiveWithAnyArgs()
            .SendOrganizationAcceptedEmailAsync(default!, default!, default!);
    }
}
