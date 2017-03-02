CREATE PROCEDURE [dbo].[Organization_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
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
        O.[Id] = @Id
        AND OU.[UserId] = @UserId
END