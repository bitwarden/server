using Bit.Admin.Enums;
using Bit.Admin.Models;
using Bit.Admin.Services;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly IPaymentService _paymentService;
    private readonly GlobalSettings _globalSettings;
    private readonly IAccessControlService _accessControlService;
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;

    public UsersController(
        IUserRepository userRepository,
        ICipherRepository cipherRepository,
        IPaymentService paymentService,
        GlobalSettings globalSettings,
        IAccessControlService accessControlService,
        ICurrentContext currentContext,
        IFeatureService featureService,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery)
    {
        _userRepository = userRepository;
        _cipherRepository = cipherRepository;
        _paymentService = paymentService;
        _globalSettings = globalSettings;
        _accessControlService = accessControlService;
        _currentContext = currentContext;
        _featureService = featureService;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
    }

    [RequirePermission(Permission.User_List_View)]
    public async Task<IActionResult> Index(string email, int page = 1, int count = 25)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (count < 1)
        {
            count = 1;
        }

        var skip = (page - 1) * count;
        var users = await _userRepository.SearchAsync(email, skip, count);

        if (_featureService.IsEnabled(FeatureFlagKeys.MembersTwoFAQueryOptimization))
        {
            var user2Fa = (await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(users.Select(u => u.Id))).ToList();
            // TempDataSerializer is having an issue serializing an empty IEnumerable<Tuple<T1,T2>>, do not set if empty.
            if (user2Fa.Count != 0)
            {
                TempData["UsersTwoFactorIsEnabled"] = user2Fa;
            }
        }

        return View(new UsersModel
        {
            Items = users as List<User>,
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            Page = page,
            Count = count,
            Action = _globalSettings.SelfHosted ? "View" : "Edit"
        });
    }

    public async Task<IActionResult> View(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return RedirectToAction("Index");
        }

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(id);
        return View(new UserViewModel(user, ciphers));
    }

    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return RedirectToAction("Index");
        }

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(id);
        var billingInfo = await _paymentService.GetBillingAsync(user);
        var billingHistoryInfo = await _paymentService.GetBillingHistoryAsync(user);
        return View(new UserEditModel(user, ciphers, billingInfo, billingHistoryInfo, _globalSettings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id, UserEditModel model)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return RedirectToAction("Index");
        }

        var canUpgradePremium = _accessControlService.UserHasPermission(Permission.User_UpgradePremium);

        if (_accessControlService.UserHasPermission(Permission.User_Premium_Edit) ||
            canUpgradePremium)
        {
            user.MaxStorageGb = model.MaxStorageGb;
            user.Premium = model.Premium;
        }

        if (_accessControlService.UserHasPermission(Permission.User_Billing_Edit))
        {
            user.Gateway = model.Gateway;
            user.GatewayCustomerId = model.GatewayCustomerId;
            user.GatewaySubscriptionId = model.GatewaySubscriptionId;
        }

        if (_accessControlService.UserHasPermission(Permission.User_Licensing_Edit) ||
            canUpgradePremium)
        {
            user.LicenseKey = model.LicenseKey;
            user.PremiumExpirationDate = model.PremiumExpirationDate;
        }

        await _userRepository.ReplaceAsync(user);
        return RedirectToAction("Edit", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.User_Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user != null)
        {
            await _userRepository.DeleteAsync(user);
        }

        return RedirectToAction("Index");
    }
}
