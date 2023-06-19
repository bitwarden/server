using Bit.Core.AdminConsole.OrganizationAuth.Interfaces;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.OpenApi.Extensions;

namespace Bit.Core.AdminConsole.OrganizationAuth;

public class UpdateOrganizationAuthRequestCommand : IUpdateOrganizationAuthRequestCommand
{
    private readonly IAuthRequestService _authRequestService;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;

    public UpdateOrganizationAuthRequestCommand(IAuthRequestService authRequestService, IMailService mailService, IUserRepository userRepository)
    {
        _authRequestService = authRequestService;
        _mailService = mailService;
        _userRepository = userRepository;
    }

    public async Task UpdateAsync(Guid requestId, Guid userId, bool requestApproved, string encryptedUserKey)
    {
        var updatedAuthRequest = await _authRequestService.UpdateAuthRequestAsync(requestId, userId,
            new AuthRequestUpdateRequestModel { RequestApproved = requestApproved, Key = encryptedUserKey });

        if (updatedAuthRequest.Approved is true)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (string.IsNullOrEmpty(user.Email))
            {
                throw new BadRequestException("User Email is Required");
            }
            var approvalDateTime = updatedAuthRequest.ResponseDate ?? DateTime.UtcNow;
            string[] identifiers =
            {
                updatedAuthRequest.RequestDeviceType.GetDisplayName(), updatedAuthRequest.RequestDeviceIdentifier
            };
            var deviceTypeIdentifier = string.Join(" ", identifiers);
            await _mailService.SendTrustedDeviceAdminApprovalEmailAsync(user.Email, approvalDateTime,
                updatedAuthRequest.RequestIpAddress, deviceTypeIdentifier);
        }
    }
}
