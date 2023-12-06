using Bit.Commercial.Core.AdminConsole.Providers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.AdminConsole.ProviderFeatures;

[SutProviderCustomize]
public class RemoveOrganizationFromProviderCommandTests
{
    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_NoProvider_BadRequest(
        ProviderOrganization providerOrganization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(null, providerOrganization));

        Assert.Equal("Failed to remove organization. Please contact support.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_NoProviderOrganization_BadRequest(
        Provider provider,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(provider, null));

        Assert.Equal("Failed to remove organization. Please contact support.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_MismatchedProviderOrganization_BadRequest(
        Provider provider,
        ProviderOrganization providerOrganization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization));

        Assert.Equal("Failed to remove organization. Please contact support.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_NoConfirmedOwners_BadRequest(
        Provider provider,
        ProviderOrganization providerOrganization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        providerOrganization.ProviderId = provider.Id;

        sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(
            providerOrganization.OrganizationId,
            Array.Empty<Guid>(),
            includeProvider: false)
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization));

        Assert.Equal("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_MakesCorrectInvocations(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        providerOrganization.ProviderId = provider.Id;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                Array.Empty<Guid>(),
                includeProvider: false)
            .Returns(true);

        organizationRepository.GetByIdAsync(providerOrganization.OrganizationId).Returns(organization);

        var organizationOwnerEmails = new List<string> { "a@gmail.com", "b@gmail.com" };

        organizationRepository.GetOwnerEmailAddressesById(organization.Id).Returns(organizationOwnerEmails);

        await sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization);

        await sutProvider.GetDependency<IRemovePaymentMethodCommand>().Received(1).RemovePaymentMethod(organization);

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
