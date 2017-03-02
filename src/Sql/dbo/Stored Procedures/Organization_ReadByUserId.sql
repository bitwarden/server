CREATE PROCEDURE [dbo].[Organization_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        O.*
    FROM
        [dbo].[OrganizationView] O
    INNER JOIN
        [dbo].[OrganizationUser] OU ON O.[Id] = OU.[OrganizationId]
    WHERE
        OU.[UserId] = @UserId
END