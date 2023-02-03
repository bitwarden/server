using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.ProviderFeatures.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Commercial.Core.ProviderFeatures;

public class CreateProviderCommand : ICreateProviderCommand
{
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderService _providerService;
    private readonly IUserRepository _userRepository;

    public CreateProviderCommand(
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderService providerService,
        IUserRepository userRepository)
    {
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerService = providerService;
        _userRepository = userRepository;
    }

    public async Task CreateMspAsync(Provider provider, string ownerEmail)
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
}
