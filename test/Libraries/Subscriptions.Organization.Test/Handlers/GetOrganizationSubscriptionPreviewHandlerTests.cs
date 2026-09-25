using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
using Bit.Subscriptions.Organization.Handlers;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Handlers;

public class GetOrganizationSubscriptionPreviewHandlerTests
{
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IGetSubscriptionPreviewQuery _getSubscriptionPreviewQuery = Substitute.For<IGetSubscriptionPreviewQuery>();
    private readonly GetOrganizationSubscriptionPreviewHandler _sut;

    public GetOrganizationSubscriptionPreviewHandlerTests() =>
        _sut = new GetOrganizationSubscriptionPreviewHandler(_organizationRepository, _getSubscriptionPreviewQuery);

    [Fact]
    public async Task HandleAsync_WhenOrganizationMissing_ThrowsNotFound()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns((OrganizationEntity?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(organizationId));
    }

    [Fact]
    public async Task HandleAsync_WhenPreviewNull_ThrowsNotFound()
    {
        var organizationId = Guid.NewGuid();
        var organization = new OrganizationEntity { Id = organizationId };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _getSubscriptionPreviewQuery.Run(organization).Returns((SubscriptionPreview?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(organizationId));
    }

    [Fact]
    public async Task HandleAsync_ReturnsPreviewFromQuery()
    {
        var organizationId = Guid.NewGuid();
        var organization = new OrganizationEntity { Id = organizationId };
        var preview = new SubscriptionPreview
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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _getSubscriptionPreviewQuery.Run(organization).Returns(preview);

        var result = await _sut.HandleAsync(organizationId);

        Assert.Same(preview, result);
    }
}
