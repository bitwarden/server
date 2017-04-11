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

namespace Bit.Core.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ISubvaultRepository _subvaultRepository;
        private readonly ISubvaultUserRepository _subvaultUserRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDataProtector _dataProtector;
        private readonly IMailService _mailService;

        public OrganizationService(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ISubvaultRepository subvaultRepository,
            ISubvaultUserRepository subvaultUserRepository,
            IUserRepository userRepository,
            IDataProtectionProvider dataProtectionProvider,
            IMailService mailService)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _subvaultRepository = subvaultRepository;
            _subvaultUserRepository = subvaultUserRepository;
            _userRepository = userRepository;
            _dataProtector = dataProtectionProvider.CreateProtector("OrganizationServiceDataProtector");
            _mailService = mailService;
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

            if(newPlan.MaxSubvaults.HasValue &&
                (!organization.MaxSubvaults.HasValue || organization.MaxSubvaults.Value > newPlan.MaxSubvaults.Value))
            {
                var subvaultCount = await _subvaultRepository.GetCountByOrganizationIdAsync(organization.Id);
                if(subvaultCount > newPlan.MaxSubvaults.Value)
                {
                    throw new BadRequestException($"Your organization currently has {subvaultCount} subvaults. " +
                        $"Your new plan allows for a maximum of ({newPlan.MaxSubvaults.Value}) subvaults. " +
                        "Remove some subvaults.");
                }
            }

            var subscriptionService = new StripeSubscriptionService();
            if(string.IsNullOrWhiteSpace(organization.StripeSubscriptionId))
            {
                // They must have been on a free plan. Create new sub.
                var subCreateOptions = new StripeSubscriptionCreateOptions
                {
                    Items = new List<StripeSubscriptionItemOption>
                    {
                        new StripeSubscriptionItemOption
                        {
                            PlanId = newPlan.StripePlanId,
                            Quantity = 1
                        }
                    }
                };

                if(additionalSeats > 0)
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
                    Items = new List<StripeSubscriptionItemUpdateOption>
                    {
                        new StripeSubscriptionItemUpdateOption
                        {
                            PlanId = newPlan.StripePlanId,
                            Quantity = 1
                        }
                    }
                };

                if(additionalSeats > 0)
                {
                    subUpdateOptions.Items.Add(new StripeSubscriptionItemUpdateOption
                    {
                        PlanId = newPlan.StripeSeatPlanId,
                        Quantity = additionalSeats
                    });
                }

                await subscriptionService.UpdateAsync(organization.StripeSubscriptionId, subUpdateOptions);
            }
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

            if(plan.Type == Enums.PlanType.Free)
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
                    Items = new List<StripeSubscriptionItemOption>
                    {
                        new StripeSubscriptionItemOption
                        {
                            PlanId = plan.StripePlanId,
                            Quantity = 1
                        }
                    }
                };

                if(signup.AdditionalSeats > 0)
                {
                    subCreateOptions.Items.Add(new StripeSubscriptionItemOption
                    {
                        PlanId = plan.StripeSeatPlanId,
                        Quantity = signup.AdditionalSeats
                    });
                }

                subscription = await subscriptionService.CreateAsync(customer.Id, subCreateOptions);
            }

            var organization = new Organization
            {
                Name = signup.Name,
                BillingEmail = signup.BillingEmail,
                BusinessName = signup.BusinessName,
                PlanType = plan.Type,
                Seats = (short)(plan.BaseSeats + signup.AdditionalSeats),
                MaxSubvaults = plan.MaxSubvaults,
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
                    Email = signup.Owner.Email,
                    Key = signup.OwnerKey,
                    Type = Enums.OrganizationUserType.Owner,
                    Status = Enums.OrganizationUserStatusType.Confirmed,
                    CreationDate = DateTime.UtcNow,
                    RevisionDate = DateTime.UtcNow
                };

                await _organizationUserRepository.CreateAsync(orgUser);

                return new Tuple<Organization, OrganizationUser>(organization, orgUser);
            }
            catch
            {
                if(subscription != null)
                {
                    await subscriptionService.CancelAsync(subscription.Id);
                }

                // TODO: reverse payments

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
            Enums.OrganizationUserType type, IEnumerable<SubvaultUser> subvaults)
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

            var orgSubvaults = await _subvaultRepository.GetManyByOrganizationIdAsync(organizationId);
            var filteredSubvaults = subvaults.Where(s => orgSubvaults.Any(os => os.Id == s.SubvaultId));

            var orgUser = new OrganizationUser
            {
                OrganizationId = organizationId,
                UserId = null,
                Email = email,
                Key = null,
                Type = type,
                Status = Enums.OrganizationUserStatusType.Invited,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            };

            await _organizationUserRepository.CreateAsync(orgUser);
            await SaveUserSubvaultsAsync(orgUser, filteredSubvaults, true);
            await SendInviteAsync(orgUser);

            return orgUser;
        }

        public async Task ResendInviteAsync(Guid organizationId, Guid invitingUserId, Guid organizationUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser == null || orgUser.OrganizationId != organizationId ||
                orgUser.Status != Enums.OrganizationUserStatusType.Invited)
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
            if(orgUser == null || orgUser.Email != user.Email)
            {
                throw new BadRequestException("User invalid.");
            }

            if(orgUser.Status != Enums.OrganizationUserStatusType.Invited)
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
                if(dataParts.Length == 4 && dataParts[0] == "OrganizationUserInvite" &&
                    new Guid(dataParts[1]) == orgUser.Id && dataParts[2] == user.Email)
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

            orgUser.Status = Enums.OrganizationUserStatusType.Accepted;
            orgUser.UserId = user.Id;
            orgUser.Email = null;
            await _organizationUserRepository.ReplaceAsync(orgUser);

            // TODO: send email

            return orgUser;
        }

        public async Task<OrganizationUser> ConfirmUserAsync(Guid organizationId, Guid organizationUserId, string key,
            Guid confirmingUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser == null || orgUser.Status != Enums.OrganizationUserStatusType.Accepted ||
                orgUser.OrganizationId != organizationId)
            {
                throw new BadRequestException("User not valid.");
            }

            orgUser.Status = Enums.OrganizationUserStatusType.Confirmed;
            orgUser.Key = key;
            orgUser.Email = null;
            await _organizationUserRepository.ReplaceAsync(orgUser);

            // TODO: send email

            return orgUser;
        }

        public async Task SaveUserAsync(OrganizationUser user, Guid savingUserId, IEnumerable<SubvaultUser> subvaults)
        {
            if(user.Id.Equals(default(Guid)))
            {
                throw new BadRequestException("Invite the user first.");
            }

            var confirmedOwners = (await GetConfirmedOwnersAsync(user.OrganizationId)).ToList();
            if(user.Type != Enums.OrganizationUserType.Owner && confirmedOwners.Count == 1 && confirmedOwners[0].Id == user.Id)
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }

            var orgSubvaults = await _subvaultRepository.GetManyByOrganizationIdAsync(user.OrganizationId);
            var filteredSubvaults = subvaults.Where(s => orgSubvaults.Any(os => os.Id == s.SubvaultId));

            await _organizationUserRepository.ReplaceAsync(user);
            await SaveUserSubvaultsAsync(user, filteredSubvaults, false);
        }

        public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid deletingUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser == null || orgUser.OrganizationId != organizationId)
            {
                throw new BadRequestException("User not valid.");
            }

            var confirmedOwners = (await GetConfirmedOwnersAsync(organizationId)).ToList();
            if(confirmedOwners.Count == 1 && confirmedOwners[0].Id == organizationUserId)
            {
                throw new BadRequestException("Organization must have at least one confirmed owner.");
            }

            await _organizationUserRepository.DeleteAsync(orgUser);
        }

        private async Task<IEnumerable<OrganizationUser>> GetConfirmedOwnersAsync(Guid organizationId)
        {
            var owners = await _organizationUserRepository.GetManyByOrganizationAsync(organizationId,
                Enums.OrganizationUserType.Owner);
            return owners.Where(o => o.Status == Enums.OrganizationUserStatusType.Confirmed);
        }

        private async Task SaveUserSubvaultsAsync(OrganizationUser user, IEnumerable<SubvaultUser> subvaults, bool newUser)
        {
            if(subvaults == null)
            {
                subvaults = new List<SubvaultUser>();
            }

            var orgSubvaults = await _subvaultRepository.GetManyByOrganizationIdAsync(user.OrganizationId);
            var currentUserSubvaults = newUser ? null : await _subvaultUserRepository.GetManyByOrganizationUserIdAsync(user.Id);

            // Let's make sure all these belong to this user and organization.
            var filteredSubvaults = subvaults.Where(s => orgSubvaults.Any(os => os.Id == s.SubvaultId));
            foreach(var subvault in filteredSubvaults)
            {
                var existingSubvaultUser = currentUserSubvaults?.FirstOrDefault(cs => cs.SubvaultId == subvault.SubvaultId);
                if(existingSubvaultUser != null)
                {
                    subvault.Id = existingSubvaultUser.Id;
                    subvault.CreationDate = existingSubvaultUser.CreationDate;
                }

                subvault.OrganizationUserId = user.Id;
                await _subvaultUserRepository.UpsertAsync(subvault);
            }

            if(!newUser)
            {
                var subvaultsToDelete = currentUserSubvaults.Where(cs =>
                    !filteredSubvaults.Any(s => s.SubvaultId == cs.SubvaultId));
                foreach(var subvault in subvaultsToDelete)
                {
                    await _subvaultUserRepository.DeleteAsync(subvault);
                }
            }
        }
    }
}
