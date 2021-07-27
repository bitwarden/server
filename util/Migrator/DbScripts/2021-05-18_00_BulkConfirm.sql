IF OBJECT_ID('[dbo].[User_ReadByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_ReadByIds]
END
GO

CREATE PROCEDURE [dbo].[User_ReadByIds]
@Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF (SELECT COUNT(1) FROM @Ids) < 1
        BEGIN
            RETURN(-1)
        END

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids)
END
GO

IF OBJECT_ID('[dbo].[User_ReadPublicKeysByOrganizationUserIds]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[User_ReadPublicKeysByOrganizationUserIds]
    END
GO

CREATE PROCEDURE [dbo].[User_ReadPublicKeysByOrganizationUserIds]
    @OrganizationId UNIQUEIDENTIFIER,
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.[Id],
        U.[PublicKey]
    FROM
        @OrganizationUserIds OUIDs
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OUIDs.Id = OU.Id AND OU.[Status] = 1 -- Accepted
    INNER JOIN
        [dbo].[User] U ON OU.UserId = U.Id
    WHERE
            OU.OrganizationId = @OrganizationId
END
