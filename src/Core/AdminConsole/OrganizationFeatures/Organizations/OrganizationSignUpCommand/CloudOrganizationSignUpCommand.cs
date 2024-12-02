﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Mappings;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand.Validation;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand;

public interface ICloudOrganizationSignUpCommand
{
    Task<SignUpOrganizationResponse_vNext> SignUpOrganizationAsync(OrganizationSignup signup);
}

public class CloudOrganizationSignUpCommand(
    IOrganizationUserRepository organizationUserRepository,
    IFeatureService featureService,
    IOrganizationBillingService organizationBillingService,
    IPaymentService paymentService,
    IPolicyService policyService,
    IReferenceEventService referenceEventService,
    ICurrentContext currentContext,
    IOrganizationRepository organizationRepository,
    IOrganizationApiKeyRepository organizationApiKeyRepository,
    IApplicationCacheService applicationCacheService,
    IPushRegistrationService pushRegistrationService,
    IPushNotificationService pushNotificationService,
    ICollectionRepository collectionRepository,
    IDeviceRepository deviceRepository,
    OrgSignUpPasswordManagerValidation signUpValidation,
    TimeProvider timeProvider
    ) : ICloudOrganizationSignUpCommand
{
    public async Task<SignUpOrganizationResponse_vNext> SignUpOrganizationAsync(OrganizationSignup signup)
    {
        var request = signup.WithPlan();

        if (signUpValidation.Validate(request) is InvalidResult<OrgSignUpWithPlan> invalidResult)
        {
            return new SignUpOrganizationResponse_vNext(invalidResult.ErrorMessages);
        }

        if (!request.Signup.IsFromProvider)
        {
            await ValidateSignUpPoliciesAsync(request.Signup.Owner.Id);
        }

        var organization = request.ToEntity(timeProvider.GetUtcNow());

        if (request.Plan.Type == PlanType.Free && !signup.IsFromProvider)
        {
            var adminCount =
                await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id);
            if (adminCount > 0)
            {
                return new SignUpOrganizationResponse_vNext("You can only be an admin of one free organization.");
            }
        }
        else if (request.Plan.Type != PlanType.Free)
        {
            if (featureService.IsEnabled(FeatureFlagKeys.AC2476_DeprecateStripeSourcesAPI))
            {
                var sale = OrganizationSale.From(organization, signup);
                await organizationBillingService.Finalize(sale);
            }
            else
            {
                if (signup.PaymentMethodType != null)
                {
                    await paymentService.PurchaseOrganizationAsync(organization, signup.PaymentMethodType.Value,
                        signup.PaymentToken, request.Plan, signup.AdditionalStorageGb, signup.AdditionalSeats,
                        signup.PremiumAccessAddon, signup.TaxInfo, signup.IsFromProvider,
                        signup.AdditionalSmSeats.GetValueOrDefault(),
                        signup.AdditionalServiceAccounts.GetValueOrDefault(), signup.IsFromSecretsManagerTrial);
                }
                else
                {
                    await paymentService.PurchaseOrganizationNoPaymentMethod(organization, request.Plan, signup.AdditionalSeats,
                        signup.PremiumAccessAddon, signup.AdditionalSmSeats.GetValueOrDefault(),
                        signup.AdditionalServiceAccounts.GetValueOrDefault(), signup.IsFromSecretsManagerTrial);
                }
            }
        }

        var ownerId = signup.IsFromProvider ? default : signup.Owner.Id;
        var returnValue = await SignUpAsync(organization, ownerId, signup.OwnerKey, signup.CollectionName, true);
        await referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.Signup, organization, currentContext)
            {
                PlanName = request.Plan.Name,
                PlanType = request.Plan.Type,
                Seats = returnValue.Item1.Seats,
                SignupInitiationPath = signup.InitiationPath,
                Storage = returnValue.Item1.MaxStorageGb,
                // TODO: add reference events for SmSeats and Service Accounts - see AC-1481
            });

        return new SignUpOrganizationResponse_vNext(returnValue.organization.Id, returnValue.organization.DisplayName(), returnValue.organizationUser.Id,
            returnValue.defaultCollection.Id);
    }

    private async Task ValidateSignUpPoliciesAsync(Guid ownerId)
    {
        var anySingleOrgPolicies = await policyService.AnyPoliciesApplicableToUserAsync(ownerId, PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            throw new BadRequestException("You may not create an organization. You belong to an organization " +
                                          "which has a policy that prohibits you from being a member of any other organization.");
        }
    }

    private async Task<(Organization organization, OrganizationUser organizationUser, Collection defaultCollection)>
        SignUpAsync(Organization organization,
            Guid ownerId, string ownerKey, string collectionName, bool withPayment)
    {
        try
        {
            await organizationRepository.CreateAsync(organization);
            await organizationApiKeyRepository.CreateAsync(new OrganizationApiKey
            {
                OrganizationId = organization.Id,
                ApiKey = CoreHelpers.SecureRandomString(30),
                Type = OrganizationApiKeyType.Default,
                RevisionDate = DateTime.UtcNow,
            });
            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);

            // ownerId == default if the org is created by a provider - in this case it's created without an
            // owner and the first owner is immediately invited afterwards
            OrganizationUser orgUser = null;
            if (ownerId != default)
            {
                orgUser = new OrganizationUser
                {
                    OrganizationId = organization.Id,
                    UserId = ownerId,
                    Key = ownerKey,
                    AccessSecretsManager = organization.UseSecretsManager,
                    Type = OrganizationUserType.Owner,
                    Status = OrganizationUserStatusType.Confirmed,
                    CreationDate = organization.CreationDate,
                    RevisionDate = organization.CreationDate
                };
                orgUser.SetNewId();

                await organizationUserRepository.CreateAsync(orgUser);

                var devices = await GetUserDeviceIdsAsync(orgUser.UserId.Value);
                await pushRegistrationService.AddUserRegistrationOrganizationAsync(devices, organization.Id.ToString());
                await pushNotificationService.PushSyncOrgKeysAsync(ownerId);
            }

            Collection defaultCollection = null;
            if (!string.IsNullOrWhiteSpace(collectionName))
            {
                defaultCollection = new Collection
                {
                    Name = collectionName,
                    OrganizationId = organization.Id,
                    CreationDate = organization.CreationDate,
                    RevisionDate = organization.CreationDate
                };

                // Give the owner Can Manage access over the default collection
                List<CollectionAccessSelection> defaultOwnerAccess = null;
                if (orgUser != null)
                {
                    defaultOwnerAccess =
                    [
                        new CollectionAccessSelection
                        {
                            Id = orgUser.Id,
                            HidePasswords = false,
                            ReadOnly = false,
                            Manage = true
                        }
                    ];
                }

                await collectionRepository.CreateAsync(defaultCollection, null, defaultOwnerAccess);
            }

            return (organization, orgUser, defaultCollection);
        }
        catch
        {
            if (withPayment)
            {
                await paymentService.CancelAndRecoverChargesAsync(organization);
            }

            if (organization.Id != default(Guid))
            {
                await organizationRepository.DeleteAsync(organization);
                await applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
            }

            throw;
        }
    }

    private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
    {
        var devices = await deviceRepository.GetManyByUserIdAsync(userId);
        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
    }
}
