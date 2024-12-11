﻿using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;

/// <summary>
/// This is only for adding SM to an existing organization
/// </summary>
public interface IAddSecretsManagerSubscriptionCommand
{
    Task SignUpAsync(
        Organization organization,
        int additionalSmSeats,
        int additionalServiceAccounts
    );
}
