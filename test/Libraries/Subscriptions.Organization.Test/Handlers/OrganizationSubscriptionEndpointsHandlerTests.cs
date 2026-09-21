using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
using Bit.Subscriptions.Organization.Handlers;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test;

public class OrganizationSubscriptionEndpointsHandlerTests
{
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IGetSubscriptionPreviewQuery _getSubscriptionPreviewQuery = Substitute.For<IGetSubscriptionPreviewQuery>();
    private readonly OrganizationSubscriptionEndpointsHandler _sut;

    public OrganizationSubscriptionEndpointsHandlerTests() =>
        _sut = new OrganizationSubscriptionEndpointsHandler(_organizationRepository, _getSubscriptionPreviewQuery);

    [Fact]
    public async Task GetPreview_WhenOrganizationMissing_ThrowsNotFound()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns((OrganizationEntity?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetPreviewAsync(organizationId));
    }

    [Fact]
    public async Task GetPreview_WhenPreviewNull_ThrowsNotFound()
    {
        var organizationId = Guid.NewGuid();
        var organization = new OrganizationEntity { Id = organizationId };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _getSubscriptionPreviewQuery.Run(organization).Returns((SubscriptionPreview?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetPreviewAsync(organizationId));
    }

    [Fact]
    public async Task GetPreview_ReturnsPreviewFromQuery()
    {
        var organizationId = Guid.NewGuid();
        var organization = new OrganizationEntity { Id = organizationId };
        var preview = SamplePreview();
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _getSubscriptionPreviewQuery.Run(organization).Returns(preview);

        var result = await _sut.GetPreviewAsync(organizationId);

        Assert.Same(preview, result);
    }

    private static SubscriptionPreview SamplePreview() => new()
    {
        Status = "active",
        InvoicePreview = new InvoicePreview
        {
            PasswordManager = new PasswordManagerInvoiceItems
            {
                Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 10m }
            },
            Cadence = PlanCadenceType.Annually,
            PlanTier = PlanTierType.Teams,
            EstimatedTax = 0m,
            Total = 10m,
            AmountDue = 10m
        }
    };
}
