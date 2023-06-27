using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;

public class UpdateSecretsManagerSubscriptionCommand : IUpdateSecretsManagerSubscriptionCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IAdjustServiceAccountsCommand _adjustServiceAccountsCommand;
    private readonly IAdjustSeatsCommand _adjustSeatsCommand;
    private readonly IUpdateServiceAccountAutoscalingCommand _updateServiceAccountAutoscalingCommand;
    private readonly IUpdateSeatsAutoscalingCommand _updateSeatsAutoscalingCommand;

    public UpdateSecretsManagerSubscriptionCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        IAdjustServiceAccountsCommand adjustServiceAccountsCommand,
        IAdjustSeatsCommand adjustSeatsCommand,
        IUpdateServiceAccountAutoscalingCommand updateServiceAccountAutoscalingCommand,
        IUpdateSeatsAutoscalingCommand updateSeatsAutoscalingCommand)
    {
        _organizationRepository = organizationRepository;
        _adjustServiceAccountsCommand = adjustServiceAccountsCommand;
        _adjustSeatsCommand = adjustSeatsCommand;
        _updateServiceAccountAutoscalingCommand = updateServiceAccountAutoscalingCommand;
        _updateSeatsAutoscalingCommand = updateSeatsAutoscalingCommand;
    }

    public async Task UpdateSecretsManagerSubscription(OrganizationUpdate update)
    {
        var organization = await _organizationRepository.GetByIdAsync(update.OrganizationId);

        if (organization == null)
        {
            throw new NotFoundException();
        }

        var newSeatCount = organization.SmSeats.HasValue
            ? organization.SmSeats.Value + update.SeatAdjustment
            : update.SeatAdjustment;


        if (update.MaxAutoscaleSeats.HasValue && newSeatCount > update.MaxAutoscaleSeats.Value)
        {
            throw new BadRequestException("Cannot set max seat autoscaling below seat count.");
        }

        var newServiceAccountCount = organization.SmServiceAccounts.HasValue
            ? organization.SmServiceAccounts + update.ServiceAccountsAdjustment
            : update.ServiceAccountsAdjustment;

        if (update.MaxAutoscaleServiceAccounts.HasValue && newServiceAccountCount > update.MaxAutoscaleServiceAccounts.Value)
        {
            throw new BadRequestException("Cannot set max service account autoscaling below service account count.");
        }

        if (update.SeatAdjustment != 0)
        {
            await _adjustSeatsCommand.AdjustSeatsAsync(organization, update.SeatAdjustment);
        }

        if (update.ServiceAccountsAdjustment != 0)
        {
            await _adjustServiceAccountsCommand.AdjustServiceAccountsAsync(organization, update.ServiceAccountsAdjustment.GetValueOrDefault());
        }

        if (update.MaxAutoscaleSeats != organization.MaxAutoscaleSmSeats)
        {
            await _updateSeatsAutoscalingCommand.UpdateSeatsAutoscalingAsync(organization, update.MaxAutoscaleSeats);
        }

        if (update.MaxAutoscaleServiceAccounts != organization.MaxAutoscaleSmServiceAccounts)
        {
            await _updateServiceAccountAutoscalingCommand.UpdateServiceAccountAutoscalingAsync(organization, update.MaxAutoscaleServiceAccounts);
        }
    }
}
