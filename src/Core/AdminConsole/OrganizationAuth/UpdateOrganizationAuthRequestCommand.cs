using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.AdminConsole.OrganizationAuth.Interfaces;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Services;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationAuth;

public class UpdateOrganizationAuthRequestCommand : IUpdateOrganizationAuthRequestCommand
{
    private readonly IAuthRequestService _authRequestService;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UpdateOrganizationAuthRequestCommand> _logger;

    public UpdateOrganizationAuthRequestCommand(
        IAuthRequestService authRequestService,
        IMailService mailService,
        IUserRepository userRepository,
        ILogger<UpdateOrganizationAuthRequestCommand> logger)
    {
        _authRequestService = authRequestService;
        _mailService = mailService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task UpdateAsync(Guid requestId, Guid userId, bool requestApproved, string encryptedUserKey)
    {
        var updatedAuthRequest = await _authRequestService.UpdateAuthRequestAsync(requestId, userId,
            new AuthRequestUpdateRequestModel { RequestApproved = requestApproved, Key = encryptedUserKey });

        if (updatedAuthRequest.Approved is true)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogError("User ({id}) not found. Trusted device admin approval email not sent.", userId);
                return;
            }
            var approvalDateTime = updatedAuthRequest.ResponseDate ?? DateTime.UtcNow;
            var deviceTypeDisplayName = updatedAuthRequest.RequestDeviceType.GetType()
                .GetMember(updatedAuthRequest.RequestDeviceType.ToString())
                .FirstOrDefault()?
                .GetCustomAttribute<DisplayAttribute>()?.Name ?? "Unknown";
            var deviceTypeAndIdentifier = $"{deviceTypeDisplayName} - {updatedAuthRequest.RequestDeviceIdentifier}";
            await _mailService.SendTrustedDeviceAdminApprovalEmailAsync(user.Email, approvalDateTime,
                updatedAuthRequest.RequestIpAddress, deviceTypeAndIdentifier);
        }
    }
}

