using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Seeder.Options;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct OrganizationStripeBillingSceneResult
{
    public Guid OrganizationId { get; init; }
    public string? GatewayCustomerId { get; init; }
    public string? GatewaySubscriptionId { get; init; }
}

/// <summary>
/// Creates a Stripe customer and subscription in the shared test account for an existing organization, the
/// same way the CLI's <c>--stripe-billing</c> does.
/// </summary>
/// <remarks>
/// Refuses an organization that already has a gateway ID, so it bills once. <c>OrganizationBillingService</c>
/// reuses a non-empty <c>GatewayCustomerId</c> instead of creating a customer.
/// </remarks>
public class OrganizationStripeBillingScene(
    IOrganizationRepository organizationRepository,
    IOrganizationStripeBillingService billingService)
    : IScene<OrganizationStripeBillingScene.Request, OrganizationStripeBillingSceneResult>
{
    private const int MinTrialDays = 1;
    private const int MaxTrialDays = 30;
    private const int DefaultTrialDays = 30;

    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        /// <summary>
        /// Starts the subscription <c>active</c> and charges the test card immediately.
        /// </summary>
        public bool SkipTrial { get; set; }
        /// <summary>
        /// Trial length, 1–30 days. Null uses 30. Not valid with <see cref="SkipTrial"/>.
        /// </summary>
        public int? TrialDays { get; set; }
        /// <summary>
        /// Test card catalog name, such as <c>mastercard</c>. Null uses <c>visa</c>.
        /// </summary>
        public string? Card { get; set; }
        /// <summary>
        /// Test address catalog name, such as <c>de-vat</c>. Null uses <c>us</c>.
        /// </summary>
        public string? Address { get; set; }
    }

    public async Task<SceneResult<OrganizationStripeBillingSceneResult>> SeedAsync(Request request)
    {
        if (request.SkipTrial && request.TrialDays.HasValue)
        {
            throw new ArgumentException(
                "SkipTrial and TrialDays are mutually exclusive: one asks for no trial, the other for a trial.");
        }

        if (request.TrialDays is < MinTrialDays or > MaxTrialDays)
        {
            throw new ArgumentException($"TrialDays must be between {MinTrialDays} and {MaxTrialDays}.");
        }

        var options = new StripeBillingOptions
        {
            SkipTrial = request.SkipTrial,
            TrialDays = request.TrialDays ?? DefaultTrialDays,
            Profile = StripeBillingProfile.Parse(request.Card, request.Address),
        };

        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization with ID {request.OrganizationId} not found.");
        }

        if (organization.Gateway.HasValue ||
            !string.IsNullOrEmpty(organization.GatewayCustomerId) ||
            !string.IsNullOrEmpty(organization.GatewaySubscriptionId))
        {
            throw new InvalidOperationException(
                $"Organization {request.OrganizationId} is already linked to a billing gateway, and this scene " +
                "bills once. Seed a fresh organization, or link your own gateway IDs through SingleOrganizationScene.");
        }

        await billingService.CreateSubscriptionAsync(organization, options);

        return new SceneResult<OrganizationStripeBillingSceneResult>(
            result: new OrganizationStripeBillingSceneResult
            {
                OrganizationId = organization.Id,
                GatewayCustomerId = organization.GatewayCustomerId,
                GatewaySubscriptionId = organization.GatewaySubscriptionId,
            },
            mangleMap: new());
    }
}
