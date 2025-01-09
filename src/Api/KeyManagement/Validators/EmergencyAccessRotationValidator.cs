using Bit.Api.Auth.Models.Request;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Api.KeyManagement.Validators;

public class EmergencyAccessRotationValidator : IRotationValidator<IEnumerable<EmergencyAccessWithIdRequestModel>,
    IEnumerable<EmergencyAccess>>
{
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IUserService _userService;

    public EmergencyAccessRotationValidator(IEmergencyAccessRepository emergencyAccessRepository,
        IUserService userService)
    {
        _emergencyAccessRepository = emergencyAccessRepository;
        _userService = userService;
    }

    public async Task<IEnumerable<EmergencyAccess>> ValidateAsync(User user,
        IEnumerable<EmergencyAccessWithIdRequestModel> emergencyAccessKeys)
    {
        var result = new List<EmergencyAccess>();

        var existing = await _emergencyAccessRepository.GetManyDetailsByGrantorIdAsync(user.Id);
        if (existing == null || existing.Count == 0)
        {
            return result;
        }
        // Exclude any emergency access that has not been confirmed yet.
        existing = existing.Where(ea => ea.KeyEncrypted != null).ToList();

        foreach (var ea in existing)
        {
            var emergencyAccess = emergencyAccessKeys.FirstOrDefault(c => c.Id == ea.Id);
            if (emergencyAccess == null)
            {
                throw new BadRequestException("All existing emergency access keys must be included in the rotation.");
            }

            if (emergencyAccess.KeyEncrypted == null)
            {
                throw new BadRequestException("Emergency access keys cannot be set to null during rotation.");
            }

            result.Add(emergencyAccess.ToEmergencyAccess(ea));
        }

        return result;
    }
}
