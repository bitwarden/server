using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;

[SutProviderCustomize]
public class SendOrganizationConfirmationCommandTests
{
    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationAsync_EnterpriseOrg_CallsUpdatedConfirmedEmail(
        Organization organization,
        string userEmail,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;

        // Act
        await sutProvider.Sut.SendConfirmationAsync(organization, userEmail, true);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendUpdatedOrganizationConfirmedEmailAsync(organization, userEmail, true);
    }

    [Theory]
    [OrganizationCustomize, BitAutoData]
    public async Task SendConfirmationAsync_FamiliesOrg_CallsUpdatedConfirmedEmail(
        Organization organization,
        string userEmail,
        SutProvider<SendOrganizationConfirmationCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.FamiliesAnnually;

        // Act
        await sutProvider.Sut.SendConfirmationAsync(organization, userEmail, false);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendUpdatedOrganizationConfirmedEmailAsync(organization, userEmail, false);
    }
}
