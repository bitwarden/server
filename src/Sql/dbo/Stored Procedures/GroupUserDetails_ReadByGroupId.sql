CREATE PROCEDURE [dbo].[GroupUserDetails_ReadByGroupId]
    @GroupId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.[Id] AS [OrganizationUserId],
        OU.[AccessAll],
        U.[Name],
        ISNULL(U.[Email], OU.[Email]) Email,
        OU.[Status],
        OU.[Type]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id]
    INNER JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId]
    WHERE
        GU.[GroupId] = @GroupId
END
