/* update the new column to have the value used in UseSso to preserve existing orgs ability */

UPDATE
    [dbo].[Organization]
SET
    [UseOrganizationDomains] = [UseSso]
WHERE
    [UseSso] = 1
GO
