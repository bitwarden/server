﻿#nullable enable

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
    private readonly IFeatureService _featureService;
    private readonly IUserService _userService;

    public UsersController(
        IUserRepository userRepository,
        ICipherRepository cipherRepository,
        IPaymentService paymentService,
        GlobalSettings globalSettings,
        IAccessControlService accessControlService,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IFeatureService featureService,
        IUserService userService)
    {
        _userRepository = userRepository;
        _cipherRepository = cipherRepository;
        _paymentService = paymentService;
        _globalSettings = globalSettings;
        _accessControlService = accessControlService;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _featureService = featureService;
        _userService = userService;
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

        var userModels = new List<UserModel>();

        if (_featureService.IsEnabled(FeatureFlagKeys.MembersTwoFAQueryOptimization))
        {
            var twoFactorAuthLookup = (await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(users.Select(u => u.Id))).ToList();

            userModels = UserModel.MapUserModels(users, twoFactorAuthLookup).ToList();
        }
        else
        {
            foreach (var user in users)
            {
                var isTwoFactorEnabled = await _userService.TwoFactorIsEnabledAsync(user);
                userModels.Add(UserModel.MapUserModel(user, isTwoFactorEnabled));
            }
        }

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

        return View(new UserViewModel(UserModel.MapUserModel(user, isTwoFactorEnabled), ciphers));
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
        return View(new UserEditModel(UserModel.MapUserModel(user, isTwoFactorEnabled), ciphers, billingInfo, billingHistoryInfo, _globalSettings));
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
