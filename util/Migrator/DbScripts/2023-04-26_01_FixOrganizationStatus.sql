-- Update the [Status] of organizations from Pending (0) to Created (1) if they have at least 1 confirmed user
-- This is to fix an issue where the default value of 0 (Pending) was accidentally written back to the database
-- for existing organizations
UPDATE
    [dbo].[Organization]
SET
    [Status] = 1 -- Created
WHERE 
    [Status] = 0 -- Pending
    AND [Id] IN (
        SELECT DISTINCT 
            ou.[OrganizationId]
        FROM 
            [dbo].OrganizationUser ou
        WHERE 
            ou.[Status] = 2   -- confirmed
    )
GO