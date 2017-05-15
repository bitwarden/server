using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
using Bit.Core.Exceptions;
using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection;
using Stripe;
using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IDataProtector _dataProtector;
        private readonly IMailService _mailService;
        private readonly IPushService _pushService;

        public OrganizationService(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ICollectionRepository collectionRepository,
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IDataProtectionProvider dataProtectionProvider,
            IMailService mailService,
            IPushService pushService)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _collectionRepository = collectionRepository;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _dataProtector = dataProtectionProvider.CreateProtector("OrganizationServiceDataProtector");
            _mailService = mailService;
            _pushService = pushService;
        }
        public async Task<OrganizationBilling> GetBillingAsync(Organization organization)
        {
            var orgBilling = new OrganizationBilling();
            var customerService = new StripeCustomerService();
            var subscriptionService = new StripeSubscriptionService();
            var chargeService = new StripeChargeService();
            var invoiceService = new StripeInvoiceService();

            if(!string.IsNullOrWhiteSpace(organization.StripeCustomerId))
            {
                var customer = await customerService.GetAsync(organization.StripeCustomerId);
                if(customer != null)
                {
                    if(!string.IsNullOrWhiteSpace(customer.DefaultSourceId) && customer.Sources?.Data != null)
                    {
                        if(customer.DefaultSourceId.StartsWith("card_"))
                        {
                            orgBilling.PaymentSource =
                                customer.Sources.Data.FirstOrDefault(s => s.Card?.Id == customer.DefaultSourceId);
                        }
                        else if(customer.DefaultSourceId.StartsWith("ba_"))
                        {
                            orgBilling.PaymentSource =
                                customer.Sources.Data.FirstOrDefault(s => s.BankAccount?.Id == customer.DefaultSourceId);
                        }
                    }

                    var charges = await chargeService.ListAsync(new StripeChargeListOptions
                    {
                        CustomerId = customer.Id,
                        Limit = 20
                    });
                    orgBilling.Charges = charges?.Data?.OrderByDescending(c => c.Created);
                }
            }

            if(!string.IsNullOrWhiteSpace(organization.StripeSubscriptionId))
            {
                var sub = await subscriptionService.GetAsync(organization.StripeSubscriptionId);
                if(sub != null)
                {
                    orgBilling.Subscription = sub;
                }

                if(!sub.CanceledAt.HasValue && !string.IsNullOrWhiteSpace(organization.StripeCustomerId))
                {
                    try
                    {
                        var upcomingInvoice = await invoiceService.UpcomingAsync(organization.StripeCustomerId);
                        if(upcomingInvoice != null)
                        {
                            orgBilling.UpcomingInvoice = upcomingInvoice;
                        }
                    }
                    catch(StripeException) { }
                }
            }

            return orgBilling;
        }

        public async Task ReplacePaymentMethodAsync(Guid organizationId, string paymentToken)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            var cardService = new StripeCardService();
            var customerService = new StripeCustomerService();
            StripeCustomer customer = null;

            if(!string.IsNullOrWhiteSpace(organization.StripeCustomerId))
            {
                customer = await customerService.GetAsync(organization.StripeCustomerId);
            }

            if(customer == null)
            {
                customer = await customerService.CreateAsync(new StripeCustomerCreateOptions
                {
                    Description = organization.BusinessName,
                    Email = organization.BillingEmail,
                    SourceToken = paymentToken
                });

                organization.StripeCustomerId = customer.Id;
                await _organizationRepository.ReplaceAsync(organization);
            }

            await cardService.CreateAsync(customer.Id, new StripeCardCreateOptions
            {
                SourceToken = paymentToken
            });

            if(!string.IsNullOrWhiteSpace(customer.DefaultSourceId))
            {
                await cardService.DeleteAsync(customer.Id, customer.DefaultSourceId);
            }
        }

        public async Task CancelSubscriptionAsync(Guid organizationId, bool endOfPeriod = false)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            if(string.IsNullOrWhiteSpace(organization.StripeSubscriptionId))
            {
                throw new BadRequestException("Organization has no subscription.");
            }

            var subscriptionService = new StripeSubscriptionService();
            var sub = await subscriptionService.GetAsync(organization.StripeSubscriptionId);
            if(sub == null)
            {
                throw new BadRequestException("Organization subscription was not found.");
            }

            if(sub.CanceledAt.HasValue)
            {
                throw new BadRequestException("Organization subscription is already canceled.");
            }

            var canceledSub = await subscriptionService.CancelAsync(sub.Id, endOfPeriod);
            if(!canceledSub.CanceledAt.HasValue)
            {
                throw new BadRequestException("Unable to cancel subscription.");
            }
        }

        public async Task ReinstateSubscriptionAsync(Guid organizationId)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            if(string.IsNullOrWhiteSpace(organization.StripeSubscriptionId))
            {
                throw new BadRequestException("Organization has no subscription.");
            }

            var subscriptionService = new StripeSubscriptionService();
            var sub = await subscriptionService.GetAsync(organization.StripeSubscriptionId);
            if(sub == null)
            {
                throw new BadRequestException("Organization subscription was not found.");
            }

            if(sub.Status != "active" || !sub.CanceledAt.HasValue)
            {
                throw new BadRequestException("Organization subscription is not marked for cancellation.");
            }

            // Just touch the subscription.
            var updatedSub = await subscriptionService.UpdateAsync(sub.Id, new StripeSubscriptionUpdateOptions { });
            if(updatedSub.CanceledAt.HasValue)
            {
                throw new BadRequestException("Unable to reinstate subscription.");
            }
        }

        public async Task UpgradePlanAsync(Guid organizationId, PlanType plan, int additionalSeats)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            if(string.IsNullOrWhiteSpace(organization.StripeCustomerId))
            {
                throw new BadRequestException("No payment method found.");
            }

            var existingPlan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            if(existingPlan == null)
            {
                throw new BadRequestException("Existing plan not found.");
            }

            var newPlan = StaticStore.Plans.FirstOrDefault(p => p.Type == plan && !p.Disabled);
            if(newPlan == null)
            {
                throw new BadRequestException("Plan not found.");
            }

            if(existingPlan.Type == newPlan.Type)
            {
                throw new BadRequestException("Organization is already on this plan.");
            }

            if(existingPlan.UpgradeSortOrder >= newPlan.UpgradeSortOrder)
            {
                throw new BadRequestException("You cannot upgrade to this plan.");
            }

            if(!newPlan.CanBuyAdditionalSeats && additionalSeats > 0)
            {
                throw new BadRequestException("Plan does not allow additional seats.");
            }

            if(newPlan.CanBuyAdditionalSeats && newPlan.MaxAdditionalSeats.HasValue &&
                additionalSeats > newPlan.MaxAdditionalSeats.Value)
            {
                throw new BadRequestException($"Selected plan allows a maximum of " +
                    $"{newPlan.MaxAdditionalSeats.Value} additional seats.");
            }

            var newPlanSeats = (short)(newPlan.BaseSeats + (newPlan.CanBuyAdditionalSeats ? additionalSeats : 0));
            if(!organization.Seats.HasValue || organization.Seats.Value > newPlanSeats)
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organization.Id);
                if(userCount >= newPlanSeats)
                {
                    throw new BadRequestException($"Your organization currently has {userCount} seats filled. Your new plan " +
                        $"only has ({newPlanSeats}) seats. Remove some users.");
                }
            }

            if(newPlan.MaxCollections.HasValue &&
                (!organization.MaxCollections.HasValue || organization.MaxCollections.Value > newPlan.MaxCollections.Value))
            {
                var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(organization.Id);
                if(collectionCount > newPlan.MaxCollections.Value)
                {
                    throw new BadRequestException($"Your organization currently has {collectionCount} collections. " +
                        $"Your new plan allows for a maximum of ({newPlan.MaxCollections.Value}) collections. " +
                        "Remove some collections.");
                }
            }

            // TODO: Groups?

            var subscriptionService = new StripeSubscriptionService();
            if(string.IsNullOrWhiteSpace(organization.StripeSubscriptionId))
            {
                // They must have been on a free plan. Create new sub.
                var subCreateOptions = new StripeSubscriptionCreateOptions
                {
                    TrialPeriodDays = newPlan.TrialPeriodDays,
                    Items = new List<StripeSubscriptionItemOption>(),
                    Metadata = new Dictionary<string, string> {
                        { "organizationId", organization.Id.ToString() }
                    }
                };

                if(newPlan.StripePlanId != null)
                {
                    subCreateOptions.Items.Add(new StripeSubscriptionItemOption
                    {
                        PlanId = newPlan.StripePlanId,
                        Quantity = 1
                    });
                }

                if(additionalSeats > 0 && newPlan.StripeSeatPlanId != null)
                {
                    subCreateOptions.Items.Add(new StripeSubscriptionItemOption
                    {
                        PlanId = newPlan.StripeSeatPlanId,
                        Quantity = additionalSeats
                    });
                }

                await subscriptionService.CreateAsync(organization.StripeCustomerId, subCreateOptions);
            }
            else
            {
                // Update existing sub.
                var subUpdateOptions = new StripeSubscriptionUpdateOptions
                {
                    Items = new List<StripeSubscriptionItemUpdateOption>()
                };

                if(newPlan.StripePlanId != null)
                {
                    subUpdateOptions.Items.Add(new StripeSubscriptionItemUpdateOption
                    {
                        PlanId = newPlan.StripePlanId,
                        Quantity = 1
                    });
                }

                if(additionalSeats > 0 && newPlan.StripeSeatPlanId != null)
                {
                    subUpdateOptions.Items.Add(new StripeSubscriptionItemUpdateOption
                    {
                        PlanId = newPlan.StripeSeatPlanId,
                        Quantity = additionalSeats
                    });
                }

                await subscriptionService.UpdateAsync(organization.StripeSubscriptionId, subUpdateOptions);
            }

            // TODO: Update organization
        }

        public async Task AdjustSeatsAsync(Guid organizationId, int seatAdjustment)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            if(string.IsNullOrWhiteSpace(organization.StripeCustomerId))
            {
                throw new BadRequestException("No payment method found.");
            }

            if(string.IsNullOrWhiteSpace(organization.StripeSubscriptionId))
            {
                throw new BadRequestException("No subscription found.");
            }

            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            if(plan == null)
            {
                throw new BadRequestException("Existing plan not found.");
            }

            if(!plan.CanBuyAdditionalSeats)
            {
                throw new BadRequestException("Plan does not allow additional seats.");
            }

            var newSeatTotal = organization.Seats + seatAdjustment;
            if(plan.BaseSeats > newSeatTotal)
            {
                throw new BadRequestException($"Plan has a minimum of {plan.BaseSeats} seats.");
            }

            var additionalSeats = newSeatTotal - plan.BaseSeats;
            if(plan.MaxAdditionalSeats.HasValue && additionalSeats > plan.MaxAdditionalSeats.Value)
            {
                throw new BadRequestException($"Organization plan allows a maximum of " +
                    $"{plan.MaxAdditionalSeats.Value} additional seats.");
            }

            if(!organization.Seats.HasValue || organization.Seats.Value > newSeatTotal)
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organization.Id);
                if(userCount > newSeatTotal)
                {
                    throw new BadRequestException($"Your organization currently has {userCount} seats filled. Your new plan " +
                        $"only has ({newSeatTotal}) seats. Remove some users.");
                }
            }

            var invoiceService = new StripeInvoiceService();
            var subscriptionItemService = new StripeSubscriptionItemService();
            var subscriptionService = new StripeSubscriptionService();
            var sub = await subscriptionService.GetAsync(organization.StripeSubscriptionId);
            if(sub == null)
            {
                throw new BadRequestException("Subscription not found.");
            }

            var seatItem = sub.Items?.Data?.FirstOrDefault(i => i.Plan.Id == plan.StripeSeatPlanId);
            if(seatItem == null)
            {
                await subscriptionItemService.CreateAsync(new StripeSubscriptionItemCreateOptions
                {
                    PlanId = plan.StripeSeatPlanId,
                    Quantity = additionalSeats,
                    Prorate = true,
                    SubscriptionId = sub.Id
                });

                await PreviewUpcomingAndPayAsync(invoiceService, organization, plan);
            }
            else if(additionalSeats > 0)
            {
                await subscriptionItemService.UpdateAsync(seatItem.Id, new StripeSubscriptionItemUpdateOptions
                {
                    PlanId = plan.StripeSeatPlanId,
                    Quantity = additionalSeats,
                    Prorate = true
                });

                await PreviewUpcomingAndPayAsync(invoiceService, organization, plan);
            }
            else if(additionalSeats == 0)
            {
                await subscriptionItemService.DeleteAsync(seatItem.Id);
            }

            organization.Seats = (short?)newSeatTotal;
            await _organizationRepository.ReplaceAsync(organization);
        }

        private async Task PreviewUpcomingAndPayAsync(StripeInvoiceService invoiceService, Organization org, Plan plan)
        {
            var upcomingPreview = await invoiceService.UpcomingAsync(org.StripeCustomerId,
                new StripeUpcomingInvoiceOptions
                {
                    SubscriptionId = org.StripeSubscriptionId
                });

            var prorationAmount = upcomingPreview.StripeInvoiceLineItems?.Data?
                .TakeWhile(i => i.Plan.Id == plan.StripeSeatPlanId).Sum(i => i.Amount);
            if(prorationAmount.GetValueOrDefault() >= 500)
            {
                // Owes more than $5.00 on next invoice. Invoice them and pay now instead of waiting until next month.
                var invoice = await invoiceService.CreateAsync(org.StripeCustomerId,
                    new StripeInvoiceCreateOptions
                    {
                        SubscriptionId = org.StripeSubscriptionId
                    });

                await invoiceService.PayAsync(invoice.Id);
            }
        }

        public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup signup)
        {
            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == signup.Plan && !p.Disabled);
            if(plan == null)
            {
                throw new BadRequestException("Plan not found.");
            }

            var customerService = new StripeCustomerService();
            var subscriptionService = new StripeSubscriptionService();
            StripeCustomer customer = null;
            StripeSubscription subscription = null;

            if(!plan.CanBuyAdditionalSeats && signup.AdditionalSeats > 0)
            {
                throw new BadRequestException("Plan does not allow additional users.");
            }

            if(plan.CanBuyAdditionalSeats && plan.MaxAdditionalSeats.HasValue &&
                signup.AdditionalSeats > plan.MaxAdditionalSeats.Value)
            {
                throw new BadRequestException($"Selected plan allows a maximum of " +
                    $"{plan.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
            }

            // Pre-generate the org id so that we can save it with the Stripe subscription..
            Guid newOrgId = CoreHelpers.GenerateComb();

            if(plan.Type == PlanType.Free)
            {
                var ownerExistingOrgCount =
                    await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id);
                if(ownerExistingOrgCount > 0)
                {
                    throw new BadRequestException("You can only be an admin of one free organization.");
                }
            }
            else
            {
                customer = await customerService.CreateAsync(new StripeCustomerCreateOptions
                {
                    Description = signup.BusinessName,
                    Email = signup.BillingEmail,
                    SourceToken = signup.PaymentToken
                });

                var subCreateOptions = new StripeSubscriptionCreateOptions
                {
                    TrialPeriodDays = plan.TrialPeriodDays,
                    Items = new List<StripeSubscriptionItemOption>(),
                    Metadata = new Dictionary<string, string> {
                        { "organizationId", newOrgId.ToString() }
                    }
                };

                if(plan.StripePlanId != null)
                {
                    subCreateOptions.Items.Add(new StripeSubscriptionItemOption
                    {
                        PlanId = plan.StripePlanId,
                        Quantity = 1
                    });
                }

                if(signup.AdditionalSeats > 0 && plan.StripeSeatPlanId != null)
                {
                    subCreateOptions.Items.Add(new StripeSubscriptionItemOption
                    {
                        PlanId = plan.StripeSeatPlanId,
                        Quantity = signup.AdditionalSeats
                    });
                }

                try
                {
                    subscription = await subscriptionService.CreateAsync(customer.Id, subCreateOptions);
                }
                catch(StripeException)
                {
                    if(customer != null)
                    {
                        await customerService.DeleteAsync(customer.Id);
                    }

                    throw;
                }
            }

            var organization = new Organization
            {
                Id = newOrgId,
                Name = signup.Name,
                BillingEmail = signup.BillingEmail,
                BusinessName = signup.BusinessName,
                PlanType = plan.Type,
                Seats = (short)(plan.BaseSeats + signup.AdditionalSeats),
                MaxCollections = plan.MaxCollections,
                UseGroups = plan.UseGroups,
                Plan = plan.Name,
                StripeCustomerId = customer?.Id,
                StripeSubscriptionId = subscription?.Id,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            };

            try
            {
                await _organizationRepository.CreateAsync(organization);

                var orgUser = new OrganizationUser
                {
                    OrganizationId = organization.Id,
                    UserId = signup.Owner.Id,
                    Key = signup.OwnerKey,
                    Type = OrganizationUserType.Owner,
                    Status = OrganizationUserStatusType.Confirmed,
                    AccessAll = true,
                    CreationDate = DateTime.UtcNow,
                    RevisionDate = DateTime.UtcNow
                };

                await _organizationUserRepository.CreateAsync(orgUser);

                // push
                await _pushService.PushSyncOrgKeysAsync(signup.Owner.Id);

                return new Tuple<Organization, OrganizationUser>(organization, orgUser);
            }
            catch
            {
                if(subscription != null)
                {
                    await subscriptionService.CancelAsync(subscription.Id, false);
                }

                if(customer != null)
                {
                    var chargeService = new StripeChargeService();
                    var charges = await chargeService.ListAsync(new StripeChargeListOptions { CustomerId = customer.Id });
                    if(charges?.Data != null)
                    {
                        var refundService = new StripeRefundService();
                        foreach(var charge in charges.Data.Where(c => !c.Refunded))
                        {
                            await refundService.CreateAsync(charge.Id);
                        }
                    }

                    await customerService.DeleteAsync(customer.Id);
                }

                if(organization.Id != default(Guid))
                {
                    await _organizationRepository.DeleteAsync(organization);
                }

                throw;
            }
        }

        public async Task DeleteAsync(Organization organization)
        {
            if(!string.IsNullOrWhiteSpace(organization.StripeSubscriptionId))
            {
                var subscriptionService = new StripeSubscriptionService();
                var canceledSub = await subscriptionService.CancelAsync(organization.StripeSubscriptionId, false);
                if(!canceledSub.CanceledAt.HasValue)
                {
                    throw new BadRequestException("Unable to cancel subscription.");
                }
            }

            await _organizationRepository.DeleteAsync(organization);
        }

        public async Task DisableAsync(Guid organizationId)
        {
            var org = await _organizationRepository.GetByIdAsync(organizationId);
            if(org != null && org.Enabled)
            {
                org.Enabled = false;
                await _organizationRepository.ReplaceAsync(org);

                // TODO: send email to owners?
            }
        }

        public async Task EnableAsync(Guid organizationId)
        {
            var org = await _organizationRepository.GetByIdAsync(organizationId);
            if(org != null && !org.Enabled)
            {
                org.Enabled = true;
                await _organizationRepository.ReplaceAsync(org);
            }
        }

        public async Task UpdateAsync(Organization organization, bool updateBilling = false)
        {
            if(organization.Id == default(Guid))
            {
                throw new ApplicationException("Cannot create org this way. Call SignUpAsync.");
            }

            await _organizationRepository.ReplaceAsync(organization);

            if(updateBilling && !string.IsNullOrWhiteSpace(organization.StripeCustomerId))
            {
                var customerService = new StripeCustomerService();
                await customerService.UpdateAsync(organization.StripeCustomerId, new StripeCustomerUpdateOptions
                {
                    Email = organization.BillingEmail,
                    Description = organization.BusinessName
                });
            }
        }

        public async Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid invitingUserId, string email,
            OrganizationUserType type, bool accessAll, IEnumerable<SelectionReadOnly> collections)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            if(organization.Seats.HasValue)
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organizationId);
                if(userCount >= organization.Seats.Value)
                {
                    throw new BadRequestException("You have reached the maximum number of users " +
                        $"({organization.Seats.Value}) for this organization.");
                }
            }

            // Make sure user is not already invited
            var existingOrgUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, email);
            if(existingOrgUser != null)
            {
                throw new BadRequestException("User already invited.");
            }

            var orgUser = new OrganizationUser
            {
                OrganizationId = organizationId,
                UserId = null,
                Email = email.ToLowerInvariant(),
                Key = null,
                Type = type,
                Status = OrganizationUserStatusType.Invited,
                AccessAll = accessAll,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            };

            if(!orgUser.AccessAll && collections.Any())
            {
                await _organizationUserRepository.CreateAsync(orgUser, collections);
            }
            else
            {
                await _organizationUserRepository.CreateAsync(orgUser);
            }

            await SendInviteAsync(orgUser);
            return orgUser;
        }

        public async Task ResendInviteAsync(Guid organizationId, Guid invitingUserId, Guid organizationUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser == null || orgUser.OrganizationId != organizationId ||
                orgUser.Status != OrganizationUserStatusType.Invited)
            {
                throw new BadRequestException("User invalid.");
            }

            await SendInviteAsync(orgUser);
        }

        private async Task SendInviteAsync(OrganizationUser orgUser)
        {
            var org = await _organizationRepository.GetByIdAsync(orgUser.OrganizationId);
            var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
            var token = _dataProtector.Protect(
                $"OrganizationUserInvite {orgUser.Id} {orgUser.Email} {nowMillis}");
            await _mailService.SendOrganizationInviteEmailAsync(org.Name, orgUser, token);
        }

        public async Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser == null || !orgUser.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new BadRequestException("User invalid.");
            }

            if(orgUser.Status != OrganizationUserStatusType.Invited)
            {
                throw new BadRequestException("Already accepted.");
            }

            var ownerExistingOrgCount = await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(user.Id);
            if(ownerExistingOrgCount > 0)
            {
                throw new BadRequestException("You can only be an admin of one free organization.");
            }

            var tokenValidationFailed = true;
            try
            {
                var unprotectedData = _dataProtector.Unprotect(token);
                var dataParts = unprotectedData.Split(' ');
                if(dataParts.Length == 4 &&
                    dataParts[0] == "OrganizationUserInvite" &&
                    new Guid(dataParts[1]) == orgUser.Id &&
                    dataParts[2].Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
                {
                    var creationTime = CoreHelpers.FromEpocMilliseconds(Convert.ToInt64(dataParts[3]));
                    tokenValidationFailed = creationTime.AddDays(5) < DateTime.UtcNow;
                }
            }
            catch
            {
                tokenValidationFailed = true;
            }

            if(tokenValidationFailed)
            {
                throw new BadRequestException("Invalid token.");
            }

            orgUser.Status = OrganizationUserStatusType.Accepted;
            orgUser.UserId = user.Id;
            orgUser.Email = null;
            await _organizationUserRepository.ReplaceAsync(orgUser);

            // TODO: send notification emails to org admins and accepting user?

            return orgUser;
        }

        public async Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key,
            Guid confirmingUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser == null || orgUser.Status != OrganizationUserStatusType.Accepted ||
                orgUser.OrganizationId != organizationId)
            {
                throw new BadRequestException("User not valid.");
            }

            orgUser.Status = OrganizationUserStatusType.Confirmed;
            orgUser.Key = key;
            orgUser.Email = null;
            await _organizationUserRepository.ReplaceAsync(orgUser);

            var user = await _userRepository.GetByIdAsync(orgUser.UserId.Value);
            var org = await _organizationRepository.GetByIdAsync(organizationId);
            if(user != null && org != null)
            {
                await _mailService.SendOrganizationConfirmedEmailAsync(org.Name, user.Email);
            }

            // push
            await _pushService.PushSyncOrgKeysAsync(orgUser.UserId.Value);

            return orgUser;
        }

        public async Task SaveUserAsync(OrganizationUser user, Guid savingUserId, IEnumerable<SelectionReadOnly> collections)
        {
            if(user.Id.Equals(default(Guid)))
            {
                throw new BadRequestException("Invite the user first.");
            }

            var confirmedOwners = (await GetConfirmedOwnersAsync(user.OrganizationId)).ToList();
            if(user.Type != OrganizationUserType.Owner && confirmedOwners.Count == 1 && confirmedOwners[0].Id == user.Id)
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }


            if(user.AccessAll)
            {
                // We don't need any collections if we're flagged to have all access.
                collections = new List<SelectionReadOnly>();
            }
            await _organizationUserRepository.ReplaceAsync(user, collections);
        }

        public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser == null || orgUser.OrganizationId != organizationId)
            {
                throw new BadRequestException("User not valid.");
            }

            if(orgUser.UserId == deletingUserId)
            {
                throw new BadRequestException("You cannot remove yourself.");
            }

            var confirmedOwners = (await GetConfirmedOwnersAsync(organizationId)).ToList();
            if(confirmedOwners.Count == 1 && confirmedOwners[0].Id == organizationUserId)
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }

            await _organizationUserRepository.DeleteAsync(orgUser);
        }

        public async Task DeleteUserAsync(Guid organizationId, Guid userId)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
            if(orgUser == null)
            {
                throw new NotFoundException();
            }

            var confirmedOwners = (await GetConfirmedOwnersAsync(organizationId)).ToList();
            if(confirmedOwners.Count == 1 && confirmedOwners[0].Id == orgUser.Id)
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }

            await _organizationUserRepository.DeleteAsync(orgUser);
        }

        public async Task ImportAsync(Guid organizationId,
            Guid importingUserId,
            IEnumerable<Tuple<Group, HashSet<string>>> groups,
            IEnumerable<string> newUsers,
            IEnumerable<string> removeUsers)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            if(!organization.UseGroups)
            {
                throw new BadRequestException("Organization cannot use groups.");
            }

            var newUsersSet = new HashSet<string>(newUsers);
            var existingUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
            var existingUsersIdDict = existingUsers.ToDictionary(u => u.Email, u => u.Id);

            // Users
            // Remove Users
            if(removeUsers.Any())
            {
                var removeUsersSet = new HashSet<string>(removeUsers);
                var existingUsersDict = existingUsers.ToDictionary(u => u.Email);

                var usersToRemove = removeUsersSet
                    .Except(newUsersSet)
                    .Where(ru => existingUsersDict.ContainsKey(ru))
                    .Select(ru => existingUsersDict[ru]);

                foreach(var user in usersToRemove)
                {
                    await _organizationUserRepository.DeleteAsync(new OrganizationUser { Id = user.Id });
                    existingUsersIdDict.Remove(user.Email);
                }
            }

            // Add new users
            if(newUsers.Any())
            {
                var existingUsersSet = new HashSet<string>(existingUsers.Select(u => u.Email));
                var usersToAdd = newUsersSet.Except(existingUsersSet).ToList();

                var seatsAvailable = int.MaxValue;
                if(organization.Seats.HasValue)
                {
                    var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organizationId);
                    seatsAvailable = organization.Seats.Value - userCount;
                    if(seatsAvailable < usersToAdd.Count)
                    {
                        // throw exception?
                        return;
                    }
                }

                foreach(var user in usersToAdd)
                {
                    try
                    {
                        var newUser = await InviteUserAsync(organizationId, importingUserId, user, OrganizationUserType.User,
                            false, new List<SelectionReadOnly>());
                        existingUsersIdDict.Add(newUser.Email, newUser.Id);
                    }
                    catch(BadRequestException)
                    {
                        continue;
                    }
                }
            }

            // Groups
            if(groups?.Any() ?? false)
            {
                var groupsDict = groups.ToDictionary(g => g.Item1.ExternalId);
                var existingGroups = (await _groupRepository.GetManyByOrganizationIdAsync(organizationId)).ToList();
                var existingGroupsDict = existingGroups.ToDictionary(g => g.ExternalId);

                var newGroups = groups
                    .Where(g => !existingGroupsDict.ContainsKey(g.Item1.ExternalId))
                    .Select(g => g.Item1);
                var updateGroups = existingGroups
                    .Where(eg => groups.Any(g => g.Item1.ExternalId == eg.ExternalId && g.Item1.Name != eg.Name))
                    .ToList();

                foreach(var group in newGroups)
                {
                    group.CreationDate = group.RevisionDate = DateTime.UtcNow;

                    await _groupRepository.CreateAsync(group);
                    await UpdateUsersAsync(group, groupsDict[group.ExternalId].Item2, existingUsersIdDict);
                }

                foreach(var group in updateGroups)
                {
                    group.RevisionDate = DateTime.UtcNow;
                    group.Name = existingGroupsDict[group.ExternalId].Name;

                    await _groupRepository.ReplaceAsync(group);
                    await UpdateUsersAsync(group, groupsDict[group.ExternalId].Item2, existingUsersIdDict);
                }
            }
        }

        private async Task UpdateUsersAsync(Group group, HashSet<string> groupUsers,
            Dictionary<string, Guid> existingUsersIdDict)
        {
            var users = groupUsers.Union(existingUsersIdDict.Keys).Select(u => existingUsersIdDict[u]);
            await _groupRepository.UpdateUsersAsync(group.Id, users);
        }

        private async Task<IEnumerable<OrganizationUser>> GetConfirmedOwnersAsync(Guid organizationId)
        {
            var owners = await _organizationUserRepository.GetManyByOrganizationAsync(organizationId,
                OrganizationUserType.Owner);
            return owners.Where(o => o.Status == OrganizationUserStatusType.Confirmed);
        }
    }
}
