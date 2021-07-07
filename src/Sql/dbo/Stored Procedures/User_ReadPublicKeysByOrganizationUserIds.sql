CREATE PROCEDURE [dbo].[User_ReadPublicKeysByOrganizationUserIds]
    @OrganizationId UNIQUEIDENTIFIER,
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.[Id],
        OU.[UserId],
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
