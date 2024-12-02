using System.Text;
using System.Text.Json;
using Bit.Admin.Enums;
using Bit.Admin.Models;
using Bit.Admin.Utilities;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.BitStripe;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Platform.Installations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxRate = Bit.Core.Entities.TaxRate;

namespace Bit.Admin.Controllers;

[Authorize]
[SelfHosted(NotSelfHostedOnly = true)]
public class ToolsController : Controller
{
    private readonly GlobalSettings _globalSettings;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICloudGetOrganizationLicenseQuery _cloudGetOrganizationLicenseQuery;
    private readonly IUserService _userService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IInstallationRepository _installationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IWebHostEnvironment _environment;

    public ToolsController(
        GlobalSettings globalSettings,
        IOrganizationRepository organizationRepository,
        ICloudGetOrganizationLicenseQuery cloudGetOrganizationLicenseQuery,
        IUserService userService,
        ITransactionRepository transactionRepository,
        IInstallationRepository installationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ITaxRateRepository taxRateRepository,
        IPaymentService paymentService,
        IStripeAdapter stripeAdapter,
        IWebHostEnvironment environment)
    {
        _globalSettings = globalSettings;
        _organizationRepository = organizationRepository;
        _cloudGetOrganizationLicenseQuery = cloudGetOrganizationLicenseQuery;
        _userService = userService;
        _transactionRepository = transactionRepository;
        _installationRepository = installationRepository;
        _organizationUserRepository = organizationUserRepository;
        _taxRateRepository = taxRateRepository;
        _paymentService = paymentService;
        _stripeAdapter = stripeAdapter;
        _environment = environment;
    }

    [RequirePermission(Permission.Tools_ChargeBrainTreeCustomer)]
    public IActionResult ChargeBraintree()
    {
        return View(new ChargeBraintreeModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ChargeBrainTreeCustomer)]
    public async Task<IActionResult> ChargeBraintree(ChargeBraintreeModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var btGateway = new Braintree.BraintreeGateway
        {
            Environment = _globalSettings.Braintree.Production ?
                Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
            MerchantId = _globalSettings.Braintree.MerchantId,
            PublicKey = _globalSettings.Braintree.PublicKey,
            PrivateKey = _globalSettings.Braintree.PrivateKey
        };

        var btObjIdField = model.Id[0] == 'o' ? "organization_id" : "user_id";
        var btObjId = new Guid(model.Id.Substring(1, 32));

        var transactionResult = await btGateway.Transaction.SaleAsync(
            new Braintree.TransactionRequest
            {
                Amount = model.Amount.Value,
                CustomerId = model.Id,
                Options = new Braintree.TransactionOptionsRequest
                {
                    SubmitForSettlement = true,
                    PayPal = new Braintree.TransactionOptionsPayPalRequest
                    {
                        CustomField = $"{btObjIdField}:{btObjId},region:{_globalSettings.BaseServiceUri.CloudRegion}"
                    }
                },
                CustomFields = new Dictionary<string, string>
                {
                    [btObjIdField] = btObjId.ToString(),
                    ["region"] = _globalSettings.BaseServiceUri.CloudRegion
                }
            });

        if (!transactionResult.IsSuccess())
        {
            ModelState.AddModelError(string.Empty, "Charge failed. " +
                "Refer to Braintree admin portal for more information.");
        }
        else
        {
            model.TransactionId = transactionResult.Target.Id;
            model.PayPalTransactionId = transactionResult.Target?.PayPalDetails?.CaptureId;
        }
        return View(model);
    }

    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public IActionResult CreateTransaction(Guid? organizationId = null, Guid? userId = null)
    {
        return View("CreateUpdateTransaction", new CreateUpdateTransactionModel
        {
            OrganizationId = organizationId,
            UserId = userId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public async Task<IActionResult> CreateTransaction(CreateUpdateTransactionModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("CreateUpdateTransaction", model);
        }

        await _transactionRepository.CreateAsync(model.ToTransaction());
        if (model.UserId.HasValue)
        {
            return RedirectToAction("Edit", "Users", new { id = model.UserId });
        }
        else
        {
            return RedirectToAction("Edit", "Organizations", new { id = model.OrganizationId });
        }
    }

    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public async Task<IActionResult> EditTransaction(Guid id)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id);
        if (transaction == null)
        {
            return RedirectToAction("Index", "Home");
        }
        return View("CreateUpdateTransaction", new CreateUpdateTransactionModel(transaction));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public async Task<IActionResult> EditTransaction(Guid id, CreateUpdateTransactionModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("CreateUpdateTransaction", model);
        }
        await _transactionRepository.ReplaceAsync(model.ToTransaction(id));
        if (model.UserId.HasValue)
        {
            return RedirectToAction("Edit", "Users", new { id = model.UserId });
        }
        else
        {
            return RedirectToAction("Edit", "Organizations", new { id = model.OrganizationId });
        }
    }

    [RequirePermission(Permission.Tools_PromoteAdmin)]
    public IActionResult PromoteAdmin()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_PromoteAdmin)]
    public async Task<IActionResult> PromoteAdmin(PromoteAdminModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var orgUsers = await _organizationUserRepository.GetManyByOrganizationAsync(
            model.OrganizationId.Value, null);
        var user = orgUsers.FirstOrDefault(u => u.UserId == model.UserId.Value);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.UserId), "User Id not found in this organization.");
        }
        else if (user.Type != Core.Enums.OrganizationUserType.Admin)
        {
            ModelState.AddModelError(nameof(model.UserId), "User is not an admin of this organization.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        user.Type = Core.Enums.OrganizationUserType.Owner;
        await _organizationUserRepository.ReplaceAsync(user);
        return RedirectToAction("Edit", "Organizations", new { id = model.OrganizationId.Value });
    }

    [RequirePermission(Permission.Tools_GenerateLicenseFile)]
    public IActionResult GenerateLicense()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_GenerateLicenseFile)]
    public async Task<IActionResult> GenerateLicense(LicenseModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        User user = null;
        Organization organization = null;
        if (model.UserId.HasValue)
        {
            user = await _userService.GetUserByIdAsync(model.UserId.Value);
            if (user == null)
            {
                ModelState.AddModelError(nameof(model.UserId), "User Id not found.");
            }
        }
        else if (model.OrganizationId.HasValue)
        {
            organization = await _organizationRepository.GetByIdAsync(model.OrganizationId.Value);
            if (organization == null)
            {
                ModelState.AddModelError(nameof(model.OrganizationId), "Organization not found.");
            }
            else if (!organization.Enabled)
            {
                ModelState.AddModelError(nameof(model.OrganizationId), "Organization is disabled.");
            }
        }
        if (model.InstallationId.HasValue)
        {
            var installation = await _installationRepository.GetByIdAsync(model.InstallationId.Value);
            if (installation == null)
            {
                ModelState.AddModelError(nameof(model.InstallationId), "Installation not found.");
            }
            else if (!installation.Enabled)
            {
                ModelState.AddModelError(nameof(model.OrganizationId), "Installation is disabled.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (organization != null)
        {
            var license = await _cloudGetOrganizationLicenseQuery.GetLicenseAsync(organization,
                model.InstallationId.Value, model.Version);
            var ms = new MemoryStream();
            await JsonSerializer.SerializeAsync(ms, license, JsonHelpers.Indented);
            ms.Seek(0, SeekOrigin.Begin);
            return File(ms, "text/plain", "bitwarden_organization_license.json");
        }
        else if (user != null)
        {
            var license = await _userService.GenerateLicenseAsync(user, null, model.Version);
            var ms = new MemoryStream();
            ms.Seek(0, SeekOrigin.Begin);
            await JsonSerializer.SerializeAsync(ms, license, JsonHelpers.Indented);
            ms.Seek(0, SeekOrigin.Begin);
            return File(ms, "text/plain", "bitwarden_premium_license.json");
        }
        else
        {
            throw new Exception("No license to generate.");
        }
    }

    [RequirePermission(Permission.Tools_ManageTaxRates)]
    public async Task<IActionResult> TaxRate(int page = 1, int count = 25)
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
        var rates = await _taxRateRepository.SearchAsync(skip, count);
        return View(new TaxRatesModel
        {
            Items = rates.ToList(),
            Page = page,
            Count = count
        });
    }

    [RequirePermission(Permission.Tools_ManageTaxRates)]
    public async Task<IActionResult> TaxRateAddEdit(string stripeTaxRateId = null)
    {
        if (string.IsNullOrWhiteSpace(stripeTaxRateId))
        {
            return View(new TaxRateAddEditModel());
        }

        var rate = await _taxRateRepository.GetByIdAsync(stripeTaxRateId);
        var model = new TaxRateAddEditModel()
        {
            StripeTaxRateId = stripeTaxRateId,
            Country = rate.Country,
            State = rate.State,
            PostalCode = rate.PostalCode,
            Rate = rate.Rate
        };

        return View(model);
    }

    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManageTaxRates)]
    public async Task<IActionResult> TaxRateUpload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentNullException(nameof(file));
        }

        // Build rates and validate them first before updating DB & Stripe
        var taxRateUpdates = new List<TaxRate>();
        var currentTaxRates = await _taxRateRepository.GetAllActiveAsync();
        using var reader = new StreamReader(file.OpenReadStream());
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var taxParts = line.Split(',');
            if (taxParts.Length < 2)
            {
                throw new Exception($"This line is not in the format of <postal code>,<rate>,<state code>,<country code>: {line}");
            }
            var postalCode = taxParts[0].Trim();
            if (string.IsNullOrWhiteSpace(postalCode))
            {
                throw new Exception($"'{line}' is not valid, the first element must contain a postal code.");
            }
            if (!decimal.TryParse(taxParts[1], out var rate) || rate <= 0M || rate > 100)
            {
                throw new Exception($"{taxParts[1]} is not a valid rate/decimal for {postalCode}");
            }
            var state = taxParts.Length > 2 ? taxParts[2] : null;
            var country = (taxParts.Length > 3 ? taxParts[3] : null);
            if (string.IsNullOrWhiteSpace(country))
            {
                country = "US";
            }
            var taxRate = currentTaxRates.FirstOrDefault(r => r.Country == country && r.PostalCode == postalCode) ??
                new TaxRate
                {
                    Country = country,
                    PostalCode = postalCode,
                    Active = true,
                };
            taxRate.Rate = rate;
            taxRate.State = state ?? taxRate.State;
            taxRateUpdates.Add(taxRate);
        }

        foreach (var taxRate in taxRateUpdates)
        {
            if (!string.IsNullOrWhiteSpace(taxRate.Id))
            {
                await _paymentService.UpdateTaxRateAsync(taxRate);
            }
            else
            {
                await _paymentService.CreateTaxRateAsync(taxRate);
            }
        }

        return RedirectToAction("TaxRate");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManageTaxRates)]
    public async Task<IActionResult> TaxRateAddEdit(TaxRateAddEditModel model)
    {
        var existingRateCheck = await _taxRateRepository.GetByLocationAsync(new TaxRate() { Country = model.Country, PostalCode = model.PostalCode });
        if (existingRateCheck.Any())
        {
            ModelState.AddModelError(nameof(model.PostalCode), "A tax rate already exists for this Country/Postal Code combination.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var taxRate = new TaxRate()
        {
            Id = model.StripeTaxRateId,
            Country = model.Country,
            State = model.State,
            PostalCode = model.PostalCode,
            Rate = model.Rate
        };

        if (!string.IsNullOrWhiteSpace(model.StripeTaxRateId))
        {
            await _paymentService.UpdateTaxRateAsync(taxRate);
        }
        else
        {
            await _paymentService.CreateTaxRateAsync(taxRate);
        }

        return RedirectToAction("TaxRate");
    }

    [RequirePermission(Permission.Tools_ManageTaxRates)]
    public async Task<IActionResult> TaxRateArchive(string stripeTaxRateId)
    {
        if (!string.IsNullOrWhiteSpace(stripeTaxRateId))
        {
            await _paymentService.ArchiveTaxRateAsync(new TaxRate() { Id = stripeTaxRateId });
        }

        return RedirectToAction("TaxRate");
    }

    [RequirePermission(Permission.Tools_ManageStripeSubscriptions)]
    public async Task<IActionResult> StripeSubscriptions(StripeSubscriptionListOptions options)
    {
        options = options ?? new StripeSubscriptionListOptions();
        options.Limit = 10;
        options.Expand = new List<string>() { "data.customer", "data.latest_invoice" };
        options.SelectAll = false;

        var subscriptions = await _stripeAdapter.SubscriptionListAsync(options);

        options.StartingAfter = subscriptions.LastOrDefault()?.Id;
        options.EndingBefore = await StripeSubscriptionsGetHasPreviousPage(subscriptions, options) ?
            subscriptions.FirstOrDefault()?.Id :
            null;

        var isProduction = _environment.IsProduction();
        var model = new StripeSubscriptionsModel()
        {
            Items = subscriptions.Select(s => new StripeSubscriptionRowModel(s)).ToList(),
            Prices = (await _stripeAdapter.PriceListAsync(new Stripe.PriceListOptions() { Limit = 100 })).Data,
            TestClocks = isProduction ? new List<Stripe.TestHelpers.TestClock>() : await _stripeAdapter.TestClockListAsync(),
            Filter = options
        };
        return View(model);
    }

    [HttpPost]
    [RequirePermission(Permission.Tools_ManageStripeSubscriptions)]
    public async Task<IActionResult> StripeSubscriptions([FromForm] StripeSubscriptionsModel model)
    {
        if (!ModelState.IsValid)
        {
            var isProduction = _environment.IsProduction();
            model.Prices = (await _stripeAdapter.PriceListAsync(new Stripe.PriceListOptions() { Limit = 100 })).Data;
            model.TestClocks = isProduction ? new List<Stripe.TestHelpers.TestClock>() : await _stripeAdapter.TestClockListAsync();
            return View(model);
        }

        if (model.Action == StripeSubscriptionsAction.Export || model.Action == StripeSubscriptionsAction.BulkCancel)
        {
            var subscriptions = model.Filter.SelectAll ?
                await _stripeAdapter.SubscriptionListAsync(model.Filter) :
                model.Items.Where(x => x.Selected).Select(x => x.Subscription);

            if (model.Action == StripeSubscriptionsAction.Export)
            {
                return StripeSubscriptionsExport(subscriptions);
            }

            if (model.Action == StripeSubscriptionsAction.BulkCancel)
            {
                await StripeSubscriptionsCancel(subscriptions);
            }
        }
        else
        {
            if (model.Action == StripeSubscriptionsAction.PreviousPage || model.Action == StripeSubscriptionsAction.Search)
            {
                model.Filter.StartingAfter = null;
            }

            if (model.Action == StripeSubscriptionsAction.NextPage || model.Action == StripeSubscriptionsAction.Search)
            {
                if (!string.IsNullOrEmpty(model.Filter.StartingAfter))
                {
                    var subscription = await _stripeAdapter.SubscriptionGetAsync(model.Filter.StartingAfter);
                    if (subscription.Status == "canceled")
                    {
                        model.Filter.StartingAfter = null;
                    }
                }
                model.Filter.EndingBefore = null;
            }
        }


        return RedirectToAction("StripeSubscriptions", model.Filter);
    }

    // This requires a redundant API call to Stripe because of the way they handle pagination.
    // The StartingBefore value has to be inferred from the list we get, and isn't supplied by Stripe.
    private async Task<bool> StripeSubscriptionsGetHasPreviousPage(List<Stripe.Subscription> subscriptions, StripeSubscriptionListOptions options)
    {
        var hasPreviousPage = false;
        if (subscriptions.FirstOrDefault()?.Id != null)
        {
            var previousPageSearchOptions = new StripeSubscriptionListOptions()
            {
                EndingBefore = subscriptions.FirstOrDefault().Id,
                Limit = 1,
                Status = options.Status,
                CurrentPeriodEndDate = options.CurrentPeriodEndDate,
                CurrentPeriodEndRange = options.CurrentPeriodEndRange,
                Price = options.Price
            };
            hasPreviousPage = (await _stripeAdapter.SubscriptionListAsync(previousPageSearchOptions)).Count > 0;
        }
        return hasPreviousPage;
    }

    private async Task StripeSubscriptionsCancel(IEnumerable<Stripe.Subscription> subscriptions)
    {
        foreach (var s in subscriptions)
        {
            await _stripeAdapter.SubscriptionCancelAsync(s.Id);
            if (s.LatestInvoice?.Status == "open")
            {
                await _stripeAdapter.InvoiceVoidInvoiceAsync(s.LatestInvoiceId);
            }
        }
    }

    private FileResult StripeSubscriptionsExport(IEnumerable<Stripe.Subscription> subscriptions)
    {
        var fieldsToExport = subscriptions.Select(s => new
        {
            StripeId = s.Id,
            CustomerEmail = s.Customer?.Email,
            SubscriptionStatus = s.Status,
            InvoiceDueDate = s.CurrentPeriodEnd,
            SubscriptionProducts = s.Items?.Data.Select(p => p.Plan.Id)
        });

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var result = System.Text.Json.JsonSerializer.Serialize(fieldsToExport, options);
        var bytes = Encoding.UTF8.GetBytes(result);
        return File(bytes, "application/json", "StripeSubscriptionsSearch.json");
    }
}
