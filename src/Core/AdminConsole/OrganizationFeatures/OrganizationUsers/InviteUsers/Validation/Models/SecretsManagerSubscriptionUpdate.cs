using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

public class SecretsManagerSubscriptionUpdate
{
    public bool UseSecretsManger { get; private init; }
    public int? Seats { get; private init; }
    public int? MaxAutoScaleSeats { get; private init; }
    public int OccupiedSeats { get; private init; }
    public int AdditionalSeats { get; private init; }
    public PasswordManagerSubscriptionUpdate PasswordManagerSubscriptionUpdate { get; private init; }
    public int? AvailableSeats => Seats - OccupiedSeats;
    public int SeatsRequiredToAdd => AdditionalSeats - AvailableSeats ?? 0;
    public int? UpdatedSeatTotal => Seats + SeatsRequiredToAdd;

    private SecretsManagerSubscriptionUpdate(bool useSecretsManger, int? organizationSeats,
        int? organizationAutoScaleSeatLimit, int currentSeats, int seatsToAdd, PasswordManagerSubscriptionUpdate passwordManagerSeats)
    {
        UseSecretsManger = useSecretsManger;
        Seats = organizationSeats;
        MaxAutoScaleSeats = organizationAutoScaleSeatLimit;
        OccupiedSeats = currentSeats;
        AdditionalSeats = seatsToAdd;
        PasswordManagerSubscriptionUpdate = passwordManagerSeats;
    }

    public static SecretsManagerSubscriptionUpdate Create(InviteUserOrganizationValidationRequest refined, PasswordManagerSubscriptionUpdate passwordManagerSubscriptionUpdate)
    {
        return new SecretsManagerSubscriptionUpdate(refined.Organization.UseSecretsManager,
            refined.Organization.SmSeats, refined.Organization.SmMaxAutoScaleSeats,
            refined.OccupiedPmSeats, refined.Invites.Count(x => x.AccessSecretsManager),
            passwordManagerSubscriptionUpdate);
    }
}
