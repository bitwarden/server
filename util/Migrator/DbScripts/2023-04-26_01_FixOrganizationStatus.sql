-- Update the status of organizations from Pending to Created if they don't meet the criteria for Pending
UPDATE [dbo].[Organization]
SET [Status] = 1 -- Created
WHERE [Status] = 0 -- Pending
    AND [Id] NOT IN (
    -- Get all OrganizationIds where the Organization is
    -- * associated with a Reseller, and
    -- * doesn't have any confirmed users
    -- (this is the criteria for being in the Pending state)
        SELECT
            po.[OrganizationId]
        FROM 
            [dbo].[ProviderOrganization] po
        INNER JOIN 
            [dbo].[Provider] p ON po.[ProviderId] = p.[Id]
        WHERE 
            p.[Type] = 1 -- Reseller
            AND po.[OrganizationId] NOT IN (
                SELECT ou.[OrganizationId]
                FROM [dbo].OrganizationUser ou
                WHERE ou.[Status] > 0
            )
    )