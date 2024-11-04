#nullable enable

using Bit.Admin.Enums;
using Bit.Admin.Models;
using Bit.Admin.Services;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
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
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IUserService _userService;
    private readonly IFeatureService _featureService;

    public UsersController(
        IUserRepository userRepository,
        ICipherRepository cipherRepository,
        IPaymentService paymentService,
        GlobalSettings globalSettings,
        IAccessControlService accessControlService,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IUserService userService,
        IFeatureService featureService)
    {
        _userRepository = userRepository;
        _cipherRepository = cipherRepository;
        _paymentService = paymentService;
        _globalSettings = globalSettings;
        _accessControlService = accessControlService;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _userService = userService;
        _featureService = featureService;
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

        var twoFactorAuthLookup = (await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(users.Select(u => u.Id))).ToList();
        var userModels = UserViewModel.MapViewModels(users, twoFactorAuthLookup).ToList();

        return View(new UsersModel
        {
            Items = userModels,
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

        var isTwoFactorEnabled = await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user);
        var verifiedDomain = await AccountDeprovisioningEnabled(user.Id);
        return View(UserViewModel.MapViewModel(user, isTwoFactorEnabled, ciphers, verifiedDomain));
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
        var isTwoFactorEnabled = await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user);
        var verifiedDomain = await AccountDeprovisioningEnabled(user.Id);
        return View(new UserEditModel(user, isTwoFactorEnabled, ciphers, billingInfo, billingHistoryInfo, _globalSettings, verifiedDomain));
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

    // TODO: Feature flag to be removed in PM-14207
    private async Task<bool?> AccountDeprovisioningEnabled(Guid userId)
    {
        return _featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            ? await _userService.IsManagedByAnyOrganizationAsync(userId)
            : null;
    }
}
