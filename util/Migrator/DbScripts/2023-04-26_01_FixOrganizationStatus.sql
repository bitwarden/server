-- Update the status of organizations from Pending to Created if they have no associated reseller provider with no active users.
UPDATE [dbo].[Organization]
SET [Status] = 1 -- Created
WHERE [Status] = 0 -- Pending
  AND [Id] NOT IN (
    SELECT po.[OrganizationId]
    FROM [dbo].[ProviderOrganization] po
    INNER JOIN [dbo].[Provider] p
        ON po.[ProviderId] = p.[Id]
    WHERE p.[Type] = 1 -- Reseller
        AND po.[OrganizationId] NOT IN (
            SELECT ou.[OrganizationId]
            FROM [dbo].OrganizationUser ou
            WHERE ou.[Status] > 0
        )
  )