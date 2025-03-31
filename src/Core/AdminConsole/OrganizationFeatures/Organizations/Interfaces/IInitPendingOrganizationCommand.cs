﻿namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IInitPendingOrganizationCommand
{
    /// <summary>
    /// Accept an invitation to initialize and join an organization created via the Admin Portal
    /// </summary>
    Task InitPendingOrganizationAsync(Guid userId, Guid organizationId, Guid organizationUserId, string publicKey, string privateKey, string collectionName);
}
