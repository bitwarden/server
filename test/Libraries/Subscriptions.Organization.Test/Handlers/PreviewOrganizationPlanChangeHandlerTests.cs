using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Handlers;

public class PreviewOrganizationPlanChangeHandlerTests
{
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly FakePreviewOrganizationPlanChangeCommand _command = new();
    private readonly PreviewOrganizationPlanChangeHandler _sut;

    public PreviewOrganizationPlanChangeHandlerTests() =>
        _sut = new PreviewOrganizationPlanChangeHandler(_organizationRepository, _command);

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
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _command.Result = SamplePreview();

        var result = await _sut.HandleAsync(organizationId, Request());

        Assert.Same(_command.Result, result);
        Assert.Same(organization, _command.ReceivedOrganization);
    }

    private static PreviewOrganizationPlanChangeRequest Request() => new()
    {
        Tier = PlanTierType.Enterprise,
        Cadence = PlanCadenceType.Annually,
        BillingAddress = new BillingAddress { Country = "US", PostalCode = "90210" }
    };

    private static InvoicePreview SamplePreview() => new()
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

    private sealed class FakePreviewOrganizationPlanChangeCommand : IPreviewOrganizationPlanChangeCommand
    {
        public InvoicePreview? Result { get; set; }
        public OrganizationEntity? ReceivedOrganization { get; private set; }

        public Task<InvoicePreview> Run(OrganizationEntity organization, PreviewOrganizationPlanChangeRequest request)
        {
            ReceivedOrganization = organization;
            return Task.FromResult(Result!);
        }
    }
}
