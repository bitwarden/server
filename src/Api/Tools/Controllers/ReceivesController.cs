using Bit.Api.Models.Response;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Auth.Identity;
using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.ReceiveFeatures.Queries.Interfaces;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

[Route("receives")]
public class ReceivesController : Controller
{
    private readonly IReceiveRepository _receiveRepository;
    private readonly IReceiveAuthorizationService _receiveAuthorizationService;
    private readonly IReceiveFileStorageService _receiveFileStorageService;
    private readonly IReceiveValidationService _receiveValidationService;
    private readonly IUserService _userService;
    private readonly ILogger<ReceivesController> _logger;
    private readonly IFeatureService _featureService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ICreateReceiveCommand _createReceiveCommand;
    private readonly IUpdateReceiveCommand _updateReceiveCommand;
    private readonly IUploadReceiveFileCommand _uploadReceiveFileCommand;
    private readonly IHasPremiumAccessQuery _hasPremiumAccessQuery;
    private readonly IReceiveOwnerQuery _receiveOwnerQuery;

    public ReceivesController(
        IReceiveRepository receiveRepository,
        IReceiveAuthorizationService receiveAuthorizationService,
        IReceiveFileStorageService receiveFileStorageService,
        IReceiveValidationService receiveValidationService,
        IUserService userService,
        ILogger<ReceivesController> logger,
        IFeatureService featureService,
        IPushNotificationService pushNotificationService,
        ICreateReceiveCommand createReceiveCommand,
        IUpdateReceiveCommand updateReceiveCommand,
        IUploadReceiveFileCommand uploadReceiveFileCommand,
        IHasPremiumAccessQuery hasPremiumAccessQuery,
        IReceiveOwnerQuery receiveOwnerQuery
    )
    {
        _receiveRepository = receiveRepository;
        _receiveAuthorizationService = receiveAuthorizationService;
        _receiveFileStorageService = receiveFileStorageService;
        _receiveValidationService = receiveValidationService;
        _userService = userService;
        _logger = logger;
        _featureService = featureService;
        _pushNotificationService = pushNotificationService;
        _hasPremiumAccessQuery = hasPremiumAccessQuery;
        _createReceiveCommand = createReceiveCommand;
        _updateReceiveCommand = updateReceiveCommand;
        _uploadReceiveFileCommand = uploadReceiveFileCommand;
        _receiveOwnerQuery = receiveOwnerQuery;
    }

    [Authorize(Policies.Application)]
    [HttpGet("{id}")]
    public async Task<ReceiveResponseModel> Get(string id)
    {
        var receiveId = new Guid(id);
        var receive = await _receiveOwnerQuery.Get(receiveId, User);
        return new ReceiveResponseModel(receive);
    }

    [Authorize(Policies.Application)]
    [HttpGet("")]
    public async Task<ListResponseModel<ReceiveResponseModel>> GetAll()
    {
        var receives = await _receiveOwnerQuery.GetOwned(User);
        var responses = receives.Select(r => new ReceiveResponseModel(r));
        return new ListResponseModel<ReceiveResponseModel>(responses);
    }

    [AllowAnonymous]
    [HttpGet("{id}/shared")]
    public async Task<SharedReceiveResponseModel> GetShared(Guid id)
    {
        var receive = await GetReceiveWithSecretValidationAsync(id);
        return new SharedReceiveResponseModel(receive);
    }

    [AllowAnonymous]
    [HttpPost("{id}/file")]
    public async Task<ReceiveFileUploadDataResponseModel> GetReceiveFileUploadUrl(Guid id)
    {
        var receive = await GetReceiveWithSecretValidationAsync(id);
        var url = await _uploadReceiveFileCommand.GetUploadUrlAsync(receive);
        if (url == null)
        {
            throw new BadRequestException("Invalid request.");
        }

        return new ReceiveFileUploadDataResponseModel(url, _receiveFileStorageService.FileUploadType);
    }

    private async Task<Core.Tools.Entities.Receive> GetReceiveWithSecretValidationAsync(Guid id)
    {
        if (!Request.Headers.TryGetValue("Receive-Secret", out var secret))
        {
            throw new BadRequestException("Invalid request.");
        }

        var receive = await _receiveRepository.GetByIdAsync(id);
        if (receive == null)
        {
            throw new NotFoundException();
        }

        var decodedSecret = System.Text.Encoding.UTF8.GetString(CoreHelpers.Base64UrlDecode(secret.ToString()));
        if (!string.Equals(receive.Secret, decodedSecret, StringComparison.Ordinal))
        {
            throw new BadRequestException("Invalid request.");
        }

        if (!_receiveAuthorizationService.ReceiveCanBeAccessed(receive))
        {
            throw new NotFoundException();
        }

        return receive;
    }

    [Authorize(Policies.Application)]
    [HttpPost("")]
    public async Task<ReceiveResponseModel> CreateReceiveAsync([FromBody] ReceiveRequestModel request)
    {
        var userId = _userService.GetProperUserId(User) ?? throw new InvalidOperationException("User ID not found");
        var hasPremium = await _hasPremiumAccessQuery.HasPremiumAccessAsync(userId);
        if (!hasPremium)
        {
            throw new BadRequestException("Creating a Receive requires premium");
        }

        var receive = await _createReceiveCommand.CreateAsync(request.ToReceive(userId));
        return new ReceiveResponseModel(receive);
    }

    [Authorize(Policies.Application)]
    [HttpPut("{id}")]
    public async Task<ReceiveResponseModel> UpdateReceiveAsync([FromRoute] Guid id, [FromBody] UpdateReceiveRequestModel request)
    {
        var userId = _userService.GetProperUserId(User) ?? throw new InvalidOperationException("User ID not found");
        var hasPremium = await _hasPremiumAccessQuery.HasPremiumAccessAsync(userId);
        if (!hasPremium)
        {
            throw new BadRequestException("Updating a receive requires premium");
        }

        var updatedReceive = await _updateReceiveCommand.UpdateAsync(request.ToUpdateData(id), userId);

        return new ReceiveResponseModel(updatedReceive);
    }
}
