using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public interface IInviteUsersValidation
{
    Task<ValidationResult<OrganizationUserInviteUpgrade>> ValidateAsync(
        InviteOrganizationUserRefined request);
}

public class InviteUsersValidation(
    IGlobalSettings globalSettings,
    IProviderRepository providerRepository,
    IPaymentService paymentService) : IInviteUsersValidation
{
    public async Task<ValidationResult<OrganizationUserInviteUpgrade>> ValidateAsync(
        InviteOrganizationUserRefined request)
    {
        if (ValidateEnvironment(globalSettings) is Invalid<IGlobalSettings> invalidEnvironment)
        {
            return new Invalid<OrganizationUserInviteUpgrade>(invalidEnvironment.ErrorMessageString);
        }

        if (InvitingUserOrganizationValidation.Validate(request.Organization) is Invalid<OrganizationDto> organizationValidation)
        {
            return new Invalid<OrganizationUserInviteUpgrade>(organizationValidation.ErrorMessageString);
        }

        var subscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(request);

        if (PasswordManagerInviteUserValidation.Validate(subscriptionUpdate) is
            Invalid<PasswordManagerSubscriptionUpdate> invalidSubscriptionUpdate)
        {
            return new Invalid<OrganizationUserInviteUpgrade>(invalidSubscriptionUpdate.ErrorMessageString);
        }

        var smSubscriptionUpdate = SecretsManagerSubscriptionUpdate.Create(request, subscriptionUpdate);

        if (SecretsManagerInviteUserValidation.Validate(smSubscriptionUpdate) is
            Invalid<SecretsManagerSubscriptionUpdate> invalidSmSubscriptionUpdate)
        {
            return new Invalid<OrganizationUserInviteUpgrade>(invalidSmSubscriptionUpdate.ErrorMessageString);
        }

        var provider = await providerRepository.GetByOrganizationIdAsync(request.Organization.OrganizationId);

        if (InvitingUserOrganizationProviderValidation.Validate(ProviderDto.FromProviderEntity(provider)) is
            Invalid<ProviderDto> invalidProviderValidation)
        {
            return new Invalid<OrganizationUserInviteUpgrade>(invalidProviderValidation.ErrorMessageString);
        }

        var paymentSubscription = await paymentService.GetSubscriptionAsync(request.Organization);

        if (InviteUserPaymentValidation.Validate(PaymentSubscriptionDto.FromSubscriptionInfo(paymentSubscription, request.Organization)) is
            Invalid<PaymentSubscriptionDto> invalidPaymentValidation)
        {
            return new Invalid<OrganizationUserInviteUpgrade>(invalidPaymentValidation.ErrorMessageString);
        }

        return new Valid<OrganizationUserInviteUpgrade>(null);
    }

    public static ValidationResult<IGlobalSettings> ValidateEnvironment(IGlobalSettings globalSettings) =>
        globalSettings.SelfHosted
            ? new Invalid<IGlobalSettings>(InviteUserValidationErrorMessages.CannotAutoScaleOnSelfHostedError)
            : new Valid<IGlobalSettings>(globalSettings);
}

public record OrganizationUserInviteUpgrade
{
    public PasswordManagerSubscriptionUpgrade PasswordManagerSubscriptionUpgrade { get; }
    public SecretManagerSubscriptionUpgrade SecretManagerSubscriptionUpgrade { get; }
    public OrganizationUserForInvite[] OrganizationUsers { get; }

    private OrganizationUserInviteUpgrade()
    {
    }

    public static OrganizationUserInviteUpgrade Create()
    {
        return new();
    }
}

public abstract record ProductSubscription
{
    public int? Seats { get; protected set; }
}

public record PasswordManagerSubscription : ProductSubscription
{
    private PasswordManagerSubscription(int? seats)
    {
        Seats = seats;
    }

    public static PasswordManagerSubscription Create(int? seats) => new(seats);
};

public record SecretManagerSubscription : ProductSubscription
{
    private SecretManagerSubscription(int? seats)
    {
        Seats = seats;
    }

    public static SecretManagerSubscription Create(int? seats) => new(seats);
};

public abstract record SubscriptionUpgrade<T> where T : ProductSubscription
{
    public T Current { get; protected set; }
    public T Upgrade { get; protected set; }
    public int NewSeatsRequired => Upgrade.Seats - Current.Seats ?? 0;
    public int? MaxAutoScaleSeats { get; protected set; }
}

public record PasswordManagerSubscriptionUpgrade : SubscriptionUpgrade<PasswordManagerSubscription>
{
    private PasswordManagerSubscriptionUpgrade(PasswordManagerSubscription current, PasswordManagerSubscription upgrade,
        int? maxAutoScale)
    {
        Current = current;
        Upgrade = upgrade;
        MaxAutoScaleSeats = maxAutoScale;
    }

    public static PasswordManagerSubscriptionUpgrade Create(PasswordManagerSubscription current,
        PasswordManagerSubscription upgrade, int? maxAutoScale) =>
        new(current, upgrade, maxAutoScale);
}

public record SecretManagerSubscriptionUpgrade : SubscriptionUpgrade<SecretManagerSubscription>
{
    private SecretManagerSubscriptionUpgrade(SecretManagerSubscription current, SecretManagerSubscription upgrade,
        int? maxAutoScale)
    {
        Current = current;
        Upgrade = upgrade;
        MaxAutoScaleSeats = maxAutoScale;
    }

    public static SecretManagerSubscriptionUpgrade Create(SecretManagerSubscription current,
        SecretManagerSubscription upgrade, int? maxAutoScale) =>
        new(current, upgrade, maxAutoScale);
}
