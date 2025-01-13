using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Tools.Services;

public class SendService : ISendService
{
    public const long MAX_FILE_SIZE = Constants.FileSize501mb;
    public const string MAX_FILE_SIZE_READABLE = "500 MB";
    private readonly ISendRepository _sendRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly IPolicyService _policyService;
    private readonly IUserService _userService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IPushNotificationService _pushService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly GlobalSettings _globalSettings;
    private readonly ICurrentContext _currentContext;
    private const long _fileSizeLeeway = 1024L * 1024L; // 1MB

    public SendService(
        ISendRepository sendRepository,
        IUserRepository userRepository,
        IUserService userService,
        IOrganizationRepository organizationRepository,
        ISendFileStorageService sendFileStorageService,
        IPasswordHasher<User> passwordHasher,
        IPushNotificationService pushService,
        IReferenceEventService referenceEventService,
        GlobalSettings globalSettings,
        IPolicyRepository policyRepository,
        IPolicyService policyService,
        ICurrentContext currentContext)
    {
        _sendRepository = sendRepository;
        _userRepository = userRepository;
        _userService = userService;
        _policyRepository = policyRepository;
        _policyService = policyService;
        _organizationRepository = organizationRepository;
        _sendFileStorageService = sendFileStorageService;
        _passwordHasher = passwordHasher;
        _pushService = pushService;
        _referenceEventService = referenceEventService;
        _globalSettings = globalSettings;
        _currentContext = currentContext;
    }

    public async Task SaveSendAsync(Send send)
    {
        // Make sure user can save Sends
        await ValidateUserCanSaveAsync(send.UserId, send);

        if (send.Id == default(Guid))
        {
            await _sendRepository.CreateAsync(send);
            await _pushService.PushSyncSendCreateAsync(send);
            await RaiseReferenceEventAsync(send, ReferenceEventType.SendCreated);
        }
        else
        {
            send.RevisionDate = DateTime.UtcNow;
            await _sendRepository.UpsertAsync(send);
            await _pushService.PushSyncSendUpdateAsync(send);
        }
    }

    public async Task<string> SaveFileSendAsync(Send send, SendFileData data, long fileLength)
    {
        if (send.Type != SendType.File)
        {
            throw new BadRequestException("Send is not of type \"file\".");
        }

        if (fileLength < 1)
        {
            throw new BadRequestException("No file data.");
        }

        var storageBytesRemaining = await StorageRemainingForSendAsync(send);

        if (storageBytesRemaining < fileLength)
        {
            throw new BadRequestException("Not enough storage available.");
        }

        var fileId = Utilities.CoreHelpers.SecureRandomString(32, upper: false, special: false);

        try
        {
            data.Id = fileId;
            data.Size = fileLength;
            data.Validated = false;
            send.Data = JsonSerializer.Serialize(data,
                JsonHelpers.IgnoreWritingNull);
            await SaveSendAsync(send);
            return await _sendFileStorageService.GetSendFileUploadUrlAsync(send, fileId);
        }
        catch
        {
            // Clean up since this is not transactional
            await _sendFileStorageService.DeleteFileAsync(send, fileId);
            throw;
        }
    }

    public async Task UploadFileToExistingSendAsync(Stream stream, Send send)
    {
        if (send?.Data == null)
        {
            throw new BadRequestException("Send does not have file data");
        }

        if (send.Type != SendType.File)
        {
            throw new BadRequestException("Not a File Type Send.");
        }

        var data = JsonSerializer.Deserialize<SendFileData>(send.Data);

        if (data.Validated)
        {
            throw new BadRequestException("File has already been uploaded.");
        }

        await _sendFileStorageService.UploadNewFileAsync(stream, send, data.Id);

        if (!await ValidateSendFile(send))
        {
            throw new BadRequestException("File received does not match expected file length.");
        }
    }

    public async Task<bool> ValidateSendFile(Send send)
    {
        var fileData = JsonSerializer.Deserialize<SendFileData>(send.Data);

        var (valid, realSize) = await _sendFileStorageService.ValidateFileAsync(send, fileData.Id, fileData.Size, _fileSizeLeeway);

        if (!valid || realSize > MAX_FILE_SIZE)
        {
            // File reported differs in size from that promised. Must be a rogue client. Delete Send
            await DeleteSendAsync(send);
            return false;
        }

        // Update Send data if necessary
        if (realSize != fileData.Size)
        {
            fileData.Size = realSize.Value;
        }
        fileData.Validated = true;
        send.Data = JsonSerializer.Serialize(fileData,
            JsonHelpers.IgnoreWritingNull);
        await SaveSendAsync(send);

        return valid;
    }

    public async Task DeleteSendAsync(Send send)
    {
        await _sendRepository.DeleteAsync(send);
        if (send.Type == Enums.SendType.File)
        {
            var data = JsonSerializer.Deserialize<SendFileData>(send.Data);
            await _sendFileStorageService.DeleteFileAsync(send, data.Id);
        }
        await _pushService.PushSyncSendDeleteAsync(send);
    }

    public (bool grant, bool passwordRequiredError, bool passwordInvalidError) SendCanBeAccessed(Send send,
        string password)
    {
        var now = DateTime.UtcNow;
        if (send == null || send.MaxAccessCount.GetValueOrDefault(int.MaxValue) <= send.AccessCount ||
            send.ExpirationDate.GetValueOrDefault(DateTime.MaxValue) < now || send.Disabled ||
            send.DeletionDate < now)
        {
            return (false, false, false);
        }
        if (!string.IsNullOrWhiteSpace(send.Password))
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, true, false);
            }
            var passwordResult = _passwordHasher.VerifyHashedPassword(new User(), send.Password, password);
            if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                send.Password = HashPassword(password);
            }
            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return (false, false, true);
            }
        }

        return (true, false, false);
    }

    // Response: Send, password required, password invalid
    public async Task<(string, bool, bool)> GetSendFileDownloadUrlAsync(Send send, string fileId, string password)
    {
        if (send.Type != SendType.File)
        {
            throw new BadRequestException("Can only get a download URL for a file type of Send");
        }

        var (grantAccess, passwordRequired, passwordInvalid) = SendCanBeAccessed(send, password);

        if (!grantAccess)
        {
            return (null, passwordRequired, passwordInvalid);
        }

        send.AccessCount++;
        await _sendRepository.ReplaceAsync(send);
        await _pushService.PushSyncSendUpdateAsync(send);
        return (await _sendFileStorageService.GetSendFileDownloadUrlAsync(send, fileId), false, false);
    }

    // Response: Send, password required, password invalid
    public async Task<(Send, bool, bool)> AccessAsync(Guid sendId, string password)
    {
        var send = await _sendRepository.GetByIdAsync(sendId);
        var (grantAccess, passwordRequired, passwordInvalid) = SendCanBeAccessed(send, password);

        if (!grantAccess)
        {
            return (null, passwordRequired, passwordInvalid);
        }

        // TODO: maybe move this to a simple ++ sproc?
        if (send.Type != SendType.File)
        {
            // File sends are incremented during file download
            send.AccessCount++;
        }

        await _sendRepository.ReplaceAsync(send);
        await _pushService.PushSyncSendUpdateAsync(send);
        await RaiseReferenceEventAsync(send, ReferenceEventType.SendAccessed);
        return (send, false, false);
    }

    private async Task RaiseReferenceEventAsync(Send send, ReferenceEventType eventType)
    {
        await _referenceEventService.RaiseEventAsync(new ReferenceEvent
        {
            Id = send.UserId ?? default,
            Type = eventType,
            Source = ReferenceEventSource.User,
            SendType = send.Type,
            MaxAccessCount = send.MaxAccessCount,
            HasPassword = !string.IsNullOrWhiteSpace(send.Password),
            SendHasNotes = send.Data?.Contains("Notes"),
            ClientId = _currentContext.ClientId,
            ClientVersion = _currentContext.ClientVersion
        });
    }

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(new User(), password);
    }

    private async Task ValidateUserCanSaveAsync(Guid? userId, Send send)
    {
        if (!userId.HasValue || (!_currentContext.Organizations?.Any() ?? true))
        {
            return;
        }

        var anyDisableSendPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(userId.Value,
            PolicyType.DisableSend);
        if (anyDisableSendPolicies)
        {
            throw new BadRequestException("Due to an Enterprise Policy, you are only able to delete an existing Send.");
        }

        if (send.HideEmail.GetValueOrDefault())
        {
            var sendOptionsPolicies = await _policyService.GetPoliciesApplicableToUserAsync(userId.Value, PolicyType.SendOptions);
            if (sendOptionsPolicies.Any(p => CoreHelpers.LoadClassFromJsonData<SendOptionsPolicyData>(p.PolicyData)?.DisableHideEmail ?? false))
            {
                throw new BadRequestException("Due to an Enterprise Policy, you are not allowed to hide your email address from recipients when creating or editing a Send.");
            }
        }
    }

    private async Task<long> StorageRemainingForSendAsync(Send send)
    {
        var storageBytesRemaining = 0L;
        if (send.UserId.HasValue)
        {
            var user = await _userRepository.GetByIdAsync(send.UserId.Value);
            if (!await _userService.CanAccessPremium(user))
            {
                throw new BadRequestException("You must have premium status to use file Sends.");
            }

            if (!user.EmailVerified)
            {
                throw new BadRequestException("You must confirm your email to use file Sends.");
            }

            if (user.Premium)
            {
                storageBytesRemaining = user.StorageBytesRemaining();
            }
            else
            {
                // Users that get access to file storage/premium from their organization get the default
                // 1 GB max storage.
                storageBytesRemaining = user.StorageBytesRemaining(
                    _globalSettings.SelfHosted ? (short)10240 : (short)1);
            }
        }
        else if (send.OrganizationId.HasValue)
        {
            var org = await _organizationRepository.GetByIdAsync(send.OrganizationId.Value);
            if (!org.MaxStorageGb.HasValue)
            {
                throw new BadRequestException("This organization cannot use file sends.");
            }

            storageBytesRemaining = org.StorageBytesRemaining();
        }

        return storageBytesRemaining;
    }
}
