using Bit.Commercial.Core.AdminConsole.Providers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Commercial.Core.Test.AdminConsole.ProviderFeatures;

[SutProviderCustomize]
public class RemoveOrganizationFromProviderCommandTests
{
    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_NoProviderOrganization_BadRequest(
        Guid providerId,
        Guid providerOrganizationId,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId).ReturnsNull();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(providerId, providerOrganizationId));

        Assert.Equal("Failed to remove organization. Please contact support.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_MismatchedProviderOrganization_BadRequest(
        Guid providerId,
        Guid providerOrganizationId,
        ProviderOrganization providerOrganization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId).Returns(providerOrganization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(providerId, providerOrganizationId));

        Assert.Equal("Failed to remove organization. Please contact support.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_NoConfirmedOwners_BadRequest(
        Guid providerId,
        Guid providerOrganizationId,
        ProviderOrganization providerOrganization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        providerOrganization.ProviderId = providerId;

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId).Returns(providerOrganization);

        sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(
            providerOrganization.OrganizationId,
            Array.Empty<Guid>(),
            includeProvider: false)
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(providerId, providerOrganizationId));

        Assert.Equal("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_MakesCorrectInvocations(
        Guid providerId,
        Guid providerOrganizationId,
        ProviderOrganization providerOrganization,
        Organization organization,
        Provider provider,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        providerOrganization.ProviderId = providerId;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId).Returns(providerOrganization);

        sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                Array.Empty<Guid>(),
                includeProvider: false)
            .Returns(true);

        organizationRepository.GetByIdAsync(providerOrganization.OrganizationId).Returns(organization);

        var organizationOwnerEmails = new List<string> { "a@gmail.com", "b@gmail.com" };

        organizationRepository.GetOwnerEmailAddressesById(organization.Id).Returns(organizationOwnerEmails);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).Returns(provider);

        await sutProvider.Sut.RemoveOrganizationFromProvider(providerId, providerOrganizationId);

        await sutProvider.GetDependency<IPaymentService>().Received(1).RemovePaymentMethod(organization);

        await organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(
            org => org.Id == organization.Id && org.BillingEmail == "a@gmail.com"));

        await sutProvider.GetDependency<IMailService>().Received(1).SendProviderUpdatePaymentMethod(
            organization.Id,
            organization.Name,
            provider.Name,
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("a@gmail.com") && emails.Contains("b@gmail.com")));

        await sutProvider.GetDependency<IProviderOrganizationRepository>().Received(1)
            .DeleteAsync(providerOrganization);

        await sutProvider.GetDependency<IEventService>().Received(1).LogProviderOrganizationEventAsync(
            providerOrganization,
            EventType.ProviderOrganization_Removed);
    }
}
