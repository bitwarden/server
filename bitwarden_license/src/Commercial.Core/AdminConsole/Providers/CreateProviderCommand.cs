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

namespace Bit.Commercial.Core.AdminConsole.Providers;

public class CreateProviderCommand : ICreateProviderCommand
{
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderService _providerService;
    private readonly IUserRepository _userRepository;
    private readonly IProviderPlanRepository _providerPlanRepository;

    public CreateProviderCommand(
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderService providerService,
        IUserRepository userRepository,
        IProviderPlanRepository providerPlanRepository)
    {
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerService = providerService;
        _userRepository = userRepository;
        _providerPlanRepository = providerPlanRepository;
    }

    public async Task CreateMspAsync(Provider provider, string ownerEmail, int teamsMinimumSeats, int enterpriseMinimumSeats)
    {
        var owner = await _userRepository.GetByEmailAsync(ownerEmail);
        if (owner == null)
        {
            throw new BadRequestException("Invalid owner. Owner must be an existing Bitwarden user.");
        }

        await ProviderRepositoryCreateAsync(provider, ProviderStatusType.Pending);

        var providerUser = new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = owner.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Confirmed,
        };
        var providerPlans = new List<ProviderPlan>
        {
            new ProviderPlan
            {
                ProviderId = provider.Id,
                PlanType = PlanType.TeamsMonthly,
                SeatMinimum = teamsMinimumSeats,
                PurchasedSeats = 0,
                AllocatedSeats = 0
            },
            new ProviderPlan
            {
                ProviderId = provider.Id,
                PlanType = PlanType.EnterpriseMonthly,
                SeatMinimum = enterpriseMinimumSeats,
                PurchasedSeats = 0,
                AllocatedSeats = 0
            }
        };

        await _providerUserRepository.CreateAsync(providerUser);
        foreach (var providerPlan in providerPlans)
        {
            await _providerPlanRepository.CreateAsync(providerPlan);
        }
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
}
