CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByOrganizationIdEmail]
    @OrganizationId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @OnlyUsers BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUser] OU
    LEFT JOIN
        [dbo].[User] U ON OU.[UserId] = U.[Id]
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND (
            (@OnlyUsers = 0 AND (OU.[Email] = @Email OR U.[Email] = @Email))
            OR (@OnlyUsers = 1 AND U.[Email] = @Email)
        )
END