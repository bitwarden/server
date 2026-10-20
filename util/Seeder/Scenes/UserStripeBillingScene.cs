using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct UserStripeBillingSceneResult
{
    public Guid UserId { get; init; }
    public string? GatewayCustomerId { get; init; }
    public string? GatewaySubscriptionId { get; init; }
    public DateTime? PremiumExpirationDate { get; init; }
}

/// <summary>
/// Creates a Stripe customer and premium subscription in the shared test account for an existing user, the
/// same way the CLI's <c>individual --subscription premium --stripe-billing</c> does.
/// </summary>
/// <remarks>
/// Refuses a user that already has a gateway ID, so it bills once. Accepts a user seeded as premium without
/// gateway IDs; the billing service overwrites its premium fields with the values from Stripe.
/// </remarks>
public class UserStripeBillingScene(
    IUserRepository userRepository,
    IUserStripeBillingService billingService)
    : IScene<UserStripeBillingScene.Request, UserStripeBillingSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        /// <summary>
        /// Test card catalog name, such as <c>mastercard</c>. Null uses <c>visa</c>.
        /// </summary>
        public string? Card { get; set; }
        /// <summary>
        /// Test address catalog name, such as <c>de</c>. Null uses <c>us</c>. An address with a tax ID is refused.
        /// </summary>
        public string? Address { get; set; }
    }

    public async Task<SceneResult<UserStripeBillingSceneResult>> SeedAsync(Request request)
    {
        var profile = StripeBillingProfile.Parse(request.Card, request.Address);
        if (profile.Address.TaxId is not null)
        {
            var withoutTaxId = Enum.GetValues<StripeTestAddress>().Where(a => a.TaxId is null).Select(a => a.Name);
            throw new InvalidOperationException(
                $"The '{profile.Address.Name}' test address carries a tax ID, and a premium user's personal account " +
                $"takes none. Use one of: {string.Join(", ", withoutTaxId)}.");
        }

        var user = await userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {request.UserId} not found.");
        }

        if (user.Gateway.HasValue ||
            !string.IsNullOrEmpty(user.GatewayCustomerId) ||
            !string.IsNullOrEmpty(user.GatewaySubscriptionId))
        {
            throw new InvalidOperationException(
                $"User {request.UserId} is already linked to a billing gateway, and this scene bills once. " +
                "Seed a fresh user, or link your own gateway IDs through SingleUserScene.");
        }

        await billingService.CreateSubscriptionAsync(user, UserSubscriptionType.Premium, profile);

        // The two stamps UserService.SaveUserAsync applies, so clients re-sync the premium change.
        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        await userRepository.ReplaceAsync(user);

        return new SceneResult<UserStripeBillingSceneResult>(
            result: new UserStripeBillingSceneResult
            {
                UserId = user.Id,
                GatewayCustomerId = user.GatewayCustomerId,
                GatewaySubscriptionId = user.GatewaySubscriptionId,
                PremiumExpirationDate = user.PremiumExpirationDate,
            },
            mangleMap: new());
    }
}
