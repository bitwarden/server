using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
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
    private readonly ILogger<SendsController> _logger;
    private readonly IFeatureService _featureService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IHasPremiumAccessQuery _hasPremiumAccessQuery;

    public ReceivesController(
        IReceiveRepository receiveRepository,
        IReceiveAuthorizationService receiveAuthorizationService,
        IReceiveFileStorageService receiveFileStorageService,
        IReceiveValidationService receiveValidationService,
        IUserService userService,
        ILogger<SendsController> logger,
        IFeatureService featureService,
        IPushNotificationService pushNotificationService,
        IHasPremiumAccessQuery hasPremiumAccessQuery
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
    }

    // add endpoints
}
