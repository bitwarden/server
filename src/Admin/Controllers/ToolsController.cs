using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Admin.Models;
using Bit.Core;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers
{
    [Authorize]
    [SelfHosted(NotSelfHostedOnly = true)]
    public class ToolsController : Controller
    {
        private readonly GlobalSettings _globalSettings;
        private readonly ITransactionRepository _transactionRepository;

        public ToolsController(
            GlobalSettings globalSettings,
            ITransactionRepository transactionRepository)
        {
            _globalSettings = globalSettings;
            _transactionRepository = transactionRepository;
        }

        public IActionResult ChargeBraintree()
        {
            return View(new ChargeBraintreeModel());
        }

        [HttpPost]
        public async Task<IActionResult> ChargeBraintree(ChargeBraintreeModel model)
        {
            if(!ModelState.IsValid)
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

            if(!transactionResult.IsSuccess())
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
            if(!ModelState.IsValid)
            {
                return View("CreateUpdateTransaction", model);
            }

            await _transactionRepository.CreateAsync(model.ToTransaction());
            if(model.UserId.HasValue)
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
            if(transaction == null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View("CreateUpdateTransaction", new CreateUpdateTransactionModel(transaction));
        }

        [HttpPost]
        public async Task<IActionResult> EditTransaction(Guid id, CreateUpdateTransactionModel model)
        {
            if(!ModelState.IsValid)
            {
                return View("CreateUpdateTransaction", model);
            }
            await _transactionRepository.ReplaceAsync(model.ToTransaction(id));
            if(model.UserId.HasValue)
            {
                return RedirectToAction("Edit", "Users", new { id = model.UserId });
            }
            else
            {
                return RedirectToAction("Edit", "Organizations", new { id = model.OrganizationId });
            }
        }
    }
}
