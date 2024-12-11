﻿using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class CountNewSmSeatsRequiredQuery : ICountNewSmSeatsRequiredQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public CountNewSmSeatsRequiredQuery(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository
    )
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
    }

    public async Task<int> CountNewSmSeatsRequiredAsync(Guid organizationId, int usersToAdd)
    {
        if (usersToAdd == 0)
        {
            return 0;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization does not use Secrets Manager");
        }

        if (!organization.SmSeats.HasValue)
        {
            return 0;
        }

        var occupiedSmSeats =
            await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(
                organization.Id
            );

        var availableSmSeats = organization.SmSeats.Value - occupiedSmSeats;

        if (availableSmSeats >= usersToAdd)
        {
            return 0;
        }

        return usersToAdd - availableSmSeats;
    }
}
