using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bit.Admin.Models;
using Bit.Core;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Bit.Admin.Controllers
{
    [Authorize]
    [SelfHosted(NotSelfHostedOnly = true)]
    public class ToolsController : Controller
    {
        private readonly GlobalSettings _globalSettings;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IUserService _userService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IInstallationRepository _installationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IPaymentService _paymentService;
        private readonly ITaxRateRepository _taxRateRepository;

        public ToolsController(
            GlobalSettings globalSettings,
            IOrganizationRepository organizationRepository,
            IOrganizationService organizationService,
            IUserService userService,
            ITransactionRepository transactionRepository,
            IInstallationRepository installationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ITaxRateRepository taxRateRepository,
            IPaymentService paymentService)
        {
            _globalSettings = globalSettings;
            _organizationRepository = organizationRepository;
            _organizationService = organizationService;
            _userService = userService;
            _transactionRepository = transactionRepository;
            _installationRepository = installationRepository;
            _organizationUserRepository = organizationUserRepository;
            _taxRateRepository = taxRateRepository;
            _paymentService = paymentService;
        }

        public IActionResult ChargeBraintree()
        {
            return View(new ChargeBraintreeModel());
        }

        [HttpPost]
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
                            CustomField = $"{btObjIdField}:{btObjId}"
                        }
                    },
                    CustomFields = new Dictionary<string, string>
                    {
                        [btObjIdField] = btObjId.ToString()
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

        public IActionResult CreateTransaction(Guid? organizationId = null, Guid? userId = null)
        {
            return View("CreateUpdateTransaction", new CreateUpdateTransactionModel
            {
                OrganizationId = organizationId,
                UserId = userId
            });
        }

        [HttpPost]
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

        public IActionResult PromoteAdmin()
        {
            return View();
        }

        [HttpPost]
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

        public IActionResult GenerateLicense()
        {
            return View();
        }

        [HttpPost]
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
                var license = await _organizationService.GenerateLicenseAsync(organization,
                    model.InstallationId.Value, model.Version);
                return File(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(license, Formatting.Indented)),
                    "text/plain", "bitwarden_organization_license.json");
            }
            else if (user != null)
            {
                var license = await _userService.GenerateLicenseAsync(user, null, model.Version);
                return File(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(license, Formatting.Indented)),
                    "text/plain", "bitwarden_premium_license.json");
            }
            else
            {
                throw new Exception("No license to generate.");
            }
        }

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

        public IActionResult TaxRateAddEdit(string stripeTaxRateId = null, string country = null, string state = null, string postalCode = null, decimal rate = 0) 
        {
            var model = new TaxRateAddEditModel()
            {
                StripeTaxRateId = stripeTaxRateId,
                Country = country,
                State = state,
                PostalCode = postalCode,
                Rate = rate
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> TaxRateAddEdit(TaxRateAddEditModel model) 
        {
            var existingRateCheck = await _taxRateRepository.GetByLocationAsync(new TaxRate() { Country = model.Country, PostalCode = model.PostalCode });
            if (existingRateCheck.FirstOrDefault() != null) 
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

        [HttpPost]
        public async Task<IActionResult> TaxRateUpdate(TaxRate model) 
        {
            if (!string.IsNullOrWhiteSpace(model.Id))
            {
                await _paymentService.UpdateTaxRateAsync(model);
            }

            return RedirectToAction("TaxRate");
        }

        public async Task<bool> TaxRateArchive(string stripeTaxRateId) 
        {
            if (!string.IsNullOrWhiteSpace(stripeTaxRateId))
            {
                await _paymentService.ArchiveTaxRateAsync(new TaxRate() { Id = stripeTaxRateId });
                return true;
            }

            return false;
        }
    }
}
