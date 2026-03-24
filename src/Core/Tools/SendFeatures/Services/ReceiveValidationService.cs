using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public class ReceiveValidationService : IReceiveValidationService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IPricingClient _pricingClient;


    public ReceiveValidationService(IUserRepository userRepository, IUserService userService,
        GlobalSettings globalSettings, IPricingClient pricingClient)
    {
        _userRepository = userRepository;
        _userService = userService;
        _pricingClient = pricingClient;
    }


    public void ValidateUpload(Receive receive)
    {
        // future policy validations can be added here, make async and see SendValidationService

        if (!receive.UserId.HasValue)
        {
            throw new BadRequestException("Invalid Receive owner");
        }
    }

    public async Task<long> StorageRemainingForReceiveAsync(Receive receive)
    {
        var storageBytesRemaining = 0L;

        var user = await _userRepository.GetByIdAsync(receive.UserId!.Value) ??
                   throw new BadRequestException("Invalid Receive Owner");

        if (!await _userService.CanAccessPremium(user))
        {
            throw new BadRequestException("The creator of this Receive does not have a Premium Subscription.");
        }

        if (!user.EmailVerified)
        {
            throw new BadRequestException("The creator of this Receive has not verified their email.");
        }

        if (user.Premium)
        {
            storageBytesRemaining = user.StorageBytesRemaining();
        }
        else
        {
            // Users that get access to file storage/premium from their organization get storage
            // based on the current premium plan from the pricing service
            var premiumPlan = await _pricingClient.GetAvailablePremiumPlan();
            var provided = (short)premiumPlan.Storage.Provided;

            storageBytesRemaining = user.StorageBytesRemaining(provided);
        }

        return storageBytesRemaining;
    }
}
