CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadOwnerEmailAddressesById]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [U].[Email]
    FROM [User] AS [U]
    INNER JOIN [OrganizationUser] AS [OU] ON [U].[Id] = [OU].[UserId]
    WHERE
        [OU].[OrganizationId] = @OrganizationId AND
        [OU].[Type] = 0 AND -- Owner
        [OU].[Status] = 2 -- Confirmed
    GROUP BY [U].[Email]
END
GO
