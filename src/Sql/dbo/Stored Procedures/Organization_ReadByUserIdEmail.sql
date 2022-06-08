CREATE PROCEDURE [dbo].[Organization_ReadByUserIdEmail]
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR (256)
AS
BEGIN
    SET NOCOUNT ON

SELECT
    O.*
FROM
    [dbo].[OrganizationView] O
INNER JOIN
    [dbo].[OrganizationUserView] OU ON OU.[OrganizationId] = O.[Id]
WHERE
    OU.[UserId] = @UserId
    OR OU.[Email] = @Email -- Invited organizationUsers are not linked to a userId yet, we can only search by email
END
GO
