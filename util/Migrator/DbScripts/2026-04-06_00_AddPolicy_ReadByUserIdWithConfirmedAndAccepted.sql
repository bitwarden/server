IF OBJECT_ID('[dbo].[Policy_ReadByUserIdWithConfirmedAndAccepted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_ReadByUserIdWithConfirmedAndAccepted]
END
GO

CREATE PROCEDURE [dbo].[Policy_ReadByUserIdWithConfirmedAndAccepted]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        P.*
    FROM
        [dbo].[PolicyView] P
    INNER JOIN
        [dbo].[OrganizationUser] OU ON P.[OrganizationId] = OU.[OrganizationId]
    INNER JOIN
        [dbo].[Organization] O ON OU.[OrganizationId] = O.[Id]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Status] IN (1, 2) -- 1 = Accepted, 2 = Confirmed
END
GO