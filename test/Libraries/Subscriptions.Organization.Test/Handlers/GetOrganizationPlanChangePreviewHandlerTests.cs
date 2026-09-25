using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Handlers;

public class GetOrganizationPlanChangePreviewHandlerTests
{
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IGetOrganizationPlanChangePreviewQuery _buildPlanChangePreview =
        Substitute.For<IGetOrganizationPlanChangePreviewQuery>();
    private readonly GetOrganizationPlanChangePreviewHandler _sut;

    public GetOrganizationPlanChangePreviewHandlerTests() =>
        _sut = new GetOrganizationPlanChangePreviewHandler(_organizationRepository, _buildPlanChangePreview);

    [Fact]
    public async Task HandleAsync_WhenOrganizationMissing_ThrowsNotFound()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns((OrganizationEntity?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(organizationId, Request()));
    }

    [Fact]
    public async Task HandleAsync_ReturnsPreviewFromCommand()
    {
        var organizationId = Guid.NewGuid();
        var organization = new OrganizationEntity { Id = organizationId };
        var preview = new InvoicePreview
        {
            PasswordManager = new PasswordManagerInvoiceItems
            {
                Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 10m }
            },
            Cadence = PlanCadenceType.Annually,
            PlanTier = PlanTierType.Enterprise,
            EstimatedTax = 0m,
            Total = 10m,
            AmountDue = 10m
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _buildPlanChangePreview.Run(organization, Arg.Any<OrganizationPlanChange>()).Returns(preview);

        var result = await _sut.HandleAsync(organizationId, Request());

        Assert.Same(preview, result);
    }

    private static GetOrganizationPlanChangePreviewRequest Request() => new()
    {
        Tier = new EnumMemberParameter<PlanTierType>(PlanTierType.Enterprise),
        Cadence = new EnumMemberParameter<PlanCadenceType>(PlanCadenceType.Annually),
        Country = "US",
        PostalCode = "90210"
    };
}
