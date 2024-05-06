using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Org.BouncyCastle.Crypto.Engines;

namespace Bit.Commercial.Core.AdminConsole.Providers;

public class CreateProviderCommand : ICreateProviderCommand
{
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderService _providerService;
    private readonly IUserRepository _userRepository;
    private readonly IProviderPlanRepository _providerPlanRepository;
    private readonly IFeatureService _featureService;

    public CreateProviderCommand(
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderService providerService,
        IUserRepository userRepository,
        IProviderPlanRepository providerPlanRepository,
        IFeatureService featureService)
    {
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerService = providerService;
        _userRepository = userRepository;
        _providerPlanRepository = providerPlanRepository;
        _featureService = featureService;
    }

    public async Task CreateMspAsync(Provider provider, string ownerEmail, int teamsMinimumSeats, int enterpriseMinimumSeats)
    {
        var owner = await _userRepository.GetByEmailAsync(ownerEmail);
        if (owner == null)
        {
            throw new BadRequestException("Invalid owner. Owner must be an existing Bitwarden user.");
        }

        var isConsolidatedBillingEnabled = _featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling);

        if (isConsolidatedBillingEnabled)
        {
            provider.Gateway = GatewayType.Stripe;
        }

        await ProviderRepositoryCreateAsync(provider, ProviderStatusType.Pending);

        var providerUser = new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = owner.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Confirmed,
        };

        if (isConsolidatedBillingEnabled)
        {
            var providerPlans = new List<ProviderPlan>
            {
                CreateProviderPlan(provider.Id, PlanType.TeamsMonthly, teamsMinimumSeats),
                CreateProviderPlan(provider.Id, PlanType.EnterpriseMonthly, enterpriseMinimumSeats)
            };

            foreach (var providerPlan in providerPlans)
            {
                await _providerPlanRepository.CreateAsync(providerPlan);
            }
        }

        await _providerUserRepository.CreateAsync(providerUser);
        await _providerService.SendProviderSetupInviteEmailAsync(provider, owner.Email);
    }

    public async Task CreateResellerAsync(Provider provider)
    {
        await ProviderRepositoryCreateAsync(provider, ProviderStatusType.Created);
    }

    private async Task ProviderRepositoryCreateAsync(Provider provider, ProviderStatusType status)
    {
        provider.Status = status;
        provider.Enabled = true;
        provider.UseEvents = true;
        await _providerRepository.CreateAsync(provider);
    }

    private ProviderPlan CreateProviderPlan(Guid providerId, PlanType planType, int seatMinimum)
    {
        return new ProviderPlan
        {
            ProviderId = providerId,
            PlanType = planType,
            SeatMinimum = seatMinimum,
            PurchasedSeats = 0,
            AllocatedSeats = 0
        };
    }
}
