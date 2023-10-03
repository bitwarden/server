CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadOwnerEmailAddressesById]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT
        U.[Email]
    FROM
        Organization AS O
        INNER JOIN OrganizationUser AS OU ON O.[Id] = OU.[OrganizationId]
        INNER JOIN [User] AS U ON OU.[UserId] = U.[Id]
    WHERE
        O.[Id] = @OrganizationId AND
        OU.[Type] = 0 AND -- Owner
        OU.[Status] = 2 -- Confirmed
END
GO
